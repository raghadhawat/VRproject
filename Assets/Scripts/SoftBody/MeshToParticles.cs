using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class MeshToParticles : MonoBehaviour
{
public float defaultMass = 1f;
public List<Particle> particles = new List<Particle>();

void Start()
{
    Mesh mesh = GetComponent<MeshFilter>().mesh;
    Vector3[] vertices = mesh.vertices;

    foreach (var v in vertices)
    {
        Vector3 worldPos = transform.TransformPoint(v);
        particles.Add(new Particle(worldPos, defaultMass));
    }

    Debug.Log($"Created {particles.Count} particles from mesh.");
}
}