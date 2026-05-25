using System;
using System.Collections.Generic;

namespace Moonforge.Core.Exploration;

public sealed class ExplorationState
{
    private readonly Dictionary<string, ExplorationActorState> _actors = new(StringComparer.Ordinal);

    public ExplorationMapState Map { get; } = new();

    public IReadOnlyDictionary<string, ExplorationActorState> Actors => _actors;

    public bool TryGetActor(string actorId, out ExplorationActorState actor)
    {
        return _actors.TryGetValue(actorId, out actor!);
    }

    public bool IsBlockingActorAt(GridPosition position, string? excludeActorId = null)
    {
        foreach ((string actorId, ExplorationActorState actor) in _actors)
        {
            if (excludeActorId is not null && string.Equals(actorId, excludeActorId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!actor.BlocksMovement)
            {
                continue;
            }

            if (actor.X == position.X && actor.Y == position.Y)
            {
                return true;
            }
        }

        return false;
    }

    public void UpsertActor(string actorId, GridPosition position, bool blocksMovement)
    {
        if (_actors.TryGetValue(actorId, out ExplorationActorState existing))
        {
            existing.X = position.X;
            existing.Y = position.Y;
            existing.BlocksMovement = blocksMovement;
            return;
        }

        _actors[actorId] = new ExplorationActorState(actorId, position, blocksMovement);
    }

    public void SetActorPosition(string actorId, GridPosition position)
    {
        if (_actors.TryGetValue(actorId, out ExplorationActorState actor))
        {
            actor.X = position.X;
            actor.Y = position.Y;
        }
    }

    public void ClearActors()
    {
        _actors.Clear();
    }

    public void CopyFrom(ExplorationState source)
    {
        Map.CopyFrom(source.Map);
        _actors.Clear();
        foreach ((string key, ExplorationActorState value) in source._actors)
        {
            _actors[key] = value.Clone();
        }
    }
}
