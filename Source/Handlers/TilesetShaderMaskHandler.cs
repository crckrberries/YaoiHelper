using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;

namespace Celeste.Mod.YaoiHelper.Handlers;

// stylemaskhelper is extensively referenced here
[Submodule]
public static class TilesetShaderMaskHandler {
	public static Dictionary<string, string> TilesetMaskGroups = [];

	internal static void ApplyHooks() {
		On.Celeste.LevelLoader.ctor += On_LevelLoaderCtor_ClearTilesetMaskGroups;
		On.Celeste.Autotiler.ReadInto += On_AutotilerReadInto_GenerateTilesetMaskGroupsList;
	}

	internal static void RemoveHooks() {
		On.Celeste.LevelLoader.ctor -= On_LevelLoaderCtor_ClearTilesetMaskGroups;
		On.Celeste.Autotiler.ReadInto -= On_AutotilerReadInto_GenerateTilesetMaskGroupsList;
	}

	internal static void On_LevelLoaderCtor_ClearTilesetMaskGroups(On.Celeste.LevelLoader.orig_ctor orig, LevelLoader self, Session session, Vector2? startPosition) {
		TilesetMaskGroups.Clear();
		orig(self, session, startPosition);
    }

    internal static void On_AutotilerReadInto_GenerateTilesetMaskGroupsList (On.Celeste.Autotiler.orig_ReadInto orig, Autotiler self, object data, Tileset tileset, XmlElement xml) {
        orig(self, data, tileset, xml);

        if (xml.HasAttr("yaoiHelper_shaderMaskGroup")) {
            TilesetMaskGroups["tilesets/" + xml.Attr("path")] = xml.Attr("yaoiHelper_shaderMaskGroup");
		}
    }

	public static HashSet<string> VisibleTilesetMaskGroups(Level level) {
		HashSet<string> visible = [];
		if (TilesetMaskGroups.Count == 0) return visible;

		foreach (TileGrid tileGrid in level.Tracker.GetComponentsTrackIfNeeded<TileGrid>().Cast<TileGrid>()) {
			if (!tileGrid.Visible || !tileGrid.Entity.Visible || tileGrid.Alpha <= 0f) continue;

			tileGrid.ClipCamera ??= level.Camera;
            Rectangle clippedTiles = tileGrid.GetClippedRenderTiles();

			for (int i = clippedTiles.Left; i < clippedTiles.Right; i++) {
				for (int j = clippedTiles.Top; j < clippedTiles.Bottom; j++) {
					MTexture tileTexture = tileGrid.Tiles[i, j];
					if (tileTexture is not null && TilesetMaskGroups.TryGetValue(tileTexture.Parent.AtlasPath, out string? maskGroup)) {
						visible.Add(maskGroup);
					}
				}
			}

		}

		return visible;
	}

}

