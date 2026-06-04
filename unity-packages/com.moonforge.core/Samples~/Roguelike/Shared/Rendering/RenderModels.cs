using System.Collections.Generic;
using Moonforge.Core.Combat;
using Moonforge.Core.Exploration;

namespace Moonforge.Sample.Roguelike.Rendering
{

    public sealed record MapRenderModel(
        string Title,
        ExplorationMapState Map,
        GridPosition? HeroPosition,
        GridPosition? GuardPosition,
        IReadOnlyList<MapMarker> Markers,
        long Gold,
        long Tokens,
        int Potions,
        int Depth,
        string ContractInfo,
        string Controls,
        string LastMessage,
        MessageTone MessageTone,
        IReadOnlyDictionary<GridPosition, char>? WallDecorations = null,
        IReadOnlyDictionary<GridPosition, char>? FloorDecorations = null,
        IReadOnlyList<MapActor>? Actors = null);

    public sealed record MapMarker(GridPosition Position, char Symbol, string Label);

    /// <summary>
    /// Renderable view of an engine actor on the exploration map. Populated by the
    /// session from <c>GameState.ExplorationState.Actors</c> so any actor the engine
    /// blocks/tracks gets a sprite — no more "added an actor but forgot to add a
    /// marker" footguns (the bug class behind the invisible guard).
    /// </summary>
    public sealed record MapActor(string ActorId, GridPosition Position, MapActorKind Kind, string DisplayName);

    public enum MapActorKind
    {
        Hero,
        Guard,
        Npc,
        Enemy,
        EliteEnemy,
        BossEnemy
    }

    /// <summary>
    /// Cardinal facing of the hero — a visual hint for hosts with directional art.
    /// Down (facing the camera) is the default and the spawn/load orientation.
    /// </summary>
    public enum FacingDirection
    {
        Down,
        Up,
        Left,
        Right
    }

    public sealed record BattleRenderModel(
        string Title,
        BattleState Battle,
        string? CurrentTurnActorId,
        string Controls,
        string ClassActionInfo,
        IReadOnlyList<BattleLogEntry> RecentLog,
        string LastMessage,
        MessageTone MessageTone);

    public sealed record BattleSummaryRenderModel(
        string Outcome,
        string EncounterTitle,
        long GoldBefore,
        long GoldAfter,
        long GoldDelta,
        long TokensBefore,
        long TokensAfter,
        long TokensDelta,
        int PotionsBefore,
        int PotionsAfter,
        int PotionsDelta,
        int HerbsBefore,
        int HerbsAfter,
        int HerbsDelta,
        IReadOnlyList<string> BossRewardOptions,
        string? BossRewardChosen,
        IReadOnlyList<BattleLogEntry> RecentLog,
        string Controls);

    public sealed record ClassSelectionOption(
        string Hotkey,
        string Name,
        string Summary);

    public sealed record DialogueRenderModel(
        string NpcName,
        string BodyText,
        IReadOnlyList<DialogueChoiceView> Choices,
        string Controls);

    public sealed record DialogueChoiceView(
        string Hotkey,
        string ChoiceId,
        string Text);

    /// <summary>
    /// Structured view of the player's loadout + carryable gear. Replaces the
    /// stringly-typed Contract-Journal reuse so each host can render this
    /// properly (two-pane Unity layout, grouped Spectre table on console)
    /// instead of parsing flattened lines.
    /// </summary>
    public sealed record GearInventoryRenderModel(
        string ClassName,
        GearSlotView WeaponSlot,
        GearSlotView ArmorSlot,
        GearSlotView AccessorySlot,
        IReadOnlyList<GearInventoryEntry> Entries,
        GearFilter Filter,
        string Controls,
        string LastMessage,
        MessageTone MessageTone);

    /// <summary>One of the three equipment slots and the item currently equipped in it.</summary>
    public sealed record GearSlotView(
        string SlotId,
        string SlotLabel,
        GearInventoryEntry? Equipped);

    /// <summary>
    /// One inventory item, with everything a host needs to draw a row:
    /// digit hotkey (1-6, or -1 if not assignable), tier, stats, deltas
    /// against the currently-equipped item in the same slot, and any
    /// granted skill ids (so wand-style "grants skill.bolt" reads naturally).
    /// </summary>
    public sealed record GearInventoryEntry(
        int HotkeyIndex,
        string ItemId,
        string Name,
        string SlotId,
        string SlotLabel,
        GearTier Tier,
        int Quantity,
        bool IsEquipped,
        IReadOnlyList<GearStatLine> Stats,
        IReadOnlyList<GearStatDelta> Deltas,
        IReadOnlyList<string> GrantedSkillIds);

    public sealed record GearStatLine(string Label, int Value);

    public sealed record GearStatDelta(string Label, int Delta);

    public enum GearFilter
    {
        All,
        Weapon,
        Armor,
        Accessory
    }

    /// <summary>
    /// Visual quality tier — derived from total stat sum (no authored tier on
    /// the data side). Hosts colour names by tier to give the screen a sense
    /// of progression even without bespoke per-item art.
    /// </summary>
    public enum GearTier
    {
        Common,
        Uncommon,
        Rare,
        Epic
    }
}
