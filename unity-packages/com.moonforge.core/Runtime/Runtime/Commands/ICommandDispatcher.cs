using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Runtime.Commands
{

    public interface ICommandDispatcher
    {
        void Register<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand;

        void RegisterReactor(IDomainEventReactor reactor);

        DomainResult Dispatch<TCommand>(GameState gameState, TCommand command, CommandContext context) where TCommand : ICommand;
    }
}
