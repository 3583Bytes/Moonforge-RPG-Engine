using System.Collections.Generic;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Combat.Commands;

public sealed class StartBattleCommand : ICommand
{
    public StartBattleCommand(
        string battleId,
        IReadOnlyList<BattleActorDefinition> actors,
        IReadOnlyList<BattleSkillDefinition> skills,
        ulong seed,
        ulong sequence = 777,
        IReadOnlyList<CurrencyDelta>? rewardCurrency = null,
        IReadOnlyList<InventoryDelta>? rewardInventory = null,
        string? rewardLootTableId = null)
    {
        BattleId = battleId;
        Actors = actors ?? System.Array.Empty<BattleActorDefinition>();
        Skills = skills ?? System.Array.Empty<BattleSkillDefinition>();
        Seed = seed;
        Sequence = sequence;
        RewardCurrency = rewardCurrency ?? System.Array.Empty<CurrencyDelta>();
        RewardInventory = rewardInventory ?? System.Array.Empty<InventoryDelta>();
        RewardLootTableId = string.IsNullOrWhiteSpace(rewardLootTableId) ? null : rewardLootTableId;
    }

    public string BattleId { get; }

    public IReadOnlyList<BattleActorDefinition> Actors { get; }

    public IReadOnlyList<BattleSkillDefinition> Skills { get; }

    public ulong Seed { get; }

    public ulong Sequence { get; }

    public IReadOnlyList<CurrencyDelta> RewardCurrency { get; }

    public IReadOnlyList<InventoryDelta> RewardInventory { get; }

    /// <summary>
    /// Optional loot table to roll on victory, layered on top of the static reward lists.
    /// Rolled atomically; failure aborts reward application.
    /// </summary>
    public string? RewardLootTableId { get; }
}
