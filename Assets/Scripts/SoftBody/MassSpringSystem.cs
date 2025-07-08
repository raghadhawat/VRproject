using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(VolumeSampler))]
public class MassSpringSystem : MonoBehaviour
{
    [Header("Simulation Toggles")]
    public bool isDeforming = false;
    [Header("Gravity")]
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    [Header("Mass-Spring Parameters")]
    public float pointMass = 1f;
    [Tooltip("Spring stiffness (Hooke's k)")]
    public float stiffness = 10f;
    [Tooltip("Spring damping")]
    public float damping = 5f;
    [Header("Ground Collision")]
    public float groundY = 0f;
    public float groundStiffness = 200f;
    [Range(0, 1)] public float groundDamping = 0.5f;
    [Range(0, 1)] public float groundFriction = 0.8f;
    [Header("Integration")]
    [Tooltip("Number of substeps per frame")]
    public int substeps = 32;
    [Tooltip("Scale simulation speed (0–1)")]
    [Range(0.01f, 1f)]
    public float timeScale = 0.5f;
    [Tooltip("Max position magnitude before abort")]

    [Header("Visualization Settings")]
    public bool showInnerPoints = true;
    public bool showSurfacePoints = false;
    public bool showSurfaceSprings = false;
    public bool showInnerSprings = true;
    public bool showWeldingSprings = false;
    public float gizmoSize = 0.01f;
    public float maxPosMag = 50f;
    public AABB aabb;
    public OctreeNode octreeRoot;




    // Internal structures
    private struct MassPoint
    {
        public Vector3 pos, vel, force;
        public float mass;
        public MassPoint(Vector3 _p, float m)
        {
            pos = _p; vel = force = Vector3.zero; mass = m;
        }
    }
    private struct Spring { public int a, b; public float restLen; public Spring(int a, int b, float r) { this.a = a; this.b = b; restLen = r; } }

    // References & data
    private VolumeSampler sampler;
    private MeshFilter mf;
    private Vector3[] localVerts;
    private int[] meshTris;

    private List<MassPoint> mps;
    private List<Spring> springs;
    private Dictionary<int, int> surfMap;
    private Dictionary<Vector3Int, int> intMap;
    private HashSet<(int, int)> existingSpringSet;

    public List<Vector3> sampledSurfacePoints; // injected from TriangleExtractor
    private List<int> sampledSurfaceIndices = new List<int>(); // holds indices in mps of added sampled surface points



    void Start()
    {
        // Existing setup
        sampler = GetComponent<VolumeSampler>();
        mf = GetComponent<MeshFilter>();

        if (sampler.InteriorLocalPoints.Count == 0)
            sampler.SampleVolume();

        TriangleExtractor extractor = GetComponent<TriangleExtractor>();
        if (extractor != null)
        {
            sampledSurfacePoints = extractor.SampleSurfacePoints(extractor.sampleSpacing);
        }
        Mesh m = mf.mesh;
        localVerts = m.vertices;
        meshTris = m.triangles;

        BuildMassPoints();
        BuildSprings();
        AdjustGroundY();

        // 👇 Throw object (set only on one object)
        if (this.name == "Cube")
            SetInitialVelocity(new Vector3(0f, 0f, 0f)); // upward and forward

    }


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            isDeforming = !isDeforming;

        if (!isDeforming) return;

        float dt = Time.deltaTime * timeScale;
        dt = dt / Mathf.Max(1, substeps);
        for (int i = 0; i < substeps; i++)
            if (!SimStep(dt)) break;

        UpdateMeshVerts();
    }

    void BuildMassPoints()
    {
        mps = new List<MassPoint>();
        surfMap = new Dictionary<int, int>();
        intMap = new Dictionary<Vector3Int, int>();

        // surface
        for (int i = 0; i < localVerts.Length; i++)
        {
            var pW = transform.TransformPoint(localVerts[i]);
            surfMap[i] = mps.Count;
            mps.Add(new MassPoint(pW, pointMass));
        }
        HashSet<Vector3Int> uniquePoints = new HashSet<Vector3Int>();
        float snap = 0.01f;

        for (int i = 0; i < sampledSurfacePoints.Count; i++)
        {
            var worldPos = sampledSurfacePoints[i];
            Vector3Int key = Vector3Int.RoundToInt(worldPos / snap);

            if (uniquePoints.Add(key))
            {
                int addedIndex = mps.Count;
                mps.Add(new MassPoint(worldPos, pointMass));
                sampledSurfaceIndices.Add(addedIndex); // track index
            }
        }
        Debug.Log($"Sampled surface particles added: {sampledSurfaceIndices.Count}");


        // interior
        for (int i = 0; i < sampler.InteriorWorldPoints.Count; i++)
        {
            var pW = sampler.InteriorWorldPoints[i];
            var key = new Vector3Int();
            foreach (var kv in sampler.InteriorGridIndices)
                if (kv.Value == i) { key = kv.Key; break; }
            intMap[key] = mps.Count;
            mps.Add(new MassPoint(pW, pointMass));
        }
    }
    void ConnectSampledSurfaceSprings(float connectRadius = 0.25f)
    {
        if (sampledSurfacePoints == null || sampledSurfacePoints.Count == 0)
            return;

        int startIdx = localVerts.Length; // where sampled particles start
        int endIdx = startIdx + sampledSurfacePoints.Count;

        for (int i = 0; i < sampledSurfaceIndices.Count; i++)
        {
            for (int j = i + 1; j < sampledSurfaceIndices.Count; j++)
            {
                int idxA = sampledSurfaceIndices[i];
                int idxB = sampledSurfaceIndices[j];
                float dist = Vector3.Distance(mps[idxA].pos, mps[idxB].pos);
                if (dist <= connectRadius)
                {
                    var key = idxA < idxB ? (idxA, idxB) : (idxB, idxA);
                    if (existingSpringSet.Add(key))
                        springs.Add(new Spring(idxA, idxB, dist));
                }
            }
        }

    }


    void BuildSprings()
    {
        springs = new List<Spring>();
        var seen = new HashSet<(int, int)>();
        existingSpringSet = new HashSet<(int, int)>();

        void AddEdge(int a, int b)
        {
            if (a == b) return;
            var key = a < b ? (a, b) : (b, a);
            if (existingSpringSet.Add(key)) // 👈 Use the hash set
            {
                float rst = Vector3.Distance(mps[a].pos, mps[b].pos);
                springs.Add(new Spring(a, b, rst));
            }
        }


        // surface edges
        for (int i = 0; i < meshTris.Length; i += 3)
        {
            int vertex1 = surfMap[meshTris[i]],
                vertex2 = surfMap[meshTris[i + 1]],
                vertex3 = surfMap[meshTris[i + 2]];
            AddEdge(vertex1, vertex2);
            AddEdge(vertex2, vertex3);
            AddEdge(vertex3, vertex1);
        }

        // Generate all 26 neighbor directions
        List<Vector3Int> neighborDirections = new List<Vector3Int>();
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && y == 0 && z == 0) continue;
                    neighborDirections.Add(new Vector3Int(x, y, z));
                }

        // Connect each interior point to all 26 neighbors
        foreach (var kv in sampler.InteriorGridIndices)
        {
            var k = kv.Key;
            int idx = intMap[k];

            foreach (var dir in neighborDirections)
            {
                var nk = k + dir;
                if (intMap.TryGetValue(nk, out int j))
                {
                    AddEdge(idx, j);
                }
            }
        }

        // surface-interior welds
        Bounds b = mf.mesh.bounds;
        Vector3 step = new Vector3(
            b.size.x / sampler.InteriorGridIndices.Keys.Max(k => k.x),
            b.size.y / sampler.InteriorGridIndices.Keys.Max(k => k.y),
            b.size.z / sampler.InteriorGridIndices.Keys.Max(k => k.z)
        );

        foreach (var kv in surfMap)
        {
            var lv = localVerts[kv.Key];
            var gi = new Vector3Int(
                Mathf.RoundToInt((lv.x - b.min.x) / step.x),
                Mathf.RoundToInt((lv.y - b.min.y) / step.y),
                Mathf.RoundToInt((lv.z - b.min.z) / step.z)
            );

            // Connect to 3x3x3 neighborhood
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        var nk = gi + new Vector3Int(x, y, z);
                        if (intMap.TryGetValue(nk, out int idxI))
                        {
                            AddEdge(kv.Value, idxI);
                        }
                    }
        }
        ConnectSampledSurfaceSprings(0.3f);

        ConnectSampledSurfaceToInterior();
    }
    void ConnectSampledSurfaceToInterior(float weldRadius = 0.3f)
    {
        if (sampledSurfaceIndices == null || sampledSurfaceIndices.Count == 0 || intMap == null)
            return;

        // Calculate voxel step size
        Bounds b = mf.mesh.bounds;
        Vector3Int maxIndices = Vector3Int.zero;
        foreach (var key in sampler.InteriorGridIndices.Keys)
            maxIndices = Vector3Int.Max(maxIndices, key);

        Vector3 step = new Vector3(
            b.size.x / (maxIndices.x == 0 ? 1 : maxIndices.x),
            b.size.y / (maxIndices.y == 0 ? 1 : maxIndices.y),
            b.size.z / (maxIndices.z == 0 ? 1 : maxIndices.z)
        );

        foreach (int surfaceIdx in sampledSurfaceIndices)
        {
            Vector3 worldPos = mps[surfaceIdx].pos;
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            Vector3Int gridIndex = new Vector3Int(
                Mathf.RoundToInt((localPos.x - b.min.x) / step.x),
                Mathf.RoundToInt((localPos.y - b.min.y) / step.y),
                Mathf.RoundToInt((localPos.z - b.min.z) / step.z)
            );

            float closestDist = float.MaxValue;
            int closestInterior = -1;

            // Find closest interior point in 3×3×3 neighborhood
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector3Int neighbor = gridIndex + new Vector3Int(x, y, z);
                        if (intMap.TryGetValue(neighbor, out int interiorIdx))
                        {
                            float dist = Vector3.Distance(mps[surfaceIdx].pos, mps[interiorIdx].pos);
                            if (dist <= weldRadius && dist < closestDist)
                            {
                                closestDist = dist;
                                closestInterior = interiorIdx;
                            }
                        }
                    }

            // Add only the closest spring
            if (closestInterior != -1)
            {
                var key = surfaceIdx < closestInterior ? (surfaceIdx, closestInterior) : (closestInterior, surfaceIdx);
                if (existingSpringSet.Add(key))
                    springs.Add(new Spring(surfaceIdx, closestInterior, closestDist));
            }
        }
    }

    void AdjustGroundY()
    {
        float minY = float.MaxValue;
        foreach (var p in mps)
            if (p.pos.y < minY) minY = p.pos.y;
        if (groundY > minY)
            groundY = minY - 0.01f;
    }

    bool SimStep(float dt)
    {
        int n = mps.Count;
        float maxDisplacement = 10f;
        float maxVel = 50f;

        // 1. Reset forces to gravity
        for (int i = 0; i < n; i++)
        {
            var p = mps[i];
            p.force = gravity * p.mass;
            mps[i] = p;
        }

        // 2. Apply spring + damping forces
        foreach (var s in springs)
        {
            var A = mps[s.a]; var B = mps[s.b];
            var delta = A.pos - B.pos;
            float dist = delta.magnitude;
            if (dist < 1e-6f) continue;

            var dir = delta / dist;
            float ext = dist - s.restLen;

            var fSpring = -stiffness * ext * dir;
            var fDamp = -damping * Vector3.Dot(A.vel - B.vel, dir) * dir;
            var fTotal = fSpring + fDamp;

            A.force += fTotal;
            B.force -= fTotal;

            mps[s.a] = A;
            mps[s.b] = B;
        }

        // 3. Integrate (semi-implicit Euler) and handle collisions
        for (int i = 0; i < n; i++)
        {
            var p = mps[i];

            // Skip invalid data
            if (!IsFiniteVector(p.pos) || p.pos.magnitude > maxDisplacement)
            {
                Debug.LogError($"Unstable: point {i} at {p.pos} -- frozen.");
                p.vel = Vector3.zero;
                p.force = Vector3.zero;
                continue;
            }

            // Semi-implicit Euler: vel first
            var acc = p.force / p.mass;
            p.vel += dt * acc;

            // Clamp velocity
            if (p.vel.magnitude > maxVel)
            {
                p.vel = p.vel.normalized * maxVel;
                Debug.LogWarning($"Velocity capped at point {i}");
            }

            p.pos += dt * p.vel;


            mps[i] = p;
        }
        ComputeAABB();
        BuildOctree();

        return true;
    }

    bool IsFiniteVector(Vector3 v)
    {
        return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }

    void UpdateMeshVerts()
    {
        var mesh = new Mesh();
        var vs = new Vector3[localVerts.Length];
        for (int i = 0; i < localVerts.Length; i++)
        {
            vs[i] = transform.InverseTransformPoint(mps[surfMap[i]].pos);
            if (!IsFiniteVector(vs[i])) vs[i] = Vector3.zero; // emergency fallback
        }
        mesh.vertices = vs;
        mesh.triangles = mf.mesh.triangles;
        mesh.uv = mf.mesh.uv;
        mesh.tangents = mf.mesh.tangents;
        mesh.colors = mf.mesh.colors;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.mesh = mesh;
    }

    void OnDrawGizmos()
    {

        if (mps == null || springs == null) return;

        // Total number of original surface mesh vertices
        int originalSurfaceCount = localVerts.Length;
        int sampledSurfaceStart = originalSurfaceCount;
        int sampledSurfaceEnd = sampledSurfaceStart + (sampledSurfacePoints?.Count ?? 0);

        // Draw springs with appropriate colors
        foreach (var s in springs)
        {
            bool aIsOrigSurf = s.a < originalSurfaceCount;
            bool bIsOrigSurf = s.b < originalSurfaceCount;
            bool aIsSampledSurf = s.a >= sampledSurfaceStart && s.a < sampledSurfaceEnd;
            bool bIsSampledSurf = s.b >= sampledSurfaceStart && s.b < sampledSurfaceEnd;

            if ((aIsOrigSurf || aIsSampledSurf) && (bIsOrigSurf || bIsSampledSurf) && showSurfaceSprings)
            {
                // Gray for any surface-to-surface spring
                Gizmos.color = Color.red;
                Gizmos.DrawLine(mps[s.a].pos, mps[s.b].pos);
            }
            else if (!aIsOrigSurf && !aIsSampledSurf && !bIsOrigSurf && !bIsSampledSurf && showInnerSprings)
            {
                // Inner-to-inner springs
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(mps[s.a].pos, mps[s.b].pos);
            }
            else if (showWeldingSprings)
            {
                // Surface-to-interior springs
                Gizmos.color = new Color(1f, 0.5f, 0f); // orange
                Gizmos.DrawLine(mps[s.a].pos, mps[s.b].pos);
            }
        }

        // Draw particles
        for (int i = 0; i < mps.Count; i++)
        {
            if (i < originalSurfaceCount && showSurfacePoints)
            {
                Gizmos.color = Color.red; // Original mesh surface
                Gizmos.DrawSphere(mps[i].pos, gizmoSize);
            }
            else if (i >= sampledSurfaceStart && i < sampledSurfaceEnd && showSurfacePoints)
            {
                Gizmos.color = Color.yellow; // Sampled surface points
                Gizmos.DrawSphere(mps[i].pos, gizmoSize * 0.9f);
            }
            else if (showInnerPoints)
            {
                Gizmos.color = Color.cyan; // Inner particles
                Gizmos.DrawSphere(mps[i].pos, gizmoSize * 0.8f);
            }
        }
        for (int i = 0; i < mps.Count; i++)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(mps[i].pos, gizmoSize);
        }


        // if (octreeRoot != null)
        // {
        //     DrawOctreeNode(octreeRoot);
        // }
    }
    void DrawOctreeNode(OctreeNode node)
    {
        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.2f); // light yellow
        Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);


        if (node.children != null)
        {
            foreach (var child in node.children)
                DrawOctreeNode(child);
        }
    }
    public void ComputeAABB()
    {
        if (mps == null || mps.Count == 0)
            return;

        Vector3 min = mps[0].pos;
        Vector3 max = mps[0].pos;

        foreach (var p in mps)
        {
            min = Vector3.Min(min, p.pos);
            max = Vector3.Max(max, p.pos);
        }

        aabb = new AABB(min, max);
    }
    public List<Vector3> GetAllParticlePositions()
    {
        List<Vector3> positions = new List<Vector3>();
        foreach (var p in mps)
            positions.Add(p.pos);
        return positions;
    }
    public void BuildOctree()
    {
        if (mps == null || mps.Count == 0) return;


        Vector3 min = mps[0].pos;
        Vector3 max = mps[0].pos;

        foreach (var p in mps)
        {
            min = Vector3.Min(min, p.pos);
            max = Vector3.Max(max, p.pos);
        }

        Bounds bounds = new Bounds((min + max) / 2f, max - min);
        octreeRoot = new OctreeNode(bounds);

        foreach (var p in mps)
        {
            octreeRoot.Insert(p.pos);
        }
        //  Debug.Log($"{gameObject.name} Octree built with {mps.Count} particles.");
    }
    public int GetParticleCount()
    {
        return mps.Count;
    }

    public Vector3 GetParticlePosition(int index)
    {
        return mps[index].pos;
    }
    public int GetParticleIndex(Vector3 pos)
    {
        for (int i = 0; i < mps.Count; i++)
        {
            if (mps[i].pos == pos) return i;
        }
        return -1;
    }
    public struct ParticleData
    {
        public Vector3 position;
        public Vector3 velocity;
        public float mass;


        public ParticleData(Vector3 pos, Vector3 vel, float m)
        {
            position = pos;
            velocity = vel;
            mass = m;
        }
    }

    public ParticleData GetParticle(int i)
    {
        var p = mps[i];
        return new ParticleData(p.pos, p.vel, p.mass);
    }
    public void OffsetParticlePosition(int i, Vector3 offset)
    {
        var p = mps[i];
        p.pos += offset;
        mps[i] = p;
    }
    public void ApplyImpulse(int i, Vector3 impulse)
    {
        var p = mps[i];
        p.vel += impulse / p.mass;
        mps[i] = p;
    }
    public void SetInitialVelocity(Vector3 velocity)
    {
        for (int i = 0; i < mps.Count; i++)
        {
            var p = mps[i];
            p.vel += velocity;
            mps[i] = p;
        }
    }
}
