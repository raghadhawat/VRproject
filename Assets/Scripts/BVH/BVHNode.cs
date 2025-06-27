using UnityEngine;
using System.Collections.Generic;

public class BVHNode
{
    public AABB BoundingBox;
    public BVHNode Left;
    public BVHNode Right;
    public GameObject GameObject;

    public bool IsLeaf => GameObject != null;

    public BVHNode(AABB boundingBox, GameObject gameObject = null)
    {
        BoundingBox = boundingBox;
        GameObject = gameObject;
    }
}
