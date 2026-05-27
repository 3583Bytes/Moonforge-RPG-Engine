using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Stats.Events;

namespace Moonforge.Core.Stats.Commands
{

    public sealed class SetStatBaseCommandHandler : ICommandHandler<SetStatBaseCommand>
    {
        public DomainResult Handle(GameState gameState, SetStatBaseCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.ActorId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Actor ID is required."));
            }

            if (string.IsNullOrWhiteSpace(command.StatId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Stat ID is required."));
            }

            StatBlock block = gameState.ActorStatsState.GetOrCreate(command.ActorId);
            int previous = block.TryGetBase(command.StatId, out int existing) ? existing : 0;
            block.SetBase(command.StatId, command.Value);

            context.EventSink.Publish(new StatBaseChangedEvent(command.ActorId, command.StatId, previous, command.Value));
            return DomainResult.Success();
        }
    }
}
