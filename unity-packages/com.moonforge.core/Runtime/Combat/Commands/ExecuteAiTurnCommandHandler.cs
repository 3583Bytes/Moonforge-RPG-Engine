using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Loot.Commands;
using Moonforge.Core.Progression.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Combat.Commands
{

    public sealed class ExecuteAiTurnCommandHandler : ICommandHandler<ExecuteAiTurnCommand>
    {
        private readonly BattleRuntime _runtime;

        /// <summary>
        /// Pass the handlers you registered for the reward commands so battle-end rewards
        /// behave identically to directly dispatched commands. All default to the built-ins.
        /// </summary>
        public ExecuteAiTurnCommandHandler(
            ICommandHandler<EconomyTransactionCommand>? rewardTransactionHandler = null,
            ICommandHandler<GrantExperienceCommand>? experienceHandler = null,
            ICommandHandler<RollAndGrantLootCommand>? lootHandler = null)
        {
            _runtime = new BattleRuntime(rewardTransactionHandler, experienceHandler, lootHandler);
        }

        public DomainResult Handle(GameState gameState, ExecuteAiTurnCommand command, CommandContext context)
        {
            DomainResult result = _runtime.ResolveAiTurn(gameState, context);
            if (!result.IsSuccess)
            {
                return result;
            }

            if (gameState.ActiveBattle is not null && gameState.ActiveBattle.Status != BattleStatus.Active)
            {
                gameState.ActiveBattle = null;
            }

            return DomainResult.Success();
        }
    }
}
