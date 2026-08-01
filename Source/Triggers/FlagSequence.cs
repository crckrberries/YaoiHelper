using System;
using System.Linq;
using System.Text.RegularExpressions;
using Celeste;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.RuntimeDetour;

namespace Crackerberries.YaoiHelper.Triggers;

[Submodule]
[CustomEntity($"{nameof(YaoiHelper)}/{nameof(FlagSequence)}")]
[Tracked]
public class FlagSequence : Trigger {
    private readonly string flagData; // raw data for the flag sequence, in the format <framecount>:[!]<flagname>
    private readonly bool useDeltaTime; // whether to use deltaTime or frame count
    private readonly bool offsetFreezeFrames; // whether to use cassette logic to offset the freezeframes or not
    private readonly bool loop; // whether to loop the flag sequence or have it fire once only
    private readonly bool useTotalTime; // whether to use the total time on the timer or to reset the timer on every flag activation

    private bool active;
    private int timer;
    private float deltaTimer;
    private int index;

    private (int, (string, bool)[])[] parsedFlagData = [];
    
    public FlagSequence(EntityData data, Vector2 offset) : base(data, offset) {
        flagData           = data.Attr("flagData");
        useDeltaTime       = data.Bool("useDeltaTime");
        offsetFreezeFrames = data.Bool("offsetFreezeFrames");
        loop               = data.Bool("loop");
        useTotalTime       = data.Bool("useTotalTime");
    }

    internal static void ApplyHooks() {
        using (new DetourConfigContext(new DetourConfig(
            $"{YaoiHelperModule.DefaultDetourID}_{nameof(DisableGlitchTrigger)}")).Use()) { 
            On.Celeste.Celeste.Freeze += on_CelesteFreeze_AdvanceFlagSequenceTimer;
        }
    }

    internal static void RemoveHooks() {
        On.Celeste.Celeste.Freeze -= on_CelesteFreeze_AdvanceFlagSequenceTimer;
    }

    private static void on_CelesteFreeze_AdvanceFlagSequenceTimer(On.Celeste.Celeste.orig_Freeze orig, float time) {
        orig(time);
        foreach (FlagSequence flagSequence in Engine.Scene.Tracker.GetEntities<FlagSequence>()) {
            if (flagSequence.offsetFreezeFrames) {
                flagSequence.advanceTimer((int) Math.Round(time * 60f));
            }
        }
    }

    public override void Awake(Scene scene) {
        base.Awake(scene);
        
        string[] splitFlagData = Regex.Split(flagData, "[;\n]");

        foreach (string data in splitFlagData) {
            string flagGroup = Regex.Split(data, ":")[1];
            string[] splitFlagGroup = Regex.Split(flagGroup, ",");
            (string, bool)[] parsedFlagGroup = [];
            foreach (string data2 in splitFlagGroup) {
                parsedFlagGroup = parsedFlagGroup.Append((
                        Regex.Replace(data2, "!", ""),
                        !Regex.IsMatch(data2, "!")
                    )).ToArray();
            }
            parsedFlagData = parsedFlagData.Append((
                    int.Parse(Regex.Split(data, ":")[0]),
                    parsedFlagGroup
                )).ToArray();
            
            foreach ((string, bool) data2 in parsedFlagGroup) {
                SceneAs<Level>()?.Session.SetFlag(data2.Item1, false); // todo: implement custom flag resetting
            }
        }
    }

    private void advanceTimer(int time = 1) {
        for (int i = 0; i < time; i++) {
            if (active) {
                if (!useDeltaTime) {
                    timer++;
                }
                else {
                    deltaTimer += Engine.DeltaTime;
                }

                while (parsedFlagData[index].Item1 <= timer || parsedFlagData[index].Item1 / 60f <= deltaTimer) {
                    foreach ((string, bool) flag in parsedFlagData[index].Item2) {
                        SceneAs<Level>()?.Session.SetFlag(flag.Item1, flag.Item2);
                    }

                    if (index == parsedFlagData.Length - 1) {
                        if (loop) {
                            index = -1; // ok
                            timer = 0;
                            deltaTimer = 0.00f;
                        }
                        else {
                            active = false;
                            return;
                        }
                    }

                    if (!useTotalTime) {
                        timer = 0;
                        deltaTimer = 0.00f;
                    }

                    index++;
                }
            }
        }
    }

    public override void Update() {
        base.Update();
        
        advanceTimer();
    }

    public override void OnEnter(Player player) {
        base.OnEnter(player);

        active = true;
    }
}