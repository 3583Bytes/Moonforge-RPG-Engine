using System;
using System.Collections.Generic;

namespace Moonforge.Core.Combat;

public sealed class BattleActorState
{
    public BattleActorState(BattleActorDefinition definition)
    {
        ActorId = definition.ActorId;
        DisplayName = definition.DisplayName;
        Faction = definition.Faction;
        MaxHp = definition.MaxHp;
        Hp = definition.MaxHp;
        Atk = definition.Atk;
        Def = definition.Def;
        Matk = definition.Matk;
        Mdef = definition.Mdef;
        Initiative = definition.Initiative;
        PlayerControlled = definition.PlayerControlled;
        AiPolicy = definition.AiPolicy;
        XpReward = definition.XpReward;
        SkillIds = new List<string>(definition.SkillIds);
        DefenderTypeIds = new List<string>(definition.DefenderTypeIds);
        Cooldowns = new Dictionary<string, int>(StringComparer.Ordinal);
        ResourceMaxes = new Dictionary<string, int>(StringComparer.Ordinal);
        Resources = new Dictionary<string, int>(StringComparer.Ordinal);
        ResourceRefreshPerTurn = new Dictionary<string, int>(StringComparer.Ordinal);
        ActiveStatusEffects = new Dictionary<string, ActiveStatusEffect>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, int> pair in definition.ResourceMaxes)
        {
            ResourceMaxes[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<string, int> pair in definition.ResourceRefreshPerTurn)
        {
            ResourceRefreshPerTurn[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<string, int> pair in definition.ResourceMaxes)
        {
            int starting = definition.StartingResources.TryGetValue(pair.Key, out int startValue) ? startValue : pair.Value;
            Resources[pair.Key] = ClampToMax(starting, pair.Value);
        }

        foreach (KeyValuePair<string, int> pair in definition.StartingResources)
        {
            if (!Resources.ContainsKey(pair.Key))
            {
                int max = definition.ResourceMaxes.TryGetValue(pair.Key, out int maxValue) ? maxValue : int.MaxValue;
                Resources[pair.Key] = ClampToMax(pair.Value, max);
            }
        }
    }

    public string ActorId { get; set; }

    public string DisplayName { get; set; }

    public CombatFaction Faction { get; set; }

    public int MaxHp { get; set; }

    public int Hp { get; set; }

    public int Atk { get; set; }

    public int Def { get; set; }

    public int Matk { get; set; }

    public int Mdef { get; set; }

    public int Initiative { get; set; }

    public bool PlayerControlled { get; set; }

    public BattleAiPolicyDefinition? AiPolicy { get; set; }

    public long XpReward { get; set; }

    public List<string> SkillIds { get; }

    public List<string> DefenderTypeIds { get; }

    public Dictionary<string, int> Cooldowns { get; }

    public Dictionary<string, int> Resources { get; }

    public Dictionary<string, int> ResourceMaxes { get; }

    public Dictionary<string, int> ResourceRefreshPerTurn { get; }

    public Dictionary<string, ActiveStatusEffect> ActiveStatusEffects { get; }

    public bool IsDowned => Hp <= 0;

    public BattleActorState Clone()
    {
        BattleActorDefinition definition = new(
            ActorId,
            DisplayName,
            Faction,
            MaxHp,
            Atk,
            Def,
            Matk,
            Mdef,
            Initiative,
            new List<string>(SkillIds),
            PlayerControlled,
            AiPolicy,
            new Dictionary<string, int>(ResourceMaxes, StringComparer.Ordinal),
            startingResources: null,
            new Dictionary<string, int>(ResourceRefreshPerTurn, StringComparer.Ordinal),
            xpReward: XpReward,
            defenderTypeIds: new List<string>(DefenderTypeIds));
        BattleActorState clone = new(definition)
        {
            Hp = Hp
        };

        clone.Resources.Clear();
        foreach (KeyValuePair<string, int> pair in Resources)
        {
            clone.Resources[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<string, int> pair in Cooldowns)
        {
            clone.Cooldowns[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<string, ActiveStatusEffect> pair in ActiveStatusEffects)
        {
            clone.ActiveStatusEffects[pair.Key] = pair.Value.Clone();
        }

        return clone;
    }

    private static int ClampToMax(int value, int max)
    {
        if (value < 0)
        {
            return 0;
        }

        return value > max ? max : value;
    }
}
