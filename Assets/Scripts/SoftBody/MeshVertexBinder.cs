using System.Collections.Generic;
using UnityEngine;

public class MeshVertexBinder
{
    private List<Particle> particles;
    private List<int> vertexToParticle;

    public MeshVertexBinder(List<Particle> particles)
    {
        this.particles = particles;
        this.vertexToParticle = new List<int>();
    }

    // Bind each mesh vertex to its closest particle
    public void Bind(Vector3[] meshVertices, Transform meshTransform)
    {
        vertexToParticle.Clear();

        for (int i = 0; i < meshVertices.Length; i++)
        {
            Vector3 worldPos = meshTransform.TransformPoint(meshVertices[i]);
            float minDist = float.MaxValue;
            int closestIndex = -1;

            for (int j = 0; j < particles.Count; j++)
            {
                float dist = (particles[j].position - worldPos).sqrMagnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    closestIndex = j;
                }
            }

            vertexToParticle.Add(closestIndex);
        }
    }

    // Get updated mesh vertex positions from bound particles
    public Vector3[] GetUpdatedVertices(Transform meshTransform)
    {
        Vector3[] updated = new Vector3[vertexToParticle.Count];

        for (int i = 0; i < vertexToParticle.Count; i++)
        {
            int particleIndex = vertexToParticle[i];
            Vector3 worldPos = particles[particleIndex].position;
            updated[i] = meshTransform.InverseTransformPoint(worldPos);
        }

        return updated;
    }
}
