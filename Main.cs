using Godot;

/**
 * All the code for the liquid simulation
 */
public class Main : Control {

	//The packed scene for the water particle because it is potentially instanced many times
	private PackedScene _waterParticleScene;

	//The user can place 'blockers', which are static segments that interact with the water; this is the blocker that is currently being placed
	private SegmentShape2D _blockerSegmentBeingPlaced;

	/**
	 * Gets the WaterParticle scene as a packed scene on startup because it will be loaded many times
	 */
	public override void _Ready() {
		this._waterParticleScene = ResourceLoader.Load<PackedScene>("res://WaterParticle.tscn");
	}

	/**
	 * Handles inputs for the simulation
	 * TODO: use keymappings
	 */
	public override void _Process(float delta) {
		//Adds a water particle to the mouse's position if the left mouse button is pressed
		if (Input.IsMouseButtonPressed(1)) {
			var waterParticleInstance = (Node2D) this._waterParticleScene.Instance();
			waterParticleInstance.Position = this.GetViewport().GetMousePosition();
			this.AddChild(waterParticleInstance);
		}

		//If the right mouse button is being pressed, creates a blocker from the start of the press to the end of the press
		//TODO: draw blockers so that they are visible
		if (Input.IsMouseButtonPressed(2)) {
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
			}
			else {
				this._blockerSegmentBeingPlaced.B = this.GetViewport().GetMousePosition();
			}
		}
		else {
			this._blockerSegmentBeingPlaced = null;
		}

		//Removes all children if the escape key is pressed
		if (Input.IsKeyPressed(16777217)) {
			foreach (var child in this.GetChildren()) {
				this.RemoveChild((Node) child);
			}
		}
	}

}
