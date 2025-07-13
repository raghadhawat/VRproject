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
         float dt = Time.fixedDeltaTime;

    // Simulate all soft bodies
    foreach (var sim in softBodies)
        sim.SimulatePBD(dt);
        DetectAABBOverlaps();
        ResolvePenetrationContacts(contacts);
        ApplyVelocityImpulses(contacts, restitution);
    }

    public struct ContactPair
    {
        public PBDSimulator systemA;
        public PBDSimulator systemB;
        public int indexA;
        public int indexB;
        public Vector3 normal;
        public float penetration;

        public ContactPair(PBDSimulator a, int ia, PBDSimulator b, int ib, Vector3 normal, float penetration)
        {
            this.systemA = a;
            this.indexA = ia;
            this.systemB = b;
            this.indexB = ib;
            this.normal = normal;
            this.penetration = penetration;
        }

        public Vector3 pointA => systemA.GetParticlePosition(indexA);
        public Vector3 pointB => systemB.GetParticlePosition(indexB);
        public Vector3 relativeVelocity => systemA.GetVelocity(indexA) - systemB.GetVelocity(indexB);
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
                                    // Debug.Log($"Contact! {a.name} ↔ {b.name} | Penetration = {penetration:F4}");
                                }
                            }
                        }
                    }

                    Debug.DrawLine(a.aabb.Center, b.aabb.Center, Color.red);
                    // Debug.Log($"Frame {Time.frameCount}: Total contacts = {contacts.Count}");
                }
            }
        }
    }

    public void ResolvePenetrationContacts(List<ContactPair> contacts)
    {
        foreach (var contact in contacts)
        {
            float wA = contact.systemA.GetInvMass(contact.indexA);
            float wB = contact.systemB.GetInvMass(contact.indexB);
            float totalWeight = wA + wB;
            if (totalWeight == 0f)
                continue;

            // Split the correction based on inverse masses
            Vector3 correction = contact.normal * (contact.penetration / totalWeight);

            contact.systemA.MoveParticle(contact.indexA, correction * wA);
            contact.systemB.MoveParticle(contact.indexB, -correction * wB);
       //     Debug.Log($"Correcting penetration between {contact.systemA.name} and {contact.systemB.name} by {contact.penetration:F4} units.");

        }

    }

public void ApplyVelocityImpulses(List<ContactPair> contacts, float restitution)
{
        foreach (var contact in contacts)
        {
            Vector3 va = contact.systemA.GetVelocity(contact.indexA);
            Vector3 vb = contact.systemB.GetVelocity(contact.indexB);
            Vector3 relativeVel = va - vb;

            float relNormalVel = Vector3.Dot(relativeVel, contact.normal);
            if (relNormalVel > 0f) continue; // already separating

            float invMassA = contact.systemA.GetInvMass(contact.indexA);
            float invMassB = contact.systemB.GetInvMass(contact.indexB);
            float totalInvMass = invMassA + invMassB;
            if (totalInvMass == 0f) continue;

            float impulseMag = -(1f + restitution) * relNormalVel / totalInvMass;
            Vector3 impulse = impulseMag * contact.normal;

            contact.systemA.ApplyImpulse(contact.indexA, impulse);
            contact.systemB.ApplyImpulse(contact.indexB, -impulse);
        Debug.Log($"Impulse applied between {contact.systemA.name} and {contact.systemB.name}: {impulse}");

    }
}

}