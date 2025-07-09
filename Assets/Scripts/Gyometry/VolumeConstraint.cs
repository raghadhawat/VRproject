using UnityEngine;

public struct VolumeConstraint
{
    public int i0, i1, i2, i3;
    public float restVolume;
    public float stiffness;

    public VolumeConstraint(int a, int b, int c, int d, float volume, float stiff = 1f)
    {
        i0 = a; i1 = b; i2 = c; i3 = d;
        restVolume = volume;
        stiffness = Mathf.Clamp01(stiff);
    }

    public void Solve(ref PBDParticle p0, ref PBDParticle p1, ref PBDParticle p2, ref PBDParticle p3)
    {
        Vector3 x0 = p0.pos, x1 = p1.pos, x2 = p2.pos, x3 = p3.pos;

        float currentVolume = SignedTetrahedronVolume(x0, x1, x2, x3);
        float delta = currentVolume - restVolume;

        float w0 = p0.invMass;
        float w1 = p1.invMass;
        float w2 = p2.invMass;
        float w3 = p3.invMass;

        float wSum = w0 + w1 + w2 + w3;
        if (Mathf.Abs(wSum) < 1e-6f) return;

        Vector3 grad0 = Vector3.Cross(x1 - x2, x3 - x2) / 6f;
        Vector3 grad1 = Vector3.Cross(x2 - x0, x3 - x0) / 6f;
        Vector3 grad2 = Vector3.Cross(x0 - x1, x3 - x1) / 6f;
        Vector3 grad3 = Vector3.Cross(x1 - x0, x2 - x0) / 6f;

        if (w0 > 0) p0.pos -= stiffness * delta * w0 / wSum * grad0;
        if (w1 > 0) p1.pos -= stiffness * delta * w1 / wSum * grad1;
        if (w2 > 0) p2.pos -= stiffness * delta * w2 / wSum * grad2;
        if (w3 > 0) p3.pos -= stiffness * delta * w3 / wSum * grad3;
    }

   public static float SignedTetrahedronVolume(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        return Vector3.Dot(Vector3.Cross(b - a, c - a), d - a) / 6f;
    }
}
