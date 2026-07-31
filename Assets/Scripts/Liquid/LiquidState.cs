using System;
using UnityEngine;

/// <summary>
/// Common four-scalar output shared by both the classical
/// and neural-network liquid estimators.
/// </summary>
[Serializable]
public struct LiquidState
{
    /// <summary>
    /// Surface-plane slopes in the beaker X and Z directions.
    ///
    /// For a surface:
    /// y = liquidDepth + slopeX*x + slopeZ*z
    /// </summary>
    public Vector2 SurfaceSlope;

    /// <summary>
    /// Rate of change of the two surface slopes, in 1/second.
    /// </summary>
    public Vector2 SurfaceSlopeVelocity;

    public LiquidState(
        Vector2 surfaceSlope,
        Vector2 surfaceSlopeVelocity)
    {
        SurfaceSlope = surfaceSlope;
        SurfaceSlopeVelocity = surfaceSlopeVelocity;
    }

    public static LiquidState Level =>
        new LiquidState(
            Vector2.zero,
            Vector2.zero);
}