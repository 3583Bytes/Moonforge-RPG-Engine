using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Combat.Commands
{

    /// <summary>
    /// Restores PP for one of an actor's skills (or all of them). Use for "PP potion", PokeCenter
    /// rest, and similar items/services. Only affects persistently tracked PP — wild enemies and
    /// other untracked actors aren't represented in <see cref="ActorSkillPpState"/> and so have
    /// nothing to restore.
    ///
    /// When <see cref="SkillId"/> is null/empty, every tracked skill for the actor is restored
    /// to full (capped at each skill's <see cref="BattleSkillDefinition.MaxPp"/> via the supplied
    /// definition lookup). When <see cref="Amount"/> is null, the affected skill(s) are restored
    /// to full; otherwise the value is added (and clamped to MaxPp).
    /// </summary>
    public sealed class RestoreSkillPpCommand : ICommand
    {
        public RestoreSkillPpCommand(string actorId, string? skillId = null, int? amount = null)
        {
            ActorId = actorId;
            SkillId = string.IsNullOrWhiteSpace(skillId) ? null : skillId;
            Amount = amount;
        }

        public string ActorId { get; }

        public string? SkillId { get; }

        public int? Amount { get; }
    }
}
