using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Moonforge.Sample.Roguelike.Rendering
{
    /// <summary>
    /// Loads sprites from the sample's <c>Resources/Sprites/</c> folder and exposes them keyed
    /// by <see cref="TileVisualKind"/>. Sprite file names are configured via
    /// <see cref="SpriteName"/> — drop the corresponding PNG into Resources/Sprites/ with the
    /// matching name and Unity will pick it up on its next compile.
    /// </summary>
    public sealed class UnitySpriteCatalog
    {
        private readonly Dictionary<TileVisualKind, Sprite> _sprites = new Dictionary<TileVisualKind, Sprite>();
        private readonly Dictionary<TileVisualKind, Tile> _tiles = new Dictionary<TileVisualKind, Tile>();
        private bool _loaded;

        public void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            LoadAll();
            _loaded = true;
        }

        public Sprite GetSprite(TileVisualKind kind)
        {
            EnsureLoaded();
            _sprites.TryGetValue(kind, out Sprite sprite);
            return sprite;
        }

        public TileBase GetTile(TileVisualKind kind)
        {
            EnsureLoaded();
            if (_tiles.TryGetValue(kind, out Tile tile))
            {
                return tile;
            }

            Sprite sprite = GetSprite(kind);
            if (sprite == null)
            {
                return null;
            }

            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            _tiles[kind] = tile;
            return tile;
        }

        private void LoadAll()
        {
            foreach (KeyValuePair<TileVisualKind, string> entry in SpriteNames)
            {
                Sprite sprite = Resources.Load<Sprite>("Sprites/" + entry.Value);
                if (sprite != null)
                {
                    _sprites[entry.Key] = sprite;
                }
                else
                {
                    Debug.LogWarning(
                        "[Moonforge.Roguelike] Missing sprite for " + entry.Key +
                        " — expected at Resources/Sprites/" + entry.Value + ".png. " +
                        "See Samples~/Roguelike/README.md for the Kenney pack mapping.");
                }
            }
        }

        // Maps each visual kind to a sprite file name (no extension). The README documents
        // which Kenney 1-bit tile each name corresponds to. Drop a PNG into Resources/Sprites/
        // and Unity will import it on next compile.
        public static readonly IReadOnlyDictionary<TileVisualKind, string> SpriteNames =
            new Dictionary<TileVisualKind, string>
            {
                { TileVisualKind.DungeonFloor, "dungeon_floor" },
                { TileVisualKind.DungeonWall, "dungeon_wall" },
                { TileVisualKind.DungeonPillar, "dungeon_pillar" },
                { TileVisualKind.DungeonStairsDown, "stairs_down" },
                { TileVisualKind.DungeonStairsUp, "stairs_up" },

                { TileVisualKind.TownFloor, "town_floor" },
                { TileVisualKind.TownWall, "town_wall" },
                { TileVisualKind.TownDoor, "town_door" },
                { TileVisualKind.TownShopMarker, "marker_shop" },
                { TileVisualKind.TownHealerMarker, "marker_healer" },
                { TileVisualKind.TownAlchemistMarker, "marker_alchemist" },
                { TileVisualKind.TownGuardMarker, "marker_guard" },
                { TileVisualKind.TownCacheMarker, "marker_cache" },
                { TileVisualKind.TownFountainMarker, "marker_fountain" },
                { TileVisualKind.TownQuestBoardMarker, "marker_questboard" },
                { TileVisualKind.TownShrineMarker, "marker_shrine" },

                { TileVisualKind.Hero, "hero" },
                { TileVisualKind.Enemy, "enemy" },
                { TileVisualKind.EnemyElite, "enemy_elite" },
                { TileVisualKind.EnemyBoss, "enemy_boss" },
                { TileVisualKind.Npc, "npc" }
            };
    }
}
