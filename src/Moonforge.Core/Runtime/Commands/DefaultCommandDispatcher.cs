using Moonforge.Core.Combat.Commands;
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

namespace Moonforge.Core.Runtime.Commands;

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
        dispatcher.RegisterReactor(new QuestObjectiveTrackingReactor());
        dispatcher.RegisterReactor(new LevelUpStatGrowthReactor());
        dispatcher.RegisterReactor(new PartyActiveSyncReactor());
        dispatcher.RegisterReactor(new PartyCaptureReactor());
        dispatcher.RegisterReactor(new LevelUpEvolutionReactor());

        dispatcher.Register(new SetWorldVariableCommandHandler());

        dispatcher.Register(new ConfigureCurrencyMaxCommandHandler());
        dispatcher.Register(new GrantCurrencyCommandHandler());
        dispatcher.Register(new SpendCurrencyCommandHandler());
        dispatcher.Register(new EconomyTransactionCommandHandler());

        dispatcher.Register(new ConfigureInventoryCapacityCommandHandler());
        dispatcher.Register(new AddInventoryItemCommandHandler());
        dispatcher.Register(new ConsumeInventoryItemCommandHandler());

        dispatcher.Register(new AttemptCraftCommandHandler());

        dispatcher.Register(new BuyFromShopCommandHandler());
        dispatcher.Register(new SellToShopCommandHandler());

        dispatcher.Register(new StartQuestCommandHandler());
        dispatcher.Register(new AbandonQuestCommandHandler());
        dispatcher.Register(new EmitQuestSignalCommandHandler());
        dispatcher.Register(new ClaimQuestRewardsCommandHandler());

        dispatcher.Register(new StartDialogueCommandHandler());
        dispatcher.Register(new ChooseDialogueChoiceCommandHandler());

        dispatcher.Register(new StartBattleCommandHandler());
        dispatcher.Register(new UseBattleSkillCommandHandler());
        dispatcher.Register(new ExecuteAiTurnCommandHandler());
        dispatcher.Register(new SwapBattleActorCommandHandler());
        dispatcher.Register(new AttemptCaptureCommandHandler());
        dispatcher.Register(new ApplyStatusEffectCommandHandler());
        dispatcher.Register(new RemoveStatusEffectCommandHandler());

        dispatcher.Register(new ConfigureExplorationMapCommandHandler());
        dispatcher.Register(new UpsertExplorationActorCommandHandler());
        dispatcher.Register(new MoveActorCommandHandler());

        dispatcher.Register(new EquipItemCommandHandler());
        dispatcher.Register(new UnequipItemCommandHandler());

        dispatcher.Register(new ConfigureActorProgressionCommandHandler());
        dispatcher.Register(new GrantExperienceCommandHandler());

        dispatcher.Register(new ConfigureActorEvolutionsCommandHandler());
        dispatcher.Register(new TriggerEvolutionCommandHandler());

        dispatcher.Register(new ConfigurePartyCommandHandler());
        dispatcher.Register(new AddPartyMemberCommandHandler());
        dispatcher.Register(new RemovePartyMemberCommandHandler());
        dispatcher.Register(new SetPartyMemberActiveCommandHandler());

        dispatcher.Register(new SetStatBaseCommandHandler());
        dispatcher.Register(new ApplyStatModifierCommandHandler());
        dispatcher.Register(new RemoveStatModifiersCommandHandler());

        dispatcher.Register(new RollAndGrantLootCommandHandler());

        dispatcher.Register(new PlaceInteractableCommandHandler());
        dispatcher.Register(new RemoveInteractableCommandHandler());
        dispatcher.Register(new InteractWithCommandHandler());
    }
}
