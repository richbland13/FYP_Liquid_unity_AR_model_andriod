using System;
using UnityEngine;

/// <summary>
/// Analytical relationships for the fundamental lateral
/// slosh mode of a circular cylindrical beaker.
/// </summary>
public static class CylindricalSloshMath
{
    public const float GravityMetresPerSecondSquared =
        9.80665f;

    /*
     * First root of the derivative of the first-order
     * Bessel function for the fundamental asymmetric
     * cylindrical slosh mode.
     */
    public const float FirstModeRoot =
        1.8411838f;

    /*
     * Effective damping ratio used by the reduced-order
     * water slosh model.
     *
     * This represents the complete simplified system,
     * including water viscosity, wall interaction and
     * energy losses omitted by the single-mode model.
     */
    public const float WaterDampingRatio =
        0.08f;

    /*
     * Numerical safety limit rather than a physical
     * calibration parameter.
     */
    public const float MaximumSurfaceTiltDegrees =
        80.0f;

    /// <summary>
    /// Calculates the first-mode natural angular
    /// frequency in radians per second.
    ///
    /// omega² = g*k*tanh(k*h)
    /// k = 1.841/R
    /// </summary>
    public static float NaturalAngularFrequency(
        float innerRadius,
        float liquidDepth)
    {
        float safeRadius =
            Mathf.Max(innerRadius, 0.001f);

        float safeDepth =
            Mathf.Max(liquidDepth, 0.001f);

        float waveNumber =
            FirstModeRoot / safeRadius;

        float omegaSquared =
            GravityMetresPerSecondSquared *
            waveNumber *
            (float)Math.Tanh(
                waveNumber * safeDepth);

        return Mathf.Sqrt(
            Mathf.Max(omegaSquared, 0.0001f));
    }

    public static float NaturalFrequencyHertz(
        float innerRadius,
        float liquidDepth)
    {
        return NaturalAngularFrequency(
                   innerRadius,
                   liquidDepth) /
               (2.0f * Mathf.PI);
    }

    /// <summary>
    /// Converts an upward-facing liquid surface normal
    /// into X and Z plane slopes.
    /// </summary>
    public static Vector2 SurfaceSlopeFromNormal(
        Vector3 surfaceNormal)
    {
        if (surfaceNormal.sqrMagnitude <
            0.000001f)
        {
            return Vector2.zero;
        }

        surfaceNormal.Normalize();

        float maximumAngleRadians =
            MaximumSurfaceTiltDegrees *
            Mathf.Deg2Rad;

        float maximumSlope =
            Mathf.Tan(maximumAngleRadians);

        float safeVertical =
            Mathf.Max(
                surfaceNormal.y,
                Mathf.Cos(
                    maximumAngleRadians));

        Vector2 slope =
            new Vector2(
                -surfaceNormal.x /
                safeVertical,

                -surfaceNormal.z /
                safeVertical);

        return Vector2.ClampMagnitude(
            slope,
            maximumSlope);
    }

    public static float SurfaceTiltDegrees(
        Vector2 surfaceSlope)
    {
        return Mathf.Atan(
                   surfaceSlope.magnitude) *
               Mathf.Rad2Deg;
    }
}