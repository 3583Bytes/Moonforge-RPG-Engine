using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Progression.Commands;
using Moonforge.Core.Progression.Reactors;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Time;
using Moonforge.Core.Stats;

namespace Moonforge.Core.Tests;

public sealed class LevelUpStatGrowthTests
{
    private const string Hero = "party.hero";
    private const string Curve = "curve.growth";

    [Fact]
    public void Single_Level_Up_Applies_Configured_Stat_Gains()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) =
            BuildWorld(gains: new Dictionary<string, int> { ["atk"] = 1, ["vit"] = 2 });

        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Hero, Curve), Context(sink, defs)).IsSuccess);

        // 15 XP crosses one threshold (10) → level 2, single LevelUpEvent.
        Assert.True(dispatcher.Dispatch(gameState, new GrantExperienceCommand(Hero, 15), Context(sink, defs)).IsSuccess);

        Assert.True(gameState.ActorStatsState.TryGet(Hero, out StatBlock block));
        Assert.Equal(1, block.Get("atk", defs, new NoOpFormulaEvaluator()));
        Assert.Equal(2, block.Get("vit", defs, new NoOpFormulaEvaluator()));
    }

    [Fact]
    public void Multi_Level_Jump_Stacks_Gains_Per_Level()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) =
            BuildWorld(gains: new Dictionary<string, int> { ["atk"] = 1 });

        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Hero, Curve), Context(sink, defs)).IsSuccess);

        // 100 XP crosses thresholds 10, 30, 60, 100 → level 5 (4 LevelUpEvents).
        Assert.True(dispatcher.Dispatch(gameState, new GrantExperienceCommand(Hero, 100), Context(sink, defs)).IsSuccess);

        Assert.True(gameState.ActorStatsState.TryGet(Hero, out StatBlock block));
        Assert.Equal(4, block.Get("atk", defs, new NoOpFormulaEvaluator()));
        // 4 distinct modifiers, one per level, all sourceKind "progression".
        Assert.Equal(4, block.Modifiers.Count(m => m.SourceKind == LevelUpStatGrowthReactor.SourceKind));
    }

    [Fact]
    public void Curve_Without_Gains_Is_A_No_Op()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) =
            BuildWorld(gains: null);

        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Hero, Curve), Context(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new GrantExperienceCommand(Hero, 100), Context(sink, defs)).IsSuccess);

        // Reactor doesn't touch the stat state when StatGainsPerLevel is empty.
        Assert.False(gameState.ActorStatsState.TryGet(Hero, out _));
    }

    [Fact]
    public void Zero_Valued_Gains_Are_Skipped_To_Avoid_Empty_Modifiers()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) =
            BuildWorld(gains: new Dictionary<string, int> { ["atk"] = 1, ["def"] = 0 });

        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Hero, Curve), Context(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new GrantExperienceCommand(Hero, 15), Context(sink, defs)).IsSuccess);

        Assert.True(gameState.ActorStatsState.TryGet(Hero, out StatBlock block));
        Assert.Single(block.Modifiers);
        Assert.Equal("atk", block.Modifiers[0].StatId);
    }

    private static (GameState, CommandDispatcher, InMemoryGameDefinitionCatalog, InMemoryDomainEventSink) BuildWorld(
        IReadOnlyDictionary<string, int>? gains)
    {
        GameState gameState = new();
        InMemoryGameDefinitionCatalog defs = new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition("currency.gold", 999))
            .AddExperienceCurve(new ExperienceCurveDefinition(
                id: Curve,
                xpThresholds: new long[] { 10, 30, 60, 100 },
                displayName: "Growth Curve",
                statGainsPerLevel: gains));

        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();
        return (gameState, dispatcher, defs, sink);
    }

    private static CommandContext Context(InMemoryDomainEventSink sink, IGameDefinitionCatalog defs)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 1, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            defs);
    }
}
