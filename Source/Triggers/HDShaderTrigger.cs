using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.Entities;
using Celeste.Mod.YaoiHelper.Types;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity(["YaoiHelper/HDShader", $"{nameof(YaoiHelper)}/{nameof(HDShaderTrigger)}"])]
[Tracked]
public sealed class HDShaderTrigger : Trigger {
	public List<Shader> Shaders;
	private bool active;
	private readonly bool alwaysActive;
	private readonly string flagName;

	public bool Activated(Level level) {
		return active && flag(level);
	}

	private bool flag(Level level) {
		return flagName switch {
			"" => true,
			_ => level.Session.GetFlag(flagName),
		};
	}

	public HDShaderTrigger(EntityData data, Vector2 offset) : base(data, offset) {
		string[] Textures = data.Attr("textures").Split(',').Select(x => x.Trim()).ToArray();
		Shaders = data.Attr("effects").Split(',').Select(x => new Shader(new Effect(Engine.Graphics.GraphicsDevice, Everest.Content.Get($"Effects/{x.Trim()}.cso", true).Data), Textures)).ToList();
		flagName = data.Attr("flag");
		alwaysActive = data.Bool("always_active");
	}

	public override void Awake(Scene scene) {
		base.Awake(scene);
		active = alwaysActive;
	}

	public override void OnEnter(Player player) {
		base.OnEnter(player);
		active = /* true && */flag(player.level);
	}

	public override void OnLeave(Player player) {
		base.OnLeave(player);
		active = /* false || */alwaysActive;
	}
}
