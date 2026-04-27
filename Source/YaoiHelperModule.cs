using System;
using Celeste.Mod.YaoiHelper.Entities;
using Celeste.Mod.YaoiHelper.Handlers;
using Celeste.Mod.YaoiHelper.Triggers;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.YaoiHelper;

public class YaoiHelperModule : EverestModule {
    public static YaoiHelperModule Instance { get; private set; }

    public override Type SettingsType => typeof(YaoiHelperModuleSettings);
    public static YaoiHelperModuleSettings Settings => (YaoiHelperModuleSettings) Instance._Settings;

    public override Type SessionType => typeof(YaoiHelperModuleSession);
    public static YaoiHelperModuleSession Session => (YaoiHelperModuleSession) Instance._Session;

    public override Type SaveDataType => typeof(YaoiHelperModuleSaveData);
    public static YaoiHelperModuleSaveData SaveData => (YaoiHelperModuleSaveData) Instance._SaveData;

    public YaoiHelperModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(YaoiHelperModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(YaoiHelperModule), LogLevel.Info);
#endif
    }

    public override void Load() {
		HDShaderHandler.ApplyHooks();
		DisableGlitchTrigger.ApplyHooks();
		Everest.Events.Level.OnLoadLevel += static (Level level, Player.IntroTypes introType, bool fromLoader) => {
			level.Add(new BuildController(new EntityData(), new Vector2(0, 0)));
		};
    }

    public override void Unload() {
		HDShaderHandler.RemoveHooks();
		DisableGlitchTrigger.RemoveHooks();
		Everest.Events.Level.OnLoadLevel -= static (Level level, Player.IntroTypes introType, bool fromLoader) => {
			level.Add(new BuildController(new EntityData(), new Vector2(0, 0)));
		};
    }
}
