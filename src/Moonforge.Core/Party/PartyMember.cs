namespace Moonforge.Core.Party;

/// <summary>
/// A single roster entry. <see cref="ActorId"/> identifies the actor whose stats live in
/// <see cref="Stats.ActorStatsState"/>; <see cref="IsActive"/> distinguishes "on the field"
/// from "on the bench/reserve". The party owns membership and active/reserve placement but
/// not the actor's stats or progression — those continue to live in their own modules and
/// are referenced by id.
/// </summary>
public sealed class PartyMember
{
    public PartyMember(string actorId, bool isActive)
    {
        ActorId = actorId;
        IsActive = isActive;
    }

    public string ActorId { get; }

    public bool IsActive { get; set; }

    public PartyMember Clone()
    {
        return new PartyMember(ActorId, IsActive);
    }
}
