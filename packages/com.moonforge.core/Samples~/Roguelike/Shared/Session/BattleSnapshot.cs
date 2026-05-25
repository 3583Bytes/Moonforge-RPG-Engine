namespace Moonforge.Sample.Roguelike.Session;

/// <summary>
/// Captures currency/inventory state at the moment a battle starts so the post-battle
/// summary can compute deltas against the after-state.
/// </summary>
public sealed record BattleSnapshot(
    string EncounterTitle,
    long Gold,
    long Tokens,
    int Potions,
    int Herbs);
