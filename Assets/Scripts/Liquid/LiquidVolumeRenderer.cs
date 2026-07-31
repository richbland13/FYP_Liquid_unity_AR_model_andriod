using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders a closed liquid volume from the shared LiquidState.
///
/// This component contains no spring-damper physics.
/// The classical estimator or future neural estimator supplies:
/// - SurfaceSlope;
/// - SurfaceSlopeVelocity.
///
/// The renderer derives only small visual ripples from slope velocity.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(250)]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class LiquidVolumeRenderer : MonoBehaviour
{
    private const int RadialSegments = 48;
    private const int SurfaceRings = 6;

    private const float MeniscusDepthMetres = 0.0012f;
    private const float SurfaceVerticalClearanceMetres = 0.0005f;

    private const float MaximumRippleAmplitudeMetres = 0.00065f;
    private const float RippleVelocityForMaximum = 3.0f;
    private const float RippleResponseRate = 8.0f;

    [Header("References")]

    [SerializeField]
    private BeakerGeometry geometry;

    [SerializeField]
    private LiquidStateProvider stateProvider;

    [Header("Materials")]

    [SerializeField]
    private Material liquidBodyMaterial;

    [SerializeField]
    private Material liquidSurfaceMaterial;

    [Header("Simple visual finishing")]

    [SerializeField]
    private bool enableSubtleRipples = true;

    private Mesh generatedMesh;

    private Vector3[] vertices;

    private int sideStart;
    private int bottomStart;
    private int surfaceCentreIndex;
    private int surfaceRingStart;
    private int meniscusStart;

    private float displayedRippleAmplitude;

    private void OnEnable()
    {
        ConfigureRenderer();
        BuildTopology();
        UpdateMeshVertices();
    }

    private void OnValidate()
    {
        ConfigureRenderer();

        if (isActiveAndEnabled)
        {
            BuildTopology();
            UpdateMeshVertices();
        }
    }

    private void LateUpdate()
    {
        UpdateMeshVertices();
        ApplyMaterials();
    }

    private void ConfigureRenderer()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            return;
        }

        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        // The liquid renders before the transparent glass.
        meshRenderer.sortingOrder = 10;

        ApplyMaterials();
    }

    private void ApplyMaterials()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer == null ||
            liquidBodyMaterial == null)
        {
            return;
        }

        Material visibleSurfaceMaterial =
            liquidSurfaceMaterial != null
                ? liquidSurfaceMaterial
                : liquidBodyMaterial;

        meshRenderer.sharedMaterials =
            new[]
            {
                liquidBodyMaterial,
                visibleSurfaceMaterial
            };
    }

    private void BuildTopology()
    {
        EnsureMesh();

        sideStart = 0;

        int sideVertexCount = RadialSegments * 2;

        bottomStart = sideStart + sideVertexCount;

        int bottomVertexCount = 1 + RadialSegments;

        surfaceCentreIndex =
            bottomStart + bottomVertexCount;

        surfaceRingStart =
            surfaceCentreIndex + 1;

        int surfaceRingVertexCount =
            SurfaceRings * RadialSegments;

        meniscusStart =
            surfaceRingStart + surfaceRingVertexCount;

        int meniscusVertexCount =
            RadialSegments * 2;

        int totalVertexCount =
            meniscusStart + meniscusVertexCount;

        vertices = new Vector3[totalVertexCount];

        var bodyTriangles = new List<int>();
        var surfaceTriangles = new List<int>();

        BuildSideTriangles(bodyTriangles);
        BuildBottomTriangles(bodyTriangles);
        BuildSurfaceTriangles(surfaceTriangles);
        BuildMeniscusTriangles(surfaceTriangles);

        generatedMesh.Clear();
        generatedMesh.vertices = vertices;

        generatedMesh.subMeshCount = 2;
        generatedMesh.SetTriangles(bodyTriangles, 0);
        generatedMesh.SetTriangles(surfaceTriangles, 1);

        ApplyMaterials();
    }

    private void EnsureMesh()
    {
        if (generatedMesh != null)
        {
            return;
        }

        generatedMesh = new Mesh
        {
            name = "Generated Model-Driven Liquid"
        };

        generatedMesh.MarkDynamic();

        GetComponent<MeshFilter>().sharedMesh =
            generatedMesh;
    }

    private void BuildSideTriangles(
        List<int> triangles)
    {
        for (int i = 0; i < RadialSegments; i++)
        {
            int next = (i + 1) % RadialSegments;

            int bottomCurrent =
                sideStart + i * 2;

            int topCurrent =
                bottomCurrent + 1;

            int bottomNext =
                sideStart + next * 2;

            int topNext =
                bottomNext + 1;

            triangles.Add(bottomCurrent);
            triangles.Add(topNext);
            triangles.Add(bottomNext);

            triangles.Add(bottomCurrent);
            triangles.Add(topCurrent);
            triangles.Add(topNext);
        }
    }

    private void BuildBottomTriangles(
        List<int> triangles)
    {
        int centre = bottomStart;
        int ringStart = bottomStart + 1;

        for (int i = 0; i < RadialSegments; i++)
        {
            int next = (i + 1) % RadialSegments;

            triangles.Add(centre);
            triangles.Add(ringStart + i);
            triangles.Add(ringStart + next);
        }
    }

    private void BuildSurfaceTriangles(
        List<int> triangles)
    {
        // Centre fan to first ring.
        for (int i = 0; i < RadialSegments; i++)
        {
            int next = (i + 1) % RadialSegments;

            int currentOuter =
                surfaceRingStart + i;

            int nextOuter =
                surfaceRingStart + next;

            triangles.Add(surfaceCentreIndex);
            triangles.Add(nextOuter);
            triangles.Add(currentOuter);
        }

        // Remaining concentric surface rings.
        for (int ring = 1;
             ring < SurfaceRings;
             ring++)
        {
            int innerStart =
                surfaceRingStart +
                (ring - 1) * RadialSegments;

            int outerStart =
                surfaceRingStart +
                ring * RadialSegments;

            for (int i = 0; i < RadialSegments; i++)
            {
                int next = (i + 1) % RadialSegments;

                int innerCurrent = innerStart + i;
                int innerNext = innerStart + next;
                int outerCurrent = outerStart + i;
                int outerNext = outerStart + next;

                triangles.Add(innerCurrent);
                triangles.Add(innerNext);
                triangles.Add(outerNext);

                triangles.Add(innerCurrent);
                triangles.Add(outerNext);
                triangles.Add(outerCurrent);
            }
        }
    }

    private void BuildMeniscusTriangles(
        List<int> triangles)
    {
        for (int i = 0; i < RadialSegments; i++)
        {
            int next = (i + 1) % RadialSegments;

            int bottomCurrent =
                meniscusStart + i * 2;

            int topCurrent =
                bottomCurrent + 1;

            int bottomNext =
                meniscusStart + next * 2;

            int topNext =
                bottomNext + 1;

            triangles.Add(bottomCurrent);
            triangles.Add(topNext);
            triangles.Add(bottomNext);

            triangles.Add(bottomCurrent);
            triangles.Add(topCurrent);
            triangles.Add(topNext);
        }
    }

    private void UpdateMeshVertices()
    {
        if (geometry == null)
        {
            return;
        }

        if (generatedMesh == null ||
            vertices == null)
        {
            BuildTopology();
        }

        LiquidState state =
            stateProvider != null &&
            stateProvider.HasValidState
                ? stateProvider.CurrentState
                : LiquidState.Level;

        float radius = geometry.LiquidRenderRadius;
        float liquidDepth = geometry.LiquidDepth;
        float internalHeight = geometry.InternalHeight;

        float targetRippleAmplitude =
            enableSubtleRipples &&
            Application.isPlaying
                ? MaximumRippleAmplitudeMetres *
                  Mathf.Clamp01(
                      state.SurfaceSlopeVelocity.magnitude /
                      RippleVelocityForMaximum)
                : 0.0f;

        float deltaTime =
            Application.isPlaying
                ? Time.deltaTime
                : 0.02f;

        float rippleBlend =
            1.0f -
            Mathf.Exp(
                -RippleResponseRate *
                Mathf.Max(deltaTime, 0.0001f));

        displayedRippleAmplitude =
            Mathf.Lerp(
                displayedRippleAmplitude,
                targetRippleAmplitude,
                rippleBlend);

        UpdateSideVertices(
            radius,
            liquidDepth,
            internalHeight,
            state);

        UpdateBottomVertices(radius);

        UpdateSurfaceVertices(
            radius,
            liquidDepth,
            internalHeight,
            state);

        UpdateMeniscusVertices(
            radius,
            liquidDepth,
            internalHeight,
            state);

        generatedMesh.vertices = vertices;
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();
    }

    private void UpdateSideVertices(
        float radius,
        float liquidDepth,
        float internalHeight,
        LiquidState state)
    {
        for (int i = 0; i < RadialSegments; i++)
        {
            float angle =
                2.0f * Mathf.PI *
                i / RadialSegments;

            float x = radius * Mathf.Cos(angle);
            float z = radius * Mathf.Sin(angle);

            float topY =
                EvaluateSurfaceHeight(
                    x,
                    z,
                    radius,
                    liquidDepth,
                    internalHeight,
                    state);

            int bottomIndex =
                sideStart + i * 2;

            vertices[bottomIndex] =
                new Vector3(x, 0.0f, z);

            vertices[bottomIndex + 1] =
                new Vector3(x, topY, z);
        }
    }

    private void UpdateBottomVertices(float radius)
    {
        vertices[bottomStart] = Vector3.zero;

        int ringStart = bottomStart + 1;

        for (int i = 0; i < RadialSegments; i++)
        {
            float angle =
                2.0f * Mathf.PI *
                i / RadialSegments;

            vertices[ringStart + i] =
                new Vector3(
                    radius * Mathf.Cos(angle),
                    0.0f,
                    radius * Mathf.Sin(angle));
        }
    }

    private void UpdateSurfaceVertices(
        float radius,
        float liquidDepth,
        float internalHeight,
        LiquidState state)
    {
        vertices[surfaceCentreIndex] =
            new Vector3(
                0.0f,
                EvaluateSurfaceHeight(
                    0.0f,
                    0.0f,
                    radius,
                    liquidDepth,
                    internalHeight,
                    state),
                0.0f);

        for (int ring = 1;
             ring <= SurfaceRings;
             ring++)
        {
            float ringRadius =
                radius *
                ring /
                SurfaceRings;

            int ringStart =
                surfaceRingStart +
                (ring - 1) *
                RadialSegments;

            for (int i = 0; i < RadialSegments; i++)
            {
                float angle =
                    2.0f * Mathf.PI *
                    i / RadialSegments;

                float x =
                    ringRadius * Mathf.Cos(angle);

                float z =
                    ringRadius * Mathf.Sin(angle);

                float y =
                    EvaluateSurfaceHeight(
                        x,
                        z,
                        radius,
                        liquidDepth,
                        internalHeight,
                        state);

                vertices[ringStart + i] =
                    new Vector3(x, y, z);
            }
        }
    }

    private void UpdateMeniscusVertices(
        float radius,
        float liquidDepth,
        float internalHeight,
        LiquidState state)
    {
        for (int i = 0; i < RadialSegments; i++)
        {
            float angle =
                2.0f * Mathf.PI *
                i / RadialSegments;

            float x = radius * Mathf.Cos(angle);
            float z = radius * Mathf.Sin(angle);

            float topY =
                EvaluateSurfaceHeight(
                    x,
                    z,
                    radius,
                    liquidDepth,
                    internalHeight,
                    state);

            float bottomY =
                Mathf.Max(
                    SurfaceVerticalClearanceMetres,
                    topY - MeniscusDepthMetres);

            int bottomIndex =
                meniscusStart + i * 2;

            vertices[bottomIndex] =
                new Vector3(x, bottomY, z);

            vertices[bottomIndex + 1] =
                new Vector3(x, topY, z);
        }
    }

    private float EvaluateSurfaceHeight(
        float x,
        float z,
        float radius,
        float liquidDepth,
        float internalHeight,
        LiquidState state)
    {
        float planeHeight =
            liquidDepth +
            state.SurfaceSlope.x * x +
            state.SurfaceSlope.y * z;

        float rippleHeight =
            EvaluateRippleHeight(
                x,
                z,
                radius,
                state.SurfaceSlopeVelocity);

        return Mathf.Clamp(
            planeHeight + rippleHeight,
            SurfaceVerticalClearanceMetres,
            internalHeight -
            SurfaceVerticalClearanceMetres);
    }

    private float EvaluateRippleHeight(
        float x,
        float z,
        float radius,
        Vector2 slopeVelocity)
    {
        if (displayedRippleAmplitude <= 0.000001f ||
            radius <= 0.000001f)
        {
            return 0.0f;
        }

        float time = Time.time;

        float normalisedX = x / radius;
        float normalisedZ = z / radius;

        Vector2 direction =
            slopeVelocity.sqrMagnitude > 0.000001f
                ? slopeVelocity.normalized
                : Vector2.right;

        float alongDirection =
            normalisedX * direction.x +
            normalisedZ * direction.y;

        float acrossDirection =
            -normalisedX * direction.y +
            normalisedZ * direction.x;

        float firstWave =
            Mathf.Sin(
                5.5f * alongDirection +
                7.0f * time);

        float secondWave =
            Mathf.Sin(
                4.0f * acrossDirection -
                5.0f * time +
                1.2f);

        float radialDistance =
            Mathf.Clamp01(
                Mathf.Sqrt(
                    normalisedX * normalisedX +
                    normalisedZ * normalisedZ));

        float edgeWeight =
            0.35f + 0.65f * radialDistance;

        return displayedRippleAmplitude *
               edgeWeight *
               (0.65f * firstWave +
                0.35f * secondWave);
    }

    private void OnDestroy()
    {
        if (generatedMesh == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(generatedMesh);
        }
        else
        {
            DestroyImmediate(generatedMesh);
        }
    }
}
