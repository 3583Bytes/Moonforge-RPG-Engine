namespace Moonforge.Sample.MonsterCatcher.Content;

/// <summary>
/// Item id constants. Item effects are inferred from the id prefix at use-time (see
/// <see cref="Items.EffectFor"/>) — the engine treats items as opaque ids, the sample
/// owns the behavior.
/// </summary>
internal static class ItemIds
{
    // Capture balls — bonus percent on AttemptCaptureCommand.
    public const string Pokeball   = "item.ball.pokeball";    // base capture rate
    public const string Greatball  = "item.ball.greatball";   // +100% capture
    public const string Ultraball  = "item.ball.ultraball";   // +300% capture

    // Healing items.
    public const string Potion       = "item.heal.potion";       // restore 20 HP
    public const string SuperPotion  = "item.heal.superpotion";  // restore 60 HP
    public const string MaxPotion    = "item.heal.maxpotion";    // restore full HP

    // Revival.
    public const string Revive       = "item.heal.revive";       // revive fainted at 50% HP
}
