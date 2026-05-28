using System.Collections.Generic;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Quests.Events
{

    public sealed class QuestRewardedEvent : DomainEvent
    {
        public QuestRewardedEvent(
            string questId,
            IReadOnlyList<CurrencyDelta> currencyGranted,
            IReadOnlyList<InventoryDelta> inventoryGranted)
            : base(nameof(QuestRewardedEvent))
        {
            QuestId = questId;
            CurrencyGranted = currencyGranted;
            InventoryGranted = inventoryGranted;
        }

        public string QuestId { get; }

        public IReadOnlyList<CurrencyDelta> CurrencyGranted { get; }

        public IReadOnlyList<InventoryDelta> InventoryGranted { get; }
    }
}
