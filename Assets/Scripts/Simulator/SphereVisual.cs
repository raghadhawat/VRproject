// using UnityEngine;
// using System.Collections;
// using System.Collections.Generic;

// [RequireComponent(typeof(MeshFilter))]
// [RequireComponent(typeof(MeshRenderer))]
// [RequireComponent(typeof(TriangleExtractor))]
// [RequireComponent(typeof(VolumeSampler))]
// public class ProceduralSphere : MonoBehaviour
// {
//     [Range(0, 10)]
//     public int resolution = 15;

//     public float springStiffness = 100f;
//     public float damping = 5f;

//     public MassSpringSystem massSpringSystem;

//     private MeshFilter meshFilter;
//     private TriangleExtractor triangleExtractor;
//     private VolumeSampler volumeSampler;

//     void Awake()
//     {
//         meshFilter = GetComponent<MeshFilter>();
//         triangleExtractor = GetComponent<TriangleExtractor>();
//         volumeSampler = GetComponent<VolumeSampler>();
//     }

//     IEnumerator Start()
//     {
//         // Step 1: Generate the procedural mesh
//         Mesh mesh = SebStuff.SphereGenerator.GenerateSphereMesh(resolution);
//         meshFilter.mesh = mesh;

//         Debug.Log($"✅ Procedural sphere mesh created with {mesh.vertexCount} vertices.");

//         // Step 2: Extract triangles
//      //   triangleExtractor.ExtractTriangles();

//         // Step 3: Sample volume
//        // volumeSampler.SampleVolume(triangleExtractor.triangles);

//         // Step 4: Wait one frame to ensure sampling is done (if needed for stability)
//         yield return null;

//         if (!volumeSampler.IsReady)
//         {
//             Debug.LogError("❌ Volume sampling failed.");
//             yield break;
//         }

//         // Step 5: Get surface points
//         List<Vector3> surfacePoints = new List<Vector3>();
//         foreach (var v in mesh.vertices)
//         {
//             surfacePoints.Add(transform.TransformPoint(v)); // Convert to world space
//         }

//         // Step 6: Combine surface + interior points
//         List<Vector3> combinedPoints = new List<Vector3>();
//         combinedPoints.AddRange(surfacePoints);
//         combinedPoints.AddRange(volumeSampler.insidePoints); // Already world space

//         // Step 7: Initialize mass-spring system
//         massSpringSystem = GetComponent<MassSpringSystem>();

//         massSpringSystem.InitializeFromPoints(combinedPoints, springStiffness, damping);

//         Debug.Log("✅ Mass-spring system initialized with surface and volume points.");
//     }
// }
