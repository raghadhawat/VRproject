using System.Collections.Generic;
using UnityEngine;
    
public class BVHBuilder
{
    public static BVHNode Build(List<Triangle> triangles, int depth = 0)
    {
        if (triangles == null || triangles.Count == 0)
            return null;

        Bounds nodeBounds = AABBUtils.ComputeBounds(triangles);
        BVHNode node = new BVHNode(nodeBounds);


        // Base case: make leaf node
        if (triangles.Count <= 2)
        {
            node.triangles = triangles;
            return node;
        }

        // Choose split axis: longest dimension
        Vector3 size = nodeBounds.size;
        int axis = 0;
        if (size.y > size.x && size.y > size.z) axis = 1;
        else if (size.z > size.x) axis = 2;

        // Sort triangles by centroid along chosen axis
        triangles.Sort((a, b) =>
            a.GetCentroid()[axis].CompareTo(b.GetCentroid()[axis]));

        int mid = triangles.Count / 2;
        var leftTris = triangles.GetRange(0, mid);
        var rightTris = triangles.GetRange(mid, triangles.Count - mid);

        // Recursive build
        node.left = Build(leftTris, depth + 1);
        node.right = Build(rightTris, depth + 1);

        return node;
    }
}
