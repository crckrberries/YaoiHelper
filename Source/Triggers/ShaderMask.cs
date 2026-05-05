using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.Entities;
using Celeste.Mod.YaoiHelper.Handlers;
using Celeste.Mod.YaoiHelper.Interfaces;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

// TODO: this should probably be an entity
[CustomEntity("YaoiHelper/ShaderMask")]
[Tracked]
public sealed class ShaderMask(EntityData data, Vector2 offset) : Trigger(data, offset), IShaderMask {
	private readonly List<string> groups = data.Attr("mask_groups").Split(',').Select(x => x.Trim()).ToList();

	public List<string> MaskGroups => groups;
	public MTexture image = GFX.Game.GetOrDefault($"shadermasks/{data.Attr("mask_image")}", null);

	public void RenderMask() {
		Vector2 position = Vector2.Transform(Collider.AbsolutePosition, SceneAs<Level>().Camera.Matrix);

		if (image == null) {
			Draw.Rect(position, Collider.Width, Collider.Height, Color.White);
		} else {
			image?.Draw(position, Vector2.Zero, Color.White, new Vector2(Width / image.Width, Height / image.Height));
		}
	}
}
