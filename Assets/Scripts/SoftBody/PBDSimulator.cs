using System.Collections.Generic;
using UnityEngine;

public class PBDSimulator : MonoBehaviour
{
    [Range(0f, 1f)] public float stretchStiffness = 0.8f;
    [Range(0f, 1f)] public float volumeStiffness = 1f;

    [Header("Ground Collision")]
    public bool enableGround = true;
    public float groundY = 0f;
    public float groundStiffness = 0.5f;
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

    public AABB aabb => GetAABB();


    void Start()
    {
        sampler = GetComponent<VolumeSampler>();
        extractor = GetComponent<TriangleExtractor>();

        BuildParticles();
        BuildStretchConstraints();
        BuildVolumeConstraints();
        ConnectSurfaceToInterior();
        ConnectSurfaceSprings();

    }

    void FixedUpdate()
    {
        float dt = Time.deltaTime * timeScale;
        SimulatePBD(dt);

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

        float minY = groundY + 0.01f;
        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            if (p.pos.y < minY)
            {
                p.pos.y = minY;
                p.vel = Vector3.zero;
            }
            if (p.IsFixed && p.pos.y < minY + 0.05f)
                p.invMass = 1f / pointMass;
            particles[i] = p;
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

        Vector3Int[] offsets = new Vector3Int[] {
            new Vector3Int(1,0,0), new Vector3Int(0,1,0), new Vector3Int(0,0,1),
            new Vector3Int(1,1,0), new Vector3Int(1,0,1), new Vector3Int(0,1,1),
            new Vector3Int(0,-1,0), new Vector3Int(1,-1,0), new Vector3Int(-1,-1,0),new Vector3Int(1,1,1), new Vector3Int(-1,1,1), new Vector3Int(1,-1,1)

        };

        float threshold = groundY + 0.01f;

        foreach (var kv in map)
        {
            Vector3Int a = kv.Key;
            int iA = kv.Value;
            if (particles[iA].pos.y < threshold) continue;

            foreach (var offset in offsets)
            {
                Vector3Int b = a + offset;
                if (map.TryGetValue(b, out int iB))
                {
                    if (particles[iB].pos.y < threshold) continue;

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

        // Debug.Log("[PBD] Volume constraints added: " + volumeConstraints.Count);

        void AddTetra(int i0, int i1, int i2, int i3)
        {
            float threshold = groundY + 0.01f;
            int below = 0;
            if (particles[i0].pos.y < threshold) below++;
            if (particles[i1].pos.y < threshold) below++;
            if (particles[i2].pos.y < threshold) below++;
            if (particles[i3].pos.y < threshold) below++;

            if (below >= 3) return;

            float v = Mathf.Abs(VolumeConstraint.SignedTetrahedronVolume(
                particles[i0].pos, particles[i1].pos, particles[i2].pos, particles[i3].pos));
            volumeConstraints.Add(new VolumeConstraint(i0, i1, i2, i3, v, volumeStiffness));
        }
    }
    void ConnectSurfaceToInterior(int maxConnections = 3)
    {
        int surfaceCount = particles.Count - sampler.InteriorWorldPoints.Count;
        int interiorStart = surfaceCount;
        int interiorCount = sampler.InteriorWorldPoints.Count;

        float voxelSpacing = sampler.voxelSize;
        float maxRadius = voxelSpacing * 1.5f;

        for (int i = 0; i < surfaceCount; i++)
        {
            Vector3 surfacePos = particles[i].pos;

            // Find closest interior particles
            List<(int index, float dist)> closest = new List<(int, float)>();

            for (int j = interiorStart; j < particles.Count; j++)
            {
                float dist = Vector3.Distance(surfacePos, particles[j].pos);
                if (dist > maxRadius) continue;

                if (closest.Count < maxConnections)
                {
                    closest.Add((j, dist));
                }
                else
                {
                    // Replace furthest if current is closer
                    int farIndex = 0;
                    for (int k = 1; k < maxConnections; k++)
                        if (closest[k].dist > closest[farIndex].dist)
                            farIndex = k;

                    if (dist < closest[farIndex].dist)
                        closest[farIndex] = (j, dist);
                }
            }

            foreach (var c in closest)
            {
                stretchConstraints.Add(new DistanceConstraint(i, c.index, c.dist, stretchStiffness));
            }
        }

        // Debug.Log("[PBD] Connected surface-to-interior springs: " + surfaceCount * maxConnections);
    }
    void ConnectSurfaceSprings(float connectRadius = 0.15f)
    {
        int surfaceCount = particles.Count - sampler.InteriorWorldPoints.Count;
        float cellSize = connectRadius * 1.1f; // Slightly larger than radius
        var grid = new Dictionary<Vector3Int, List<int>>();
        var added = new HashSet<(int, int)>();

        // 1. Insert surface particles into spatial grid
        for (int i = 0; i < surfaceCount; i++)
        {
            Vector3 pos = particles[i].pos;
            Vector3Int cell = Vector3Int.FloorToInt(pos / cellSize);

            if (!grid.TryGetValue(cell, out var list))
            {
                list = new List<int>();
                grid[cell] = list;
            }

            list.Add(i);
        }

        // 2. For each surface particle, check nearby cells only
        Vector3Int[] neighborOffsets = {
        new Vector3Int(0,0,0), new Vector3Int(1,0,0), new Vector3Int(-1,0,0),
        new Vector3Int(0,1,0), new Vector3Int(0,-1,0), new Vector3Int(0,0,1),
        new Vector3Int(0,0,-1), new Vector3Int(1,1,0), new Vector3Int(-1,-1,0),
        new Vector3Int(1,0,1), new Vector3Int(-1,0,-1), new Vector3Int(0,1,1),
        new Vector3Int(0,-1,-1), new Vector3Int(1,1,1), new Vector3Int(-1,-1,-1)
    };

        for (int i = 0; i < surfaceCount; i++)
        {
            Vector3 posA = particles[i].pos;
            Vector3Int baseCell = Vector3Int.FloorToInt(posA / cellSize);

            foreach (var offset in neighborOffsets)
            {
                Vector3Int neighborCell = baseCell + offset;

                if (grid.TryGetValue(neighborCell, out var candidates))
                {
                    foreach (int j in candidates)
                    {
                        if (j <= i) continue; // avoid duplicates

                        float dist = Vector3.Distance(posA, particles[j].pos);
                        if (dist <= connectRadius)
                        {
                            var key = (i, j);
                            if (added.Add(key)) // only if not added yet
                            {
                                stretchConstraints.Add(new DistanceConstraint(i, j, dist, stretchStiffness));
                            }
                        }
                    }
                }
            }
        }

        // Debug.Log("[PBD] Spatially connected surface-surface springs: " + added.Count);
    }


    void SimulatePBD(float dt)
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

        if (enableGround)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                if (p.IsFixed) continue;
                if (p.pos.y < groundY)
                {
                    float penetration = groundY - p.pos.y;
                    p.pos.y += penetration * groundStiffness;

                    if (p.vel.y < 0)
                        p.vel.y *= -groundFriction;
                }
                particles[i] = p;
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
}
