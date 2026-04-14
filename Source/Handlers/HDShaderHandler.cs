using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.YaoiHelper.Entities;
using Celeste.Mod.YaoiHelper.Triggers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.Cil;

namespace Celeste.Mod.YaoiHelper.Handlers;

public static class HDShaderHandler {
	private static readonly List<VirtualRenderTarget> flipflop_targets = new(2) { 
		VirtualContent.CreateRenderTarget("hd-shader-flip", 1920, 1080),
		VirtualContent.CreateRenderTarget("hd-shader-flop", 1920, 1080),
	};

	private static readonly List<VirtualRenderTarget> mask_groups = new();

	public static void AddMaskGroup(string name) {
		if (mask_groups.Select(x => x.Name).Contains($"hd-shader-mask-{name}")) return;
		mask_groups.Add(VirtualContent.CreateRenderTarget($"hd-shader-mask-{name}", 1920, 1080));
	}

	public static void RemoveMaskGroup(string name) {
		if (!mask_groups.Select(x => x.Name).Contains($"hd-shader-mask-{name}")) return;
		mask_groups.Remove(mask_groups.First(x => x.Name == $"hd-shader-mask-{name}"));
	}

	public static VirtualRenderTarget GetMaskGroupTarget(string name) {
		return mask_groups.FirstOrDefault(x => x.Name == $"hd-shader-mask-{name}", null) ?? throw new KeyNotFoundException("No matching mask group found");
	}

	public static void il_LevelRender_ApplyShader(ILContext il) {
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

		ILLabel dodge_regularrender = cursor.DefineLabel();
		cursor.EmitBr(dodge_regularrender);

		cursor.GotoNext(MoveType.After, cursor => cursor.MatchCallvirt<SpriteBatch>("End"));
		cursor.MarkLabel(dodge_regularrender);

		cursor.MoveAfterLabels();
		cursor.EmitLdarg0();
		cursor.EmitDelegate(renderWithEffects);
	}

	private static void renderPlayerToTempA(Level level) {
		if (level.Tracker.CountEntities<HDShaderController>() == 0) return;
		if (!level.Tracker.GetEntities<HDShaderController>().Cast<HDShaderController>().First().RenderPlayerOver) return;
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

		// level.Entities.FindAll<TrailManager>().ForEach(tm => tm.Render());

		// if (Engine.Commands.Open) {
		// 	level.Entities.DebugRender(level.Camera);
		// }

		// level.ParticlesFG.Render();
		level.Particles.Render();
		// level.ParticlesBG.Render();

		Draw.SpriteBatch.End();
	}

	private static Effect passShaderParams(Effect eff, Level level, RenderTarget2D target, List<string> maskGroups) {
		eff.Parameters["Time"].SetValue(level.TimeActive);
		eff.Parameters["CamPos"].SetValue(level.Camera.Position);
		eff.Parameters["Dimensions"].SetValue(new Vector2(1920, 1080));

		// Go my jank
		eff.Parameters["ViewMatrix"].SetValue(target == null ? Matrix.CreateOrthographicOffCenter(0, Engine.Viewport.Width, Engine.Viewport.Height, 0, 0, 1) : Matrix.CreateOrthographicOffCenter(0, target.Width, target.Height, 0, 0, 1));
		eff.Parameters["TransformMatrix"].SetValue(Matrix.Identity);

		// bind group 3 and on are shader masks
		for (int i = 0; i < maskGroups.Count; i++) {
			Engine.Graphics.GraphicsDevice.Textures[i + 3] = GetMaskGroupTarget(maskGroups[i]);
		}

		return eff;
	}

	private static void renderWithEffects(Level level) {
		List<HDShaderTrigger> shaderTriggers = level.Tracker.GetEntities<HDShaderTrigger>().Cast<HDShaderTrigger>().Where(x => x.Activated).ToList();
		List<Effect> effects = shaderTriggers.SelectMany(x => x.Effects).ToList();
		
		// i have no clue what these constants do 
		Vector2 vector = new Vector2(320f, 180f);
		Vector2 vector2 = vector / level.ZoomTarget;
		Vector2 vector3 = level.ZoomTarget != 1f ? (level.ZoomFocusPoint - vector2 / 2f) / (vector - vector2) * vector : Vector2.Zero;
		float scale = level.Zoom * ((vector.X -  level.ScreenPadding * 2f) / 320f);
		Vector2 vector4 = new Vector2(level.ScreenPadding, level.ScreenPadding * 0.5625f);

		// draw mask
		// TODO: add hires mask support
		// ==========================================================================
		// Engine.Graphics.GraphicsDevice.SetRenderTarget(masks[0]);
		// Engine.Graphics.GraphicsDevice.Clear(Color.Black);
		List<ShaderMask> shaderMasks = level.Tracker.GetEntities<ShaderMask>().Cast<ShaderMask>().ToList();
		List<string> groupsInScene = shaderMasks.SelectMany(x => x.MaskGroups).ToList();
		foreach (string group in groupsInScene) {
			Engine.Graphics.GraphicsDevice.SetRenderTarget(GetMaskGroupTarget(group));
			Engine.Graphics.GraphicsDevice.Clear(Color.Black);

			Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, ColorGrade.Effect, Matrix.CreateScale(6f) * Engine.ScreenMatrix);

			foreach (ShaderMask sm in shaderMasks.Where(x => x.MaskGroups.Contains(group))) {
				sm.RenderMask();
			}

			Draw.SpriteBatch.End();
		}

		// draw level
		// ==========================================================================
		Engine.Graphics.GraphicsDevice.SetRenderTarget(effects.Count > 0 ? (RenderTarget2D)flipflop_targets[0] : null);
		Engine.Graphics.GraphicsDevice.Clear(Color.Black);

		// for proper letterboxing
		if (effects.Count == 0) {
			Engine.Graphics.GraphicsDevice.Viewport = Engine.Viewport;
		}

		Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, ColorGrade.Effect, Matrix.CreateScale(6f) * Engine.ScreenMatrix);
		Draw.SpriteBatch.Draw((RenderTarget2D)GameplayBuffers.Level, vector3 + vector4, GameplayBuffers.Level.Bounds, Color.White, 0f, vector3, scale, SpriteEffects.None, 0f);
		Draw.SpriteBatch.End();

		RenderTarget2D source, target;
		int total_effects_applied = 0;
		if (effects.Count == 0) return;
		// TODO: this entire thing needs cleaning up but this especially
		foreach (HDShaderTrigger trigger in shaderTriggers) {
			for (int i = 0; i < trigger.Effects.Count; i++) {
				source = flipflop_targets[total_effects_applied % 2];
				target = total_effects_applied switch {
					_ when total_effects_applied == (effects.Count - 1) => null,
					_ => (RenderTarget2D)flipflop_targets[1 - (total_effects_applied % 2)],
				};

				Engine.Graphics.GraphicsDevice.SetRenderTarget(target);
				Engine.Graphics.GraphicsDevice.Clear(Color.Black);

				// again, for proper letterboxing
				if (target == null) {
					Engine.Graphics.GraphicsDevice.Viewport = Engine.Viewport;
				}

				Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, passShaderParams(trigger.Effects[i], level, target, trigger.MaskGroups), Engine.ScreenMatrix);
				Draw.SpriteBatch.Draw((RenderTarget2D)source, Vector2.Zero, source.Bounds, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
				Draw.SpriteBatch.End();

				total_effects_applied++;

			}
		}


		// render player over
		// TODO: should this just be a shader mask
		if (level.Tracker.CountEntities<HDShaderController>() == 0) return;
		if (!level.Tracker.GetEntities<HDShaderController>().Cast<HDShaderController>().First().RenderPlayerOver) return;
		Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, ColorGrade.Effect, Matrix.CreateScale(6f) * Engine.ScreenMatrix);
		Draw.SpriteBatch.Draw((RenderTarget2D)GameplayBuffers.TempA, vector3 + vector4, GameplayBuffers.Level.Bounds, Color.White, 0f, vector3, scale, SpriteEffects.None, 0f);
		Draw.SpriteBatch.End();
	}
			
	public static void ApplyHooks() {
		IL.Celeste.Level.Render += il_LevelRender_ApplyShader;
	}

	public static void RemoveHooks() {
		IL.Celeste.Level.Render -= il_LevelRender_ApplyShader;
	}

}
