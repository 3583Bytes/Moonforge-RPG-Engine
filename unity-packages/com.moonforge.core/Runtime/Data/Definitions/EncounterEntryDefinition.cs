using System;

namespace Moonforge.Core.Data.Definitions
{

    /// <summary>
    /// One contributor to an <see cref="EncounterTableDefinition"/>. Names an actor / enemy id
    /// the engine will hand back when the entry is rolled. Quantity range supports packs.
    /// </summary>
    public sealed class EncounterEntryDefinition
    {
        public EncounterEntryDefinition(
            string actorId,
            int weight = 1,
            int chancePercent = 100,
            int minCount = 1,
            int maxCount = 1)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException("Actor ID is required.", nameof(actorId));
            }

            if (weight < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be non-negative.");
            }

            if (chancePercent < 0 || chancePercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(chancePercent), "ChancePercent must be in [0, 100].");
            }

            if (minCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minCount), "MinCount must be non-negative.");
            }

            if (maxCount < minCount)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), "MaxCount must be >= MinCount.");
            }

            ActorId = actorId;
            Weight = weight;
            ChancePercent = chancePercent;
            MinCount = minCount;
            MaxCount = maxCount;
        }

        public string ActorId { get; }

        public int Weight { get; }

        public int ChancePercent { get; }

        public int MinCount { get; }

        public int MaxCount { get; }
    }
}
