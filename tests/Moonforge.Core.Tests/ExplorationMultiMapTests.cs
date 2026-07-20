using System.Collections.Generic;
using Moonforge.Core;
using Moonforge.Core.Exploration;
using Moonforge.Core.Exploration.Commands;
using Moonforge.Core.Exploration.Events;
using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class ExplorationMultiMapTests
{
    private const string Hero = "party.hero";

    [Fact]
    public void Configure_Registers_Map_And_Makes_It_Active()
    {
        GameState gameState = new();
        (CommandDispatcher dispatcher, CommandContext context) = BuildWorld();

        Assert.True(Dispatch(dispatcher, gameState, ConfigureMap("town", 4, 3), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, ConfigureMap("dungeon.1", 5, 5), context).IsSuccess);

        Assert.Equal("dungeon.1", gameState.ExplorationState.ActiveMapId);
        Assert.Equal(new[] { "dungeon.1", "town" }, gameState.ExplorationState.MapIds);
        Assert.True(gameState.ExplorationState.TryGetMap("town", out ExplorationMapState town));
        Assert.Equal(4, town.Width);
        Assert.Equal(5, gameState.ExplorationState.Map.Width);
    }

    [Fact]
    public void Actors_Stay_On_Their_Map_Across_Switches()
    {
        GameState gameState = new();
        (CommandDispatcher dispatcher, CommandContext context) = BuildWorld();

        Assert.True(Dispatch(dispatcher, gameState, ConfigureMap("town", 4, 3), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new UpsertExplorationActorCommand("npc.elder", 2, 1, blocksMovement: true), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, ConfigureMap("dungeon.1", 5, 5), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new UpsertExplorationActorCommand("enemy.rat", 3, 3, blocksMovement: true), context).IsSuccess);

        // Each map sees only its own actors.
        Assert.False(gameState.ExplorationState.Actors.ContainsKey("npc.elder"));
        Assert.True(gameState.ExplorationState.Actors.ContainsKey("enemy.rat"));

        // Switching back restores the town's actors exactly where they were.
        Assert.True(Dispatch(dispatcher, gameState, new SwitchExplorationMapCommand("town"), context).IsSuccess);
        Assert.True(gameState.ExplorationState.TryGetActor("npc.elder", out ExplorationActorState elder));
        Assert.Equal(2, elder.X);
        Assert.False(gameState.ExplorationState.Actors.ContainsKey("enemy.rat"));
    }

    [Fact]
    public void Switch_Carries_Actors_To_Spawn_Positions()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        (CommandDispatcher dispatcher, CommandContext context) = BuildWorld(sink);

        Assert.True(Dispatch(dispatcher, gameState, ConfigureMap("town", 4, 3), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new UpsertExplorationActorCommand(Hero, 1, 1, blocksMovement: true), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, ConfigureMap("dungeon.1", 5, 5), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new SwitchExplorationMapCommand("town"), context).IsSuccess);

        DomainResult result = Dispatch(dispatcher, gameState, new SwitchExplorationMapCommand(
            "dungeon.1",
            new[] { new ExplorationActorCarry(Hero, 2, 2) }), context);

        Assert.True(result.IsSuccess);
        Assert.Equal("dungeon.1", gameState.ExplorationState.ActiveMapId);
        Assert.True(gameState.ExplorationState.TryGetActor(Hero, out ExplorationActorState hero));
        Assert.Equal(2, hero.X);
        Assert.Equal(2, hero.Y);
        // The hero left the town — no duplicate left behind.
        Assert.False(gameState.ExplorationState.GetActorsForMap("town").ContainsKey(Hero));
        Assert.Contains(sink.Events, e => e is ExplorationMapSwitchedEvent sw && sw.FromMapId == "town" && sw.ToMapId == "dungeon.1");
    }

    [Fact]
    public void Switch_Rolls_Back_When_Carry_Position_Is_Invalid()
    {
        GameState gameState = new();
        (CommandDispatcher dispatcher, CommandContext context) = BuildWorld();

        Assert.True(Dispatch(dispatcher, gameState, ConfigureMap("town", 4, 3), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new UpsertExplorationActorCommand(Hero, 1, 1, blocksMovement: true), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, ConfigureMap("dungeon.1", 5, 5), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new SwitchExplorationMapCommand("town"), context).IsSuccess);

        DomainResult result = Dispatch(dispatcher, gameState, new SwitchExplorationMapCommand(
            "dungeon.1",
            new[] { new ExplorationActorCarry(Hero, 99, 99) }), context);

        Assert.False(result.IsSuccess);
        // Atomic rollback: still in town, hero untouched.
        Assert.Equal("town", gameState.ExplorationState.ActiveMapId);
        Assert.True(gameState.ExplorationState.TryGetActor(Hero, out ExplorationActorState hero));
        Assert.Equal(1, hero.X);
    }

    [Fact]
    public void Switch_To_Unknown_Map_Fails()
    {
        GameState gameState = new();
        (CommandDispatcher dispatcher, CommandContext context) = BuildWorld();

        DomainResult result = Dispatch(dispatcher, gameState, new SwitchExplorationMapCommand("nowhere"), context);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.NotFound, result.Error!.Code);
    }

    [Fact]
    public void Remove_Discards_Map_But_Refuses_The_Active_One()
    {
        GameState gameState = new();
        (CommandDispatcher dispatcher, CommandContext context) = BuildWorld();

        Assert.True(Dispatch(dispatcher, gameState, ConfigureMap("town", 4, 3), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, ConfigureMap("dungeon.1", 5, 5), context).IsSuccess);

        Assert.False(Dispatch(dispatcher, gameState, new RemoveExplorationMapCommand("dungeon.1"), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new RemoveExplorationMapCommand("town"), context).IsSuccess);
        Assert.Equal(new[] { "dungeon.1" }, gameState.ExplorationState.MapIds);
    }

    [Fact]
    public void Clone_And_Rollback_Preserve_All_Maps_And_Their_Actors()
    {
        GameState gameState = new();
        (CommandDispatcher dispatcher, CommandContext context) = BuildWorld();

        Assert.True(Dispatch(dispatcher, gameState, ConfigureMap("town", 4, 3), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new UpsertExplorationActorCommand("npc.elder", 2, 1, blocksMovement: true), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, ConfigureMap("dungeon.1", 5, 5), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new UpsertExplorationActorCommand(Hero, 1, 1, blocksMovement: true), context).IsSuccess);

        GameState clone = gameState.Clone();
        Assert.Equal("dungeon.1", clone.ExplorationState.ActiveMapId);
        Assert.Equal(2, clone.ExplorationState.MapIds.Count);
        Assert.True(clone.ExplorationState.GetActorsForMap("town").ContainsKey("npc.elder"));
        Assert.True(clone.ExplorationState.GetActorsForMap("dungeon.1").ContainsKey(Hero));

        // Mutating the original doesn't leak into the clone (deep copy).
        Assert.True(Dispatch(dispatcher, gameState, new RemoveExplorationMapCommand("town"), context).IsSuccess);
        Assert.Equal(2, clone.ExplorationState.MapIds.Count);
    }

    [Fact]
    public void Save_Round_Trip_Preserves_All_Maps_Actors_And_Active_Id()
    {
        GameState original = new();
        (CommandDispatcher dispatcher, CommandContext context) = BuildWorld();

        Assert.True(Dispatch(dispatcher, original, ConfigureMap("town", 4, 3), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, original, new UpsertExplorationActorCommand("npc.elder", 2, 1, blocksMovement: true), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, original, ConfigureMap("dungeon.1", 5, 5), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, original, new UpsertExplorationActorCommand(Hero, 1, 1, blocksMovement: true), context).IsSuccess);
        Assert.True(Dispatch(dispatcher, original, new SwitchExplorationMapCommand("town"), context).IsSuccess);

        JsonGameStateSerializer serializer = new();
        string json = serializer.Serialize(GameStateSnapshotMapper.Capture(original));
        GameStateSnapshot decoded = serializer.Deserialize(json);
        GameState rebuilt = new();
        GameStateSnapshotMapper.Apply(rebuilt, decoded);

        Assert.Equal("town", rebuilt.ExplorationState.ActiveMapId);
        Assert.Equal(new[] { "dungeon.1", "town" }, rebuilt.ExplorationState.MapIds);
        Assert.True(rebuilt.ExplorationState.TryGetActor("npc.elder", out ExplorationActorState elder));
        Assert.Equal(2, elder.X);
        Assert.True(rebuilt.ExplorationState.GetActorsForMap("dungeon.1").ContainsKey(Hero));
        Assert.True(rebuilt.ExplorationState.TryGetMap("dungeon.1", out ExplorationMapState dungeon));
        Assert.Equal(5, dungeon.Width);
    }

    [Fact]
    public void Legacy_Single_Map_Snapshot_Applies_As_One_Active_Map()
    {
        // Shape written by schema ≤ 8 — only "map" + "actors", no "maps"/"activeMapId".
        JsonGameStateSerializer serializer = new();
        string legacyJson = "{\"schemaVersion\":8,\"contentVersion\":\"v1\",\"simulationMinutes\":0," +
                            "\"exploration\":{\"map\":{\"mapId\":\"town\",\"width\":2,\"height\":2,\"tiles\":[1,1,1,1]}," +
                            "\"actors\":[{\"actorId\":\"party.hero\",\"x\":1,\"y\":0,\"blocksMovement\":true}]}}";

        GameStateSnapshot decoded = serializer.Deserialize(legacyJson);
        GameState rebuilt = new();
        GameStateSnapshotMapper.Apply(rebuilt, decoded);

        Assert.Equal("town", rebuilt.ExplorationState.ActiveMapId);
        Assert.Equal(new[] { "town" }, rebuilt.ExplorationState.MapIds);
        Assert.True(rebuilt.ExplorationState.TryGetActor(Hero, out ExplorationActorState hero));
        Assert.Equal(1, hero.X);
    }

    [Fact]
    public void Direct_Map_Configure_Still_Works_And_Registry_Self_Heals()
    {
        // Legacy callers configure through the Map property directly.
        GameState gameState = new();
        ExplorationTileFlags[] tiles = WalkableTiles(3, 3);
        Assert.True(gameState.ExplorationState.Map.TryConfigure("legacy", 3, 3, tiles, out _));

        Assert.Equal("legacy", gameState.ExplorationState.ActiveMapId);
        Assert.Equal(new[] { "legacy" }, gameState.ExplorationState.MapIds);
        gameState.ExplorationState.UpsertActor(Hero, new GridPosition(1, 1), blocksMovement: true);
        Assert.True(gameState.ExplorationState.GetActorsForMap("legacy").ContainsKey(Hero));
    }

    private static ConfigureExplorationMapCommand ConfigureMap(string mapId, int width, int height)
    {
        return new ConfigureExplorationMapCommand(mapId, width, height, WalkableTiles(width, height));
    }

    private static ExplorationTileFlags[] WalkableTiles(int width, int height)
    {
        ExplorationTileFlags[] tiles = new ExplorationTileFlags[width * height];
        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i] = ExplorationTileFlags.Walkable;
        }

        return tiles;
    }

    private static DomainResult Dispatch<TCommand>(
        CommandDispatcher dispatcher,
        GameState gameState,
        TCommand command,
        CommandContext context) where TCommand : ICommand
    {
        return dispatcher.Dispatch(gameState, command, context);
    }

    private static (CommandDispatcher, CommandContext) BuildWorld(InMemoryDomainEventSink? sink = null)
    {
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new ConfigureExplorationMapCommandHandler());
        dispatcher.Register(new SwitchExplorationMapCommandHandler());
        dispatcher.Register(new RemoveExplorationMapCommandHandler());
        dispatcher.Register(new UpsertExplorationActorCommandHandler());
        dispatcher.Register(new MoveActorCommandHandler());

        CommandContext context = new(
            new Pcg32RandomSource(seed: 7, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink ?? new InMemoryDomainEventSink());
        return (dispatcher, context);
    }
}
