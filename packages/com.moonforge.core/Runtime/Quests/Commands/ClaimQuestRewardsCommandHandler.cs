using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Quests.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Quests.Commands;

public sealed class ClaimQuestRewardsCommandHandler : ICommandHandler<ClaimQuestRewardsCommand>
{
    private readonly EconomyTransactionCommandHandler _transactionHandler = new();

    public DomainResult Handle(GameState gameState, ClaimQuestRewardsCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.QuestId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Quest ID is required."));
        }

        if (!gameState.QuestState.TryGet(command.QuestId, out QuestInstanceState quest))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Quest '{command.QuestId}' was not found in state."));
        }

        if (quest.Status == QuestStatus.Rewarded)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, $"Quest '{command.QuestId}' has already been rewarded."));
        }

        if (quest.Status != QuestStatus.Completed)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, $"Quest '{command.QuestId}' is not completed."));
        }

        if (!context.Definitions.TryGetQuest(command.QuestId, out QuestDefinition definition))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Unknown quest definition '{command.QuestId}'."));
        }

        if (definition.RewardCurrency.Count > 0 || definition.RewardInventory.Count > 0)
        {
            DomainResult transactionResult = _transactionHandler.Handle(
                gameState,
                new EconomyTransactionCommand(definition.RewardCurrency, definition.RewardInventory),
                context);
            if (!transactionResult.IsSuccess)
            {
                return transactionResult;
            }
        }

        quest.Status = QuestStatus.Rewarded;
        context.EventSink.Publish(new QuestRewardedEvent(command.QuestId, definition.RewardCurrency, definition.RewardInventory));
        return DomainResult.Success();
    }
}
