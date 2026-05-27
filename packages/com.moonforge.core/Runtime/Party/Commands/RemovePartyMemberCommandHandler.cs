using Moonforge.Core.Party.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Party.Commands
{

    public sealed class RemovePartyMemberCommandHandler : ICommandHandler<RemovePartyMemberCommand>
    {
        public DomainResult Handle(GameState gameState, RemovePartyMemberCommand command, CommandContext context)
        {
            if (!gameState.PartyState.TryRemove(command.ActorId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Actor '{command.ActorId}' is not in the party."));
            }

            context.EventSink.Publish(new PartyMemberRemovedEvent(command.ActorId));
            return DomainResult.Success();
        }
    }
}
