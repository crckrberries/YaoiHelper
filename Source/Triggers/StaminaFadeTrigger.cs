// (voice that perfectly imitates berries's) a bunch of this is referenced from (goes back to my own voice) Speed Fade TRigger Dot Cs 
using System.Linq;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity($"{nameof(YaoiHelper)}/{nameof(StaminaFadeTrigger)}")]
[Tracked]
[Submodule]
public sealed class StaminaFadeTrigger : Trigger {
    private readonly Vector2[] nodes;

    public Trigger? Trigger;
    public readonly float LowerBound;
    public readonly float UpperBound;
    
    public StaminaFadeTrigger(EntityData data, Vector2 offset)
        : base(data, offset) {
        nodes = data.NodesOffset(offset);
        LowerBound = data.Float("lower_bound");
        UpperBound = data.Float("upper_bound");
    }

    public override void Awake(Scene scene) {
        base.Awake(scene);
        Trigger = scene.CollideFirst<Trigger>(nodes[0]) ?? scene.Tracker.GetNearestEntity<Trigger>(nodes[0]);
        Trigger?.Collidable = false;
    }
    
    public override void OnEnter(Player player) {
        base.OnEnter(player);
        Trigger?.OnEnter(player);
    }

    public override void OnStay(Player player) {
        base.OnStay(player);
        Trigger?.OnStay(player);
    }

    public override void OnLeave(Player player) {
        base.OnLeave(player);
        Trigger?.OnLeave(player);
    }

    internal static void ApplyHooks() {
        On.Celeste.Trigger.GetPositionLerp += On_TriggerGetPositionLerp_ApplyStaminaFade;
    }

    internal static void RemoveHooks() {
        On.Celeste.Trigger.GetPositionLerp -= On_TriggerGetPositionLerp_ApplyStaminaFade;
    }

    public static float On_TriggerGetPositionLerp_ApplyStaminaFade(On.Celeste.Trigger.orig_GetPositionLerp orig,
        Trigger self, Player player, PositionModes mode) {
        if (self.Scene is not Level level) return orig(self, player, mode);
        foreach (StaminaFadeTrigger staminaFadeTrigger in level.Tracker.GetEntities<StaminaFadeTrigger>().Cast<StaminaFadeTrigger>()) {
            if (staminaFadeTrigger.Trigger == self) {
                return float.Clamp((player.Stamina - staminaFadeTrigger.LowerBound) / (staminaFadeTrigger.UpperBound - staminaFadeTrigger.LowerBound), 0f, 1f);
            }
        }
        
        return orig(self, player, mode);
    }
}