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
public sealed class ShaderMask(EntityData data, Vector2 offset) : Trigger(data, offset), IShaderMask {
	public List<string> MaskGroups = data.Attr("mask_groups").Split(',').Select(x => x.Trim()).ToList();

	public void RenderMask() {
		Draw.Rect(Collider.AbsolutePosition - SceneAs<Level>().LevelOffset, Collider.Width, Collider.Height, Color.White);
	}
}
