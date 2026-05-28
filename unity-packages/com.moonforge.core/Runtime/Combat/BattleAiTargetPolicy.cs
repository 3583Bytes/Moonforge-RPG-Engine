namespace Moonforge.Core.Combat
{

    public enum BattleAiTargetPolicy
    {
        LowestHpEnemy = 1,
        HighestThreatEnemy = 2,
        LowestHpAlly = 3,
        Self = 4,
        RandomEnemy = 5,
        RandomAlly = 6
    }
}
