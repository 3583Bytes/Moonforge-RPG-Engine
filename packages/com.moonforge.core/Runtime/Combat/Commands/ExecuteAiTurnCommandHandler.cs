using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Combat.Commands
{

    public sealed class ExecuteAiTurnCommandHandler : ICommandHandler<ExecuteAiTurnCommand>
    {
        public DomainResult Handle(GameState gameState, ExecuteAiTurnCommand command, CommandContext context)
        {
            DomainResult result = BattleRuntime.Instance.ResolveAiTurn(gameState, context);
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
