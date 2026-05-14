using System;
using System.Collections.Generic;

namespace Moonforge.Core.Progression;

public sealed class ProgressionState
{
    private readonly Dictionary<string, ActorProgression> _actors = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, ActorProgression> Actors => _actors;

    public bool TryGet(string actorId, out ActorProgression progression)
    {
        return _actors.TryGetValue(actorId, out progression!);
    }

    public ActorProgression GetOrCreate(string actorId, string curveId, int level = 1, long xp = 0)
    {
        if (_actors.TryGetValue(actorId, out ActorProgression existing))
        {
            return existing;
        }

        ActorProgression created = new(actorId, curveId, level, xp);
        _actors[actorId] = created;
        return created;
    }

    public void Set(ActorProgression progression)
    {
        _actors[progression.ActorId] = progression;
    }

    public bool Remove(string actorId)
    {
        return _actors.Remove(actorId);
    }

    public void CopyFrom(ProgressionState source)
    {
        _actors.Clear();
        foreach (KeyValuePair<string, ActorProgression> pair in source._actors)
        {
            _actors[pair.Key] = pair.Value.Clone();
        }
    }
}
