using UnityEngine;

public struct AABB
{
    public Vector3 Min;
    public Vector3 Max;

    public AABB(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public bool Intersects(AABB other)
    {
        return (Min.x <= other.Max.x && Max.x >= other.Min.x) &&
               (Min.y <= other.Max.y && Max.y >= other.Min.y) &&
               (Min.z <= other.Max.z && Max.z >= other.Min.z);
    }

    public static AABB Union(AABB a, AABB b)
    {
        Vector3 min = Vector3.Min(a.Min, b.Min);
        Vector3 max = Vector3.Max(a.Max, b.Max);
        return new AABB(min, max);
    }

    public float Volume()
    {
        Vector3 size = Max - Min;
        return size.x * size.y * size.z;
    }

    public float SurfaceArea()
    {
        Vector3 size = Max - Min;
        return 2 * (size.x * size.y + size.x * size.z + size.y * size.z);
    }

    public bool Contains(Vector3 point)
    {
        return (point.x >= Min.x && point.x <= Max.x) &&
               (point.y >= Min.y && point.y <= Max.y) &&
               (point.z >= Min.z && point.z <= Max.z);
    }

    public Vector3 Center => (Min + Max) * 0.5f;
    public Vector3 Size => Max - Min;

    // Optional: Convert to Unity Bounds for drawing
    public Bounds ToBounds()
    {
        return new Bounds(Center, Size);
    }
}
