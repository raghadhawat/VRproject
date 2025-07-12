using UnityEngine;

public struct ContactPair
{
    public ISoftBodySystem bodyA;
    public int indexA;

    public ISoftBodySystem bodyB;
    public int indexB;

    public Vector3 normal;       // From B to A
    public float penetration;    // Overlap amount

    public float restitution;    // 0 = fully inelastic, 1 = elastic

    public ContactPair(ISoftBodySystem a, int iA, ISoftBodySystem b, int iB, Vector3 n, float pen, float e = 0.2f)
    {
        bodyA = a;
        indexA = iA;
        bodyB = b;
        indexB = iB;
        normal = n;
        penetration = pen;
        restitution = Mathf.Clamp01(e);
    }
}
