using System.Collections.Generic;
using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events
{

    /// <summary>
    /// Fired when a battle's <see cref="BattleStatus"/> transitions off <see cref="BattleStatus.Active"/>.
    /// The runtime nulls <see cref="GameState.ActiveBattle"/> immediately after this event is
    /// buffered, so consumers that need post-battle actor HP must read it from
    /// <see cref="FinalActorHp"/> / <see cref="FinalActorMaxHp"/> rather than from
    /// <c>gameState.ActiveBattle</c>, which will already be <c>null</c> by the time the event
    /// is observed.
    /// </summary>
    public sealed class BattleEndedEvent : DomainEvent
    {
        public BattleEndedEvent(
            string battleId,
            BattleStatus status,
            IReadOnlyDictionary<string, int>? finalActorHp = null,
            IReadOnlyDictionary<string, int>? finalActorMaxHp = null)
            : base(nameof(BattleEndedEvent))
        {
            BattleId = battleId;
            Status = status;
            FinalActorHp = finalActorHp ?? EmptyMap;
            FinalActorMaxHp = finalActorMaxHp ?? EmptyMap;
        }

        public string BattleId { get; }

        public BattleStatus Status { get; }

        /// <summary>HP of each actor at the moment the battle ended. Keyed by actor id.</summary>
        public IReadOnlyDictionary<string, int> FinalActorHp { get; }

        /// <summary>MaxHp of each actor at the moment the battle ended. Keyed by actor id.</summary>
        public IReadOnlyDictionary<string, int> FinalActorMaxHp { get; }

        private static readonly IReadOnlyDictionary<string, int> EmptyMap = new Dictionary<string, int>();
    }
}
