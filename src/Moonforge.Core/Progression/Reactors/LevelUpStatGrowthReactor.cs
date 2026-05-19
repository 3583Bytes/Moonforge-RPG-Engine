using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Progression.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Stats;

namespace Moonforge.Core.Progression.Reactors;

/// <summary>
/// Watches <see cref="LevelUpEvent"/>s and applies the per-level stat gains declared on the
/// actor's <see cref="ExperienceCurveDefinition.StatGainsPerLevel"/>. Each gain is added as
/// a Flat <see cref="StatModifier"/> tagged with sourceKind <c>"progression"</c> and a
/// per-level sourceId, so a future migration or reset can identify and remove level-derived
/// modifiers cleanly.
/// </summary>
public sealed class LevelUpStatGrowthReactor : IDomainEventReactor
{
    public const string SourceKind = "progression";

    public DomainResult React(GameState gameState, DomainEvent domainEvent, CommandContext context)
    {
        if (domainEvent is not LevelUpEvent levelUp)
        {
            return DomainResult.Success();
        }

        if (!gameState.ProgressionState.TryGet(levelUp.ActorId, out ActorProgression progression))
        {
            return DomainResult.Success();
        }

        if (!context.Definitions.TryGetExperienceCurve(progression.CurveId, out ExperienceCurveDefinition curve))
        {
            return DomainResult.Success();
        }

        if (curve.StatGainsPerLevel.Count == 0)
        {
            return DomainResult.Success();
        }

        StatBlock block = gameState.ActorStatsState.GetOrCreate(levelUp.ActorId);
        string sourceId = $"{levelUp.ActorId}.level.{levelUp.ToLevel}";

        foreach (KeyValuePair<string, int> gain in curve.StatGainsPerLevel)
        {
            if (gain.Value == 0)
            {
                continue;
            }

            block.AddModifier(new StatModifier(
                statId: gain.Key,
                bucket: StatModifierBucket.Flat,
                value: gain.Value,
                sourceKind: SourceKind,
                sourceId: sourceId));
        }

        return DomainResult.Success();
    }
}
