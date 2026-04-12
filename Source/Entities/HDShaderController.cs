using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Entities;

[CustomEntity("YaoiHelper/HDShaderController")]
[Tracked]
public class HDShaderController : Entity {
	public bool RenderPlayerOver;

	public HDShaderController(EntityData data, Vector2 offset) : base() {
		RenderPlayerOver = data.Bool("render_player_over");
	}
}

