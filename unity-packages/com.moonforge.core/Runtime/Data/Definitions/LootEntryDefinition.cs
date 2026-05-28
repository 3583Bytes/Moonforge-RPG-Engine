using System;
using System.Collections.Generic;
using Moonforge.Core.Loot;

namespace Moonforge.Core.Data.Definitions
{

    /// <summary>
    /// One contributor to a <see cref="LootTableDefinition"/>. Use the static factories
    /// to construct entries; the kind discriminator selects which payload fields are read.
    /// </summary>
    public sealed class LootEntryDefinition
    {
        private static readonly IReadOnlyList<LootConditionDefinition> EmptyConditions =
            System.Array.Empty<LootConditionDefinition>();

        private LootEntryDefinition(
            LootEntryKind kind,
            string targetId,
            int weight,
            int chancePercent,
            int minQuantity,
            int maxQuantity,
            IReadOnlyList<LootConditionDefinition>? conditions)
        {
            if (weight < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be non-negative.");
            }

            if (chancePercent < 0 || chancePercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(chancePercent), "ChancePercent must be in [0, 100].");
            }

            if (minQuantity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minQuantity), "MinQuantity must be non-negative.");
            }

            if (maxQuantity < minQuantity)
            {
                throw new ArgumentOutOfRangeException(nameof(maxQuantity), "MaxQuantity must be >= MinQuantity.");
            }

            Kind = kind;
            TargetId = targetId;
            Weight = weight;
            ChancePercent = chancePercent;
            MinQuantity = minQuantity;
            MaxQuantity = maxQuantity;
            Conditions = conditions ?? EmptyConditions;
        }

        public static LootEntryDefinition Item(
            string itemId,
            int weight = 1,
            int chancePercent = 100,
            int minQuantity = 1,
            int maxQuantity = 1,
            IReadOnlyList<LootConditionDefinition>? conditions = null)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("Item ID is required.", nameof(itemId));
            }

            return new LootEntryDefinition(LootEntryKind.Item, itemId, weight, chancePercent, minQuantity, maxQuantity, conditions);
        }

        public static LootEntryDefinition Currency(
            string currencyId,
            int weight = 1,
            int chancePercent = 100,
            int minQuantity = 1,
            int maxQuantity = 1,
            IReadOnlyList<LootConditionDefinition>? conditions = null)
        {
            if (string.IsNullOrWhiteSpace(currencyId))
            {
                throw new ArgumentException("Currency ID is required.", nameof(currencyId));
            }

            return new LootEntryDefinition(LootEntryKind.Currency, currencyId, weight, chancePercent, minQuantity, maxQuantity, conditions);
        }

        public static LootEntryDefinition NestedTable(
            string tableId,
            int weight = 1,
            int chancePercent = 100,
            IReadOnlyList<LootConditionDefinition>? conditions = null)
        {
            if (string.IsNullOrWhiteSpace(tableId))
            {
                throw new ArgumentException("Nested table ID is required.", nameof(tableId));
            }

            return new LootEntryDefinition(LootEntryKind.NestedTable, tableId, weight, chancePercent, minQuantity: 1, maxQuantity: 1, conditions);
        }

        public LootEntryKind Kind { get; }

        /// <summary>Item ID, currency ID, or nested table ID — interpretation depends on <see cref="Kind"/>.</summary>
        public string TargetId { get; }

        /// <summary>Relative weight used by <see cref="LootRollMode.PickOne"/>. Zero excludes the entry.</summary>
        public int Weight { get; }

        /// <summary>Independent drop chance (0-100) used by <see cref="LootRollMode.RollEach"/>.</summary>
        public int ChancePercent { get; }

        public int MinQuantity { get; }

        public int MaxQuantity { get; }

        public IReadOnlyList<LootConditionDefinition> Conditions { get; }
    }
}
