using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.Entities;
using Celeste.Mod.YaoiHelper.Interfaces;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity("YaoiHelper/ShaderMask")]
[Tracked]
public sealed class ShaderMask : Entity, IShaderMask {
	private readonly List<string> groups;
	public List<string> MaskGroups => groups;
	public MTexture image;

	public ShaderMask(EntityData data, Vector2 offset) : base(data.Position + offset) {
		groups = data.Attr("mask_groups").Split(',').Select(x => x.Trim()).ToList();
		Collider = new Hitbox(data.Width, data.Height);
		image = GFX.Game.GetOrDefault($"shadermasks/{data.Attr("mask_image")}", null);
	}

	public void RenderMask() {
		Vector2 position = Vector2.Transform(Collider.AbsolutePosition, SceneAs<Level>().Camera.Matrix);

		if (image == null) {
			Draw.Rect(position, Collider.Width, Collider.Height, Color.White);
		} else {
			image?.Draw(position, Vector2.Zero, Color.White, new Vector2(Width / image.Width, Height / image.Height));
		}
	}
}
