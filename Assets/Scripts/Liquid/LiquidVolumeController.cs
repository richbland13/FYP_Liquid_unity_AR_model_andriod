using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Generates a closed cylindrical liquid volume with a planar free surface.
///
/// The model:
/// - keeps the liquid inside a cylindrical beaker;
/// - maintains approximately constant volume;
/// - keeps the equilibrium surface horizontal relative to world gravity;
/// - adds a low-order spring/damper slosh response;
/// - calculates a normalised spill-risk index.
///
/// It is a reduced-order visual model, not a CFD simulation.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class LiquidVolumeController : MonoBehaviour
{
    [Header("Scene references")]

    [SerializeField]
    private Transform beakerAnchor;

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

    [Range(12, 128)]
    [SerializeField]
    private int radialSegments = 48;

    [Header("Dynamic slosh model")]

    [SerializeField]
    private bool enableDynamicSlosh = true;

    [Tooltip("Approximate oscillation frequency of the liquid surface.")]
    [Range(0.2f, 5.0f)]
    [SerializeField]
    private float naturalFrequencyHz = 1.4f;

    [Tooltip("Below 1 produces oscillation. Higher values settle faster.")]
    [Range(0.05f, 1.5f)]
    [SerializeField]
    private float dampingRatio = 0.28f;

    [Tooltip("Prevents extreme or inverted visual geometry.")]
    [Range(10.0f, 80.0f)]
    [SerializeField]
    private float maximumVisualTiltDegrees = 70.0f;

    [Header("Risk display")]

    [Range(0.0f, 1.0f)]
    [SerializeField]
    private float warningThreshold = 0.70f;

    public float SpillRiskIndex { get; private set; }

    private Mesh liquidMesh;
    private Vector3[] vertices;
    private int[] triangles;

    private Vector2 currentSlope;
    private Vector2 slopeVelocity;

    private int bottomCenterIndex;
    private int topCenterIndex;
    private int sideBottomStart;
    private int sideTopStart;
    private int capBottomStart;
    private int capTopStart;

    private void Awake()
    {
        if (beakerAnchor == null)
        {
            beakerAnchor = transform.parent;
        }

        BuildMesh();
        UpdateMeshGeometry();
    }

    private void LateUpdate()
    {
        if (beakerAnchor == null || liquidMesh == null)
        {
            return;
        }

        UpdateLiquidDynamics();
        UpdateMeshGeometry();
        UpdateRiskDisplay();
    }

    private void BuildMesh()
    {
        radialSegments = Mathf.Max(radialSegments, 12);

        bottomCenterIndex = 0;
        topCenterIndex = 1;

        sideBottomStart = 2;
        sideTopStart = sideBottomStart + radialSegments;
        capBottomStart = sideTopStart + radialSegments;
        capTopStart = capBottomStart + radialSegments;

        int vertexCount = 2 + radialSegments * 4;

        vertices = new Vector3[vertexCount];

        var triangleList = new List<int>(radialSegments * 12);

        for (int i = 0; i < radialSegments; i++)
        {
            int next = (i + 1) % radialSegments;

            int sideBottomCurrent = sideBottomStart + i;
            int sideBottomNext = sideBottomStart + next;
            int sideTopCurrent = sideTopStart + i;
            int sideTopNext = sideTopStart + next;

            // Outer cylindrical wall.
            triangleList.Add(sideBottomCurrent);
            triangleList.Add(sideTopNext);
            triangleList.Add(sideBottomNext);

            triangleList.Add(sideBottomCurrent);
            triangleList.Add(sideTopCurrent);
            triangleList.Add(sideTopNext);

            int capTopCurrent = capTopStart + i;
            int capTopNext = capTopStart + next;

            // Top liquid surface.
            triangleList.Add(topCenterIndex);
            triangleList.Add(capTopNext);
            triangleList.Add(capTopCurrent);

            int capBottomCurrent = capBottomStart + i;
            int capBottomNext = capBottomStart + next;

            // Bottom surface.
            triangleList.Add(bottomCenterIndex);
            triangleList.Add(capBottomCurrent);
            triangleList.Add(capBottomNext);
        }

        triangles = triangleList.ToArray();

        liquidMesh = new Mesh
        {
            name = "Runtime Liquid Volume"
        };

        // The vertices are updated every frame.
        liquidMesh.MarkDynamic();

        liquidMesh.SetVertices(vertices);
        liquidMesh.SetTriangles(triangles, 0);
        liquidMesh.RecalculateNormals();
        liquidMesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = liquidMesh;
    }

    private void UpdateLiquidDynamics()
    {
        /*
         * The desired free-surface normal is world-up.
         * Convert world-up into the beaker's local coordinate system.
         */
        Vector3 surfaceUpLocal =
            beakerAnchor.InverseTransformDirection(Vector3.up).normalized;

        // Avoid division by zero when the beaker approaches 90 degrees.
        float safeY = Mathf.Max(surfaceUpLocal.y, 0.10f);

        /*
         * Plane:
         *
         * y = fillHeight + slopeX*x + slopeZ*z
         */
        Vector2 targetSlope = new Vector2(
            -surfaceUpLocal.x / safeY,
            -surfaceUpLocal.z / safeY);

        float maximumSlope =
            Mathf.Tan(maximumVisualTiltDegrees * Mathf.Deg2Rad);

        targetSlope = Vector2.ClampMagnitude(
            targetSlope,
            maximumSlope);

        if (!enableDynamicSlosh)
        {
            currentSlope = targetSlope;
            slopeVelocity = Vector2.zero;
            return;
        }

        /*
         * Second-order spring/damper model:
         *
         * slopeAcceleration =
         *     naturalFrequency² * positionError
         *     - 2*dampingRatio*naturalFrequency*velocity
         *
         * A damping ratio below 1 creates an oscillatory response.
         */
        float omega = 2.0f * Mathf.PI * naturalFrequencyHz;

        float frameDt = Mathf.Min(Time.deltaTime, 0.033f);

        // Small substeps improve numerical stability.
        const int substeps = 2;
        float dt = frameDt / substeps;

        for (int step = 0; step < substeps; step++)
        {
            Vector2 error = targetSlope - currentSlope;

            Vector2 slopeAcceleration =
                omega * omega * error
                - 2.0f * dampingRatio * omega * slopeVelocity;

            slopeVelocity += slopeAcceleration * dt;
            currentSlope += slopeVelocity * dt;

            currentSlope = Vector2.ClampMagnitude(
                currentSlope,
                maximumSlope);
        }
    }

    private void UpdateMeshGeometry()
    {
        float fillHeight = internalHeight * fillFraction;

        vertices[bottomCenterIndex] = Vector3.zero;
        vertices[topCenterIndex] = new Vector3(0.0f, fillHeight, 0.0f);

        for (int i = 0; i < radialSegments; i++)
        {
            float angle =
                2.0f * Mathf.PI * i / radialSegments;

            float x = innerRadius * Mathf.Cos(angle);
            float z = innerRadius * Mathf.Sin(angle);

            float unclampedSurfaceHeight =
                fillHeight
                + currentSlope.x * x
                + currentSlope.y * z;

            /*
             * Clamping keeps the displayed mesh within the beaker.
             * Spill risk is calculated separately from the unclamped plane.
             */
            float surfaceHeight = Mathf.Clamp(
                unclampedSurfaceHeight,
                0.001f,
                internalHeight - 0.001f);

            Vector3 bottom = new Vector3(x, 0.0f, z);
            Vector3 top = new Vector3(x, surfaceHeight, z);

            vertices[sideBottomStart + i] = bottom;
            vertices[sideTopStart + i] = top;

            // Duplicate cap vertices produce sharper rim normals.
            vertices[capBottomStart + i] = bottom;
            vertices[capTopStart + i] = top;
        }

        liquidMesh.SetVertices(vertices);
        liquidMesh.RecalculateNormals();
        liquidMesh.RecalculateBounds();
    }

    private void UpdateRiskDisplay()
    {
        float fillHeight = internalHeight * fillFraction;
        float headspace = internalHeight - fillHeight;

        /*
         * For a planar surface, maximum height rise at the rim is:
         *
         * radius * magnitude(surface slope)
         */
        float rimRise = innerRadius * currentSlope.magnitude;

        SpillRiskIndex = Mathf.Clamp01(
            rimRise / Mathf.Max(headspace, 0.001f));

        if (liquidStatusText == null)
        {
            return;
        }

        float beakerTiltDegrees =
            Vector3.Angle(beakerAnchor.up, Vector3.up);

        float surfaceTiltDegrees =
            Mathf.Atan(currentSlope.magnitude) * Mathf.Rad2Deg;

        string state;

        if (SpillRiskIndex >= 1.0f)
        {
            state = "SPILL LIKELY";
            liquidStatusText.color = Color.red;
        }
        else if (SpillRiskIndex >= warningThreshold)
        {
            state = "High spill risk";
            liquidStatusText.color = Color.yellow;
        }
        else
        {
            state = "Low spill risk";
            liquidStatusText.color = Color.green;
        }

        liquidStatusText.text =
            $"Beaker tilt: {beakerTiltDegrees:F1}°\n" +
            $"Surface tilt: {surfaceTiltDegrees:F1}°\n" +
            $"Risk index: {SpillRiskIndex * 100.0f:F0}%\n" +
            state;
    }

    public void SetFillFraction(float value)
    {
        fillFraction = Mathf.Clamp(value, 0.05f, 0.95f);
    }

    private void OnDestroy()
    {
        if (liquidMesh != null)
        {
            Destroy(liquidMesh);
        }
    }
}