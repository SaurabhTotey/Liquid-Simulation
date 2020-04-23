using System.Collections.Generic;
using Godot;

/**
 * A class that represents a particle of liquid: only exists to store state
 * Doesn't handle any of the math itself and is instead managed by Main.cs
 */
public class LiquidParticle : KinematicBody2D {

	//The velocity of the particle
	public Vector2 Velocity = new Vector2();

	//The position of the particle before performing any sort of physics processes on it every step
	public Vector2 OldPosition;

	//A list of potential 'neighbor particles'; is a hashset because order doesn't matter as much as checking membership TODO: a quadrant-based approach is probably more accurate and efficient
	public readonly HashSet<LiquidParticle> PotentialNeighbors = new HashSet<LiquidParticle>();

	//A mapping of particles to a spring connecting them to this particle
	public readonly Dictionary<LiquidParticle, Spring> ParticleToSpring = new Dictionary<LiquidParticle, Spring>();

	//The 'density' at this particle: depends on its neighbors
	public float Density;

	//Similar to the 'density' of the particle, but is used only for particles close to each other to repel to prevent clustering
	public float NearDensity;

}
