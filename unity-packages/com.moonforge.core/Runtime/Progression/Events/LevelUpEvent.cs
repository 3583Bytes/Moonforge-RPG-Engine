using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Progression.Events
{

    public sealed class LevelUpEvent : DomainEvent
    {
        public LevelUpEvent(string actorId, int fromLevel, int toLevel)
            : base(nameof(LevelUpEvent))
        {
            ActorId = actorId;
            FromLevel = fromLevel;
            ToLevel = toLevel;
        }

        public string ActorId { get; }

        public int FromLevel { get; }

        public int ToLevel { get; }
    }
}
