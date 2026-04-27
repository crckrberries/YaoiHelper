// XXX this is like radioactive 
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity("YaoiHelper/MouselineTrigger")]
public class MouselineTrigger : Trigger {
	private Vector2 mousePos;

	public bool Fling;
	public bool AllowDashing;

	public MouselineTrigger(EntityData data, Vector2 offset) : base(data, offset) {
		Visible = true;
		Fling = data.Bool("fling");
		AllowDashing = data.Bool("allow_dashing");
	}

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
		mousePos = SceneAs<Level>().ScreenToWorld(new Vector2(MInput.Mouse.X - Engine.Viewport.X, MInput.Mouse.Y - Engine.Viewport.Y));

	}

	public override void Render() {
		base.Render();
		Draw.Circle(mousePos, 5, Color.Red, 5);
	}
}
