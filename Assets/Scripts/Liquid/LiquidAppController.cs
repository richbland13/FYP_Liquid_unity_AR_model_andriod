using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the liquid-model demonstration UI.
///
/// Supports:
/// - real-time fill-fraction adjustment;
/// - switching between the classical and ONNX estimators;
/// - fixed geometry and fill-range protection for the ONNX model;
/// - restoration of the previous classical geometry;
/// - resetting both estimators;
/// - live spill-risk display;
/// - ONNX model status and performance information.
///
/// PlaceholderNeuralLiquidEstimator is no longer required.
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
    private OnnxLiquidEstimator onnxEstimator;

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

    [Header("ONNX training envelope")]

    [Tooltip(
        "The internal beaker radius used to train the ONNX model.")]
    [Min(0.005f)]
    [SerializeField]
    private float trainedInnerRadius = 0.032f;

    [Tooltip(
        "The internal beaker height used to train the ONNX model.")]
    [Min(0.020f)]
    [SerializeField]
    private float trainedInternalHeight = 0.120f;

    [Tooltip(
        "Minimum fill fraction represented in the ONNX training data.")]
    [Range(0.05f, 0.95f)]
    [SerializeField]
    private float minimumTrainedFillFraction = 0.30f;

    [Tooltip(
        "Maximum fill fraction represented in the ONNX training data.")]
    [Range(0.05f, 0.95f)]
    [SerializeField]
    private float maximumTrainedFillFraction = 0.80f;

    [Header("ONNX metadata")]

    [SerializeField]
    private string neuralModelDisplayName =
        "ONNX test CNN";

    [Min(1)]
    [SerializeField]
    private int neuralWindowSamples = 50;

    [Min(1.0f)]
    [SerializeField]
    private float neuralInputRateHertz = 50.0f;

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

    /*
     * These values store the user's classical configuration
     * while the ONNX model temporarily applies its fixed
     * training geometry.
     */
    private bool hasSavedClassicalGeometry;

    private float savedClassicalRadius;
    private float savedClassicalHeight;
    private float savedClassicalFillFraction;

    private void Start()
    {
        if (fillFractionSlider != null)
        {
            fillFractionSlider.onValueChanged.AddListener(
                OnFillFractionChanged);
        }

        if (classicalButton != null)
        {
            classicalButton.onClick.AddListener(
                UseClassicalModel);
        }

        if (neuralButton != null)
        {
            neuralButton.onClick.AddListener(
                UseOnnxModel);
        }

        /*
         * The application should normally begin in classical
         * mode, so preserve its starting geometry.
         */
        if (estimatorRouter != null &&
            estimatorRouter.Mode ==
            LiquidEstimatorRouter.EstimatorMode.Classical)
        {
            SaveClassicalGeometry();
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
            classicalButton.onClick.RemoveListener(
                UseClassicalModel);
        }

        if (neuralButton != null)
        {
            neuralButton.onClick.RemoveListener(
                UseOnnxModel);
        }
    }

    public void OnFillFractionChanged(float value)
    {
        if (suppressFillCallback ||
            geometry == null)
        {
            return;
        }

        geometry.SetFillFraction(value);

        /*
         * Remember live fill changes made while classical mode
         * is active, so the value is restored after ONNX mode.
         */
        if (IsClassicalMode())
        {
            savedClassicalFillFraction =
                geometry.FillFraction;

            hasSavedClassicalGeometry = true;
        }

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

        RestoreClassicalGeometry();

        ConfigureForCurrentMode();
        ResetLiquidState();
        RefreshAllUi();
    }

    public void UseOnnxModel()
    {
        if (estimatorRouter == null ||
            onnxEstimator == null)
        {
            SetModelInformation(
                "<b>ONNX estimator unavailable</b>\n" +
                "Assign OnnxLiquidEstimator in " +
                "LiquidAppController.");

            return;
        }

        /*
         * Capture the current user-selected classical
         * configuration before overwriting it.
         */
        if (IsClassicalMode())
        {
            SaveClassicalGeometry();
        }

        ApplyOnnxTrainingGeometry();

        bool switched =
            estimatorRouter.TryUseNeuralEstimator();

        if (!switched)
        {
            RestoreClassicalGeometry();

            SetModelInformation(
                "<b>ONNX estimator unavailable</b>\n" +
                "Assign OnnxLiquidEstimator as the router's " +
                "Neural Estimator.");

            return;
        }

        ConfigureForCurrentMode();
        ResetLiquidState();
        RefreshAllUi();
    }

    /*
     * Kept as a compatibility alias in case an older Unity
     * button event was manually connected to this method.
     */
    public void UsePlaceholderNeuralModel()
    {
        UseOnnxModel();
    }

    /// <summary>
    /// Resets both estimator states.
    ///
    /// The Reset button can continue calling this method.
    /// </summary>
    public void ResetLiquidState()
    {
        if (classicalEstimator != null)
        {
            classicalEstimator.ResetEstimator();
        }

        if (onnxEstimator != null)
        {
            onnxEstimator.ResetEstimator();
        }
    }

    private void SaveClassicalGeometry()
    {
        if (geometry == null)
        {
            return;
        }

        savedClassicalRadius =
            geometry.InnerRadius;

        savedClassicalHeight =
            geometry.InternalHeight;

        savedClassicalFillFraction =
            geometry.FillFraction;

        hasSavedClassicalGeometry = true;
    }

    private void RestoreClassicalGeometry()
    {
        if (geometry == null ||
            !hasSavedClassicalGeometry)
        {
            return;
        }

        geometry.SetInnerRadius(
            savedClassicalRadius);

        geometry.SetInternalHeight(
            savedClassicalHeight);

        geometry.SetFillFraction(
            Mathf.Clamp(
                savedClassicalFillFraction,
                classicalMinimumFillFraction,
                classicalMaximumFillFraction));
    }

    private void ApplyOnnxTrainingGeometry()
    {
        if (geometry == null)
        {
            return;
        }

        geometry.SetInnerRadius(
            trainedInnerRadius);

        geometry.SetInternalHeight(
            trainedInternalHeight);

        geometry.SetFillFraction(
            Mathf.Clamp(
                geometry.FillFraction,
                minimumTrainedFillFraction,
                maximumTrainedFillFraction));
    }

    private void ConfigureForCurrentMode()
    {
        if (geometry == null ||
            estimatorRouter == null)
        {
            return;
        }

        bool neuralMode =
            estimatorRouter.Mode ==
            LiquidEstimatorRouter
                .EstimatorMode.NeuralNetwork;

        float minimumFill =
            neuralMode
                ? minimumTrainedFillFraction
                : classicalMinimumFillFraction;

        float maximumFill =
            neuralMode
                ? maximumTrainedFillFraction
                : classicalMaximumFillFraction;

        float clampedFill =
            Mathf.Clamp(
                geometry.FillFraction,
                minimumFill,
                maximumFill);

        geometry.SetFillFraction(clampedFill);

        if (fillFractionSlider != null)
        {
            suppressFillCallback = true;

            fillFractionSlider.minValue =
                minimumFill;

            fillFractionSlider.maxValue =
                maximumFill;

            fillFractionSlider.value =
                clampedFill;

            suppressFillCallback = false;
        }
    }

    private bool IsClassicalMode()
    {
        return
            estimatorRouter != null &&
            estimatorRouter.Mode ==
            LiquidEstimatorRouter
                .EstimatorMode.Classical;
    }

    private bool OnnxGeometryIsValid()
    {
        if (geometry == null)
        {
            return false;
        }

        const float toleranceMetres =
            0.0001f;

        bool radiusValid =
            Mathf.Abs(
                geometry.InnerRadius -
                trainedInnerRadius) <=
            toleranceMetres;

        bool heightValid =
            Mathf.Abs(
                geometry.InternalHeight -
                trainedInternalHeight) <=
            toleranceMetres;

        bool fillValid =
            geometry.FillFraction >=
                minimumTrainedFillFraction &&
            geometry.FillFraction <=
                maximumTrainedFillFraction;

        return
            radiusValid &&
            heightValid &&
            fillValid;
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
        if (fillFractionLabel == null ||
            geometry == null)
        {
            return;
        }

        fillFractionLabel.text =
            $"Fill fraction: " +
            $"{geometry.FillFraction:P0}";
    }

    private void UpdateRiskDisplay()
    {
        float risk =
            spillRiskCalculator != null &&
            spillRiskCalculator.HasValidResult
                ? spillRiskCalculator.SpillRiskIndex
                : 0.0f;

        risk =
            Mathf.Clamp01(risk);

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
            label =
                risk >= 1.0f
                    ? "RIM REACHED"
                    : "HIGH";

            colour = highRiskColour;
        }

        if (riskFillImage != null)
        {
            riskFillImage.color = colour;
        }

        if (riskLabel != null)
        {
            riskLabel.text =
                $"Spill risk: {risk:P0} — {label}";

            riskLabel.color = colour;
        }
    }

    private void UpdateModelInformation()
    {
        if (modelInformationText == null ||
            estimatorRouter == null)
        {
            return;
        }

        if (IsClassicalMode())
        {
            SetModelInformation(
                "<b>Estimator: Classical</b>\n" +
                "2-axis spring-damper\n" +
                "Geometry: adjustable\n" +
                "Input: Nicla IMU / Vuforia fallback");

            return;
        }

        string modelStatus;

        if (onnxEstimator == null)
        {
            modelStatus =
                "Estimator component not assigned";
        }
        else if (!onnxEstimator.ModelLoaded)
        {
            modelStatus =
                "Model not loaded";
        }
        else if (!onnxEstimator.HasValidState)
        {
            modelStatus =
                $"Window: " +
                $"{onnxEstimator.SamplesInWindow}/" +
                $"{onnxEstimator.RequiredWindowSamples}";
        }
        else
        {
            modelStatus =
                $"Running: " +
                $"{onnxEstimator.LastInferenceMilliseconds:F2} ms";
        }

        string geometryStatus =
            OnnxGeometryIsValid()
                ? "Geometry: valid"
                : "Geometry: OUT OF RANGE";

        float windowSeconds =
            neuralWindowSamples /
            Mathf.Max(
                neuralInputRateHertz,
                1.0f);

        SetModelInformation(
            $"<b>Estimator: {neuralModelDisplayName}</b>\n" +
            $"{modelStatus}\n" +
            $"Window: {windowSeconds:F1} s " +
            $"at {neuralInputRateHertz:F0} Hz\n" +
            $"R: {trainedInnerRadius * 1000.0f:F0} mm, " +
            $"H: {trainedInternalHeight * 1000.0f:F0} mm\n" +
            $"Fill: {minimumTrainedFillFraction:P0}–" +
            $"{maximumTrainedFillFraction:P0}\n" +
            geometryStatus);
    }

    private void UpdateButtonInteractability()
    {
        if (estimatorRouter == null)
        {
            return;
        }

        bool classicalMode =
            IsClassicalMode();

        if (classicalButton != null)
        {
            classicalButton.interactable =
                !classicalMode;
        }

        if (neuralButton != null)
        {
            neuralButton.interactable =
                classicalMode &&
                onnxEstimator != null &&
                estimatorRouter.NeuralEstimatorAvailable;
        }
    }

    private void SetModelInformation(
        string message)
    {
        if (modelInformationText != null)
        {
            modelInformationText.text =
                message;
        }
    }

    private void OnValidate()
    {
        classicalMaximumFillFraction =
            Mathf.Max(
                classicalMinimumFillFraction,
                classicalMaximumFillFraction);

        maximumTrainedFillFraction =
            Mathf.Max(
                minimumTrainedFillFraction,
                maximumTrainedFillFraction);
    }
}