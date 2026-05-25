namespace Moonforge.Core.Data.Definitions;

public sealed class EquipmentSlotDefinition
{
    public EquipmentSlotDefinition(string id, string? displayName = null)
    {
        Id = id;
        DisplayName = displayName;
    }

    public string Id { get; }

    public string? DisplayName { get; }
}
