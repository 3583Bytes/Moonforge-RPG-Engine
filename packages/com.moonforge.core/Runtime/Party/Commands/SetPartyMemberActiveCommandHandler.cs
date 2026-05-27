using Moonforge.Core.Party.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Party.Commands
{

    public sealed class SetPartyMemberActiveCommandHandler : ICommandHandler<SetPartyMemberActiveCommand>
    {
        public DomainResult Handle(GameState gameState, SetPartyMemberActiveCommand command, CommandContext context)
        {
            if (!gameState.PartyState.TryGet(command.ActorId, out PartyMember member))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Actor '{command.ActorId}' is not in the party."));
            }

            bool previous = member.IsActive;
            if (!gameState.PartyState.TrySetActive(command.ActorId, command.Active, out string? error))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, error ?? "Unable to change active state."));
            }

            if (previous != command.Active)
            {
                context.EventSink.Publish(new PartyMemberActiveChangedEvent(command.ActorId, command.Active));
            }

            return DomainResult.Success();
        }
    }
}
