using System;

namespace Moonforge.Core.World.Conditions;

public sealed class WorldVariableNumberCondition : ICondition
{
    public WorldVariableNumberCondition(string key, NumericComparisonOperator comparison, double target)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Comparison = comparison;
        Target = target;
    }

    public string Key { get; }

    public NumericComparisonOperator Comparison { get; }

    public double Target { get; }

    public bool Evaluate(GameState gameState)
    {
        if (!gameState.WorldState.TryGet(Key, out WorldVariableValue value))
        {
            return false;
        }

        double actual;
        if (value.Kind == WorldVariableKind.Int && value.TryGetInt(out int intValue))
        {
            actual = intValue;
        }
        else if (value.Kind == WorldVariableKind.Float && value.TryGetFloat(out double floatValue))
        {
            actual = floatValue;
        }
        else
        {
            return false;
        }

        switch (Comparison)
        {
            case NumericComparisonOperator.Equal:
                return actual == Target;
            case NumericComparisonOperator.NotEqual:
                return actual != Target;
            case NumericComparisonOperator.GreaterThan:
                return actual > Target;
            case NumericComparisonOperator.GreaterThanOrEqual:
                return actual >= Target;
            case NumericComparisonOperator.LessThan:
                return actual < Target;
            case NumericComparisonOperator.LessThanOrEqual:
                return actual <= Target;
            default:
                return false;
        }
    }
}
