using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class TriangleExtractor : MonoBehaviour
{
    public List<Triangle> triangles = new List<Triangle>();

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

            Triangle tri = new Triangle(v0, v1, v2); // ✅ This uses your Triangle class!
            triangles.Add(tri);
        }

        Debug.Log($"Extracted {triangles.Count} triangles.");
    }
}
