using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/**
 * All the code for the liquid simulation
 * Liquid is simulated in Lagrangian method by using discrete liquid particles
 * TODO: RestDensity, Stiffness, NearStiffness, LinearViscosity, and QuadraticViscosity seem like they would be better as properties of LiquidParticle
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

	//The spring constant that is symbolized by k in Hooke's Law
	[Export] public float SpringConstant = 1f;

	//A constant that controls how much a spring's rest length changes each time-step
	[Export] public float PlasticityConstant = 5f;

	//A spring's rest-length will only change if the difference between distance and rest-length is larger than this fraction of current rest-length
	[Export] public float YieldRatio = 0.2f;

	//How much linear dependence the liquid's viscosity has on velocity
	[Export] public float LinearViscosity = 1f;

	//How much quadratic dependence the liquid's viscosity has on velocity
	[Export] public float QuadraticViscosity = 1f;

	//The packed scene for the liquid particle because it is potentially instanced many times
	private PackedScene _liquidParticleScene;

	//The user can place 'blockers', which are static segments that interact with the liquid; this is the blocker that is currently being placed
	private SegmentShape2D _blockerSegmentBeingPlaced;

	//A list of all liquid particles so that they can have their physics controlled
	private readonly List<LiquidParticle> _liquidParticles = new List<LiquidParticle>();

	//A list of all springs between particles
	private readonly List<Spring> _springs = new List<Spring>();

	//A list of all blockers so that they can be drawn
	private readonly List<SegmentShape2D> _blockers = new List<SegmentShape2D>();

	/**
	 * Gets the LiquidParticle scene as a packed scene on startup because it will be loaded many times
	 */
	public override void _Ready() {
		this._liquidParticleScene = ResourceLoader.Load<PackedScene>("res://LiquidParticle.tscn");
	}

	/**
	 * Returns a vector from particle1 to particle2 if they are neighbors, null otherwise
	 */
	private Vector2? GetOffsetIfNeighbors(LiquidParticle particle1, LiquidParticle particle2) {
		if (!particle1.PotentialNeighbors.Contains(particle2)) {
			return null;
		}

		var offset = particle2.Position - particle1.Position;
		if (offset.LengthSquared() < Math.Pow(this.InteractionRadius, 2)) {
			return offset;
		}

		return null;
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
			liquidParticle.PotentialNeighbors.Clear();
			liquidParticle.Density = 0;
			liquidParticle.NearDensity = 0;
		}

		//Finds potential particle neighbors for each particle
		for (var i = 1; i < this._liquidParticles.Count; i++) {
			var particle1 = this._liquidParticles[i];
			for (var j = 0; j < i; j++) {
				var particle2 = this._liquidParticles[j];
				var offset = particle2.Position - particle1.Position;
				if (offset.LengthSquared() >= 5 * Math.Pow(this.InteractionRadius, 2)) {
					continue;
				}

				particle1.PotentialNeighbors.Add(particle2);
				particle2.PotentialNeighbors.Add(particle1);
			}
		}

		//Applies viscosity impulses TODO: this code is seemingly buggy; it will hopefully be fixed with collisions implemented
		for (var i = 1; i < this._liquidParticles.Count; i++) {
			var particle1 = this._liquidParticles[i];
			for (var j = 0; j < i; j++) { //TODO: this looping over pairs code can possibly be abstracted
				var particle2 = this._liquidParticles[j];
				var offset = this.GetOffsetIfNeighbors(particle1, particle2);
				if (!offset.HasValue) {
					continue;
				}

				var unitOffset = offset.Value.Normalized();
				var inwardRadialSpeed = (particle1.Velocity - particle2.Velocity).Dot(unitOffset);
				if (inwardRadialSpeed <= 0) {
					continue;
				}

				var impulse = (float) (delta * (1 - offset.Value.Length() / this.InteractionRadius) * (this.LinearViscosity * inwardRadialSpeed + this.QuadraticViscosity * Math.Pow(inwardRadialSpeed, 2)) / 2f) * unitOffset;
				particle1.Velocity -= impulse;
				particle2.Velocity += impulse;
			}
		}

		//Saves each particle's current position and advances it to its forward euler's method velocity-based predicted position
		foreach (var liquidParticle in this._liquidParticles) {
			liquidParticle.OldPosition = new Vector2(liquidParticle.Position);
			liquidParticle.Position += liquidParticle.Velocity * delta;
		}

		//Adds and adjusts springs TODO: consider moving this code into a Spring method
		for (var i = 1; i < this._liquidParticles.Count; i++) {
			var particle1 = this._liquidParticles[i];
			for (var j = 0; j < i; j++) {
				var particle2 = this._liquidParticles[j];
				var offset = this.GetOffsetIfNeighbors(particle1, particle2);
				if (!offset.HasValue) {
					continue;
				}

				Spring spring;
				if (particle1.ParticleToSpring.ContainsKey(particle2)) {
					spring = particle1.ParticleToSpring[particle2];
				}
				else {
					spring = new Spring(this.InteractionRadius, particle1, particle2);
					this._springs.Add(spring);
				}

				var tolerableDeformation = this.YieldRatio * spring.RestLength;
				var distance = offset.Value.Length();
				if (distance > spring.RestLength + tolerableDeformation) {
					spring.RestLength += delta * this.PlasticityConstant *
										 (distance - spring.RestLength - tolerableDeformation);
				}
				else if (distance < spring.RestLength - tolerableDeformation) {
					spring.RestLength -= delta * this.PlasticityConstant *
										 (spring.RestLength - distance - tolerableDeformation);
				}
			}
		}

		//Removes springs between particles if they are too far apart; TODO: consider moving into a spring method
		foreach (var spring in from spring in this._springs
			let a = spring.A
			let b = spring.B
			where (a.Position - b.Position).LengthSquared() >= Math.Pow(this.InteractionRadius, 2)
			select spring) {
			spring.Remove();
		}

		//Applies displacements to particles based on spring forces for springs that are between particles
		foreach (var spring in this._springs) {
			var offset = spring.B.Position - spring.A.Position;
			var displacementTerm = (float) (Math.Pow(delta, 2) * this.SpringConstant *
				(1 - spring.RestLength / this.InteractionRadius) *
				(spring.RestLength - offset.Length()) / 2f) * offset;
			spring.A.Position -= displacementTerm;
			spring.B.Position += displacementTerm;
		}

		//Applies double-density relaxations
		foreach (var liquidParticle in this._liquidParticles) {
			foreach (var inverseNormalizedDistance in from potentialNeighbor in liquidParticle.PotentialNeighbors
				select this.GetOffsetIfNeighbors(liquidParticle, potentialNeighbor)
				into offset
				where offset.HasValue
				select 1 - offset.Value.Length() / this.InteractionRadius) {
				liquidParticle.Density += (float) Math.Pow(inverseNormalizedDistance, 2);
				liquidParticle.NearDensity += (float) Math.Pow(inverseNormalizedDistance, 3);
			}

			var pressure = this.Stiffness * (liquidParticle.Density - this.RestDensity);
			var nearPressure = this.NearStiffness * liquidParticle.NearDensity;
			var selfDisplacement = Vector2.Zero;

			foreach (var potentialNeighbor in liquidParticle.PotentialNeighbors) {
				var offset = this.GetOffsetIfNeighbors(liquidParticle, potentialNeighbor);
				if (!offset.HasValue) {
					continue;
				}

				var inverseNormalizedDistance = 1 - offset.Value.Length() / this.InteractionRadius;
				var displacementTerm = (float) (Math.Pow(delta, 2) *
										   (pressure * inverseNormalizedDistance +
											nearPressure * Math.Pow(inverseNormalizedDistance, 2)) / 2f) *
									   offset.Value;
				potentialNeighbor.Position += displacementTerm;
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
			foreach (var spring in particleToRemove.ParticleToSpring.Values.ToList()) {
				this._springs.Remove(spring);
				spring.Remove();
			}

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
			this._springs.Clear();
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
