using Moonforge.Core.Exploration;

namespace Moonforge.Core.Interactables;

/// <summary>
/// Mutable per-instance state for an interactable placed on the map.
/// </summary>
public sealed class InteractableInstance
{
    public InteractableInstance(
        string instanceId,
        string definitionId,
        GridPosition position,
        InteractableStatus status = InteractableStatus.Default,
        int usesRemaining = 1,
        bool locked = false)
    {
        InstanceId = instanceId;
        DefinitionId = definitionId;
        Position = position;
        Status = status;
        UsesRemaining = usesRemaining;
        Locked = locked;
    }

    public string InstanceId { get; }

    public string DefinitionId { get; }

    public GridPosition Position { get; set; }

    public InteractableStatus Status { get; set; }

    /// <summary>Decremented on each successful interact. <c>-1</c> denotes unlimited uses.</summary>
    public int UsesRemaining { get; set; }

    public bool Locked { get; set; }

    public InteractableInstance Clone()
    {
        return new InteractableInstance(InstanceId, DefinitionId, Position, Status, UsesRemaining, Locked);
    }
}
