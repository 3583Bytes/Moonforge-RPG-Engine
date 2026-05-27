namespace Moonforge.Core.Combat
{

    public sealed class BattleAiConditionDefinition
    {
        public BattleAiConditionDefinition(BattleAiConditionType type, double thresholdPercent)
        {
            Type = type;
            ThresholdPercent = thresholdPercent;
        }

        public BattleAiConditionType Type { get; }

        public double ThresholdPercent { get; }
    }
}
