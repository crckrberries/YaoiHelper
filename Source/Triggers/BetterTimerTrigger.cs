using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.YaoiHelper.Triggers;

[CustomEntity($"{nameof(YaoiHelper)}/{nameof(BetterTimerTrigger)}")]
internal sealed class BetterTimerTrigger : Trigger {
	private readonly float time;
	private readonly int frames;

	private readonly string flag;
	private readonly bool unset;

	private readonly string ctrl;
	private readonly bool ctrlInvert;

	private readonly int mode;
	private readonly bool resetOnLeave;
	private readonly bool resetOnCtrlUnset;
	private readonly bool unsetOnRoomLoad;
	private readonly bool compareLOnly;

	private bool playerInside = false;
	private float timer = 0f;
	private int frameTimer = 0;

	public BetterTimerTrigger(EntityData data, Vector2 offset) : base(data, offset) {
		time = data.Float("time");
		frames = data.Int("frames");
		flag = data.Attr("flagToSet");
		unset = data.Bool("unsetFlag");
		ctrl = data.Attr("controlFlag");
		ctrlInvert = data.Bool("controlFlagInverted");
		mode = data.Int("mode");
		resetOnLeave = data.Bool("resetTimerOnLeave");
		resetOnCtrlUnset = data.Bool("resetTimerOnControlFlagUnset");
		unsetOnRoomLoad = data.Bool("unsetOnRoomLoad");
		compareLOnly = data.Bool("compareLOnly");
	}

	public override void Added(Scene scene) {
		base.Added(scene);

		if (unsetOnRoomLoad)
			(Scene as Level)?.Session.SetFlag(flag, unset);
	}


	public override void Update() {
		base.Update();

		if (!playerInside)
			return;

		Session session = ((Level)Scene).Session;

		if (session.GetFlag(flag) == !unset)
			return;

		if (string.IsNullOrEmpty(ctrl) || session.GetFlag(ctrl) != ctrlInvert) {
			switch (mode) {
				case 0: // "DeltaTime"
					timer += Engine.DeltaTime;
					if ((!compareLOnly && timer >= time) || (compareLOnly && timer > time))
						session.SetFlag(flag, !unset);
					break;
				case 1: // "RawDeltaTime"
					timer += Engine.RawDeltaTime;
					if ((!compareLOnly && timer >= time) || (compareLOnly && timer > time))
						session.SetFlag(flag, !unset);
					break;
				case 2: // "Frame count"
					frameTimer++;
					if ((!compareLOnly && frameTimer >= frames) || (compareLOnly && frameTimer > frames))
						session.SetFlag(flag, !unset);
					break;
			}
		} else if (resetOnCtrlUnset && !string.IsNullOrEmpty(ctrl)) {
			timer = 0f;
			frameTimer = 0;
		}
	}

	public override void OnEnter(Player player) {
		base.OnEnter(player);

		playerInside = true;
	}

	public override void OnLeave(Player player) {
		base.OnLeave(player);

		playerInside = false;
		if (resetOnLeave) {
			timer = 0f;
			frameTimer = 0;
		}
	}
}
