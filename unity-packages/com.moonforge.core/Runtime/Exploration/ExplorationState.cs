using System;
using System.Collections.Generic;

namespace Moonforge.Core.Exploration
{

    /// <summary>
    /// Holds every exploration map the game has configured (dungeon floors, towns,
    /// overworld regions, ...) plus which one is currently active. Each map carries its
    /// own actor set, so actors stay where they were left when the player switches maps.
    /// <see cref="Map"/> and <see cref="Actors"/> are views over the <em>active</em> map,
    /// which keeps single-map games working unchanged.
    /// </summary>
    public sealed class ExplorationState
    {
        private readonly Dictionary<string, ExplorationMapState> _maps = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, ExplorationActorState>> _actorsByMap = new(StringComparer.Ordinal);

        public ExplorationState()
        {
            // The initial, not-yet-configured map slot. The first configure re-keys this
            // slot to the configured map id so legacy single-map flows (and any held
            // references to Map) keep working.
            _maps[string.Empty] = new ExplorationMapState();
            _actorsByMap[string.Empty] = new Dictionary<string, ExplorationActorState>(StringComparer.Ordinal);
        }

        private string _activeMapId = string.Empty;

        /// <summary>Id of the active map — empty until the first map is configured.</summary>
        public string ActiveMapId
        {
            get
            {
                SyncActiveKey();
                return _activeMapId;
            }
        }

        /// <summary>The active map. Never null; unconfigured until the first configure.</summary>
        public ExplorationMapState Map
        {
            get
            {
                SyncActiveKey();
                return _maps[_activeMapId];
            }
        }

        /// <summary>Actors on the active map.</summary>
        public IReadOnlyDictionary<string, ExplorationActorState> Actors
        {
            get
            {
                SyncActiveKey();
                return _actorsByMap[_activeMapId];
            }
        }

        /// <summary>Ids of every configured map, sorted ordinal for deterministic iteration.</summary>
        public IReadOnlyList<string> MapIds
        {
            get
            {
                SyncActiveKey();
                List<string> ids = new();
                foreach (KeyValuePair<string, ExplorationMapState> pair in _maps)
                {
                    if (pair.Key.Length > 0 && pair.Value.IsConfigured)
                    {
                        ids.Add(pair.Key);
                    }
                }

                ids.Sort(StringComparer.Ordinal);
                return ids;
            }
        }

        public bool TryGetMap(string mapId, out ExplorationMapState map)
        {
            SyncActiveKey();
            return _maps.TryGetValue(mapId, out map!);
        }

        /// <summary>Actors on a specific map; empty for unknown map ids.</summary>
        public IReadOnlyDictionary<string, ExplorationActorState> GetActorsForMap(string mapId)
        {
            SyncActiveKey();
            return _actorsByMap.TryGetValue(mapId, out Dictionary<string, ExplorationActorState> actors)
                ? actors
                : EmptyActors;
        }

        private static readonly Dictionary<string, ExplorationActorState> EmptyActors = new(StringComparer.Ordinal);

        /// <summary>
        /// Creates or re-tiles the map with the given id and makes it active. The map keeps
        /// its existing actor set (the configure <em>command</em> clears it — direct state
        /// callers decide for themselves).
        /// </summary>
        public bool TryConfigureMap(
            string mapId,
            int width,
            int height,
            IReadOnlyList<ExplorationTileFlags> tiles,
            out string? error)
        {
            SyncActiveKey();

            if (_maps.TryGetValue(mapId, out ExplorationMapState existing))
            {
                if (!existing.TryConfigure(mapId, width, height, tiles, out error))
                {
                    return false;
                }

                _activeMapId = mapId;
                return true;
            }

            // First-ever configure: adopt the pristine initial slot (preserves references
            // to Map and any actors upserted before the first configure).
            if (_activeMapId.Length == 0 && !_maps[string.Empty].IsConfigured)
            {
                ExplorationMapState initial = _maps[string.Empty];
                if (!initial.TryConfigure(mapId, width, height, tiles, out error))
                {
                    return false;
                }

                RekeyMap(string.Empty, mapId);
                _activeMapId = mapId;
                return true;
            }

            ExplorationMapState fresh = new();
            if (!fresh.TryConfigure(mapId, width, height, tiles, out error))
            {
                return false;
            }

            _maps[mapId] = fresh;
            _actorsByMap[mapId] = new Dictionary<string, ExplorationActorState>(StringComparer.Ordinal);
            _activeMapId = mapId;
            return true;
        }

        /// <summary>Makes an already-configured map active. Its actor set becomes <see cref="Actors"/>.</summary>
        public bool TrySwitchMap(string mapId, out string? error)
        {
            SyncActiveKey();
            error = null;

            if (!_maps.TryGetValue(mapId, out ExplorationMapState map) || !map.IsConfigured)
            {
                error = $"Unknown or unconfigured exploration map '{mapId}'.";
                return false;
            }

            _activeMapId = mapId;
            return true;
        }

        /// <summary>Removes a non-active map and its actors.</summary>
        public bool TryRemoveMap(string mapId, out string? error)
        {
            SyncActiveKey();
            error = null;

            if (string.Equals(mapId, _activeMapId, StringComparison.Ordinal))
            {
                error = "Cannot remove the active exploration map.";
                return false;
            }

            if (!_maps.Remove(mapId))
            {
                error = $"Unknown exploration map '{mapId}'.";
                return false;
            }

            _actorsByMap.Remove(mapId);
            return true;
        }

        /// <summary>Removes an actor from a specific map. Returns false if absent.</summary>
        public bool RemoveActorFromMap(string mapId, string actorId)
        {
            SyncActiveKey();
            return _actorsByMap.TryGetValue(mapId, out Dictionary<string, ExplorationActorState> actors)
                && actors.Remove(actorId);
        }

        public bool TryGetActor(string actorId, out ExplorationActorState actor)
        {
            return ActiveActors.TryGetValue(actorId, out actor!);
        }

        public bool IsBlockingActorAt(GridPosition position, string? excludeActorId = null)
        {
            foreach ((string actorId, ExplorationActorState actor) in ActiveActors)
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
            Dictionary<string, ExplorationActorState> actors = ActiveActors;
            if (actors.TryGetValue(actorId, out ExplorationActorState existing))
            {
                existing.X = position.X;
                existing.Y = position.Y;
                existing.BlocksMovement = blocksMovement;
                return;
            }

            actors[actorId] = new ExplorationActorState(actorId, position, blocksMovement);
        }

        public void SetActorPosition(string actorId, GridPosition position)
        {
            if (ActiveActors.TryGetValue(actorId, out ExplorationActorState actor))
            {
                actor.X = position.X;
                actor.Y = position.Y;
            }
        }

        public void ClearActors()
        {
            ActiveActors.Clear();
        }

        public void CopyFrom(ExplorationState source)
        {
            source.SyncActiveKey();
            _maps.Clear();
            _actorsByMap.Clear();
            foreach ((string mapId, ExplorationMapState map) in source._maps)
            {
                ExplorationMapState mapClone = new();
                mapClone.CopyFrom(map);
                _maps[mapId] = mapClone;

                Dictionary<string, ExplorationActorState> actorsClone = new(StringComparer.Ordinal);
                foreach ((string actorId, ExplorationActorState actor) in source._actorsByMap[mapId])
                {
                    actorsClone[actorId] = actor.Clone();
                }

                _actorsByMap[mapId] = actorsClone;
            }

            _activeMapId = source._activeMapId;
        }

        private Dictionary<string, ExplorationActorState> ActiveActors
        {
            get
            {
                SyncActiveKey();
                return _actorsByMap[_activeMapId];
            }
        }

        /// <summary>
        /// Heals the registry after a legacy direct <c>Map.TryConfigure(newId, ...)</c> call:
        /// if the active instance's id no longer matches its registry key, re-key the map and
        /// its actor set (replacing any stale entry under the new id).
        /// </summary>
        private void SyncActiveKey()
        {
            ExplorationMapState active = _maps[_activeMapId];
            if (active.MapId.Length > 0 && !string.Equals(active.MapId, _activeMapId, StringComparison.Ordinal))
            {
                RekeyMap(_activeMapId, active.MapId);
                _activeMapId = active.MapId;
            }
        }

        private void RekeyMap(string oldKey, string newKey)
        {
            ExplorationMapState map = _maps[oldKey];
            Dictionary<string, ExplorationActorState> actors = _actorsByMap[oldKey];
            _maps.Remove(oldKey);
            _actorsByMap.Remove(oldKey);
            _maps[newKey] = map;
            _actorsByMap[newKey] = actors;
        }
    }
}
