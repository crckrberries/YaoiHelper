using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using VivHelper;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity($"{nameof(YaoiHelper)}/{nameof(NonBrokenInstantCameraCatchupTrigger)}")]
internal sealed class NonBrokenInstantCameraCatchupTrigger : Trigger {
	// TODO: stop depending on Viv entirely and write a custom camera lock, this is
	// like one gigantic walking race condition

	private readonly string flag;
	private readonly bool flagInverted;
	private readonly bool onlyOnEnter;

	private bool camLockDirty = false;

	public NonBrokenInstantCameraCatchupTrigger(EntityData data, Vector2 offset) : base(data, offset) {
		flag = data.Attr("flag");
		flagInverted = data.Bool("flagInverted");
		onlyOnEnter = data.Bool("onlyOnEnter");
	}

	public override void OnEnter(Player player) {
		base.OnEnter(player);
		if (string.IsNullOrEmpty(flag) || player.level.Session.GetFlag(flag) != flagInverted) {
			VivHelperModule.Session.lockCamera = onlyOnEnter ? 1 : -1; // expire either after 1f or Never
			camLockDirty = true;
		}
	}

	public override void OnLeave(Player player) {
		base.OnLeave(player);
		if (camLockDirty) {
			VivHelperModule.Session.lockCamera = 0;
			camLockDirty = false;
		}
	}

	public override void SceneEnd(Scene scene) {
		base.SceneEnd(scene);
		if (camLockDirty) {
			VivHelperModule.Session.lockCamera = 0;
			camLockDirty = false;
		}
	}

	~NonBrokenInstantCameraCatchupTrigger() {
		try {
			if (camLockDirty) {
				VivHelperModule.Session.lockCamera = 0;
				camLockDirty = false;
			}
		} catch {
		}
	}
}
