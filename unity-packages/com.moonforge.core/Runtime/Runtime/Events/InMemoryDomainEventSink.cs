using System;
using System.Collections.Generic;

namespace Moonforge.Core.Runtime.Events
{

    public sealed class InMemoryDomainEventSink : IDomainEventSink
    {
        private readonly List<DomainEvent> _events = new();
        private int _drainCursor;

        public IReadOnlyList<DomainEvent> Events => _events;

        public void Publish(DomainEvent domainEvent)
        {
            _events.Add(domainEvent);
        }

        /// <summary>
        /// Returns events appended since the previous call to <see cref="DrainNewEvents"/>
        /// (or since construction). Useful for a pull-based consumer loop that processes
        /// each event exactly once across many dispatches.
        /// </summary>
        public IReadOnlyList<DomainEvent> DrainNewEvents()
        {
            if (_drainCursor >= _events.Count)
            {
                return Array.Empty<DomainEvent>();
            }

            int count = _events.Count - _drainCursor;
            DomainEvent[] batch = new DomainEvent[count];
            _events.CopyTo(_drainCursor, batch, 0, count);
            _drainCursor = _events.Count;
            return batch;
        }
    }
}
