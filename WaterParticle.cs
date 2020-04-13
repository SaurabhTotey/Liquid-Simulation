using System.Collections.Generic;
using Godot;

/**
 * A class that represents the water particle: only exists to store state
 * Doesn't handle any of the math itself and is instead managed by Main.cs
 */
public class WaterParticle : KinematicBody2D {

	//The velocity of the water particle
	public Vector2 Velocity = new Vector2();

	//The position of the water particle before performing any sort of physics processes on it every step
	public Vector2 OldPosition;
	
	//A mapping of 'neighbor particles' (determined by interaction radius) to a vector pointing from this particle to that neighbor
	public readonly Dictionary<WaterParticle, Vector2> NeighborToOffset = new Dictionary<WaterParticle, Vector2>();
	
	//The 'density' at this particle: depends on its neighbors
	public float Density;

	//Similar to the 'density' of the particle, but is used only for particles close to each other to repel to prevent clustering
	public float NearDensity;

}
