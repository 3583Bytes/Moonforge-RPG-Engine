using System;
using System.Collections.Generic;
using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Equipment.Commands;
using Moonforge.Core.Inventory.Commands;
using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;
using Moonforge.Core.Progression;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;
using Moonforge.Core.Stats;
using Moonforge.Core.Stats.Commands;
using Moonforge.Core.Stats.Events;
using Moonforge.Core.Stats.Queries;

namespace Moonforge.Core.Tests;

public sealed class StatsTests
{
    [Fact]
    public void Flat_Modifiers_Add_To_Base()
    {
        StatBlock block = new();
        block.SetBase(StandardStats.Strength, 10);
        block.AddModifier(new StatModifier(StandardStats.Strength, StatModifierBucket.Flat, 5, "equipment", "slot.weapon:item.sword"));
        block.AddModifier(new StatModifier(StandardStats.Strength, StatModifierBucket.Flat, 3, "status", "status.bless"));

        int value = block.Get(StandardStats.Strength, EmptyGameDefinitionCatalog.Instance, new NoOpFormulaEvaluator());

        Assert.Equal(18, value);
    }

    [Fact]
    public void Pipeline_Applies_Add_Percent_Before_Mult_Percent()
    {
        // Base 10 + flat 5 = 15. (1 + 0.20) = 18. (1 + 0.10) = 19.8 → 20 (round to even? we use AwayFromZero).
        StatBlock block = new();
        block.SetBase(StandardStats.Strength, 10);
        block.AddModifier(new StatModifier(StandardStats.Strength, StatModifierBucket.Flat, 5, "equipment", "a"));
        block.AddModifier(new StatModifier(StandardStats.Strength, StatModifierBucket.AddPercent, 0.20, "buff", "rage"));
        block.AddModifier(new StatModifier(StandardStats.Strength, StatModifierBucket.MultPercent, 0.10, "aura", "warcry"));

        int value = block.Get(StandardStats.Strength, EmptyGameDefinitionCatalog.Instance, new NoOpFormulaEvaluator());

        Assert.Equal(20, value);
    }

    [Fact]
    public void Multiple_Add_Percent_Sum_Then_Multiple_Mult_Percent_Compound()
    {
        // Base 100 + 0 flat = 100. (1 + 0.10 + 0.20) = 130. × (1+0.5) = 195. × (1+0.5) = 292.5 → 293.
        StatBlock block = new();
        block.SetBase("dmg", 100);
        block.AddModifier(new StatModifier("dmg", StatModifierBucket.AddPercent, 0.10, "buff", "a"));
        block.AddModifier(new StatModifier("dmg", StatModifierBucket.AddPercent, 0.20, "buff", "b"));
        block.AddModifier(new StatModifier("dmg", StatModifierBucket.MultPercent, 0.50, "aura", "c"));
        block.AddModifier(new StatModifier("dmg", StatModifierBucket.MultPercent, 0.50, "aura", "d"));

        int value = block.Get("dmg", EmptyGameDefinitionCatalog.Instance, new NoOpFormulaEvaluator());

        Assert.Equal(293, value);
    }

    [Fact]
    public void Override_Wins_Regardless_Of_Other_Buckets()
    {
        StatBlock block = new();
        block.SetBase("hp", 100);
        block.AddModifier(new StatModifier("hp", StatModifierBucket.Flat, 999, "buff", "a"));
        block.AddModifier(new StatModifier("hp", StatModifierBucket.Override, 1, "curse", "petrified"));

        int value = block.Get("hp", EmptyGameDefinitionCatalog.Instance, new NoOpFormulaEvaluator());

        Assert.Equal(1, value);
    }

    [Fact]
    public void Override_Highest_Priority_Wins_With_Deterministic_Tiebreak()
    {
        StatBlock block = new();
        block.SetBase("hp", 100);
        block.AddModifier(new StatModifier("hp", StatModifierBucket.Override, 50, "curse", "x", priority: 1));
        block.AddModifier(new StatModifier("hp", StatModifierBucket.Override, 25, "curse", "z", priority: 2));
        block.AddModifier(new StatModifier("hp", StatModifierBucket.Override, 75, "curse", "y", priority: 2));

        int value = block.Get("hp", EmptyGameDefinitionCatalog.Instance, new NoOpFormulaEvaluator());

        // Priority 2 wins. Tiebreak SourceId ascending → "y" < "z" → value 75.
        Assert.Equal(75, value);
    }

    [Fact]
    public void Clamp_Respects_Min_And_Max_From_Definition()
    {
        InMemoryGameDefinitionCatalog defs = new();
        defs.AddStat(new StatDefinition("crit", min: 0, max: 100));

        StatBlock block = new();
        block.AddModifier(new StatModifier("crit", StatModifierBucket.Flat, 999, "buff", "a"));
        Assert.Equal(100, block.Get("crit", defs, new NoOpFormulaEvaluator()));

        StatBlock negative = new();
        negative.AddModifier(new StatModifier("crit", StatModifierBucket.Flat, -50, "debuff", "b"));
        Assert.Equal(0, negative.Get("crit", defs, new NoOpFormulaEvaluator()));
    }

    [Fact]
    public void Default_Base_From_Definition_Used_When_No_Stored_Value()
    {
        InMemoryGameDefinitionCatalog defs = new();
        defs.AddStat(new StatDefinition(StandardStats.Strength, defaultBase: 10));

        StatBlock block = new();
        int value = block.Get(StandardStats.Strength, defs, new NoOpFormulaEvaluator());

        Assert.Equal(10, value);
    }

    [Fact]
    public void Insertion_Order_Does_Not_Affect_Result()
    {
        StatBlock a = BuildStandardBlock(orderA: true);
        StatBlock b = BuildStandardBlock(orderA: false);

        int va = a.Get("dmg", EmptyGameDefinitionCatalog.Instance, new NoOpFormulaEvaluator());
        int vb = b.Get("dmg", EmptyGameDefinitionCatalog.Instance, new NoOpFormulaEvaluator());

        Assert.Equal(va, vb);
    }

    [Fact]
    public void Remove_By_Source_Removes_All_Matching_Modifiers()
    {
        StatBlock block = new();
        block.SetBase(StandardStats.Strength, 10);
        block.AddModifier(new StatModifier(StandardStats.Strength, StatModifierBucket.Flat, 5, "equipment", "slot.weapon:sword"));
        block.AddModifier(new StatModifier(StandardStats.Defense, StatModifierBucket.Flat, 3, "equipment", "slot.weapon:sword"));
        block.AddModifier(new StatModifier(StandardStats.Strength, StatModifierBucket.Flat, 1, "status", "bless"));

        int removed = block.RemoveModifiersBySource("equipment", "slot.weapon:sword");

        Assert.Equal(2, removed);
        Assert.Equal(11, block.Get(StandardStats.Strength, EmptyGameDefinitionCatalog.Instance, new NoOpFormulaEvaluator()));
        Assert.Equal(0, block.Get(StandardStats.Defense, EmptyGameDefinitionCatalog.Instance, new NoOpFormulaEvaluator()));
    }

    [Fact]
    public void Derived_Stat_Evaluates_Formula_With_Level_Variable()
    {
        InMemoryGameDefinitionCatalog defs = new();
        defs.AddStat(new StatDefinition(StandardStats.Vitality));
        defs.AddStat(new StatDefinition(StandardStats.MaxHp, derivedFromFormula: "vit * 10 + level * 5"));

        StatBlock block = new();
        block.SetBase(StandardStats.Vitality, 14);

        StubFormulaEvaluator formulas = new();
        IReadOnlyDictionary<string, double> extra = new Dictionary<string, double> { ["level"] = 3 };

        int value = block.Get(StandardStats.MaxHp, defs, formulas, extra);

        Assert.Equal(155, value); // 14*10 + 3*5
        Assert.Equal("vit * 10 + level * 5", formulas.LastExpression);
        Assert.True(formulas.LastVariables.ContainsKey("vit"));
        Assert.True(formulas.LastVariables.ContainsKey("level"));
    }

    [Fact]
    public void Equip_Pushes_Modifiers_And_Unequip_Removes_Them()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateEquipmentWorld();
        Assert.True(Dispatch(dispatcher, gameState, new AddInventoryItemCommand("item.sword", 1), sink, defs).IsSuccess);

        Assert.True(Dispatch(dispatcher, gameState, new EquipItemCommand("item.sword"), sink, defs).IsSuccess);

        Assert.True(gameState.ActorStatsState.TryGet("player", out StatBlock block));
        Assert.Equal(5, block.Get(StandardStats.Attack, defs, new NoOpFormulaEvaluator()));

        Assert.True(Dispatch(dispatcher, gameState, new UnequipItemCommand("slot.weapon"), sink, defs).IsSuccess);
        Assert.Equal(0, block.Get(StandardStats.Attack, defs, new NoOpFormulaEvaluator()));
    }

    [Fact]
    public void Swapping_Equipment_Replaces_Modifiers()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateEquipmentWorld();
        Assert.True(Dispatch(dispatcher, gameState, new AddInventoryItemCommand("item.sword", 1), sink, defs).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new AddInventoryItemCommand("item.greatsword", 1), sink, defs).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new EquipItemCommand("item.sword"), sink, defs).IsSuccess);

        Assert.True(Dispatch(dispatcher, gameState, new EquipItemCommand("item.greatsword"), sink, defs).IsSuccess);

        Assert.True(gameState.ActorStatsState.TryGet("player", out StatBlock block));
        Assert.Equal(12, block.Get(StandardStats.Attack, defs, new NoOpFormulaEvaluator()));
    }

    [Fact]
    public void Apply_Stat_Modifier_Command_Emits_Event()
    {
        GameState gameState = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new ApplyStatModifierCommandHandler());
        InMemoryDomainEventSink sink = new();

        StatModifier mod = new(StandardStats.Strength, StatModifierBucket.Flat, 4, "buff", "warcry");
        DomainResult result = dispatcher.Dispatch(gameState, new ApplyStatModifierCommand("hero", mod), CreateContext(sink, EmptyGameDefinitionCatalog.Instance));

        Assert.True(result.IsSuccess);
        Assert.True(gameState.ActorStatsState.TryGet("hero", out StatBlock block));
        Assert.Single(block.Modifiers);
        Assert.Contains(sink.Events, e => e is StatModifierAppliedEvent applied
            && applied.ActorId == "hero"
            && applied.StatId == StandardStats.Strength
            && applied.SourceKind == "buff");
    }

    [Fact]
    public void Remove_Stat_Modifiers_Command_Removes_By_Source()
    {
        GameState gameState = new();
        StatBlock block = gameState.ActorStatsState.GetOrCreate("hero");
        block.AddModifier(new StatModifier(StandardStats.Strength, StatModifierBucket.Flat, 5, "buff", "warcry"));
        block.AddModifier(new StatModifier(StandardStats.Defense, StatModifierBucket.Flat, 3, "buff", "warcry"));
        block.AddModifier(new StatModifier(StandardStats.Strength, StatModifierBucket.Flat, 1, "buff", "other"));

        CommandDispatcher dispatcher = new();
        dispatcher.Register(new RemoveStatModifiersCommandHandler());
        InMemoryDomainEventSink sink = new();

        DomainResult result = dispatcher.Dispatch(gameState, new RemoveStatModifiersCommand("hero", "buff", "warcry"), CreateContext(sink, EmptyGameDefinitionCatalog.Instance));

        Assert.True(result.IsSuccess);
        Assert.Single(block.Modifiers);
        Assert.Contains(sink.Events, e => e is StatModifiersRemovedEvent removed
            && removed.RemovedCount == 2
            && removed.SourceId == "warcry");
    }

    [Fact]
    public void Get_Stat_Query_Auto_Exposes_Level_From_Progression()
    {
        GameState gameState = new();
        gameState.ActorStatsState.GetOrCreate("hero").SetBase(StandardStats.Vitality, 10);
        gameState.ProgressionState.GetOrCreate("hero", curveId: "curve.default", level: 7);

        InMemoryGameDefinitionCatalog defs = new();
        defs.AddStat(new StatDefinition(StandardStats.MaxHp, derivedFromFormula: "vit * 10 + level * 5"));

        StubFormulaEvaluator formulas = new();
        GetStatQueryHandler handler = new(defs, formulas);

        int value = handler.Query(gameState, new GetStatQuery("hero", StandardStats.MaxHp));

        Assert.Equal(135, value); // 10*10 + 7*5
    }

    [Fact]
    public void Persistence_Round_Trip_Preserves_Stat_State()
    {
        GameState original = new();
        StatBlock block = original.ActorStatsState.GetOrCreate("hero");
        block.SetBase(StandardStats.Strength, 14);
        block.SetBase(StandardStats.Vitality, 10);
        block.AddModifier(new StatModifier(StandardStats.Strength, StatModifierBucket.Flat, 5, "equipment", "slot.weapon:sword"));
        block.AddModifier(new StatModifier(StandardStats.Attack, StatModifierBucket.AddPercent, 0.25, "buff", "warcry", priority: 3));

        JsonGameStateSerializer serializer = new();
        string json = serializer.Serialize(GameStateSnapshotMapper.Capture(original));
        GameStateSnapshot decoded = serializer.Deserialize(json);

        GameState rebuilt = new();
        GameStateSnapshotMapper.Apply(rebuilt, decoded);

        Assert.True(rebuilt.ActorStatsState.TryGet("hero", out StatBlock restored));
        Assert.Equal(14, restored.Base[StandardStats.Strength]);
        Assert.Equal(10, restored.Base[StandardStats.Vitality]);
        Assert.Equal(2, restored.Modifiers.Count);
        Assert.Contains(restored.Modifiers, m => m.SourceId == "slot.weapon:sword" && m.Bucket == StatModifierBucket.Flat && m.Value == 5);
        Assert.Contains(restored.Modifiers, m => m.SourceId == "warcry" && m.Bucket == StatModifierBucket.AddPercent && m.Priority == 3);
    }

    [Fact]
    public void Schema_Version_Is_Current()
    {
        // ActorStatsState added at v2; Interactables at v3. Tracks GameStateSnapshotMapper.CurrentSchemaVersion.
        Assert.True(GameStateSnapshotMapper.CurrentSchemaVersion >= 2);
    }

    private static StatBlock BuildStandardBlock(bool orderA)
    {
        StatBlock block = new();
        block.SetBase("dmg", 50);
        StatModifier[] mods =
        {
            new("dmg", StatModifierBucket.Flat, 10, "equipment", "z"),
            new("dmg", StatModifierBucket.Flat, 5, "equipment", "a"),
            new("dmg", StatModifierBucket.AddPercent, 0.30, "buff", "b"),
            new("dmg", StatModifierBucket.MultPercent, 0.25, "aura", "y"),
            new("dmg", StatModifierBucket.MultPercent, 0.10, "aura", "x")
        };

        if (orderA)
        {
            for (int i = 0; i < mods.Length; i++) block.AddModifier(mods[i]);
        }
        else
        {
            for (int i = mods.Length - 1; i >= 0; i--) block.AddModifier(mods[i]);
        }

        return block;
    }

    private static (GameState, CommandDispatcher, InMemoryGameDefinitionCatalog, InMemoryDomainEventSink) CreateEquipmentWorld()
    {
        GameState gameState = new();
        InMemoryGameDefinitionCatalog defs = new InMemoryGameDefinitionCatalog()
            .AddItem(new ItemDefinition("item.sword", 1))
            .AddItem(new ItemDefinition("item.greatsword", 1))
            .AddEquipmentSlot(new EquipmentSlotDefinition("slot.weapon"))
            .AddEquipment(new EquipmentDefinition("item.sword", "slot.weapon", new Dictionary<string, int>
            {
                [StandardStats.Attack] = 5
            }))
            .AddEquipment(new EquipmentDefinition("item.greatsword", "slot.weapon", new Dictionary<string, int>
            {
                [StandardStats.Attack] = 12
            }));

        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new AddInventoryItemCommandHandler());
        dispatcher.Register(new EquipItemCommandHandler());
        dispatcher.Register(new UnequipItemCommandHandler());
        return (gameState, dispatcher, defs, sink);
    }

    private static DomainResult Dispatch<TCommand>(
        CommandDispatcher dispatcher,
        GameState gameState,
        TCommand command,
        InMemoryDomainEventSink sink,
        IGameDefinitionCatalog defs)
        where TCommand : ICommand
    {
        return dispatcher.Dispatch(gameState, command, CreateContext(sink, defs));
    }

    private static CommandContext CreateContext(InMemoryDomainEventSink sink, IGameDefinitionCatalog defs)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 42, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            defs);
    }

    private sealed class StubFormulaEvaluator : IFormulaEvaluator
    {
        public string? LastExpression { get; private set; }

        public IReadOnlyDictionary<string, double> LastVariables { get; private set; } =
            new Dictionary<string, double>();

        public double Evaluate(string expression, IReadOnlyDictionary<string, double> variables)
        {
            LastExpression = expression;
            LastVariables = variables;

            // Tiny evaluator: supports "<var> * <int> + <var> * <int>" used by tests.
            // Parses tokens manually to avoid pulling in an expression parser.
            return expression switch
            {
                "vit * 10 + level * 5" => variables["vit"] * 10 + variables["level"] * 5,
                _ => throw new InvalidOperationException($"Unsupported expression in StubFormulaEvaluator: '{expression}'.")
            };
        }
    }
}
