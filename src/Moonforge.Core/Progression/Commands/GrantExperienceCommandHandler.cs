using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Progression.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Progression.Commands;

public sealed class GrantExperienceCommandHandler : ICommandHandler<GrantExperienceCommand>
{
    public DomainResult Handle(GameState gameState, GrantExperienceCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.ActorId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Actor ID is required."));
        }

        if (command.Amount <= 0)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Experience amount must be positive."));
        }

        if (!gameState.ProgressionState.TryGet(command.ActorId, out ActorProgression progression))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.NotFound,
                $"Actor '{command.ActorId}' has no progression. Call ConfigureActorProgressionCommand first."));
        }

        if (!context.Definitions.TryGetExperienceCurve(progression.CurveId, out ExperienceCurveDefinition curve))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.NotFound,
                $"Unknown experience curve '{progression.CurveId}'."));
        }

        int previousLevel = progression.Level;
        progression.Xp = checked(progression.Xp + command.Amount);
        int newLevel = curve.ResolveLevelForXp(progression.Xp);
        if (newLevel > curve.MaxLevel)
        {
            newLevel = curve.MaxLevel;
        }

        progression.Level = newLevel;
        context.EventSink.Publish(new ExperienceGrantedEvent(command.ActorId, command.Amount, progression.Xp, progression.Level));

        for (int level = previousLevel + 1; level <= newLevel; level++)
        {
            context.EventSink.Publish(new LevelUpEvent(command.ActorId, level - 1, level));
        }

        return DomainResult.Success();
    }
}
