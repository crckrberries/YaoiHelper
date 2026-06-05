using Celeste.Mod.YaoiHelper.Handlers;
using YamlDotNet.Serialization;

namespace Celeste.Mod.YaoiHelper;

public sealed class YaoiHelperModuleSettings : EverestModuleSettings {
	public bool BuildAnywhere { get; set; }
	public bool DisplayLog { get; set; }
	[YamlIgnore]
	public bool ClearLog { get; set; }
	
	public void CreateClearLogEntry(TextMenu menu, bool inGame) {
		TextMenu.Button clearLog = new TextMenu.Button(Dialog.Clean("MODOPTIONS_YAOIHELPER_CLEARLOG"));
		clearLog.Pressed(() => YaoiLogger.ClearLog());
		menu.Add(clearLog);
	}
}
