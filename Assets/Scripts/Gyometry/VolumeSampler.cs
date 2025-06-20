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

        int numX = Mathf.CeilToInt((max.x - min.x) / spacing);
        int numY = Mathf.CeilToInt((max.y - min.y) / spacing);
        int numZ = Mathf.CeilToInt((max.z - min.z) / spacing);

        int insideCount = 0;

        for (int ix = 0; ix <= numX; ix++)
        {
            for (int iy = 0; iy <= numY; iy++)
            {
                for (int iz = 0; iz <= numZ; iz++)
                {
                    Vector3 point = new Vector3(
                        min.x + ix * spacing,
                        min.y + iy * spacing,
                        min.z + iz * spacing
                    );

                    // Slight inward offset toward center to avoid precision issues
                    Vector3 offset = (bounds.center - point).normalized * 0.001f;
                    Vector3 testPoint = point + offset;

                    Vector3 rayDir = Vector3.right;
                    int hitCount = 0;

                    foreach (var tri in tris)
                    {
                        if (tri.IntersectRay(testPoint, rayDir, out float _))
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

        Debug.Log($"✅ Found {insideCount} inside points from voxel grid.");
    }

    public bool IsReady => insidePoints != null && insidePoints.Count > 0;
}
