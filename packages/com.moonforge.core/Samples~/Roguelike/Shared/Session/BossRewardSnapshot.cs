using System.Collections.Generic;
using Moonforge.Sample.Roguelike.Content;

namespace Moonforge.Sample.Roguelike.Session
{

    /// <summary>
    /// Pending boss-reward chest: which floor it dropped on and the three player-pickable
    /// reward options.
    /// </summary>
    public sealed record BossRewardSnapshot(
        int Floor,
        IReadOnlyList<BossRewardChoice> Choices);
}
