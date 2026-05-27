using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events
{

    public sealed class BattleActionResolvedEvent : DomainEvent
    {
        public BattleActionResolvedEvent(
            string battleId,
            string actorId,
            string skillId,
            string targetActorId,
            int amount,
            bool wasHeal,
            bool wasCritical = false)
            : base(nameof(BattleActionResolvedEvent))
        {
            BattleId = battleId;
            ActorId = actorId;
            SkillId = skillId;
            TargetActorId = targetActorId;
            Amount = amount;
            WasHeal = wasHeal;
            WasCritical = wasCritical;
        }

        public string BattleId { get; }

        public string ActorId { get; }

        public string SkillId { get; }

        public string TargetActorId { get; }

        public int Amount { get; }

        public bool WasHeal { get; }

        /// <summary>
        /// True when a damage skill rolled a critical hit. Always false for heals.
        /// </summary>
        public bool WasCritical { get; }
    }
}
