using Moonforge.Core.Combat.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Party.Reactors;

/// <summary>
/// Keeps <see cref="PartyState"/> active/reserve flags in sync with mid-battle actor swaps.
/// When a <see cref="BattleActorSwappedEvent"/> fires, the outgoing actor (if known to the
/// party) is moved to reserve and the incoming actor (if known) is promoted to active.
///
/// Either or both ids may be absent from the party — wild battles or scripted encounters
/// can swap actors that don't belong to the player. Unknown actors are silently ignored.
/// </summary>
public sealed class PartyActiveSyncReactor : IDomainEventReactor
{
    public DomainResult React(GameState gameState, DomainEvent domainEvent, CommandContext context)
    {
        if (domainEvent is not BattleActorSwappedEvent swap)
        {
            return DomainResult.Success();
        }

        PartyState party = gameState.PartyState;

        if (party.TryGet(swap.OutActorId, out PartyMember outgoing))
        {
            outgoing.IsActive = false;
        }

        if (party.TryGet(swap.InActorId, out PartyMember incoming))
        {
            incoming.IsActive = true;
        }

        return DomainResult.Success();
    }
}
