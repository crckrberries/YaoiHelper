using System;
using System.Runtime.InteropServices;
using Celeste;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Crackerberries.YaoiHelper.Triggers;

[CustomEntity(["YaoiHelper/WindowDance", $"{nameof(YaoiHelper)}/{nameof(WindowDanceTrigger)}"])]
public sealed partial class WindowDanceTrigger : Trigger {
	private Vector2 bounds;
	private readonly float zoom;
	private Vector2 windowSize => bounds / zoom;

	public WindowDanceTrigger(EntityData data, Vector2 offset) : base(data, offset) {
		zoom = data.Float("zoom_level", 6f);
	}

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
		SDL2.SDL.SDL_SetWindowPosition(Engine.Instance.Window.Handle, (int)((player.Position.X - player.level.LevelOffset.X) * (bounds.X / 320f) - windowSize.X / 2), (int)((player.Position.Y - player.level.LevelOffset.Y) * (bounds.Y / 180f) - windowSize.Y / 2));
		SceneAs<Level>().Camera.CenterOrigin();
	}

	public override void OnLeave(Player player) {
		base.OnLeave(player);
		
		Engine.SetFullscreen();
		SceneAs<Level>().Camera.Origin = SceneAs<Level>().CameraOffset;
		SceneAs<Level>().Camera.Zoom = 1f;
	}
}
