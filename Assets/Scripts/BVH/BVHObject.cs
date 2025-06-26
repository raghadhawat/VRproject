using System.Collections.Generic;
using UnityEngine;

public class BVHObject : MonoBehaviour
{
    public List<AABB> BoundingBoxes = new List<AABB>();
    public List<AABB> AllBoxes;
    public bool isON = false;
    [SerializeField] public int subdivisions = 10; // You can adjust this for more/less accuracy
    public void UpdateBoundingBoxes()
    {
        BoundingBoxes.Clear();

        // Assuming the object has a Renderer component
        Renderer renderer = GetComponent<Renderer>();
        if (renderer)
        {
            Bounds bounds = renderer.bounds;
            Vector3 distance = (bounds.max - bounds.min) / subdivisions;
            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();

            if (meshFilter)
            {
                Mesh mesh = meshFilter.mesh;
                Vector3[] vertices = mesh.vertices;
                Transform transform = renderer.transform;

                for (int x = 0; x < subdivisions; x++)
                {
                    for (int y = 0; y < subdivisions; y++)
                    {
                        for (int z = 0; z < subdivisions; z++)
                        {
                            Vector3 subMin = new Vector3(
                                bounds.min.x + x * distance.x,
                                bounds.min.y + y * distance.y,
                                bounds.min.z + z * distance.z
                            );

                            Vector3 subMax = new Vector3(
                                bounds.min.x + (x + 1) * distance.x,
                                bounds.min.y + (y + 1) * distance.y,
                                bounds.min.z + (z + 1) * distance.z
                            );

                            AABB subAABB = new AABB(subMin, subMax);
                            // MassSpringSystem massSpringSystem =  new MassSpringSystem();
                            // Vector3[] massPointsPosition = massSpringSystem.UpdateMassPoints();

                            if (SubAABBIntersectsVertices(subAABB, vertices, transform))
                            {
                                BoundingBoxes.Add(subAABB);
                            }
                        }
                    }
                }
            }
        }
    }

    public bool SubAABBIntersectsVertices(AABB aabb, Vector3[] vertices, Transform transform)
    {
        foreach (var vertex in vertices)
        {
            Vector3 worldVertex = transform.TransformPoint(vertex);

            if (aabb.Contains(worldVertex))
            {
                return true;
            }
        }
        return false;
    }
}


// public void UpdateBoundingBoxes()
//     {
//         BoundingBoxes.Clear();

//         // Assuming the object has a Renderer component
//         Renderer renderer = GetComponent<Renderer>();
//         if (renderer)
//         {
//             MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();

//             if (meshFilter)
//             {
//                 Mesh mesh = meshFilter.mesh;
//                 Vector3[] vertices = mesh.vertices;
//                 Transform transform = renderer.transform;

//                 foreach (var vertex in vertices)
//                 {
//                     Vector3 worldVertex = transform.TransformPoint(vertex);
//                     Vector3 min = worldVertex - Vector3.one * (boxSize / 2);
//                     Vector3 max = worldVertex + Vector3.one * (boxSize / 2);
//                     AABB aabb = new AABB(min, max);
//                     BoundingBoxes.Add(aabb);
//                 }
//             }
//         }
//     }