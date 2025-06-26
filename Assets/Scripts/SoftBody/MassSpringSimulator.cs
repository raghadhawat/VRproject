// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// [RequireComponent(typeof(MassSpringSystem))]
// [RequireComponent(typeof(VolumeSampler))]
// public class MassSpringSimulator : MonoBehaviour
// {
//     public MassSpringSystem springSystem;

//     public float gravity = -9.81f;
//     public float timeStep = 0.02f;
//     public float particleMass = 0.3f;

//     public float restVolume = 1f;
//     private Mesh deformingMesh;
//     private Vector3[] originalVertices;
//     private Vector3[] deformedVertices;

//     private MeshVertexBinder binder;

//     void Start()
//     {
//         StartCoroutine(WaitForVolumeData());
//     }

//     IEnumerator WaitForVolumeData()
//     {
//         springSystem = GetComponent<MassSpringSystem>();
//         if (springSystem == null)
//         {
//             Debug.LogError("No MassSpringSystem found!");
//             yield break;
//         }

//         VolumeSampler sampler = GetComponent<VolumeSampler>();
//         if (sampler == null)
//         {
//             Debug.LogError("No VolumeSampler found!");
//             yield break;
//         }

//         while (!sampler.IsReady)
//             yield return null;

//         // 1. Initialize springs from volume points
//         springSystem.InitializeFromPoints(sampler.insidePoints, springRestLength: 0.2f, connectRadius: 0.3f);
       
//         Debug.Log("Mass-spring system initialized from voxel data.");

//         // 2. Set up mesh reference
//         MeshFilter filter = GetComponent<MeshFilter>();
//         deformingMesh = filter.mesh;
//         originalVertices = deformingMesh.vertices;
//         deformedVertices = new Vector3[originalVertices.Length];

//         // 3. Bind surface vertices to nearest particle
//         binder = new MeshVertexBinder(springSystem.particles);
//         binder.Bind(originalVertices, transform);
//     }

//     void Update()
//     {
//         Simulate();
//         UpdateMesh();
//     }

// void Simulate()
// {
//     foreach (Spring s in springSystem.springs)
//     {
//         s.ApplyForce();
//     }

//     foreach (Particle p in springSystem.particles)
//     {
//         Vector3 gravityForce = new Vector3(0, gravity * particleMass, 0);
//         p.velocity += gravityForce / particleMass * timeStep;

//         p.position += p.velocity * timeStep;

//         float floorY = 0.1f;
//         if (p.position.y < floorY)
//         {
//             p.position.y = floorY;

//             p.velocity.y *= -0.5f;  
//         }
//     }
// }


//     void UpdateMesh()
//     {
//         if (binder == null || deformingMesh == null)
//             return;

//         deformedVertices = binder.GetUpdatedVertices(transform);
//         deformingMesh.vertices = deformedVertices;
//         deformingMesh.RecalculateNormals();
//     }

//     void OnDrawGizmos()
//     {
//         if (springSystem == null || springSystem.particles == null || springSystem.springs == null)
//             return;

//         Gizmos.color = Color.yellow;
//         foreach (Particle p in springSystem.particles)
//             Gizmos.DrawSphere(p.position, 0.03f);

//         Gizmos.color = Color.gray;
//         foreach (Spring s in springSystem.springs)
//             Gizmos.DrawLine(s.a.position, s.b.position);
//     }
// }
