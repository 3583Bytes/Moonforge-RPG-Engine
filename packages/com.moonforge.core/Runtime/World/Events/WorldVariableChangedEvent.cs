using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.World.Events
{

    public sealed class WorldVariableChangedEvent : DomainEvent
    {
        public WorldVariableChangedEvent(string key, WorldVariableValue value)
            : base(nameof(WorldVariableChangedEvent))
        {
            Key = key;
            Value = value;
        }

        public string Key { get; }

        public WorldVariableValue Value { get; }
    }
}
