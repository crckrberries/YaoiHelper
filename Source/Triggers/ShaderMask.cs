using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.Entities;
using Celeste.Mod.YaoiHelper.Handlers;
using Celeste.Mod.YaoiHelper.Interfaces;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity("YaoiHelper/ShaderMask")]
[Tracked]
public sealed class ShaderMask : Trigger, IShaderMask {
	public List<string> MaskGroups;

	public ShaderMask(EntityData data, Vector2 offset) : base(data, offset) {
		MaskGroups = data.Attr("mask_groups").Split(',').Select(x => x.Trim()).ToList();
		// TODO: remove these somewhere
		foreach (string group in MaskGroups) {
			HDShaderHandler.AddMaskGroup(group);
		}
	}

	public void RenderMask() {
		Draw.Rect(Collider.AbsolutePosition - SceneAs<Level>().LevelOffset, Collider.Width, Collider.Height, Color.White);
	}
}
