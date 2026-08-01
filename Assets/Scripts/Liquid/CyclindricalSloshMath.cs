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
     * First cylindrical asymmetric slosh-mode root.
     */
    public const float FirstModeRoot =
        1.8411838f;

    /*
     * Effective damping ratio for the reduced-order
     * small-beaker water model.
     *
     * This is an effective model value, not a universal
     * material constant for water.
     */
    public const float WaterDampingRatio =
        0.05f;

    public const float MaximumSurfaceTiltDegrees =
        80.0f;

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
            Mathf.Max(
                omegaSquared,
                0.0001f));
    }

    public static float NaturalFrequencyHertz(
        float innerRadius,
        float liquidDepth)
    {
        return
            NaturalAngularFrequency(
                innerRadius,
                liquidDepth) /
            (2.0f * Mathf.PI);
    }

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
            Mathf.Tan(
                maximumAngleRadians);

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
        return
            Mathf.Atan(
                surfaceSlope.magnitude) *
            Mathf.Rad2Deg;
    }
}
