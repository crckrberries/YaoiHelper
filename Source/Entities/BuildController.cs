// using Celeste.Mod.Entities;
// using Microsoft.Xna.Framework;
// using Microsoft.Xna.Framework.Input;
// using Monocle;
//
// namespace Celeste.Mod.YaoiHelper.Entities;
//
// [CustomEntity("YaoiHelper/BuildController")]
// [Tracked]
// public sealed class BuildController : Entity {
// 	private Vector2 mouse_pos;
// 	private bool building;
// 	private bool mining;
//
// 	private TileGrid tileGrid;
// 	public override void Awake(Scene scene) {
// 		base.Awake(scene);
// 	}
//
// 	public override void Update() {
// 		Level level = SceneAs<Level>();
// 		base.Update();
//
// 		MouseState state = MInput.Mouse.CurrentState;
// 		mouse_pos = new Vector2((MInput.Mouse.X - Engine.Viewport.X) / 6, (MInput.Mouse.Y - Engine.Viewport.Y) / 6);
// 		building = state.LeftButton.HasFlag(ButtonState.Pressed);
// 		mining = state.RightButton.HasFlag(ButtonState.Pressed);
//
// 		if (building) {
// 			level.SolidTiles.Grid[(int)mouse_pos.X / 8 + level.LevelSolidOffset.X, (int)mouse_pos.Y / 8 + level.LevelSolidOffset.Y] = true;
// 		}
// 	}
//
// 	public override void Render() {
// 		base.Render();
// 		Draw.HollowRect(new Vector2(mouse_pos.X - (mouse_pos.X % 8), mouse_pos.Y - (mouse_pos.Y % 8)) + SceneAs<Level>().LevelOffset, 8, 8, Color.Red);
// 	}
// }
