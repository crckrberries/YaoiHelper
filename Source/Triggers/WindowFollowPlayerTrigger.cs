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
	private Vector2 windowSize => new Vector2(1920, 1080) / zoom;

	public WindowFollowPlayerTrigger(EntityData data, Vector2 offset) : base(data, offset) {
		zoom = data.Float("zoom_level", 6f);
	}

	[DllImport("SDL2", CharSet = CharSet.Unicode)]
	public static extern void SDL_SetWindowPosition(IntPtr window, int x, int y);

	[DllImport("SDL2", CharSet = CharSet.Unicode)]
	public static extern IntPtr SDL_GetRenderer(IntPtr window);

	[DllImport("SDL2", CharSet = CharSet.Unicode)]
	public static extern void SDL_GetRendererOutputSize(IntPtr renderer, int *x, int *y);

	// [DllImport("SDL2", CharSet = CharSet.Unicode)]
	// public static extern char *SDL_GetError();

	public override void OnEnter(Player player) {
		base.OnEnter(player);
		int x, y;
		// TODO: this doesn't work on macos hence why it's commented out and the bounds are hardcoded in
		SDL_GetRendererOutputSize(SDL_GetRenderer(Engine.Instance.Window.Handle), &x, &y);
		bounds = new Vector2(x, y);
		// bounds = new Vector2(1470, 956);
		Engine.SetWindowed((int)windowSize.X, (int)windowSize.Y);
		player.SceneAs<Level>().Camera.Zoom = zoom;
	}
	public override void OnStay(Player player) {
		base.OnStay(player);
		// if (!player.Active) return;

		player.SceneAs<Level>().Camera.Position = player.Position + player.Collider.Size / 2;
		player.SceneAs<Level>().Camera.CenterOrigin();
		SDL_SetWindowPosition(Engine.Instance.Window.Handle, (int)((player.Position.X - player.level.LevelOffset.X) * (bounds.X / 320f) - windowSize.X / 2), (int)((player.Position.Y - player.level.LevelOffset.Y) * (bounds.Y / 180f) - windowSize.Y / 2));
	}

	public override void OnLeave(Player player) {
		base.OnLeave(player);
		Engine.SetFullscreen();
		player.SceneAs<Level>().Camera.Zoom = 1f;
	}
}
