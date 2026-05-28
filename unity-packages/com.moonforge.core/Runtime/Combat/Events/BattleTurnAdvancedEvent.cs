using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events
{

    public sealed class BattleTurnAdvancedEvent : DomainEvent
    {
        public BattleTurnAdvancedEvent(string battleId, string actorId, int round)
            : base(nameof(BattleTurnAdvancedEvent))
        {
            BattleId = battleId;
            ActorId = actorId;
            Round = round;
        }

        public string BattleId { get; }

        public string ActorId { get; }

        public int Round { get; }
    }
}
