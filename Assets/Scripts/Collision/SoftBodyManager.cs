using System.Collections.Generic;
using UnityEngine;

public class SoftBodyManager : MonoBehaviour
{
    public List<MassSpringSystem> softBodies = new List<MassSpringSystem>();
    public bool showCollisionContacts = true;
    void Start()
    {
softBodies = new List<MassSpringSystem>(FindObjectsByType<MassSpringSystem>(FindObjectsSortMode.None));
        AssignObjectIDs();

        foreach (var body in softBodies)
        {
            body.RecomputeAABB();
            body.RebuildBVH();
        }
    }

    void Update()
    {
        DetectCollisions();
    }

    void AssignObjectIDs()
    {
        for (int i = 0; i < softBodies.Count; i++)
        {
            softBodies[i].objectID = i;
        }
    }

    void DetectCollisions()
    {
        for (int i = 0; i < softBodies.Count; i++)
        {
            for (int j = i + 1; j < softBodies.Count; j++)
            {
                var a = softBodies[i];
                var b = softBodies[j];

                if (a.aabb.Intersects(b.aabb))
                {
                    NarrowPhaseCheck(a, b);
                }
            }
        }
    }

    void NarrowPhaseCheck(MassSpringSystem a, MassSpringSystem b)
    {
        TraverseBVH(a.bvhRoot, b.bvhRoot);
    }

    void TraverseBVH(BVHNode A, BVHNode B)
    {
        if (!A.boundingBox.Intersects(B.boundingBox))
            return;

        if (A.IsLeaf && B.IsLeaf)
        {
            foreach (var ta in A.triangles)
            {
                foreach (var tb in B.triangles)
                {
                    if (TriangleIntersection(ta, tb))
                    {
                        RecordCollision(ta, tb);
                    }
                }
            }
        }
        else
        {
            if (A.IsLeaf)
            {
                TraverseBVH(A, B.left);
                TraverseBVH(A, B.right);
            }
            else if (B.IsLeaf)
            {
                TraverseBVH(A.left, B);
                TraverseBVH(A.right, B);
            }
            else
            {
                TraverseBVH(A.left, B.left);
                TraverseBVH(A.left, B.right);
                TraverseBVH(A.right, B.left);
                TraverseBVH(A.right, B.right);
            }
        }
    }

    bool TriangleIntersection(Triangle a, Triangle b)
    {
        float distance = Vector3.Distance(a.GetCentroid(), b.GetCentroid());
        return distance < 0.3f; // TEMP: Replace with real intersection later
    }

    void RecordCollision(Triangle a, Triangle b)
{
    Vector3 contactPoint = (a.GetCentroid() + b.GetCentroid()) * 0.5f;

    if (showCollisionContacts)
    {
        Debug.DrawRay(contactPoint, Vector3.up * 0.1f, Color.red, 0.2f);
    }

    Debug.Log($"Collision detected between triangles at: {contactPoint}");
}

}