using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.YaoiHelper.Entities;
using Celeste.Mod.YaoiHelper.Interfaces;
using Celeste.Mod.YaoiHelper.Triggers;
using Celeste.Mod.YaoiHelper.Types;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.Cil;

namespace Celeste.Mod.YaoiHelper.Handlers;

// TODO: maybe make this a renderer?
[Submodule]
public static class HDShaderHandler {
	private static readonly List<VirtualRenderTarget> flipflop_targets = new(2) { 
		VirtualContent.CreateRenderTarget("hd-shader-flip", 1920, 1080),
		VirtualContent.CreateRenderTarget("hd-shader-flop", 1920, 1080),
	};
			
	internal static void ApplyHooks() {
		IL.Celeste.Level.Render += IL_LevelRender_ApplyShader;
	}

	internal static void RemoveHooks() {
		IL.Celeste.Level.Render -= IL_LevelRender_ApplyShader;
	}

	internal static void IL_LevelRender_ApplyShader(ILContext il) {
		ILCursor cursor = new ILCursor(il);

		cursor.GotoNext(MoveType.Before,
			cursor => cursor.MatchLdnull(), 
			cursor => cursor.MatchCallvirt<GraphicsDevice>("SetRenderTarget")
		);
		cursor.Index -= 2;

		cursor.MoveAfterLabels();
		cursor.EmitLdarg0();
		cursor.EmitDelegate(renderPlayerToTempA);

		cursor.GotoNext(MoveType.Before, cursor => cursor.MatchLdloc2());
		cursor.GotoPrev(MoveType.Before, cursor => cursor.MatchCall(typeof(Draw), "get_SpriteBatch"));

		ILLabel dodgeRegularRender = cursor.DefineLabel();
		cursor.EmitBr(dodgeRegularRender);

		cursor.GotoNext(MoveType.After, cursor => cursor.MatchCallvirt<SpriteBatch>("End"));
		cursor.MarkLabel(dodgeRegularRender);

		cursor.MoveAfterLabels();
		cursor.EmitLdarg0();
		cursor.EmitDelegate(renderWithShaders);
	}

	private static void renderPlayerToTempA(Level level) {
		if (level.Tracker.CountEntities<HDShaderController>() == 0) return;
		if (!level.Tracker.GetEntity<HDShaderController>().RenderPlayerOver) return;

		Engine.Graphics.GraphicsDevice.SetRenderTarget(GameplayBuffers.TempA);
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

		if (Engine.Commands.Open) {
			level.Entities.DebugRender(level.Camera);
		}

		// level.ParticlesFG.Render();
		level.Particles.Render();
		// level.ParticlesBG.Render();

		Draw.SpriteBatch.End();
	}

	private static void loadTextures(Shader shader, HDShaderController controller) {
		for (int i = 0; i < shader.Textures.Length; i++) {
			if (string.IsNullOrEmpty(shader.Textures[i])) continue;

			int slot = int.Parse(shader.Textures[i].Split(':')[0].TrimEnd());
			string value = shader.Textures[i].Split(':')[1].TrimStart();

			Engine.Graphics.GraphicsDevice.Textures[slot] = value.ToCharArray()[0] switch {
				'%' => controller.GetMaskGroupTarget(value[1..]) ?? throw new KeyNotFoundException("mask group specified in HD shader not found"),
				'/' => GFX.Game.GetOrDefault(value[1..], null)?.Texture.Texture_Safe ?? throw new KeyNotFoundException("texture specified in HD shader not found"),
				'$' => (VirtualRenderTarget?)typeof(GameplayBuffers).GetField(value[1..])?.GetValue(null) ?? throw new KeyNotFoundException("GameplayBuffer specified in HD shader not found"),
				_ => /* null ?? */ throw new KeyNotFoundException("invalid prefix - valid ones are '%' for mask groups, $ for GameplayBuffers and '/' for texture files"),
			};
		}
	}

	private static Effect passShaderParams(Shader shader, Level level, RenderTarget2D target, HDShaderController controller) {
		Effect eff = shader.Effect;
		eff.Parameters["Time"]?.SetValue(level.TimeActive);
		eff.Parameters["CamPos"]?.SetValue(level.Camera.Position);
		eff.Parameters["PlayerPos"]?.SetValue(level.Tracker.CountEntities<Player>() == 1 ? level.Tracker.GetEntity<Player>().Position : new Vector2(-1, -1));
		eff.Parameters["Dimensions"]?.SetValue(target == null ? new Vector2(Engine.Viewport.Width, Engine.Viewport.Height) : new Vector2(target.Width, target.Height));

		// Go my jank
		eff.Parameters["ViewMatrix"]?.SetValue(target == null ? Matrix.CreateOrthographicOffCenter(0, 1920, 1080, 0, 0, 1) : Matrix.CreateOrthographicOffCenter(0, target.Width, target.Height, 0, 0, 1));
		eff.Parameters["TransformMatrix"]?.SetValue(Matrix.Identity);

		loadTextures(shader, controller);


		return eff;
	}

	private static void renderWithShaders(Level level) {
		RenderTarget2D origTarget = (RenderTarget2D)Engine.Graphics.GraphicsDevice.GetRenderTargets().ElementAtOrDefault(0).RenderTarget;
		HDShaderController controller = level.Tracker.GetEntity<HDShaderController>();
		// TODO this is really really jank
		List<Shader> shaders = level.Tracker.GetEntities<HDShaderTrigger>().Cast<HDShaderTrigger>().Where(x => x.Activated(level) && x.SourceData.Level.Name == controller.SourceData.Level.Name).SelectMany(x => x.Shaders).ToList();
		bool applyShaders = shaders.Count > 0 && level.Tracker.CountEntities<HDShaderController>() > 0;
		
		Vector2 vector = new Vector2(320f, 180f);
		Vector2 vector2 = vector / level.ZoomTarget;
		Vector2 vector3 = level.ZoomTarget != 1f ? (level.ZoomFocusPoint - vector2 / 2f) / (vector - vector2) * vector : Vector2.Zero;
		float scale = level.Zoom * ((vector.X - level.ScreenPadding * 2f) / 320f);
		Vector2 vector4 = new Vector2(level.ScreenPadding, level.ScreenPadding * /* 9f/16f, which is */ 0.5625f);

		Engine.Graphics.GraphicsDevice.SetRenderTarget(applyShaders ? (RenderTarget2D)flipflop_targets[0] : origTarget);
		Engine.Graphics.GraphicsDevice.Clear(Color.Black);

		// for proper letterboxing
		if (!applyShaders && origTarget == null) {
			Engine.Graphics.GraphicsDevice.Viewport = Engine.Viewport;
		}

		Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, ColorGrade.Effect, Matrix.CreateScale(6f) * (applyShaders ? Matrix.Identity : Engine.ScreenMatrix));
		Draw.SpriteBatch.Draw((RenderTarget2D)GameplayBuffers.Level, vector3 + vector4, GameplayBuffers.Level.Bounds, Color.White, 0f, vector3, scale, SpriteEffects.None, 0f);
		Draw.SpriteBatch.End();

		if (!applyShaders) return;
		
		List<IShaderMask> shaderMasks = level.Tracker.GetEntities<ShaderMask>().Cast<IShaderMask>().ToList();
		List<string> maskGroups = shaderMasks.SelectMany(x => x.MaskGroups).ToList();

		foreach (string group in maskGroups) {
			Engine.Graphics.GraphicsDevice.SetRenderTarget(controller.GetMaskGroupTarget(group));
			Engine.Graphics.GraphicsDevice.Clear(Color.Black);

			Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, Matrix.CreateScale(6f));

			foreach (IShaderMask sm in shaderMasks.Where(x => x.MaskGroups.Contains(group))) {
				sm.RenderMask();
			}

			Draw.SpriteBatch.End();
		}

		RenderTarget2D source;
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

			Draw.SpriteBatch.Begin(
				SpriteSortMode.Deferred,
				BlendState.AlphaBlend,
				SamplerState.PointClamp,
				DepthStencilState.Default,
				RasterizerState.CullNone,
				target == origTarget ? null : passShaderParams(shaders[i], level, target ?? throw new InvalidOperationException("expected nonnull target if it's not orig"), controller),
				target == null ? Engine.ScreenMatrix : Matrix.Identity
			);
			Draw.SpriteBatch.Draw(source, Vector2.Zero, source.Bounds, Color.White, 0f, Vector2.Zero, 1f, target == origTarget && SaveData.Instance.Assists.MirrorMode ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
			Draw.SpriteBatch.End();
		}

		// render player over
		if (!(controller.RenderPlayerOver || controller.RenderLevelOver)) return;
		Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, ColorGrade.Effect, Matrix.CreateScale(6f) * Engine.ScreenMatrix);
		if (controller.RenderLevelOver) {
			Draw.SpriteBatch.Draw((RenderTarget2D)GameplayBuffers.Gameplay, vector3 + vector4, GameplayBuffers.Level.Bounds, Color.White, 0f, vector3, scale, SpriteEffects.None, 0f);
		} else {
			Draw.SpriteBatch.Draw((RenderTarget2D)GameplayBuffers.TempA, vector3 + vector4, GameplayBuffers.Level.Bounds, Color.White, 0f, vector3, scale, SpriteEffects.None, 0f);
		}
		Draw.SpriteBatch.End();
	}
}
