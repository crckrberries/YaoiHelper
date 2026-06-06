using Celeste.Mod.Entities;
using Celeste.Mod.YaoiHelper.Handlers;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Entities;

[CustomEntity($"{nameof(YaoiHelper)}/{nameof(BuildController)}")]
[Tracked(false)]
public sealed class BuildController : Entity {
	private readonly int tileLimit;
	private readonly bool unlimited;
	private readonly bool allowEntityMode;

	public BuildController(EntityData data, Vector2 offset) : base(data.Position + offset) {
		tileLimit = data.Int("tile_limit");
		unlimited = data.Bool("unlimited");
		allowEntityMode = data.Bool("allow_entity_mode");

	}

	public override void Awake(Scene scene) {
		base.Awake(scene);
		if (scene is not Level level) return;
		BuildHandler.TileLimit = tileLimit;
		BuildHandler.Unlimited = unlimited;
		BuildHandler.AllowEntityMode = allowEntityMode;
	}
}
