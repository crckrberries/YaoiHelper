using Celeste.Mod.Entities;
using Celeste.Mod.YaoiHelper.Entities;
using Celeste.Mod.YaoiHelper.Handlers;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.YaoiHelper.Triggers;

[Submodule]
[CustomEntity($"{nameof(YaoiHelper)}/{nameof(ForceInputsTrigger)}")]
[Tracked]
internal sealed class ForceInputsTrigger : Trigger {
	private readonly string inputstring;
	private readonly ForcedInputSet forcedSet;
	private readonly string flag;
	private readonly bool flagInverted;
	private readonly bool showStatus;
	private readonly string statusPrefix;
	private readonly bool hideStatusInCutscenes;
	private readonly float extraStatusVPadding;

	private bool playerInside = false;
	private StatusText? statusText;

	public ForceInputsTrigger(EntityData data, Vector2 offset) : base(data, offset) {
		inputstring = data.Attr("inputs");
		flag = data.Attr("flag");
		flagInverted = data.Bool("flagInverted");
		showStatus = data.Bool("showStatus");
		statusPrefix = data.Attr("statusPrefix");
		hideStatusInCutscenes = data.Bool("hideStatusInCutscenes");
		extraStatusVPadding = data.Float("extraStatusVerticalPadding");

		string[] inputs = inputstring.ToLower().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		validateInputs(inputs);
		forcedSet = toForcedInputSet(inputs);
	}

	internal static void ApplyHooks() {
		On.Celeste.Player.Update += On_PlayerUpdate_RunWithForced;
	}

	internal static void RemoveHooks() {
		On.Celeste.Player.Update -= On_PlayerUpdate_RunWithForced;
	}

	internal static void On_PlayerUpdate_RunWithForced(On.Celeste.Player.orig_Update orig, Player self) {
		ForceInputsTrigger? trigger = self.level.Tracker.GetEntities<ForceInputsTrigger>().OfType<ForceInputsTrigger>().FirstOrDefault(t => t.playerInside);
		if (trigger is not null) {
			ForceInputsHandler.WithForced(trigger.forcedSet, () => orig(self));
		} else {
			orig(self);
		}
	}

	private static void validateInputs(string[] inputs) {
		List<char> seen = new();
		bool seenH = false;
		bool seenV = false;
		int i;
		foreach (string input in inputs) {
			i = (input[0] == '!') ? 1 : 0;
			if (input.Length > 1 + i)
				throw new ArgumentException($"expected length of at most {1 + i}, got '{input}' which is length {input.Length}");
			if (seen.Contains(input[i]))
				throw new ArgumentException("can't have same input twice, or same input force pressed and force released!");
			seen.Add(input[i]);

			switch (input[i]) {
				case 'l':
				case 'r':
					if (seenH && input[0] != '!')
						throw new ArgumentException("can't force both L & R!");
					seenH = true;
					break;
				case 'u':
				case 'd':
					if (seenV && input[0] != '!')
						throw new ArgumentException("can't force both U & D!");
					seenV = true;
					break;
				case 'j':
				case 'x':
				case 'z':
				case 'g':
					break;
				default:
					throw new ArgumentException($"unknown input '{input[i]}'!");
			}
		}
	}

	private static ForcedInputState checkInputOverride(string[] inputs, char input) {
		if (inputs.Contains(input.ToString()))
			return ForcedInputState.ForcePress;
		else if (inputs.Contains("!" + input))
			return ForcedInputState.ForceRelease;
		return ForcedInputState.DontAffect;
	}

	private static ForcedInputSet toForcedInputSet(string[] inputs) => new(
		ForcedL: checkInputOverride(inputs, 'l'),
		ForcedR: checkInputOverride(inputs, 'r'),
		ForcedU: checkInputOverride(inputs, 'u'),
		ForcedD: checkInputOverride(inputs, 'd'),
		ForcedJ: checkInputOverride(inputs, 'j'),
		ForcedX: checkInputOverride(inputs, 'x'),
		ForcedZ: checkInputOverride(inputs, 'z'),
		ForcedG: checkInputOverride(inputs, 'g')
	);

	// TODO this just reuses the same logic from ShowStatusTrigger, fold them into one at some (barry, 63 voice) Pint

	public override void OnEnter(Player player) {
		base.OnEnter(player);

		if (!string.IsNullOrEmpty(flag) && player.level.Session.GetFlag(flag) == flagInverted)
			return;

		playerInside = true;

		if (showStatus && player is not null && player.level is not null) {
			if (statusText is not null) {
				Logger.Log(LogLevel.Warn, $"{nameof(YaoiHelper)}/ForceInputsTrigger", "statusText already exists on enter..? replacing");
				statusText.RemoveSelf();
			}
			statusText = new StatusText(statusPrefix + inputstring, 0.7f, Color.White, extraVPad: extraStatusVPadding);
			player.level.Add(statusText);
			player.level.OnEndOfFrame += () => {
				if (player is not null && player.level is not null)
					player.level.Entities.UpdateLists();
			};
		}
	}

	public override void OnStay(Player player) {
		base.OnStay(player);

		if (!string.IsNullOrEmpty(flag) && player.level.Session.GetFlag(flag) == flagInverted) {
			playerInside = false;
			return;
		}

		if (showStatus) {
			if (hideStatusInCutscenes)
				statusText?.ShouldRender = player.StateMachine.State != Player.StDummy;

			// for some reason, this is necessary to prevent it from sometimes
			// stopping to render 1 frame late
			player.level.OnEndOfFrame += () => {
				if (statusText is not null && (player is null || !CollideCheck(player))) {
					statusText.RemoveSelf();
					statusText = null;
					if (player is not null && player.level is not null)
						player.level.Entities.UpdateLists();
				}
			};
		}
	}

	public override void OnLeave(Player player) {
		base.OnLeave(player);

		playerInside = false;

		statusText?.RemoveSelf();
		statusText = null;

		if (showStatus && player is not null && player.level is not null) {
			player.level.OnEndOfFrame += () => {
				if (player is not null && player.level is not null)
					player.level.Entities.UpdateLists();
			};
		}
	}

	public override void Removed(Scene scene) {
		statusText?.RemoveSelf();
		statusText = null;
	}
}
