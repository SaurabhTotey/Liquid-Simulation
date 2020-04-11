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

}
