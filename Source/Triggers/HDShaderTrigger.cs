using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.Entities;
using Celeste.Mod.YaoiHelper.Types;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity("YaoiHelper/HDShaderTrigger")]
[Tracked]
public sealed class HDShaderTrigger : Trigger {
	public List<Shader> Shaders;
	public bool AlwaysActive;
	public bool Activated;

	public HDShaderTrigger(EntityData data, Vector2 offset) : base(data, offset) {
		Console.WriteLine(data.String("effects"));
		string[] MaskGroups = data.Attr("mask_groups").Split(',').Select(x => x.Trim()).ToArray();
		Shaders = data.Attr("effects").Split(',').Select(x => new Shader(new Effect(Engine.Graphics.GraphicsDevice, Everest.Content.Get($"Effects/{x.Trim()}.cso", true).Data), MaskGroups)).ToList();
		AlwaysActive = data.Bool("always_active");
		Activated = AlwaysActive;
	}

	public override void OnEnter(Player player) {
		base.OnEnter(player);
		Activated = true;
	}

	public override void OnLeave(Player player) {
		base.OnLeave(player);
		Activated = false || AlwaysActive;
	}
	
}
