using System.Collections.Generic;
using UnityEngine;

public class BVHVisualizer : MonoBehaviour
{
    public BVHNode bvhRoot;
    public bool autoBuild = true;

    void Start()
{
    TriangleExtractor extractor = GetComponent<TriangleExtractor>();
    if (extractor == null)
    {
        Debug.LogError("TriangleExtractor not found on this GameObject.");
        return;
    }

    var extractedTris = extractor.triangles;
    if (extractedTris == null || extractedTris.Count == 0)
    {
        Debug.LogError("No triangles found in TriangleExtractor.");
        return;
    }

    List<Triangle> tris = extractor.triangles;
    bvhRoot = BVHBuilder.Build(tris);


 }


    private List<Triangle> MeshToTriangles(Mesh mesh)
    {
        List<Triangle> tris = new List<Triangle>();
        Vector3[] verts = mesh.vertices;
        int[] indices = mesh.triangles;

        for (int i = 0; i < indices.Length; i += 3)
        {
            tris.Add(new Triangle(
                transform.TransformPoint(verts[indices[i]]),
                transform.TransformPoint(verts[indices[i + 1]]),
                transform.TransformPoint(verts[indices[i + 2]])
            ));
        }

        return tris;
    }

    private void OnDrawGizmosSelected()
    {
        if (bvhRoot != null)
            DrawBVHNode(bvhRoot);
    }

    private void DrawBVHNode(BVHNode node)
    {
        if (node == null) return;

        Gizmos.color = node.IsLeaf ? Color.green : Color.red;
        Gizmos.DrawWireCube(node.boundingBox.center, node.boundingBox.size);

        DrawBVHNode(node.left);
        DrawBVHNode(node.right);
    }
}
