using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Progression.Events;

public sealed class ExperienceGrantedEvent : DomainEvent
{
    public ExperienceGrantedEvent(string actorId, long amount, long newXp, int level)
        : base(nameof(ExperienceGrantedEvent))
    {
        ActorId = actorId;
        Amount = amount;
        NewXp = newXp;
        Level = level;
    }

    public string ActorId { get; }

    public long Amount { get; }

    public long NewXp { get; }

    public int Level { get; }
}
