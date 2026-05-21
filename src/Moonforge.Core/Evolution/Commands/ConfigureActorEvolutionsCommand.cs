using System.Collections.Generic;
using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Evolution.Commands;

/// <summary>
/// Registers the set of <see cref="Data.Definitions.EvolutionDefinition"/> ids the actor is
/// eligible to undergo. Replaces any prior eligibility list — pass an empty list to clear.
/// </summary>
public sealed class ConfigureActorEvolutionsCommand : ICommand
{
    public ConfigureActorEvolutionsCommand(string actorId, IReadOnlyList<string> evolutionIds)
    {
        ActorId = actorId;
        EvolutionIds = evolutionIds ?? System.Array.Empty<string>();
    }

    public string ActorId { get; }

    public IReadOnlyList<string> EvolutionIds { get; }
}
