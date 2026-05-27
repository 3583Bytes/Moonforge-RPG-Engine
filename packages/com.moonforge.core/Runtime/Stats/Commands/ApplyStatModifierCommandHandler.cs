using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Stats.Events;

namespace Moonforge.Core.Stats.Commands
{

    public sealed class ApplyStatModifierCommandHandler : ICommandHandler<ApplyStatModifierCommand>
    {
        public DomainResult Handle(GameState gameState, ApplyStatModifierCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.ActorId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Actor ID is required."));
            }

            if (command.Modifier is null)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Modifier is required."));
            }

            StatBlock block = gameState.ActorStatsState.GetOrCreate(command.ActorId);
            block.AddModifier(command.Modifier);

            context.EventSink.Publish(new StatModifierAppliedEvent(
                command.ActorId,
                command.Modifier.StatId,
                command.Modifier.Bucket,
                command.Modifier.SourceKind,
                command.Modifier.SourceId,
                command.Modifier.Value));
            return DomainResult.Success();
        }
    }
}
