using UnityEngine;

/// <summary>
/// Calculates geometric freeboard margin and an
/// uncalibrated 0-1 spill-risk index.
///
/// This component is outside both estimators, ensuring
/// that the classical and neural models use exactly the
/// same risk calculation.
/// </summary>
[DefaultExecutionOrder(200)]
public sealed class SpillRiskCalculator :
    MonoBehaviour
{
    [SerializeField]
    private BeakerGeometry geometry;

    [SerializeField]
    private LiquidStateProvider stateProvider;

    public bool HasValidResult
    {
        get;
        private set;
    }

    /// <summary>
    /// Static liquid height available above the
    /// undisturbed surface, in metres.
    /// </summary>
    public float StaticFreeboard
    {
        get;
        private set;
    }

    /// <summary>
    /// Rise at the highest wall point based on the
    /// current surface slope.
    /// </summary>
    public float CurrentRimRise
    {
        get;
        private set;
    }

    /// <summary>
    /// Maximum of current and one-natural-timescale-ahead
    /// estimated rim rise.
    /// </summary>
    public float PredictedRimRise
    {
        get;
        private set;
    }

    public float CurrentFreeboardMargin
    {
        get;
        private set;
    }

    public float PredictedFreeboardMargin
    {
        get;
        private set;
    }

    /// <summary>
    /// Dimensionless risk index:
    ///
    /// 0 = level liquid
    /// 1 = predicted surface reaches or exceeds rim
    ///
    /// This is not a statistically calibrated probability.
    /// </summary>
    [field: SerializeField]
    [field: Range(0.0f, 1.0f)]
    public float SpillRiskIndex
    {
        get;
        private set;
    }

    private void Update()
    {
        if (geometry == null ||
            stateProvider == null ||
            !stateProvider.HasValidState)
        {
            HasValidResult = false;

            SpillRiskIndex = 0.0f;

            return;
        }

        LiquidState state =
            stateProvider.CurrentState;

        float radius =
            geometry.InnerRadius;

        StaticFreeboard =
            geometry.Freeboard;

        float omega =
            CylindricalSloshMath
                .NaturalAngularFrequency(
                    radius,
                    geometry.LiquidDepth);

        /*
         * Predict approximately one natural timescale
         * forward without introducing an arbitrary
         * time parameter.
         */
        Vector2 predictedSlope =
            state.SurfaceSlope +
            state.SurfaceSlopeVelocity /
            Mathf.Max(omega, 0.001f);

        float currentSlopeMagnitude =
            state.SurfaceSlope.magnitude;

        float predictedSlopeMagnitude =
            Mathf.Max(
                currentSlopeMagnitude,
                predictedSlope.magnitude);

        CurrentRimRise =
            radius *
            currentSlopeMagnitude;

        PredictedRimRise =
            radius *
            predictedSlopeMagnitude;

        CurrentFreeboardMargin =
            StaticFreeboard -
            CurrentRimRise;

        PredictedFreeboardMargin =
            StaticFreeboard -
            PredictedRimRise;

        if (StaticFreeboard <= 0.000001f)
        {
            SpillRiskIndex = 1.0f;
        }
        else
        {
            SpillRiskIndex =
                Mathf.Clamp01(
                    PredictedRimRise /
                    StaticFreeboard);
        }

        HasValidResult = true;
    }
}