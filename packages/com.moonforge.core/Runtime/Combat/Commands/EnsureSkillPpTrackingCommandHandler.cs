using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Combat.Commands;

public sealed class EnsureSkillPpTrackingCommandHandler : ICommandHandler<EnsureSkillPpTrackingCommand>
{
    public DomainResult Handle(GameState gameState, EnsureSkillPpTrackingCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.ActorId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Actor id is required."));
        }

        gameState.ActorSkillPpState.EnsureTracked(command.ActorId);
        return DomainResult.Success();
    }
}
