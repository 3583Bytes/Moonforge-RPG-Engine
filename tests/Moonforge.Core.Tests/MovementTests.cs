using Moonforge.Core;
using Moonforge.Core.Exploration;
using Moonforge.Core.Exploration.Commands;
using Moonforge.Core.Exploration.Queries;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class MovementTests
{
    [Fact]
    public void MoveActor_Rejects_Walls()
    {
        GameState gameState = CreateConfiguredMapGameState(heroPosition: new GridPosition(1, 1), includeBlockingNpc: false);
        CommandDispatcher dispatcher = CreateDispatcher();
        InMemoryDomainEventSink sink = new();

        var result = dispatcher.Dispatch(gameState, new MoveActorCommand("party.hero", deltaX: -1, deltaY: 0), CreateContext(sink));

        Assert.False(result.IsSuccess);
        Assert.True(gameState.ExplorationState.TryGetActor("party.hero", out ExplorationActorState actor));
        Assert.Equal(1, actor.X);
        Assert.Equal(1, actor.Y);
    }

    [Fact]
    public void MoveActor_Rejects_Out_Of_Bounds()
    {
        GameState gameState = new();
        CommandDispatcher dispatcher = CreateDispatcher();
        InMemoryDomainEventSink sink = new();
        CommandContext context = CreateContext(sink);

        List<ExplorationTileFlags> tiles = [];
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                tiles.Add(ExplorationTileFlags.Walkable);
            }
        }

        Assert.True(dispatcher.Dispatch(
            gameState,
            new ConfigureExplorationMapCommand("open.map", 3, 3, tiles),
            context).IsSuccess);

        Assert.True(dispatcher.Dispatch(
            gameState,
            new UpsertExplorationActorCommand("party.hero", 0, 1, blocksMovement: true),
            context).IsSuccess);

        var result = dispatcher.Dispatch(gameState, new MoveActorCommand("party.hero", deltaX: -1, deltaY: 0), context);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void MoveActor_Rejects_Blocked_Target()
    {
        GameState gameState = CreateConfiguredMapGameState(heroPosition: new GridPosition(2, 2), includeBlockingNpc: true);
        CommandDispatcher dispatcher = CreateDispatcher();
        InMemoryDomainEventSink sink = new();

        var result = dispatcher.Dispatch(gameState, new MoveActorCommand("party.hero", deltaX: 1, deltaY: 0), CreateContext(sink));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void MoveActor_Allows_Valid_Tile()
    {
        GameState gameState = CreateConfiguredMapGameState(heroPosition: new GridPosition(2, 2), includeBlockingNpc: false);
        CommandDispatcher dispatcher = CreateDispatcher();
        InMemoryDomainEventSink sink = new();

        var result = dispatcher.Dispatch(gameState, new MoveActorCommand("party.hero", deltaX: 1, deltaY: 0), CreateContext(sink));

        Assert.True(result.IsSuccess);
        Assert.True(gameState.ExplorationState.TryGetActor("party.hero", out ExplorationActorState actor));
        Assert.Equal(3, actor.X);
        Assert.Equal(2, actor.Y);
    }

    [Fact]
    public void CanMoveActor_Query_Uses_Same_Rules()
    {
        GameState gameState = CreateConfiguredMapGameState(heroPosition: new GridPosition(2, 2), includeBlockingNpc: true);
        CanMoveActorQueryHandler queryHandler = new();

        bool canMoveIntoWall = queryHandler.Query(gameState, new CanMoveActorQuery("party.hero", 0, 2));
        bool canMoveIntoNpc = queryHandler.Query(gameState, new CanMoveActorQuery("party.hero", 3, 2));
        bool canMoveIntoFloor = queryHandler.Query(gameState, new CanMoveActorQuery("party.hero", 2, 3));

        Assert.False(canMoveIntoWall);
        Assert.False(canMoveIntoNpc);
        Assert.True(canMoveIntoFloor);
    }

    private static GameState CreateConfiguredMapGameState(GridPosition heroPosition, bool includeBlockingNpc)
    {
        GameState gameState = new();
        CommandDispatcher dispatcher = CreateDispatcher();
        InMemoryDomainEventSink sink = new();
        CommandContext context = CreateContext(sink);

        List<ExplorationTileFlags> tiles = [];
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                bool border = x == 0 || y == 0 || x == 4 || y == 4;
                tiles.Add(border
                    ? ExplorationTileFlags.BlocksLineOfSight
                    : ExplorationTileFlags.Walkable | ExplorationTileFlags.EncounterAllowed);
            }
        }

        Assert.True(dispatcher.Dispatch(
            gameState,
            new ConfigureExplorationMapCommand("test.map", 5, 5, tiles),
            context).IsSuccess);

        Assert.True(dispatcher.Dispatch(
            gameState,
            new UpsertExplorationActorCommand("party.hero", heroPosition.X, heroPosition.Y, blocksMovement: true),
            context).IsSuccess);

        if (includeBlockingNpc)
        {
            Assert.True(dispatcher.Dispatch(
                gameState,
                new UpsertExplorationActorCommand("npc.guard", 3, 2, blocksMovement: true),
                context).IsSuccess);
        }

        return gameState;
    }

    private static CommandDispatcher CreateDispatcher()
    {
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new ConfigureExplorationMapCommandHandler());
        dispatcher.Register(new UpsertExplorationActorCommandHandler());
        dispatcher.Register(new MoveActorCommandHandler());
        return dispatcher;
    }

    private static CommandContext CreateContext(InMemoryDomainEventSink sink)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 321, sequence: 17),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink);
    }
}
