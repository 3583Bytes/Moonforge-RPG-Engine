namespace Moonforge.Core.Combat;

public sealed class ActiveStatusEffect
{
    public ActiveStatusEffect(string statusId, int remainingTurns, string? sourceActorId = null)
    {
        StatusId = statusId;
        RemainingTurns = remainingTurns;
        SourceActorId = sourceActorId;
    }

    public string StatusId { get; }

    public int RemainingTurns { get; set; }

    public string? SourceActorId { get; }

    public ActiveStatusEffect Clone()
    {
        return new ActiveStatusEffect(StatusId, RemainingTurns, SourceActorId);
    }
}
