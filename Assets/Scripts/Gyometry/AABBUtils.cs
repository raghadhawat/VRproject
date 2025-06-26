using System.Collections.Generic;
using UnityEngine;

public static class AABBUtils
{
    // Computes AABB for a single triangle
    public static AABB ComputeAABB(Triangle tri)
    {
        Vector3 min = Vector3.Min(tri.v0, Vector3.Min(tri.v1, tri.v2));
        Vector3 max = Vector3.Max(tri.v0, Vector3.Max(tri.v1, tri.v2));
        return new AABB(min, max);
    }

    // Computes AABB for a list of triangles
    public static AABB ComputeAABB(List<Triangle> tris)
    {
        if (tris == null || tris.Count == 0)
            return new AABB(Vector3.zero, Vector3.zero);

        AABB bounds = ComputeAABB(tris[0]);
        for (int i = 1; i < tris.Count; i++)
        {
            AABB triBox = ComputeAABB(tris[i]);
            bounds = AABB.Union(bounds, triBox);
        }
        return bounds;
    }

    // Returns axis index with the largest size: 0 (x), 1 (y), or 2 (z)
    public static int GetLongestAxis(AABB box)
    {
        Vector3 size = box.Size;
        if (size.y > size.x && size.y > size.z) return 1;
        else if (size.z > size.x) return 2;
        else return 0;
    }

    // Optional: pad small boxes (for visualization)
    public static AABB PadAABB(AABB box, float minSize = 0.001f)
    {
        Vector3 size = box.Size;
        size.x = Mathf.Max(size.x, minSize);
        size.y = Mathf.Max(size.y, minSize);
        size.z = Mathf.Max(size.z, minSize);
        Vector3 center = box.Center;
        Vector3 min = center - size * 0.5f;
        Vector3 max = center + size * 0.5f;
        return new AABB(min, max);
    }
}
