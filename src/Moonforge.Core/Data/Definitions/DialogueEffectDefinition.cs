namespace Moonforge.Core.Data.Definitions;

public sealed class DialogueEffectDefinition
{
    public DialogueEffectDefinition(
        DialogueEffectType effectType,
        string key,
        bool boolValue = false,
        int intValue = 0)
    {
        EffectType = effectType;
        Key = key;
        BoolValue = boolValue;
        IntValue = intValue;
    }

    public DialogueEffectType EffectType { get; }

    public string Key { get; }

    public bool BoolValue { get; }

    public int IntValue { get; }
}
