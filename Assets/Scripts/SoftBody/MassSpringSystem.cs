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
    [Range(0,1)] public float groundDamping = 0.5f;
    [Range(0,1)] public float groundFriction = 0.8f;
    [Header("Integration")]
    [Tooltip("Number of substeps per frame")]
    public int substeps = 32;
    [Tooltip("Scale simulation speed (0–1)")]
    [Range(0.01f,1f)]
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
    private struct Spring { public int a, b; public float restLen; public Spring(int a,int b,float r){this.a=a;this.b=b;restLen=r;} }

    // References & data
    private VolumeSampler sampler;
    private MeshFilter mf;
    private Vector3[] localVerts;
    private int[] meshTris;

    private List<MassPoint> mps;
    private List<Spring> springs;
    private Dictionary<int,int> surfMap;
    private Dictionary<Vector3Int,int> intMap;

    void Start()
    {
        sampler = GetComponent<VolumeSampler>();
        mf = GetComponent<MeshFilter>();

        if (sampler.InteriorLocalPoints.Count == 0)
            sampler.SampleVolume();  // ensure data

        Mesh m = mf.mesh;
        localVerts = m.vertices;
        meshTris = m.triangles;

        BuildMassPoints();
        BuildSprings();
        AdjustGroundY();

        
        // 💥 Kick test: Add initial downward velocity
        for (int i = 0; i < mps.Count; i++)
        {
            MassPoint massPoint = mps[i];
            massPoint.vel = new Vector3(0, -1f, 0); // You can tweak this value
            mps[i] = massPoint;
        }

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
        surfMap = new Dictionary<int,int>();
        intMap = new Dictionary<Vector3Int,int>();

        // surface
        for (int i = 0; i < localVerts.Length; i++)
        {
            var pW = transform.TransformPoint(localVerts[i]);
            surfMap[i] = mps.Count;
            mps.Add(new MassPoint(pW, pointMass));
        }
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

void BuildSprings()
{
    springs = new List<Spring>();
    var seen = new HashSet<(int,int)>();

    void AddEdge(int a, int b)
    {
        if (a == b) return;
        var key = a < b ? (a,b) : (b,a);
        if (seen.Add(key))
        {
            float rst = Vector3.Distance(mps[a].pos, mps[b].pos);
            springs.Add(new Spring(a,b,rst));
        }
    }

    // surface edges
    for (int i = 0; i < meshTris.Length; i += 3)
    {
        int vertex1 = surfMap[meshTris[i]], 
            vertex2 = surfMap[meshTris[i+1]], 
            vertex3 = surfMap[meshTris[i+2]];
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
    
    foreach(var kv in surfMap)
    {
        var lv = localVerts[kv.Key];
        var gi = new Vector3Int(
            Mathf.RoundToInt((lv.x - b.min.x)/step.x),
            Mathf.RoundToInt((lv.y - b.min.y)/step.y),
            Mathf.RoundToInt((lv.z - b.min.z)/step.z)
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

        // 4. Ground collision
        if (p.pos.y < groundY)
        {
            float penetration = groundY - p.pos.y;

            // Position correction only
            p.pos.y = groundY + 0.001f;

            // Bounce
            if (p.vel.y < 0)
                p.vel.y *= -1f * (1f - groundDamping);

            // Friction
            p.vel.x *= groundFriction;
            p.vel.z *= groundFriction;
        }

        mps[i] = p;
    }

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
        for(int i=0;i<localVerts.Length;i++)
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

        // Draw springs with appropriate colors
        foreach (var s in springs)
        {
            bool aIsSurface = s.a < localVerts.Length;
            bool bIsSurface = s.b < localVerts.Length;

            if (aIsSurface && bIsSurface && showSurfaceSprings)
            {
                // Surface-to-surface springs (gray)
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(mps[s.a].pos, mps[s.b].pos);
            }
            else if (!aIsSurface && !bIsSurface && showInnerSprings)
            {
                // Inner-to-inner springs (blue)
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(mps[s.a].pos, mps[s.b].pos);
            }
            else if (showWeldingSprings)
            {
                // Surface-to-inner welding springs (orange)
                Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
                Gizmos.DrawLine(mps[s.a].pos, mps[s.b].pos);
            }
        }

        // Draw points
        for (int i = 0; i < mps.Count; i++)
        {
            if (i < localVerts.Length && showSurfacePoints)
            {
                // Surface points (red)
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(mps[i].pos, gizmoSize);
            }
            else if (showInnerPoints)
            {
                // Inner points (blue)
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(mps[i].pos, gizmoSize * 0.8f);
            }
        }
    }

}
