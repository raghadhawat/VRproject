using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class MassSpringSystem : MonoBehaviour
{
    public float stiffness = 100f;  
    public float damping = 1f;
    public List<Particle> particles = new List<Particle>();
    public List<Spring> springs = new List<Spring>();

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

        Debug.Log($"Created {particles.Count} particles and {springs.Count} springs.");
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
}
