using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Runtime.Events
{

    public interface IDomainEventReactor
    {
        DomainResult React(GameState gameState, DomainEvent domainEvent, CommandContext context);
    }
}
