using Godot;

/**
 * A class that represents the water particle: only exists to store state
 * More specifically, only stores the velocity of the particle
 * Doesn't handle any of the math itself
 */
public class WaterParticle : KinematicBody2D {

	//The velocity of the water particle: isn't directly used, but is managed by Main.cs
	public Vector2 Velocity = new Vector2();

}
