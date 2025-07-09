using UnityEngine;

public struct PBDParticle
{
    public Vector3 pos;
    public Vector3 prevPos;
    public Vector3 vel;
    public float invMass;
    public bool isSurface;

    public PBDParticle(Vector3 position, float mass, bool surface)
    {
        pos = prevPos = position;
        vel = Vector3.zero;
        invMass = (mass <= 0) ? 0 : 1f / mass;
        isSurface = surface;
    }

    public bool IsFixed => invMass == 0f;
}
