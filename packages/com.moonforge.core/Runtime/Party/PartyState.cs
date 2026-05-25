using System;
using System.Collections.Generic;

namespace Moonforge.Core.Party;

/// <summary>
/// Roster state for the player party. Holds an ordered list of <see cref="PartyMember"/>
/// entries plus the configured caps for active slots (combatants on the field) and total
/// roster size (active + reserve). Membership is enforced uniquely by <c>ActorId</c>; per-actor
/// stats and progression live in their own modules and are referenced by id.
///
/// Defaults to <see cref="MaxActive"/> = 1 and <see cref="MaxRoster"/> = 1 so the engine
/// matches its pre-Party single-hero behavior until a game calls
/// <see cref="Commands.ConfigurePartyCommand"/> with its real caps.
/// </summary>
public sealed class PartyState
{
    private readonly List<PartyMember> _members = new();

    public int MaxActive { get; private set; } = 1;

    public int MaxRoster { get; private set; } = 1;

    public IReadOnlyList<PartyMember> Members => _members;

    public int ActiveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].IsActive)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public void SetCaps(int maxActive, int maxRoster)
    {
        if (maxActive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxActive), "MaxActive must be positive.");
        }

        if (maxRoster < maxActive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRoster), "MaxRoster must be >= MaxActive.");
        }

        if (maxRoster < _members.Count)
        {
            throw new InvalidOperationException("MaxRoster cannot be set below current member count.");
        }

        if (maxActive < ActiveCount)
        {
            throw new InvalidOperationException("MaxActive cannot be set below current active count.");
        }

        MaxActive = maxActive;
        MaxRoster = maxRoster;
    }

    public bool TryGet(string actorId, out PartyMember member)
    {
        for (int i = 0; i < _members.Count; i++)
        {
            if (_members[i].ActorId == actorId)
            {
                member = _members[i];
                return true;
            }
        }

        member = null!;
        return false;
    }

    public bool Contains(string actorId)
    {
        return TryGet(actorId, out _);
    }

    public bool TryAdd(string actorId, bool active, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(actorId))
        {
            error = "Actor id is required.";
            return false;
        }

        if (Contains(actorId))
        {
            error = $"Actor '{actorId}' is already in the party.";
            return false;
        }

        if (_members.Count >= MaxRoster)
        {
            error = $"Roster is full ({MaxRoster}).";
            return false;
        }

        if (active && ActiveCount >= MaxActive)
        {
            error = $"All {MaxActive} active slot(s) are occupied.";
            return false;
        }

        _members.Add(new PartyMember(actorId, active));
        return true;
    }

    public bool TryRemove(string actorId)
    {
        for (int i = 0; i < _members.Count; i++)
        {
            if (_members[i].ActorId == actorId)
            {
                _members.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public bool TrySetActive(string actorId, bool active, out string? error)
    {
        error = null;

        if (!TryGet(actorId, out PartyMember member))
        {
            error = $"Actor '{actorId}' is not in the party.";
            return false;
        }

        if (member.IsActive == active)
        {
            return true;
        }

        if (active && ActiveCount >= MaxActive)
        {
            error = $"All {MaxActive} active slot(s) are occupied.";
            return false;
        }

        member.IsActive = active;
        return true;
    }

    public void CopyFrom(PartyState source)
    {
        MaxActive = source.MaxActive;
        MaxRoster = source.MaxRoster;
        _members.Clear();
        for (int i = 0; i < source._members.Count; i++)
        {
            _members.Add(source._members[i].Clone());
        }
    }
}
