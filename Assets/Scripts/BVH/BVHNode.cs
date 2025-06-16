using System.Collections.Generic;
using UnityEngine;

public class BVHNode
{
    public Bounds boundingBox;
    public BVHNode left;
    public BVHNode right;
    public List<Triangle> triangles;

    public bool IsLeaf => triangles != null && triangles.Count > 0;

    public BVHNode(Bounds box)
    {
        boundingBox = box;
    }
}
