using System.Collections.Generic;
using UnityEngine;

public class MassSpringSimulator : MonoBehaviour
{
    public MassSpringSystem springSystem;

    public float gravity = -9.81f;
    public float timeStep = 0.02f;
    public float particleMass = 1f;

    private Mesh deformingMesh;
    private Vector3[] originalVertices;
    private Vector3[] deformedVertices;

    void Start()
    {
        if (springSystem == null)
        {
            springSystem = GetComponent<MassSpringSystem>();
            if (springSystem == null)
            {
                Debug.LogError("No MassSpringSystem found!");
                enabled = false;
                return;
            }
        }

        MeshFilter filter = GetComponent<MeshFilter>();
        deformingMesh = filter.mesh;

        originalVertices = deformingMesh.vertices;
        deformedVertices = new Vector3[originalVertices.Length];
    }

    void Update()
    {
        Simulate();
        UpdateMesh();
    }

    void Simulate()
    {
        // Apply spring forces
        foreach (Spring s in springSystem.springs)
        {
            s.ApplyForce();
        }

        // Apply gravity and integrate motion
        foreach (Particle p in springSystem.particles)
        {
            Vector3 force = new Vector3(0, gravity * particleMass, 0);
            p.velocity += force / particleMass * timeStep;
            p.position += p.velocity * timeStep;
        }
for (int i = 0; i < Mathf.Min(3, springSystem.particles.Count); i++)
{
    Debug.Log($"Particle[{i}] Pos: {springSystem.particles[i].position}  Vel: {springSystem.particles[i].velocity}");
}

    }

    void UpdateMesh()
    {
        // Map updated particle positions back to mesh vertices
        for (int i = 0; i < springSystem.particles.Count; i++)
        {
            deformedVertices[i] = transform.InverseTransformPoint(springSystem.particles[i].position);
        }

        deformingMesh.vertices = deformedVertices;
        deformingMesh.RecalculateNormals();
    }
}
