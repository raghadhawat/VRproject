using System;
using System.Collections.Generic;
using UnityEngine;


public class PBDSimulator : MonoBehaviour
{
    [Range(0f, 1f)] public float stretchStiffness = 0.8f;
    [Range(0f, 1f)] public float volumeStiffness = 1f;

    public float groundFriction = 0.5f;
    [Header("Collision Settings")]
    public float contactSearchRadius = 0.2f;

    public float pointMass = 1f;
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    public int solverIterations = 30;
    public float timeScale = 1f;

    public bool visualizeParticles = true;
    public float gizmoSize = 0.015f;
    public OctreeNode octreeRoot { get; private set; }

    private List<PBDParticle> particles = new List<PBDParticle>();
    private List<DistanceConstraint> stretchConstraints = new List<DistanceConstraint>();
    private List<VolumeConstraint> volumeConstraints = new List<VolumeConstraint>();

    private VolumeSampler sampler;
    private TriangleExtractor extractor;
    private Mesh deformableMesh;
    private Vector3[] originalVertices;
    private int meshVertexStartIndex = -1;


    static readonly Vector3Int[] kNeighborOffsets = {
        new( 0, 0, 0), new( 1, 0, 0), new(-1, 0, 0),
        new( 0, 1, 0), new( 0,-1, 0), new( 0, 0, 1),
        new( 0, 0,-1), new( 1, 1, 0), new(-1,-1, 0),
        new( 1, 0, 1), new(-1, 0,-1), new( 0, 1, 1),
        new( 0,-1,-1), new( 1, 1, 1), new(-1,-1,-1),
        // six more for full 27-cell neighbourhood
        new( 1,-1, 0), new(-1, 1, 0), new( 1, 0,-1),
        new(-1, 0, 1), new( 0, 1,-1), new( 0,-1, 1),
        new( 1,-1,-1), new(-1, 1, 1), new( 1,-1, 1),
        new(-1, 1,-1), new( 0, 0, 0)   // repeated centre keeps length = 27
    };

    static readonly Stack<List<int>> cellListPool = new();         // avoids new/GC
    readonly Dictionary<long, List<int>> grid = new();   

    //   [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static long PackCell(int x, int y, int z)
        => ((long)(uint)x << 42) | ((long)(uint)y << 21) | (uint)z;

    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static long PackCell(Vector3Int c) => PackCell(c.x, c.y, c.z);

    public AABB aabb => GetAABB();

    class ClosestComparer : IComparer<(int index, float dist)>
    {
        public int Compare((int index, float dist) a, (int index, float dist) b)
        {
            int cmp = a.dist.CompareTo(b.dist);
            if (cmp != 0) return cmp;
            return a.index.CompareTo(b.index);
        }
    }
    void Start()
    {
        sampler = GetComponent<VolumeSampler>();
        extractor = GetComponent<TriangleExtractor>();

        BuildParticles();
        BuildStretchConstraints();
        BuildVolumeConstraints();
        ConnectSurfaceToInterior();
        ConnectSurfaceSprings();

        // if (this.name == "Cube (1)")
        //     SetInitialVelocity(new Vector3(2f, 0f, 0f)); // upward and forward
        // if (this.name == "Cube")
        //     SetInitialVelocity(new Vector3(-3f, 0f, 0f));
    }

    void FixedUpdate()
    {
        float dt = Time.deltaTime * timeScale;
        // SimulatePBD(dt);

        if (deformableMesh != null && meshVertexStartIndex >= 0)
        {
            Vector3[] newVerts = new Vector3[originalVertices.Length];
            for (int i = 0; i < newVerts.Length; i++)
            {
                newVerts[i] = transform.InverseTransformPoint(particles[meshVertexStartIndex + i].pos);
            }
            deformableMesh.vertices = newVerts;
            deformableMesh.RecalculateNormals();
        }
    }

    void OnDrawGizmos()
    {
        if (!visualizeParticles || particles == null) return;

        foreach (var p in particles)
        {
            Gizmos.color = Color.yellow;

            Gizmos.DrawSphere(p.pos, gizmoSize);
        }

        if (octreeRoot != null)
        {
            DrawOctreeNode(octreeRoot);
        }
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

    void BuildParticles()
    {
        particles.Clear();

        if (sampler.InteriorWorldPoints.Count == 0)
            sampler.SampleVolume();

        List<Vector3> surfacePoints = extractor.SampleSurfacePoints(0.1f);
        foreach (Vector3 p in surfacePoints)
            particles.Add(new PBDParticle(p, pointMass, true));

        foreach (Vector3 p in sampler.InteriorWorldPoints)
            particles.Add(new PBDParticle(p, pointMass, false));

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        Mesh mesh = meshFilter != null ? meshFilter.mesh : null;
        if (mesh == null)
        {
            Debug.LogError("No mesh found on this object.");
            return;
        }

        originalVertices = mesh.vertices;
        deformableMesh = mesh;
        meshVertexStartIndex = particles.Count;

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 worldPos = transform.TransformPoint(originalVertices[i]);
            particles.Add(new PBDParticle(worldPos, pointMass, true));
        }
    }

    void BuildStretchConstraints()
    {
        stretchConstraints.Clear();
        var grid = sampler.InteriorGridIndices;
        var map = new Dictionary<Vector3Int, int>();
        int baseIndex = particles.Count - sampler.InteriorWorldPoints.Count;

        foreach (var kv in grid)
            map[kv.Key] = baseIndex + kv.Value;

        Vector3Int[] offsets = {
            new Vector3Int(1,0,0), new Vector3Int(0,1,0), new Vector3Int(0,0,1),
            new Vector3Int(1,1,0), new Vector3Int(1,0,1), new Vector3Int(0,1,1),
            new Vector3Int(0,-1,0), new Vector3Int(1,-1,0), new Vector3Int(-1,-1,0),
            new Vector3Int(1,1,1), new Vector3Int(-1,1,1), new Vector3Int(1,-1,1)
        };

        foreach (var kv in map)
        {
            Vector3Int a = kv.Key;
            int iA = kv.Value;

            foreach (var offset in offsets)
            {
                Vector3Int b = a + offset;
                if (map.TryGetValue(b, out int iB))
                {
                    float restLen = Vector3.Distance(particles[iA].pos, particles[iB].pos);
                    stretchConstraints.Add(new DistanceConstraint(iA, iB, restLen, stretchStiffness));
                }
            }
        }
    }

    void BuildVolumeConstraints()
    {
        volumeConstraints.Clear();

        var grid = sampler.InteriorGridIndices;
        var map = new Dictionary<Vector3Int, int>();
        foreach (var kv in grid)
            map[kv.Key] = particles.Count - sampler.InteriorWorldPoints.Count + kv.Value;

        foreach (var kv in grid)
        {
            Vector3Int baseIdx = kv.Key;

            Vector3Int[] cornerOffsets = {
                new Vector3Int(0,0,0), new Vector3Int(1,0,0), new Vector3Int(0,1,0), new Vector3Int(1,1,0),
                new Vector3Int(0,0,1), new Vector3Int(1,0,1), new Vector3Int(0,1,1), new Vector3Int(1,1,1),
            };

            bool fullCell = true;
            int[] ids = new int[8];
            for (int i = 0; i < 8; i++)
            {
                Vector3Int key = baseIdx + cornerOffsets[i];
                if (!map.TryGetValue(key, out ids[i]))
                {
                    fullCell = false;
                    break;
                }
            }

            if (!fullCell) continue;

            AddTetra(ids[0], ids[1], ids[3], ids[7]);
            AddTetra(ids[0], ids[3], ids[2], ids[7]);
            AddTetra(ids[0], ids[2], ids[6], ids[7]);
            AddTetra(ids[0], ids[4], ids[6], ids[7]);
            AddTetra(ids[0], ids[5], ids[1], ids[7]);
        }

        void AddTetra(int i0, int i1, int i2, int i3)
        {
            float v = Mathf.Abs(VolumeConstraint.SignedTetrahedronVolume(
                particles[i0].pos, particles[i1].pos, particles[i2].pos, particles[i3].pos));
            volumeConstraints.Add(new VolumeConstraint(i0, i1, i2, i3, v, volumeStiffness));
        }
    }

   public void ConnectSurfaceToInterior(int maxConnections = 3)
{
    // 1. Split particle array into surface / interior
    int surfaceCount  = particles.Count - sampler.InteriorWorldPoints.Count;
    int interiorStart = surfaceCount;
     if (interiorStart >= particles.Count)
        return; 
    // 2. Pre-compute radius²
    float voxelSpacing = sampler.voxelSize;
    float maxRadius    = voxelSpacing * 1.5f;
    float r2           = maxRadius * maxRadius;

    // 3. Build KD-tree over *interior* points
    var interiorPts = new List<(Vector3 pos, int idx)>(particles.Count - interiorStart);
    for (int j = interiorStart; j < particles.Count; j++)
        interiorPts.Add((particles[j].pos, j));

    var kd = new TinyKDTree(interiorPts);

    // 4. Scratch buffer for the k nearest results
    Span<(int idx, float dist2)> buf = stackalloc (int, float)[maxConnections];

    // 5. Query nearest neighbours for every surface particle
    for (int i = 0; i < surfaceCount; i++)
    {
        Vector3 p = particles[i].pos;

        int found = kd.RadialKNearest(p, r2, maxConnections, buf);

        // 6. Emit constraints
        for (int n = 0; n < found; n++)
        {
            var (j, d2) = buf[n];
            stretchConstraints.Add(
                new DistanceConstraint(i, j, Mathf.Sqrt(d2), stretchStiffness));
        }
    }

    // Debug.Log($"[PBD] Connected surface-to-interior springs: {surfaceCount * maxConnections}");
}

    /*  void ConnectSurfaceToInterior(int maxConnections = 3)
      {
          int surfaceCount = particles.Count - sampler.InteriorWorldPoints.Count;
          int interiorStart = surfaceCount;

          float voxelSpacing = sampler.voxelSize;
          float maxRadius = voxelSpacing * 1.5f;
          float maxRadius2 = maxRadius * maxRadius;

          for (int i = 0; i < surfaceCount; i++)
          {
              Vector3 surfacePos = particles[i].pos;

              // --- Three slots for (index, distance) sorted by distance ascending ---
              int   bestIdx0 = -1, bestIdx1 = -1, bestIdx2 = -1;
              float bestDist0 = float.MaxValue,
                    bestDist1 = float.MaxValue,
                    bestDist2 = float.MaxValue;

              for (int j = interiorStart; j < particles.Count; j++)
              {
                  // squared-distance test
                  float d2 = (surfacePos - particles[j].pos).sqrMagnitude;
                  if (d2 > maxRadius2) continue;

                  // only take the real distance when we know it's a contender
                  float d = Mathf.Sqrt(d2);

                  // insert into the sorted top-3 if it belongs
                  if (d < bestDist0)
                  {
                      // shift slot 0 → 1, 1 → 2
                      bestDist2 = bestDist1; bestIdx2 = bestIdx1;
                      bestDist1 = bestDist0; bestIdx1 = bestIdx0;
                      bestDist0 = d;         bestIdx0 = j;
                  }
                  else if (d < bestDist1)
                  {
                      // shift slot 1 → 2
                      bestDist2 = bestDist1; bestIdx2 = bestIdx1;
                      bestDist1 = d;         bestIdx1 = j;
                  }
                  else if (d < bestDist2)
                  {
                      bestDist2 = d;
                      bestIdx2 = j;
                  }
              }

              // Emit up to maxConnections = 3 constraints
              if (bestIdx0 != -1)
                  stretchConstraints.Add(new DistanceConstraint(i, bestIdx0, bestDist0, stretchStiffness));
              if (bestIdx1 != -1)
                  stretchConstraints.Add(new DistanceConstraint(i, bestIdx1, bestDist1, stretchStiffness));
              if (bestIdx2 != -1)
                  stretchConstraints.Add(new DistanceConstraint(i, bestIdx2, bestDist2, stretchStiffness));
          }

          // Debug.Log("[PBD] Connected surface-to-interior springs: " + surfaceCount * maxConnections);
      }

  */
    
    // void ConnectSurfaceSprings(float connectRadius = 0.15f)
    //     {
    //         int surfaceCount = particles.Count - sampler.InteriorWorldPoints.Count;
    //         float cellSize = connectRadius * 1.1f; // Slightly larger than radius
    //         var grid = new Dictionary<Vector3Int, List<int>>();
    //         var added = new HashSet<(int, int)>();

    //         // 1. Insert surface particles into spatial grid
    //         for (int i = 0; i < surfaceCount; i++)
    //         {
    //             Vector3 pos = particles[i].pos;
    //             Vector3Int cell = Vector3Int.FloorToInt(pos / cellSize);

    //             if (!grid.TryGetValue(cell, out var list))
    //             {
    //                 list = new List<int>();
    //                 grid[cell] = list;
    //             }

    //             list.Add(i);
    //         }

    //         // 2. For each surface particle, check nearby cells only
    //         Vector3Int[] neighborOffsets = {
    //         new Vector3Int(0,0,0), new Vector3Int(1,0,0), new Vector3Int(-1,0,0),
    //         new Vector3Int(0,1,0), new Vector3Int(0,-1,0), new Vector3Int(0,0,1),
    //         new Vector3Int(0,0,-1), new Vector3Int(1,1,0), new Vector3Int(-1,-1,0),
    //         new Vector3Int(1,0,1), new Vector3Int(-1,0,-1), new Vector3Int(0,1,1),
    //         new Vector3Int(0,-1,-1), new Vector3Int(1,1,1), new Vector3Int(-1,-1,-1)
    //     };

    //         for (int i = 0; i < surfaceCount; i++)
    //         {
    //             Vector3 posA = particles[i].pos;
    //             Vector3Int baseCell = Vector3Int.FloorToInt(posA / cellSize);

    //             foreach (var offset in neighborOffsets)
    //             {
    //                 Vector3Int neighborCell = baseCell + offset;

    //                 if (grid.TryGetValue(neighborCell, out var candidates))
    //                 {
    //                     foreach (int j in candidates)
    //                     {
    //                         if (j <= i) continue; // avoid duplicates

    //                         float dist = Vector3.Distance(posA, particles[j].pos);
    //                         if (dist <= connectRadius)
    //                         {
    //                             var key = (i, j);
    //                             if (added.Add(key)) // only if not added yet
    //                             {
    //                                 stretchConstraints.Add(new DistanceConstraint(i, j, dist, stretchStiffness));
    //                             }
    //                         }
    //                     }
    //                 }
    //             }
    //         }

    //         // Debug.Log("[PBD] Spatially connected surface-surface springs: " + added.Count);
    //     }
    

    void ConnectSurfaceSprings(float connectRadius = 0.15f)
    {
        // 1. constants & pre-comp 
        int surfaceCount = particles.Count - sampler.InteriorWorldPoints.Count;

        float cellSize = connectRadius;            // guarantee bounded bucket size
        float r2       = connectRadius * connectRadius;

        
        foreach (var list in grid.Values) { list.Clear(); cellListPool.Push(list); }
        grid.Clear();

        for (int i = 0; i < surfaceCount; i++)
        {
            Vector3     p     = particles[i].pos;
            Vector3Int  cell  = Vector3Int.FloorToInt(p / cellSize);
            long        key   = PackCell(cell);

            if (!grid.TryGetValue(key, out var list))
            {
                list = cellListPool.Count > 0 ? cellListPool.Pop() : new List<int>(4);
                grid[key] = list;
            }
            list.Add(i);
        }

        for (int i = 0; i < surfaceCount; i++)
        {
            Vector3    posA   = particles[i].pos;
            Vector3Int baseC  = Vector3Int.FloorToInt(posA / cellSize);
            long       baseK  = PackCell(baseC);

            foreach (Vector3Int off in kNeighborOffsets)
            {
                long key = baseK + PackCell(off.x, off.y, off.z);   // add packed offset

                if (!grid.TryGetValue(key, out var candidates)) continue;

                foreach (int j in candidates)
                {
                    if (j <= i) continue;                          // unordered pair once

                    Vector3 d  = particles[j].pos - posA;
                    float   d2 = d.sqrMagnitude;
                    if (d2 > r2) continue;                         // outside radius

                    float dist = Mathf.Sqrt(d2);                   // only now
                    stretchConstraints.Add(
                        new DistanceConstraint(i, j, dist, stretchStiffness));
                }
            }
        }

        // Debug.Log($"[PBD] spatial springs: {stretchConstraints.Count}");
    }


    public void SimulatePBD(float dt)
    {
        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            if (p.IsFixed) continue;

            p.vel += gravity * dt;
            p.prevPos = p.pos;
            p.pos += p.vel * dt;
            particles[i] = p;
        }

        for (int iter = 0; iter < solverIterations; iter++)
        {
            foreach (var c in stretchConstraints)
            {
                var pa = particles[c.indexA];
                var pb = particles[c.indexB];
                c.Solve(ref pa, ref pb);
                particles[c.indexA] = pa;
                particles[c.indexB] = pb;
            }

            foreach (var c in volumeConstraints)
            {
                var p0 = particles[c.i0];
                var p1 = particles[c.i1];
                var p2 = particles[c.i2];
                var p3 = particles[c.i3];
                c.Solve(ref p0, ref p1, ref p2, ref p3);
                particles[c.i0] = p0;
                particles[c.i1] = p1;
                particles[c.i2] = p2;
                particles[c.i3] = p3;
            }
        }

        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            p.vel = (p.pos - p.prevPos) / dt;
            particles[i] = p;
        }
    }
    public AABB GetAABB()
    {
        if (particles == null || particles.Count == 0)
            return new AABB(Vector3.zero, Vector3.zero);

        Vector3 min = particles[0].pos;
        Vector3 max = particles[0].pos;

        for (int i = 1; i < particles.Count; i++)
        {
            Vector3 p = particles[i].pos;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        return new AABB(min, max);
    }
    public OctreeNode BuildOctree()
    {
        int surfaceCount = particles.Count - sampler.InteriorWorldPoints.Count;
        if (surfaceCount <= 0) return null;

        Vector3 min = particles[0].pos;
        Vector3 max = particles[0].pos;

        for (int i = 1; i < surfaceCount; i++)
        {
            Vector3 p = particles[i].pos;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        Bounds rootBounds = new Bounds((min + max) * 0.5f, max - min + Vector3.one * 0.01f);
        OctreeNode root = new OctreeNode(rootBounds);

        for (int i = 0; i < surfaceCount; i++)
            root.Insert(particles[i].pos);

        octreeRoot = root; // ✅ This line is required
        return root;
    }


    public void UpdateOctree()
    {
        octreeRoot = BuildOctree();
    }

    public int GetParticleCount()
    {
        return particles.Count;
    }


    public new string name => gameObject.name;

    public List<Vector3> GetAllParticlePositions()
    {
        List<Vector3> positions = new List<Vector3>(particles.Count);
        foreach (var p in particles)
            positions.Add(p.pos);
        return positions;
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

    public int GetParticleIndex(Vector3 pos)
    {
        for (int i = 0; i < particles.Count; i++)
        {
            if (particles[i].pos == pos) return i;
        }
        return -1;
    }

    public ParticleData GetParticle(int i)
    {
        var p = particles[i];
        return new ParticleData(p.pos, p.vel, p.invMass);
    }

    public void OffsetParticlePosition(int i, Vector3 offset)
    {
        var p = particles[i];
        p.pos += offset;
        particles[i] = p;
    }
    public Vector3 GetParticlePosition(int index)
    {
        return particles[index].pos;
    }
    public Vector3 GetVelocity(int index) => particles[index].vel;
    public void MoveParticle(int index, Vector3 delta)
    {
        var p = particles[index];
        p.pos += delta;
        particles[index] = p;
    }
    public float GetInvMass(int index)
    {
        float mass = particles[index].invMass;
        return mass <= 0f ? 0f : 1f / mass;
    }
    public void ApplyImpulse(int index, Vector3 impulse)
    {
        var p = particles[index];
        float invMass = GetInvMass(index);
        p.vel += impulse * invMass;
        particles[index] = p;
    }
    public void SetInitialVelocity(Vector3 velocity)
    {
        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            if (!p.IsFixed)
                p.vel = velocity;
            particles[i] = p;
        }
    }
 public void RestartSimulation()
    {
        Debug.Log($"[PBD] Restarting... current particle count = {particles.Count}");

        particles.Clear();
        stretchConstraints.Clear();
        Debug.Log($"Before Clear: {particles.Count}");
        volumeConstraints.Clear();

        if (sampler == null)
            sampler = GetComponent<VolumeSampler>();
        if (extractor == null)
            extractor = GetComponent<TriangleExtractor>();

        sampler.SampleVolume();
        extractor.SampleSurfacePoints(sampler.voxelSize); // or 0.2f if you prefer

        BuildParticles();
        Debug.Log($"After BuildParticles: {particles.Count}");
        BuildStretchConstraints();
        BuildVolumeConstraints();
        ConnectSurfaceToInterior();
        ConnectSurfaceSprings();

        Debug.Log("[PBD] Simulation restarted.");
    }

}


// /// </summary>
// public sealed class TinyKDTree
// {
//     private struct Node
//     {
//         public int   index;   // original particle index
//         public Vector3 pos;   // position in world space
//         public int   left;    // index of left child in the nodes array (‑1 if none)
//         public int   right;   // index of right child (‑1 if none)
//     }

//     private Node[] nodes; // flat array representing the tree

//     /// <summary>
//     /// Build from a list of (position, particleIndex) pairs.
//     /// </summary>
//     public TinyKDTree(List<(Vector3 pos, int index)> points)
//     {
//         if (points == null || points.Count == 0)
//             throw new ArgumentException("Point list must be non‑empty");

//         // copy to mutable list because we'll sort in‑place per recursion level
//         var pts = new List<(Vector3 pos, int index)>(points);
//         nodes = new Node[pts.Count];
//         BuildRecursive(pts, 0, pts.Count, 0, 0);
//     }

//     // ───────────────────────────────────────────────────────────── private helpers ──

//     private int BuildRecursive(List<(Vector3 pos, int index)> pts, int start, int end, int depth, int arrayIdx)
//     {
//         if (start >= end) return -1;

//         int axis = depth % 3;
//         int mid  = (start + end) >> 1;

//         // partial sort: nth‑element would be ideal, but Unity lacks it → quick Sort slice
//         pts.Sort(start, end - start, Comparer<(Vector3 pos,int index)>.Create(
//             (a, b) => a.pos[axis].CompareTo(b.pos[axis])));

//         // ensure capacity
//         if (arrayIdx >= nodes.Length)
//             Array.Resize(ref nodes, arrayIdx + 32);

//         var (p, idx) = pts[mid];
//         nodes[arrayIdx].pos   = p;
//         nodes[arrayIdx].index = idx;

//         int left  = BuildRecursive(pts, start, mid, depth + 1, arrayIdx + 1);
//         int right = BuildRecursive(pts, mid + 1, end, depth + 1,
//                                    left == -1 ? arrayIdx + 1 : left + SubtreeSize(left));

//         nodes[arrayIdx].left  = left;
//         nodes[arrayIdx].right = right;
//         return arrayIdx;
//     }

//     private int SubtreeSize(int idx)
//     {
//         if (idx == -1) return 0;
//         return 1 + SubtreeSize(nodes[idx].left) + SubtreeSize(nodes[idx].right);
//     }

//     // ─────────────────────────────────────────────── public query: radial k‑nearest ──

//     /// <summary>
//     /// Finds up to <paramref name="k"/> nearest neighbours within radius² of <paramref name="query"/>.
//     /// Writes results to <paramref name="outBuf"/> (index, squared distance) sorted ascending.
//     /// Returns the number of neighbours found (≤ k).
//     /// </summary>
//     public int RadialKNearest(Vector3 query, float radius2, int k,
//                               Span<(int index, float dist2)> outBuf)
//     {
//         if (k <= 0 || k > outBuf.Length)
//             throw new ArgumentException("outBuf must have length ≥ k");

//         // tiny fixed‑size max‑heap stored in arrays
//         Span<float> dHeap = stackalloc float[k];
//         Span<int>   iHeap = stackalloc int[k];
//         int count = 0;     // current heap size
//         float worst = 0f;  // largest distance² currently in heap

//         SearchRecursive(0, query, radius2, k, ref count, ref worst, dHeap, iHeap, 0);

//        for (int c = 0; c < count; c++)
//             outBuf[c] = (iHeap[c], dHeap[c]);

//         // insertion-sort the at-most-3 items (count ≤ k ≤ 3)
//         for (int a = 1; a < count; a++)
//         {
//             var key = outBuf[a];
//             int b = a - 1;
//            while (b >= 0 && outBuf[b].dist2 > key.dist2)
//             {
//                 outBuf[b + 1] = outBuf[b];
//                 b--;
//             }
//             outBuf[b + 1] = key;
//         }

//         return count;
//     }

//     private void SearchRecursive(int n, Vector3 q, float r2, int k,
//                                  ref int count, ref float worst,
//                                  Span<float> dHeap, Span<int> iHeap,
//                                  int depth)
//     {
//         if (n == -1) return;

//         ref Node node = ref nodes[n];
//         float d2 = (node.pos - q).sqrMagnitude;
//         bool inRadius = d2 <= r2;

//         // maintain max‑heap of size ≤ k with the closest distances²
//         if (inRadius)
//         {
//             if (count < k) // heap not full: append
//             {
//                 dHeap[count] = d2;
//                 iHeap[count] = node.index;
//                 if (d2 > worst) worst = d2;
//                 count++;
//             }
//             else if (d2 < worst) // replace current worst
//             {
//                 int wi = 0;
//                 for (int h = 1; h < count; h++) if (dHeap[h] > dHeap[wi]) wi = h;
//                 dHeap[wi] = d2;
//                 iHeap[wi] = node.index;
//                 worst = dHeap[0];
//                 for (int h = 1; h < count; h++) if (dHeap[h] > worst) worst = dHeap[h];
//             }
//         }

//         int axis = depth % 3;
//         float diff = q[axis] - node.pos[axis];
//         int first  = diff < 0 ? node.left : node.right;
//         int second = diff < 0 ? node.right : node.left;

//         SearchRecursive(first, q, r2, k, ref count, ref worst, dHeap, iHeap, depth + 1);

//         // Check if we need to examine the other side of the split plane.
//         if (diff * diff < r2 || count < k)
//             SearchRecursive(second, q, r2, k, ref count, ref worst, dHeap, iHeap, depth + 1);
//     }
// }