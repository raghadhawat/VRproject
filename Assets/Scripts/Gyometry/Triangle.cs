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
}
