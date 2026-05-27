using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Random;

namespace Moonforge.Core.Encounters
{

    /// <summary>
    /// Deterministic resolver that converts an <see cref="EncounterTableDefinition"/> into a
    /// list of <see cref="EncounterSpawn"/> entries. All randomness flows through the caller's
    /// <see cref="IRandomSource"/>. Public so game code can roll outside the query pipeline.
    /// </summary>
    public static class EncounterResolver
    {
        public static EncounterRollResult Roll(IRandomSource rng, EncounterTableDefinition table)
        {
            switch (table.RollMode)
            {
                case EncounterRollMode.PickOne:
                    return RollPickOne(rng, table);
                case EncounterRollMode.RollEach:
                    return RollEach(rng, table);
                default:
                    return EncounterRollResult.Empty;
            }
        }

        private static EncounterRollResult RollPickOne(IRandomSource rng, EncounterTableDefinition table)
        {
            int totalWeight = 0;
            for (int i = 0; i < table.Entries.Count; i++)
            {
                totalWeight += table.Entries[i].Weight;
            }

            if (totalWeight <= 0)
            {
                return EncounterRollResult.Empty;
            }

            int roll = rng.NextInt(totalWeight);
            int cumulative = 0;
            for (int i = 0; i < table.Entries.Count; i++)
            {
                EncounterEntryDefinition entry = table.Entries[i];
                if (entry.Weight <= 0)
                {
                    continue;
                }

                cumulative += entry.Weight;
                if (roll < cumulative)
                {
                    int count = RollCount(rng, entry.MinCount, entry.MaxCount);
                    if (count <= 0)
                    {
                        return EncounterRollResult.Empty;
                    }

                    return new EncounterRollResult(new[] { new EncounterSpawn(entry.ActorId, count) });
                }
            }

            return EncounterRollResult.Empty;
        }

        private static EncounterRollResult RollEach(IRandomSource rng, EncounterTableDefinition table)
        {
            List<EncounterSpawn> spawns = new();
            for (int i = 0; i < table.Entries.Count; i++)
            {
                EncounterEntryDefinition entry = table.Entries[i];
                if (entry.ChancePercent <= 0)
                {
                    continue;
                }

                // Always consume one RNG step per entry so adding/removing a never-spawns entry
                // does not shift the RNG stream for the rest of the table.
                int roll = rng.NextInt(100);
                if (roll >= entry.ChancePercent)
                {
                    continue;
                }

                int count = RollCount(rng, entry.MinCount, entry.MaxCount);
                if (count > 0)
                {
                    spawns.Add(new EncounterSpawn(entry.ActorId, count));
                }
            }

            if (spawns.Count == 0)
            {
                return EncounterRollResult.Empty;
            }

            return new EncounterRollResult(spawns);
        }

        private static int RollCount(IRandomSource rng, int min, int max)
        {
            if (min == max)
            {
                return min;
            }

            return min + rng.NextInt(max - min + 1);
        }
    }
}
