using System.Linq;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Combat.Commands;

public sealed class StartBattleCommandHandler : ICommandHandler<StartBattleCommand>
{
    public DomainResult Handle(GameState gameState, StartBattleCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.BattleId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Battle ID is required."));
        }

        if (command.Actors.Count == 0)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Battle requires at least one actor."));
        }

        if (command.Skills.Count == 0)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Battle requires at least one skill."));
        }

        if (gameState.ActiveBattle is not null && gameState.ActiveBattle.Status == BattleStatus.Active)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, "Another battle is currently active."));
        }

        if (!command.Actors.Any(x => x.Faction == CombatFaction.Party)
            || !command.Actors.Any(x => x.Faction == CombatFaction.Enemy))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.ValidationFailed,
                "Battle requires at least one party actor and one enemy actor."));
        }

        foreach (BattleActorDefinition actor in command.Actors)
        {
            if (actor.SkillIds.Count == 0)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.ValidationFailed,
                    $"Actor '{actor.ActorId}' has no skills."));
            }

            foreach (string skillId in actor.SkillIds)
            {
                if (!command.Skills.Any(x => x.Id == skillId))
                {
                    return DomainResult.Fail(new DomainError(
                        DomainErrorCode.ValidationFailed,
                        $"Actor '{actor.ActorId}' references unknown battle skill '{skillId}'."));
                }
            }
        }

        gameState.ActiveBattle = BattleRuntime.Instance.CreateBattle(command, context);
        return DomainResult.Success();
    }
}
