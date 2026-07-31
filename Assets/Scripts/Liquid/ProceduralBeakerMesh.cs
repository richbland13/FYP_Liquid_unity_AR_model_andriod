using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Generates a simple, hollow cylindrical glass beaker.
///
/// High-payoff details:
/// - outer wall;
/// - inner wall;
/// - solid-looking base;
/// - open top;
/// - rounded upper rim;
/// - separate material slot for the rim.
///
/// Radius and height come only from BeakerGeometry.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class ProceduralBeakerMesh : MonoBehaviour
{
    private const int RadialSegments = 64;
    private const int RimCrossSectionSegments = 10;

    [Header("References")]

    [SerializeField]
    private BeakerGeometry geometry;

    [Header("Materials")]

    [SerializeField]
    private Material glassMaterial;

    [SerializeField]
    private Material rimMaterial;

    private Mesh generatedMesh;

    private float previousInnerRadius = float.NaN;
    private float previousInternalHeight = float.NaN;

    private void OnEnable()
    {
        ConfigureRenderer();
        EnsureMesh();
        ForceRebuild();
    }

    private void Update()
    {
        if (geometry == null)
        {
            return;
        }

        bool geometryChanged =
            !Mathf.Approximately(
                previousInnerRadius,
                geometry.InnerRadius) ||
            !Mathf.Approximately(
                previousInternalHeight,
                geometry.InternalHeight);

        if (geometryChanged)
        {
            RebuildMesh();
        }

        ApplyMaterials();
    }

    private void OnValidate()
    {
        ConfigureRenderer();
        ForceRebuild();
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

        // Draw the transparent glass after the transparent liquid.
        meshRenderer.sortingOrder = 20;

        ApplyMaterials();
    }

    private void ApplyMaterials()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer == null || glassMaterial == null)
        {
            return;
        }

        Material visibleRimMaterial =
            rimMaterial != null ? rimMaterial : glassMaterial;

        meshRenderer.sharedMaterials =
            new[]
            {
                glassMaterial,
                visibleRimMaterial
            };
    }

    private void ForceRebuild()
    {
        previousInnerRadius = float.NaN;
        previousInternalHeight = float.NaN;

        if (!isActiveAndEnabled || geometry == null)
        {
            return;
        }

        EnsureMesh();
        RebuildMesh();
    }

    private void EnsureMesh()
    {
        if (generatedMesh != null)
        {
            return;
        }

        generatedMesh = new Mesh
        {
            name = "Generated Hollow Beaker"
        };

        generatedMesh.MarkDynamic();

        GetComponent<MeshFilter>().sharedMesh = generatedMesh;
    }

    private void RebuildMesh()
    {
        EnsureMesh();

        float innerRadius = geometry.InnerRadius;
        float outerRadius = geometry.OuterRadius;
        float bottomY = geometry.OuterBottomY;
        float floorY = 0.0f;
        float topY = geometry.RimY;

        var vertices = new List<Vector3>();
        var bodyTriangles = new List<int>();
        var rimTriangles = new List<int>();

        AddOuterWall(
            vertices,
            bodyTriangles,
            outerRadius,
            bottomY,
            topY);

        AddInnerWall(
            vertices,
            bodyTriangles,
            innerRadius,
            floorY,
            topY);

        AddBottomSurface(
            vertices,
            bodyTriangles,
            outerRadius,
            bottomY);

        AddInternalFloor(
            vertices,
            bodyTriangles,
            innerRadius,
            floorY);

        AddRoundedRim(
            vertices,
            rimTriangles,
            innerRadius,
            outerRadius,
            topY);

        generatedMesh.Clear();
        generatedMesh.SetVertices(vertices);

        generatedMesh.subMeshCount = 2;
        generatedMesh.SetTriangles(bodyTriangles, 0);
        generatedMesh.SetTriangles(rimTriangles, 1);

        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();

        previousInnerRadius = geometry.InnerRadius;
        previousInternalHeight = geometry.InternalHeight;

        ApplyMaterials();
    }

    private static void AddOuterWall(
        List<Vector3> vertices,
        List<int> triangles,
        float radius,
        float bottomY,
        float topY)
    {
        int start = vertices.Count;

        for (int i = 0; i < RadialSegments; i++)
        {
            float angle =
                2.0f * Mathf.PI * i / RadialSegments;

            float x = radius * Mathf.Cos(angle);
            float z = radius * Mathf.Sin(angle);

            vertices.Add(new Vector3(x, bottomY, z));
            vertices.Add(new Vector3(x, topY, z));
        }

        for (int i = 0; i < RadialSegments; i++)
        {
            int next = (i + 1) % RadialSegments;

            int bottomCurrent = start + i * 2;
            int topCurrent = bottomCurrent + 1;
            int bottomNext = start + next * 2;
            int topNext = bottomNext + 1;

            // Outward-facing winding.
            triangles.Add(bottomCurrent);
            triangles.Add(topNext);
            triangles.Add(bottomNext);

            triangles.Add(bottomCurrent);
            triangles.Add(topCurrent);
            triangles.Add(topNext);
        }
    }

    private static void AddInnerWall(
        List<Vector3> vertices,
        List<int> triangles,
        float radius,
        float floorY,
        float topY)
    {
        int start = vertices.Count;

        for (int i = 0; i < RadialSegments; i++)
        {
            float angle =
                2.0f * Mathf.PI * i / RadialSegments;

            float x = radius * Mathf.Cos(angle);
            float z = radius * Mathf.Sin(angle);

            vertices.Add(new Vector3(x, floorY, z));
            vertices.Add(new Vector3(x, topY, z));
        }

        for (int i = 0; i < RadialSegments; i++)
        {
            int next = (i + 1) % RadialSegments;

            int bottomCurrent = start + i * 2;
            int topCurrent = bottomCurrent + 1;
            int bottomNext = start + next * 2;
            int topNext = bottomNext + 1;

            // Inward-facing winding.
            triangles.Add(bottomCurrent);
            triangles.Add(bottomNext);
            triangles.Add(topNext);

            triangles.Add(bottomCurrent);
            triangles.Add(topNext);
            triangles.Add(topCurrent);
        }
    }

    private static void AddBottomSurface(
        List<Vector3> vertices,
        List<int> triangles,
        float radius,
        float bottomY)
    {
        int centre = vertices.Count;
        vertices.Add(new Vector3(0.0f, bottomY, 0.0f));

        int ringStart = vertices.Count;

        for (int i = 0; i < RadialSegments; i++)
        {
            float angle =
                2.0f * Mathf.PI * i / RadialSegments;

            vertices.Add(
                new Vector3(
                    radius * Mathf.Cos(angle),
                    bottomY,
                    radius * Mathf.Sin(angle)));
        }

        for (int i = 0; i < RadialSegments; i++)
        {
            int next = (i + 1) % RadialSegments;

            // Downward-facing base.
            triangles.Add(centre);
            triangles.Add(ringStart + i);
            triangles.Add(ringStart + next);
        }
    }

    private static void AddInternalFloor(
        List<Vector3> vertices,
        List<int> triangles,
        float radius,
        float floorY)
    {
        int centre = vertices.Count;
        vertices.Add(new Vector3(0.0f, floorY, 0.0f));

        int ringStart = vertices.Count;

        for (int i = 0; i < RadialSegments; i++)
        {
            float angle =
                2.0f * Mathf.PI * i / RadialSegments;

            vertices.Add(
                new Vector3(
                    radius * Mathf.Cos(angle),
                    floorY,
                    radius * Mathf.Sin(angle)));
        }

        for (int i = 0; i < RadialSegments; i++)
        {
            int next = (i + 1) % RadialSegments;

            // Upward-facing internal floor.
            triangles.Add(centre);
            triangles.Add(ringStart + next);
            triangles.Add(ringStart + i);
        }
    }

    private static void AddRoundedRim(
        List<Vector3> vertices,
        List<int> triangles,
        float innerRadius,
        float outerRadius,
        float topY)
    {
        int start = vertices.Count;

        float majorRadius =
            0.5f * (innerRadius + outerRadius);

        float minorRadius =
            BeakerGeometry.WallThicknessMetres * 0.75f;

        for (int radial = 0;
             radial < RadialSegments;
             radial++)
        {
            float radialAngle =
                2.0f * Mathf.PI * radial / RadialSegments;

            float radialCos = Mathf.Cos(radialAngle);
            float radialSin = Mathf.Sin(radialAngle);

            for (int cross = 0;
                 cross < RimCrossSectionSegments;
                 cross++)
            {
                float crossAngle =
                    2.0f * Mathf.PI *
                    cross / RimCrossSectionSegments;

                float ringRadius =
                    majorRadius +
                    minorRadius * Mathf.Cos(crossAngle);

                float y =
                    topY +
                    minorRadius * Mathf.Sin(crossAngle);

                vertices.Add(
                    new Vector3(
                        ringRadius * radialCos,
                        y,
                        ringRadius * radialSin));
            }
        }

        for (int radial = 0;
             radial < RadialSegments;
             radial++)
        {
            int nextRadial =
                (radial + 1) % RadialSegments;

            for (int cross = 0;
                 cross < RimCrossSectionSegments;
                 cross++)
            {
                int nextCross =
                    (cross + 1) %
                    RimCrossSectionSegments;

                int a =
                    start +
                    radial * RimCrossSectionSegments +
                    cross;

                int b =
                    start +
                    radial * RimCrossSectionSegments +
                    nextCross;

                int c =
                    start +
                    nextRadial * RimCrossSectionSegments +
                    cross;

                int d =
                    start +
                    nextRadial * RimCrossSectionSegments +
                    nextCross;

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);

                triangles.Add(b);
                triangles.Add(d);
                triangles.Add(c);
            }
        }
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
