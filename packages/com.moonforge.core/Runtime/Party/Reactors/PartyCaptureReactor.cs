using Moonforge.Core.Combat.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Party.Reactors
{

    /// <summary>
    /// Adds a captured actor to <see cref="PartyState"/> as a reserve member. Reacts to
    /// <see cref="BattleActorCapturedEvent"/>, runs inside the same dispatcher transaction as
    /// the capture command, and rolls the whole capture back if the party can't accept the new
    /// member (full roster, dup id, etc).
    ///
    /// Games that want a "PC box" overflow store should validate roster room before dispatching
    /// the capture command — when this reactor fails, the player sees the capture undone (ball
    /// not consumed, target still in battle).
    /// </summary>
    public sealed class PartyCaptureReactor : IDomainEventReactor
    {
        public DomainResult React(GameState gameState, DomainEvent domainEvent, CommandContext context)
        {
            if (domainEvent is not BattleActorCapturedEvent captured)
            {
                return DomainResult.Success();
            }

            if (gameState.PartyState.Contains(captured.CapturedActorId))
            {
                // Already in the party — capturing a runaway/transferred member is a no-op.
                return DomainResult.Success();
            }

            if (!gameState.PartyState.TryAdd(captured.CapturedActorId, active: false, out string? error))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.ValidationFailed,
                    error ?? "Captured actor could not be added to party."));
            }

            return DomainResult.Success();
        }
    }
}
