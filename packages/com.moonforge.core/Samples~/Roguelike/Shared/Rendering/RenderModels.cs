using System.Collections.Generic;
using Moonforge.Core.Combat;
using Moonforge.Core.Exploration;

namespace Moonforge.Sample.Roguelike.Rendering;

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
    IReadOnlyDictionary<GridPosition, char>? FloorDecorations = null);

public sealed record MapMarker(GridPosition Position, char Symbol, string Label);

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
