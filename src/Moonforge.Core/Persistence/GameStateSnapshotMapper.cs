using System;
using System.Collections.Generic;
using Moonforge.Core.Dialogue;
using Moonforge.Core.Economy;
using Moonforge.Core.Equipment;
using Moonforge.Core.Exploration;
using Moonforge.Core.Interactables;
using Moonforge.Core.Inventory;
using Moonforge.Core.Party;
using Moonforge.Core.Persistence.Snapshots;
using Moonforge.Core.Progression;
using Moonforge.Core.Quests;
using Moonforge.Core.Shops;
using Moonforge.Core.Stats;
using Moonforge.Core.World;

namespace Moonforge.Core.Persistence;

/// <summary>
/// Maps the engine's mutable <see cref="GameState"/> aggregate to and from a serializable
/// <see cref="GameStateSnapshot"/>. The active battle is intentionally excluded — saves
/// should be taken between battles.
/// </summary>
public static class GameStateSnapshotMapper
{
    public const int CurrentSchemaVersion = 4;

    public static GameStateSnapshot Capture(GameState gameState)
    {
        return new GameStateSnapshot
        {
            SchemaVersion = CurrentSchemaVersion,
            ContentVersion = gameState.ContentVersion,
            SimulationMinutes = gameState.SimulationMinutes,
            CurrencyWallet = CaptureCurrency(gameState.CurrencyWallet),
            InventoryBag = CaptureInventory(gameState.InventoryBag),
            Quest = CaptureQuests(gameState.QuestState),
            Dialogue = CaptureDialogue(gameState.DialogueState),
            Shop = CaptureShop(gameState.ShopState),
            World = CaptureWorld(gameState.WorldState),
            Exploration = CaptureExploration(gameState.ExplorationState),
            Equipment = CaptureEquipment(gameState.EquipmentState),
            Progression = CaptureProgression(gameState.ProgressionState),
            ActorStats = CaptureActorStats(gameState.ActorStatsState),
            Interactables = CaptureInteractables(gameState.InteractablesState),
            Party = CaptureParty(gameState.PartyState)
        };
    }

    public static void Apply(GameState gameState, GameStateSnapshot snapshot)
    {
        gameState.ContentVersion = snapshot.ContentVersion;
        gameState.SimulationMinutes = snapshot.SimulationMinutes;
        ApplyCurrency(gameState.CurrencyWallet, snapshot.CurrencyWallet);
        ApplyInventory(gameState.InventoryBag, snapshot.InventoryBag);
        ApplyQuests(gameState.QuestState, snapshot.Quest);
        ApplyDialogue(gameState.DialogueState, snapshot.Dialogue);
        ApplyShop(gameState.ShopState, snapshot.Shop);
        ApplyWorld(gameState.WorldState, snapshot.World);
        ApplyExploration(gameState.ExplorationState, snapshot.Exploration);
        ApplyEquipment(gameState.EquipmentState, snapshot.Equipment);
        ApplyProgression(gameState.ProgressionState, snapshot.Progression);
        ApplyActorStats(gameState.ActorStatsState, snapshot.ActorStats);
        ApplyInteractables(gameState.InteractablesState, snapshot.Interactables);
        ApplyParty(gameState.PartyState, snapshot.Party);
    }

    private static ProgressionStateSnapshot CaptureProgression(ProgressionState progressionState)
    {
        ProgressionStateSnapshot snapshot = new();
        foreach (KeyValuePair<string, ActorProgression> pair in progressionState.Actors)
        {
            snapshot.Actors.Add(new ActorProgressionSnapshot
            {
                ActorId = pair.Value.ActorId,
                CurveId = pair.Value.CurveId,
                Level = pair.Value.Level,
                Xp = pair.Value.Xp
            });
        }

        return snapshot;
    }

    private static void ApplyProgression(ProgressionState progressionState, ProgressionStateSnapshot snapshot)
    {
        progressionState.CopyFrom(new ProgressionState());
        foreach (ActorProgressionSnapshot entry in snapshot.Actors)
        {
            progressionState.Set(new ActorProgression(entry.ActorId, entry.CurveId, entry.Level, entry.Xp));
        }
    }

    private static CurrencyWalletSnapshot CaptureCurrency(CurrencyWallet wallet)
    {
        CurrencyWalletSnapshot snapshot = new();
        foreach (KeyValuePair<string, long> pair in wallet.Balances)
        {
            snapshot.Balances.Add(new CurrencyBalanceSnapshot { CurrencyId = pair.Key, Amount = pair.Value });
        }

        foreach (string currencyId in EnumerateMaxBalances(wallet))
        {
            snapshot.Maxes.Add(new CurrencyBalanceSnapshot { CurrencyId = currencyId, Amount = wallet.GetMax(currencyId) });
        }

        return snapshot;
    }

    private static IEnumerable<string> EnumerateMaxBalances(CurrencyWallet wallet)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string id in wallet.Balances.Keys)
        {
            if (seen.Add(id))
            {
                yield return id;
            }
        }
    }

    private static void ApplyCurrency(CurrencyWallet wallet, CurrencyWalletSnapshot snapshot)
    {
        wallet.CopyFrom(new CurrencyWallet());
        foreach (CurrencyBalanceSnapshot max in snapshot.Maxes)
        {
            wallet.ConfigureMax(max.CurrencyId, max.Amount);
        }

        foreach (CurrencyBalanceSnapshot balance in snapshot.Balances)
        {
            if (balance.Amount > 0)
            {
                if (wallet.GetMax(balance.CurrencyId) < balance.Amount)
                {
                    wallet.ConfigureMax(balance.CurrencyId, balance.Amount);
                }

                wallet.Grant(balance.CurrencyId, balance.Amount);
            }
        }
    }

    private static InventoryBagSnapshot CaptureInventory(InventoryBag bag)
    {
        InventoryBagSnapshot snapshot = new()
        {
            CapacitySlots = bag.CapacitySlots
        };

        foreach (InventoryStack stack in bag.Stacks)
        {
            snapshot.Stacks.Add(new InventoryStackSnapshot
            {
                ItemId = stack.ItemId,
                Quantity = stack.Quantity,
                StackLimit = stack.StackLimit
            });
        }

        return snapshot;
    }

    private static void ApplyInventory(InventoryBag bag, InventoryBagSnapshot snapshot)
    {
        bag.CopyFrom(new InventoryBag());
        bag.SetCapacity(snapshot.CapacitySlots);
        foreach (InventoryStackSnapshot stack in snapshot.Stacks)
        {
            if (stack.Quantity > 0)
            {
                bag.TryAdd(stack.ItemId, stack.Quantity, stack.StackLimit, out _);
            }
        }
    }

    private static QuestStateSnapshot CaptureQuests(QuestState questState)
    {
        QuestStateSnapshot snapshot = new();
        foreach (KeyValuePair<string, QuestInstanceState> pair in questState.Quests)
        {
            QuestInstanceSnapshot instance = new()
            {
                QuestId = pair.Key,
                Status = pair.Value.Status
            };

            foreach (KeyValuePair<string, int> progress in pair.Value.ObjectiveProgress)
            {
                instance.ObjectiveProgress[progress.Key] = progress.Value;
            }

            snapshot.Quests.Add(instance);
        }

        return snapshot;
    }

    private static void ApplyQuests(QuestState questState, QuestStateSnapshot snapshot)
    {
        questState.CopyFrom(new QuestState());
        foreach (QuestInstanceSnapshot instance in snapshot.Quests)
        {
            QuestInstanceState quest = questState.GetOrCreate(instance.QuestId);
            quest.Status = instance.Status;
            quest.ClearObjectiveProgress();
            foreach (KeyValuePair<string, int> progress in instance.ObjectiveProgress)
            {
                quest.SetObjectiveProgress(progress.Key, progress.Value);
            }
        }
    }

    private static DialogueStateSnapshot CaptureDialogue(DialogueState dialogueState)
    {
        DialogueStateSnapshot snapshot = new();
        foreach (KeyValuePair<string, DialogueInstanceState> pair in dialogueState.Dialogues)
        {
            DialogueInstanceSnapshot instance = new()
            {
                DialogueId = pair.Key,
                CurrentNodeId = pair.Value.CurrentNodeId,
                Completed = pair.Value.Completed
            };

            foreach (string node in pair.Value.VisitedNodes)
            {
                instance.VisitedNodes.Add(node);
            }

            foreach (string choice in pair.Value.ChosenChoices)
            {
                instance.ChosenChoices.Add(choice);
            }

            snapshot.Dialogues.Add(instance);
        }

        return snapshot;
    }

    private static void ApplyDialogue(DialogueState dialogueState, DialogueStateSnapshot snapshot)
    {
        dialogueState.CopyFrom(new DialogueState());
        foreach (DialogueInstanceSnapshot instance in snapshot.Dialogues)
        {
            DialogueInstanceState dialogue = dialogueState.GetOrCreate(instance.DialogueId);
            dialogue.CurrentNodeId = instance.CurrentNodeId;
            dialogue.Completed = instance.Completed;
            foreach (string node in instance.VisitedNodes)
            {
                dialogue.MarkVisitedNode(node);
            }

            foreach (string choice in instance.ChosenChoices)
            {
                dialogue.MarkChosenChoice(choice);
            }
        }
    }

    private static ShopStateSnapshot CaptureShop(ShopState shopState)
    {
        ShopStateSnapshot snapshot = new();
        foreach (KeyValuePair<string, int> pair in shopState.EntryStock)
        {
            snapshot.Stocks.Add(new ShopStockSnapshot { Key = pair.Key, Stock = pair.Value });
        }

        foreach (KeyValuePair<string, long> pair in shopState.LastRestockMinutes)
        {
            snapshot.Restocks.Add(new ShopRestockSnapshot { ShopId = pair.Key, LastRestockMinute = pair.Value });
        }

        return snapshot;
    }

    private static void ApplyShop(ShopState shopState, ShopStateSnapshot snapshot)
    {
        shopState.CopyFrom(new ShopState());
        foreach (ShopStockSnapshot stock in snapshot.Stocks)
        {
            int separator = stock.Key.IndexOf('|');
            if (separator <= 0)
            {
                continue;
            }

            string shopId = stock.Key.Substring(0, separator);
            string itemId = stock.Key.Substring(separator + 1);
            shopState.SetStock(shopId, itemId, stock.Stock);
        }

        foreach (ShopRestockSnapshot restock in snapshot.Restocks)
        {
            shopState.SetLastRestockMinute(restock.ShopId, restock.LastRestockMinute);
        }
    }

    private static WorldStateSnapshot CaptureWorld(WorldState worldState)
    {
        WorldStateSnapshot snapshot = new();
        foreach (KeyValuePair<string, WorldVariableValue> pair in worldState.Variables)
        {
            WorldVariableSnapshot variable = new()
            {
                Key = pair.Key,
                Kind = pair.Value.Kind
            };

            switch (pair.Value.Kind)
            {
                case WorldVariableKind.Bool:
                    pair.Value.TryGetBool(out bool boolValue);
                    variable.BoolValue = boolValue;
                    break;
                case WorldVariableKind.Int:
                    pair.Value.TryGetInt(out int intValue);
                    variable.IntValue = intValue;
                    break;
                case WorldVariableKind.Float:
                    pair.Value.TryGetFloat(out double floatValue);
                    variable.FloatValue = floatValue;
                    break;
                case WorldVariableKind.String:
                    pair.Value.TryGetString(out string stringValue);
                    variable.StringValue = stringValue;
                    break;
            }

            snapshot.Variables.Add(variable);
        }

        return snapshot;
    }

    private static void ApplyWorld(WorldState worldState, WorldStateSnapshot snapshot)
    {
        worldState.CopyFrom(new WorldState());
        foreach (WorldVariableSnapshot variable in snapshot.Variables)
        {
            WorldVariableValue value = variable.Kind switch
            {
                WorldVariableKind.Bool => WorldVariableValue.FromBool(variable.BoolValue),
                WorldVariableKind.Int => WorldVariableValue.FromInt(variable.IntValue),
                WorldVariableKind.Float => WorldVariableValue.FromFloat(variable.FloatValue),
                WorldVariableKind.String => WorldVariableValue.FromString(variable.StringValue ?? string.Empty),
                _ => WorldVariableValue.FromString(variable.StringValue ?? string.Empty)
            };
            worldState.Set(variable.Key, value);
        }
    }

    private static ExplorationStateSnapshot CaptureExploration(ExplorationState explorationState)
    {
        ExplorationStateSnapshot snapshot = new();
        ExplorationMapState map = explorationState.Map;
        snapshot.Map.MapId = map.MapId;
        snapshot.Map.Width = map.Width;
        snapshot.Map.Height = map.Height;
        if (map.IsConfigured)
        {
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    map.TryGetTileFlags(new GridPosition(x, y), out ExplorationTileFlags flags);
                    snapshot.Map.Tiles.Add((int)flags);
                }
            }
        }

        foreach (KeyValuePair<string, ExplorationActorState> pair in explorationState.Actors)
        {
            snapshot.Actors.Add(new ExplorationActorSnapshot
            {
                ActorId = pair.Key,
                X = pair.Value.X,
                Y = pair.Value.Y,
                BlocksMovement = pair.Value.BlocksMovement
            });
        }

        return snapshot;
    }

    private static void ApplyExploration(ExplorationState explorationState, ExplorationStateSnapshot snapshot)
    {
        explorationState.ClearActors();
        if (snapshot.Map.Width > 0 && snapshot.Map.Height > 0 && snapshot.Map.Tiles.Count == snapshot.Map.Width * snapshot.Map.Height)
        {
            ExplorationTileFlags[] tiles = new ExplorationTileFlags[snapshot.Map.Tiles.Count];
            for (int i = 0; i < snapshot.Map.Tiles.Count; i++)
            {
                tiles[i] = (ExplorationTileFlags)snapshot.Map.Tiles[i];
            }

            explorationState.Map.TryConfigure(snapshot.Map.MapId, snapshot.Map.Width, snapshot.Map.Height, tiles, out _);
        }

        foreach (ExplorationActorSnapshot actor in snapshot.Actors)
        {
            explorationState.UpsertActor(actor.ActorId, new GridPosition(actor.X, actor.Y), actor.BlocksMovement);
        }
    }

    private static EquipmentStateSnapshot CaptureEquipment(EquipmentState equipmentState)
    {
        EquipmentStateSnapshot snapshot = new();
        foreach (KeyValuePair<string, string> pair in equipmentState.EquippedItems)
        {
            snapshot.Slots.Add(new EquipmentSlotSnapshot { SlotId = pair.Key, ItemId = pair.Value });
        }

        return snapshot;
    }

    private static void ApplyEquipment(EquipmentState equipmentState, EquipmentStateSnapshot snapshot)
    {
        equipmentState.CopyFrom(new EquipmentState());
        foreach (EquipmentSlotSnapshot slot in snapshot.Slots)
        {
            if (!string.IsNullOrWhiteSpace(slot.ItemId))
            {
                equipmentState.SetEquipped(slot.SlotId, slot.ItemId);
            }
        }
    }

    private static ActorStatsStateSnapshot CaptureActorStats(ActorStatsState actorStatsState)
    {
        ActorStatsStateSnapshot snapshot = new();
        foreach (KeyValuePair<string, StatBlock> pair in actorStatsState.Actors)
        {
            ActorStatsSnapshot actor = new() { ActorId = pair.Key };
            foreach (KeyValuePair<string, int> baseEntry in pair.Value.Base)
            {
                actor.Base.Add(new StatBaseSnapshot { StatId = baseEntry.Key, Value = baseEntry.Value });
            }

            foreach (StatModifier mod in pair.Value.Modifiers)
            {
                actor.Modifiers.Add(new StatModifierSnapshot
                {
                    StatId = mod.StatId,
                    Bucket = mod.Bucket,
                    Value = mod.Value,
                    SourceKind = mod.SourceKind,
                    SourceId = mod.SourceId,
                    Priority = mod.Priority
                });
            }

            snapshot.Actors.Add(actor);
        }

        return snapshot;
    }

    private static void ApplyActorStats(ActorStatsState actorStatsState, ActorStatsStateSnapshot snapshot)
    {
        actorStatsState.CopyFrom(new ActorStatsState());
        if (snapshot is null)
        {
            return;
        }

        foreach (ActorStatsSnapshot actor in snapshot.Actors)
        {
            if (string.IsNullOrWhiteSpace(actor.ActorId))
            {
                continue;
            }

            StatBlock block = actorStatsState.GetOrCreate(actor.ActorId);
            foreach (StatBaseSnapshot baseEntry in actor.Base)
            {
                if (!string.IsNullOrWhiteSpace(baseEntry.StatId))
                {
                    block.SetBase(baseEntry.StatId, baseEntry.Value);
                }
            }

            foreach (StatModifierSnapshot mod in actor.Modifiers)
            {
                if (string.IsNullOrWhiteSpace(mod.StatId)
                    || string.IsNullOrWhiteSpace(mod.SourceKind)
                    || string.IsNullOrWhiteSpace(mod.SourceId))
                {
                    continue;
                }

                block.AddModifier(new StatModifier(
                    mod.StatId,
                    mod.Bucket,
                    mod.Value,
                    mod.SourceKind,
                    mod.SourceId,
                    mod.Priority));
            }
        }
    }

    private static PartyStateSnapshot CaptureParty(PartyState partyState)
    {
        PartyStateSnapshot snapshot = new()
        {
            MaxActive = partyState.MaxActive,
            MaxRoster = partyState.MaxRoster
        };

        for (int i = 0; i < partyState.Members.Count; i++)
        {
            PartyMember member = partyState.Members[i];
            snapshot.Members.Add(new PartyMemberSnapshot
            {
                ActorId = member.ActorId,
                IsActive = member.IsActive
            });
        }

        return snapshot;
    }

    private static void ApplyParty(PartyState partyState, PartyStateSnapshot snapshot)
    {
        partyState.CopyFrom(new PartyState());
        if (snapshot is null)
        {
            return;
        }

        partyState.SetCaps(snapshot.MaxActive, snapshot.MaxRoster);
        foreach (PartyMemberSnapshot member in snapshot.Members)
        {
            if (string.IsNullOrWhiteSpace(member.ActorId))
            {
                continue;
            }

            partyState.TryAdd(member.ActorId, member.IsActive, out _);
        }
    }

    private static InteractablesStateSnapshot CaptureInteractables(InteractablesState state)
    {
        InteractablesStateSnapshot snapshot = new();
        foreach (KeyValuePair<string, InteractableInstance> pair in state.Instances)
        {
            InteractableInstance instance = pair.Value;
            snapshot.Instances.Add(new InteractableInstanceSnapshot
            {
                InstanceId = instance.InstanceId,
                DefinitionId = instance.DefinitionId,
                X = instance.Position.X,
                Y = instance.Position.Y,
                Status = instance.Status,
                UsesRemaining = instance.UsesRemaining,
                Locked = instance.Locked
            });
        }

        return snapshot;
    }

    private static void ApplyInteractables(InteractablesState state, InteractablesStateSnapshot snapshot)
    {
        state.CopyFrom(new InteractablesState());
        if (snapshot is null)
        {
            return;
        }

        foreach (InteractableInstanceSnapshot entry in snapshot.Instances)
        {
            if (string.IsNullOrWhiteSpace(entry.InstanceId) || string.IsNullOrWhiteSpace(entry.DefinitionId))
            {
                continue;
            }

            state.Add(new InteractableInstance(
                entry.InstanceId,
                entry.DefinitionId,
                new Exploration.GridPosition(entry.X, entry.Y),
                entry.Status,
                entry.UsesRemaining,
                entry.Locked));
        }
    }
}
