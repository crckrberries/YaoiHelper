using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity($"{nameof(YaoiHelper)}/{nameof(BuildRegion)}")]
[Tracked]
public sealed class BuildRegion : Entity {
	public BuildRegion(EntityData data, Vector2 offset) : base(data.Position + offset) {
		Collider = new Hitbox(data.Width, data.Height);
	}

	// public override void Render() {
	// 	base.Render();
	//
	// 	// TODO this is ugly
	// 	for (int i = (int)Collider.AbsoluteLeft; i <= Collider.AbsoluteRight; i += 8) {
	// 		Draw.Line(i, Collider.AbsoluteTop, i, Collider.AbsoluteBottom, Color.Gray);
	// 	}
	// 	for (int i = (int)Collider.AbsoluteTop; i <= Collider.AbsoluteBottom; i += 8) {
	// 		Draw.Line(Collider.AbsoluteLeft, i, Collider.AbsoluteRight, i, Color.Gray);
	// 	}
	//
	// }
}
