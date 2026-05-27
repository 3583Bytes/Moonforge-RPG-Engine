using System.Collections.Generic;

namespace Moonforge.Core.Combat
{

    public sealed class BattleAiPolicyDefinition
    {
        public BattleAiPolicyDefinition(
            IReadOnlyList<BattleAiRuleDefinition>? rules = null,
            string? fallbackSkillId = null,
            BattleAiTargetPolicy fallbackTargetPolicy = BattleAiTargetPolicy.LowestHpEnemy)
        {
            Rules = rules ?? System.Array.Empty<BattleAiRuleDefinition>();
            FallbackSkillId = fallbackSkillId;
            FallbackTargetPolicy = fallbackTargetPolicy;
        }

        public IReadOnlyList<BattleAiRuleDefinition> Rules { get; }

        public string? FallbackSkillId { get; }

        public BattleAiTargetPolicy FallbackTargetPolicy { get; }
    }
}
