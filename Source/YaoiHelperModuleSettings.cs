using Celeste.Mod.YaoiHelper.Handlers;
using YamlDotNet.Serialization;

namespace Celeste.Mod.YaoiHelper;

public sealed class YaoiHelperModuleSettings : EverestModuleSettings {
	public bool BuildAnywhere { get; set; }

	[SettingSubMenu]
	public class SubMenu0 {
		public bool DisplayLog { get; set; }
		[SettingRange(min:0,max:100)]
		public int LogLifespan { get; set; } = 20;
		[SettingMinLength(1)]
		[SettingMaxLength(5)]
		public string LogPrefix { get; set; } = "BL/";
		[YamlIgnore]
		public bool ClearLog { get; set; }
		
		public void CreateClearLogEntry(TextMenuExt.SubMenu subMenu, bool inGame) {
			TextMenu.Button clearLog = new TextMenu.Button(Dialog.Clean("MODOPTIONS_YAOIHELPER_CLEARLOG"));
			clearLog.Pressed(() => YaoiLogger.ClearLog());
			subMenu.Add(clearLog);
		}
	}

	[SettingName("MODOPTIONS_YAOIHELPER_LOGSUBMENU")]
	public SubMenu0 LogSubMenu { get; set; } = new();
}
