// using System.Collections.Generic;
// using UnityEngine;
    
// public class BVHBuilder
// {
//     public static BVHNode Build(List<Triangle> triangles, int depth = 0)
//     {
//         if (triangles == null || triangles.Count == 0)
//             return null;

//         AABB nodeBounds = AABBUtils.ComputeAABB(triangles);
//         BVHNode node = new BVHNode(nodeBounds);


//         if (triangles.Count <= 2)
//         {
//             node.triangles = triangles;
//             return node;
//         }

//         Vector3 size = node.BoundingBox.Size;
//         int axis = 0;
//         if (size.y > size.x && size.y > size.z) axis = 1;
//         else if (size.z > size.x) axis = 2;

//         triangles.Sort((a, b) =>
//             a.GetCentroid()[axis].CompareTo(b.GetCentroid()[axis]));

//         int mid = triangles.Count / 2;
//         var leftTris = triangles.GetRange(0, mid);
//         var rightTris = triangles.GetRange(mid, triangles.Count - mid);

//         node.Left = Build(leftTris, depth + 1);
//         node.Right = Build(rightTris, depth + 1);

//         return node;
//     }
// }
