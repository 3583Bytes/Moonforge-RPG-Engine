using Moonforge.Core.Quests.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Quests.Commands
{

    public sealed class EmitQuestSignalCommandHandler : ICommandHandler<EmitQuestSignalCommand>
    {
        public DomainResult Handle(GameState gameState, EmitQuestSignalCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.TargetId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Quest signal target ID is required."));
            }

            if (command.Amount <= 0)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Quest signal amount must be positive."));
            }

            context.EventSink.Publish(new QuestSignalEvent(command.SignalType, command.TargetId, command.Amount));
            return DomainResult.Success();
        }
    }
}
