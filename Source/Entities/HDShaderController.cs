using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Entities;

[CustomEntity($"{nameof(YaoiHelper)}/{nameof(HDShaderController)}")]
[Tracked]
public sealed class HDShaderController : Entity {
	public readonly bool RenderPlayerOver;
	public readonly bool RenderLevelOver;

	private readonly List<VirtualRenderTarget> maskGroups = new();

	public HDShaderController(EntityData data, Vector2 offset) : base(data.Position + offset) {
		Visible = false;
		RenderPlayerOver = data.Bool("render_player_over");
		RenderLevelOver = data.Bool("render_level_over");
	}

	public override void Awake(Scene scene) {
		base.Awake(scene);
		maskGroups.Clear();
		foreach (string group in scene.Tracker.GetEntities<ShaderMask>().Cast<ShaderMask>().SelectMany(x => x.MaskGroups)) {
			addMaskGroup(group);
		}
	}

	private void addMaskGroup(string name) {
		if (maskGroups.Select(x => x.Name).Contains($"hd-shader-mask-{name}")) return;
		maskGroups.Add(VirtualContent.CreateRenderTarget($"hd-shader-mask-{name}", 1920, 1080));
	}

	public void RemoveMaskGroup(string name) {
		if (!maskGroups.Select(x => x.Name).Contains($"hd-shader-mask-{name}")) return;
		maskGroups.Remove(maskGroups.First(x => x.Name == $"hd-shader-mask-{name}"));
	}

	public VirtualRenderTarget? GetMaskGroupTarget(string name) {
		return maskGroups.FirstOrDefault(x => x?.Name == $"hd-shader-mask-{name}", null);
	}
}

