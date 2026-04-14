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
	public bool HiRes = data.Bool("hi_res", false);

	public void RenderMask() {
		if (HiRes) {
			renderhires();
		} else {
			renderlowres();
		}
	}

	private void renderlowres() {
		Draw.Rect(Vector2.Transform(Collider.AbsolutePosition, SceneAs<Level>().Camera.Matrix), Collider.Width, Collider.Height, Color.White);
	}
	private void renderhires() {
		Draw.Rect(Vector2.Transform(Collider.AbsolutePosition, SceneAs<Level>().Camera.Matrix * Matrix.CreateScale(6f)), Collider.Width * 6, Collider.Height * 6, Color.White);
	}
}
