using UnityEngine;

public class Spring
{
    public Particle a, b;
    public float restLength;
    public float stiffness;
    public float damping;

    public Spring(Particle a, Particle b, float stiffness, float damping)
    {
        this.a = a;
        this.b = b;
        this.restLength = Vector3.Distance(a.position, b.position);
        this.stiffness = stiffness;
        this.damping = damping;
    }

    public void ApplyForce()
    {
        Vector3 delta = b.position - a.position;
        float dist = delta.magnitude;
        if (dist == 0f) return;

        Vector3 dir = delta.normalized;
        Vector3 relativeVelocity = b.velocity - a.velocity;
        float springForce = stiffness * (dist - restLength);
        float dampingForce = damping * Vector3.Dot(relativeVelocity, dir);

        Vector3 force = -(springForce + dampingForce) * dir;

        a.ApplyForce(force);
        b.ApplyForce(-force);
    }
}
