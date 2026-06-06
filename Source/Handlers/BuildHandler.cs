using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Celeste.Mod.YaoiHelper.Entities;
using Celeste.Mod.YaoiHelper.Triggers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.YaoiHelper.Handlers;

public enum BuildMode {
	Tiles,
	Entities
}

[Submodule]
public static class BuildHandler {
	public static BuildMode Mode { get; private set; } = BuildMode.Tiles;
	public static Vector2 MousePos { get; private set; }
	private static float modeSwitchDoubleTapTimer = 0f;
	public static bool AllowEntityMode { get; set; }

	public static bool BuildRoom(string level) => tileModifications.ContainsKey(level) || Mode == BuildMode.Entities;

	// tile stuff
	// ---------------------------------------------------------------------------
	private static Dictionary<string, Dictionary<Point, TileModification>> tileModifications = [];
	private static readonly char selectedTile = '3';

	public static bool TileBuilding { get; private set; }
	public static bool TileMining { get; private set; }
	public static bool IsValidPosition { get; private set; }
	
	private static int tileLimit = -1;
	private static bool unlimited = true;

	public static int TileLimit { get => tileLimit; set => tileLimit = value; }
	public static bool Unlimited { get => unlimited || YaoiHelperModule.Settings.BuildAnywhere; set => unlimited = value; }

	public static int TilesLeft(string level) => TileLimit - tileModifications[level].Count(x => x.Value.Type == TileModificationType.Built);
	// ---------------------------------------------------------------------------
	// tile stuff


	// entity stuff
	// ---------------------------------------------------------------------------
	public static List<Entity> Selection { get; private set; } = [];

	public static bool DragSelecting { get; private set; } = false;
	public static Hitbox? DragSelectBox { get; private set; }
	private static float? dragSelectionSwitchoverX, dragSelectionSwitchoverY = null;

	public static bool Dragging { get; private set; } = false;
	private static Vector2 dragOrigin;
	private static int dragSnapThreshold = 8; 

	// public static List<Entity>? Clipboard { get; private set; }
	// public static Vector2? CopyLocation { get; private set; } = null;
	// ---------------------------------------------------------------------------
	// entity stuff
	
	internal static void ApplyHooks() {
		On.Celeste.Level.Update += On_LevelUpdate_Build;
		Everest.Events.LevelLoader.OnLoadingThread += OnLoadingThread_AddCursorDisplayAndClearBuilds;
		Everest.Events.Level.OnLoadLevel += OnLoadLevel_ClearEntitySelection;
	}

	internal static void RemoveHooks() {
		On.Celeste.Level.Update -= On_LevelUpdate_Build;
		Everest.Events.LevelLoader.OnLoadingThread -= OnLoadingThread_AddCursorDisplayAndClearBuilds;
		Everest.Events.Level.OnLoadLevel -= OnLoadLevel_ClearEntitySelection;
	}

	public static void ResetTileModifications() {
		tileModifications = [];
	}

	public static void OnLoadLevel_ClearEntitySelection(Level level, Player.IntroTypes introTypes, bool isFromLoader) {
		Selection.Clear();
	}

	internal static void OnLoadingThread_AddCursorDisplayAndClearBuilds(Level level) {
        ResetTileModifications();
		level.Add(new BuildCursorDisplay());
	}

	internal static void On_LevelUpdate_Build(On.Celeste.Level.orig_Update orig, Level level) {
        orig(level);

        if (level.FrozenOrPaused || (level.Tracker.CountEntities<BuildController>() == 0 && !YaoiHelperModule.Settings.BuildAnywhere)) return;

		// TODO don't hardcode the key
		if (MInput.Keyboard.Pressed(Keys.LeftControl)) {
			modeSwitchDoubleTapTimer += 1;
			if (modeSwitchDoubleTapTimer > 1) {
				Mode = (Mode == BuildMode.Entities) ? BuildMode.Tiles : BuildMode.Entities;
				modeSwitchDoubleTapTimer = 0;
			}
		} else {
			modeSwitchDoubleTapTimer = (modeSwitchDoubleTapTimer > 0) ? modeSwitchDoubleTapTimer - Engine.DeltaTime : 0;
		}

		Mode = (!Unlimited && !AllowEntityMode) ? BuildMode.Tiles : Mode;

        MouseState state = MInput.Mouse.CurrentState;
        MousePos = level.ScreenToWorld(new Vector2(MInput.Mouse.X - Engine.Viewport.X, MInput.Mouse.Y - Engine.Viewport.Y));

        bool built = Mode switch {
			BuildMode.Tiles => tileBuild(state, level),
			BuildMode.Entities => entityBuild(state, level),
			_ => false,
		};
    }

	private static bool entityBuild(MouseState state, Level level) {
		// TODO this is like yanderedev tier
		DragSelecting = (DragSelecting || Selection.Count == 0) && state.LeftButton.HasFlag(ButtonState.Pressed);

		if (DragSelecting) {
			DragSelectBox ??= new Hitbox(0, 0, MousePos.X, MousePos.Y);
			if ((dragSelectionSwitchoverX ?? DragSelectBox.AbsoluteX) < MousePos.X) {
				dragSelectionSwitchoverX = null;
				DragSelectBox.Width = MousePos.X - DragSelectBox.AbsoluteX;
			} else {
				dragSelectionSwitchoverX ??= DragSelectBox.AbsoluteX;
				DragSelectBox.Width += DragSelectBox.AbsoluteX - MousePos.X;
				DragSelectBox.Position = new Vector2(MousePos.X, DragSelectBox.AbsoluteY);
			}

			if ((dragSelectionSwitchoverY ?? DragSelectBox.AbsoluteY) < MousePos.Y) {
				dragSelectionSwitchoverY = null;
				DragSelectBox.Height = MousePos.Y - DragSelectBox.AbsoluteY;
			} else {
				dragSelectionSwitchoverY ??= DragSelectBox.AbsoluteY;
				DragSelectBox.Height += DragSelectBox.AbsoluteY - MousePos.Y;
				DragSelectBox.Position = new Vector2(DragSelectBox.AbsoluteX, MousePos.Y);
			}
		} else if (DragSelectBox is not null) {
			Selection = level.Entities.Where(x => x.Collider is not null && x is not SolidTiles && x is not Player && x is not Trigger).Where(x => DragSelectBox.Collide(x.Collider)).ToList();
			DragSelectBox = null;
			dragSelectionSwitchoverX = dragSelectionSwitchoverY = null;
		}

		if (Selection.Any(x => new Hitbox(1, 1, MousePos.X, MousePos.Y).Collide(x.Collider))) {
			if (state.LeftButton.HasFlag(ButtonState.Pressed) && !Dragging) {
				Dragging = true;
				dragOrigin = MousePos;
			}
		} else if (MInput.Mouse.PressedLeftButton) {
			Selection = [];
		}

		Dragging = Dragging && state.LeftButton.HasFlag(ButtonState.Pressed);

		dragSnapThreshold = MInput.Keyboard.Check(Keys.LeftShift) ? 1 : 8;

		if (Dragging) {
			Vector2 offset = new Vector2((int)(MousePos.X - dragOrigin.X) - (int)(MousePos.X - dragOrigin.X) % dragSnapThreshold, (int)(MousePos.Y - dragOrigin.Y) - (int)(MousePos.Y - dragOrigin.Y) % dragSnapThreshold);
			foreach (Entity entity in Selection) {
				entity.Position += offset;
			}

			dragOrigin += offset;
		}

		if (MInput.Keyboard.Pressed(Keys.Back)) {
			Selection.ForEach(x => level.Remove(x));
			Selection.Clear();
		}

		// if (MInput.Keyboard.Check(Keys.LeftControl)) {
		// 	if (MInput.Keyboard.Pressed(Keys.C) || MInput.Keyboard.Pressed(Keys.X)) {
		// 		Clipboard = Selection;
		// 		CopyLocation = MousePos;
		// 		if (MInput.Keyboard.Pressed(Keys.X)) {
		// 			Selection.ForEach(x => level.Remove(x));
		// 			Selection.Clear();
		// 		}
		// 	}
		//
		// 	if (MInput.Keyboard.Pressed(Keys.V) && Clipboard is not null) {
		// 		foreach (Entity entity in Clipboard) {
		// 			// this is so fucking broken
		// 			level.Add((Entity)Activator.CreateInstance(entity.GetType(), new object[2] { entity.SourceData, entity.Position + (MousePos - CopyLocation)}) ?? throw new Exception("mrrow"));
		// 		}
		// 	}
		// }

		return true;
	}

    private static bool tileBuild(MouseState state, Level level) {
        if (!tileModifications.ContainsKey(level.Session.Level)) {
            tileModifications[level.Session.Level] = [];
        }

        Point tile = new Point((int)(MousePos.X - level.LevelOffset.X) / 8, (int)(MousePos.Y - level.LevelOffset.Y) / 8) + level.LevelSolidOffset;

        TileBuilding = state.LeftButton.HasFlag(ButtonState.Pressed);
        TileMining = state.RightButton.HasFlag(ButtonState.Pressed);

        if (level.Tracker.CountEntities<BuildRegion>() == 0 || YaoiHelperModule.Settings.BuildAnywhere) {
            IsValidPosition = true;
        }
        else {
            IsValidPosition = false;
            foreach (BuildRegion buildRegion in level.Tracker.GetEntities<BuildRegion>().Cast<BuildRegion>()) {
                IsValidPosition = IsValidPosition || ((Hitbox)buildRegion.Collider).Collide(MousePos);
            }

            if (IsValidPosition && level.Tracker.GetEntity<Player>() is Player player) {
                foreach (BuildRegion buildRegion in level.Tracker.GetEntities<BuildRegion>().Cast<BuildRegion>().Where(x => x.PreventBuildingWhenInside)) {
                    IsValidPosition = IsValidPosition && !((Hitbox)player.Collider).Collide(buildRegion.Collider);
                }
            }
        }

        if (!(TileBuilding || TileMining) || !IsValidPosition) return false;

        if (TileBuilding) {
            if (level.SolidsData[tile.X, tile.Y] == '0' && ((TilesLeft(level.Session.Level) > 0) || Unlimited)) {
                if (tileModifications[level.Session.Level].TryGetValue(tile, out TileModification modification) && modification.Type == TileModificationType.Mined) {
                    tileModifications[level.Session.Level].Remove(tile);
                }
                else {
                    tileModifications[level.Session.Level].Add(tile, new TileModification {
                        Type = TileModificationType.Built,
                        OrigTile = '0'
                    });
                }

                level.SolidTiles.Grid[tile.X, tile.Y] = true;
                level.SolidsData[tile.X, tile.Y] = selectedTile;
                updateTilesAround(level, tile, 2);
            }
        }
        else { // mining
            if (level.SolidsData[tile.X, tile.Y] != '0') {
                if (tileModifications[level.Session.Level].TryGetValue(tile, out TileModification modification) && modification.Type == TileModificationType.Built) {
                    tileModifications[level.Session.Level].Remove(tile);
                }
                else {
                    tileModifications[level.Session.Level].Add(tile, new TileModification {
                        Type = TileModificationType.Mined,
                        OrigTile = level.SolidsData[tile.X, tile.Y]
                    });
                }

                level.SolidTiles.Grid[tile.X, tile.Y] = false;
                level.SolidsData[tile.X, tile.Y] = '0';
                updateTilesAround(level, tile, 2);
            }
        }

        return true;
    }

    private static void updateTilesAround(Level level, Point tile, int radius) {
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
}

internal struct TileModification {
	public TileModificationType Type;
	public char OrigTile;
}

internal enum TileModificationType {
	Built,
	Mined
}

public sealed class BuildCursorDisplay : Entity {
	public BuildCursorDisplay() {
		Tag = Tags.HUD | Tags.Global;
		Depth = -0xabcdef;
	}

	public static void RenderColliderHiRes(Collider collider, Level level, Color color) {
		switch (collider) {
			case Hitbox: {
				for (int i = 0; i < 3; i++) {
					Draw.HollowRect(level.WorldToScreen(collider.AbsolutePosition) + new Vector2(i, i), collider.Width*6 - 2*i, collider.Height*6 - 2*i, color);
				}
				break;
			}

			case Circle: {
				Draw.Circle(level.WorldToScreen(collider.AbsolutePosition), ((Circle)collider).Radius * 6, color, 3, 10);
				break;
			}

			default: break;
		}
	}

	public override void Render() {
		base.Render();
		if (Scene is not Level level || level.FrozenOrPaused || (level.Tracker.CountEntities<BuildController>() == 0 && !YaoiHelperModule.Settings.BuildAnywhere) || !BuildHandler.BuildRoom(level.Session.Level)) return;

		switch (BuildHandler.Mode) {
			case BuildMode.Tiles: {
				Vector2 cursorPos = new Vector2(BuildHandler.MousePos.X - ((BuildHandler.MousePos.X - level.LevelOffset.X) % 8), BuildHandler.MousePos.Y  - ((BuildHandler.MousePos.Y - level.LevelOffset.Y) % 8));
				Color cursorColor = BuildHandler.IsValidPosition switch {
					false => Color.Red,
					true when BuildHandler.TileBuilding || BuildHandler.TileMining => Color.Yellow,
					_ => Color.LightGreen,
				};

				for (int i = 0; i < 6; i++) {
					Draw.HollowRect(level.WorldToScreen(cursorPos) + new Vector2(i, i), 8*6 - 2*i, 8*6 - 2*i, cursorColor);
				}

				if (!BuildHandler.Unlimited) {
					ActiveFont.Draw($"{BuildHandler.TilesLeft(level.Session.Level)}/{BuildHandler.TileLimit}", level.WorldToScreen(cursorPos + new Vector2(8, 8)), Vector2.Zero, Vector2.One / 2, cursorColor);
				}
				break;
			}

			case BuildMode.Entities: {
                Color color = Color.Red;
				
				if (!BuildHandler.DragSelecting) {
					Draw.Circle(level.WorldToScreen(BuildHandler.MousePos), 10, color, 5, 10);
				} else {
					Draw.Rect(level.WorldToScreen(BuildHandler.DragSelectBox!.AbsolutePosition), BuildHandler.DragSelectBox!.Width * 6, BuildHandler.DragSelectBox.Height * 6, Color.White * 0.3f);
				}

				foreach (Entity entity in BuildHandler.Selection) {
					switch (entity.Collider) {
						case Hitbox: case Circle: {
							RenderColliderHiRes(entity.Collider, level, color);
							break;
						}
						
						case ColliderList: {
							foreach (Collider collider in ((ColliderList)entity.Collider).colliders) {
								RenderColliderHiRes(collider, level, color);
							}
							break;
						}
						

						default: break;
					}
				}
				break;
			}
			
			default: {
				break;
			}
		}

	}

}
