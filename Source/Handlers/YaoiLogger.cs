using System;
using Monocle;
using System.Linq;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.YaoiHelper.Handlers;

/**
 *  <summary>
 * Displays every log with a configurable prefix (<c>BL/</c> by default) on a text box.
 * </summary>
 */
[Submodule]
public static class YaoiLogger {
    private static Hook? loggerLogHook;

    public static void ClearLog() {
        LogDisplay.Content = new List<LogLine>();
    }
    
    internal static void ApplyHooks() {
        Everest.Events.LevelLoader.OnLoadingThread += On_LoadingThread_AddLogDisplay;
        loggerLogHook = new Hook(
                typeof(Logger).GetMethod("Log", BindingFlags.Static | BindingFlags.Public, 
                    [typeof(LogLevel), typeof(string), typeof(string)])
                    ?? throw new MissingMethodException(nameof(Logger), "Log"),
                On_Logger_Log
            );
    }

    internal static void RemoveHooks() {
        Everest.Events.LevelLoader.OnLoadingThread -= On_LoadingThread_AddLogDisplay;
        loggerLogHook?.Dispose();
        loggerLogHook = null;
    }

    internal static void On_LoadingThread_AddLogDisplay(Level level) {
        level.Add(new LogDisplay());
    }
    internal static void On_Logger_Log(Action<LogLevel, string, string> orig, LogLevel level, string tag, string str) {
        string prefix = YaoiHelperModule.Settings.LogSubMenu.LogPrefix;
        if(tag.StartsWith(prefix)) 
            LogDisplay.AddLog($"[{tag.Substring(prefix.Length)}] [{level.ToString()}] {str}");
        orig(level, tag, str);
    }
}

public sealed record LogLine(string Line, int TimeLeft) {
    public int TimeLeft { get; set; } = TimeLeft;
}

public sealed class LogDisplay : Entity {
    private const int maxLines = 20;
    private const float maxLineLength = 130;
    private const float fontSize = .4f;
    private static readonly Vector2 padding = new Vector2(7.5f, 5f);
    public static List<LogLine> Content = new List<LogLine>();
    private float width, height;
    public LogDisplay() {
        Position.Y = padding.Y;
        Tag = Tags.HUD | Tags.Global;
        Depth = -0xB00B1E;
    }

    public static void AddLog(string log) {
        if (log.Length > maxLineLength) {
            string[] words = log.Split(" ");
            if (words.Length == 1) {
                pushLine("Bro what");
                return;
            }
            List<string> lines = new List<string>();
            string currentLine = words[0];
            for (int i = 1; i < words.Length; i++) {
                if ($"{currentLine} {words[i]}".Length > maxLineLength) {
                    lines.Add(currentLine);
                    currentLine = words[i];
                } else {
                    currentLine += $" {words[i]}";
                }
            }
            lines.Add(currentLine);
            foreach (string line in lines) {
                pushLine(line);
            }
            return;
        }
        pushLine(log);
    }

    private static void pushLine(string line) {
        if(Content.Count == maxLines) Content.RemoveAt(0);
        Content.Add(new LogLine(line, YaoiHelperModule.Settings.LogSubMenu.LogLifespan*60));
    }

    private void writeLine(string line, Vector2 position) {
        ActiveFont.DrawOutline(line, position, Vector2.Zero, Vector2.One*fontSize, Color.White, 2, Color.Black);
    }

    public override void Update() {
        base.Update();
        if(YaoiHelperModule.Settings.LogSubMenu.LogLifespan > 0) {
            foreach (LogLine line in Content) {
                line.TimeLeft--;
            }

            Content.RemoveAll(line => line.TimeLeft <= 0);
        }
    }

    public override void Render() {
        base.Render();
        if (!YaoiHelperModule.Settings.LogSubMenu.DisplayLog || Scene is not Level level || level.FrozenOrPaused || Content.Count() == 0) return;
        width = ActiveFont.Measure(Content.Aggregate("", (longest, log) => log.Line.Length > longest.Length ? log.Line : longest)).X * fontSize + padding.X * 2;
        height = Content.Count() * 25 + padding.Y * 2;
        Position.X = Engine.Width - width - padding.X;
        Draw.Rect(Position, width, height, new Color(Color.Black,.75f));
        for(int i = 0; i < Content.Count; i++) {
            Vector2 linePosition = Position + new Vector2(padding.X, padding.Y + 25 * i);
            writeLine(Content[i].Line, linePosition);
        }
    }
}