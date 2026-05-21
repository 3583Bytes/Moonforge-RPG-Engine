using System.Collections.Generic;
using System.Linq;
using Moonforge.Core;
using Moonforge.Core.Bestiary;
using Moonforge.Core.Bestiary.Commands;
using Moonforge.Core.Bestiary.Events;
using Moonforge.Core.Combat;
using Moonforge.Core.Combat.Commands;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Party.Commands;
using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class BestiaryTests
{
    private const string Pidgey = "species.pidgey";
    private const string Rattata = "species.rattata";
    private const string Hero = "party.hero";
    private const string EnemyA = "enemy.pidgey.001";
    private const string EnemyB = "enemy.rattata.001";

    [Fact]
    public void BattleStarted_Auto_Tracks_Enemy_Species()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartBattle(pidgeySpecies: Pidgey, rattataSpecies: Rattata, clockMinutes: 42);

        Assert.True(gameState.BestiaryState.TryGet(Pidgey, out BestiaryEntry pidgey));
        Assert.Equal(1, pidgey.EncounterCount);
        Assert.Equal(42, pidgey.FirstEncounteredAtMinutes);
        Assert.True(gameState.BestiaryState.TryGet(Rattata, out BestiaryEntry rattata));
        Assert.Equal(1, rattata.EncounterCount);
        Assert.Equal(2, sink.Events.OfType<SpeciesFirstEncounteredEvent>().Count());
    }

    [Fact]
    public void Untagged_Enemies_Are_Ignored()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartBattle(pidgeySpecies: null, rattataSpecies: null, clockMinutes: 0);

        Assert.Empty(gameState.BestiaryState.Entries);
        Assert.Empty(sink.Events.OfType<SpeciesFirstEncounteredEvent>());
    }

    [Fact]
    public void Repeated_Encounters_Increment_Count_But_Fire_FirstEvent_Once()
    {
        // Battle 1
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartBattle(pidgeySpecies: Pidgey, rattataSpecies: Rattata, clockMinutes: 10);
        // Tear down active battle to legally start a second one in the same gameState.
        gameState.ActiveBattle!.Status = BattleStatus.Victory;
        gameState.ActiveBattle = null;

        // Battle 2 — same species again
        Assert.True(dispatcher.Dispatch(gameState, BuildStartBattleCommand("battle.2", Pidgey, Rattata), Ctx(sink, clockMinutes: 99)).IsSuccess);

        Assert.True(gameState.BestiaryState.TryGet(Pidgey, out BestiaryEntry pidgey));
        Assert.Equal(2, pidgey.EncounterCount);
        Assert.Equal(10, pidgey.FirstEncounteredAtMinutes); // unchanged
        Assert.Single(sink.Events.OfType<SpeciesFirstEncounteredEvent>(), e => e.SpeciesId == Pidgey);
    }

    [Fact]
    public void Capture_Marks_Species_Captured_And_Fires_First_Event()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartBattle(pidgeySpecies: Pidgey, rattataSpecies: Rattata, clockMinutes: 5);
        Assert.True(dispatcher.Dispatch(gameState, new ConfigurePartyCommand(maxActive: 1, maxRoster: 6), Ctx(sink, 5)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand(Hero, active: true), Ctx(sink, 5)).IsSuccess);

        // Force HP low so guaranteed-capture math works (bonusPercent 200 caps chance at 100).
        gameState.ActiveBattle!.Actors[EnemyA].Hp = 1;
        gameState.ActiveBattle!.Actors[EnemyA].CaptureBaseRate = 100;

        Assert.True(dispatcher.Dispatch(gameState, new AttemptCaptureCommand(Hero, EnemyA, bonusPercent: 200), Ctx(sink, 5)).IsSuccess);

        Assert.True(gameState.BestiaryState.TryGet(Pidgey, out BestiaryEntry pidgey));
        Assert.Equal(1, pidgey.CaptureCount);
        Assert.Equal(5, pidgey.FirstCapturedAtMinutes);
        Assert.Single(sink.Events.OfType<SpeciesFirstCapturedEvent>(), e => e.SpeciesId == Pidgey);
    }

    [Fact]
    public void MarkSpeciesObservedCommand_Records_Manual_Reveal()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();

        Assert.True(dispatcher.Dispatch(gameState, new MarkSpeciesObservedCommand(Pidgey, encountered: true, captured: true), Ctx(sink, defs, clockMinutes: 100)).IsSuccess);

        Assert.True(gameState.BestiaryState.TryGet(Pidgey, out BestiaryEntry entry));
        Assert.True(entry.IsEncountered);
        Assert.True(entry.IsCaptured);
        Assert.Equal(100, entry.FirstEncounteredAtMinutes);
        Assert.Equal(100, entry.FirstCapturedAtMinutes);
    }

    [Fact]
    public void Save_RoundTrip_Preserves_Bestiary()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new MarkSpeciesObservedCommand(Pidgey, encountered: true, captured: true), Ctx(sink, defs, clockMinutes: 100)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new MarkSpeciesObservedCommand(Rattata, encountered: true, captured: false), Ctx(sink, defs, clockMinutes: 200)).IsSuccess);

        JsonGameStateSerializer serializer = new();
        string json = serializer.Serialize(GameStateSnapshotMapper.Capture(gameState));
        GameStateSnapshot decoded = serializer.Deserialize(json);
        GameState rebuilt = new();
        GameStateSnapshotMapper.Apply(rebuilt, decoded);

        Assert.True(rebuilt.BestiaryState.TryGet(Pidgey, out BestiaryEntry pidgey));
        Assert.Equal(1, pidgey.EncounterCount);
        Assert.Equal(1, pidgey.CaptureCount);
        Assert.Equal(100, pidgey.FirstEncounteredAtMinutes);
        Assert.True(rebuilt.BestiaryState.TryGet(Rattata, out BestiaryEntry rattata));
        Assert.Equal(1, rattata.EncounterCount);
        Assert.Equal(0, rattata.CaptureCount);
    }

    private static (GameState, CommandDispatcher, InMemoryDomainEventSink) StartBattle(string? pidgeySpecies, string? rattataSpecies, long clockMinutes)
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();
        Assert.True(dispatcher.Dispatch(gameState, BuildStartBattleCommand("battle.1", pidgeySpecies, rattataSpecies), Ctx(sink, clockMinutes)).IsSuccess);
        return (gameState, dispatcher, sink);
    }

    private static (GameState, CommandDispatcher, InMemoryGameDefinitionCatalog, InMemoryDomainEventSink) CreateWorld()
    {
        GameState gameState = new();
        InMemoryGameDefinitionCatalog defs = new InMemoryGameDefinitionCatalog().AddCurrency(new CurrencyDefinition("currency.gold", 999));
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();
        return (gameState, dispatcher, defs, sink);
    }

    private static StartBattleCommand BuildStartBattleCommand(string battleId, string? speciesA, string? speciesB)
    {
        List<BattleSkillDefinition> skills = [new BattleSkillDefinition("skill.tackle", BattleSkillEffectType.PhysicalDamage, power: 5)];
        List<BattleActorDefinition> actors =
        [
            new BattleActorDefinition(
                actorId: Hero,
                displayName: "Hero",
                faction: CombatFaction.Party,
                maxHp: 30, atk: 7, def: 4, matk: 4, mdef: 3,
                initiative: 20,
                skillIds: ["skill.tackle"],
                playerControlled: true),
            new BattleActorDefinition(
                actorId: EnemyA,
                displayName: "Pidgey",
                faction: CombatFaction.Enemy,
                maxHp: 20, atk: 5, def: 3, matk: 3, mdef: 2,
                initiative: 10,
                skillIds: ["skill.tackle"],
                playerControlled: false,
                speciesId: speciesA),
            new BattleActorDefinition(
                actorId: EnemyB,
                displayName: "Rattata",
                faction: CombatFaction.Enemy,
                maxHp: 18, atk: 6, def: 2, matk: 2, mdef: 1,
                initiative: 8,
                skillIds: ["skill.tackle"],
                playerControlled: false,
                speciesId: speciesB)
        ];

        return new StartBattleCommand(battleId, actors, skills, seed: 7, sequence: 1);
    }

    private static CommandContext Ctx(InMemoryDomainEventSink sink, long clockMinutes)
    {
        return Ctx(sink, new InMemoryGameDefinitionCatalog().AddCurrency(new CurrencyDefinition("currency.gold", 999)), clockMinutes);
    }

    private static CommandContext Ctx(InMemoryDomainEventSink sink, IGameDefinitionCatalog defs, long clockMinutes)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 1, sequence: 1),
            new SimulationClock(clockMinutes),
            new NoOpFormulaEvaluator(),
            sink,
            defs);
    }
}
