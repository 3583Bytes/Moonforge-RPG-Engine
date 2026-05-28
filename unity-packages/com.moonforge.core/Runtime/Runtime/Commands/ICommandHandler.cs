using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Runtime.Commands
{

    public interface ICommandHandler<in TCommand> where TCommand : ICommand
    {
        DomainResult Handle(GameState gameState, TCommand command, CommandContext context);
    }
}
