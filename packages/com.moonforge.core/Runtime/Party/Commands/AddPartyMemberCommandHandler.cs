using Moonforge.Core.Party.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Party.Commands;

public sealed class AddPartyMemberCommandHandler : ICommandHandler<AddPartyMemberCommand>
{
    public DomainResult Handle(GameState gameState, AddPartyMemberCommand command, CommandContext context)
    {
        if (!gameState.PartyState.TryAdd(command.ActorId, command.Active, out string? error))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, error ?? "Unable to add party member."));
        }

        context.EventSink.Publish(new PartyMemberAddedEvent(command.ActorId, command.Active));
        return DomainResult.Success();
    }
}
