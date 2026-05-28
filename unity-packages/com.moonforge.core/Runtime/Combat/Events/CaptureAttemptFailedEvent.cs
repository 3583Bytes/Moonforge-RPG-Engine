using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events
{

    /// <summary>
    /// Raised when a capture attempt's roll missed. The target is still in the battle; the
    /// capturer's turn was consumed.
    /// </summary>
    public sealed class CaptureAttemptFailedEvent : DomainEvent
    {
        public CaptureAttemptFailedEvent(string battleId, string capturerActorId, string targetActorId, int rolledChancePercent)
            : base(nameof(CaptureAttemptFailedEvent))
        {
            BattleId = battleId;
            CapturerActorId = capturerActorId;
            TargetActorId = targetActorId;
            RolledChancePercent = rolledChancePercent;
        }

        public string BattleId { get; }

        public string CapturerActorId { get; }

        public string TargetActorId { get; }

        /// <summary>The effective success chance the engine computed for this attempt, for telemetry/UI.</summary>
        public int RolledChancePercent { get; }
    }
}
