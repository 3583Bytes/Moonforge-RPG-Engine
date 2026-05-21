using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events;

public sealed class SkillPpRestoredEvent : DomainEvent
{
    public SkillPpRestoredEvent(string actorId, string skillId, int amount, int newPp)
        : base(nameof(SkillPpRestoredEvent))
    {
        ActorId = actorId;
        SkillId = skillId;
        Amount = amount;
        NewPp = newPp;
    }

    public string ActorId { get; }

    public string SkillId { get; }

    public int Amount { get; }

    public int NewPp { get; }
}
