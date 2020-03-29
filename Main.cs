using Godot;

public class Main : Control {

	private PackedScene _waterParticleScene;

	private SegmentShape2D _currentPlacedBlocker;

	public override void _Ready() {
		this._waterParticleScene = ResourceLoader.Load<PackedScene>("res://WaterParticle.tscn");
	}

	public override void _Process(float delta) {
		if (Input.IsMouseButtonPressed(1)) {
			var waterParticleInstance = (Node2D) this._waterParticleScene.Instance();
			waterParticleInstance.Position = this.GetViewport().GetMousePosition();
			this.AddChild(waterParticleInstance);
		}

		//TODO: draw blockers so that they are visible
		if (Input.IsMouseButtonPressed(2)) {
			if (this._currentPlacedBlocker == null) {
				this._currentPlacedBlocker = new SegmentShape2D {
					A = this.GetViewport().GetMousePosition(),
					B = this.GetViewport().GetMousePosition()
				};
				var body = new StaticBody2D();
				body.AddChild(new CollisionShape2D {
					Shape = this._currentPlacedBlocker
				});
				this.AddChild(body);
			}
			else {
				this._currentPlacedBlocker.B = this.GetViewport().GetMousePosition();
			}
		}
		else {
			this._currentPlacedBlocker = null;
		}
		
		//KeyList.Escape
		if (Input.IsKeyPressed(16777217)) {
			foreach (var child in this.GetChildren()) {
				this.RemoveChild((Node) child);		
			}
		}
	}

}
