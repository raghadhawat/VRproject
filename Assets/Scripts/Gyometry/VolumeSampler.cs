using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class VolumeSampler : MonoBehaviour
{
    [Header("Sampling Settings")]
    [Tooltip("Approximate voxel spacing (local-space units). The grid will be adjusted to fit bounds evenly.")]
    public float voxelSize = 0.2f;
    [Tooltip("Maximum number of divisions along any axis to avoid extremely large loops.")]
    public int maxSamplesPerAxis = 50;
    [Tooltip("Automatically sample interior in Start(). Otherwise, use context menu 'Sample Volume'.")]
    public bool autoSampleOnStart = true;

    [Header("Sampling Results (read-only)")]
    [Tooltip("Interior points in local-space coordinates.")]
    public List<Vector3> InteriorLocalPoints = new List<Vector3>();
    [Tooltip("Interior points in world-space coordinates.")]
    public List<Vector3> InteriorWorldPoints = new List<Vector3>();
    [Tooltip("Mapping from integer grid coords (x,y,z) to index in InteriorLocalPoints/InteriorWorldPoints.")]
    public Dictionary<Vector3Int, int> InteriorGridIndices = new Dictionary<Vector3Int, int>();

    // Cached mesh data (local-space)
    private Vector3[] localVerts;
    private int[] meshTriangles;
    private Bounds localBounds;

    void Start()
    {
        if (autoSampleOnStart)
            SampleVolume();
    }

    /// <summary>
    /// Context-menu in Inspector: click the ⋮ on this component and choose "Sample Volume".
    /// </summary>
    [ContextMenu("Sample Volume")]
    public void SampleVolume()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("VolumeSampler: MeshFilter or mesh missing.");
            return;
        }

        Mesh mesh = mf.sharedMesh;
        localVerts = mesh.vertices;
        meshTriangles = mesh.triangles;
        localBounds = mesh.bounds; // in local space

        InteriorLocalPoints.Clear();
        InteriorWorldPoints.Clear();
        InteriorGridIndices.Clear();

        Vector3 min = localBounds.min;
        Vector3 max = localBounds.max;
        Vector3 size = localBounds.size;

        if (voxelSize <= 0f)
        {
            Debug.LogError("VolumeSampler: voxelSize must be > 0.");
            return;
        }

        // Determine how many divisions along each axis, clamped by maxSamplesPerAxis
        int divX = Mathf.FloorToInt(size.x / voxelSize);
        int divY = Mathf.FloorToInt(size.y / voxelSize);
        int divZ = Mathf.FloorToInt(size.z / voxelSize);
        divX = Mathf.Clamp(divX, 1, maxSamplesPerAxis);
        divY = Mathf.Clamp(divY, 1, maxSamplesPerAxis);
        divZ = Mathf.Clamp(divZ, 1, maxSamplesPerAxis);

        // Compute adjusted spacing so grid fits within bounds evenly
        float stepX = size.x / divX;
        float stepY = size.y / divY;
        float stepZ = size.z / divZ;

        int count = 0;
        // Loop over grid indices 0..divX-1, etc.
        for (int xi = 0; xi < divX; xi++)
        {
            // local x-coordinate centered in the cell
            float x = min.x + (xi + 0.5f) * stepX;
            for (int yi = 0; yi < divY; yi++)
            {
                float y = min.y + (yi + 0.5f) * stepY;
                for (int zi = 0; zi < divZ; zi++)
                {
                    float z = min.z + (zi + 0.5f) * stepZ;
                    Vector3 localP = new Vector3(x, y, z);

                    if (IsPointInsideMeshLocal(localP))
                    {
                        InteriorLocalPoints.Add(localP);
                        InteriorWorldPoints.Add(transform.TransformPoint(localP));
                        Vector3Int key = new Vector3Int(xi, yi, zi);
                        InteriorGridIndices[key] = InteriorLocalPoints.Count - 1;
                        count++;
                    }
                }
            }
        }
        Debug.Log($"VolumeSampler: sampled {count} interior points (grid {divX}×{divY}×{divZ}, step ({stepX:F3},{stepY:F3},{stepZ:F3})).");
    }

    /// <summary>
    /// Ray-based inside test in local space. Casts ray from slightly offset origin along +X.
    /// </summary>
    private bool IsPointInsideMeshLocal(Vector3 localP)
    {
        // Ray direction (local space)
        Vector3 dir = Vector3.right;
        // Offset origin slightly along dir to avoid starting exactly on surface
        const float epsilon = 1e-4f;
        Vector3 origin = localP + dir * epsilon;

        int hitCount = 0;
        // Brute-force over triangles; for large meshes consider acceleration (BVH).
        for (int i = 0; i < meshTriangles.Length; i += 3)
        {
            Vector3 v0 = localVerts[meshTriangles[i]];
            Vector3 v1 = localVerts[meshTriangles[i + 1]];
            Vector3 v2 = localVerts[meshTriangles[i + 2]];
            if (IntersectRayTriangle(origin, dir, v0, v1, v2, out float t))
            {
                if (t > 0f)
                    hitCount++;
            }
        }
        // Odd = inside
        return (hitCount & 1) == 1;
    }

    /// <summary>
    /// Möller–Trumbore ray-triangle intersection (local space).
    /// Returns true if intersects, and sets t >= 0.
    /// </summary>
    private bool IntersectRayTriangle(Vector3 origin, Vector3 dir, Vector3 v0, Vector3 v1, Vector3 v2, out float t)
    {
        t = 0f;
        Vector3 e1 = v1 - v0;
        Vector3 e2 = v2 - v0;
        Vector3 p = Vector3.Cross(dir, e2);
        float det = Vector3.Dot(e1, p);
        if (Mathf.Abs(det) < 1e-6f) return false;
        float invDet = 1f / det;
        Vector3 T = origin - v0;
        float u = Vector3.Dot(T, p) * invDet;
        if (u < 0f || u > 1f) return false;
        Vector3 q = Vector3.Cross(T, e1);
        float v = Vector3.Dot(dir, q) * invDet;
        if (v < 0f || u + v > 1f) return false;
        t = Vector3.Dot(e2, q) * invDet;
        return t >= 0f;
    }
}
