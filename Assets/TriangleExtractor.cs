using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class TriangleExtractor : MonoBehaviour
{
    void Start()
    {
        // Step 1: Access the MeshFilter component
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null)
        {
            Debug.LogError("MeshFilter not found on this GameObject.");
            return;
        }

        // Step 2: Get the mesh from the MeshFilter
        Mesh mesh = meshFilter.sharedMesh;
Debug.Log($"  v0: {mesh}");
        if (mesh == null)
        {
            Debug.LogError("No mesh found in MeshFilter.");
            return;
        }

        // Step 3: Access vertices and triangle indices
        Vector3[] vertices = mesh.vertices;    // all points
        int[] triangles = mesh.triangles;      // triplets of indices to vertices

        Debug.Log($"Loaded mesh with {vertices.Length} vertices and {triangles.Length / 3} triangles.");

        // Optional: Log the first triangle's vertices
        if (triangles.Length >= 3)
        {
            int i0 = triangles[0];
            int i1 = triangles[1];
            int i2 = triangles[2];

            Debug.Log($"First triangle:");
            Debug.Log($"  v0: {vertices[i0]}");
            Debug.Log($"  v1: {vertices[i1]}");
            Debug.Log($"  v2: {vertices[i2]}");
            
        }
    }
}
