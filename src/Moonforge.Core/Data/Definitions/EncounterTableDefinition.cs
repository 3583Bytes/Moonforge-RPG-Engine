using System;
using System.Collections.Generic;
using Moonforge.Core.Encounters;

namespace Moonforge.Core.Data.Definitions;

public sealed class EncounterTableDefinition
{
    public EncounterTableDefinition(
        string id,
        EncounterRollMode rollMode,
        IReadOnlyList<EncounterEntryDefinition> entries,
        string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Encounter table ID is required.", nameof(id));
        }

        Id = id;
        RollMode = rollMode;
        Entries = entries ?? System.Array.Empty<EncounterEntryDefinition>();
        DisplayName = displayName;
    }

    public string Id { get; }

    public EncounterRollMode RollMode { get; }

    public IReadOnlyList<EncounterEntryDefinition> Entries { get; }

    public string? DisplayName { get; }
}
