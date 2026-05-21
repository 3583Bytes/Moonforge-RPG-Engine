namespace Moonforge.Core.Persistence.Snapshots;

/// <summary>
/// Serializable point-in-time snapshot of the engine-owned <see cref="GameState"/>.
/// Does NOT include the active battle — saves should be taken between battles.
/// </summary>
public sealed class GameStateSnapshot
{
    public int SchemaVersion { get; set; } = 1;

    public string ContentVersion { get; set; } = string.Empty;

    public long SimulationMinutes { get; set; }

    public CurrencyWalletSnapshot CurrencyWallet { get; set; } = new();

    public InventoryBagSnapshot InventoryBag { get; set; } = new();

    public QuestStateSnapshot Quest { get; set; } = new();

    public DialogueStateSnapshot Dialogue { get; set; } = new();

    public ShopStateSnapshot Shop { get; set; } = new();

    public WorldStateSnapshot World { get; set; } = new();

    public ExplorationStateSnapshot Exploration { get; set; } = new();

    public EquipmentStateSnapshot Equipment { get; set; } = new();

    public ProgressionStateSnapshot Progression { get; set; } = new();

    public ActorStatsStateSnapshot ActorStats { get; set; } = new();

    public InteractablesStateSnapshot Interactables { get; set; } = new();

    public PartyStateSnapshot Party { get; set; } = new();

    public EvolutionStateSnapshot Evolution { get; set; } = new();

    public BestiaryStateSnapshot Bestiary { get; set; } = new();

    public ActorSkillPpStateSnapshot ActorSkillPp { get; set; } = new();
}
