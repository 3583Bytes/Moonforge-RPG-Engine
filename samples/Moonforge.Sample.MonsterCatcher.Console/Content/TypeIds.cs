namespace Moonforge.Sample.MonsterCatcher.Content;

/// <summary>String constants for the move-type IDs the sample uses for both damage types
/// and defender types. The shared name space is fine because the engine treats them as
/// opaque strings on both sides.</summary>
internal static class TypeIds
{
    public const string Normal = "type.normal";
    public const string Fire = "type.fire";
    public const string Water = "type.water";
    public const string Grass = "type.grass";
    public const string Electric = "type.electric";
    public const string Ground = "type.ground";
    public const string Rock = "type.rock";
    public const string Flying = "type.flying";
    public const string Ghost = "type.ghost";
    public const string Dark = "type.dark";

    public const string EffectivenessChart = "chart.elemental";
}
