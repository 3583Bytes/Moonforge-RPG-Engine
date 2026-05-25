using System;

namespace Moonforge.Core.World.Conditions;

public sealed class WorldVariableEqualsCondition : ICondition
{
    public WorldVariableEqualsCondition(string key, WorldVariableValue expectedValue)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        ExpectedValue = expectedValue ?? throw new ArgumentNullException(nameof(expectedValue));
    }

    public string Key { get; }

    public WorldVariableValue ExpectedValue { get; }

    public bool Evaluate(GameState gameState)
    {
        if (!gameState.WorldState.TryGet(Key, out WorldVariableValue value))
        {
            return false;
        }

        if (value.Kind != ExpectedValue.Kind)
        {
            return false;
        }

        return Equals(value.Value, ExpectedValue.Value);
    }
}
