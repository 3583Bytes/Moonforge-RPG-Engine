namespace Moonforge.Core.Runtime.Events
{

    public interface IDomainEventSink
    {
        void Publish(DomainEvent domainEvent);
    }
}
