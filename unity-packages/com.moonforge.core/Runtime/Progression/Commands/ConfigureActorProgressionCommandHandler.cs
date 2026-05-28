using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Progression.Commands
{

    public sealed class ConfigureActorProgressionCommandHandler : ICommandHandler<ConfigureActorProgressionCommand>
    {
        public DomainResult Handle(GameState gameState, ConfigureActorProgressionCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.ActorId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Actor ID is required."));
            }

            if (string.IsNullOrWhiteSpace(command.CurveId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Curve ID is required."));
            }

            if (!context.Definitions.TryGetExperienceCurve(command.CurveId, out ExperienceCurveDefinition curve))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Unknown experience curve '{command.CurveId}'."));
            }

            int level = command.Level < 1 ? 1 : command.Level;
            if (level > curve.MaxLevel)
            {
                level = curve.MaxLevel;
            }

            long xp = command.Xp < 0 ? 0 : command.Xp;
            gameState.ProgressionState.Set(new ActorProgression(command.ActorId, command.CurveId, level, xp));
            return DomainResult.Success();
        }
    }
}
