using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Combat.Commands
{

    /// <summary>
    /// Roll to capture an enemy actor and move them into the player's party. Counts as the
    /// capturer's action — the turn advances on both success and failure (matching the way a
    /// missed skill use still consumes the turn).
    ///
    /// Success probability is <c>target.CaptureBaseRate × hpFactor × (BonusPercent / 100)</c>
    /// where <c>hpFactor = (3·maxHp − 2·hp) / (3·maxHp)</c> — i.e. a target at 0 HP is roughly
    /// 3× as catchable as one at full HP. <see cref="BonusPercent"/> is the only at-call modifier
    /// the engine knows about; layer ball-quality, status, and habitat bonuses into that number
    /// in your game code.
    ///
    /// On success a <see cref="Events.BattleActorCapturedEvent"/> fires; the built-in Party
    /// reactor adds the captured actor to the roster as a reserve. If the party is full the
    /// reactor fails and the entire capture transaction rolls back — check party room before
    /// attempting.
    /// </summary>
    public sealed class AttemptCaptureCommand : ICommand
    {
        public AttemptCaptureCommand(string actorId, string targetActorId, int bonusPercent = 100)
        {
            ActorId = actorId;
            TargetActorId = targetActorId;
            BonusPercent = bonusPercent < 0 ? 0 : bonusPercent;
        }

        public string ActorId { get; }

        public string TargetActorId { get; }

        public int BonusPercent { get; }
    }
}
