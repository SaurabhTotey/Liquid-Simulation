using Godot;

public class Main : Control {

	private PackedScene _waterParticleScene;

	public override void _Ready() {
		this._waterParticleScene = ResourceLoader.Load<PackedScene>("res://WaterParticle.tscn");
	}

	public override void _Process(float delta) {
		if (Input.IsMouseButtonPressed(1)) {
			var waterParticleInstance = (Node2D) this._waterParticleScene.Instance();
			waterParticleInstance.Position = this.GetViewport().GetMousePosition();
			this.AddChild(waterParticleInstance);
			GD.Print("Gottem at " + this.GetViewport().GetMousePosition());
		}
	}

}
