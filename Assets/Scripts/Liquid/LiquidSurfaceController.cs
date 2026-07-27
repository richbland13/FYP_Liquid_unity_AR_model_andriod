using TMPro;
using UnityEngine;

/// <summary>
/// Provides an initial quasi-static liquid model.
///
/// Assumptions:
/// - BeakerAnchor's local Y axis points upward through the beaker.
/// - BeakerAnchor's origin is at the centre of the beaker base.
/// - Unity world Y is aligned with gravity.
/// - Dynamic sloshing and translational acceleration are not yet modelled.
/// </summary>
public class LiquidSurfaceController : MonoBehaviour
{
    [Header("Scene references")]

    [SerializeField]
    private Transform beakerAnchor;

    [SerializeField]
    private Transform liquidSurface;

    [SerializeField]
    private TMP_Text liquidStatusText;

    [Header("Beaker geometry in metres")]

    [Min(0.001f)]
    [SerializeField]
    private float innerRadius = 0.032f;

    [Min(0.001f)]
    [SerializeField]
    private float internalHeight = 0.120f;

    [Range(0.05f, 0.95f)]
    [SerializeField]
    private float fillFraction = 0.60f;

    [Tooltip("Use this if the BeakerAnchor origin is not exactly at the base.")]
    [SerializeField]
    private float baseOffsetY = 0.0f;

    [Min(0.0001f)]
    [SerializeField]
    private float surfaceThickness = 0.002f;

    [Header("Visual response")]

    [Tooltip("Higher values make the liquid surface respond more quickly.")]
    [Min(0.1f)]
    [SerializeField]
    private float orientationResponse = 8.0f;

    [Header("Risk display")]

    [Range(0.0f, 1.0f)]
    [SerializeField]
    private float warningThreshold = 0.70f;

    public float SpillRiskIndex { get; private set; }

    private void Reset()
    {
        beakerAnchor = transform;
    }

    private void Awake()
    {
        if (beakerAnchor == null)
        {
            beakerAnchor = transform;
        }

        ApplyLiquidGeometry();
    }

    private void Update()
    {
        if (beakerAnchor == null || liquidSurface == null)
        {
            return;
        }

        ApplyLiquidGeometry();
        UpdateSurfaceOrientation();
        UpdateSpillRisk();
    }

    private void ApplyLiquidGeometry()
    {
        if (liquidSurface == null)
        {
            return;
        }

        float liquidHeight = internalHeight * fillFraction;

        liquidSurface.localPosition = new Vector3(
            0.0f,
            baseOffsetY + liquidHeight,
            0.0f);

        // Unity's built-in cylinder has:
        // diameter = 1 unit
        // height   = 2 units
        liquidSurface.localScale = new Vector3(
            innerRadius * 2.0f,
            surfaceThickness * 0.5f,
            innerRadius * 2.0f);
    }

    private void UpdateSurfaceOrientation()
    {
        // Express world-up in the coordinate system of the beaker.
        Vector3 gravityUpInBeaker =
            beakerAnchor.InverseTransformDirection(Vector3.up).normalized;

        // Rotate the surface's local up direction toward gravity-up.
        Quaternion desiredLocalRotation =
            Quaternion.FromToRotation(Vector3.up, gravityUpInBeaker);

        // Frame-rate-independent smoothing.
        float interpolation =
            1.0f - Mathf.Exp(-orientationResponse * Time.deltaTime);

        liquidSurface.localRotation = Quaternion.Slerp(
            liquidSurface.localRotation,
            desiredLocalRotation,
            interpolation);
    }

    private void UpdateSpillRisk()
    {
        float tiltDegrees = Vector3.Angle(
            beakerAnchor.up,
            Vector3.up);

        // Prevent tan(90 degrees) from becoming unbounded.
        float boundedTiltDegrees = Mathf.Min(tiltDegrees, 89.0f);
        float tiltRadians = boundedTiltDegrees * Mathf.Deg2Rad;

        float headspace =
            internalHeight * (1.0f - fillFraction);

        // Approximate rise of the liquid surface at the elevated rim.
        float rimRise =
            innerRadius * Mathf.Tan(tiltRadians);

        SpillRiskIndex = Mathf.Clamp01(
            rimRise / Mathf.Max(headspace, 0.001f));

        if (liquidStatusText == null)
        {
            return;
        }

        string riskDescription;

        if (SpillRiskIndex >= 1.0f)
        {
            riskDescription = "SPILL LIKELY";
            liquidStatusText.color = Color.red;
        }
        else if (SpillRiskIndex >= warningThreshold)
        {
            riskDescription = "High spill risk";
            liquidStatusText.color = Color.yellow;
        }
        else
        {
            riskDescription = "Low spill risk";
            liquidStatusText.color = Color.green;
        }

        liquidStatusText.text =
            $"Beaker tilt: {tiltDegrees:F1}°\n" +
            $"Risk index: {SpillRiskIndex * 100.0f:F0}%\n" +
            riskDescription;
    }

    public void SetFillFraction(float newFillFraction)
    {
        fillFraction = Mathf.Clamp(
            newFillFraction,
            0.05f,
            0.95f);
    }
}
