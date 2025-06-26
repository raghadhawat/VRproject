using UnityEngine;

public class Triangle
{
    public Vector3 v0, v1, v2;

    public Triangle(Vector3 a, Vector3 b, Vector3 c)
    {
        v0 = a;
        v1 = b;
        v2 = c;
    }

    public Vector3 GetCentroid()
    {
        return (v0 + v1 + v2) / 3f;
    }

    // Ray-triangle intersection using Möller–Trumbore algorithm
    public bool IntersectRay(Vector3 rayOrigin, Vector3 rayDir, out float distance)
    {
        distance = 0f;

        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;

        Vector3 h = Vector3.Cross(rayDir, edge2);
        float a = Vector3.Dot(edge1, h);

        if (Mathf.Abs(a) < 1e-7f) return false; // Ray is parallel to triangle

        float f = 1.0f / a;
        Vector3 s = rayOrigin - v0;
        float u = f * Vector3.Dot(s, h);
        if (u < 0.0f || u > 1.0f) return false;

        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(rayDir, q);
        if (v < 0.0f || u + v > 1.0f) return false;

        distance = f * Vector3.Dot(edge2, q);
        return distance > 0.0001f;
    }
}
