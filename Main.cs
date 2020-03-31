using System.Collections.Generic;
using Godot;

/**
 * All the code for the liquid simulation
 */
public class Main : Control {

	//The packed scene for the water particle because it is potentially instanced many times
	private PackedScene _waterParticleScene;

	//The user can place 'blockers', which are static segments that interact with the water; this is the blocker that is currently being placed
	private SegmentShape2D _blockerSegmentBeingPlaced;

	//A list of all water particles so that they can exert forces on each other
	private readonly List<RigidBody2D> _allWaterParticles = new List<RigidBody2D>();
	
	//A list of all blockers so that they can be drawn
	private readonly List<SegmentShape2D> _allBlockers = new List<SegmentShape2D>();

	/**
	 * Gets the WaterParticle scene as a packed scene on startup because it will be loaded many times
	 */
	public override void _Ready() {
		this._waterParticleScene = ResourceLoader.Load<PackedScene>("res://WaterParticle.tscn");
	}

	/**
	 * Handles attraction between water particles
	 * TODO: check out https://www.researchgate.net/publication/220789321_Particle-based_viscoelastic_fluid_simulation
	 */
	public override void _PhysicsProcess(float delta) {
		base._PhysicsProcess(delta);
	}

	/**
	 * Removes particles from the list of interacting particles if their center is out of bounds
	 */
	public override void _Process(float delta) {
		this._allWaterParticles.RemoveAll(waterParticle => !this.GetViewportRect().HasPoint(waterParticle.Position));
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
			this._allWaterParticles.Add((RigidBody2D) waterParticleInstance);
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
		this._allBlockers.ForEach(segment => {
			this.DrawLine(segment.A, segment.B, Colors.Black);
		});
	}

}
