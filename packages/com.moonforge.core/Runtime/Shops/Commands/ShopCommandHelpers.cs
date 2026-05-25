using System;
using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Shops.Events;

namespace Moonforge.Core.Shops.Commands;

internal static class ShopCommandHelpers
{
    public static bool TryEnsureShopAndItem(
        CommandContext context,
        string shopId,
        string itemId,
        out ShopDefinition shopDefinition,
        out ItemDefinition itemDefinition,
        out ShopEntryDefinition shopEntry,
        out string error)
    {
        if (!context.Definitions.TryGetShop(shopId, out shopDefinition))
        {
            itemDefinition = null!;
            shopEntry = null!;
            error = $"Unknown shop definition '{shopId}'.";
            return false;
        }

        if (!context.Definitions.TryGetItem(itemId, out itemDefinition))
        {
            shopEntry = null!;
            error = $"Unknown item definition '{itemId}'.";
            return false;
        }

        foreach (ShopEntryDefinition entry in shopDefinition.Entries)
        {
            if (entry.ItemId == itemId)
            {
                shopEntry = entry;
                error = string.Empty;
                return true;
            }
        }

        shopEntry = null!;
        error = $"Item '{itemId}' is not sold by shop '{shopId}'.";
        return false;
    }

    public static void EnsureRestocked(GameState gameState, ShopDefinition shopDefinition, CommandContext context)
    {
        if (shopDefinition.RestockIntervalMinutes <= 0)
        {
            return;
        }

        long now = context.Clock.CurrentSimulationMinutes;
        long? last = gameState.ShopState.GetLastRestockMinute(shopDefinition.Id);
        if (!last.HasValue)
        {
            InitializeLimitedStocks(gameState, shopDefinition);
            gameState.ShopState.SetLastRestockMinute(shopDefinition.Id, now);
            return;
        }

        if (now - last.Value < shopDefinition.RestockIntervalMinutes)
        {
            return;
        }

        bool changed = false;
        foreach (ShopEntryDefinition entry in shopDefinition.Entries)
        {
            if (!entry.MaxStock.HasValue)
            {
                continue;
            }

            gameState.ShopState.SetStock(shopDefinition.Id, entry.ItemId, entry.MaxStock.Value);
            changed = true;
        }

        gameState.ShopState.SetLastRestockMinute(shopDefinition.Id, now);
        if (changed)
        {
            context.EventSink.Publish(new ShopRestockedEvent(shopDefinition.Id, now));
        }
    }

    public static int EnsureAndGetCurrentStock(GameState gameState, ShopDefinition shopDefinition, ShopEntryDefinition entry)
    {
        if (!entry.MaxStock.HasValue)
        {
            return int.MaxValue;
        }

        return gameState.ShopState.GetOrInitializeStock(shopDefinition.Id, entry.ItemId, entry.MaxStock.Value);
    }

    public static List<CurrencyDelta> CreateCostDeltas(IReadOnlyList<PriceComponentDefinition> cost, int quantity)
    {
        List<CurrencyDelta> deltas = new();
        foreach (PriceComponentDefinition component in cost)
        {
            long scaled = checked(component.Amount * quantity);
            deltas.Add(new CurrencyDelta(component.CurrencyId, -scaled));
        }

        return deltas;
    }

    public static List<CurrencyDelta> CreateSellDeltas(IReadOnlyList<PriceComponentDefinition> sellPrice, int quantity)
    {
        List<CurrencyDelta> deltas = new();
        foreach (PriceComponentDefinition component in sellPrice)
        {
            long scaled = checked(component.Amount * quantity);
            deltas.Add(new CurrencyDelta(component.CurrencyId, scaled));
        }

        return deltas;
    }

    private static void InitializeLimitedStocks(GameState gameState, ShopDefinition shopDefinition)
    {
        foreach (ShopEntryDefinition entry in shopDefinition.Entries)
        {
            if (!entry.MaxStock.HasValue)
            {
                continue;
            }

            gameState.ShopState.GetOrInitializeStock(shopDefinition.Id, entry.ItemId, entry.MaxStock.Value);
        }
    }
}
