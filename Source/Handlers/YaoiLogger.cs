using Monocle;
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
    internal static void ApplyHooks() {
        Everest.Events.LevelLoader.OnLoadingThread += On_LoadingThread_AddLogDisplay;
    }

    internal static void RemoveHooks() {
        Everest.Events.LevelLoader.OnLoadingThread -= On_LoadingThread_AddLogDisplay;
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
            //TODO figure out what the fuck to do with this
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