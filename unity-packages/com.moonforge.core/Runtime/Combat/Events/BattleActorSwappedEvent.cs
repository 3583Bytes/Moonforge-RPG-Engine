using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events
{

    /// <summary>
    /// Raised when <see cref="Commands.SwapBattleActorCommand"/> succeeds. Carries both the
    /// outgoing and incoming actor ids so cross-module reactors (e.g. the Party module's
    /// active/reserve sync) can update their state in the same transaction.
    /// </summary>
    public sealed class BattleActorSwappedEvent : DomainEvent
    {
        public BattleActorSwappedEvent(string battleId, string outActorId, string inActorId)
            : base(nameof(BattleActorSwappedEvent))
        {
            BattleId = battleId;
            OutActorId = outActorId;
            InActorId = inActorId;
        }

        public string BattleId { get; }

        public string OutActorId { get; }

        public string InActorId { get; }
    }
}
