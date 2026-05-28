using System.Collections.Generic;
using Moonforge.Sample.Roguelike.Rendering;

namespace Moonforge.Sample.Roguelike.Session
{

    /// <summary>
    /// The fully-resolved data needed to render the post-battle summary screen: the outcome
    /// label, before/after currency and inventory counts, the battle log, and (for boss
    /// fights) the chosen reward.
    /// </summary>
    public sealed record BattleSummarySnapshot(
        string Outcome,
        string EncounterTitle,
        long GoldBefore,
        long GoldAfter,
        long TokensBefore,
        long TokensAfter,
        int PotionsBefore,
        int PotionsAfter,
        int HerbsBefore,
        int HerbsAfter,
        IReadOnlyList<BattleLogEntry> RecentLog,
        IReadOnlyList<string> BossRewardOptions,
        string? BossRewardChosen);
}
