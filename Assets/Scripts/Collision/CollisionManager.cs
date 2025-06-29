using System.Collections.Generic;
using UnityEngine;
public class CollisionManager : MonoBehaviour
{
    public List<MassSpringSystem> softBodies = new List<MassSpringSystem>();
    public List<ContactPair> contacts = new List<ContactPair>();
    public List<(MassSpringSystem, MassSpringSystem)> potentialCollisions = new List<(MassSpringSystem, MassSpringSystem)>();
    public float contactSearchRadius = 0.2f;

    [System.Obsolete]
    void Start()
    {
        softBodies.AddRange(FindObjectsOfType<MassSpringSystem>());
    }
    void Update()
    {
        DetectAABBOverlaps();
    }

    public struct ContactPair
{
public Vector3 pointA;
public Vector3 pointB;
public float distance;
public Vector3 normal;


public ContactPair(Vector3 a, Vector3 b)
{
    pointA = a;
    pointB = b;
    Vector3 delta = a - b;
    distance = delta.magnitude;
    normal = delta.normalized;
}
}
    void DetectAABBOverlaps()
    {
        contacts.Clear();
        potentialCollisions.Clear();

        for (int i = 0; i < softBodies.Count; i++)
        {
            var a = softBodies[i];
            for (int j = i + 1; j < softBodies.Count; j++)
            {
                var b = softBodies[j];
                if (a.aabb.Intersects(b.aabb))
                {
                    a.BuildOctree();
                    b.BuildOctree();


                    foreach (var pa in a.GetAllParticlePositions())
                    {
                        Bounds search = new Bounds(pa, Vector3.one * contactSearchRadius);
                        List<Vector3> neighbors = new List<Vector3>();
                        b.octreeRoot.Query(search, neighbors);

                        foreach (var pb in neighbors)
                        {
                            float dist = Vector3.Distance(pa, pb);
                            if (dist < contactSearchRadius)
                            {
                                contacts.Add(new ContactPair(pa, pb));
                                Debug.Log($"Contact! {a.name} ↔ {b.name} | d = {dist:F4}");
                                // Optional: visualize the connection
                                Debug.DrawLine(pa, pb, Color.green);

                                // Step 1.4 (coming next): here you would save this (pa, pb) pair for collision response
                            }
                        }
                    }
                    Debug.DrawLine(a.aabb.Center, b.aabb.Center, Color.red); // Optional: visualize connection
                    Debug.Log($"Frame {Time.frameCount}: Total contacts = {contacts.Count}");
                }
            }
        }
    }


}
