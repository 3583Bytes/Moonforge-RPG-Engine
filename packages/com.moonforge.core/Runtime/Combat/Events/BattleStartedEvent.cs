using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events
{

    public sealed class BattleStartedEvent : DomainEvent
    {
        public BattleStartedEvent(string battleId)
            : base(nameof(BattleStartedEvent))
        {
            BattleId = battleId;
        }

        public string BattleId { get; }
    }
}
