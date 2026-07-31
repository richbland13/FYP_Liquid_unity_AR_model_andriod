using UnityEngine;

/// <summary>
/// Two-axis first-mode classical liquid estimator.
///
/// State:
/// surfaceSlope
/// surfaceSlopeVelocity
///
/// Dynamics:
///
/// slope' = velocity
///
/// velocity' =
///     omega^2 * (equilibriumSlope - slope)
///     - 2*zeta*omega*velocity
///
/// The Nicla accelerometer supplies the primary equilibrium
/// surface direction. Vuforia orientation is the fallback.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class ClassicalLiquidEstimator :
    LiquidStateProvider
{
    public enum InputSource
    {
        None,
        NiclaImu,
        VuforiaFallback
    }

    private const float MinimumAccelerationMagnitudeG =
        0.15f;

    private const float MaximumIntegrationStepSeconds =
        1.0f / 240.0f;

    private const float MaximumFrameTimeSeconds =
        0.10f;

    [Header("References")]

    [SerializeField]
    private BeakerGeometry geometry;

    [SerializeField]
    private NiclaImuMotionProvider imuMotionProvider;

    [Tooltip(
        "Transform whose local axes define the beaker frame.")]
    [SerializeField]
    private Transform beakerAnchor;

    [Header("Fallback")]

    [SerializeField]
    private bool useVuforiaFallback = true;

    /*
     * Backing fields solve the read-only override problem.
     */
    private LiquidState currentState =
        LiquidState.Level;

    private bool hasValidState;

    public override LiquidState CurrentState =>
        currentState;

    public override bool HasValidState =>
        hasValidState;

    public InputSource CurrentInputSource
    {
        get;
        private set;
    }

    public Vector2 CurrentEquilibriumSlope
    {
        get;
        private set;
    }

    public float NaturalAngularFrequency
    {
        get;
        private set;
    }

    public float NaturalFrequencyHertz =>
        NaturalAngularFrequency /
        (2.0f * Mathf.PI);

    public float DampingRatio =>
        CylindricalSloshMath.WaterDampingRatio;

    private Vector2 surfaceSlope;
    private Vector2 surfaceSlopeVelocity;

    private bool stateInitialised;

    private void Update()
    {
        if (geometry == null)
        {
            hasValidState = false;

            CurrentInputSource =
                InputSource.None;

            return;
        }

        bool hasEquilibrium =
            TryGetEquilibriumSlope(
                out Vector2 equilibriumSlope,
                out InputSource inputSource);

        if (!hasEquilibrium)
        {
            CurrentInputSource =
                InputSource.None;

            /*
             * Keep the latest state instead of snapping
             * the liquid back to level.
             */
            hasValidState =
                stateInitialised;

            return;
        }

        CurrentInputSource =
            inputSource;

        CurrentEquilibriumSlope =
            equilibriumSlope;

        NaturalAngularFrequency =
            CylindricalSloshMath
                .NaturalAngularFrequency(
                    geometry.InnerRadius,
                    geometry.LiquidDepth);

        if (!stateInitialised)
        {
            /*
             * Start at equilibrium to avoid an artificial
             * oscillation when the application launches.
             */
            surfaceSlope =
                equilibriumSlope;

            surfaceSlopeVelocity =
                Vector2.zero;

            stateInitialised = true;
        }

        float remainingTime =
            Mathf.Min(
                Time.deltaTime,
                MaximumFrameTimeSeconds);

        while (remainingTime > 0.0f)
        {
            float step =
                Mathf.Min(
                    remainingTime,
                    MaximumIntegrationStepSeconds);

            IntegrateOneStep(
                equilibriumSlope,
                step);

            remainingTime -= step;
        }

        currentState =
            new LiquidState(
                surfaceSlope,
                surfaceSlopeVelocity);

        hasValidState = true;
    }

    private void IntegrateOneStep(
        Vector2 equilibriumSlope,
        float deltaTime)
    {
        float omega =
            NaturalAngularFrequency;

        float dampingRatio =
            CylindricalSloshMath
                .WaterDampingRatio;

        Vector2 slopeAcceleration =
            omega * omega *
            (equilibriumSlope - surfaceSlope)
            -
            2.0f *
            dampingRatio *
            omega *
            surfaceSlopeVelocity;

        /*
         * Semi-implicit Euler integration:
         * update velocity before position.
         */
        surfaceSlopeVelocity +=
            slopeAcceleration * deltaTime;

        surfaceSlope +=
            surfaceSlopeVelocity * deltaTime;

        float maximumSlope =
            Mathf.Tan(
                CylindricalSloshMath
                    .MaximumSurfaceTiltDegrees *
                Mathf.Deg2Rad);

        if (surfaceSlope.magnitude >
            maximumSlope)
        {
            surfaceSlope =
                Vector2.ClampMagnitude(
                    surfaceSlope,
                    maximumSlope);

            Vector2 outwardDirection =
                surfaceSlope.normalized;

            float outwardVelocity =
                Vector2.Dot(
                    surfaceSlopeVelocity,
                    outwardDirection);

            if (outwardVelocity > 0.0f)
            {
                surfaceSlopeVelocity -=
                    outwardVelocity *
                    outwardDirection;
            }
        }
    }

    private bool TryGetEquilibriumSlope(
        out Vector2 equilibriumSlope,
        out InputSource inputSource)
    {
        /*
         * Primary source: mapped Nicla acceleration.
         */
        if (imuMotionProvider != null &&
            imuMotionProvider.HasValidInput)
        {
            Vector3 acceleration =
                imuMotionProvider
                    .FilteredAccelerationBeakerG;

            if (acceleration.magnitude >=
                MinimumAccelerationMagnitudeG)
            {
                equilibriumSlope =
                    CylindricalSloshMath
                        .SurfaceSlopeFromNormal(
                            acceleration.normalized);

                inputSource =
                    InputSource.NiclaImu;

                return true;
            }
        }

        /*
         * Fallback: world-up direction expressed in the
         * local beaker coordinate frame.
         */
        if (useVuforiaFallback &&
            beakerAnchor != null)
        {
            Vector3 worldUpInBeakerFrame =
                beakerAnchor
                    .InverseTransformDirection(
                        Vector3.up)
                    .normalized;

            equilibriumSlope =
                CylindricalSloshMath
                    .SurfaceSlopeFromNormal(
                        worldUpInBeakerFrame);

            inputSource =
                InputSource.VuforiaFallback;

            return true;
        }

        equilibriumSlope =
            Vector2.zero;

        inputSource =
            InputSource.None;

        return false;
    }

    public void ResetEstimator()
    {
        surfaceSlope =
            Vector2.zero;

        surfaceSlopeVelocity =
            Vector2.zero;

        currentState =
            LiquidState.Level;

        CurrentEquilibriumSlope =
            Vector2.zero;

        stateInitialised = false;
        hasValidState = false;

        CurrentInputSource =
            InputSource.None;
    }
}