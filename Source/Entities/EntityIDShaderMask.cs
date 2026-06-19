using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.Entities;
using Celeste.Mod.YaoiHelper.Interfaces;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Entities;

[CustomEntity($"{nameof(YaoiHelper)}/{nameof(EntityIDShaderMask)}")]
[Tracked]
public sealed class EntityIDShaderMask : Entity, IShaderMask {
	private readonly List<int> ids;
	private List<Entity?> entities = [];

	public List<string> MaskGroups { get; private set; }
    public bool LowRes { get; set; }

    public EntityIDShaderMask(EntityData data, Vector2 offset) : base(data.Position + offset) {
		MaskGroups = data.Attr("mask_groups").Split(',').Select(x => x.Trim()).ToList();
		ids = data.Attr("entity_ids").Split(',').Select(x => int.Parse(x.Trim())).ToList();
		LowRes = data.Bool("low_res");
	}

    public override void Awake(Scene scene) {
		base.Awake(scene);
		if (scene is not Level level) return;
		entities = ids.Select(i => level.Entities.FirstOrDefault(x => x.SourceId.ID == i && x.SourceId.Level == SourceId.Level)).ToList();
		
    }

	public void RenderMask() {
		if (LowRes) {
			renderLowRes();
		} else {
			renderHiRes();
		}
	}

	private void renderHiRes() {
		if (Scene is not Level level) return;
		foreach (Entity? entity in entities) {
			Vector2? oldPos = entity?.Position;
			if (oldPos is Vector2 pos) {
				entity?.Position = Vector2.Transform(pos, level.Camera.Matrix);
				entity?.Render();
				entity?.Position = pos;
			}
		}
	}

    private void renderLowRes() {
		foreach (Entity? entity in entities) {
			entity?.Render();
		}
    }
}
