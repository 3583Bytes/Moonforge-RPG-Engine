using System;
using System.Collections.Generic;
using Moonforge.Sample.MonsterCatcher.WorldGen;
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
            AnsiConsole.MarkupLine(Escape(prompt));
            return 0;
        }

        SelectionPrompt<string> selection = new SelectionPrompt<string>()
            .Title(Escape(prompt))
            .PageSize(10)
            .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold));

        List<string> escapedOptions = new(options.Count);
        for (int i = 0; i < options.Count; i++)
        {
            escapedOptions.Add(Escape(options[i]));
            selection.AddChoice(escapedOptions[i]);
        }

        string picked = AnsiConsole.Prompt(selection);
        for (int i = 0; i < escapedOptions.Count; i++)
        {
            if (escapedOptions[i] == picked) return i;
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
    /// Renders one screen of the world. The screen IS the viewport — the whole grid is
    /// drawn each frame with the player glyph overlaid. No camera scrolling within a screen.
    /// </summary>
    public static void RenderScreen(WorldScreen screen, int playerX, int playerY)
    {
        System.Text.StringBuilder line = new();
        for (int y = 0; y < screen.Height; y++)
        {
            line.Clear();
            for (int x = 0; x < screen.Width; x++)
            {
                if (x == playerX && y == playerY)
                {
                    line.Append("[bold cyan1]@[/]");
                    continue;
                }

                line.Append(GlyphFor(screen, x, y));
            }

            AnsiConsole.MarkupLine(line.ToString());
        }
    }

    private static string GlyphFor(WorldScreen screen, int x, int y) => screen.TileAt(x, y) switch
    {
        OverworldTile.Wall    => "[green4]#[/]",          // tree / rock
        OverworldTile.Path    => "[grey50].[/]",
        OverworldTile.Grass   => "[green3_1],[/]",        // tall grass
        OverworldTile.Water   => "[blue]~[/]",
        OverworldTile.HealPad => "[bold red]+[/]",        // heal pad (was 'C')
        OverworldTile.ShopPad => "[bold yellow]$[/]",     // shop counter
        OverworldTile.GymPad  => "[bold magenta]G[/]",    // gym leader's mat
        OverworldTile.Goal    => "[bold yellow]>[/]",     // Champion's Hall ending tile
        _ => "[grey50] [/]"
    };

    private static string Escape(string text) => Markup.Escape(text);
}
