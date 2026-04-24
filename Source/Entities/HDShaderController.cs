using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.Entities;
using Celeste.Mod.YaoiHelper.Triggers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.Cil;

namespace Celeste.Mod.YaoiHelper.Entities;

[CustomEntity("YaoiHelper/HDShaderController")]
[Tracked]
public class HDShaderController : Entity {
	public bool RenderPlayerOver;

	private List<VirtualRenderTarget> mask_groups;

	public HDShaderController(EntityData data, Vector2 offset) : base() {
		Visible = false;
		RenderPlayerOver = data.Bool("render_player_over");
	}

	public override void Awake(Scene scene) {
		base.Awake(scene);
		mask_groups = new();

		foreach (string group in scene.Tracker.GetEntities<ShaderMask>().Cast<ShaderMask>().SelectMany(x => x.MaskGroups)) {
		    AddMaskGroup(group);
		}
	}


	private void AddMaskGroup(string name) {
		if (mask_groups.Select(x => x.Name).Contains($"hd-shader-mask-{name}")) return;
		mask_groups.Add(VirtualContent.CreateRenderTarget($"hd-shader-mask-{name}", 1920, 1080));
	}

	public void RemoveMaskGroup(string name) {
		if (!mask_groups.Select(x => x.Name).Contains($"hd-shader-mask-{name}")) return;
		mask_groups.Remove(mask_groups.First(x => x.Name == $"hd-shader-mask-{name}"));
	}

	public VirtualRenderTarget GetMaskGroupTarget(string name) {
		return mask_groups.FirstOrDefault(x => x.Name == $"hd-shader-mask-{name}", null) ?? throw new KeyNotFoundException("No matching mask group found");
	}
}

