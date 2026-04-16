// XXX this is like radioactive 
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity("YaoiHelper/MouselineTrigger")]
public class MouselineTrigger(EntityData data, Vector2 offset) : Trigger(data, offset) {
	private Vector2 mousePos;

	public bool Fling = data.Bool("fling");
	public bool AllowDashing = data.Bool("allow_dashing");

	public override void OnStay(Player player) {
		base.Update();

		Vector2 last = player.Position;

		if (player.StateMachine.State != 0 && AllowDashing) return;
		player.Position = mousePos;
		if (!CollideCheck(player) && Fling) {
			player.Speed += (player.Position - last) * 10;
		}

	}

	public override void Update() {
		base.Update();
		mousePos = new Vector2(MInput.Mouse.X - Engine.Viewport.X, MInput.Mouse.Y - Engine.Viewport.Y) / 6 + SceneAs<Level>().LevelOffset;

	}

	public override void Render() {
		base.Render();
		Draw.Point(mousePos, Color.Red);
	}
}
