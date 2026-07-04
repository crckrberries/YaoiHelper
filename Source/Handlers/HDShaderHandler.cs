using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.YaoiHelper.Interfaces;
using Celeste.Mod.YaoiHelper.Triggers;
using Celeste.Mod.YaoiHelper.Types;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.Cil;

namespace Celeste.Mod.YaoiHelper.Handlers;

public enum TextureType : byte {
	MaskGroup,
	Path,
	SpecialBuffer,
	GameplayBuffer
}

[Submodule]
public static class HDShaderHandler {
	private static readonly VirtualRenderTarget[] flipflop_targets = { 
		VirtualContent.CreateRenderTarget("hd-shader-flip", 1920, 1080),
		VirtualContent.CreateRenderTarget("hd-shader-flop", 1920, 1080),
	};

	private static readonly Dictionary<string, Effect> utilShaders = new Dictionary<string, Effect>() {
		["texmodifiers"] = new Effect(Engine.Graphics.GraphicsDevice, Everest.Content.Get("Effects/YaoiHelper/util/texmodifiers.cso").Data),
	};

	private static readonly Dictionary<TextureType, Dictionary<string, Texture2D>> texturePool = new Dictionary<TextureType, Dictionary<string, Texture2D>>();

	private static readonly Dictionary<int, VirtualRenderTarget> concatTargets = new Dictionary<int, VirtualRenderTarget>(16);

	private static readonly VirtualRenderTarget tempLowRes = VirtualContent.CreateRenderTarget("hd-shader-temp-lowres", 320, 180);

	internal static void ApplyHooks() {
		Everest.Events.Level.OnLoadLevel += On_LoadLevel_GenerateTexturePool;
		IL.Celeste.Level.Render += IL_LevelRender_ApplyShader;
	}

	internal static void RemoveHooks() {
		Everest.Events.Level.OnLoadLevel -= On_LoadLevel_GenerateTexturePool;
		IL.Celeste.Level.Render -= IL_LevelRender_ApplyShader;
	}

	private static void clearTexturePool() {
		foreach (Texture2D texture in texturePool.Values.SelectMany(x => x.Values)) {
			texture.Dispose();
		}	

		texturePool.Clear();

		foreach (TextureType type in Enum.GetValues<TextureType>()) {
			texturePool.Add(type, new Dictionary<string, Texture2D>());
		}
	}

	internal static void On_LoadLevel_GenerateTexturePool(Level level, Player.IntroTypes introTypes, bool isFromLoader) {
		clearTexturePool();

		List<string> textures = level.Tracker.GetEntities<HDShaderTrigger>().Cast<HDShaderTrigger>().Where(x => !string.IsNullOrEmpty(string.Concat(x.Shaders.SelectMany(x => x.Textures))) && x.SourceData.Level.Name == level.Session.Level).SelectMany(x => x.Shaders).SelectMany(x => x.Textures).SelectMany(x => x.Split(':')[1].TrimStart().Split('+')).Select(x => x.Trim()).Distinct().ToList();
		foreach (string texIdentifierWithModifier in textures) {
			char? modifier = texIdentifierWithModifier[0] switch {
				'-' => '-',
				'*' => '*',
				'!' => '!',
				_ => null
			};

			string textureIdentifier = modifier is null ? texIdentifierWithModifier : texIdentifierWithModifier[1..];
            TextureType type = prefixToType(textureIdentifier[0]);
            texturePool[type].Add(textureIdentifier, VirtualContent.CreateRenderTarget($"hd-texture-pool-{textureIdentifier}", 1920, 1080));

			if (type == TextureType.Path) {
				Texture2D texture = GFX.Game.GetOrDefault(textureIdentifier[1..], null)?.Texture?.Texture_Safe ?? throw new ArgumentException($"texture at path {textureIdentifier[1..]} specified in HD shader not found");

				Engine.Graphics.GraphicsDevice.SetRenderTarget((RenderTarget2D)texturePool[type][textureIdentifier]);
				Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);


				Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, Matrix.CreateScale(1920 / texture.Width, 1080 / texture.Height, 1));
				Draw.SpriteBatch.Draw(texture, Vector2.Zero, texture.Bounds, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
				Draw.SpriteBatch.End();
			}
		}


	}

	internal static void IL_LevelRender_ApplyShader(ILContext il) {
		ILCursor cursor = new ILCursor(il);

		cursor.GotoNext(MoveType.Before,
			cursor => cursor.MatchLdnull(), 
			cursor => cursor.MatchCallvirt<GraphicsDevice>("SetRenderTarget")
		);
		cursor.Index -= 2;

		cursor.GotoNext(MoveType.After, cursor => cursor.MatchLdloc2());
		cursor.EmitLdarg0();
		ILLabel dodgeSpriteBatchBegin = cursor.DefineLabel();
		cursor.EmitBr(dodgeSpriteBatchBegin);

		cursor.GotoNext(MoveType.After, cursor => cursor.MatchCall(typeof(Draw), "get_SpriteBatch"));
		cursor.MarkLabel(dodgeSpriteBatchBegin);

		cursor.GotoNext(MoveType.Before, cursor => cursor.MatchCallvirt<SpriteBatch>("Draw"));
		ILLabel dodgeSpriteBatchDrawAndEnd = cursor.DefineLabel();
		cursor.EmitBr(dodgeSpriteBatchDrawAndEnd);

		cursor.GotoNext(MoveType.After, cursor => cursor.MatchCallvirt<SpriteBatch>("End"));
		cursor.MarkLabel(dodgeSpriteBatchDrawAndEnd);

		cursor.MoveAfterLabels();
		cursor.EmitDelegate(renderWithShaders);
	}

	private static TextureType prefixToType(char pfx) {
		return pfx switch {
			'%'  => TextureType.MaskGroup,
			'/'  => TextureType.Path,
			'$'  => TextureType.GameplayBuffer,
			'#'  => TextureType.SpecialBuffer,
			_ => throw new ArgumentException($"invalid prefix {pfx} - valid ones are '%' "),
		};
	}

	private static void loadTextures(Shader shader) {
		for (int i = 0; i < shader.Textures.Length; i++) {
			if (string.IsNullOrEmpty(shader.Textures[i])) continue;

			int slot = int.Parse(shader.Textures[i].Split(':')[0].TrimEnd());
			string values = shader.Textures[i].Split(':')[1].TrimStart();

			List<string> identifiers = values.Split('+').Select(x => x.Trim()).ToList();

			// TODO jank
			if (identifiers.Count == 1 && "%/$#".Contains(identifiers[0])) {
				Engine.Graphics.GraphicsDevice.Textures[slot] = texturePool[prefixToType(identifiers[0][0])][identifiers[0]];
				continue;
			}

			if (!concatTargets.TryGetValue(slot, out VirtualRenderTarget? concatTarget)) {
				concatTargets[slot] = concatTarget = VirtualContent.CreateRenderTarget($"hd-shader-rescale-{slot}", 1920, 1080);
			}

			Engine.Graphics.GraphicsDevice.SetRenderTarget(concatTarget);
			Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

			foreach (string value in identifiers) {
				char? modifier = value[0] switch {
					'-' => '-',
					'*' => '*',
					'!' => '!',
					_ => null
				};

				string texIdentifier = modifier is null ? value : value[1..];

				Texture2D texture = texturePool[prefixToType(texIdentifier[0])][texIdentifier];

				Effect? texShader = modifier is null ? null : utilShaders["texmodifiers"];

				texShader?.CurrentTechnique = modifier switch {
					'-' => texShader.Techniques[0],
					'*' => texShader.Techniques[1],
					'!' => texShader.Techniques[2],
					_ => texShader.CurrentTechnique,
				};

				Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, texShader, Matrix.Identity);
				Draw.SpriteBatch.Draw(texture, Vector2.Zero, texture.Bounds, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
				Draw.SpriteBatch.End();

			}

			Engine.Graphics.GraphicsDevice.Textures[slot] = concatTarget;
		}


	}

	private static Effect passShaderParams(Shader shader, Level level, RenderTarget2D target, RenderTarget2D origTarget) {
		Effect eff = shader.Effect;
		eff.Parameters["Time"]?.SetValue(level.TimeActive);
		eff.Parameters["CamPos"]?.SetValue(level.Camera.Position);
		eff.Parameters["PlayerPos"]?.SetValue(level.Tracker.CountEntities<Player>() == 1 ? level.Tracker.GetEntity<Player>().Position : new Vector2(-1, -1));
		eff.Parameters["Dimensions"]?.SetValue(new Vector2(target.Width, target.Height));

		// ausp shader compat
		eff.Parameters["time"]?.SetValue(level.TimeActive + 2);
		eff.Parameters["cpos"]?.SetValue(level.Camera.Position);
		eff.Parameters["pscale"]?.SetValue(new Vector2(1f / target.Width, 1f / target.Height));

		// from frosthelper
		eff.Parameters["TransformMatrix"]?.SetValue(Matrix.CreateOrthographicOffCenter(0, target.Width, target.Height, 0, 0, 1));
		eff.Parameters["ViewMatrix"]?.SetValue(Matrix.Identity);

		loadTextures(shader);

		Engine.Graphics.GraphicsDevice.SetRenderTarget(origTarget);

		return eff;
	}

	private static void renderWithShaders(SpriteBatch _spriteBatch, SpriteSortMode spriteSortMode, BlendState blendState, SamplerState samplerState, DepthStencilState depthStencilState, RasterizerState rasterizerState, Effect effect, Matrix matrix, Level level, RenderTarget2D initialDrawSource, Vector2 initialDrawPosition, Rectangle initialSourceRect, Color initialColor, float initialRotation, Vector2 initialOrigin, float initialScale, SpriteEffects initialSpriteEffects, float initialLayerDepth) {
		RenderTarget2D origTarget = (RenderTarget2D)Engine.Graphics.GraphicsDevice.GetRenderTargets().ElementAtOrDefault(0).RenderTarget;
		// TODO this is really really jank
		List<Shader> shaders = level.Tracker.GetEntities<HDShaderTrigger>().Cast<HDShaderTrigger>().Where(x => x.Activated(level) && x.SourceData.Level.Name == level.Session.Level).SelectMany(x => x.Shaders).ToList();
		bool applyShaders = shaders.Count > 0;
		
		Engine.Graphics.GraphicsDevice.SetRenderTarget(applyShaders ? (RenderTarget2D)flipflop_targets[0] : origTarget);
		Engine.Graphics.GraphicsDevice.Clear(applyShaders ? Color.Transparent : level.BackgroundColor);

		// for proper letterboxing
		if (!applyShaders && origTarget == null) {
			Engine.Graphics.GraphicsDevice.Viewport = Engine.Viewport;
		}

		Draw.SpriteBatch.Begin(spriteSortMode, blendState, samplerState, depthStencilState, rasterizerState, applyShaders ? null : effect, applyShaders ? Matrix.CreateScale(6f) : matrix);
		Draw.SpriteBatch.Draw(initialDrawSource, initialDrawPosition, initialDrawSource.Bounds, Color.White, initialRotation, initialOrigin, initialScale, SpriteEffects.None, initialLayerDepth);
		Draw.SpriteBatch.End();

		if (!applyShaders) return;
		
		shaders.Sort((a, b) => a.Priority.CompareTo(b.Priority));

		foreach (KeyValuePair<string, Texture2D> texPair in texturePool[TextureType.SpecialBuffer].Concat(texturePool[TextureType.GameplayBuffer])) {
			Engine.Graphics.GraphicsDevice.SetRenderTarget((RenderTarget2D)texPair.Value);
			Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

			Texture2D texture = prefixToType(texPair.Key[0]) switch {
				TextureType.SpecialBuffer => SpecialBuffers.Get(texPair.Key[1..])?? throw new ArgumentException($"special buffer {texPair.Key[1..]} specified in HD shader not found"),
				TextureType.GameplayBuffer => (VirtualRenderTarget?)typeof(GameplayBuffers).GetField(texPair.Key[1..])?.GetValue(null)?? throw new ArgumentException($"GameplayBuffer {texPair.Key[1..]} specified in HD shader not found"),
				_ => throw new Exception("cosmic bit flip"),
			};

			Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, Matrix.CreateScale(1920 / texture.Width, 1080 / texture.Height, 1));
			Draw.SpriteBatch.Draw(texture, Vector2.Zero, texture.Bounds, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
			Draw.SpriteBatch.End();
		}

		// TODO: keep a list of all IShaderMask types somewhere instead of doing expensive reflection every frame
		List<IShaderMask> shaderMasks = level.Tracker.Entities.Values.SelectMany(x => x).Where(x => typeof(IShaderMask).IsAssignableFrom(x.GetType())).Cast<IShaderMask>().ToList();
		List<string> maskGroups = texturePool[TextureType.MaskGroup].Keys.Select(x => x[1..]).ToList();

		Texture[] colorgradeTextures = new Texture[2] {
			Engine.Graphics.GraphicsDevice.Textures[1],
			Engine.Graphics.GraphicsDevice.Textures[2]
		};

		VirtualMap<MTexture> orig = level.SolidTiles.Tiles.Tiles.Clone();

		foreach (string group in maskGroups) {
			Engine.Graphics.GraphicsDevice.SetRenderTarget(tempLowRes);
			Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

			Draw.SpriteBatch.Begin(spriteSortMode, blendState, samplerState, depthStencilState, rasterizerState, null, level.Camera.Matrix);

			foreach (IShaderMask sm in shaderMasks.Where(x => x.LowRes && x.MaskGroups.Contains(group))) {
				sm.RenderMask();
			}

			if (TilesetShaderMaskHandler.TilesetMaskGroups.ContainsValue(group)) {
				List<string> maskTilesetPaths = TilesetShaderMaskHandler.TilesetMaskGroups.Where(x => x.Value == group).Select(x => x.Key).ToList();
				for (int i = 0; i < level.SolidTiles.Tiles.Tiles.Columns; i++) {
					for (int j = 0; j < level.SolidTiles.Tiles.Tiles.Rows; j++) {
						level.SolidTiles.Tiles.Tiles[i, j] = maskTilesetPaths.Contains(orig[i, j]?.Parent.AtlasPath!) ? orig[i, j] : null;
					}
				}

				level.SolidTiles.Tiles.Render();

			}

			Draw.SpriteBatch.End();

			Engine.Graphics.GraphicsDevice.SetRenderTarget((RenderTarget2D)texturePool[TextureType.MaskGroup][string.Concat("%", group)]);
			Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

			Draw.SpriteBatch.Begin(spriteSortMode, blendState, samplerState, depthStencilState, rasterizerState, null, Matrix.CreateScale(6f));

			Draw.SpriteBatch.Draw(tempLowRes, initialDrawPosition, tempLowRes.Bounds, Color.White, initialRotation, initialOrigin, initialScale, SpriteEffects.None, initialLayerDepth);

			foreach (IShaderMask sm in shaderMasks.Where(x => !x.LowRes && x.MaskGroups.Contains(group))) {
				sm.RenderMask();
			}

			Draw.SpriteBatch.End();
		}

		level.SolidTiles.Tiles.Tiles = orig;

		RenderTarget2D? source;
		RenderTarget2D? target;

		// TODO: this wastes a draw call
		for (int i = 0; i <= shaders.Count; i++) {
			source = flipflop_targets[i % 2];
			target = i switch {
				_ when i == shaders.Count => origTarget,
				_ => (RenderTarget2D)flipflop_targets[1 - (i % 2)],
			};

			Engine.Graphics.GraphicsDevice.SetRenderTarget(target);
			Engine.Graphics.GraphicsDevice.Clear(Color.Black);

			// again, for proper letterboxing
			if (target == null) {
				Engine.Graphics.GraphicsDevice.Viewport = Engine.Viewport;
			}

			if (target == origTarget) {
				Engine.Graphics.GraphicsDevice.Textures[1] = colorgradeTextures[0];
				Engine.Graphics.GraphicsDevice.Textures[2] = colorgradeTextures[1];
			}

			Draw.SpriteBatch.Begin(
				SpriteSortMode.Deferred,
				BlendState.AlphaBlend,
				SamplerState.PointClamp,
				DepthStencilState.Default,
				RasterizerState.CullNone,
				target == origTarget ? ColorGrade.Effect : passShaderParams(shaders[i], level, target ?? throw new InvalidOperationException("expected nonnull target if it's not orig"),  target),
				target == null ? Engine.ScreenMatrix : Matrix.Identity
			);
			Draw.SpriteBatch.Draw(source, Vector2.Zero, source.Bounds, Color.White, 0f, Vector2.Zero, 1f, target == origTarget && SaveData.Instance.Assists.MirrorMode ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
			Draw.SpriteBatch.End();

			if (i == shaders.Count - 1 && SpecialBuffers.Get("last_frame") is VirtualRenderTarget lastFrame) {
				Engine.Graphics.GraphicsDevice.SetRenderTarget(lastFrame);
				Engine.Graphics.GraphicsDevice.Clear(Color.Black);

				Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, ColorGrade.Effect, Matrix.Identity);
				Draw.SpriteBatch.Draw(target, Vector2.Zero, target?.Bounds, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
				Draw.SpriteBatch.End();
			}
		}
	}
}

// TODO i have like no clue where to put this
[Submodule]
public static class SpecialBuffers {
	internal static void ApplyHooks() {
		IL.Celeste.Level.Render += IL_LevelRender_RenderToSpecialBuffers;
		IL.Celeste.LightingRenderer.BeforeRender += IL_LightingRendererBeforeRender_RenderWithoutBlur;
		On.Celeste.Level.Begin += On_LevelBegin_InitSpecialBuffers;
		On.Celeste.Level.End += On_LevelEnd_UnloadSpecialBuffers;
	}

	internal static void RemoveHooks() {
		IL.Celeste.Level.Render -= IL_LevelRender_RenderToSpecialBuffers;
		IL.Celeste.LightingRenderer.BeforeRender -= IL_LightingRendererBeforeRender_RenderWithoutBlur;
		On.Celeste.Level.Begin -= On_LevelBegin_InitSpecialBuffers;
		On.Celeste.Level.End -= On_LevelEnd_UnloadSpecialBuffers;
	}

	public static void On_LevelBegin_InitSpecialBuffers(On.Celeste.Level.orig_Begin orig, Level self) {
		orig(self);
		Init();
	}

	public static void On_LevelEnd_UnloadSpecialBuffers(On.Celeste.Level.orig_End orig, Level self) {
		orig(self);
		Unload();
	}

	internal static void IL_LightingRendererBeforeRender_RenderWithoutBlur(ILContext il) {
		ILCursor cursor = new ILCursor(il);

		cursor.GotoNext(MoveType.Before, cursor => cursor.MatchCallOrCallvirt(typeof(GaussianBlur).GetMethod("Blur")!));
		cursor.EmitLdsfld(typeof(GameplayBuffers).GetField("Light")!);
		cursor.EmitDelegate(renderLightWithoutBlur);
	}

	private static void renderLightWithoutBlur(VirtualRenderTarget source) {
		Engine.Graphics.GraphicsDevice.SetRenderTarget(Get("light_no_blur"));
		Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
		Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, Matrix.Identity);
		Draw.SpriteBatch.Draw((Texture2D)source, Vector2.Zero, Color.White);
		Draw.SpriteBatch.End();
	}

	internal static void IL_LevelRender_RenderToSpecialBuffers(ILContext il) {
		ILCursor cursor = new ILCursor(il);

		cursor.GotoNext(MoveType.Before,
			cursor => cursor.MatchLdnull(), 
			cursor => cursor.MatchCallvirt<GraphicsDevice>("SetRenderTarget")
		);
		cursor.Index -= 2;

		// todo clean this up
		cursor.MoveAfterLabels();
		cursor.EmitLdarg0();
		cursor.EmitDelegate(renderToSpecialBuffers);
	}

	private static void renderToSpecialBuffers(Level level) {
		Engine.Graphics.GraphicsDevice.SetRenderTarget(Get("player"));
		Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
		Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, level.Camera.Matrix);
		if (level.Tracker.CountEntities<Player>() > 0) {
			foreach (Player player in level.Tracker.GetEntities<Player>().Cast<Player>()) {
				if (player.Visible) {
					player.Render();
				}
			}
		} else {
			foreach (PlayerDeadBody body in level.Entities.FindAll<PlayerDeadBody>()) {
				if (body.Visible) {
					body.Render();
				}
			}
		}
		Draw.SpriteBatch.End();

		// if (Engine.Commands.Open) {
		// 	level.Entities.DebugRender(level.Camera);
		// }

		Engine.Graphics.GraphicsDevice.SetRenderTarget(Get("particles"));
		Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
		Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, level.Camera.Matrix);
		level.ParticlesFG.Render();
		level.Particles.Render();
		level.ParticlesBG.Render();

		Draw.SpriteBatch.End();
		//
		// Engine.Graphics.GraphicsDevice.SetRenderTarget(Get("decals"));
		// Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
		// Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, level.Camera.Matrix);
		// foreach (Decal decal in level.Entities.OfType<Decal>()) {
		// 	decal.Render();
		// }
		//
		// Draw.SpriteBatch.End();
		//
		// Engine.Graphics.GraphicsDevice.SetRenderTarget(Get("entities"));
		// Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
		// Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, level.Camera.Matrix);
		// foreach (Entity entity in level.Entities) {
		// 	entity.Render();
		// }
		//
		// Draw.SpriteBatch.End();
		//
		// Engine.Graphics.GraphicsDevice.SetRenderTarget(Get("fgtiles"));
		// Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
		// Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, level.Camera.Matrix);
		//
		// level.SolidTiles.Render();
		//
		// Draw.SpriteBatch.End();
		//
		// Engine.Graphics.GraphicsDevice.SetRenderTarget(Get("bgtiles"));
		// Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
		// Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, level.Camera.Matrix);
		//
		// level.BgTiles.Render();
		//
		// Draw.SpriteBatch.End();
		//
		// Engine.Graphics.GraphicsDevice.SetRenderTarget(Get("fgstylegrounds"));
		// Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
		// level.Foreground.Render(level);
		//
		// Engine.Graphics.GraphicsDevice.SetRenderTarget(Get("bgstylegrounds"));
		// Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
		// level.Background.Render(level);

	}

	private static readonly Dictionary<string, VirtualRenderTarget?> targets = [];

	public static VirtualRenderTarget? Get(string name) {
		return targets.GetValueOrDefault(name);
	}

	public static void Create(string name, int width, int height) {
		targets.Add(name, VirtualContent.CreateRenderTarget($"hd-shader-special-target-{name}", width, height));
	}	

	public static void Init() {
		Create("empty", 320, 180);
		Create("player", 320, 180);
		Create("particles", 320, 180);
		// Create("decals", 320, 180);
		// Create("entities", 320, 180);
		// Create("fgstylegrounds", 320, 180);
		// Create("bgstylegrounds", 320, 180);
		// Create("fgtiles", 320, 180);
		// Create("bgtiles", 320, 180);
		Create("light_no_blur", 320, 180);
		Create("last_frame", 1920, 1080);
	}

	public static void Unload() {
		foreach (string target in targets.Keys) {
			targets[target]?.Dispose();
		}

		targets.Clear();
	}
}

