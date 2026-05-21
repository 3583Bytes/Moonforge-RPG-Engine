using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Combat.Commands;

public sealed class AttemptCaptureCommandHandler : ICommandHandler<AttemptCaptureCommand>
{
    public DomainResult Handle(GameState gameState, AttemptCaptureCommand command, CommandContext context)
    {
        DomainResult result = BattleRuntime.Instance.ResolveCapture(gameState, command, context);
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
