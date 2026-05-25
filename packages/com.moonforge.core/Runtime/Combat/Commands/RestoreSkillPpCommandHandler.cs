using System.Collections.Generic;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Combat.Commands;

public sealed class RestoreSkillPpCommandHandler : ICommandHandler<RestoreSkillPpCommand>
{
    public DomainResult Handle(GameState gameState, RestoreSkillPpCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.ActorId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Actor id is required."));
        }

        IReadOnlyDictionary<string, int>? tracked = gameState.ActorSkillPpState.GetActorSkillPp(command.ActorId);
        if (tracked is null)
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.NotFound,
                $"Actor '{command.ActorId}' is not tracked for PP. Call EnsureSkillPpTrackingCommand first."));
        }

        if (command.SkillId is not null)
        {
            RestoreOne(gameState, context, command.ActorId, command.SkillId, command.Amount);
            return DomainResult.Success();
        }

        // Snapshot keys first so we can mutate the dict while iterating.
        List<string> skillIds = new(tracked.Keys);
        foreach (string skillId in skillIds)
        {
            RestoreOne(gameState, context, command.ActorId, skillId, command.Amount);
        }

        return DomainResult.Success();
    }

    private static void RestoreOne(GameState gameState, CommandContext context, string actorId, string skillId, int? amount)
    {
        int currentPp = gameState.ActorSkillPpState.TryGetSkillPp(actorId, skillId, out int existing) ? existing : 0;

        // Resolve max PP via the active battle's skill registry if one is in progress; otherwise
        // amount-less restores can't compute a cap. In practice games will pass a concrete amount
        // outside of battle, or restore inside battle so the registry is present.
        int maxPp = ResolveMaxPp(gameState, skillId);

        int newPp;
        if (amount.HasValue)
        {
            newPp = currentPp + amount.Value;
        }
        else
        {
            newPp = maxPp > 0 ? maxPp : currentPp;
        }

        if (maxPp > 0 && newPp > maxPp)
        {
            newPp = maxPp;
        }

        if (newPp < 0)
        {
            newPp = 0;
        }

        gameState.ActorSkillPpState.SetSkillPp(actorId, skillId, newPp);

        // Mirror into the active battle so an in-progress fight sees the restoration too.
        if (gameState.ActiveBattle is not null
            && gameState.ActiveBattle.TryGetActor(actorId, out BattleActorState actor))
        {
            actor.SkillPp[skillId] = newPp;
        }

        context.EventSink.Publish(new SkillPpRestoredEvent(actorId, skillId, newPp - currentPp, newPp));
    }

    private static int ResolveMaxPp(GameState gameState, string skillId)
    {
        if (gameState.ActiveBattle is not null && gameState.ActiveBattle.TryGetSkill(skillId, out BattleSkillDefinition skill))
        {
            return skill.MaxPp;
        }

        return 0;
    }
}
