using TMPro;
using UnityEngine;

/// <summary>
/// Displays a compact summary of the active liquid model,
/// vessel state and shared spill-risk calculation.
///
/// This display works with either the classical estimator
/// or the future neural-network estimator through the
/// LiquidEstimatorRouter.
/// </summary>
[DefaultExecutionOrder(300)]
public sealed class LiquidStatusDisplay : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private LiquidEstimatorRouter estimatorRouter;

    [SerializeField]
    private SpillRiskCalculator spillRiskCalculator;

    [SerializeField]
    private BeakerGeometry geometry;

    [SerializeField]
    private TMP_Text statusText;

    [Header("Display")]

    [SerializeField]
    private bool showFillFraction = true;

    [SerializeField]
    private bool showSloshRate = true;

    [SerializeField]
    private bool showFreeboardMargin = true;

    private void Update()
    {
        if (statusText == null)
        {
            return;
        }

        if (estimatorRouter == null ||
            !estimatorRouter.HasValidState)
        {
            statusText.text =
                "<b>LIQUID MODEL</b>\n" +
                "Waiting for motion input...";

            return;
        }

        LiquidState state =
            estimatorRouter.CurrentState;

        float surfaceTiltDegrees =
            CylindricalSloshMath.SurfaceTiltDegrees(
                state.SurfaceSlope);

        float sloshRate =
            state.SurfaceSlopeVelocity.magnitude;

        string modelName =
            GetModelName();

        string inputName =
            GetInputName();

        string message =
            "<b>LIQUID MODEL</b>\n" +
            $"Model: {modelName}\n" +
            $"Input: {inputName}\n";

        if (showFillFraction &&
            geometry != null)
        {
            message +=
                $"Fill: {geometry.FillFraction:P0}\n";
        }

        message += "\n";

        message +=
            $"Surface tilt: {surfaceTiltDegrees:F1}°\n";

        if (showSloshRate)
        {
            message +=
                $"Slosh rate: {sloshRate:F2} /s\n";
        }

        if (spillRiskCalculator != null &&
            spillRiskCalculator.HasValidResult)
        {
            if (showFreeboardMargin)
            {
                float marginMillimetres =
                    spillRiskCalculator
                        .PredictedFreeboardMargin *
                    1000.0f;

                message +=
                    $"Freeboard: {marginMillimetres:F1} mm\n";
            }

            float risk =
                spillRiskCalculator.SpillRiskIndex;

            string riskLabel =
                GetRiskLabel(risk);

            string riskColour =
                GetRiskColour(risk);

            message +=
                $"Spill risk: " +
                $"<color={riskColour}>" +
                $"{risk:P0} — {riskLabel}" +
                "</color>";
        }
        else
        {
            message +=
                "Spill risk: unavailable";
        }

        statusText.text = message;
    }

    private string GetModelName()
    {
        if (estimatorRouter == null)
        {
            return "None";
        }

        switch (estimatorRouter.Mode)
        {
            case LiquidEstimatorRouter
                .EstimatorMode.Classical:

                return "Classical";

            case LiquidEstimatorRouter
                .EstimatorMode.NeuralNetwork:

                return "Neural network";

            default:

                return "Unknown";
        }
    }

    private string GetInputName()
    {
        if (estimatorRouter == null)
        {
            return "None";
        }

        LiquidStateProvider activeProvider =
            estimatorRouter.ActiveProvider;

        if (activeProvider is
            ClassicalLiquidEstimator classical)
        {
            switch (classical.CurrentInputSource)
            {
                case ClassicalLiquidEstimator
                    .InputSource.NiclaImu:

                    return "Nicla IMU";

                case ClassicalLiquidEstimator
                    .InputSource.VuforiaFallback:

                    return "Vuforia fallback";

                default:

                    return "No input";
            }
        }

        if (estimatorRouter.Mode ==
            LiquidEstimatorRouter
                .EstimatorMode.NeuralNetwork)
        {
            return "Nicla IMU window";
        }

        return activeProvider != null
            ? activeProvider.GetType().Name
            : "None";
    }

    private static string GetRiskLabel(float risk)
    {
        if (risk < 0.35f)
        {
            return "LOW";
        }

        if (risk < 0.70f)
        {
            return "MODERATE";
        }

        if (risk < 1.0f)
        {
            return "HIGH";
        }

        return "RIM REACHED";
    }

    private static string GetRiskColour(float risk)
    {
        if (risk < 0.35f)
        {
            return "#59FF6A";
        }

        if (risk < 0.70f)
        {
            return "#FFD84A";
        }

        return "#FF5555";
    }
}