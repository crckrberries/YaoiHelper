using System;
using System.Runtime.InteropServices;
using Celeste;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Crackerberries.YaoiHelper.Triggers;

[CustomEntity($"{nameof(YaoiHelper)}/{nameof(MinimizeGameTrigger)}")]
public sealed partial class MinimizeGameTrigger(EntityData data, Vector2 offset) : Trigger(data, offset) {
	public override void OnEnter(Player player) {
		base.OnEnter(player);
		SDL2.SDL.SDL_MinimizeWindow(Engine.Instance.Window.Handle);
	}
}
