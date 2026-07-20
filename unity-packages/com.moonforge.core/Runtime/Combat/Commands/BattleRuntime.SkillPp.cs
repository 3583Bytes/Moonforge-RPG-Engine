using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;

namespace Moonforge.Core.Combat.Commands
{

    // Bridges per-skill PP between the durable ActorSkillPpState and the in-battle actor: hydrate
    // on entry (battle start / swap-in) and persist back for tracked actors (swap-out / battle end).
    internal sealed partial class BattleRuntime
    {
        private static void HydrateSkillPp(GameState gameState, BattleState battle, BattleActorState actor)
        {
            bool tracked = gameState.ActorSkillPpState.IsTracked(actor.ActorId);
            foreach (string skillId in actor.SkillIds)
            {
                if (!battle.TryGetSkill(skillId, out BattleSkillDefinition skill) || skill.MaxPp <= 0)
                {
                    continue;
                }

                int pp = skill.MaxPp;
                if (tracked && gameState.ActorSkillPpState.TryGetSkillPp(actor.ActorId, skillId, out int stored))
                {
                    pp = stored;
                }

                actor.SkillPp[skillId] = pp;
            }
        }

        private static void PersistActorSkillPpIfTracked(GameState gameState, BattleActorState actor)
        {
            if (!gameState.ActorSkillPpState.IsTracked(actor.ActorId))
            {
                return;
            }

            foreach (KeyValuePair<string, int> pair in actor.SkillPp)
            {
                gameState.ActorSkillPpState.SetSkillPp(actor.ActorId, pair.Key, pair.Value);
            }
        }

        private static void PersistAllTrackedSkillPp(GameState gameState, BattleState battle)
        {
            foreach (BattleActorState actor in battle.Actors.Values)
            {
                PersistActorSkillPpIfTracked(gameState, actor);
            }
        }
    }
}
