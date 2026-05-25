namespace Moonforge.Core.Equipment;

/// <summary>
/// Shared identifiers used when equipment commands push <c>StatModifier</c>s onto an actor's
/// stat block. Game code can read these constants when re-deriving modifiers from an already
/// populated <c>EquipmentState</c> (e.g. on save load).
/// </summary>
public static class EquipmentStatSource
{
    public const string Kind = "equipment";

    public static string Id(string slotId, string itemId) => $"{slotId}:{itemId}";
}
