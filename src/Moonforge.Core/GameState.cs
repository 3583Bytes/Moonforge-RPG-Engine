using Moonforge.Core.Bestiary;
using Moonforge.Core.Dialogue;
using Moonforge.Core.Combat;
using Moonforge.Core.Economy;
using Moonforge.Core.Equipment;
using Moonforge.Core.Evolution;
using Moonforge.Core.Inventory;
using Moonforge.Core.Exploration;
using Moonforge.Core.Interactables;
using Moonforge.Core.Party;
using Moonforge.Core.Progression;
using Moonforge.Core.Quests;
using Moonforge.Core.Shops;
using Moonforge.Core.Stats;
using Moonforge.Core.World;

namespace Moonforge.Core;

/// <summary>
/// Aggregate root for all persistent gameplay state.
/// </summary>
public sealed class GameState
{
    public int SchemaVersion { get; set; } = 1;

    public string ContentVersion { get; set; } = "v1.0.0-data.0";

    /// <summary>
    /// Simulation time in in-game minutes.
    /// </summary>
    public long SimulationMinutes { get; set; }

    public CurrencyWallet CurrencyWallet { get; } = new();

    public InventoryBag InventoryBag { get; } = new();

    public QuestState QuestState { get; } = new();

    public DialogueState DialogueState { get; } = new();

    public BattleState? ActiveBattle { get; set; }

    public ShopState ShopState { get; } = new();

    public WorldState WorldState { get; } = new();

    public ExplorationState ExplorationState { get; } = new();

    public EquipmentState EquipmentState { get; } = new();

    public ProgressionState ProgressionState { get; } = new();

    public ActorStatsState ActorStatsState { get; } = new();

    public InteractablesState InteractablesState { get; } = new();

    public PartyState PartyState { get; } = new();

    public EvolutionState EvolutionState { get; } = new();

    public BestiaryState BestiaryState { get; } = new();

    public ActorSkillPpState ActorSkillPpState { get; } = new();

    public GameState Clone()
    {
        GameState clone = new()
        {
            SchemaVersion = SchemaVersion,
            ContentVersion = ContentVersion,
            SimulationMinutes = SimulationMinutes
        };

        clone.CurrencyWallet.CopyFrom(CurrencyWallet);
        clone.InventoryBag.CopyFrom(InventoryBag);
        clone.QuestState.CopyFrom(QuestState);
        clone.DialogueState.CopyFrom(DialogueState);
        clone.ActiveBattle = ActiveBattle?.Clone();
        clone.ShopState.CopyFrom(ShopState);
        clone.WorldState.CopyFrom(WorldState);
        clone.ExplorationState.CopyFrom(ExplorationState);
        clone.EquipmentState.CopyFrom(EquipmentState);
        clone.ProgressionState.CopyFrom(ProgressionState);
        clone.ActorStatsState.CopyFrom(ActorStatsState);
        clone.InteractablesState.CopyFrom(InteractablesState);
        clone.PartyState.CopyFrom(PartyState);
        clone.EvolutionState.CopyFrom(EvolutionState);
        clone.BestiaryState.CopyFrom(BestiaryState);
        clone.ActorSkillPpState.CopyFrom(ActorSkillPpState);
        return clone;
    }

    public void RestoreFrom(GameState snapshot)
    {
        SchemaVersion = snapshot.SchemaVersion;
        ContentVersion = snapshot.ContentVersion;
        SimulationMinutes = snapshot.SimulationMinutes;
        CurrencyWallet.CopyFrom(snapshot.CurrencyWallet);
        InventoryBag.CopyFrom(snapshot.InventoryBag);
        QuestState.CopyFrom(snapshot.QuestState);
        DialogueState.CopyFrom(snapshot.DialogueState);
        ActiveBattle = snapshot.ActiveBattle?.Clone();
        ShopState.CopyFrom(snapshot.ShopState);
        WorldState.CopyFrom(snapshot.WorldState);
        ExplorationState.CopyFrom(snapshot.ExplorationState);
        EquipmentState.CopyFrom(snapshot.EquipmentState);
        ProgressionState.CopyFrom(snapshot.ProgressionState);
        ActorStatsState.CopyFrom(snapshot.ActorStatsState);
        InteractablesState.CopyFrom(snapshot.InteractablesState);
        PartyState.CopyFrom(snapshot.PartyState);
        EvolutionState.CopyFrom(snapshot.EvolutionState);
        BestiaryState.CopyFrom(snapshot.BestiaryState);
        ActorSkillPpState.CopyFrom(snapshot.ActorSkillPpState);
    }
}
