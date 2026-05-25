using Moonforge.Core.Combat.Events;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Stats;

namespace Moonforge.Core.Combat.Commands;

public sealed class ApplyStatusEffectCommandHandler : ICommandHandler<ApplyStatusEffectCommand>
{
    public DomainResult Handle(GameState gameState, ApplyStatusEffectCommand command, CommandContext context)
    {
        if (gameState.ActiveBattle is null)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, "No active battle."));
        }

        if (!gameState.ActiveBattle.TryGetActor(command.ActorId, out BattleActorState actor))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Actor '{command.ActorId}' not found."));
        }

        if (!context.Definitions.TryGetStatusEffect(command.StatusId, out StatusEffectDefinition definition))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Unknown status effect '{command.StatusId}'."));
        }

        int duration = command.DurationOverride ?? definition.DurationTurns;
        if (duration <= 0)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, $"Status effect '{command.StatusId}' has non-positive duration."));
        }

        if (actor.ActiveStatusEffects.TryGetValue(command.StatusId, out ActiveStatusEffect existing))
        {
            if (definition.StackPolicy == StatusStackPolicy.IgnoreIfPresent)
            {
                return DomainResult.Success();
            }

            existing.RemainingTurns = duration;
        }
        else
        {
            actor.ActiveStatusEffects[command.StatusId] = new ActiveStatusEffect(command.StatusId, duration, command.SourceActorId);
            StatusStatModifierMirror.Apply(gameState, command.ActorId, definition);
        }

        context.EventSink.Publish(new StatusAppliedEvent(command.ActorId, command.StatusId, duration, command.SourceActorId));
        return DomainResult.Success();
    }
}
