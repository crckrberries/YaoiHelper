using System;
using System.Linq;
using Celeste.Mod.Entities;
using Celeste.Mod.YaoiHelper.Triggers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Entities;

[CustomEntity("YaoiHelper/BuildController")]
public sealed class BuildController : Entity {
	private Vector2 mouse_pos;
	private bool building;
	private bool mining;
	private bool valid_position;

	private readonly char selectedTile;

	public BuildController(EntityData data, Vector2 offset) : base() {
		Depth = -10000;
		selectedTile = '3';
	}

	public override void Awake(Scene scene) {
		base.Awake(scene);
	}

	public override void Update() {
		Level level = SceneAs<Level>();
		base.Update();

		MouseState state = MInput.Mouse.CurrentState;
		mouse_pos = level.ScreenToWorld(new Vector2(MInput.Mouse.X - Engine.Viewport.X, MInput.Mouse.Y - Engine.Viewport.Y)) - level.LevelOffset;
		Point tile = new Point((int)mouse_pos.X / 8, (int)mouse_pos.Y / 8) + level.LevelSolidOffset;

		building = state.LeftButton.HasFlag(ButtonState.Pressed);
		mining = state.RightButton.HasFlag(ButtonState.Pressed);


		if (level.Tracker.CountEntities<BuildRegion>() == 0) {
			valid_position = true;
		} else {
			valid_position = false;
			foreach (BuildRegion buildRegion in level.Tracker.GetEntities<BuildRegion>().Cast<BuildRegion>()) {
				valid_position = valid_position || (buildRegion.Collider as Hitbox).Collide(mouse_pos + level.LevelOffset);
			}
		}

		if (!(building || mining) || !valid_position) return;


		if (building) {
			if (level.SolidsData[tile.X, tile.Y] == '0')  {
				level.SolidTiles.Grid[tile.X, tile.Y] = true;
				level.SolidsData[tile.X, tile.Y] = selectedTile;
				UpdateTilesAround(level, tile, 2);
			}
		} else { // mining
			if (level.SolidsData[tile.X, tile.Y] != '0') {
				level.SolidTiles.Grid[tile.X, tile.Y] = false;
				level.SolidsData[tile.X, tile.Y] = '0';
				UpdateTilesAround(level, tile, 2);
			}
		}
		
	}

	private static void UpdateTilesAround(Level level, Point tile, int radius) {
		Autotiler.Generated genned = GFX.FGAutotiler.Generate(level.SolidsData, tile.X - radius, tile.Y - radius, 2 * radius + 1, 2 * radius + 1, forceSolid: false, '0', new Autotiler.Behaviour {
			EdgesExtend = true,
			EdgesIgnoreOutOfLevel = false,
			PaddingIgnoreOutOfLevel = false
		});

		for (int i = -radius; i <= radius; i++) {
			for (int j = -radius; j <= radius; j++) {
				level.SolidTiles.Tiles.Tiles[tile.X + i, tile.Y + j] = genned.TileGrid.Tiles[i + radius, j + radius];
			}
		}
	}

	public override void Render() {
		base.Render();
		Draw.HollowRect(new Vector2(mouse_pos.X - (mouse_pos.X % 8), mouse_pos.Y - (mouse_pos.Y % 8)) + SceneAs<Level>().LevelOffset, 8, 8, valid_position switch {
					false => Color.Red,
					true when building || mining => Color.Yellow,
					_ => Color.LightGreen
				});
	}
}
