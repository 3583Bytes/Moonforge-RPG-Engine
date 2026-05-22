using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;

namespace Moonforge.Sample.MonsterCatcher.Content;

/// <summary>
/// The single tracked main quest — "Defeat the eight Wardens." One Kill-style objective
/// with required count 8; each gym victory emits a <see cref="Moonforge.Core.Quests.QuestSignalType.Kill"/>
/// signal with target id <see cref="WardenSignalTarget"/>, advancing the count.
/// </summary>
internal static class MainQuest
{
    public const string QuestId = "quest.main.wardens";
    public const string ObjectiveId = "objective.wardens";
    public const string WardenSignalTarget = "gym.warden.defeated";

    public static QuestDefinition EngineDefinition { get; } = new QuestDefinition(
        id: QuestId,
        objectives: new[]
        {
            new QuestObjectiveDefinition(
                id: ObjectiveId,
                objectiveType: QuestObjectiveType.Kill,
                targetId: WardenSignalTarget,
                requiredCount: 8,
                displayName: "Defeat the eight Wardens")
        },
        autoTrack: true,
        autoClaim: true,
        displayName: "The Eight Wardens",
        description: "Defeat the gym leader at each town between here and the Champion's Hall.",
        rewardCurrency: new[] { new CurrencyDelta(ContentCatalog.CurrencyGold, 5000) });
}
