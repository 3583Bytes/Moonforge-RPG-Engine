using System.Collections.Generic;

namespace Moonforge.Sample.ConsoleApp.Rendering;

/// <summary>
/// Centralized color names (Spectre.Console palette) so the renderer doesn't sprinkle
/// magic strings everywhere. Change once, reskin everywhere.
/// </summary>
internal static class Theme
{
    // Map symbols
    public const string Hero = "yellow1";
    public const string Guard = "cyan1";
    public const string Wall = "grey39";
    public const string Floor = "grey50";
    public const string Stairs = "red1";

    // HUD figures
    public const string Gold = "yellow";
    public const string Tokens = "mediumorchid1";
    public const string Potions = "green3";
    public const string Depth = "white";
    public const string ContractActive = "deepskyblue1";
    public const string ContractReady = "green1";

    // Battle factions
    public const string Party = "green";
    public const string Enemy = "red";
    public const string Turn = "bold yellow1";

    // HP bars
    public const string HpHigh = "green3";
    public const string HpMid = "yellow";
    public const string HpLow = "red1";
    public const string HpEmpty = "grey27";

    // Resource pips (focus)
    public const string FocusFilled = "deepskyblue1";
    public const string FocusEmpty = "grey39";

    // Log / messages
    public const string Damage = "red";
    public const string Heal = "green3";
    public const string Info = "skyblue1";
    public const string Success = "green1";
    public const string Warning = "yellow";
    public const string Error = "red1";
    public const string Muted = "grey58";

    // Per-symbol marker colors. Symbols not listed fall back to white.
    public static readonly IReadOnlyDictionary<char, string> MarkerColors = new Dictionary<char, string>
    {
        ['>'] = "red1",
        ['<'] = "red1",
        ['Q'] = "gold1",
        ['S'] = "mediumorchid1",
        ['A'] = "green3",
        ['H'] = "pink1",
        ['C'] = "chartreuse1",
        ['F'] = "lightskyblue1",
        ['P'] = "grey78",
        ['G'] = Guard
    };
}
