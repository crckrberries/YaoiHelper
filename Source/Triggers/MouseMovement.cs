using System.Linq;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity(["YaoiHelper/MouseMovement", "YaoiHelper/MouselineTrigger", $"{nameof(YaoiHelper)}/{nameof(MouseMovementTrigger)}"])]
public sealed class MouseMovementTrigger : Trigger {
	private Vector2 mousePos;

	private readonly bool fling;
	private readonly bool clickAndDrag;
	private readonly bool allowDashing;
	private readonly bool drawCursor;
	
	private Vector2 grabOffset = Vector2.Zero;
	private bool grabbed = false;
	private bool canGrab(Player player) => CollideCheck(player) && (!clickAndDrag || ((Hitbox)player.Collider).Collide(mousePos));

	public MouseMovementTrigger(EntityData data, Vector2 offset) : base(data, offset) {
		fling = data.Bool("fling");
		allowDashing = data.Bool("allow_dashing");
		clickAndDrag = data.Bool("click_and_drag");
		Visible = drawCursor = data.Bool("draw_cursor");
		Depth = -1000;
	}

	public override void Update() {
		base.Update();
		if (Scene is not Level level) return;
		if (level.Tracker.GetEntities<Player>().OfType<Player>().FirstOrDefault() is not Player player) return;

		mousePos = SceneAs<Level>().ScreenToWorld(new Vector2(MInput.Mouse.X - Engine.Viewport.X, MInput.Mouse.Y - Engine.Viewport.Y));
		

		if (!grabbed && canGrab(player)) {
			grabbed = true;
			grabOffset = mousePos - player.Position;
		} 

		grabbed = grabbed && MInput.Mouse.CheckLeftButton;
		player.onGround = grabbed;

		Vector2 last = player.Position;

		if (grabbed && !(player.StateMachine.State != 0 && allowDashing)) {
			player.Position = mousePos - grabOffset;
		} 
		
		// fix by: wellington. follow for more fixes/improvements that will make your silly ahh lose it 
		if (fling) { 
			player.Speed += (player.Position - last) * 10;
		}

	}

	public override void Render() {
		base.Render();
		if (!drawCursor) return;
		Draw.Circle(mousePos, 3, grabbed ? Color.LightGreen : Color.Red, 5);
	}
}
