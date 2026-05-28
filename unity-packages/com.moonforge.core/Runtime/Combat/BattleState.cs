using System;
using System.Collections.Generic;
using System.Linq;
using Moonforge.Core.Economy.Commands;

namespace Moonforge.Core.Combat
{

    public sealed class BattleState
    {
        private readonly Dictionary<string, BattleActorState> _actors = new(StringComparer.Ordinal);
        private readonly Dictionary<string, BattleSkillDefinition> _skills = new(StringComparer.Ordinal);

        public BattleState(string battleId, BattleRngState rngState)
        {
            BattleId = battleId;
            RngState = rngState;
            Status = BattleStatus.Active;
            Round = 1;
            TurnOrder = new List<string>();
            RewardCurrency = new List<CurrencyDelta>();
            RewardInventory = new List<InventoryDelta>();
        }

        public string BattleId { get; set; }

        public BattleStatus Status { get; set; }

        public int Round { get; set; }

        public int TurnIndex { get; set; }

        public List<string> TurnOrder { get; }

        public BattleRngState RngState { get; set; }

        public bool RewardsApplied { get; set; }

        public IReadOnlyDictionary<string, BattleActorState> Actors => _actors;

        public IReadOnlyDictionary<string, BattleSkillDefinition> Skills => _skills;

        public List<CurrencyDelta> RewardCurrency { get; }

        public List<InventoryDelta> RewardInventory { get; }

        public string? RewardLootTableId { get; set; }

        public void AddActor(BattleActorState actor)
        {
            _actors[actor.ActorId] = actor;
        }

        /// <summary>
        /// Removes the actor from both the actor map and any position in <see cref="TurnOrder"/>.
        /// Used by swap and similar mid-battle roster changes.
        /// </summary>
        public bool RemoveActor(string actorId)
        {
            if (!_actors.Remove(actorId))
            {
                return false;
            }

            TurnOrder.RemoveAll(id => id == actorId);
            return true;
        }

        public bool TryGetActor(string actorId, out BattleActorState actor)
        {
            return _actors.TryGetValue(actorId, out actor!);
        }

        public void AddSkill(BattleSkillDefinition skill)
        {
            _skills[skill.Id] = skill;
        }

        public bool TryGetSkill(string skillId, out BattleSkillDefinition skill)
        {
            return _skills.TryGetValue(skillId, out skill!);
        }

        public IEnumerable<BattleActorState> GetAliveFaction(CombatFaction faction)
        {
            return _actors.Values.Where(x => x.Faction == faction && !x.IsDowned);
        }

        public BattleState Clone()
        {
            BattleState clone = new(BattleId, RngState.Clone())
            {
                Status = Status,
                Round = Round,
                TurnIndex = TurnIndex
            };

            foreach (string actorId in TurnOrder)
            {
                clone.TurnOrder.Add(actorId);
            }

            foreach ((string key, BattleActorState actor) in _actors)
            {
                clone._actors[key] = actor.Clone();
            }

            foreach ((string key, BattleSkillDefinition skill) in _skills)
            {
                clone._skills[key] = skill.Clone();
            }

            clone.RewardsApplied = RewardsApplied;
            clone.RewardLootTableId = RewardLootTableId;
            foreach (CurrencyDelta delta in RewardCurrency)
            {
                clone.RewardCurrency.Add(new CurrencyDelta(delta.CurrencyId, delta.Amount));
            }

            foreach (InventoryDelta delta in RewardInventory)
            {
                clone.RewardInventory.Add(new InventoryDelta(delta.ItemId, delta.Amount));
            }

            return clone;
        }
    }
}
