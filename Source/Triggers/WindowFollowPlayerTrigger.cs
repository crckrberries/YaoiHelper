using System;
using System.Runtime.InteropServices;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity("YaoiHelper/WindowFollowPlayerTrigger")]
public sealed unsafe class WindowFollowPlayerTrigger : Trigger {
	public static Vector2 bounds;
	public static float zoom;
	private Vector2 windowSize => bounds / zoom;

	public WindowFollowPlayerTrigger(EntityData data, Vector2 offset) : base(data, offset) {
		zoom = data.Float("zoom_level", 6f);
	}

	[DllImport("SDL2", CharSet = CharSet.Unicode)]
	public static extern void SDL_SetWindowPosition(IntPtr window, int x, int y);


	public override void OnEnter(Player player) {
		base.OnEnter(player);
		bounds = new Vector2(Engine.Instance.Window.ClientBounds.Width, Engine.Instance.Window.ClientBounds.Height);
		Engine.SetWindowed((int)windowSize.X, (int)windowSize.Y);
		SceneAs<Level>().Camera.Zoom = zoom;
	}
	public override void OnStay(Player player) {
		base.OnStay(player);
		if (!player.Visible) return;

		SceneAs<Level>().Camera.Position = player.Position + player.Collider.Size / 2;
		SDL_SetWindowPosition(Engine.Instance.Window.Handle, (int)((player.Position.X - player.level.LevelOffset.X) * (bounds.X / 320f) - windowSize.X / 2), (int)((player.Position.Y - player.level.LevelOffset.Y) * (bounds.Y / 180f) - windowSize.Y / 2));
		SceneAs<Level>().Camera.CenterOrigin();
	}

	public override void OnLeave(Player player) {
		base.OnLeave(player);
		
		Engine.SetFullscreen();
		SceneAs<Level>().Camera.Origin = SceneAs<Level>().CameraOffset;
		SceneAs<Level>().Camera.Zoom = 1f;
	}
}
