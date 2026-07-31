using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the final demonstration UI:
/// - real-time fill fraction;
/// - classical/placeholder-neural model switching;
/// - liquid-state reset;
/// - spill-risk bar;
/// - model metadata and geometry protection.
/// </summary>
[DefaultExecutionOrder(400)]
public sealed class LiquidAppController : MonoBehaviour
{
    [Header("Model references")]
    [SerializeField]
    private BeakerGeometry geometry;

    [SerializeField]
    private ClassicalLiquidEstimator classicalEstimator;

    [SerializeField]
    private PlaceholderNeuralLiquidEstimator placeholderNeuralEstimator;

    [SerializeField]
    private LiquidEstimatorRouter estimatorRouter;

    [SerializeField]
    private SpillRiskCalculator spillRiskCalculator;

    [Header("Fill controls")]
    [SerializeField]
    private Slider fillFractionSlider;

    [SerializeField]
    private TMP_Text fillFractionLabel;

    [Range(0.05f, 0.95f)]
    [SerializeField]
    private float classicalMinimumFillFraction = 0.10f;

    [Range(0.05f, 0.95f)]
    [SerializeField]
    private float classicalMaximumFillFraction = 0.90f;

    [Header("Mode controls")]
    [SerializeField]
    private Button classicalButton;

    [SerializeField]
    private Button neuralButton;

    [SerializeField]
    private TMP_Text modelInformationText;

    [Header("Risk display")]
    [SerializeField]
    private Slider riskBar;

    [SerializeField]
    private Image riskFillImage;

    [SerializeField]
    private TMP_Text riskLabel;

    [Header("Risk colours")]
    [SerializeField]
    private Color lowRiskColour =
        new Color(0.20f, 0.90f, 0.28f, 1.0f);

    [SerializeField]
    private Color moderateRiskColour =
        new Color(1.00f, 0.72f, 0.16f, 1.0f);

    [SerializeField]
    private Color highRiskColour =
        new Color(1.00f, 0.20f, 0.20f, 1.0f);

    private bool suppressFillCallback;

    private void Start()
    {
        if (fillFractionSlider != null)
        {
            fillFractionSlider.onValueChanged.AddListener(
                OnFillFractionChanged);
        }

        if (classicalButton != null)
        {
            classicalButton.onClick.AddListener(UseClassicalModel);
        }

        if (neuralButton != null)
        {
            neuralButton.onClick.AddListener(UsePlaceholderNeuralModel);
        }

        ConfigureForCurrentMode();
        RefreshAllUi();
    }

    private void Update()
    {
        UpdateRiskDisplay();
        UpdateModelInformation();
        UpdateButtonInteractability();
    }

    private void OnDestroy()
    {
        if (fillFractionSlider != null)
        {
            fillFractionSlider.onValueChanged.RemoveListener(
                OnFillFractionChanged);
        }

        if (classicalButton != null)
        {
            classicalButton.onClick.RemoveListener(UseClassicalModel);
        }

        if (neuralButton != null)
        {
            neuralButton.onClick.RemoveListener(UsePlaceholderNeuralModel);
        }
    }

    public void OnFillFractionChanged(float value)
    {
        if (suppressFillCallback || geometry == null)
        {
            return;
        }

        geometry.SetFillFraction(value);
        ResetLiquidState();
        RefreshFillLabel();
    }

    public void UseClassicalModel()
    {
        if (estimatorRouter == null)
        {
            return;
        }

        estimatorRouter.UseClassicalEstimator();
        ConfigureForCurrentMode();
        ResetLiquidState();
        RefreshAllUi();
    }

    public void UsePlaceholderNeuralModel()
    {
        if (estimatorRouter == null ||
            placeholderNeuralEstimator == null)
        {
            SetModelInformation("Neural placeholder is not assigned.");
            return;
        }

        placeholderNeuralEstimator.ApplyTrainedGeometry();

        if (!estimatorRouter.TryUseNeuralEstimator())
        {
            SetModelInformation(
                "Neural estimator is not assigned to LiquidEstimatorRouter.");
            return;
        }

        ConfigureForCurrentMode();
        ResetLiquidState();
        RefreshAllUi();
    }

    public void ResetLiquidState()
    {
        classicalEstimator?.ResetEstimator();
        placeholderNeuralEstimator?.ResetEstimator();
    }

    private void ConfigureForCurrentMode()
    {
        if (geometry == null || estimatorRouter == null)
        {
            return;
        }

        bool neuralMode =
            estimatorRouter.Mode ==
            LiquidEstimatorRouter.EstimatorMode.NeuralNetwork;

        float minimumFill =
            neuralMode && placeholderNeuralEstimator != null
                ? placeholderNeuralEstimator.MinimumTrainedFillFraction
                : classicalMinimumFillFraction;

        float maximumFill =
            neuralMode && placeholderNeuralEstimator != null
                ? placeholderNeuralEstimator.MaximumTrainedFillFraction
                : classicalMaximumFillFraction;

        float clampedFill = Mathf.Clamp(
            geometry.FillFraction,
            minimumFill,
            maximumFill);

        geometry.SetFillFraction(clampedFill);

        if (fillFractionSlider != null)
        {
            suppressFillCallback = true;
            fillFractionSlider.minValue = minimumFill;
            fillFractionSlider.maxValue = maximumFill;
            fillFractionSlider.value = clampedFill;
            suppressFillCallback = false;
        }
    }

    private void RefreshAllUi()
    {
        RefreshFillLabel();
        UpdateRiskDisplay();
        UpdateModelInformation();
        UpdateButtonInteractability();
    }

    private void RefreshFillLabel()
    {
        if (fillFractionLabel == null || geometry == null)
        {
            return;
        }

        fillFractionLabel.text =
            $"Fill fraction: {geometry.FillFraction:P0}";
    }

    private void UpdateRiskDisplay()
    {
        float risk =
            spillRiskCalculator != null &&
            spillRiskCalculator.HasValidResult
                ? spillRiskCalculator.SpillRiskIndex
                : 0.0f;

        if (riskBar != null)
        {
            riskBar.minValue = 0.0f;
            riskBar.maxValue = 1.0f;
            riskBar.value = risk;
            riskBar.interactable = false;
        }

        string label;
        Color colour;

        if (risk < 0.35f)
        {
            label = "LOW";
            colour = lowRiskColour;
        }
        else if (risk < 0.70f)
        {
            label = "MODERATE";
            colour = moderateRiskColour;
        }
        else
        {
            label = risk >= 1.0f ? "RIM REACHED" : "HIGH";
            colour = highRiskColour;
        }

        if (riskFillImage != null)
        {
            riskFillImage.color = colour;
        }

        if (riskLabel != null)
        {
            riskLabel.text = $"Spill risk: {risk:P0} — {label}";
            riskLabel.color = colour;
        }
    }

    private void UpdateModelInformation()
    {
        if (modelInformationText == null || estimatorRouter == null)
        {
            return;
        }

        if (estimatorRouter.Mode ==
            LiquidEstimatorRouter.EstimatorMode.Classical)
        {
            SetModelInformation(
                "<b>Estimator: Classical</b>\n" +
                "2-axis spring-damper\n" +
                "Geometry: adjustable\n" +
                "Input: IMU, Vuforia fallback");
            return;
        }

        if (placeholderNeuralEstimator == null)
        {
            SetModelInformation(
                "<b>Estimator: Neural</b>\nNo neural estimator assigned");
            return;
        }

        string geometryStatus =
            placeholderNeuralEstimator.GeometryMatchesTrainingEnvelope
                ? "Geometry: valid"
                : "Geometry: OUT OF RANGE";

        SetModelInformation(
            "<b>Estimator: Neural placeholder</b>\n" +
            "Not a trained AI model\n" +
            "Future model: temporal CNN, ONNX\n" +
            "Window: 1.0 s at 50 Hz\n" +
            $"R: {placeholderNeuralEstimator.TrainedInnerRadius * 1000.0f:F0} mm, " +
            $"H: {placeholderNeuralEstimator.TrainedInternalHeight * 1000.0f:F0} mm\n" +
            $"Fill range: {placeholderNeuralEstimator.MinimumTrainedFillFraction:P0}" +
            "–" +
            $"{placeholderNeuralEstimator.MaximumTrainedFillFraction:P0}\n" +
            geometryStatus);
    }

    private void UpdateButtonInteractability()
    {
        if (estimatorRouter == null)
        {
            return;
        }

        bool classicalMode =
            estimatorRouter.Mode ==
            LiquidEstimatorRouter.EstimatorMode.Classical;

        if (classicalButton != null)
        {
            classicalButton.interactable = !classicalMode;
        }

        if (neuralButton != null)
        {
            neuralButton.interactable =
                classicalMode &&
                placeholderNeuralEstimator != null &&
                estimatorRouter.NeuralEstimatorAvailable;
        }
    }

    private void SetModelInformation(string message)
    {
        if (modelInformationText != null)
        {
            modelInformationText.text = message;
        }
    }
}
