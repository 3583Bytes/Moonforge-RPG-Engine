using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Quests.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Quests.Commands;

public sealed class StartQuestCommandHandler : ICommandHandler<StartQuestCommand>
{
    public DomainResult Handle(GameState gameState, StartQuestCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.QuestId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Quest ID is required."));
        }

        if (!context.Definitions.TryGetQuest(command.QuestId, out QuestDefinition _))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Unknown quest definition '{command.QuestId}'."));
        }

        QuestInstanceState quest = gameState.QuestState.GetOrCreate(command.QuestId);
        if (quest.Status == QuestStatus.Completed)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, $"Quest '{command.QuestId}' is already completed."));
        }

        if (quest.Status == QuestStatus.Active)
        {
            return DomainResult.Success();
        }

        quest.Status = QuestStatus.Active;
        quest.ClearObjectiveProgress();
        context.EventSink.Publish(new QuestStartedEvent(command.QuestId));
        return DomainResult.Success();
    }
}
