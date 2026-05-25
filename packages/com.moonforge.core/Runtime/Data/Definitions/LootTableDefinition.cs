using System;
using System.Collections.Generic;
using Moonforge.Core.Loot;

namespace Moonforge.Core.Data.Definitions;

public sealed class LootTableDefinition
{
    public LootTableDefinition(
        string id,
        LootRollMode rollMode,
        IReadOnlyList<LootEntryDefinition> entries,
        string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Loot table ID is required.", nameof(id));
        }

        Id = id;
        RollMode = rollMode;
        Entries = entries ?? System.Array.Empty<LootEntryDefinition>();
        DisplayName = displayName;
    }

    public string Id { get; }

    public LootRollMode RollMode { get; }

    public IReadOnlyList<LootEntryDefinition> Entries { get; }

    public string? DisplayName { get; }
}
