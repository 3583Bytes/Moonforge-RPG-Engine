using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Bestiary.Commands;

/// <summary>
/// Manually record a species as encountered and/or captured. Useful for scripted reveals
/// ("you spot a Mew flying overhead") or starter monster gifts that bypass the normal
/// battle/capture flow.
/// </summary>
public sealed class MarkSpeciesObservedCommand : ICommand
{
    public MarkSpeciesObservedCommand(string speciesId, bool encountered = true, bool captured = false)
    {
        SpeciesId = speciesId;
        Encountered = encountered;
        Captured = captured;
    }

    public string SpeciesId { get; }

    public bool Encountered { get; }

    public bool Captured { get; }
}
