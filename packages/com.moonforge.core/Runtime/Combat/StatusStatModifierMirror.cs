using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Stats;

namespace Moonforge.Core.Combat
{

    /// <summary>
    /// Translates <see cref="StatusEffectDefinition.StatModifiers"/> entries into
    /// <see cref="StatModifier"/> contributions on the actor's <see cref="StatBlock"/>.
    /// Status modifiers are stored as Flat with <c>SourceKind="status"</c> so they can be
    /// removed by status ID when the effect expires or is dispelled.
    /// </summary>
    internal static class StatusStatModifierMirror
    {
        public const string Kind = "status";

        public static void Apply(GameState gameState, string actorId, StatusEffectDefinition definition)
        {
            if (definition.StatModifiers.Count == 0)
            {
                return;
            }

            StatBlock block = gameState.ActorStatsState.GetOrCreate(actorId);
            foreach (KeyValuePair<string, int> pair in definition.StatModifiers)
            {
                block.AddModifier(new StatModifier(
                    pair.Key,
                    StatModifierBucket.Flat,
                    pair.Value,
                    Kind,
                    definition.Id));
            }
        }

        public static void Remove(GameState gameState, string actorId, string statusId)
        {
            if (!gameState.ActorStatsState.TryGet(actorId, out StatBlock block))
            {
                return;
            }

            block.RemoveModifiersBySource(Kind, statusId);
        }
    }
}
