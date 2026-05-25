using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Stats.Commands;

public sealed class RemoveStatModifiersCommand : ICommand
{
    public RemoveStatModifiersCommand(string actorId, string sourceKind, string sourceId)
    {
        ActorId = actorId;
        SourceKind = sourceKind;
        SourceId = sourceId;
    }

    public string ActorId { get; }

    public string SourceKind { get; }

    public string SourceId { get; }
}
