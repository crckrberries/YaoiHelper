// XXX this is like radioactive 
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity("YaoiHelper/MouselineTrigger")]
public class MouselineTrigger(EntityData data, Vector2 offset) : Trigger(data, offset) {
	private Vector2 mousePos;
	public override void OnStay(Player player) {
		base.Update();

		if (player.StateMachine.State != 0) return;
		// player.Speed = (mousePos - player.Position) * 10; 
		player.Position = mousePos;

	}

	public override void Update() {
		base.Update();
		mousePos = (new Vector2(MInput.Mouse.X, MInput.Mouse.Y)) / 6 + SceneAs<Level>().LevelOffset;

	}

	public override void Render() {
		base.Render();

		Draw.Point(mousePos / 6 + SceneAs<Level>().LevelOffset, Color.Red);
	}
}
