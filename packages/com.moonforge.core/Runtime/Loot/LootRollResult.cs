using System.Collections.Generic;

namespace Moonforge.Core.Loot
{

    public sealed class LootRollResult
    {
        public LootRollResult(IReadOnlyList<LootDrop> items, IReadOnlyList<LootCurrencyDrop> currencies)
        {
            Items = items;
            Currencies = currencies;
        }

        public IReadOnlyList<LootDrop> Items { get; }

        public IReadOnlyList<LootCurrencyDrop> Currencies { get; }

        public bool IsEmpty => Items.Count == 0 && Currencies.Count == 0;

        public static readonly LootRollResult Empty = new(System.Array.Empty<LootDrop>(), System.Array.Empty<LootCurrencyDrop>());
    }
}
