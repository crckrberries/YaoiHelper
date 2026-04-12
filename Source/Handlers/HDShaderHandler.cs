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
	private static readonly List<VirtualRenderTarget> targets = new(2) { 
		VirtualContent.CreateRenderTarget("hd-shader-1", 1920, 1080),
		VirtualContent.CreateRenderTarget("hd-shader-2", 1920, 1080),
	};

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

		// TODO: work out how the rendering should be ordered
		// also i don't think this dodge works for some reason
		ILLabel dodge_regularrender = cursor.DefineLabel();
		cursor.EmitBr(dodge_regularrender);

		cursor.GotoNext(MoveType.After, cursor => cursor.MatchCallvirt<SpriteBatch>("End"));
		cursor.MarkLabel(dodge_regularrender);

		cursor.EmitLdarg0();
		cursor.EmitDelegate(renderWithEffects);

		Logger.Log(LogLevel.Info, "asdasd", il.ToString());
	}

	private static void renderPlayerToTempA(Level level) {
		Console.WriteLine("Haiiiii");
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

	private static Effect passShaderParams(Effect eff, Level level) {
		eff.Parameters["Time"].SetValue(level.TimeActive);
		eff.Parameters["CamPos"].SetValue(level.Camera.Position);
		eff.Parameters["Dimensions"].SetValue(new Vector2(1920, 1080));

		return eff;
	}

	private static void renderWithEffects(Level level) {
		List<Effect> effects = level.Tracker.GetEntities<HDShaderTrigger>().Cast<HDShaderTrigger>().Where(x => x.Activated).SelectMany(x => x.Effects).ToList();
		RenderTarget2D source, target;
		
		// i have no clue what these constants do 
		Vector2 vector = new Vector2(320f, 180f);
		Vector2 vector2 = vector / level.ZoomTarget;
		Vector2 vector3 = level.ZoomTarget != 1f ? (level.ZoomFocusPoint - vector2 / 2f) / (vector - vector2) * vector : Vector2.Zero;
		float scale = level.Zoom * ((vector.X -  level.ScreenPadding * 2f) / 320f);
		Vector2 vector4 = new Vector2(level.ScreenPadding, level.ScreenPadding * 0.5625f);

		Engine.Graphics.GraphicsDevice.SetRenderTarget(effects.Count > 0 ? (RenderTarget2D)targets[0] :  null);

		Engine.Graphics.GraphicsDevice.Clear(Color.Black);
		Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, ColorGrade.Effect, Matrix.CreateScale(6f) * Engine.ScreenMatrix);
		Draw.SpriteBatch.Draw((RenderTarget2D)GameplayBuffers.Level, vector3 + vector4, GameplayBuffers.Level.Bounds, Color.White, 0f, vector3, scale, SpriteEffects.None, 0f);
		Draw.SpriteBatch.End();

		if (effects.Count > 0) {
			for (int i = 0; i < effects.Count; i++) {
				source = targets[i % 2];
				target = i switch {
					_ when i == (effects.Count - 1) => null,
					_ => (RenderTarget2D)targets[1 - (i % 2)],
				};

				Engine.Graphics.GraphicsDevice.SetRenderTarget(target);
				Engine.Graphics.GraphicsDevice.Clear(Color.Black);
				Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, passShaderParams(effects[i], level), Engine.ScreenMatrix);
				// TODO: handle letterboxing properly
				Draw.SpriteBatch.Draw((RenderTarget2D)source, Vector2.Zero, source.Bounds, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
				Draw.SpriteBatch.End();
			}
		}

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
