using System;
using System.Runtime.InteropServices;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity("YaoiHelper/MinimizeGameTrigger")]
public sealed class MinimizeGameTrigger(EntityData data, Vector2 offset) : Trigger(data, offset) {
	[DllImport("SDL2", CharSet = CharSet.Unicode)]
	private static extern void SDL_MinimizeWindow(IntPtr window);

    public override void OnEnter(Player player) {
        base.OnEnter(player);
		SDL_MinimizeWindow(Engine.Instance.Window.Handle);
    }
}
