using System.Collections.Generic;
using UnityEngine;

public static class AABBUtils
{
    public static Bounds ComputeBounds(Triangle tri)
    {
        Bounds b = new Bounds(tri.v0, Vector3.zero);
        b.Encapsulate(tri.v1);
        b.Encapsulate(tri.v2);
        return b;
    }

    public static Bounds ComputeBounds(List<Triangle> tris)
    {
        if (tris == null || tris.Count == 0)
            return new Bounds();

        Bounds b = new Bounds(tris[0].v0, Vector3.zero);
        foreach (var tri in tris)
        {
            b.Encapsulate(tri.v0);
            b.Encapsulate(tri.v1);
            b.Encapsulate(tri.v2);
        }
        return b;
    }
}
