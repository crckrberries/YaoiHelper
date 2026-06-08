using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Reflection;

namespace Celeste.Mod.YaoiHelper.Handlers;

public enum ForcedInputState {
	ForcePress,
	ForceRelease,
	DontAffect,
}

public readonly record struct ForcedInputSet(
	ForcedInputState ForcedL,
	ForcedInputState ForcedR,
	ForcedInputState ForcedU,
	ForcedInputState ForcedD,
	ForcedInputState ForcedJ,
	ForcedInputState ForcedX,
	ForcedInputState ForcedZ,
	ForcedInputState ForcedG
) {
	internal void Validate() {
		static void validateOne(ForcedInputState state) {
			if (!(state is ForcedInputState.ForcePress or ForcedInputState.ForceRelease or ForcedInputState.DontAffect))
				throw new ArgumentOutOfRangeException(nameof(state), state, "out of range ForcedInputState enum value");
		}
		validateOne(ForcedL);
		validateOne(ForcedR);
		validateOne(ForcedU);
		validateOne(ForcedD);
		validateOne(ForcedJ);
		validateOne(ForcedX);
		validateOne(ForcedZ);
		validateOne(ForcedG);
	}
};

[Submodule]
public static class ForceInputsHandler {
	private static bool hooksEnabled = false;
	private static bool? globalForcedJ = null;
	private static bool? globalForcedX = null;
	private static bool? globalForcedZ = null;
	private static bool? globalForcedG = null;

	private static DynData<VirtualJoystick>? aimDd;

	private static Hook? virtualButtonGetPressedHook;
	private static Hook? virtualButtonGetCheckHook;
	private static Hook? virtualButtonGetReleasedHook;
	private static Hook? inputGetDashPressedHook;
	private static Hook? inputGetCrouchDashPressedHook;
	private static Hook? inputGetGrabCheckHook;

	public static void WithForced(in ForcedInputSet forced, Action fn) {
		forced.Validate();

		Vector2 oldAim = Input.Aim;
		int oldMoveX = Input.MoveX.Value;
		int oldMoveY = Input.MoveY.Value;
		int oldGliderMoveY = Input.GliderMoveY.Value;

		Vector2 newAim = Input.Aim;
		int newMoveX = Input.MoveX.Value;
		int newMoveY = Input.MoveY.Value;
		int newGliderMoveY = Input.GliderMoveY.Value;

		if (forced.ForcedL != ForcedInputState.DontAffect) {
			if (forced.ForcedL == ForcedInputState.ForcePress) {
				// force press left
				newAim.X = -1.0f;
				newMoveX = -1;
			} else {
				// force release left
				newAim.X = Math.Max(0, newAim.X);
				newMoveX = Math.Max(0, newMoveX);
			}
		}
		if (forced.ForcedR != ForcedInputState.DontAffect) {
			if (forced.ForcedR == ForcedInputState.ForcePress) {
				// force press right
				newAim.X = 1.0f;
				newMoveX = 1;
			} else {
				// force release right
				newAim.X = Math.Min(0, newAim.X);
				newMoveX = Math.Min(0, newMoveX);
			}
		}
		if (forced.ForcedU != ForcedInputState.DontAffect) {
			if (forced.ForcedU == ForcedInputState.ForcePress) {
				// force press up
				newAim.Y = -1.0f;
				newMoveY = -1;
				newGliderMoveY = -1;

			} else {
				// force release up
				newAim.Y = Math.Max(0, newAim.Y);
				newMoveY = Math.Max(0, newMoveY);
				newGliderMoveY = Math.Max(0, newGliderMoveY);
			}
		}
		if (forced.ForcedD != ForcedInputState.DontAffect) {
			if (forced.ForcedD == ForcedInputState.ForcePress) {
				// force press down
				newAim.Y = 1.0f;
				newMoveY = 1;
				newGliderMoveY = 1;
			} else {
				// force release down
				newAim.Y = Math.Min(0, newAim.Y);
				newMoveY = Math.Min(0, newMoveY);
				newGliderMoveY = Math.Min(0, newGliderMoveY);
			}
		}

		// the latter two shouldn't ever happen but just in case
		if (aimDd is null || !aimDd.IsAlive || !ReferenceEquals(aimDd.Target, Input.Aim))
			aimDd = new DynData<VirtualJoystick>(Input.Aim);

		aimDd["Value"] = newAim;
		Input.MoveX.Value = newMoveX;
		Input.MoveY.Value = newMoveY;
		Input.GliderMoveY.Value = newGliderMoveY;

		globalForcedJ = toBool(forced.ForcedJ);
		globalForcedX = toBool(forced.ForcedX);
		globalForcedZ = toBool(forced.ForcedZ);
		globalForcedG = toBool(forced.ForcedG);

		try {
			hooksEnabled = true;
			fn();
		}
		finally {
			hooksEnabled = false;
			resetGlobal();

			aimDd["Value"] = oldAim;
			Input.MoveX.Value = oldMoveX;
			Input.MoveY.Value = oldMoveY;
			Input.GliderMoveY.Value = oldGliderMoveY;
		}
	}

	private static bool? toBool(ForcedInputState state) => state switch {
		ForcedInputState.ForcePress => true,
		ForcedInputState.ForceRelease => false,
		ForcedInputState.DontAffect => null,
		_ => throw new ArgumentOutOfRangeException(nameof(state), state, "out of range ForcedInputState enum value"),
	};

	internal static void ApplyHooks() {
		On.Celeste.Player.Die += On_PlayerDie_ResetState;

		virtualButtonGetPressedHook = new Hook(
			typeof(VirtualButton).GetProperty("Pressed", BindingFlags.Instance | BindingFlags.Public)?.GetGetMethod() ??
				throw new MissingMethodException(nameof(VirtualButton), "get_Pressed"),
			On_VirtualButtonGetCheckOrPressed_OverrideInput
		);
		virtualButtonGetCheckHook = new Hook(
			typeof(VirtualButton).GetProperty("Check", BindingFlags.Instance | BindingFlags.Public)?.GetGetMethod() ??
				throw new MissingMethodException(nameof(VirtualButton), "get_Check"),
			On_VirtualButtonGetCheckOrPressed_OverrideInput
		);
		virtualButtonGetReleasedHook = new Hook(
			typeof(VirtualButton).GetProperty("Released", BindingFlags.Instance | BindingFlags.Public)?.GetGetMethod() ??
				throw new MissingMethodException(nameof(VirtualButton), "get_Released"),
			On_VirtualButtonGetReleased_OverrideInput
		);
		inputGetDashPressedHook = new Hook(
			typeof(Input).GetProperty("DashPressed", BindingFlags.Static | BindingFlags.Public)?.GetGetMethod() ??
				throw new MissingMethodException(nameof(Input), "get_DashPressed"),
			On_InputGetDashPressed_OverrideInput
		);
		inputGetCrouchDashPressedHook = new Hook(
			typeof(Input).GetProperty("CrouchDashPressed", BindingFlags.Static | BindingFlags.Public)?.GetGetMethod() ??
				throw new MissingMethodException(nameof(Input), "get_CrouchDashPressed"),
			On_InputGetCrouchDashPressed_OverrideInput
		);
		inputGetGrabCheckHook = new Hook(
			typeof(Input).GetProperty("GrabCheck", BindingFlags.Static | BindingFlags.Public)?.GetGetMethod() ??
				throw new MissingMethodException(nameof(Input), "get_GrabCheck"),
			On_InputGetGrabCheck_OverrideInput
		);
	}

	internal static void RemoveHooks() {
		On.Celeste.Player.Die -= On_PlayerDie_ResetState;

		virtualButtonGetPressedHook?.Dispose();
		virtualButtonGetCheckHook?.Dispose();
		virtualButtonGetReleasedHook?.Dispose();
		inputGetDashPressedHook?.Dispose();
		inputGetCrouchDashPressedHook?.Dispose();
		inputGetGrabCheckHook?.Dispose();

		virtualButtonGetPressedHook = null;
		virtualButtonGetCheckHook = null;
		virtualButtonGetReleasedHook = null;
		inputGetDashPressedHook = null;
		inputGetCrouchDashPressedHook = null;
		inputGetGrabCheckHook = null;
	}

	internal static PlayerDeadBody On_PlayerDie_ResetState(On.Celeste.Player.orig_Die orig, Player player, Vector2 direction, bool evenIfInvincible = false, bool registerDeathInStats = true) {
		resetGlobal();
		return orig(player, direction, evenIfInvincible, registerDeathInStats);
	}

	private static bool? checkButtonOverride(VirtualButton button) {
		if (button == Input.Jump)
			return globalForcedJ;
		if (button == Input.Dash)
			return globalForcedX;
		if (button == Input.CrouchDash)
			return globalForcedZ;
		if (button == Input.Grab)
			return globalForcedG;
		return null;
	}

	internal static bool On_VirtualButtonGetCheckOrPressed_OverrideInput(Func<VirtualButton, bool> orig, VirtualButton self) {
		bool? overrideInput = checkButtonOverride(self);
		return hooksEnabled ? (overrideInput ?? orig(self)) : orig(self);
	}

	internal static bool On_VirtualButtonGetReleased_OverrideInput(Func<VirtualButton, bool> orig, VirtualButton self) {
		bool? overrideInput = checkButtonOverride(self);
		return hooksEnabled ? (overrideInput ?? orig(self)) : orig(self);
	}

	internal static bool On_InputGetDashPressed_OverrideInput(Func<bool> orig) {
		return hooksEnabled ? (globalForcedX ?? orig()) : orig();
	}

	internal static bool On_InputGetCrouchDashPressed_OverrideInput(Func<bool> orig) {
		return hooksEnabled ? (globalForcedZ ?? orig()) : orig();
	}

	internal static bool On_InputGetGrabCheck_OverrideInput(Func<bool> orig) {
		return hooksEnabled ? (globalForcedG ?? orig()) : orig();
	}

	private static void resetGlobal() {
		globalForcedJ = null;
		globalForcedX = null;
		globalForcedZ = null;
		globalForcedG = null;
	}
}
