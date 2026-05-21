using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Evolution.Events;
using Moonforge.Core.Progression;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Evolution.Commands;

public sealed class TriggerEvolutionCommandHandler : ICommandHandler<TriggerEvolutionCommand>
{
    public DomainResult Handle(GameState gameState, TriggerEvolutionCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.ActorId) || string.IsNullOrWhiteSpace(command.EvolutionId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Actor id and evolution id are required."));
        }

        if (!gameState.EvolutionState.IsEligible(command.ActorId, command.EvolutionId))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.UnsupportedOperation,
                $"Actor '{command.ActorId}' is not eligible for evolution '{command.EvolutionId}'."));
        }

        if (!context.Definitions.TryGetEvolution(command.EvolutionId, out EvolutionDefinition definition))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.NotFound,
                $"Unknown evolution '{command.EvolutionId}'."));
        }

        if (definition.Trigger == EvolutionTrigger.LevelUp)
        {
            if (!gameState.ProgressionState.TryGet(command.ActorId, out ActorProgression progression))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.NotFound,
                    $"Actor '{command.ActorId}' has no progression — cannot evaluate LevelUp trigger."));
            }

            if (progression.Level < definition.RequiredLevel)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.Conflict,
                    $"Actor '{command.ActorId}' is level {progression.Level}; evolution '{definition.Id}' requires {definition.RequiredLevel}."));
            }
        }

        context.EventSink.Publish(new EvolutionTriggeredEvent(
            command.ActorId,
            definition.Id,
            definition.Trigger,
            definition.EvolvedSpeciesId));
        return DomainResult.Success();
    }
}
