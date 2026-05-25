namespace Moonforge.Core.Combat;

public enum StatusApplicationTarget
{
    Target = 0,
    Self = 1
}

public sealed class StatusApplicationDefinition
{
    public StatusApplicationDefinition(
        string statusId,
        StatusApplicationTarget targetMode = StatusApplicationTarget.Target,
        int chancePercent = 100,
        int? durationOverride = null)
    {
        StatusId = statusId;
        TargetMode = targetMode;
        ChancePercent = chancePercent < 0 ? 0 : (chancePercent > 100 ? 100 : chancePercent);
        DurationOverride = durationOverride;
    }

    public string StatusId { get; }

    public StatusApplicationTarget TargetMode { get; }

    public int ChancePercent { get; }

    public int? DurationOverride { get; }
}
