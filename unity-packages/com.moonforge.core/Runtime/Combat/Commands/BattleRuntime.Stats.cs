using System;
using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Progression;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Stats;

namespace Moonforge.Core.Combat.Commands
{

    // Effective-stat resolution shared by skill resolution, status ticking, and turn ordering.
    // Prefers a registered StatBlock (with the full modifier pipeline); falls back to the
    // scalar BattleActorState fields plus legacy status-modifier aggregation.
    internal sealed partial class BattleRuntime
    {
        private static int GetEffectiveStat(GameState gameState, BattleActorState actor, string statKey, int baseValue, CommandContext context)
        {
            int signed = GetEffectiveStatSigned(gameState, actor, statKey, baseValue, context);
            return signed < 0 ? 0 : signed;
        }

        private static int GetEffectiveStatSigned(GameState gameState, BattleActorState actor, string statKey, int baseValue, CommandContext context)
        {
            if (gameState.ActorStatsState.TryGet(actor.ActorId, out StatBlock block))
            {
                IReadOnlyDictionary<string, double>? extra = null;
                if (gameState.ProgressionState.TryGet(actor.ActorId, out ActorProgression progression))
                {
                    extra = new Dictionary<string, double>(StringComparer.Ordinal) { ["level"] = progression.Level };
                }

                return block.Get(statKey, context.Definitions, context.FormulaEvaluator, extra, fallbackBase: baseValue);
            }

            // Fallback path: actors with no stat block use scalar BattleActorState fields plus
            // legacy status-modifier aggregation. Mirrors the original engine behavior.
            int modifier = 0;
            foreach (ActiveStatusEffect effect in actor.ActiveStatusEffects.Values)
            {
                if (context.Definitions.TryGetStatusEffect(effect.StatusId, out StatusEffectDefinition def)
                    && def.StatModifiers.TryGetValue(statKey, out int delta))
                {
                    modifier += delta;
                }
            }

            return baseValue + modifier;
        }

        private static int LegacyScalarFor(BattleActorState actor, string statKey)
        {
            return statKey switch
            {
                "atk" => actor.Atk,
                "def" => actor.Def,
                "matk" => actor.Matk,
                "mdef" => actor.Mdef,
                "maxhp" => actor.MaxHp,
                "initiative" => actor.Initiative,
                _ => 0
            };
        }
    }
}
