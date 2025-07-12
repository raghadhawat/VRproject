using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CollisionManager : MonoBehaviour
{
    public List<ISoftBodySystem> softBodies = new List<ISoftBodySystem>();
    public List<ContactPair> contacts = new List<ContactPair>();
    public float contactSearchRadius = 0.05f;


    void Start()
    {
        softBodies.AddRange(FindObjectsOfType<MonoBehaviour>().OfType<ISoftBodySystem>());
        Debug.Log($"[CollisionManager] Found {softBodies.Count} soft bodies.");
    }
void FixedUpdate()
{
    contacts.Clear();
    DetectAABBOverlaps();
}


    void DetectAABBOverlaps()
    {
        for (int i = 0; i < softBodies.Count; i++)
        {
            for (int j = i + 1; j < softBodies.Count; j++)
            {
                var a = softBodies[i];
                var b = softBodies[j];

                if (!a.GetAABB().Intersects(b.GetAABB()))
                    continue;

                Debug.DrawLine(a.GetAABB().Center, b.GetAABB().Center, Color.magenta);
                DetectContactsBetween(a, b);
            }
        }
    }

    void DetectContactsBetween(ISoftBodySystem a, ISoftBodySystem b)
    {
        OctreeNode octreeB = b.BuildOctree();
        if (octreeB == null) return;

        float searchRadius = contactSearchRadius;
        float searchRadiusSqr = searchRadius * searchRadius;
        float epsilon = 1e-6f;

        for (int i = 0; i < a.GetParticleCount(); i++)
        {
            Vector3 posA = a.GetParticlePosition(i);
            Bounds searchArea = new Bounds(posA, Vector3.one * searchRadius * 2f);

            // Check all particles in b
            for (int j = 0; j < b.GetParticleCount(); j++)
            {
                Vector3 posB = b.GetParticlePosition(j);
                if (!searchArea.Contains(posB)) continue;

                Vector3 delta = posA - posB;
                float distSqr = delta.sqrMagnitude;

                if (distSqr < searchRadiusSqr && distSqr > epsilon)
                {
                    float dist = Mathf.Sqrt(distSqr);
                    Vector3 normal = delta / dist;
                    float penetration = searchRadius - dist;

                    contacts.Add(new ContactPair(a, i, b, j, normal, penetration));
                    Debug.Log($"[Contact] A:{a.name}[{i}] B:{b.name}[{j}] Penetration={penetration:F3}");

                }
            }
        }
    }    void OnDrawGizmos()
    {
        if (contacts == null) return;

        Gizmos.color = Color.blue;

        foreach (var c in contacts)
        {
            Vector3 posA = c.bodyA.GetParticlePosition(c.indexA);
            Vector3 posB = c.bodyB.GetParticlePosition(c.indexB);

            Gizmos.DrawLine(posA, posB);
            Gizmos.DrawSphere((posA + posB) * 0.5f, 0.01f);
        }
    }


}
