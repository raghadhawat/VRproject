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
    public float pointMass = 0.1f;
    [Tooltip("Spring stiffness (Hooke's k)")]
    public float stiffness = 200f;
    [Tooltip("Spring damping")]
    public float damping = 10f;
    [Header("Ground Collision")]
    public float groundY = 0f;
    public float groundStiffness = 500f;
    [Range(0,1)] public float groundDamping = 0.5f;
    [Range(0,1)] public float groundFriction = 0.8f;
    [Header("Integration")]
    [Tooltip("Number of substeps per frame")]
    public int substeps = 8;
    [Tooltip("Scale simulation speed (0–1)")]
    [Range(0.01f,1f)]
    public float timeScale = 0.5f;
    [Tooltip("Max position magnitude before abort")]
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
        // surface edges
        for (int i = 0; i < meshTris.Length; i+=3)
        {
            int vertex1 = surfMap[meshTris[i]], 
                vertex2 = surfMap[meshTris[i+1]], 
                vertex3 = surfMap[meshTris[i+2]];
            AddEdge(vertex1, vertex2); 
            AddEdge(vertex2, vertex3); 
            AddEdge(vertex3, vertex1);
        }

        // interior grid + diagonals
        int[] dx = {1,0,0}, dy={0,1,0}, dz={0,0,1};
        foreach (var kv in sampler.InteriorGridIndices)
        {
            var k = kv.Key; int idx = intMap[k];
            foreach (var del in new[]{new Vector3Int(1,0,0), new Vector3Int(0,1,0), new Vector3Int(0,0,1)})
            {
                var nk = k + del;
                if (intMap.TryGetValue(nk, out int j))
                    AddEdge(idx, j);
            }
            // diagonals (xy, yz, zx faces)
            var diag = new[]{new Vector3Int(1,1,0), new Vector3Int(1,0,1), new Vector3Int(0,1,1)};
            foreach (var d in diag)
            {
                var nk = k + d;
                if (intMap.TryGetValue(nk, out int j))
                    AddEdge(idx, j);
            }
        }

        // surface-interior welds
        Bounds b = mf.mesh.bounds;
        Vector3 step = new Vector3(b.size.x / sampler.InteriorGridIndices.Keys.Max(k=>k.x),
                                   b.size.y / sampler.InteriorGridIndices.Keys.Max(k=>k.y),
                                   b.size.z / sampler.InteriorGridIndices.Keys.Max(k=>k.z));
        foreach(var kv in surfMap)
        {
            var lv = localVerts[kv.Key];
            var gi = new Vector3Int(
                Mathf.RoundToInt((lv.x - b.min.x)/step.x),
                Mathf.RoundToInt((lv.y - b.min.y)/step.y),
                Mathf.RoundToInt((lv.z - b.min.z)/step.z)
            );

            foreach(var di in new[]{Vector3Int.zero, Vector3Int.one, new Vector3Int(1,1,0), new Vector3Int(1,0,1), new Vector3Int(0,1,1)})
            {
                var nk = gi + di;
                if (intMap.TryGetValue(nk, out int idxI))
                    AddEdge(kv.Value, idxI);
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
        for (int i = 0; i < n; i++)
        {
            var p = mps[i];
            p.force = gravity * p.mass;
            mps[i] = p;
        }

        foreach (var s in springs)
        {
            var A = mps[s.a]; var B=mps[s.b];
            var delta = A.pos - B.pos;
            float d = delta.magnitude;
            if (d < 1e-9f) continue;
            var dir = delta / d;
            var ext = d - s.restLen;
            var f = -stiffness * ext * dir;
            var dam = -damping * Vector3.Dot(A.vel - B.vel, dir) * dir;
            var total = f + dam;
            A.force += total; B.force -= total;
            mps[s.a] = A; mps[s.b] = B;
        }

        for(int i=0;i<n;i++)
        {
            var p = mps[i];
            var acc = p.force / p.mass;
            p.vel += dt * acc;
            p.pos += dt * p.vel;

            // divergence guard
            if (float.IsNaN(p.pos.x) || p.pos.magnitude > maxPosMag)
            {
                isDeforming = false;
                Debug.LogError("Simulation unstable! Aborting deformation.");
                return false;
            }

            if (p.pos.y < groundY)
            {
                float pen = groundY - p.pos.y;
                pen = Mathf.Min(pen, 1f);
                Vector3 fg = Vector3.up * groundStiffness * pen;
                p.vel += dt * fg / p.mass;
                p.vel.y = -p.vel.y * (1f - groundDamping);
                p.vel.x *= groundFriction; p.vel.z *= groundFriction;
                p.pos.y = groundY + 0.001f;
            }

            mps[i] = p;
        }
        return true;
    }

    void UpdateMeshVerts()
    {
        var mesh = new Mesh();
        var vs = new Vector3[localVerts.Length];
        for(int i=0;i<localVerts.Length;i++)
        {
            vs[i] = transform.InverseTransformPoint(mps[surfMap[i]].pos);
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
        Gizmos.color = Color.gray;
        foreach (var s in springs)
            Gizmos.DrawLine(mps[s.a].pos, mps[s.b].pos);
        Gizmos.color = Color.red;
        foreach (var p in mps)
            Gizmos.DrawSphere(p.pos, 0.01f);
    }
}
