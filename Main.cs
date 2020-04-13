using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/**
 * All the code for the liquid simulation
 */
public class Main : Control {

	//How much gravity the water particles experience
	[Export] public float Gravity = 1000f;

	//The maximum distance between particles before they stop affecting each other
	[Export] public float InteractionRadius = 30f;

	//The default 'density' that particles try and maintain
	[Export] public float RestDensity = 20f;

	//TODO: understand
	[Export] public float Stiffness = 1f;

	//TODO: understand
	[Export] public float NearStiffness = 1f;

	//The packed scene for the water particle because it is potentially instanced many times
	private PackedScene _waterParticleScene;

	//The user can place 'blockers', which are static segments that interact with the water; this is the blocker that is currently being placed
	private SegmentShape2D _blockerSegmentBeingPlaced;

	//A list of all water particles so that they can have their physics controlled
	private readonly List<WaterParticle> _allWaterParticles = new List<WaterParticle>();

	//A list of all blockers so that they can be drawn
	private readonly List<SegmentShape2D> _allBlockers = new List<SegmentShape2D>();

	/**
	 * Gets the WaterParticle scene as a packed scene on startup because it will be loaded many times
	 */
	public override void _Ready() {
		this._waterParticleScene = ResourceLoader.Load<PackedScene>("res://WaterParticle.tscn");
	}

	/**
	 * Manages the particles' physics
	 * TODO: implement https://www.researchgate.net/publication/220789321_Particle-based_viscoelastic_fluid_simulation
	 * TODO: decide if it is better to, instead of setting Position for WaterParticle directly, set a different field and then MoveAndCollide to the new position
	 */
	public override void _PhysicsProcess(float delta) {
		//Applies gravity to each particle and handles resetting particles from previous pass
		foreach (var waterParticle in this._allWaterParticles) {
			waterParticle.Velocity.y += this.Gravity * delta;
			waterParticle.NeighborToOffset.Clear();
			waterParticle.Density = 0;
			waterParticle.NearDensity = 0;
		}

		//TODO: read about and then implement viscosity impulses

		//Saves each particle's current position and advances it to its forward euler's method velocity-based predicted position
		foreach (var waterParticle in this._allWaterParticles) {
			waterParticle.OldPosition = new Vector2(waterParticle.Position);
			waterParticle.Position += waterParticle.Velocity * delta;
		}

		//TODO: read about and then implement adding and removing springs between particles
		//TODO: read about adjust particle positions based on spring positions

		//Finds particle neighbors for each particle
		for (var i = 1; i < this._allWaterParticles.Count; i++) {
			var particle1 = this._allWaterParticles[i];
			for (var j = 0; j < i; j++) {
				var particle2 = this._allWaterParticles[j];
				var offset = particle2.Position - particle1.Position;
				if (offset.LengthSquared() >= Math.Pow(this.InteractionRadius, 2)) {
					continue;
				}

				particle1.NeighborToOffset.Add(particle2, offset);
				particle2.NeighborToOffset.Add(particle1, -offset);
			}
		}

		//Applies double-density relaxations
		foreach (var waterParticle in this._allWaterParticles) {
			foreach (var inverseNormalizedDistance in waterParticle.NeighborToOffset.Select(neighborToOffset =>
				1 - neighborToOffset.Value.Length() / this.InteractionRadius)) {
				waterParticle.Density += (float) Math.Pow(inverseNormalizedDistance, 2);
				waterParticle.NearDensity += (float) Math.Pow(inverseNormalizedDistance, 3);
			}

			var pressure = this.Stiffness * (waterParticle.Density - this.RestDensity);
			var nearPressure = this.NearStiffness * waterParticle.NearDensity;
			var selfDisplacement = Vector2.Zero;

			foreach (var neighborToOffset in waterParticle.NeighborToOffset) {
				var inverseNormalizedDistance = 1 - neighborToOffset.Value.Length() / this.InteractionRadius;
				var displacementTerm = (float) (Math.Pow(delta, 2) *
					                       (pressure * inverseNormalizedDistance +
					                        nearPressure * Math.Pow(inverseNormalizedDistance, 2)) / 2f) *
				                       neighborToOffset.Value;
				neighborToOffset.Key.Position += displacementTerm;
				selfDisplacement -= displacementTerm;
			}

			waterParticle.Position += selfDisplacement;
		}

		//TODO: read about and then decide how to handle collisions: see what the paper does and figure out what Godot has and decide how to proceed

		//Sets each particle's velocity to be the difference in its position divided by delta
		foreach (var waterParticle in this._allWaterParticles) {
			waterParticle.Velocity = (waterParticle.Position - waterParticle.OldPosition) / delta;
		}
	}

	/**
	 * Removes particles from the scene if they are out of bounds
	 */
	public override void _Process(float delta) {
		foreach (var particleToRemove in this._allWaterParticles.Where(waterParticle =>
			!this.GetViewportRect().Encloses(new Rect2(waterParticle.Position, 10, 10))).ToList()) {
			this._allWaterParticles.Remove(particleToRemove);
			this.RemoveChild(particleToRemove);
		}
	}

	/**
	 * Handles inputs for the simulation
	 */
	public override void _Input(InputEvent inputEvent) {
		//Adds a water particle to the mouse's position if the left mouse button is pressed
		if (Input.IsActionPressed("add_water_particle")) {
			var waterParticleInstance = (Node2D) this._waterParticleScene.Instance();
			waterParticleInstance.Position = this.GetViewport().GetMousePosition();
			this.AddChild(waterParticleInstance);
			this._allWaterParticles.Add((WaterParticle) waterParticleInstance);
		}

		//If the right mouse button is being pressed, creates a blocker from the start of the press to the end of the press
		if (Input.IsActionPressed("add_blocker")) {
			if (this._blockerSegmentBeingPlaced == null) {
				this._blockerSegmentBeingPlaced = new SegmentShape2D {
					A = this.GetViewport().GetMousePosition(),
					B = this.GetViewport().GetMousePosition()
				};
				var body = new StaticBody2D();
				body.AddChild(new CollisionShape2D {
					Shape = this._blockerSegmentBeingPlaced
				});
				this.AddChild(body);
				this._allBlockers.Add(this._blockerSegmentBeingPlaced);
				this.Update();
			}
			else {
				this._blockerSegmentBeingPlaced.B = this.GetViewport().GetMousePosition();
				this.Update();
			}
		}
		else {
			this._blockerSegmentBeingPlaced = null;
			this.Update();
		}

		//Removes all children if the escape key is pressed
		if (Input.IsActionPressed("clear_everything")) {
			foreach (var child in this.GetChildren()) {
				this.RemoveChild((Node) child);
			}

			this._allWaterParticles.Clear();
			this._allBlockers.Clear();
			this.Update();
		}
	}

	/**
	 * Draws all blockers
	 */
	public override void _Draw() {
		this._allBlockers.ForEach(segment => { this.DrawLine(segment.A, segment.B, Colors.Black); });
	}

}
