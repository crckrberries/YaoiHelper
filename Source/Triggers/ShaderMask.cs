using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity("YaoiHelper/ShaderMask")]
[Tracked]
public sealed class ShaderMask(EntityData data, Vector2 offset) : Trigger(data, offset) {
	public void RenderMask() {
		Draw.Rect(Collider.AbsolutePosition - SceneAs<Level>().LevelOffset, Collider.Width, Collider.Height, Color.White);
	}
}
