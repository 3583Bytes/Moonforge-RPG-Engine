using Moonforge.Core.Combat;

namespace Moonforge.Sample.Roguelike.Content;

public sealed record ClassAbilityDefinition(
    PlayerClass ClassId,
    string SkillId,
    string Name,
    string Summary,
    BattleSkillEffectType EffectType,
    int Power,
    int CooldownTurns,
    int FocusCost,
    bool TargetSelf,
    string? DamageTypeId = null,
    BattleSkillTargetMode TargetMode = BattleSkillTargetMode.Single);
