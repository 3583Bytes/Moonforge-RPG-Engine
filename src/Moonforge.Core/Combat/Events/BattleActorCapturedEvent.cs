using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events;

/// <summary>
/// Raised when a capture attempt succeeds. The captured actor has already been removed from
/// the active battle. <see cref="HpAtCapture"/> carries the HP the target had at the moment
/// of capture so the game can persist it into whatever per-actor HP store it maintains.
/// </summary>
public sealed class BattleActorCapturedEvent : DomainEvent
{
    public BattleActorCapturedEvent(
        string battleId,
        string capturerActorId,
        string capturedActorId,
        int hpAtCapture,
        int maxHpAtCapture,
        string? capturedSpeciesId = null)
        : base(nameof(BattleActorCapturedEvent))
    {
        BattleId = battleId;
        CapturerActorId = capturerActorId;
        CapturedActorId = capturedActorId;
        HpAtCapture = hpAtCapture;
        MaxHpAtCapture = maxHpAtCapture;
        CapturedSpeciesId = capturedSpeciesId;
    }

    public string BattleId { get; }

    public string CapturerActorId { get; }

    public string CapturedActorId { get; }

    public int HpAtCapture { get; }

    public int MaxHpAtCapture { get; }

    /// <summary>Species tag of the captured actor, if it was set on the definition. Drives bestiary tracking.</summary>
    public string? CapturedSpeciesId { get; }
}
