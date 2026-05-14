namespace Moonforge.Core.Runtime.Events;

public abstract class DomainEvent
{
    protected DomainEvent(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
