using System;
using System.Collections.Generic;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Runtime.Commands
{

    public sealed class CommandDispatcher : ICommandDispatcher
    {
        private readonly Dictionary<Type, object> _handlers = new();
        private readonly List<IDomainEventReactor> _reactors = new();

        /// <summary>
        /// Upper bound on buffered events processed in a single dispatch. Reactors may publish
        /// further events while the buffer is being drained (intentional cascading), so two
        /// reactors that trigger each other would otherwise loop forever. Exceeding the cap
        /// fails and rolls back the transaction instead of hanging. The default is far above
        /// anything a legitimate command produces.
        /// </summary>
        public int MaxBufferedEventsPerDispatch { get; set; } = 1024;

        public void Register<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand
        {
            _handlers[typeof(TCommand)] = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void RegisterReactor(IDomainEventReactor reactor)
        {
            _reactors.Add(reactor ?? throw new ArgumentNullException(nameof(reactor)));
        }

        public DomainResult Dispatch<TCommand>(GameState gameState, TCommand command, CommandContext context) where TCommand : ICommand
        {
            if (!_handlers.TryGetValue(typeof(TCommand), out object handlerObject))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.UnsupportedOperation,
                    $"No handler is registered for command type '{typeof(TCommand).Name}'."));
            }

            ICommandHandler<TCommand> handler = (ICommandHandler<TCommand>)handlerObject;
            GameState snapshot = gameState.Clone();
            BufferedDomainEventSink bufferedEventSink = new();
            CommandContext bufferedContext = context.WithEventSink(bufferedEventSink);

            try
            {
                DomainResult result = handler.Handle(gameState, command, bufferedContext);
                if (!result.IsSuccess)
                {
                    gameState.RestoreFrom(snapshot);
                    return result;
                }

                for (int i = 0; i < bufferedEventSink.Events.Count; i++)
                {
                    if (i >= MaxBufferedEventsPerDispatch)
                    {
                        gameState.RestoreFrom(snapshot);
                        return DomainResult.Fail(new DomainError(
                            DomainErrorCode.InternalError,
                            $"Command '{typeof(TCommand).Name}' produced more than {MaxBufferedEventsPerDispatch} buffered events in one dispatch — likely an event cascade loop between reactors."));
                    }

                    DomainEvent domainEvent = bufferedEventSink.Events[i];
                    foreach (IDomainEventReactor reactor in _reactors)
                    {
                        DomainResult reactorResult = reactor.React(gameState, domainEvent, bufferedContext);
                        if (!reactorResult.IsSuccess)
                        {
                            gameState.RestoreFrom(snapshot);
                            return reactorResult;
                        }
                    }
                }

                foreach (DomainEvent domainEvent in bufferedEventSink.Events)
                {
                    context.EventSink.Publish(domainEvent);
                }

                return result;
            }
            catch (Exception ex)
            {
                gameState.RestoreFrom(snapshot);
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.InternalError,
                    $"Command '{typeof(TCommand).Name}' failed unexpectedly: {ex.GetType().Name}: {ex.Message}",
                    ex));
            }
        }
    }
}
