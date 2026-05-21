using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Combat.Commands;

/// <summary>
/// Opts an actor into persistent PP tracking. Once tracked, the actor's PP is hydrated from
/// state at battle start and written back at battle end (and at mid-battle swap-out /
/// capture). Call this once when the actor enters long-term play — typically right after
/// adding them to the party. The built-in auto-track reactors call it for you on
/// <see cref="Moonforge.Core.Party.Events.PartyMemberAddedEvent"/> and
/// <see cref="Events.BattleActorCapturedEvent"/>.
/// </summary>
public sealed class EnsureSkillPpTrackingCommand : ICommand
{
    public EnsureSkillPpTrackingCommand(string actorId)
    {
        ActorId = actorId;
    }

    public string ActorId { get; }
}
