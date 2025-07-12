using UnityEngine;

public interface ISoftBodySystem
{
    AABB GetAABB();                          // For broad-phase
    OctreeNode BuildOctree();                // For narrow-phase
    int GetParticleCount();                  // For collision loop
    Vector3 GetParticlePosition(int index);  // For resolving contacts
    string name { get; }                     // Optional debug name
     Vector3 GetParticleVelocity(int index);
    void SetParticleVelocity(int index, Vector3 velocity);
    void SetParticlePosition(int index, Vector3 position);
    float GetInverseMass(int index);
}
