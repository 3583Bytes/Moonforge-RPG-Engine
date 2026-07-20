using Moonforge.Core.Bestiary.Commands;
using Moonforge.Core.Bestiary.Reactors;
using Moonforge.Core.Combat.Commands;
using Moonforge.Core.Combat.Reactors;
using Moonforge.Core.Crafting.Commands;
using Moonforge.Core.Dialogue.Commands;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Equipment.Commands;
using Moonforge.Core.Evolution.Commands;
using Moonforge.Core.Evolution.Reactors;
using Moonforge.Core.Exploration.Commands;
using Moonforge.Core.Interactables.Commands;
using Moonforge.Core.Inventory.Commands;
using Moonforge.Core.Loot.Commands;
using Moonforge.Core.Party.Commands;
using Moonforge.Core.Party.Reactors;
using Moonforge.Core.Progression.Commands;
using Moonforge.Core.Progression.Reactors;
using Moonforge.Core.Quests;
using Moonforge.Core.Quests.Commands;
using Moonforge.Core.Shops.Commands;
using Moonforge.Core.Stats.Commands;
using Moonforge.Core.World.Commands;

namespace Moonforge.Core.Runtime.Commands
{

    /// <summary>
    /// Convenience helpers that wire every built-in command handler and reactor into a <see cref="CommandDispatcher"/>.
    /// Games that need a non-standard set can still construct a dispatcher manually.
    /// </summary>
    public static class DefaultCommandDispatcher
    {
        public static CommandDispatcher Create()
        {
            CommandDispatcher dispatcher = new();
            RegisterBuiltIns(dispatcher);
            return dispatcher;
        }

        public static void RegisterBuiltIns(CommandDispatcher dispatcher)
        {
            // Composed sub-handlers: a handler that needs another module's command as part of
            // its own transaction (e.g. a shop purchase embedding an economy transaction) calls
            // that handler directly. Sharing one instance here keeps the composed path and the
            // directly dispatched path behaviorally identical — replace a shared handler and
            // every composition site picks it up.
            SetWorldVariableCommandHandler setWorldVariable = new();
            EconomyTransactionCommandHandler economyTransaction = new();
            GrantExperienceCommandHandler grantExperience = new();
            EmitQuestSignalCommandHandler emitQuestSignal = new();
            RollAndGrantLootCommandHandler rollAndGrantLoot = new(economyTransaction);
            ClaimQuestRewardsCommandHandler claimQuestRewards = new(economyTransaction);

            dispatcher.RegisterReactor(new QuestObjectiveTrackingReactor(claimQuestRewards));
            dispatcher.RegisterReactor(new LevelUpStatGrowthReactor());
            dispatcher.RegisterReactor(new PartyActiveSyncReactor());
            dispatcher.RegisterReactor(new PartyCaptureReactor());
            dispatcher.RegisterReactor(new LevelUpEvolutionReactor());
            dispatcher.RegisterReactor(new BestiaryAutoTrackReactor());
            dispatcher.RegisterReactor(new SkillPpAutoTrackReactor());

            dispatcher.Register(setWorldVariable);

            dispatcher.Register(new ConfigureCurrencyMaxCommandHandler());
            dispatcher.Register(new GrantCurrencyCommandHandler());
            dispatcher.Register(new SpendCurrencyCommandHandler());
            dispatcher.Register(economyTransaction);

            dispatcher.Register(new ConfigureInventoryCapacityCommandHandler());
            dispatcher.Register(new AddInventoryItemCommandHandler());
            dispatcher.Register(new ConsumeInventoryItemCommandHandler());

            dispatcher.Register(new AttemptCraftCommandHandler(economyTransaction));

            dispatcher.Register(new BuyFromShopCommandHandler(economyTransaction));
            dispatcher.Register(new SellToShopCommandHandler(economyTransaction));

            dispatcher.Register(new StartQuestCommandHandler());
            dispatcher.Register(new AbandonQuestCommandHandler());
            dispatcher.Register(emitQuestSignal);
            dispatcher.Register(claimQuestRewards);

            dispatcher.Register(new StartDialogueCommandHandler(setWorldVariable, emitQuestSignal));
            dispatcher.Register(new ChooseDialogueChoiceCommandHandler(setWorldVariable, emitQuestSignal));

            dispatcher.Register(new StartBattleCommandHandler());
            dispatcher.Register(new UseBattleSkillCommandHandler(economyTransaction, grantExperience, rollAndGrantLoot));
            dispatcher.Register(new ExecuteAiTurnCommandHandler(economyTransaction, grantExperience, rollAndGrantLoot));
            dispatcher.Register(new SwapBattleActorCommandHandler(economyTransaction, grantExperience, rollAndGrantLoot));
            dispatcher.Register(new AttemptCaptureCommandHandler(economyTransaction, grantExperience, rollAndGrantLoot));
            dispatcher.Register(new EnsureSkillPpTrackingCommandHandler());
            dispatcher.Register(new RestoreSkillPpCommandHandler());
            dispatcher.Register(new ApplyStatusEffectCommandHandler());
            dispatcher.Register(new RemoveStatusEffectCommandHandler());

            dispatcher.Register(new ConfigureExplorationMapCommandHandler());
            dispatcher.Register(new SwitchExplorationMapCommandHandler());
            dispatcher.Register(new RemoveExplorationMapCommandHandler());
            dispatcher.Register(new UpsertExplorationActorCommandHandler());
            dispatcher.Register(new MoveActorCommandHandler());

            dispatcher.Register(new EquipItemCommandHandler());
            dispatcher.Register(new UnequipItemCommandHandler());

            dispatcher.Register(new ConfigureActorProgressionCommandHandler());
            dispatcher.Register(grantExperience);

            dispatcher.Register(new ConfigureActorEvolutionsCommandHandler());
            dispatcher.Register(new TriggerEvolutionCommandHandler());

            dispatcher.Register(new MarkSpeciesObservedCommandHandler());

            dispatcher.Register(new ConfigurePartyCommandHandler());
            dispatcher.Register(new AddPartyMemberCommandHandler());
            dispatcher.Register(new RemovePartyMemberCommandHandler());
            dispatcher.Register(new SetPartyMemberActiveCommandHandler());

            dispatcher.Register(new SetStatBaseCommandHandler());
            dispatcher.Register(new ApplyStatModifierCommandHandler());
            dispatcher.Register(new RemoveStatModifiersCommandHandler());

            dispatcher.Register(rollAndGrantLoot);

            dispatcher.Register(new PlaceInteractableCommandHandler());
            dispatcher.Register(new RemoveInteractableCommandHandler());
            dispatcher.Register(new InteractWithCommandHandler(rollAndGrantLoot, setWorldVariable));
        }
    }
}
