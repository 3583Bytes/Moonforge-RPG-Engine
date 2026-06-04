using Moonforge.Core;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;
using Moonforge.Core.World;
using Moonforge.Core.World.Commands;

namespace Moonforge.Core.Tests;

public sealed class CommandDispatcherTests
{
    [Fact]
    public void Dispatch_Sets_World_Variable_And_Emits_Event()
    {
        GameState gameState = new();
        InMemoryDomainEventSink eventSink = new();
        CommandContext context = CreateContext(eventSink);
        CommandDispatcher dispatcher = new();

        dispatcher.Register(new SetWorldVariableCommandHandler());

        DomainResult result = dispatcher.Dispatch(
            gameState,
            new SetWorldVariableCommand("quest.main.started", WorldVariableValue.FromBool(true)),
            context);

        Assert.True(result.IsSuccess);
        Assert.True(gameState.WorldState.TryGet("quest.main.started", out WorldVariableValue stored));
        Assert.True(stored.TryGetBool(out bool started));
        Assert.True(started);
        Assert.Single(eventSink.Events);
    }

    [Fact]
    public void Dispatch_Rolls_Back_State_When_Handler_Fails()
    {
        GameState gameState = new();
        gameState.WorldState.Set("player.gold", WorldVariableValue.FromInt(10));
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new FailingMutationCommandHandler());

        DomainResult result = dispatcher.Dispatch(
            gameState,
            new FailingMutationCommand(),
            CreateContext(new InMemoryDomainEventSink()));

        Assert.False(result.IsSuccess);
        Assert.True(gameState.WorldState.TryGet("player.gold", out WorldVariableValue gold));
        Assert.True(gold.TryGetInt(out int currentGold));
        Assert.Equal(10, currentGold);
    }

    [Fact]
    public void Dispatch_Does_Not_Emit_Events_When_Handler_Fails()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new FailingMutationCommandHandler());

        DomainResult result = dispatcher.Dispatch(gameState, new FailingMutationCommand(), CreateContext(sink));

        Assert.False(result.IsSuccess);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void Dispatch_Fails_And_Rolls_Back_When_Reactors_Cascade_Forever()
    {
        GameState gameState = new();
        gameState.WorldState.Set("player.gold", WorldVariableValue.FromInt(10));
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new() { MaxBufferedEventsPerDispatch = 16 };
        dispatcher.Register(new PublishPingCommandHandler());
        dispatcher.RegisterReactor(new PingPongReactor(reactTo: "test.ping", publish: "test.pong"));
        dispatcher.RegisterReactor(new PingPongReactor(reactTo: "test.pong", publish: "test.ping"));

        DomainResult result = dispatcher.Dispatch(gameState, new PublishPingCommand(), CreateContext(sink));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InternalError, result.Error!.Code);
        Assert.Contains("cascade", result.Error.Message);
        // The command's own mutation must roll back and no events may leak.
        Assert.True(gameState.WorldState.TryGet("player.gold", out WorldVariableValue gold));
        Assert.True(gold.TryGetInt(out int currentGold));
        Assert.Equal(10, currentGold);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void Dispatch_Allows_Bounded_Reactor_Cascades_Below_The_Cap()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new() { MaxBufferedEventsPerDispatch = 16 };
        dispatcher.Register(new PublishPingCommandHandler());
        // One-way cascade: ping → pong, nothing reacts to pong. Two events total.
        dispatcher.RegisterReactor(new PingPongReactor(reactTo: "test.ping", publish: "test.pong"));

        DomainResult result = dispatcher.Dispatch(gameState, new PublishPingCommand(), CreateContext(sink));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, sink.Events.Count);
        Assert.Contains(sink.Events, e => e is WarningEvent w && w.Code == "test.ping");
        Assert.Contains(sink.Events, e => e is WarningEvent w && w.Code == "test.pong");
    }

    private static CommandContext CreateContext(InMemoryDomainEventSink sink)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 1234, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink);
    }

    private sealed class FailingMutationCommand : ICommand
    {
    }

    private sealed class FailingMutationCommandHandler : ICommandHandler<FailingMutationCommand>
    {
        public DomainResult Handle(GameState gameState, FailingMutationCommand command, CommandContext context)
        {
            gameState.WorldState.Set("player.gold", WorldVariableValue.FromInt(0));
            context.EventSink.Publish(new WarningEvent("test.fail", "Failure event should not leak."));
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Forced failure."));
        }
    }

    private sealed class PublishPingCommand : ICommand
    {
    }

    private sealed class PublishPingCommandHandler : ICommandHandler<PublishPingCommand>
    {
        public DomainResult Handle(GameState gameState, PublishPingCommand command, CommandContext context)
        {
            gameState.WorldState.Set("player.gold", WorldVariableValue.FromInt(0));
            context.EventSink.Publish(new WarningEvent("test.ping", "ping"));
            return DomainResult.Success();
        }
    }

    /// <summary>Publishes one event in response to another — registering two of these in
    /// opposite directions produces an infinite cascade.</summary>
    private sealed class PingPongReactor : IDomainEventReactor
    {
        private readonly string _reactTo;
        private readonly string _publish;

        public PingPongReactor(string reactTo, string publish)
        {
            _reactTo = reactTo;
            _publish = publish;
        }

        public DomainResult React(GameState gameState, DomainEvent domainEvent, CommandContext context)
        {
            if (domainEvent is WarningEvent warning && warning.Code == _reactTo)
            {
                context.EventSink.Publish(new WarningEvent(_publish, "cascade"));
            }

            return DomainResult.Success();
        }
    }
}
