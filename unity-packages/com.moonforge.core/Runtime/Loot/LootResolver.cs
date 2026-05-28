using System;
using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Progression;
using Moonforge.Core.Quests;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.World;

namespace Moonforge.Core.Loot
{

    /// <summary>
    /// Deterministic resolver that converts a <see cref="LootTableDefinition"/> into a list of
    /// concrete drops. All randomness flows through the provided <see cref="IRandomSource"/>.
    /// Public so game code can roll outside the command/query pipeline (e.g., for previews or
    /// custom deposit handling).
    /// </summary>
    public static class LootResolver
    {
        private const int MaxNestedDepth = 8;

        public static LootRollResult Roll(
            GameState gameState,
            IGameDefinitionCatalog definitions,
            IRandomSource rng,
            LootTableDefinition table)
        {
            List<LootDrop> items = new();
            List<LootCurrencyDrop> currencies = new();
            HashSet<string> visited = new(StringComparer.Ordinal);
            RollInto(gameState, definitions, rng, table, items, currencies, visited, depth: 0);
            if (items.Count == 0 && currencies.Count == 0)
            {
                return LootRollResult.Empty;
            }

            return new LootRollResult(items, currencies);
        }

        private static void RollInto(
            GameState gameState,
            IGameDefinitionCatalog definitions,
            IRandomSource rng,
            LootTableDefinition table,
            List<LootDrop> items,
            List<LootCurrencyDrop> currencies,
            HashSet<string> visited,
            int depth)
        {
            if (depth >= MaxNestedDepth)
            {
                return;
            }

            if (!visited.Add(table.Id))
            {
                // Cycle detected. Drop silently — the depth cap also bounds runaway recursion.
                return;
            }

            try
            {
                switch (table.RollMode)
                {
                    case LootRollMode.PickOne:
                        RollPickOne(gameState, definitions, rng, table, items, currencies, visited, depth);
                        break;
                    case LootRollMode.RollEach:
                        RollEach(gameState, definitions, rng, table, items, currencies, visited, depth);
                        break;
                }
            }
            finally
            {
                visited.Remove(table.Id);
            }
        }

        private static void RollPickOne(
            GameState gameState,
            IGameDefinitionCatalog definitions,
            IRandomSource rng,
            LootTableDefinition table,
            List<LootDrop> items,
            List<LootCurrencyDrop> currencies,
            HashSet<string> visited,
            int depth)
        {
            int totalWeight = 0;
            for (int i = 0; i < table.Entries.Count; i++)
            {
                LootEntryDefinition entry = table.Entries[i];
                if (entry.Weight <= 0)
                {
                    continue;
                }

                if (!ConditionsPass(gameState, entry))
                {
                    continue;
                }

                totalWeight += entry.Weight;
            }

            if (totalWeight <= 0)
            {
                return;
            }

            int roll = rng.NextInt(totalWeight);
            int cumulative = 0;
            for (int i = 0; i < table.Entries.Count; i++)
            {
                LootEntryDefinition entry = table.Entries[i];
                if (entry.Weight <= 0)
                {
                    continue;
                }

                if (!ConditionsPass(gameState, entry))
                {
                    continue;
                }

                cumulative += entry.Weight;
                if (roll < cumulative)
                {
                    EmitEntry(gameState, definitions, rng, entry, items, currencies, visited, depth);
                    return;
                }
            }
        }

        private static void RollEach(
            GameState gameState,
            IGameDefinitionCatalog definitions,
            IRandomSource rng,
            LootTableDefinition table,
            List<LootDrop> items,
            List<LootCurrencyDrop> currencies,
            HashSet<string> visited,
            int depth)
        {
            for (int i = 0; i < table.Entries.Count; i++)
            {
                LootEntryDefinition entry = table.Entries[i];
                if (!ConditionsPass(gameState, entry))
                {
                    continue;
                }

                if (entry.ChancePercent <= 0)
                {
                    continue;
                }

                // Always consume one RNG step per entry so adding a never-drops entry to the table
                // doesn't shift the RNG stream of subsequent entries.
                int roll = rng.NextInt(100);
                if (roll >= entry.ChancePercent)
                {
                    continue;
                }

                EmitEntry(gameState, definitions, rng, entry, items, currencies, visited, depth);
            }
        }

        private static void EmitEntry(
            GameState gameState,
            IGameDefinitionCatalog definitions,
            IRandomSource rng,
            LootEntryDefinition entry,
            List<LootDrop> items,
            List<LootCurrencyDrop> currencies,
            HashSet<string> visited,
            int depth)
        {
            switch (entry.Kind)
            {
                case LootEntryKind.Item:
                {
                    int qty = RollQuantity(rng, entry.MinQuantity, entry.MaxQuantity);
                    if (qty > 0)
                    {
                        items.Add(new LootDrop(entry.TargetId, qty));
                    }
                    break;
                }
                case LootEntryKind.Currency:
                {
                    int qty = RollQuantity(rng, entry.MinQuantity, entry.MaxQuantity);
                    if (qty > 0)
                    {
                        currencies.Add(new LootCurrencyDrop(entry.TargetId, qty));
                    }
                    break;
                }
                case LootEntryKind.NestedTable:
                {
                    if (definitions.TryGetLootTable(entry.TargetId, out LootTableDefinition nested))
                    {
                        RollInto(gameState, definitions, rng, nested, items, currencies, visited, depth + 1);
                    }
                    break;
                }
            }
        }

        private static int RollQuantity(IRandomSource rng, int min, int max)
        {
            if (min == max)
            {
                return min;
            }

            // NextInt(n) is [0, n). Range size is (max - min + 1).
            return min + rng.NextInt(max - min + 1);
        }

        private static bool ConditionsPass(GameState gameState, LootEntryDefinition entry)
        {
            IReadOnlyList<LootConditionDefinition> conditions = entry.Conditions;
            for (int i = 0; i < conditions.Count; i++)
            {
                if (!EvaluateCondition(gameState, conditions[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool EvaluateCondition(GameState gameState, LootConditionDefinition condition)
        {
            switch (condition.ConditionType)
            {
                case LootConditionType.WorldBoolEquals:
                {
                    if (!gameState.WorldState.TryGet(condition.Key, out WorldVariableValue value))
                    {
                        return condition.BoolValue == false;
                    }

                    return value.TryGetBool(out bool actual) && actual == condition.BoolValue;
                }
                case LootConditionType.WorldIntAtLeast:
                {
                    if (!gameState.WorldState.TryGet(condition.Key, out WorldVariableValue value))
                    {
                        return condition.IntValue <= 0;
                    }

                    return value.TryGetInt(out int actual) && actual >= condition.IntValue;
                }
                case LootConditionType.QuestStatusEquals:
                {
                    if (!gameState.QuestState.TryGet(condition.Key, out QuestInstanceState quest))
                    {
                        return condition.QuestStatus == QuestStatus.NotStarted;
                    }

                    return quest.Status == condition.QuestStatus;
                }
                case LootConditionType.ActorLevelAtLeast:
                {
                    if (!gameState.ProgressionState.TryGet(condition.Key, out ActorProgression progression))
                    {
                        return condition.IntValue <= 1;
                    }

                    return progression.Level >= condition.IntValue;
                }
                default:
                    return false;
            }
        }
    }
}
