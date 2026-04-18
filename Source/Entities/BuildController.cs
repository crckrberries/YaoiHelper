using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Entities;

[CustomEntity("YaoiHelper/BuildController")]
[Tracked]
public sealed class BuildController : Entity {
	private Vector2 mouse_pos;
	private bool building;
	private bool mining;

	private TileGrid tileGrid;
	private Grid grid;
	public BuildController(EntityData data, Vector2 offset) : base(offset) {
	}
	public override void Awake(Scene scene) {
		base.Awake(scene);
		Collider = grid;
		tileGrid = GFX.FGAutotiler.GenerateOverlay('3', 10, 10, 10, 10, SceneAs<Level>().SolidsData).TileGrid;
		Add(tileGrid);
	}

	public override void Update() {
		Level level = SceneAs<Level>();
		base.Update();

		MouseState state = MInput.Mouse.CurrentState;
		mouse_pos = new Vector2((MInput.Mouse.X - Engine.Viewport.X) / 6, (MInput.Mouse.Y - Engine.Viewport.Y) / 6) + level.CameraOffset;
		building = state.LeftButton.HasFlag(ButtonState.Pressed);
		mining = state.RightButton.HasFlag(ButtonState.Pressed);

		if (building) {
			Remove(tileGrid);
			Point tile = new Point((int)mouse_pos.X / 8 + level.LevelSolidOffset.X, (int)mouse_pos.Y / 8 + level.LevelSolidOffset.Y);
			level.SolidTiles.Grid[tile.X, tile.Y] = true;
			tileGrid = GFX.FGAutotiler.GenerateOverlay('3', tile.X, tile.Y, 1, 1, level.SolidsData).TileGrid;
			Add(tileGrid);
		}
	}

	public override void Render() {
		base.Render();
		Draw.HollowRect(new Vector2(mouse_pos.X - (mouse_pos.X % 8), mouse_pos.Y - (mouse_pos.Y % 8)) + SceneAs<Level>().LevelOffset, 8, 8, Color.Red);
	}
}
