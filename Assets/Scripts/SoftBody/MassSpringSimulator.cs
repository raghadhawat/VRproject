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

        // Wait until the insidePoints are ready
        while (!sampler.IsReady)
        {
            yield return null;
        }

        // Initialize spring system from voxel interior points
        springSystem.InitializeFromPoints(sampler.insidePoints, springRestLength: 0.2f, connectRadius: 0.3f);
        Debug.Log("Mass-spring system initialized from voxel data.");
    }

    void Update()
    {
        Simulate();
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
    }

    void OnDrawGizmos()
    {
        if (springSystem == null || springSystem.particles == null || springSystem.springs == null)
            return;

        Gizmos.color = Color.yellow;
        foreach (Particle p in springSystem.particles)
        {
            Gizmos.DrawSphere(p.position, 0.03f);
        }

        Gizmos.color = Color.gray;
        foreach (Spring s in springSystem.springs)
        {
            Gizmos.DrawLine(s.a.position, s.b.position);
        }
    }
}
