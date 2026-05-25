namespace Moonforge.Core.Combat;

/// <summary>
/// Recommended string keys for built-in damage types. Games may use other keys; these
/// constants exist to encourage a consistent vocabulary across modules. Register the types
/// you need with the game definition catalog before they affect damage resolution.
/// </summary>
public static class StandardDamageTypes
{
    public const string Physical = "physical";
    public const string Magical = "magical";

    public const string Fire = "fire";
    public const string Ice = "ice";
    public const string Lightning = "lightning";
    public const string Earth = "earth";
    public const string Water = "water";
    public const string Wind = "wind";

    public const string Holy = "holy";
    public const string Dark = "dark";
    public const string Poison = "poison";

    /// <summary>Bypasses all resistances and flat defenses. Useful for scripted execution damage.</summary>
    public const string True = "true";
}
