using Moonforge.Core;
using Moonforge.Core.World;
using Moonforge.Core.World.Conditions;

namespace Moonforge.Core.Tests;

public sealed class WorldConditionTests
{
    [Fact]
    public void Equals_Condition_Matches_Exact_Typed_Value()
    {
        GameState gameState = new();
        gameState.WorldState.Set("quest.side.blacksmith.completed", WorldVariableValue.FromBool(true));

        WorldVariableEqualsCondition condition = new(
            "quest.side.blacksmith.completed",
            WorldVariableValue.FromBool(true));

        Assert.True(condition.Evaluate(gameState));
    }

    [Fact]
    public void Numeric_Condition_Compares_Int_And_Float()
    {
        GameState gameState = new();
        gameState.WorldState.Set("player.level", WorldVariableValue.FromInt(12));
        gameState.WorldState.Set("world.threat", WorldVariableValue.FromFloat(3.5));

        WorldVariableNumberCondition levelCondition = new(
            "player.level",
            NumericComparisonOperator.GreaterThanOrEqual,
            10);

        WorldVariableNumberCondition threatCondition = new(
            "world.threat",
            NumericComparisonOperator.LessThan,
            5);

        Assert.True(levelCondition.Evaluate(gameState));
        Assert.True(threatCondition.Evaluate(gameState));
    }

    [Fact]
    public void Composite_Conditions_Respect_And_Or_Semantics()
    {
        GameState gameState = new();
        gameState.WorldState.Set("quest.main.started", WorldVariableValue.FromBool(true));
        gameState.WorldState.Set("player.level", WorldVariableValue.FromInt(4));

        ICondition questStarted = new WorldVariableEqualsCondition(
            "quest.main.started",
            WorldVariableValue.FromBool(true));

        ICondition highEnoughLevel = new WorldVariableNumberCondition(
            "player.level",
            NumericComparisonOperator.GreaterThanOrEqual,
            5);

        AnyCondition any = new([questStarted, highEnoughLevel]);
        AllCondition all = new([questStarted, highEnoughLevel]);

        Assert.True(any.Evaluate(gameState));
        Assert.False(all.Evaluate(gameState));
    }
}
