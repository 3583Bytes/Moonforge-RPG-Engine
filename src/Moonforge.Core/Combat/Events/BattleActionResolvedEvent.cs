using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events;

public sealed class BattleActionResolvedEvent : DomainEvent
{
    public BattleActionResolvedEvent(
        string battleId,
        string actorId,
        string skillId,
        string targetActorId,
        int amount,
        bool wasHeal)
        : base(nameof(BattleActionResolvedEvent))
    {
        BattleId = battleId;
        ActorId = actorId;
        SkillId = skillId;
        TargetActorId = targetActorId;
        Amount = amount;
        WasHeal = wasHeal;
    }

    public string BattleId { get; }

    public string ActorId { get; }

    public string SkillId { get; }

    public string TargetActorId { get; }

    public int Amount { get; }

    public bool WasHeal { get; }
}
