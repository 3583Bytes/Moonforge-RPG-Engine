using System;
using System.Collections.Generic;

namespace Moonforge.Core.World;

public sealed class WorldState
{
    private readonly Dictionary<string, WorldVariableValue> _variables = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, WorldVariableValue> Variables => _variables;

    public bool TryGet(string key, out WorldVariableValue value)
    {
        return _variables.TryGetValue(key, out value!);
    }

    public void Set(string key, WorldVariableValue value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("World variable key is required.", nameof(key));
        }

        _variables[key] = value;
    }

    public bool Remove(string key)
    {
        return _variables.Remove(key);
    }

    public void CopyFrom(WorldState source)
    {
        _variables.Clear();
        foreach ((string key, WorldVariableValue value) in source._variables)
        {
            _variables[key] = value;
        }
    }
}
