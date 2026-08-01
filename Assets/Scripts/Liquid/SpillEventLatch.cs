using TMPro;
using UnityEngine;

/// <summary>
/// Tracks the result of one robotic-arm liquid-handling trial.
///
/// The component maintains:
/// - the current live spill-risk index;
/// - a cumulative trial-risk index;
/// - the peak live risk;
/// - the minimum predicted freeboard margin;
/// - elapsed trial time.
///
/// A spill prediction remains latched until ResetTrial() is called.
///
/// Important:
/// The cumulative value is an engineering risk index, not a
/// statistically calibrated probability of a real spill.
/// </summary>
[DefaultExecutionOrder(350)]
public sealed class SpillEventLatch : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private SpillRiskCalculator spillRiskCalculator;

    [SerializeField]
    private TMP_Text trialStatusText;

    [Header("Immediate spill detection")]

    [Tooltip(
        "Immediately latch when the live spill-risk index " +
        "reaches this value.")]
    [Range(0.5f, 1.0f)]
    [SerializeField]
    private float immediateRiskThreshold = 1.0f;

    [Tooltip(
        "Immediately latch when the predicted liquid surface " +
        "reaches or exceeds the beaker rim.")]
    [SerializeField]
    private bool latchOnRimCrossing = true;

    [Header("Cumulative trial risk")]

    [Tooltip(
        "Live risk below this value does not add to the " +
        "cumulative trial-risk index.")]
    [Range(0.0f, 0.9f)]
    [SerializeField]
    private float accumulationDeadZone = 0.25f;

    [Tooltip(
        "Higher values make high live-risk values contribute " +
        "much more strongly than moderate values.")]
    [Range(1.0f, 5.0f)]
    [SerializeField]
    private float riskWeightPower = 2.0f;

    [Tooltip(
        "Approximate time at maximum live risk required to " +
        "build a strong cumulative risk result.")]
    [Min(0.1f)]
    [SerializeField]
    private float accumulationTimeConstantSeconds = 2.0f;

    [Tooltip(
        "Latch the trial when cumulative trial risk reaches " +
        "this value.")]
    [Range(0.5f, 1.0f)]
    [SerializeField]
    private float cumulativeLatchThreshold = 0.85f;

    [Header("Display")]

    [Tooltip(
        "Update the display this many times per second.")]
    [Range(1.0f, 30.0f)]
    [SerializeField]
    private float displayUpdateRateHertz = 10.0f;

    public bool SpillLatched { get; private set; }

    public float PeakLiveRisk { get; private set; }

    public float CumulativeTrialRisk { get; private set; }

    public float MinimumFreeboardMarginMetres
    {
        get;
        private set;
    } = float.PositiveInfinity;

    public float TrialElapsedSeconds { get; private set; }

    public float LatchTimeSeconds { get; private set; } = -1.0f;

    private float accumulatedExposure;
    private float nextDisplayUpdateTime;
    private string latchReason = string.Empty;

    private void Start()
    {
        ResetTrial();
    }

    private void Update()
    {
        TrialElapsedSeconds += Time.deltaTime;

        if (spillRiskCalculator != null &&
            spillRiskCalculator.HasValidResult)
        {
            ProcessCurrentRisk();
        }

        if (Time.unscaledTime >= nextDisplayUpdateTime)
        {
            RefreshDisplay();

            nextDisplayUpdateTime =
                Time.unscaledTime +
                1.0f /
                Mathf.Max(
                    displayUpdateRateHertz,
                    1.0f);
        }
    }

    private void ProcessCurrentRisk()
    {
        float liveRisk =
            Mathf.Clamp01(
                spillRiskCalculator.SpillRiskIndex);

        float freeboardMargin =
            spillRiskCalculator
                .PredictedFreeboardMargin;

        PeakLiveRisk =
            Mathf.Max(
                PeakLiveRisk,
                liveRisk);

        MinimumFreeboardMarginMetres =
            Mathf.Min(
                MinimumFreeboardMarginMetres,
                freeboardMargin);

        UpdateCumulativeRisk(liveRisk);

        if (SpillLatched)
        {
            return;
        }

        bool rimReached =
            latchOnRimCrossing &&
            freeboardMargin <= 0.0f;

        bool immediateRiskReached =
            liveRisk >= immediateRiskThreshold;

        bool cumulativeRiskReached =
            CumulativeTrialRisk >=
            cumulativeLatchThreshold;

        if (rimReached)
        {
            LatchSpill(
                "Predicted liquid surface reached the rim");
        }
        else if (immediateRiskReached)
        {
            LatchSpill(
                "Immediate spill-risk limit reached");
        }
        else if (cumulativeRiskReached)
        {
            LatchSpill(
                "Sustained high-risk motion detected");
        }
    }

    /// <summary>
    /// Builds a monotonic cumulative risk index.
    ///
    /// Risk below the dead zone contributes nothing.
    /// Risk above it is normalised, weighted and integrated.
    ///
    /// The exponential conversion keeps the result between
    /// zero and one:
    ///
    /// cumulativeRisk = 1 - exp(-accumulatedExposure)
    /// </summary>
    private void UpdateCumulativeRisk(float liveRisk)
    {
        float usableRange =
            Mathf.Max(
                1.0f - accumulationDeadZone,
                0.0001f);

        float normalisedRisk =
            Mathf.Clamp01(
                (liveRisk - accumulationDeadZone) /
                usableRange);

        float weightedRisk =
            Mathf.Pow(
                normalisedRisk,
                riskWeightPower);

        float timeConstant =
            Mathf.Max(
                accumulationTimeConstantSeconds,
                0.1f);

        accumulatedExposure +=
            weightedRisk *
            Time.deltaTime /
            timeConstant;

        CumulativeTrialRisk =
            1.0f -
            Mathf.Exp(
                -accumulatedExposure);

        CumulativeTrialRisk =
            Mathf.Clamp01(
                CumulativeTrialRisk);
    }

    private void LatchSpill(string reason)
    {
        SpillLatched = true;
        latchReason = reason;
        LatchTimeSeconds = TrialElapsedSeconds;

        RefreshDisplay();

        Debug.LogWarning(
            "MODELLED SPILL PREDICTED: " +
            latchReason,
            this);
    }

    /// <summary>
    /// Clears all stored trial results and starts a new trial.
    /// Keep this connected to the existing Reset button.
    /// </summary>
    public void ResetTrial()
    {
        SpillLatched = false;

        PeakLiveRisk = 0.0f;
        CumulativeTrialRisk = 0.0f;

        MinimumFreeboardMarginMetres =
            float.PositiveInfinity;

        TrialElapsedSeconds = 0.0f;
        LatchTimeSeconds = -1.0f;

        accumulatedExposure = 0.0f;
        latchReason = string.Empty;

        nextDisplayUpdateTime = 0.0f;

        RefreshDisplay();

        Debug.Log(
            "Trial spill-risk history reset.",
            this);
    }

    private void RefreshDisplay()
    {
        if (trialStatusText == null)
        {
            return;
        }

        float liveRisk = 0.0f;

        if (spillRiskCalculator != null &&
            spillRiskCalculator.HasValidResult)
        {
            liveRisk =
                Mathf.Clamp01(
                    spillRiskCalculator
                        .SpillRiskIndex);
        }

        if (SpillLatched)
        {
            float minimumMarginMillimetres =
                float.IsPositiveInfinity(
                    MinimumFreeboardMarginMetres)
                    ? 0.0f
                    : MinimumFreeboardMarginMetres *
                      1000.0f;

            trialStatusText.text =
                "<b><color=#FF5555>" +
                "MODELLED SPILL PREDICTED" +
                "</color></b>\n" +
                $"{latchReason}\n" +
                $"Time: {LatchTimeSeconds:F1} s   " +
                $"Peak: {PeakLiveRisk:P0}\n" +
                $"Accumulated risk: " +
                $"{CumulativeTrialRisk:P0}   " +
                $"Min margin: " +
                $"{minimumMarginMillimetres:F1} mm";

            return;
        }

        string riskColour =
            GetRiskColour(
                CumulativeTrialRisk);

        trialStatusText.text =
            "<b><color=#59FF6A>" +
            "TRIAL ACTIVE — NO SPILL" +
            "</color></b>\n" +
            $"Live risk: {liveRisk:P0}   " +
            $"Peak: {PeakLiveRisk:P0}\n" +
            $"Accumulated risk: " +
            $"<color={riskColour}>" +
            $"{CumulativeTrialRisk:P0}" +
            "</color>   " +
            $"Time: {TrialElapsedSeconds:F1} s";
    }

    private static string GetRiskColour(
        float risk)
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