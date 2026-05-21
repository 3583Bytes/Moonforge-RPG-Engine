using System;
using System.Collections.Generic;
using Spectre.Console;

namespace Moonforge.Sample.MonsterCatcher.Rendering;

/// <summary>
/// Thin wrapper over Spectre.Console for the sample's UI primitives. Keeps the game class
/// focused on game logic, not console rendering.
/// </summary>
internal static class Ui
{
    public static void Clear()
    {
        try
        {
            AnsiConsole.Clear();
        }
        catch
        {
            // Redirected output (tests) — best-effort.
        }
    }

    public static void Heading(string text, string color = "magenta")
    {
        AnsiConsole.MarkupLine($"[bold {color}]== {Escape(text)} ==[/]");
    }

    public static void Line(string text = "") => AnsiConsole.MarkupLine(text);

    public static void Info(string text) => AnsiConsole.MarkupLine($"[grey]{Escape(text)}[/]");

    public static void Note(string text) => AnsiConsole.MarkupLine($"[yellow]{Escape(text)}[/]");

    public static void Success(string text) => AnsiConsole.MarkupLine($"[green]{Escape(text)}[/]");

    public static void Failure(string text) => AnsiConsole.MarkupLine($"[red]{Escape(text)}[/]");

    public static void PressEnter(string prompt = "Press Enter to continue.")
    {
        AnsiConsole.MarkupLine($"[grey]{Escape(prompt)}[/]");
        if (Console.IsInputRedirected)
        {
            Console.ReadLine();
            return;
        }

        Console.ReadKey(intercept: true);
    }

    public static int ChooseOption(string prompt, IReadOnlyList<string> options)
    {
        if (Console.IsInputRedirected)
        {
            // In tests we can't run an interactive prompt; pick the first option deterministically.
            AnsiConsole.MarkupLine(Escape(prompt));
            return 0;
        }

        SelectionPrompt<string> selection = new SelectionPrompt<string>()
            .Title(Escape(prompt))
            .PageSize(10)
            .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold));

        // Spectre renders SelectionPrompt choices through its markup engine, so any '[' or ']'
        // in a label (e.g. "[fire, power 7, PP 25/25]") would be parsed as a style tag and
        // crash. Escape every label up-front so callers can pass arbitrary text. We map the
        // escaped form back to the original index after the user picks.
        List<string> escapedOptions = new(options.Count);
        for (int i = 0; i < options.Count; i++)
        {
            escapedOptions.Add(Escape(options[i]));
            selection.AddChoice(escapedOptions[i]);
        }

        string picked = AnsiConsole.Prompt(selection);
        for (int i = 0; i < escapedOptions.Count; i++)
        {
            if (escapedOptions[i] == picked)
            {
                return i;
            }
        }

        return 0;
    }

    public static string TypeColor(string typeId) => typeId switch
    {
        "type.fire" => "red",
        "type.water" => "blue",
        "type.grass" => "green",
        "type.electric" => "yellow",
        "type.ground" => "olive",
        "type.rock" => "tan",
        "type.flying" => "skyblue1",
        "type.ghost" => "purple",
        "type.dark" => "grey",
        _ => "white"
    };

    public static string TypeLabel(string typeId) => typeId.StartsWith("type.", StringComparison.Ordinal)
        ? typeId.Substring(5)
        : typeId;

    public static string HpBar(int hp, int maxHp, int width = 20)
    {
        if (maxHp <= 0) return new string(' ', width);
        int filled = Math.Max(0, Math.Min(width, (int)Math.Round((double)hp / maxHp * width)));
        string color = (hp * 4) switch
        {
            int x when x > maxHp * 2 => "green",
            int x when x > maxHp => "yellow",
            _ => "red"
        };

        string filledStr = new string('█', filled);
        string emptyStr = new string('░', width - filled);
        return $"[{color}]{filledStr}[/][grey]{emptyStr}[/]";
    }

    /// <summary>
    /// Renders a camera window of the overworld centered on the player. The viewport size
    /// is fixed; tiles outside the world bounds render as dark filler so the frame stays
    /// rectangular near the edges.
    /// </summary>
    public static void RenderMap(
        Moonforge.Sample.MonsterCatcher.WorldGen.Overworld map,
        int playerX,
        int playerY,
        int viewportWidth = 48,
        int viewportHeight = 14)
    {
        // Camera follows the player but clamps so we never scroll past the map edges.
        int camX = Math.Clamp(playerX - viewportWidth / 2, 0, Math.Max(0, map.Width - viewportWidth));
        int camY = Math.Clamp(playerY - viewportHeight / 2, 0, Math.Max(0, map.Height - viewportHeight));

        System.Text.StringBuilder line = new();
        for (int dy = 0; dy < viewportHeight; dy++)
        {
            int y = camY + dy;
            line.Clear();
            for (int dx = 0; dx < viewportWidth; dx++)
            {
                int x = camX + dx;
                if (x == playerX && y == playerY)
                {
                    line.Append("[bold cyan1]@[/]");
                    continue;
                }

                line.Append(GlyphFor(map, x, y));
            }

            AnsiConsole.MarkupLine(line.ToString());
        }
    }

    private static string GlyphFor(Moonforge.Sample.MonsterCatcher.WorldGen.Overworld map, int x, int y)
    {
        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
        {
            // Off-map: dark filler so the viewport stays a solid rectangle near edges.
            return "[grey15] [/]";
        }

        return map.TileAt(x, y) switch
        {
            Moonforge.Sample.MonsterCatcher.WorldGen.OverworldTile.Wall       => "[green4]#[/]",     // tree
            Moonforge.Sample.MonsterCatcher.WorldGen.OverworldTile.Path       => "[grey50].[/]",
            Moonforge.Sample.MonsterCatcher.WorldGen.OverworldTile.Grass      => "[green3_1],[/]",   // tall grass
            Moonforge.Sample.MonsterCatcher.WorldGen.OverworldTile.Water      => "[blue]~[/]",
            Moonforge.Sample.MonsterCatcher.WorldGen.OverworldTile.PokeCenter => "[bold red]C[/]",
            Moonforge.Sample.MonsterCatcher.WorldGen.OverworldTile.Goal       => "[bold yellow]>[/]",
            _ => "[grey50] [/]"
        };
    }

    private static string Escape(string text) => Markup.Escape(text);
}
