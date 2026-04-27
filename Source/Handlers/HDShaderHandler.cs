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
public static class HDShaderHandler {
	private static readonly List<VirtualRenderTarget> flipflop_targets = new(2) { 
		VirtualContent.CreateRenderTarget("hd-shader-flip", 1920, 1080),
		VirtualContent.CreateRenderTarget("hd-shader-flop", 1920, 1080),
	};

	public static void IL_LevelRender_ApplyShader(ILContext il) {
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

	private static Effect passShaderParams(Shader shader, Level level, RenderTarget2D target) {
		Effect eff = shader.Effect;
		eff.Parameters["Time"].SetValue(level.TimeActive);
		eff.Parameters["CamPos"].SetValue(level.Camera.Position);
		eff.Parameters["Dimensions"].SetValue(new Vector2(1920, 1080));

		// Go my jank
		// XXX: 1920x1080 works on my other computer, Engine.Viewport works on this computer
		eff.Parameters["ViewMatrix"].SetValue(target == null ? Matrix.CreateOrthographicOffCenter(0, Engine.Viewport.Width, Engine.Viewport.Height, 0, 0, 1) : Matrix.CreateOrthographicOffCenter(0, target.Width, target.Height, 0, 0, 1));
		eff.Parameters["TransformMatrix"].SetValue(Matrix.Identity);

		// TODO TODO TODO TODO TODO AUGHHAHGHAHHGHHAHGHAH
		for (int i = 0; i < shader.MaskGroups.Length; i++) {
			if (shader.MaskGroups[i] != "") {
				Engine.Graphics.GraphicsDevice.Textures[i + 3] = level.Tracker.GetEntity<HDShaderController>().GetMaskGroupTarget(shader.MaskGroups[i]);
			}
		}

		return eff;
	}

	private static void renderWithShaders(Level level) {
		HDShaderController controller = level.Tracker.GetEntity<HDShaderController>();
		List<Shader> shaders = level.Tracker.GetEntities<HDShaderTrigger>().Cast<HDShaderTrigger>().Where(x => x.Activated).SelectMany(x => x.Shaders).ToList();
		bool applyShaders = shaders.Count > 0 && level.Tracker.CountEntities<HDShaderController>() > 0;
		
		// i have no clue what these constants do 
		Vector2 vector = new Vector2(320f, 180f);
		Vector2 vector2 = vector / level.ZoomTarget;
		Vector2 vector3 = level.ZoomTarget != 1f ? (level.ZoomFocusPoint - vector2 / 2f) / (vector - vector2) * vector : Vector2.Zero;
		float scale = level.Zoom * ((vector.X -  level.ScreenPadding * 2f) / 320f);
		Vector2 vector4 = new Vector2(level.ScreenPadding, level.ScreenPadding * /* 9f/16f, which is */ 0.5625f);

		// draw level
		Engine.Graphics.GraphicsDevice.SetRenderTarget(applyShaders ? (RenderTarget2D)flipflop_targets[0] : null);
		Engine.Graphics.GraphicsDevice.Clear(Color.Black);

		// for proper letterboxing
		if (!applyShaders) {
			Engine.Graphics.GraphicsDevice.Viewport = Engine.Viewport;
		}

		Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, ColorGrade.Effect, Matrix.CreateScale(6f) * Engine.ScreenMatrix);
		Draw.SpriteBatch.Draw((RenderTarget2D)GameplayBuffers.Level, vector3 + vector4, GameplayBuffers.Level.Bounds, Color.White, 0f, vector3, scale, SpriteEffects.None, 0f);
		Draw.SpriteBatch.End();

		if (!applyShaders) return;
		
		// draw masks
		List<IShaderMask> shaderMasks = level.Tracker.GetEntities<ShaderMask>().Cast<IShaderMask>().ToList();
		List<string> maskGroups = shaderMasks.SelectMany(x => x.MaskGroups).ToList();

		foreach (string group in maskGroups) {
			Engine.Graphics.GraphicsDevice.SetRenderTarget(controller.GetMaskGroupTarget(group));
			Engine.Graphics.GraphicsDevice.Clear(Color.Black);

			Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, ColorGrade.Effect, Matrix.CreateScale(6f) * Engine.ScreenMatrix);

			foreach (IShaderMask sm in shaderMasks.Where(x => x.MaskGroups.Contains(group))) {
				sm.RenderMask();
			}

			Draw.SpriteBatch.End();
		}

		// apply shaders
		RenderTarget2D source, target;

		for (int i = 0; i < shaders.Count; i++) {
			source = flipflop_targets[i % 2];
			target = i switch {
				_ when i == (shaders.Count - 1) => null,
				_ => (RenderTarget2D)flipflop_targets[1 - (i % 2)],
			};

			Engine.Graphics.GraphicsDevice.SetRenderTarget(target);
			Engine.Graphics.GraphicsDevice.Clear(Color.Black);

			// again, for proper letterboxing
			if (target == null) {
				Engine.Graphics.GraphicsDevice.Viewport = Engine.Viewport;
			}

			Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, passShaderParams(shaders[i], level, target), Engine.ScreenMatrix);
			Draw.SpriteBatch.Draw((RenderTarget2D)source, Vector2.Zero, source.Bounds, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
			Draw.SpriteBatch.End();

		}

		// render player over
		// TODO: should this just be a shader mask
		if (!level.Tracker.GetEntity<HDShaderController>().RenderPlayerOver) return;
		Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, ColorGrade.Effect, Matrix.CreateScale(6f) * Engine.ScreenMatrix);
		Draw.SpriteBatch.Draw((RenderTarget2D)GameplayBuffers.TempA, vector3 + vector4, GameplayBuffers.Level.Bounds, Color.White, 0f, vector3, scale, SpriteEffects.None, 0f);
		Draw.SpriteBatch.End();
	}
			
	public static void ApplyHooks() {
		IL.Celeste.Level.Render += IL_LevelRender_ApplyShader;
	}

	public static void RemoveHooks() {
		IL.Celeste.Level.Render -= IL_LevelRender_ApplyShader;
	}

}
