using Moonforge.Core.Quests.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Quests.Commands;

public sealed class AbandonQuestCommandHandler : ICommandHandler<AbandonQuestCommand>
{
    public DomainResult Handle(GameState gameState, AbandonQuestCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.QuestId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Quest ID is required."));
        }

        if (!gameState.QuestState.TryGet(command.QuestId, out QuestInstanceState quest))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Quest '{command.QuestId}' was not found in state."));
        }

        if (quest.Status != QuestStatus.Active)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, $"Quest '{command.QuestId}' is not active."));
        }

        quest.Status = QuestStatus.Abandoned;
        context.EventSink.Publish(new QuestAbandonedEvent(command.QuestId));
        return DomainResult.Success();
    }
}
