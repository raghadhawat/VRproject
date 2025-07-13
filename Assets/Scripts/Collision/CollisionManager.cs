using System.Collections.Generic;
using UnityEngine;

public class CollisionManager : MonoBehaviour
{
    public List<PBDSimulator> softBodies = new List<PBDSimulator>();
    public List<ContactPair> contacts = new List<ContactPair>();
    public List<(PBDSimulator, PBDSimulator)> potentialCollisions = new List<(PBDSimulator, PBDSimulator)>();
    public float contactSearchRadius = 0.2f;
    public float restitution = 0.3f;
    public float friction = 0.3f;

    [System.Obsolete]
    void Start()
    {
        softBodies.AddRange(FindObjectsOfType<PBDSimulator>());
    }

    void Update()
    {
        DetectAABBOverlaps();
        // ResolvePenetrationContacts();
        // ApplyVelocityImpulses(restitution);
    }

    public struct ContactPair
    {
        public PBDSimulator systemA;
        public PBDSimulator systemB;
        public int indexA;
        public int indexB;
        public float penetration;
        public Vector3 normal;

        public ContactPair(PBDSimulator a, int ia, PBDSimulator b, int ib, Vector3 normal, float penetration)
        {
            systemA = a;
            systemB = b;
            indexA = ia;
            indexB = ib;
            this.normal = normal;
            this.penetration = penetration;
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
                    potentialCollisions.Add((a, b));
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
                                    float penetration = contactSearchRadius - dist;
                                    contacts.Add(new ContactPair(a, indexA, b, indexB, normal, penetration));

                                    Debug.DrawLine(pa, pb, Color.green);
                                    Debug.Log($"Contact! {a.name} ↔ {b.name} | Penetration = {penetration:F4}");
                                }
                            }
                        }
                    }

                    Debug.DrawLine(a.aabb.Center, b.aabb.Center, Color.red);
                    Debug.Log($"Frame {Time.frameCount}: Total contacts = {contacts.Count}");
                }
            }
        }
    }

   
}