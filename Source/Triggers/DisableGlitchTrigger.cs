using System.Linq;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity("YaoiHelper/DisableGlitchTrigger")]
[Tracked]
public sealed class DisableGlitchTrigger : Trigger {
	public bool AlwaysActive;
	public bool Activated;

	public DisableGlitchTrigger(EntityData data, Vector2 offset) : base(data, offset) {
		AlwaysActive = data.Bool("always_active");
		Activated = AlwaysActive;
	}

	public override void OnEnter(Player player) {
        base.OnEnter(player);
		Activated = true;
    }

    public override void OnLeave(Player player) {
        base.OnLeave(player);
		Activated = false || AlwaysActive;
    }

	public static void On_GlitchApply_DisableIfTrigger(On.Celeste.Glitch.orig_Apply orig, VirtualRenderTarget target, float timer, float seed, float amplitude) {
		if (!Engine.Scene.Tracker.GetEntities<DisableGlitchTrigger>().Cast<DisableGlitchTrigger>().Any(x => x.Activated)) {
			orig(target, timer, seed, amplitude);
		}
	}

	public static void ApplyHooks() {
		// hook as late as possible as to not intervene with other people's stuff 
		DetourConfig config = new DetourConfig($"{nameof(YaoiHelperModule)}/{nameof(DisableGlitchTrigger)}", priority: null);
		DetourConfigContext context = new DetourConfigContext(config);

		using (context.Use()) {
			On.Celeste.Glitch.Apply += On_GlitchApply_DisableIfTrigger;
		}
	}

	public static void RemoveHooks() {
		On.Celeste.Glitch.Apply -= On_GlitchApply_DisableIfTrigger;
	}
}
