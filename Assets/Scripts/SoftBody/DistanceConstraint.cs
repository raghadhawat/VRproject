using UnityEngine;

public struct DistanceConstraint
{
    public int indexA, indexB;
    public float restLength;
    public float stiffness;

    public DistanceConstraint(int a, int b, float rest, float stiff = 1f)
    {
        indexA = a;
        indexB = b;
        restLength = rest;
        stiffness = Mathf.Clamp01(stiff);
    }

    public void Solve(ref PBDParticle a, ref PBDParticle b)
    {
        Vector3 delta = b.pos - a.pos;
        float dist = delta.magnitude;
        if (dist < 1e-6f) return;

        float w1 = a.invMass;
        float w2 = b.invMass;
        float wSum = w1 + w2;
        if (wSum == 0f) return;

        float correction = (dist - restLength) / wSum;
        Vector3 dir = delta.normalized;

        if (w1 > 0f)
            a.pos += stiffness * w1 * correction * dir;

        if (w2 > 0f)
            b.pos -= stiffness * w2 * correction * dir;
    }
}
