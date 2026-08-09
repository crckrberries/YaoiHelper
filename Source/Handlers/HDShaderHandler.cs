using System;
using System.Linq;
using System.Reflection;
using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Crackerberries.YaoiHelper.Triggers;
using Crackerberries.YaoiHelper.Types;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System.Collections.Generic;

namespace Crackerberries.YaoiHelper.Handlers;

public enum TextureType : byte {
	MaskGroup,
	Path,
	SpecialBuffer,
	GameplayBuffer,
	Register
}

[Submodule]
public static class HDShaderHandler {
	private static RenderTarget2D?[]? fakeRTs = null;

	private static RenderTarget2D? origTarget = null;
	private static Matrix? origScreenMatrix = null;
	private static Viewport? origViewport = null;

	private static bool inLevelRender = false;

	// `VirtualContent.CreateRenderTarget` relies on state that doesn't exist at hook application time, so this is initialized later
	private static VirtualRenderTarget? captured_backbuffer = /* VirtualContent.CreateRenderTarget("yaoihelper-hd-shader-captured-backbuffer", 1920, 1080) */ null;

	// same as captured_backbuffer
	private static VirtualRenderTarget[]? flipflop_targets = /* { 
		VirtualContent.CreateRenderTarget("yaoihelper-hd-shader-flip", 1920, 1080),
		VirtualContent.CreateRenderTarget("yaoihelper-hd-shader-flop", 1920, 1080),
	}; */ null;

	// Everest.Content.Get relies on state that doesn't exist at hook application time, so this is initialized later
	private static Dictionary<string, Effect>? utilShaders = /* new Dictionary<string, Effect>() {
		["texmodifiers"] = new Effect(Engine.Graphics.GraphicsDevice, Everest.Content.Get("Effects/YaoiHelper/util/texmodifiers.cso").Data),
	}; */ null;

	private static readonly Dictionary<TextureType, Dictionary<string, Texture2D>> texturePool = new Dictionary<TextureType, Dictionary<string, Texture2D>>();

	private static readonly Dictionary<int, VirtualRenderTarget> concatTargets = new Dictionary<int, VirtualRenderTarget>(16);

	// same as captured_backbuffer
	private static VirtualRenderTarget? tempLowRes = /* VirtualContent.CreateRenderTarget("yaoihelper-hd-shader-temp-lowres", 320, 180) */ null;

	private static Hook? hook_SetRenderTarget = null;

	internal static void ApplyHooks() {
		hook_SetRenderTarget = new Hook(typeof(GraphicsDevice).GetMethod(nameof(GraphicsDevice.SetRenderTargets), BindingFlags.Public | BindingFlags.Instance, [typeof(RenderTargetBinding[])]) ?? throw new KeyNotFoundException("mp ,etjpd mda,dm"), on_GraphicsDeviceSetRenderTargets_CaptureBackbuffer);
		Everest.Events.Level.OnLoadLevel += on_LoadLevel_GenerateTexturePool;
		IL.Celeste.Level.Render += il_LevelRender_ApplyShader;
		On.Celeste.Level.Render += on_LevelRender_SetInLevelRender;
	}

    internal static void RemoveHooks() {
		hook_SetRenderTarget?.Dispose();
		hook_SetRenderTarget = null;
		Everest.Events.Level.OnLoadLevel -= on_LoadLevel_GenerateTexturePool;
		IL.Celeste.Level.Render -= il_LevelRender_ApplyShader;
		On.Celeste.Level.Render -= on_LevelRender_SetInLevelRender;
	}

	// private static void initializeFakeRTs() {
	// 	fakeRTs = [null];
	// 	if (YaoiHelperModule.CNetLoaded && Everest.Loader.TryGetDependency(YaoiHelperModule.CelesteNetClientMetadata, out EverestModule cnetModule)) {
	// 		MethodInfo? ofTypeOpen = typeof(GameComponentCollection).GetMethod("OfType");
	// 		Type? renderHelperComponent = cnetModule.GetType().Assembly.GetType("CelesteNetRenderHelperComponent") ?? throw new TypeLoadException("meowkema");
	// 		MethodInfo? ofType = ofTypeOpen?.MakeGenericMethod(renderHelperComponent);
	// 	}
	// }

	private static void on_LevelRender_SetInLevelRender(On.Celeste.Level.orig_Render orig, Level self) {
		inLevelRender = true;
		try {
			orig(self);
		} finally {
			inLevelRender = false;
		}
	}

	private static void on_GraphicsDeviceSetRenderTargets_CaptureBackbuffer(Action<GraphicsDevice, RenderTargetBinding[]?> orig, GraphicsDevice self, RenderTargetBinding[]? rts) {
		// if (inLevelRender && captured_backbuffer is not null && self.GetRenderTargets().ElementAtOrDefault(0)?.RenderTarget == (RenderTarget2D)captured_backbuffer) {
		// 	return;
		// }

		// Console.WriteLine(rts?.ElementAtOrDefault(0).RenderTarget?.Name);
		if (inLevelRender && (rts is null || rts?.ElementAtOrDefault(0) is null/* || rts?.Any(x => x.RenderTarget?.Name?.Contains("dbb") == true) == true */)) {
			origTarget = (RenderTarget2D?)(rts?.ElementAtOrDefault(0).RenderTarget);

			captured_backbuffer ??= VirtualContent.CreateRenderTarget("yaoihelper-hd-shader-captured-backbuffer", 1920, 1080);
			orig(self, [(RenderTarget2D)captured_backbuffer]);
		} else {
			orig(self, rts);
		}
	}

    private static void il_LevelRender_ApplyShader(ILContext il) {
		ILCursor cursor = new ILCursor(il);

		// VariableDefinition origTarget = new VariableDefinition(il.Method.Module.ImportReference(typeof(RenderTarget2D)));
		// VariableDefinition origScreenMatrix = new VariableDefinition(il.Method.Module.ImportReference(typeof(Matrix)));
		// il.Body.Variables.Add(origTarget);
		// il.Body.Variables.Add(origScreenMatrix);

		cursor.GotoNext(MoveType.After,
			static i => i.MatchLdnull(), 
			static i => i.MatchCallvirt<GraphicsDevice>("SetRenderTarget")
		);

		// cursor.EmitDelegate(setOrigTarget);
		// cursor.EmitDelegate(swapToCaptureBuffer);
		cursor.EmitDelegate(replaceEngineScreenMatrix);
		// cursor.EmitStloc(origScreenMatrix);

		// cursor.GotoNext(MoveType.Before,
		// 	static i => i.MatchLdcR4(0.0f),
		// 	static i => i.MatchCallvirt<SpriteBatch>("Draw")
		// );
		
		cursor.GotoNext(MoveType.Before,
			static i => i.MatchLdarg0(),
			static i => i.MatchLdfld<Level>("SubHudRenderer")
		);

		cursor.MoveAfterLabels();
		// cursor.EmitLdloc(origScreenMatrix);
		cursor.EmitDelegate(restoreEngineScreenMatrix);
		cursor.EmitLdarg0();
		cursor.EmitDelegate(renderCaptured);
    }

	private static void swapToCaptureBuffer() {
		captured_backbuffer ??= VirtualContent.CreateRenderTarget("yaoihelper-hd-shader-captured-backbuffer", 1920, 1080);
		Engine.Graphics.GraphicsDevice.SetRenderTarget(captured_backbuffer);
	}
	
	private static /* RenderTarget2D? */ void setOrigTarget() {
		// return (RenderTarget2D)Engine.Graphics.GraphicsDevice.GetRenderTargets().ElementAtOrDefault(0).RenderTarget;
		origTarget = (RenderTarget2D)Engine.Graphics.GraphicsDevice.GetRenderTargets().ElementAtOrDefault(0).RenderTarget;
	}

	private static void replaceEngineScreenMatrix() {
		origScreenMatrix = Engine.ScreenMatrix;
		origViewport = Engine.Viewport;
		Engine.ScreenMatrix = Matrix.Identity;
		Engine.Viewport = new Viewport(0, 0, 1920, 1080);
	}

	private static void restoreEngineScreenMatrix() {
		Engine.ScreenMatrix = origScreenMatrix ?? throw new NullReferenceException("origScreenMatrix is null at this point, somehow");
		Engine.Viewport = origViewport ?? throw new NullReferenceException("origViewport is null at this point, somehow");
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

	private static void on_LoadLevel_GenerateTexturePool(Level level, Player.IntroTypes introTypes, bool isFromLoader) {
		clearTexturePool();

        IEnumerable<HDShaderTrigger> triggers = level.Tracker.GetEntities<HDShaderTrigger>().Cast<HDShaderTrigger>().Where(x => x.SourceData.Level.Name == level.Session.Level);
        List<string> textures = triggers
			.Where(x => !string.IsNullOrEmpty(string.Concat(x.Shaders.SelectMany(x => x.Textures))))
			.SelectMany(x => x.Shaders).SelectMany(x => x.Textures)
			.SelectMany(x => x.Split(':')[1].TrimStart().Split('+'))
			.Select(x => x.Trim())
			.Select(x => "!*-".Contains(x[0]) ? x[1..] : x)
			.Concat(triggers.SelectMany(x => x.Shaders).Where(x => !string.IsNullOrEmpty(x.Target)).Select(x => string.Concat('@', x.Target)))
			.Distinct().ToList();

		foreach (string textureIdentifier in textures) {
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

	private static TextureType prefixToType(char pfx) {
		return pfx switch {
			'%'  => TextureType.MaskGroup,
			'/'  => TextureType.Path,
			'$'  => TextureType.GameplayBuffer,
			'#'  => TextureType.SpecialBuffer,
			'@'  => TextureType.Register,
			_ => throw new ArgumentException($"invalid prefix {pfx} - valid ones are '%' for mask groups, $ for GameplayBuffers, # for special buffers, / for paths and @ for registers"),
		};
	}

	private static void loadTextures(Shader shader) {
		for (int i = 0; i < shader.Textures.Length; i++) {
			if (string.IsNullOrEmpty(shader.Textures[i])) continue;

			int slot = int.Parse(shader.Textures[i].Split(':')[0].TrimEnd());
			string values = shader.Textures[i].Split(':')[1].TrimStart();

			List<string> identifiers = values.Split('+').Select(x => x.Trim()).ToList();

			// TODO jank
			if (identifiers.Count == 1 && "%/$#@".Contains(identifiers[0])) {
				Engine.Graphics.GraphicsDevice.Textures[slot] = texturePool[prefixToType(identifiers[0][0])][identifiers[0]];
				continue;
			}

			if (!concatTargets.TryGetValue(slot, out VirtualRenderTarget? concatTarget)) {
				concatTargets[slot] = concatTarget = VirtualContent.CreateRenderTarget($"yaoihelper-hd-shader-rescale-{slot}", 1920, 1080);
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

				utilShaders ??= new Dictionary<string, Effect>() {
					["texmodifiers"] = new Effect(Engine.Graphics.GraphicsDevice, Everest.Content.Get("Effects/YaoiHelper/util/texmodifiers.cso").Data),
				};
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
	
	public static void renderCaptured(Level level) {
		inLevelRender = false;

		List<Shader> shaders = level.Tracker.GetEntities<HDShaderTrigger>().Cast<HDShaderTrigger>().Where(x => x.Activated(level) && x.SourceData.Level.Name == level.Session.Level).SelectMany(x => x.Shaders).ToList();
		bool applyShaders = shaders.Count > 0;

		if (!applyShaders) {
			Engine.Graphics.GraphicsDevice.SetRenderTarget(origTarget);
			Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

			Engine.Graphics.GraphicsDevice.Viewport = Engine.Viewport;
			Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, Engine.ScreenMatrix);
			Draw.SpriteBatch.Draw(captured_backbuffer, Vector2.Zero, captured_backbuffer?.Bounds, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
			Draw.SpriteBatch.End();
			return;
		}

		shaders.Sort((a, b) => a.Priority.CompareTo(b.Priority));

		RenderTarget2D? source;
		RenderTarget2D? target;
		flipflop_targets ??= [ 
			VirtualContent.CreateRenderTarget("yaoihelper-hd-shader-flip", 1920, 1080),
			VirtualContent.CreateRenderTarget("yaoihelper-hd-shader-flop", 1920, 1080),
		];

		for (int i = 0, flopulation = 0; i <= shaders.Count; i++) {
			source = flopulation == 0 ? captured_backbuffer : flipflop_targets[flopulation % 2];
			target = 0 switch {
				_ when shaders.ElementAtOrDefault(i)?.Target is not null => (RenderTarget2D)texturePool[TextureType.Register][string.Concat('@', shaders[i].Target)],
				_ when flopulation == shaders.Count(x => x.Target is null) => origTarget,
				_ => (RenderTarget2D)flipflop_targets[1 - (flopulation % 2)],
			};

			Engine.Graphics.GraphicsDevice.SetRenderTarget(target);
			Engine.Graphics.GraphicsDevice.Clear(Color.Black);

			// again, for proper letterboxing
			if (target == origTarget) {
				Engine.Graphics.GraphicsDevice.Viewport = Engine.Viewport;
			}

			Draw.SpriteBatch.Begin(
				SpriteSortMode.Deferred,
				BlendState.AlphaBlend,
				SamplerState.PointClamp,
				DepthStencilState.Default,
				RasterizerState.CullNone,
				target == origTarget ? null : passShaderParams(shaders[i], level, target ?? throw new InvalidOperationException("expected nonnull target if it's not orig"),  target),
				target == origTarget ? Engine.ScreenMatrix : Matrix.Identity
			);

			Draw.SpriteBatch.Draw(source, Vector2.Zero, source.Bounds, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
			Draw.SpriteBatch.End();

			if (shaders.ElementAtOrDefault(i)?.Target is null) {
				flopulation++;
			}

			// if (flopulation == shaders.Count(x => x.Target is null) - 1 && SpecialBuffers.Get("last_frame") is VirtualRenderTarget lastFrame) {
			// 	Engine.Graphics.GraphicsDevice.SetRenderTarget(lastFrame);
			// 	Engine.Graphics.GraphicsDevice.Clear(Color.Black);
			//
			// 	Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, ColorGrade.Effect, Matrix.Identity);
			// 	Draw.SpriteBatch.Draw(target, Vector2.Zero, target?.Bounds, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
			// 	Draw.SpriteBatch.End();
			// }
		}
	}
}

// TODO i have like no clue where to put this
[Submodule]
public static class SpecialBuffers {
	// XXX: this shouldn't run unless the special buffers are in use
	internal static void ApplyHooks() {
		IL.Celeste.Level.Render += il_LevelRender_RenderToSpecialBuffers;
		IL.Celeste.LightingRenderer.BeforeRender += il_LightingRendererBeforeRender_RenderWithoutBlur;
		On.Celeste.Level.Begin += on_LevelBegin_InitSpecialBuffers;
		On.Celeste.Level.End += on_LevelEnd_UnloadSpecialBuffers;
	}

	internal static void RemoveHooks() {
		IL.Celeste.Level.Render -= il_LevelRender_RenderToSpecialBuffers;
		IL.Celeste.LightingRenderer.BeforeRender -= il_LightingRendererBeforeRender_RenderWithoutBlur;
		On.Celeste.Level.Begin -= on_LevelBegin_InitSpecialBuffers;
		On.Celeste.Level.End -= on_LevelEnd_UnloadSpecialBuffers;
	}

	private static void on_LevelBegin_InitSpecialBuffers(On.Celeste.Level.orig_Begin orig, Level self) {
		orig(self);
		Init();
	}

	private static void on_LevelEnd_UnloadSpecialBuffers(On.Celeste.Level.orig_End orig, Level self) {
		orig(self);
		Unload();
	}

	private static void il_LightingRendererBeforeRender_RenderWithoutBlur(ILContext il) {
		ILCursor cursor = new ILCursor(il);

		cursor.GotoNext(MoveType.Before, i => i.MatchCallOrCallvirt(typeof(GaussianBlur).GetMethod("Blur")!));
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

	private static void il_LevelRender_RenderToSpecialBuffers(ILContext il) {
		ILCursor cursor = new ILCursor(il);

		cursor.GotoNext(MoveType.Before,
			i => i.MatchLdnull(), 
			i => i.MatchCallvirt<GraphicsDevice>("SetRenderTarget")
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
		Engine.Graphics.GraphicsDevice.SetRenderTarget(Get("fgtiles"));
		Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
		Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, level.Camera.Matrix);

		level.SolidTiles.Render();

		Draw.SpriteBatch.End();

		Engine.Graphics.GraphicsDevice.SetRenderTarget(Get("bgtiles"));
		Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
		Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, level.Camera.Matrix);

		level.BgTiles.Render();

		Draw.SpriteBatch.End();
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
		targets.Add(name, VirtualContent.CreateRenderTarget($"yaoihelper-hd-shader-special-target-{name}", width, height));
	}	

	public static void Init() {
		Create("empty", 320, 180);
		Create("player", 320, 180);
		Create("particles", 320, 180);
		// Create("decals", 320, 180);
		// Create("entities", 320, 180);
		// Create("fgstylegrounds", 320, 180);
		// Create("bgstylegrounds", 320, 180);
		Create("fgtiles", 320, 180);
		Create("bgtiles", 320, 180);
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

