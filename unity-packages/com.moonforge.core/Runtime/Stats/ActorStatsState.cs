using System;
using System.Collections.Generic;

namespace Moonforge.Core.Stats
{

    /// <summary>
    /// Per-actor stat blocks keyed by ActorId. Lives on <see cref="GameState"/> so stats persist
    /// across battles and are visible to non-combat systems (exploration, dialogue, shops).
    /// </summary>
    public sealed class ActorStatsState
    {
        private readonly Dictionary<string, StatBlock> _actors = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, StatBlock> Actors => _actors;

        public bool TryGet(string actorId, out StatBlock block)
        {
            return _actors.TryGetValue(actorId, out block!);
        }

        public StatBlock GetOrCreate(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException("Actor ID is required.", nameof(actorId));
            }

            if (!_actors.TryGetValue(actorId, out StatBlock block))
            {
                block = new StatBlock();
                _actors[actorId] = block;
            }

            return block;
        }

        public bool Remove(string actorId)
        {
            return _actors.Remove(actorId);
        }

        public void CopyFrom(ActorStatsState source)
        {
            _actors.Clear();
            foreach (KeyValuePair<string, StatBlock> pair in source._actors)
            {
                _actors[pair.Key] = pair.Value.Clone();
            }
        }
    }
}
