using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class TriangleExtractor : MonoBehaviour
{
    public List<Triangle> triangles = new List<Triangle>();
    public bool showSamplePoints = true;
    public float sampleSpacing = 0.2f;

    private List<Vector3> debugPoints;

    void Awake()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("MeshFilter or mesh not found.");
            return;
        }

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] indices = mesh.triangles;

        triangles.Clear();

        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 v0 = transform.TransformPoint(vertices[indices[i]]);
            Vector3 v1 = transform.TransformPoint(vertices[indices[i + 1]]);
            Vector3 v2 = transform.TransformPoint(vertices[indices[i + 2]]);

            Triangle tri = new Triangle(v0, v1, v2);
            triangles.Add(tri);
        }

        // Precompute surface samples
        debugPoints = SampleSurfacePoints(sampleSpacing);
        Debug.Log($"Extracted {triangles.Count} triangles.");
    }

    public List<Vector3> SampleSurfacePoints(float spacing)
    {
        List<Vector3> surfacePoints = new List<Vector3>();

        foreach (var tri in triangles)
        {
            surfacePoints.AddRange(tri.SamplePointsUniform(spacing));
        }

        Debug.Log($"Sampled total surface points: {surfacePoints.Count}");
        return surfacePoints;
    }

    public List<Vector3> GetSampledPoints()
    {
        return debugPoints;
    }

    // void OnDrawGizmos()
    // {
    //     if (!Application.isPlaying || !showSamplePoints || debugPoints == null) return;

    //     Gizmos.color = Color.yellow;
    //     foreach (var p in debugPoints)
    //         Gizmos.DrawSphere(p, 0.025f);
    // }
}
