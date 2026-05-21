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

        foreach (string option in options)
        {
            selection.AddChoice(option);
        }

        string picked = AnsiConsole.Prompt(selection);
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == picked)
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

    private static string Escape(string text) => Markup.Escape(text);
}
