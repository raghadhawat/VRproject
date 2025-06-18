using System.Collections.Generic;
using UnityEngine;

public static class AABBUtils
{
    // Computes the bounds of a single triangle
    public static Bounds ComputeBounds(Triangle tri)
    {
        Bounds b = new Bounds(tri.v0, Vector3.zero);
        b.Encapsulate(tri.v1);
        b.Encapsulate(tri.v2);
        return b;
    }

    // Computes the bounds of a list of triangles
    public static Bounds ComputeBounds(List<Triangle> tris)
    {
        if (tris == null || tris.Count == 0)
            return new Bounds();

        Bounds b = ComputeBounds(tris[0]);

        for (int i = 1; i < tris.Count; i++)
        {
            Bounds triBounds = ComputeBounds(tris[i]);
            b.Encapsulate(triBounds.min);
            b.Encapsulate(triBounds.max);
        }

        return b;
    }

    // Returns axis index with the largest size: 0 (x), 1 (y), or 2 (z)
    public static int GetLongestAxis(Bounds b)
    {
        Vector3 size = b.size;
        if (size.y > size.x && size.y > size.z) return 1;
        else if (size.z > size.x) return 2;
        else return 0;
    }

    // Merges two bounds into a larger bound
    public static Bounds Union(Bounds a, Bounds b)
    {
        Bounds result = new Bounds(a.center, a.size);
        result.Encapsulate(b.min);
        result.Encapsulate(b.max);
        return result;
    }

    // Optional: Make thin boxes slightly thicker (e.g. for visualization)
    public static Bounds PadBounds(Bounds b, float minSize = 0.001f)
    {
        Vector3 size = b.size;
        size.x = Mathf.Max(size.x, minSize);
        size.y = Mathf.Max(size.y, minSize);
        size.z = Mathf.Max(size.z, minSize);
        return new Bounds(b.center, size);
    }
}
