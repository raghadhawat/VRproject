// using System.Collections.Generic;
// using UnityEngine;

// public class BVHNode
// {
//     public AABB BoundingBox;
//     public List<Triangle> triangles;
//     public BVHNode Left;
//     public BVHNode Right;

//     public GameObject GameObject;

//     public bool IsLeaf => triangles != null;

//     public BVHNode(AABB bounds)
//     {
//         this.BoundingBox = bounds;
//     }
// }

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
