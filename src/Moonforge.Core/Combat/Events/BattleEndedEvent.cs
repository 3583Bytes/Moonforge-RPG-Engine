using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events;

public sealed class BattleEndedEvent : DomainEvent
{
    public BattleEndedEvent(string battleId, BattleStatus status)
        : base(nameof(BattleEndedEvent))
    {
        BattleId = battleId;
        Status = status;
    }

    public string BattleId { get; }

    public BattleStatus Status { get; }
}
