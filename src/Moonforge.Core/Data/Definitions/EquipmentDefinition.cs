using System;
using System.Collections.Generic;

namespace Moonforge.Core.Data.Definitions;

public sealed class EquipmentDefinition
{
    private static readonly IReadOnlyDictionary<string, int> EmptyBonuses =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public EquipmentDefinition(
        string itemId,
        string slotId,
        IReadOnlyDictionary<string, int>? statBonuses = null,
        string? displayName = null,
        string? description = null)
    {
        ItemId = itemId;
        SlotId = slotId;
        StatBonuses = statBonuses ?? EmptyBonuses;
        DisplayName = displayName;
        Description = description;
    }

    public string ItemId { get; }

    public string SlotId { get; }

    public IReadOnlyDictionary<string, int> StatBonuses { get; }

    public string? DisplayName { get; }

    public string? Description { get; }
}
