using Moonforge.Core.Bestiary.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Bestiary.Commands
{

    public sealed class MarkSpeciesObservedCommandHandler : ICommandHandler<MarkSpeciesObservedCommand>
    {
        public DomainResult Handle(GameState gameState, MarkSpeciesObservedCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.SpeciesId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Species id is required."));
            }

            if (!command.Encountered && !command.Captured)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "At least one of Encountered or Captured must be true."));
            }

            long minutes = context.Clock.CurrentSimulationMinutes;

            if (command.Encountered)
            {
                bool first = gameState.BestiaryState.RecordEncounter(command.SpeciesId, minutes);
                if (first)
                {
                    context.EventSink.Publish(new SpeciesFirstEncounteredEvent(command.SpeciesId, minutes));
                }
            }

            if (command.Captured)
            {
                bool first = gameState.BestiaryState.RecordCapture(command.SpeciesId, minutes);
                if (first)
                {
                    context.EventSink.Publish(new SpeciesFirstCapturedEvent(command.SpeciesId, minutes));
                }
            }

            return DomainResult.Success();
        }
    }
}
