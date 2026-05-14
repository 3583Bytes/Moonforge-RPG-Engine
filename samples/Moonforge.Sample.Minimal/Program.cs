using System.Collections.Generic;
using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;
using Moonforge.Core.Stats;
using Moonforge.Core.Stats.Commands;
using Moonforge.Core.Stats.Queries;
using Moonforge.Core.World;
using Moonforge.Core.World.Commands;
using Moonforge.Core.World.Queries;

GameState gameState = new();
InMemoryDomainEventSink sink = new();

// Register MaxHp as a derived stat: MaxHp = VIT * 10 + level * 5
InMemoryGameDefinitionCatalog definitions = new();
definitions.AddStat(new StatDefinition(StandardStats.Vitality));
definitions.AddStat(new StatDefinition(StandardStats.MaxHp, derivedFromFormula: "vit * 10 + level * 5"));

CommandContext context = new(
    new Pcg32RandomSource(seed: 1234, sequence: 54),
    new SimulationClock(0),
    new HardcodedMaxHpEvaluator(),
    sink,
    definitions);

CommandDispatcher dispatcher = new();
dispatcher.Register(new SetWorldVariableCommandHandler());
dispatcher.Register(new SetStatBaseCommandHandler());
dispatcher.Register(new ApplyStatModifierCommandHandler());

DomainResult worldResult = dispatcher.Dispatch(
    gameState,
    new SetWorldVariableCommand("quest.main.started", WorldVariableValue.FromBool(true)),
    context);
if (!worldResult.IsSuccess)
{
    System.Console.WriteLine($"World dispatch failed: {worldResult.Error?.Code} - {worldResult.Error?.Message}");
    return;
}

// Establish hero's base stats and progression, then apply a buff.
gameState.ProgressionState.GetOrCreate("hero", curveId: "curve.default", level: 5);
_ = dispatcher.Dispatch(gameState, new SetStatBaseCommand("hero", StandardStats.Vitality, 12), context);
_ = dispatcher.Dispatch(gameState, new SetStatBaseCommand("hero", StandardStats.Attack, 20), context);
_ = dispatcher.Dispatch(
    gameState,
    new ApplyStatModifierCommand("hero", new StatModifier(StandardStats.Attack, StatModifierBucket.AddPercent, 0.25, "buff", "warcry")),
    context);

GetStatQueryHandler statQuery = new(definitions, context.FormulaEvaluator);
int maxHp = statQuery.Query(gameState, new GetStatQuery("hero", StandardStats.MaxHp));
int attack = statQuery.Query(gameState, new GetStatQuery("hero", StandardStats.Attack));

GetWorldVariableQueryHandler worldQuery = new();
WorldVariableValue? value = worldQuery.Query(gameState, new GetWorldVariableQuery("quest.main.started"));
if (value is null || !value.TryGetBool(out bool started))
{
    System.Console.WriteLine("World variable was not set as expected.");
    return;
}

System.Console.WriteLine($"Quest started: {started}");
System.Console.WriteLine($"Hero MaxHp (derived, lvl 5, vit 12): {maxHp}"); // 12*10 + 5*5 = 145
System.Console.WriteLine($"Hero Attack (base 20 + 25% buff): {attack}");   // 20 * 1.25 = 25
System.Console.WriteLine($"Events published: {sink.Events.Count}");

sealed class HardcodedMaxHpEvaluator : IFormulaEvaluator
{
    public double Evaluate(string expression, IReadOnlyDictionary<string, double> variables)
    {
        return expression switch
        {
            "vit * 10 + level * 5" => variables["vit"] * 10 + variables["level"] * 5,
            _ => 0
        };
    }
}
