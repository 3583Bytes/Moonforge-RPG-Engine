using System;
using System.Collections.Generic;

namespace Moonforge.Core.Combat;

/// <summary>
/// Persistent per-actor PP store. An actor is "tracked" once it has any entry here;
/// untracked actors (wild enemies in a roguelike, for example) get fresh max-PP every
/// battle and don't leave footprints in this state.
///
/// Hydration: battle start fills <see cref="BattleActorState.SkillPp"/> from this state if
/// the actor is tracked, else initializes from <see cref="BattleSkillDefinition.MaxPp"/>.
///
/// Persistence: battle end writes back per-skill PP for tracked actors only. Mid-battle
/// transitions (swap-out, capture) also flush PP for the affected actor when tracked.
/// </summary>
public sealed class ActorSkillPpState
{
    private readonly Dictionary<string, Dictionary<string, int>> _byActor = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, Dictionary<string, int>> ByActor => _byActor;

    public bool IsTracked(string actorId)
    {
        return _byActor.ContainsKey(actorId);
    }

    public bool TryGetSkillPp(string actorId, string skillId, out int pp)
    {
        if (_byActor.TryGetValue(actorId, out Dictionary<string, int>? skills)
            && skills.TryGetValue(skillId, out pp))
        {
            return true;
        }

        pp = 0;
        return false;
    }

    public IReadOnlyDictionary<string, int>? GetActorSkillPp(string actorId)
    {
        return _byActor.TryGetValue(actorId, out Dictionary<string, int>? skills) ? skills : null;
    }

    /// <summary>
    /// Starts tracking PP for the actor (creates an empty entry if one doesn't exist).
    /// Future battles will hydrate from and persist to this entry. Idempotent.
    /// </summary>
    public void EnsureTracked(string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return;
        }

        if (!_byActor.ContainsKey(actorId))
        {
            _byActor[actorId] = new Dictionary<string, int>(StringComparer.Ordinal);
        }
    }

    public void SetSkillPp(string actorId, string skillId, int pp)
    {
        if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(skillId))
        {
            return;
        }

        if (!_byActor.TryGetValue(actorId, out Dictionary<string, int>? skills))
        {
            skills = new Dictionary<string, int>(StringComparer.Ordinal);
            _byActor[actorId] = skills;
        }

        skills[skillId] = pp < 0 ? 0 : pp;
    }

    public bool Remove(string actorId)
    {
        return _byActor.Remove(actorId);
    }

    public void CopyFrom(ActorSkillPpState source)
    {
        _byActor.Clear();
        foreach (KeyValuePair<string, Dictionary<string, int>> pair in source._byActor)
        {
            Dictionary<string, int> skills = new(pair.Value.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> sk in pair.Value)
            {
                skills[sk.Key] = sk.Value;
            }

            _byActor[pair.Key] = skills;
        }
    }
}
