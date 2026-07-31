using UnityEngine;

/// <summary>
/// Shared physical dimensions for the cylindrical beaker and liquid.
///
/// Coordinate convention:
/// - The centre of the internal floor is local (0, 0, 0).
/// - Local +Y points upward through the beaker.
/// - Dimensions are in metres.
/// </summary>
[ExecuteAlways]
public sealed class BeakerGeometry : MonoBehaviour
{
    // Fixed construction details. These are deliberately not user parameters.
    public const float WallThicknessMetres = 0.002f;
    public const float BaseThicknessMetres = 0.004f;
    public const float LiquidWallClearanceMetres = 0.00075f;

    [Header("Adjustable dimensions")]

    [Min(0.005f)]
    [SerializeField]
    private float innerRadius = 0.032f;

    [Min(0.020f)]
    [SerializeField]
    private float internalHeight = 0.120f;

    [Range(0.05f, 0.95f)]
    [SerializeField]
    private float fillFraction = 0.60f;

    public float InnerRadius => innerRadius;
    public float InternalHeight => internalHeight;
    public float FillFraction => fillFraction;

    public float LiquidDepth =>
        internalHeight * fillFraction;

    public float LiquidRenderRadius =>
        Mathf.Max(
            0.001f,
            innerRadius - LiquidWallClearanceMetres);

    public float OuterRadius =>
        innerRadius + WallThicknessMetres;

    public float OuterBottomY =>
        -BaseThicknessMetres;

    public float RimY =>
        internalHeight;

    public float Freeboard =>
        internalHeight - LiquidDepth;

    public void SetInnerRadius(float value)
    {
        innerRadius = Mathf.Max(0.005f, value);
    }

    public void SetInternalHeight(float value)
    {
        internalHeight = Mathf.Max(0.020f, value);
    }

    public void SetFillFraction(float value)
    {
        fillFraction = Mathf.Clamp(value, 0.05f, 0.95f);
    }

    private void OnValidate()
    {
        innerRadius = Mathf.Max(0.005f, innerRadius);
        internalHeight = Mathf.Max(0.020f, internalHeight);
        fillFraction = Mathf.Clamp(fillFraction, 0.05f, 0.95f);
    }
}
