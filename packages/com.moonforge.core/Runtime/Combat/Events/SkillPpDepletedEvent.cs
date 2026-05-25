using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events;

/// <summary>
/// Fired when a skill use takes the actor's current PP to 0. Diagnostic / UX hook so games
/// can surface a "out of PP" notice without polling.
/// </summary>
public sealed class SkillPpDepletedEvent : DomainEvent
{
    public SkillPpDepletedEvent(string actorId, string skillId)
        : base(nameof(SkillPpDepletedEvent))
    {
        ActorId = actorId;
        SkillId = skillId;
    }

    public string ActorId { get; }

    public string SkillId { get; }
}
