using System.Collections.Generic;
using UnityEngine;

public class OctreeNode
{
public Bounds bounds;
public List<Vector3> points = new List<Vector3>();
public OctreeNode[] children = null;
public int maxPoints = 8;
public int maxDepth = 5;
private int depth;


public bool IsLeaf => children == null;

public OctreeNode(Bounds bounds, int depth = 0)
{
    this.bounds = bounds;
    this.depth = depth;
}

public void Insert(Vector3 point)
{
    if (!bounds.Contains(point)) return;

    if (IsLeaf)
    {
        if (points.Count < maxPoints || depth >= maxDepth)
        {
            points.Add(point);
            return;
        }

        Subdivide();
        foreach (var oldPoint in points)
            InsertIntoChildren(oldPoint);

        points.Clear();
    }

    InsertIntoChildren(point);
}

private void InsertIntoChildren(Vector3 point)
{
    foreach (var child in children)
        child.Insert(point);
}

private void Subdivide()
{
    children = new OctreeNode[8];
    Vector3 size = bounds.size / 2f;
    Vector3 center = bounds.center;
    int i = 0;

    for (int x = -1; x <= 1; x += 2)
    for (int y = -1; y <= 1; y += 2)
    for (int z = -1; z <= 1; z += 2)
    {
        Vector3 childCenter = center + Vector3.Scale(size / 2f, new Vector3(x, y, z));
        Bounds childBounds = new Bounds(childCenter, size);
        children[i++] = new OctreeNode(childBounds, depth + 1);
    }
}

public void Query(Bounds area, List<Vector3> result)
{
    if (!bounds.Intersects(area)) return;

    if (IsLeaf)
    {
        foreach (var p in points)
            if (area.Contains(p)) result.Add(p);
    }
    else
    {
        foreach (var child in children)
            child.Query(area, result);
    }
}
}