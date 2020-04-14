using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/**
 * All the code for the liquid simulation
 * Liquid is simulated in Lagrangian method by using discrete liquid particles
 * TODO: RestDensity, Stiffness, and NearStiffness seem like they would be better as properties of LiquidParticle
 */
public class Main : Control {

	//How much gravity the particles experience
	[Export] public float Gravity = 1000f;

	//The maximum distance between particles before they stop affecting each other
	[Export] public float InteractionRadius = 30f;

	//The default 'density' that particles try and maintain
	[Export] public float RestDensity = 20f;

	//TODO: understand
	[Export] public float Stiffness = 1f;

	//TODO: understand
	[Export] public float NearStiffness = 1f;

	//The packed scene for the liquid particle because it is potentially instanced many times
	private PackedScene _liquidParticleScene;

	//The user can place 'blockers', which are static segments that interact with the liquid; this is the blocker that is currently being placed
	private SegmentShape2D _blockerSegmentBeingPlaced;

	//A list of all liquid particles so that they can have their physics controlled
	private readonly List<LiquidParticle> _liquidParticles = new List<LiquidParticle>();

	//A list of all blockers so that they can be drawn
	private readonly List<SegmentShape2D> _blockers = new List<SegmentShape2D>();

	/**
	 * Gets the LiquidParticle scene as a packed scene on startup because it will be loaded many times
	 */
	public override void _Ready() {
		this._liquidParticleScene = ResourceLoader.Load<PackedScene>("res://LiquidParticle.tscn");
	}

	/**
	 * Manages the particles' physics
	 * TODO: implement https://www.researchgate.net/publication/220789321_Particle-based_viscoelastic_fluid_simulation
	 * TODO: decide if it is better to, instead of setting Position for LiquidParticle directly, set a different field and then MoveAndCollide to the new position
	 */
	public override void _PhysicsProcess(float delta) {
		//Applies gravity to each particle and handles resetting particles from previous pass
		foreach (var liquidParticle in this._liquidParticles) {
			liquidParticle.Velocity.y += this.Gravity * delta;
			liquidParticle.NeighborToOffset.Clear();
			liquidParticle.Density = 0;
			liquidParticle.NearDensity = 0;
		}

		//TODO: read about and then implement viscosity impulses

		//Saves each particle's current position and advances it to its forward euler's method velocity-based predicted position
		foreach (var liquidParticle in this._liquidParticles) {
			liquidParticle.OldPosition = new Vector2(liquidParticle.Position);
			liquidParticle.Position += liquidParticle.Velocity * delta;
		}

		//Finds particle neighbors for each particle
		for (var i = 1; i < this._liquidParticles.Count; i++) {
			var particle1 = this._liquidParticles[i];
			for (var j = 0; j < i; j++) {
				var particle2 = this._liquidParticles[j];
				var offset = particle2.Position - particle1.Position;
				if (offset.LengthSquared() >= Math.Pow(this.InteractionRadius, 2)) {
					continue;
				}

				particle1.NeighborToOffset.Add(particle2, offset);
				particle2.NeighborToOffset.Add(particle1, -offset);
			}
		}

		//TODO: read about and then implement adding and removing springs between particles
		//TODO: read about and adjust particle positions based on spring positions

		//Applies double-density relaxations
		foreach (var liquidParticle in this._liquidParticles) {
			foreach (var inverseNormalizedDistance in liquidParticle.NeighborToOffset.Select(neighborToOffset =>
				1 - neighborToOffset.Value.Length() / this.InteractionRadius)) {
				liquidParticle.Density += (float) Math.Pow(inverseNormalizedDistance, 2);
				liquidParticle.NearDensity += (float) Math.Pow(inverseNormalizedDistance, 3);
			}

			var pressure = this.Stiffness * (liquidParticle.Density - this.RestDensity);
			var nearPressure = this.NearStiffness * liquidParticle.NearDensity;
			var selfDisplacement = Vector2.Zero;

			foreach (var neighborToOffset in liquidParticle.NeighborToOffset) {
				var inverseNormalizedDistance = 1 - neighborToOffset.Value.Length() / this.InteractionRadius;
				var displacementTerm = (float) (Math.Pow(delta, 2) *
					                       (pressure * inverseNormalizedDistance +
					                        nearPressure * Math.Pow(inverseNormalizedDistance, 2)) / 2f) *
				                       neighborToOffset.Value;
				neighborToOffset.Key.Position += displacementTerm;
				selfDisplacement -= displacementTerm;
			}

			liquidParticle.Position += selfDisplacement;
		}

		//TODO: read about and then decide how to handle collisions: see what the paper does and figure out what Godot has and decide how to proceed

		//Sets each particle's velocity to be the difference in its position divided by delta
		foreach (var liquidParticle in this._liquidParticles) {
			liquidParticle.Velocity = (liquidParticle.Position - liquidParticle.OldPosition) / delta;
		}
	}

	/**
	 * Removes particles from the scene if they are out of bounds
	 */
	public override void _Process(float delta) {
		foreach (var particleToRemove in this._liquidParticles.Where(liquidParticle =>
			!this.GetViewportRect().Encloses(new Rect2(liquidParticle.Position, 10, 10))).ToList()) {
			this._liquidParticles.Remove(particleToRemove);
			this.RemoveChild(particleToRemove);
		}
	}

	/**
	 * Handles inputs for the simulation
	 */
	public override void _Input(InputEvent inputEvent) {
		//Adds a liquid particle to the mouse's position if the left mouse button is pressed
		if (Input.IsActionPressed("add_liquid_particle")) {
			var liquidParticleInstance = (Node2D) this._liquidParticleScene.Instance();
			liquidParticleInstance.Position = this.GetViewport().GetMousePosition();
			this.AddChild(liquidParticleInstance);
			this._liquidParticles.Add((LiquidParticle) liquidParticleInstance);
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
				this._blockers.Add(this._blockerSegmentBeingPlaced);
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

			this._liquidParticles.Clear();
			this._blockers.Clear();
			this.Update();
		}
	}

	/**
	 * Draws all blockers
	 */
	public override void _Draw() {
		this._blockers.ForEach(segment => { this.DrawLine(segment.A, segment.B, Colors.Black); });
	}

}
