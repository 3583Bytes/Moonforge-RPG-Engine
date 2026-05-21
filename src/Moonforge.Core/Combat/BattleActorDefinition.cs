using System;
using System.Collections.Generic;

namespace Moonforge.Core.Combat;

public sealed class BattleActorDefinition
{
    private static readonly IReadOnlyDictionary<string, int> EmptyResourceMap =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private static readonly IReadOnlyList<string> EmptyTypeList = System.Array.Empty<string>();

    public BattleActorDefinition(
        string actorId,
        string displayName,
        CombatFaction faction,
        int maxHp,
        int atk,
        int def,
        int matk,
        int mdef,
        int initiative,
        IReadOnlyList<string> skillIds,
        bool playerControlled = false,
        BattleAiPolicyDefinition? aiPolicy = null,
        IReadOnlyDictionary<string, int>? resourceMaxes = null,
        IReadOnlyDictionary<string, int>? startingResources = null,
        IReadOnlyDictionary<string, int>? resourceRefreshPerTurn = null,
        long xpReward = 0,
        IReadOnlyList<string>? defenderTypeIds = null)
    {
        ActorId = actorId;
        DisplayName = displayName;
        Faction = faction;
        MaxHp = maxHp;
        Atk = atk;
        Def = def;
        Matk = matk;
        Mdef = mdef;
        Initiative = initiative;
        SkillIds = skillIds ?? System.Array.Empty<string>();
        PlayerControlled = playerControlled;
        AiPolicy = aiPolicy;
        ResourceMaxes = resourceMaxes ?? EmptyResourceMap;
        StartingResources = startingResources ?? EmptyResourceMap;
        ResourceRefreshPerTurn = resourceRefreshPerTurn ?? EmptyResourceMap;
        XpReward = xpReward < 0 ? 0 : xpReward;
        DefenderTypeIds = defenderTypeIds ?? EmptyTypeList;
    }

    public string ActorId { get; }

    public string DisplayName { get; }

    public CombatFaction Faction { get; }

    public int MaxHp { get; }

    public int Atk { get; }

    public int Def { get; }

    public int Matk { get; }

    public int Mdef { get; }

    public int Initiative { get; }

    public IReadOnlyList<string> SkillIds { get; }

    public bool PlayerControlled { get; }

    public BattleAiPolicyDefinition? AiPolicy { get; }

    public IReadOnlyDictionary<string, int> ResourceMaxes { get; }

    public IReadOnlyDictionary<string, int> StartingResources { get; }

    public IReadOnlyDictionary<string, int> ResourceRefreshPerTurn { get; }

    public long XpReward { get; }

    /// <summary>
    /// The actor's "types" for type-chart effectiveness lookups (e.g. Pokemon's Grass/Poison
    /// dual typing). Empty list means the actor is untyped — chart lookups will always return
    /// 1× neutral against them.
    /// </summary>
    public IReadOnlyList<string> DefenderTypeIds { get; }
}
