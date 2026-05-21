using System.Collections.Generic;
using System.Linq;
using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Evolution;
using Moonforge.Core.Evolution.Commands;
using Moonforge.Core.Evolution.Events;
using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;
using Moonforge.Core.Progression.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class EvolutionTests
{
    private const string Actor = "mon.caterpie";
    private const string EvoEarly = "evo.caterpie-to-metapod";
    private const string EvoLate = "evo.metapod-to-butterfree";
    private const string EvoManual = "evo.eevee-fire-stone";
    private const string Curve = "curve.short";

    [Fact]
    public void Configure_Sets_Per_Actor_Eligibility()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();

        Assert.True(dispatcher.Dispatch(
            gameState,
            new ConfigureActorEvolutionsCommand(Actor, new[] { EvoEarly, EvoLate }),
            Ctx(sink, defs)).IsSuccess);

        Assert.True(gameState.EvolutionState.IsEligible(Actor, EvoEarly));
        Assert.True(gameState.EvolutionState.IsEligible(Actor, EvoLate));
        Assert.False(gameState.EvolutionState.IsEligible(Actor, EvoManual));
    }

    [Fact]
    public void Configure_With_Empty_List_Clears_Eligibility()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorEvolutionsCommand(Actor, new[] { EvoEarly }), Ctx(sink, defs)).IsSuccess);

        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorEvolutionsCommand(Actor, System.Array.Empty<string>()), Ctx(sink, defs)).IsSuccess);

        Assert.False(gameState.EvolutionState.IsEligible(Actor, EvoEarly));
        Assert.Empty(gameState.EvolutionState.GetEvolutions(Actor));
    }

    [Fact]
    public void Trigger_Manual_Evolution_Emits_Event()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorEvolutionsCommand(Actor, new[] { EvoManual }), Ctx(sink, defs)).IsSuccess);

        Assert.True(dispatcher.Dispatch(gameState, new TriggerEvolutionCommand(Actor, EvoManual), Ctx(sink, defs)).IsSuccess);

        EvolutionTriggeredEvent evt = Assert.Single(sink.Events.OfType<EvolutionTriggeredEvent>());
        Assert.Equal(Actor, evt.ActorId);
        Assert.Equal(EvoManual, evt.EvolutionId);
        Assert.Equal(EvolutionTrigger.Manual, evt.Trigger);
        Assert.Equal("species.flareon", evt.EvolvedSpeciesId);
    }

    [Fact]
    public void Trigger_Rejects_Ineligible_Actor()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        // No ConfigureActorEvolutionsCommand → actor isn't registered.

        DomainResult result = dispatcher.Dispatch(gameState, new TriggerEvolutionCommand(Actor, EvoEarly), Ctx(sink, defs));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.UnsupportedOperation, result.Error!.Code);
    }

    [Fact]
    public void Trigger_Rejects_Unknown_Evolution()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorEvolutionsCommand(Actor, new[] { "evo.bogus" }), Ctx(sink, defs)).IsSuccess);

        DomainResult result = dispatcher.Dispatch(gameState, new TriggerEvolutionCommand(Actor, "evo.bogus"), Ctx(sink, defs));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.NotFound, result.Error!.Code);
    }

    [Fact]
    public void Trigger_LevelUp_Evolution_Rejects_Below_Threshold()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Actor, Curve), Ctx(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorEvolutionsCommand(Actor, new[] { EvoEarly }), Ctx(sink, defs)).IsSuccess);
        // Actor is still level 1 — required level is 7.

        DomainResult result = dispatcher.Dispatch(gameState, new TriggerEvolutionCommand(Actor, EvoEarly), Ctx(sink, defs));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.Conflict, result.Error!.Code);
    }

    [Fact]
    public void LevelUp_Auto_Fires_Single_Step_Evolution()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Actor, Curve), Ctx(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorEvolutionsCommand(Actor, new[] { EvoEarly, EvoLate }), Ctx(sink, defs)).IsSuccess);

        // Curve thresholds bring the actor to exactly level 7 with 60 xp.
        Assert.True(dispatcher.Dispatch(gameState, new GrantExperienceCommand(Actor, 60), Ctx(sink, defs)).IsSuccess);

        EvolutionTriggeredEvent evt = Assert.Single(sink.Events.OfType<EvolutionTriggeredEvent>());
        Assert.Equal(EvoEarly, evt.EvolutionId);
    }

    [Fact]
    public void LevelUp_Multi_Step_Fires_Each_Crossed_Threshold_Once()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Actor, Curve), Ctx(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorEvolutionsCommand(Actor, new[] { EvoEarly, EvoLate }), Ctx(sink, defs)).IsSuccess);

        // Granting enough XP to jump from level 1 straight to level 11 — both evolution
        // thresholds (7, 10) sit in between; both must fire exactly once.
        Assert.True(dispatcher.Dispatch(gameState, new GrantExperienceCommand(Actor, 350), Ctx(sink, defs)).IsSuccess);

        List<EvolutionTriggeredEvent> events = sink.Events.OfType<EvolutionTriggeredEvent>().ToList();
        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e.EvolutionId == EvoEarly);
        Assert.Contains(events, e => e.EvolutionId == EvoLate);
    }

    [Fact]
    public void LevelUp_Reactor_Ignores_Unregistered_Evolutions()
    {
        // Actor only has EvoLate registered. Reaching level 7 should NOT fire EvoEarly even
        // though it would otherwise qualify.
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Actor, Curve), Ctx(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorEvolutionsCommand(Actor, new[] { EvoLate }), Ctx(sink, defs)).IsSuccess);

        Assert.True(dispatcher.Dispatch(gameState, new GrantExperienceCommand(Actor, 60), Ctx(sink, defs)).IsSuccess);

        Assert.Empty(sink.Events.OfType<EvolutionTriggeredEvent>());
    }

    [Fact]
    public void LevelUp_Reactor_Skips_Manual_Trigger_Evolutions()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Actor, Curve), Ctx(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorEvolutionsCommand(Actor, new[] { EvoManual }), Ctx(sink, defs)).IsSuccess);

        // Manual evolution shouldn't auto-fire regardless of level.
        Assert.True(dispatcher.Dispatch(gameState, new GrantExperienceCommand(Actor, 350), Ctx(sink, defs)).IsSuccess);

        Assert.Empty(sink.Events.OfType<EvolutionTriggeredEvent>());
    }

    [Fact]
    public void Save_RoundTrip_Preserves_Eligibility()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorEvolutionsCommand(Actor, new[] { EvoEarly, EvoLate }), Ctx(sink, defs)).IsSuccess);

        JsonGameStateSerializer serializer = new();
        string json = serializer.Serialize(GameStateSnapshotMapper.Capture(gameState));
        GameStateSnapshot decoded = serializer.Deserialize(json);

        GameState rebuilt = new();
        GameStateSnapshotMapper.Apply(rebuilt, decoded);

        Assert.True(rebuilt.EvolutionState.IsEligible(Actor, EvoEarly));
        Assert.True(rebuilt.EvolutionState.IsEligible(Actor, EvoLate));
    }


    private static (GameState, CommandDispatcher, InMemoryGameDefinitionCatalog, InMemoryDomainEventSink) CreateWorld()
    {
        GameState gameState = new();
        InMemoryGameDefinitionCatalog defs = new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition("currency.gold", 999))
            .AddExperienceCurve(new ExperienceCurveDefinition(
                id: Curve,
                xpThresholds: new long[] { 10, 20, 30, 40, 50, 60, 80, 110, 150, 200, 300 },
                displayName: "Short Curve"))
            .AddEvolution(new EvolutionDefinition(
                id: EvoEarly,
                trigger: EvolutionTrigger.LevelUp,
                requiredLevel: 7,
                displayName: "Caterpie → Metapod",
                evolvedSpeciesId: "species.metapod"))
            .AddEvolution(new EvolutionDefinition(
                id: EvoLate,
                trigger: EvolutionTrigger.LevelUp,
                requiredLevel: 10,
                displayName: "Metapod → Butterfree",
                evolvedSpeciesId: "species.butterfree"))
            .AddEvolution(new EvolutionDefinition(
                id: EvoManual,
                trigger: EvolutionTrigger.Manual,
                displayName: "Eevee + Fire Stone",
                evolvedSpeciesId: "species.flareon"));

        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();
        return (gameState, dispatcher, defs, sink);
    }

    private static CommandContext Ctx(InMemoryDomainEventSink sink, IGameDefinitionCatalog defs)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 1, sequence: 1),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            defs);
    }
}
