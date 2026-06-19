using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.Entities;
using Celeste.Mod.YaoiHelper.Handlers;
using Celeste.Mod.YaoiHelper.Interfaces;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Entities;

[CustomEntity($"{nameof(YaoiHelper)}/{nameof(HDShaderController)}")]
[Tracked]
public sealed class HDShaderController : Entity {
	public Dictionary<string, VirtualRenderTarget> MaskGroups { get; set; } = [];

	public HDShaderController(EntityData data, Vector2 offset) : base(data.Position + offset) {
		Visible = false;
	}

	public override void Awake(Scene scene) {
		base.Awake(scene);
		MaskGroups.Clear();
		foreach (string group in scene.Tracker.Entities.Values.SelectMany(x => x).Where(x => typeof(IShaderMask).IsAssignableFrom(x.GetType())).Cast<IShaderMask>().SelectMany(x => x.MaskGroups)) {
			addMaskGroup(group);
		} 

		foreach (string group in TilesetShaderMaskHandler.TilesetMaskGroups.Values) {
			addMaskGroup(group);
		}
	}

	private void addMaskGroup(string name) {
		if (MaskGroups.Select(x => x.Value.Name).Contains($"hd-shader-mask-{name}")) return;
		MaskGroups.Add(name, VirtualContent.CreateRenderTarget($"hd-shader-mask-{name}", 1920, 1080));
	}

	private void removeMaskGroup(string name) {
		if (!MaskGroups.Select(x => x.Value.Name).Contains($"hd-shader-mask-{name}")) return;
		MaskGroups.Remove(name);
	}

	public VirtualRenderTarget? GetMaskGroupTarget(string name) {
		return MaskGroups.GetValueOrDefault(name);
	}
}

