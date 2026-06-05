using Monocle;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Celeste.Mod.YaoiHelper.Handlers;

/**
 *  <summary>
 * Logger that outputs to an ingame text box and a separate text file.
 * </summary>
 */
[Submodule]
public static class YaoiLogger {
    private static readonly string logPath = Path.Combine(Everest.PathGame, "YaoiLog.txt");
    
    internal static void ApplyHooks() {
        Everest.Events.LevelLoader.OnLoadingThread += On_LoadingThread_AddLogDisplay;
    }

    internal static void RemoveHooks() {
        Everest.Events.LevelLoader.OnLoadingThread -= On_LoadingThread_AddLogDisplay;
    }
    
    public static void ClearLog() {
        LogDisplay.Content = new List<string>();
        if (!File.Exists(logPath)) return;
        using (StreamWriter sw = File.CreateText(logPath)) {
            sw.Write(string.Empty);
            sw.Close();
        }
    }
    
    public static void Log(LogLevel level, string tag, string message) {
        string logMessage = $"[{tag}] [{level.ToString()}] {message}";
        string fullLogMessage = $"({System.DateTime.Now}) {logMessage}";
        Logger.Log(level, tag, message);
        LogDisplay.AddLog(logMessage);
        using (StreamWriter sw = File.AppendText(logPath)) {
            sw.WriteLine(fullLogMessage);
            sw.Close();
        }
    }
    
    public static void Log(string tag, string message) {
        Log(LogLevel.Verbose, tag, message);
    }

    public static void Verbose(string tag, string message) {
        Log(LogLevel.Verbose, tag, message);
    }

    public static void Debug(string tag, string message) {
        Log(LogLevel.Debug, tag, message);
    }

    public static void Info(string tag, string message) {
        Log(LogLevel.Info, tag, message);
    }

    public static void Warn(string tag, string message) {
        Log(LogLevel.Warn, tag, message);
    }

    public static void Error(string tag, string message) {
        Log(LogLevel.Error, tag, message);
    }

    internal static void On_LoadingThread_AddLogDisplay(Level level) {
        level.Add(new LogDisplay());
    }
}

public sealed class LogDisplay : Entity {
    private const int maxLines = 20;
    private const float maxWidth = 1200;
    private const float fontSize = .4f;
    private static readonly Vector2 padding = new Vector2(7.5f, 5f);
    public static List<string> Content = new List<string>();
    private float width, height;
    public LogDisplay() {
        Position.Y = padding.Y;
        Tag = Tags.HUD | Tags.Global;
        Depth = -0xB00B1E;
    }

    public static void AddLog(string log) {
        if (ActiveFont.Measure(log).X * fontSize + padding.X * 2 > maxWidth) {
            YaoiLogger.Warn($"{nameof(YaoiHelper)}/{nameof(LogDisplay)}","Line was too long to display! Check YaoiLog.txt");
            return;
        }
        if(Content.Count == maxLines) Content.RemoveAt(0);
        Content.Add(log);
    }

    private void writeLine(string line, Vector2 position) {
        ActiveFont.DrawOutline(line, position, Vector2.Zero, Vector2.One*fontSize, Color.White, 2, Color.Black);
    }
    
    public override void Render() {
        base.Render();
        if (!YaoiHelperModule.Settings.DisplayLog || Scene is not Level || Content.Count() == 0) return;
        width = ActiveFont.Measure(Content.Aggregate("", (longest, log) => log.Length > longest.Length ? log : longest)).X * fontSize + padding.X * 2;
        height = Content.Count() * 25 + padding.Y * 2;
        Position.X = Engine.Width - width - padding.X;
        Draw.Rect(Position, width, height, new Color(Color.Black,.75f));
        for(int i = 0; i < Content.Count; i++) {
            Vector2 linePosition = Position + new Vector2(padding.X, padding.Y + 25 * i);
            writeLine(Content[i], linePosition);
        }
    }
}