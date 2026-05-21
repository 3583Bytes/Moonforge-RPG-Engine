using System;
using System.Collections.Generic;

namespace Moonforge.Core.Evolution;

/// <summary>
/// Per-actor eligibility map for evolution. An actor only auto-evolves through definitions
/// listed here; this is how a Caterpie's instance knows to watch the
/// <c>caterpie-to-metapod</c> evolution but not <c>charmander-to-charmeleon</c>. Set via
/// <see cref="Commands.ConfigureActorEvolutionsCommand"/>.
/// </summary>
public sealed class EvolutionState
{
    private readonly Dictionary<string, HashSet<string>> _byActor = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, HashSet<string>> ByActor => _byActor;

    public void Set(string actorId, IReadOnlyList<string> evolutionIds)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return;
        }

        HashSet<string> set = new(StringComparer.Ordinal);
        if (evolutionIds is not null)
        {
            for (int i = 0; i < evolutionIds.Count; i++)
            {
                string id = evolutionIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                {
                    set.Add(id);
                }
            }
        }

        if (set.Count == 0)
        {
            _byActor.Remove(actorId);
        }
        else
        {
            _byActor[actorId] = set;
        }
    }

    public bool IsEligible(string actorId, string evolutionId)
    {
        return _byActor.TryGetValue(actorId, out HashSet<string>? set) && set.Contains(evolutionId);
    }

    public IReadOnlyCollection<string> GetEvolutions(string actorId)
    {
        return _byActor.TryGetValue(actorId, out HashSet<string>? set)
            ? set
            : (IReadOnlyCollection<string>)Array.Empty<string>();
    }

    public bool RemoveEvolution(string actorId, string evolutionId)
    {
        if (_byActor.TryGetValue(actorId, out HashSet<string>? set) && set.Remove(evolutionId))
        {
            if (set.Count == 0)
            {
                _byActor.Remove(actorId);
            }

            return true;
        }

        return false;
    }

    public void Clear(string actorId)
    {
        _byActor.Remove(actorId);
    }

    public void CopyFrom(EvolutionState source)
    {
        _byActor.Clear();
        foreach (KeyValuePair<string, HashSet<string>> pair in source._byActor)
        {
            _byActor[pair.Key] = new HashSet<string>(pair.Value, StringComparer.Ordinal);
        }
    }
}
