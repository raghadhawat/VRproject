using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class MassSpringSystem : MonoBehaviour
{
    public float stiffness = 20000;
    public float damping = 50f;
    public List<Particle> particles = new List<Particle>();
    public List<Spring> springs = new List<Spring>();
    private List<Vector3> originalOffsets = new List<Vector3>();

    private Vector3 initialCenter;
    public float shapeRestoreStrength = 50f; // tunable parameter
    public BVHNode bvhRoot;
    public Bounds aabb;
    public int objectID;

public void RecomputeAABB()
{
    if (particles == null || particles.Count == 0)
        return;

    Bounds bounds = new Bounds(particles[0].position, Vector3.zero);
    for (int i = 1; i < particles.Count; i++)
    {
        bounds.Encapsulate(particles[i].position);
    }

    this.aabb = bounds;
}

public void RebuildBVH()
{
    // You may already have triangles from triangle extractor
    // Otherwise you can approximate using a generated triangle list
    TriangleExtractor extractor = GetComponent<TriangleExtractor>();
    if (extractor == null || extractor.triangles == null || extractor.triangles.Count == 0)
    {
        Debug.LogWarning("BVH rebuild skipped: no triangle data found.");
        return;
    }

    bvhRoot = BVHBuilder.Build(extractor.triangles);
}


    void Start()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        // Create particles
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 worldPos = transform.TransformPoint(verts[i]);
            particles.Add(new Particle(worldPos, 1f));
        }

        // Create springs from triangle edges
        HashSet<(int, int)> edgeSet = new HashSet<(int, int)>();

        for (int i = 0; i < tris.Length; i += 3)
        {
            AddSpring(edgeSet, tris[i], tris[i + 1]);
            AddSpring(edgeSet, tris[i + 1], tris[i + 2]);
            AddSpring(edgeSet, tris[i + 2], tris[i]);
        }

    }

    void AddSpring(HashSet<(int, int)> edgeSet, int i, int j)
    {
        int min = Mathf.Min(i, j);
        int max = Mathf.Max(i, j);
        var edge = (min, max);
        if (edgeSet.Contains(edge)) return;

        edgeSet.Add(edge);
        springs.Add(new Spring(particles[i], particles[j], stiffness, damping));
    }
    public void InitializeFromPoints(List<Vector3> points, float springRestLength, float connectRadius)
    {
        particles.Clear();
        springs.Clear();

        // Create particles
        foreach (Vector3 p in points)
        {
            particles.Add(new Particle(p, 1f)); // default mass
        }

        // Create springs between nearby particles
        int n = particles.Count;
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                float dist = Vector3.Distance(particles[i].position, particles[j].position);
                if (dist <= connectRadius)
                {
                    springs.Add(new Spring(particles[i], particles[j], stiffness, damping));
                }
            }
        }
        // Store original center of mass
        initialCenter = Vector3.zero;
        foreach (var p in particles)
            initialCenter += p.position;
        initialCenter /= particles.Count;

        // Compute offsets from center for each particle
        originalOffsets.Clear();
        for (int i = 0; i < particles.Count; i++)
        {
            Vector3 offset = particles[i].position - initialCenter;
            originalOffsets.Add(offset);
        }






        Debug.Log($"Initialized system: {particles.Count} particles, {springs.Count} springs.");
    }

    public void ApplyShapePreservationForces()
    {
        if (originalOffsets == null || originalOffsets.Count != particles.Count)
            return;

        // Compute current center of mass
        Vector3 currentCenter = Vector3.zero;
        foreach (var p in particles)
            currentCenter += p.position;
        currentCenter /= particles.Count;

        // Apply restorative force based on offset from original
        for (int i = 0; i < particles.Count; i++)
        {
            Vector3 target = currentCenter + originalOffsets[i];
            Vector3 displacement = target - particles[i].position;

            // Add elastic force (like a spring)
            Vector3 restorativeForce = displacement * shapeRestoreStrength;
            particles[i].velocity += restorativeForce * Time.deltaTime;
        }
    }


}
