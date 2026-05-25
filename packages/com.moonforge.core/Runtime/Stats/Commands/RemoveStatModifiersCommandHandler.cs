using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Stats.Events;

namespace Moonforge.Core.Stats.Commands;

public sealed class RemoveStatModifiersCommandHandler : ICommandHandler<RemoveStatModifiersCommand>
{
    public DomainResult Handle(GameState gameState, RemoveStatModifiersCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.ActorId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Actor ID is required."));
        }

        if (string.IsNullOrWhiteSpace(command.SourceKind))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Source kind is required."));
        }

        if (string.IsNullOrWhiteSpace(command.SourceId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Source ID is required."));
        }

        if (!gameState.ActorStatsState.TryGet(command.ActorId, out StatBlock block))
        {
            return DomainResult.Success();
        }

        int removed = block.RemoveModifiersBySource(command.SourceKind, command.SourceId);
        if (removed > 0)
        {
            context.EventSink.Publish(new StatModifiersRemovedEvent(
                command.ActorId,
                command.SourceKind,
                command.SourceId,
                removed));
        }

        return DomainResult.Success();
    }
}
