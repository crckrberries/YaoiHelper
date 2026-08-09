using Celeste.Mod;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Crackerberries.YaoiHelper;

public sealed class YaoiHelperModule : EverestModule {
	public const string DefaultDetourID = "YaoiHelper";

	public static YaoiHelperModule Instance {
		get => field ?? throw new InvalidOperationException("YaoiHelperModule not instantiated yet");
		private set;
	}

	public override Type SettingsType => typeof(YaoiHelperModuleSettings);
	public static YaoiHelperModuleSettings Settings => (YaoiHelperModuleSettings) Instance._Settings;

	public override Type SessionType => typeof(YaoiHelperModuleSession);
	public static YaoiHelperModuleSession Session => (YaoiHelperModuleSession) Instance._Session;

	public override Type SaveDataType => typeof(YaoiHelperModuleSaveData);
	public static YaoiHelperModuleSaveData SaveData => (YaoiHelperModuleSaveData) Instance._SaveData;

	public static readonly EverestModuleMetadata SRTModuleMetadata = new() {
		Name = "SpeedrunTool",
		Version = new Version(3, 16, 1),
	};
	public static bool SRTLoaded { get; private set; }

	public static readonly EverestModuleMetadata CelesteNetClientMetadata = new() {
		Name = "CelesteNet.Client",
		Version = new Version(1, 0, 0),
	};
	public static bool CNetLoaded { get; private set; }

	private static Dictionary<Type, SubmoduleAttribute>? submodules;

	public YaoiHelperModule() {
		Instance = this;
#if DEBUG
		Logger.SetLogLevel(nameof(YaoiHelper), LogLevel.Verbose);
#else
		Logger.SetLogLevel(nameof(YaoiHelper), LogLevel.Info);
#endif
	}

	public override void Load() {
		SRTLoaded = Everest.Loader.DependencyLoaded(SRTModuleMetadata);
		CNetLoaded = Everest.Loader.DependencyLoaded(CelesteNetClientMetadata);

		Dictionary<Type, BootstrapAttribute> bootstrap = getTypesWithAttr<BootstrapAttribute>(typeof(YaoiHelperModule).Assembly);
		foreach ((Type t, BootstrapAttribute attr) in bootstrap.OrderBy(static kvp => kvp.Value.Order)) {
			Logger.Log(LogLevel.Debug, $"{nameof(YaoiHelper)}/Load", $"calling bootstrap {t.Name} (Order = {attr.Order})");
			invoke(t, "Init");
		}

		submodules = getTypesWithAttr<SubmoduleAttribute>(typeof(YaoiHelperModule).Assembly);
		using (new DetourConfigContext(new DetourConfig(
			DefaultDetourID,
			priority: 0
		)).Use()) {
			foreach ((Type t, SubmoduleAttribute attr) in submodules.OrderBy(static kvp => kvp.Value.Order)) {
				Logger.Log(LogLevel.Debug, $"{nameof(YaoiHelper)}/Load", $"loading submodule {t.Name} (Order = {attr.Order})");
				invoke(t, "ApplyHooks");
				if (SRTLoaded && attr.HasSRTSupport) {
					Logger.Log(LogLevel.Debug, $"{nameof(YaoiHelper)}/Load", $"loading SRT support for submodule {t.Name} (Order = {attr.Order})");
					invoke(t, "RegisterSRTSupport");
				}
			}
		}
	}

	public override void Unload() {
		if (submodules is not null) {
			foreach ((Type t, SubmoduleAttribute attr) in submodules.OrderByDescending(static kvp => kvp.Value.Order)) {
				if (SRTLoaded && attr.HasSRTSupport) {
					Logger.Log(LogLevel.Debug, $"{nameof(YaoiHelper)}/Unload", $"unloading SRT support for submodule {t.Name} (Order = {attr.Order})");
					invoke(t, "UnregisterSRTSupport");
				}
				Logger.Log(LogLevel.Debug, $"{nameof(YaoiHelper)}/Unload", $"unloading submodule {t.Name} (Order = {attr.Order})");
				invoke(t, "RemoveHooks");
			}
		}
	}

	private static Dictionary<Type, T> getTypesWithAttr<T>(Assembly asm) where T : Attribute {
		// enumerate upfront to hit ReflectionTypeLoadException if there is one
		Type[] types;
		try {
			types = asm.GetTypes().Where(static t => t.IsDefined(typeof(T))).ToArray();
		} catch (ReflectionTypeLoadException e) {
			types = e.Types.Where(static t => t is not null && t.IsDefined(typeof(T))).ToArray()!;
		}
		return types.ToDictionary(static t => t, static t => t.GetCustomAttribute<T>() ?? throw new InvalidOperationException("expected GetCustomAttribute to succeed after IsDefined returned true"));
	}

	private static void invoke(Type t, string m) {
		MethodInfo mi = t.GetMethod(m, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new MissingMethodException($"{t.Name} is missing {m}()");
		try {
			mi.Invoke(null, null);
		} catch (TargetParameterCountException) {
			throw new MissingMethodException($"{t.Name} must have {m}() take in no arguments");
		}
	}
}
