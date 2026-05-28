using System.Collections.Generic;

namespace Moonforge.Core.Combat
{

    public sealed class BattleAiRuleDefinition
    {
        public BattleAiRuleDefinition(
            string skillId,
            int priorityWeight,
            BattleAiTargetPolicy targetPolicy,
            IReadOnlyList<BattleAiConditionDefinition>? conditions = null)
        {
            SkillId = skillId;
            PriorityWeight = priorityWeight;
            TargetPolicy = targetPolicy;
            Conditions = conditions ?? System.Array.Empty<BattleAiConditionDefinition>();
        }

        public string SkillId { get; }

        public int PriorityWeight { get; }

        public BattleAiTargetPolicy TargetPolicy { get; }

        public IReadOnlyList<BattleAiConditionDefinition> Conditions { get; }
    }
}
