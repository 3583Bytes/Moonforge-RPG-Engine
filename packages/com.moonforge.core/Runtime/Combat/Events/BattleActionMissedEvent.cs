using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events;

public sealed class BattleActionMissedEvent : DomainEvent
{
    public BattleActionMissedEvent(
        string battleId,
        string actorId,
        string skillId,
        string targetActorId)
        : base(nameof(BattleActionMissedEvent))
    {
        BattleId = battleId;
        ActorId = actorId;
        SkillId = skillId;
        TargetActorId = targetActorId;
    }

    public string BattleId { get; }

    public string ActorId { get; }

    public string SkillId { get; }

    public string TargetActorId { get; }
}
