using System;
using System.Globalization;

namespace Moonforge.Core.World;

public sealed class WorldVariableValue
{
    private WorldVariableValue(WorldVariableKind kind, object value)
    {
        Kind = kind;
        Value = value;
    }

    public WorldVariableKind Kind { get; }

    public object Value { get; }

    public static WorldVariableValue FromBool(bool value) => new(WorldVariableKind.Bool, value);

    public static WorldVariableValue FromInt(int value) => new(WorldVariableKind.Int, value);

    public static WorldVariableValue FromFloat(double value) => new(WorldVariableKind.Float, value);

    public static WorldVariableValue FromString(string value) => new(WorldVariableKind.String, value ?? string.Empty);

    public bool TryGetBool(out bool value)
    {
        if (Kind == WorldVariableKind.Bool)
        {
            value = (bool)Value;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetInt(out int value)
    {
        if (Kind == WorldVariableKind.Int)
        {
            value = (int)Value;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetFloat(out double value)
    {
        if (Kind == WorldVariableKind.Float)
        {
            value = (double)Value;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetString(out string value)
    {
        if (Kind == WorldVariableKind.String)
        {
            value = (string)Value;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public override string ToString()
    {
        return Convert.ToString(Value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
