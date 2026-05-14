using Moonforge.Core.Exploration;
using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Interactables.Commands;

public sealed class PlaceInteractableCommand : ICommand
{
    public PlaceInteractableCommand(string instanceId, string definitionId, GridPosition position)
    {
        InstanceId = instanceId;
        DefinitionId = definitionId;
        Position = position;
    }

    public string InstanceId { get; }

    public string DefinitionId { get; }

    public GridPosition Position { get; }
}
