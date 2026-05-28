using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Shops.Events
{

    public sealed class ShopRestockedEvent : DomainEvent
    {
        public ShopRestockedEvent(string shopId, long simulationMinute)
            : base(nameof(ShopRestockedEvent))
        {
            ShopId = shopId;
            SimulationMinute = simulationMinute;
        }

        public string ShopId { get; }

        public long SimulationMinute { get; }
    }
}
