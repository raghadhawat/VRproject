using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MassSpringSystem))]
[RequireComponent(typeof(VolumeSampler))]
public class MassSpringSimulator : MonoBehaviour
{
    public MassSpringSystem springSystem;

    public float gravity = -9.81f;
    public float timeStep = 0.02f;
    public float particleMass = 1f;

    private Mesh deformingMesh;
    private Vector3[] originalVertices;
    private Vector3[] deformedVertices;

    private MeshVertexBinder binder;

    void Start()
    {
        StartCoroutine(WaitForVolumeData());
    }

    IEnumerator WaitForVolumeData()
    {
        springSystem = GetComponent<MassSpringSystem>();
        if (springSystem == null)
        {
            Debug.LogError("No MassSpringSystem found!");
            yield break;
        }

        VolumeSampler sampler = GetComponent<VolumeSampler>();
        if (sampler == null)
        {
            Debug.LogError("No VolumeSampler found!");
            yield break;
        }

        while (!sampler.IsReady)
            yield return null;

        // 1. Initialize springs from volume points
        springSystem.InitializeFromPoints(sampler.insidePoints, springRestLength: 0.2f, connectRadius: 0.3f);
        Debug.Log("Mass-spring system initialized from voxel data.");

        // 2. Set up mesh reference
        MeshFilter filter = GetComponent<MeshFilter>();
        deformingMesh = filter.mesh;
        originalVertices = deformingMesh.vertices;
        deformedVertices = new Vector3[originalVertices.Length];

        // 3. Bind surface vertices to nearest particle
        binder = new MeshVertexBinder(springSystem.particles);
        binder.Bind(originalVertices, transform);
    }

    void FixedUpdate()
    {
        Simulate();
        UpdateMesh();
    }

   void Simulate()
{
    foreach (Spring s in springSystem.springs)
        s.ApplyForce();

    // Gravity
    foreach (Particle p in springSystem.particles)
    {
        Vector3 force = new Vector3(0, gravity * particleMass, 0);
        p.velocity += force / particleMass * timeStep;
    }
float groundY = 0f;
float restitution = 9f;

foreach (Particle p in springSystem.particles)
{
    // Only apply gravity if particle is above ground
    if (p.position.y > groundY + 0.001f)
    {
        Vector3 gravityForce = new Vector3(0, gravity * particleMass, 0);
        p.velocity += gravityForce / particleMass * timeStep;
    }

    // Integrate motion
    p.position += p.velocity * timeStep;

    // Ground collision response
    if (p.position.y < groundY)
    {
        p.position.y = groundY;

        if (p.velocity.y < 0f)
        {
            p.velocity.y *= -restitution;

            // // Optional: if you want to fully stop motion after touch
            // if (Mathf.Abs(p.velocity.y) < 0.1f)
            // {
            //     p.velocity.y = 0f;
            // }
        }
    }
}


foreach (Particle p in springSystem.particles)
{
    // Apply gravity
    Vector3 force = new Vector3(0, gravity * particleMass, 0);
    p.velocity += force / particleMass * timeStep;

    // Integrate
    p.position += p.velocity * timeStep;

    
}

        // 🧩 Add elastic shape-preserving force
        springSystem.ApplyShapePreservationForces();

    // Integrate motion
    foreach (Particle p in springSystem.particles)
    {
        p.position += p.velocity * timeStep;
    }
}


    void UpdateMesh()
    {
        if (binder == null || deformingMesh == null)
            return;

        deformedVertices = binder.GetUpdatedVertices(transform);
        deformingMesh.vertices = deformedVertices;
        deformingMesh.RecalculateNormals();
    }

    void OnDrawGizmos()
    {
        if (springSystem == null || springSystem.particles == null || springSystem.springs == null)
            return;

        Gizmos.color = Color.yellow;
        foreach (Particle p in springSystem.particles)
            Gizmos.DrawSphere(p.position, 0.03f);

        Gizmos.color = Color.gray;
        foreach (Spring s in springSystem.springs)
            Gizmos.DrawLine(s.a.position, s.b.position);
    }
}
