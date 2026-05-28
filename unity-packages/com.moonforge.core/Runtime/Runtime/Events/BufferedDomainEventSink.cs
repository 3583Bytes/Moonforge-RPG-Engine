using System.Collections.Generic;

namespace Moonforge.Core.Runtime.Events
{

    public sealed class BufferedDomainEventSink : IDomainEventSink
    {
        private readonly List<DomainEvent> _events = new();

        public IReadOnlyList<DomainEvent> Events => _events;

        public void Publish(DomainEvent domainEvent)
        {
            _events.Add(domainEvent);
        }
    }
}
