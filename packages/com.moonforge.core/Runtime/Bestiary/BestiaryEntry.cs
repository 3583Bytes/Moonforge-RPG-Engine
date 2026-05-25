namespace Moonforge.Core.Bestiary;

/// <summary>
/// One species' entry in the bestiary. Counts encounters and captures separately and timestamps
/// the first of each via <see cref="Runtime.Time.IGameClock"/> minutes — game can compare or
/// format them however it likes.
/// </summary>
public sealed class BestiaryEntry
{
    public BestiaryEntry(string speciesId)
    {
        SpeciesId = speciesId;
    }

    public string SpeciesId { get; }

    public int EncounterCount { get; set; }

    public int CaptureCount { get; set; }

    public long? FirstEncounteredAtMinutes { get; set; }

    public long? FirstCapturedAtMinutes { get; set; }

    public bool IsEncountered => EncounterCount > 0;

    public bool IsCaptured => CaptureCount > 0;

    public BestiaryEntry Clone()
    {
        return new BestiaryEntry(SpeciesId)
        {
            EncounterCount = EncounterCount,
            CaptureCount = CaptureCount,
            FirstEncounteredAtMinutes = FirstEncounteredAtMinutes,
            FirstCapturedAtMinutes = FirstCapturedAtMinutes
        };
    }
}
