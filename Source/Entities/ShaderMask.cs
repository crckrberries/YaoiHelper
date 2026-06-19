using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.Entities;
using Celeste.Mod.YaoiHelper.Interfaces;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Entities;

[Tracked]
[CustomEntity($"{nameof(YaoiHelper)}/{nameof(ShaderMask)}")]
public sealed class ShaderMask : Entity, IShaderMask {
	private readonly List<string> groups;
	private readonly MTexture image;

	public List<string> MaskGroups => groups;
	public bool LowRes { get; set; }

	public ShaderMask(EntityData data, Vector2 offset) : base(data.Position + offset) {
		groups = data.Attr("mask_groups").Split(',').Select(x => x.Trim()).ToList();
		LowRes = data.Bool("low_res");
		Collider = new Hitbox(data.Width, data.Height);
		image = GFX.Game.GetOrDefault($"shadermasks/{data.Attr("mask_image")}", null);
	}

	public void RenderMask() {
		if (LowRes) {
			renderLowRes();
		} else {
			renderHiRes();
		}
	}

	private void renderLowRes() {
		Vector2 position = Collider.AbsolutePosition;

		if (image == null) {
			Draw.Rect(position, Collider.Width, Collider.Height, Color.White);
		} else {
			image?.Draw(position, Vector2.Zero, Color.White, new Vector2(Width / image.Width, Height / image.Height));
		}
	}

	private void renderHiRes() {
		Vector2 position = Vector2.Transform(Collider.AbsolutePosition, SceneAs<Level>().Camera.Matrix);

		if (image == null) {
			Draw.Rect(position, Collider.Width, Collider.Height, Color.White);
		} else {
			image?.Draw(position, Vector2.Zero, Color.White, new Vector2(Width / image.Width, Height / image.Height));
		}
	}
}
