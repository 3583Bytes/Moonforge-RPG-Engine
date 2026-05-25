using Moonforge.Core.Combat.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Combat.Commands;

public sealed class RemoveStatusEffectCommandHandler : ICommandHandler<RemoveStatusEffectCommand>
{
    public DomainResult Handle(GameState gameState, RemoveStatusEffectCommand command, CommandContext context)
    {
        if (gameState.ActiveBattle is null)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, "No active battle."));
        }

        if (!gameState.ActiveBattle.TryGetActor(command.ActorId, out BattleActorState actor))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Actor '{command.ActorId}' not found."));
        }

        if (!actor.ActiveStatusEffects.Remove(command.StatusId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Status '{command.StatusId}' is not active on '{command.ActorId}'."));
        }

        StatusStatModifierMirror.Remove(gameState, command.ActorId, command.StatusId);
        context.EventSink.Publish(new StatusExpiredEvent(command.ActorId, command.StatusId));
        return DomainResult.Success();
    }
}
