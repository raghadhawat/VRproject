using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(TriangleExtractor))]
public class VolumeSampler : MonoBehaviour
{
    public float spacing = 0.2f;
    public List<Vector3> insidePoints = new List<Vector3>();

    void Start()
    {
        TriangleExtractor extractor = GetComponent<TriangleExtractor>();
        List<Triangle> tris = extractor.triangles;

        if (tris == null || tris.Count == 0)
        {
            Debug.LogError("No triangle data found.");
            return;
        }

        Bounds bounds = AABBUtils.ComputeBounds(tris);
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        int insideCount = 0;

        for (float x = min.x; x <= max.x; x += spacing)
        {
            for (float y = min.y; y <= max.y; y += spacing)
            {
                for (float z = min.z; z <= max.z; z += spacing)
                {
                    Vector3 point = new Vector3(x, y, z);
                    Vector3 rayDir = Vector3.right;
                    int hitCount = 0;

                    foreach (var tri in tris)
                    {
                        if (tri.IntersectRay(point, rayDir, out float _))
                            hitCount++;
                    }

                    if (hitCount % 2 == 1)
                    {
                        insidePoints.Add(point);
                        Debug.DrawRay(point, Vector3.up * 0.1f, Color.cyan, 10f);
                        insideCount++;
                    }
                }
            }
        }

        Debug.Log($"Found {insideCount} inside points from voxel grid.");
    }
    public bool IsReady => insidePoints != null && insidePoints.Count > 0;

}
