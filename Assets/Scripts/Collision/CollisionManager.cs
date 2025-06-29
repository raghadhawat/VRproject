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
        public MassSpringSystem systemA;
        public MassSpringSystem systemB;
        public int indexA;
        public int indexB;
        public float distance;
        public Vector3 normal;

        public ContactPair(MassSpringSystem a, int ia, MassSpringSystem b, int ib, Vector3 normal, float distance)
        {
            systemA = a;
            systemB = b;
            indexA = ia;
            indexB = ib;
            this.normal = normal;
            this.distance = distance;
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

                    for (int indexA = 0; indexA < a.GetParticleCount(); indexA++)
                    {
                        Vector3 pa = a.GetParticlePosition(indexA);
                        Bounds search = new Bounds(pa, Vector3.one * contactSearchRadius);
                        List<Vector3> neighbors = new List<Vector3>();
                        b.octreeRoot.Query(search, neighbors);

                        foreach (var pb in neighbors)
                        {
                            float dist = Vector3.Distance(pa, pb);
                            if (dist < contactSearchRadius)
                            {
                                int indexB = b.GetParticleIndex(pb);
                                if (indexB != -1)
                                {
                                    Vector3 normal = (pa - pb).normalized;
                                    contacts.Add(new ContactPair(a, indexA, b, indexB, normal, dist));
                                    Debug.DrawLine(pa, pb, Color.green);
                                    Debug.Log($"Contact! {a.name} ↔ {b.name} | d = {dist:F4}");
                                }
                            }
                        }
                    }

                    Debug.DrawLine(a.aabb.Center, b.aabb.Center, Color.red); // Visualize AABB center connection
                    Debug.Log($"Frame {Time.frameCount}: Total contacts = {contacts.Count}");
                }
            }
        }
    }
}
