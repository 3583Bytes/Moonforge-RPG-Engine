using System.Collections.Generic;
using System.Linq;
using Moonforge.Core.Data.Definitions;

namespace Moonforge.Sample.MonsterCatcher.Content;

/// <summary>
/// One shop definition shared across every town. Restocks are unlimited (MaxStock = null),
/// so the player can buy as much as their gold allows. Phase 4 might gate higher-tier
/// stock behind badges.
/// </summary>
internal static class TownShop
{
    public const string ShopId = "shop.town";

    public static ShopDefinition EngineDefinition { get; } = new ShopDefinition(
        id: ShopId,
        entries: Items.All
            .Select(i => new ShopEntryDefinition(i.Id))
            .ToList(),
        restockIntervalMinutes: 0);

    /// <summary>Items the player walks in with — enough to make capture a real option in early gyms.</summary>
    public static IReadOnlyList<(string itemId, int quantity)> StartingInventory { get; } = new[]
    {
        (ItemIds.Pokeball, 5),
        (ItemIds.Potion, 3)
    };
}
