using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Party.Events;

public sealed class PartyConfiguredEvent : DomainEvent
{
    public PartyConfiguredEvent(int maxActive, int maxRoster)
        : base(nameof(PartyConfiguredEvent))
    {
        MaxActive = maxActive;
        MaxRoster = maxRoster;
    }

    public int MaxActive { get; }

    public int MaxRoster { get; }
}
