using UnityEngine;

public class Particle
{
public Vector3 position;
public Vector3 velocity;
public Vector3 force;
public float mass;
public bool isFixed;
public Particle(Vector3 position, float mass)
{
    this.position = position;
    this.velocity = Vector3.zero;
    this.force = Vector3.zero;
    this.mass = mass;
    this.isFixed = false;
}

public void Integrate(float dt)
{
    if (isFixed) return;
    velocity += (force / mass) * dt;
    position += velocity * dt;
    force = Vector3.zero;
}

public void ApplyForce(Vector3 f)
{
    if (!isFixed)
        force += f;
}
}