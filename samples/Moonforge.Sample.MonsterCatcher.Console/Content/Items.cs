using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;

namespace Moonforge.Sample.MonsterCatcher.Content;

/// <summary>
/// One in-game item — engine-side metadata (display + price) plus sample-side runtime
/// effect (kind + magnitude). <see cref="EngineDefinition"/> is what gets registered with
/// the catalog; <see cref="Kind"/> and <see cref="Magnitude"/> are read by the sample's
/// item-use handler.
/// </summary>
internal sealed class Item
{
    public Item(
        string id,
        string displayName,
        string description,
        ItemKind kind,
        int magnitude,
        long buyPrice,
        long sellPrice = 0)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        Kind = kind;
        Magnitude = magnitude;
        BuyPrice = buyPrice;
        SellPrice = sellPrice;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public ItemKind Kind { get; }

    /// <summary>
    /// Effect magnitude — for balls, the capture bonusPercent; for potions, HP healed;
    /// for revive, the HP fraction restored (50 = 50%); for max-potion ignored.
    /// </summary>
    public int Magnitude { get; }

    public long BuyPrice { get; }

    public long SellPrice { get; }

    public ItemDefinition EngineDefinition => new ItemDefinition(
        id: Id,
        stackLimit: 99,
        buyPriceOptions: new[]
        {
            new PriceOptionDefinition(new[]
            {
                new PriceComponentDefinition(ContentCatalog.CurrencyGold, BuyPrice)
            })
        },
        sellPrice: SellPrice > 0
            ? new[] { new PriceComponentDefinition(ContentCatalog.CurrencyGold, SellPrice) }
            : System.Array.Empty<PriceComponentDefinition>(),
        displayName: DisplayName,
        description: Description);
}

internal enum ItemKind
{
    CaptureBall,
    HealHp,           // Magnitude = HP healed
    FullRestoreHp,    // Restore to MaxHp
    Revive            // Magnitude = HP percentage to restore on revive
}

internal static class Items
{
    public static IReadOnlyList<Item> All { get; } = new List<Item>
    {
        // Capture balls — each subsequent tier multiplies effective capture chance.
        new Item(ItemIds.Pokeball,   "Pokeball",     "A basic capture ball.",            ItemKind.CaptureBall,    0,    100,  50),
        new Item(ItemIds.Greatball,  "Greatball",    "Doubles the base capture chance.", ItemKind.CaptureBall,  100,    300, 150),
        new Item(ItemIds.Ultraball,  "Ultraball",    "Quadruples the base capture chance.", ItemKind.CaptureBall, 300,  800, 400),

        // Healing.
        new Item(ItemIds.Potion,      "Potion",      "Heals 20 HP.",                     ItemKind.HealHp,         20,    50,  20),
        new Item(ItemIds.SuperPotion, "Super Potion","Heals 60 HP.",                     ItemKind.HealHp,         60,   200,  80),
        new Item(ItemIds.MaxPotion,   "Max Potion",  "Fully restores a monster.",        ItemKind.FullRestoreHp,   0,   800, 320),

        // Revive.
        new Item(ItemIds.Revive,      "Revive",      "Revives a fainted monster at half HP.", ItemKind.Revive,    50,  1000, 400)
    };

    private static readonly IReadOnlyDictionary<string, Item> ById = BuildLookup();

    private static IReadOnlyDictionary<string, Item> BuildLookup()
    {
        Dictionary<string, Item> map = new();
        foreach (Item i in All) map[i.Id] = i;
        return map;
    }

    public static Item Get(string itemId) => ById[itemId];

    public static bool TryGet(string itemId, out Item item)
    {
        if (ById.TryGetValue(itemId, out Item? value))
        {
            item = value;
            return true;
        }
        item = default!;
        return false;
    }
}
