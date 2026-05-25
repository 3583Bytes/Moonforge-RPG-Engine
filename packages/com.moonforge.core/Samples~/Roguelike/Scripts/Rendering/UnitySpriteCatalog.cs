using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Moonforge.Sample.Roguelike.Rendering
{
    /// <summary>
    /// Source of sprites + tile assets keyed by <see cref="TileVisualKind"/>. Tries to load
    /// each kind from the sample's <c>Resources/Sprites/</c> folder first; if a sprite isn't
    /// present, falls back to a runtime-generated coloured placeholder so the sample looks
    /// alive even before the user drops in a real tileset.
    /// </summary>
    public sealed class UnitySpriteCatalog
    {
        private const int PlaceholderSize = 16;

        private readonly Dictionary<TileVisualKind, Sprite> _sprites = new Dictionary<TileVisualKind, Sprite>();
        private readonly Dictionary<TileVisualKind, Tile> _tiles = new Dictionary<TileVisualKind, Tile>();
        private bool _loaded;

        public void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            LoadFromResources();
            _loaded = true;
        }

        public Sprite GetSprite(TileVisualKind kind)
        {
            EnsureLoaded();
            if (!_sprites.TryGetValue(kind, out Sprite sprite) || sprite == null)
            {
                sprite = GeneratePlaceholderSprite(kind);
                _sprites[kind] = sprite;
            }
            return sprite;
        }

        public TileBase GetTile(TileVisualKind kind)
        {
            if (_tiles.TryGetValue(kind, out Tile tile) && tile != null)
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

        private void LoadFromResources()
        {
            foreach (KeyValuePair<TileVisualKind, string> entry in SpriteNames)
            {
                Sprite sprite = Resources.Load<Sprite>("Sprites/" + entry.Value);
                if (sprite != null)
                {
                    _sprites[entry.Key] = sprite;
                }
            }
        }

        private static Sprite GeneratePlaceholderSprite(TileVisualKind kind)
        {
            Color body = GetPlaceholderColor(kind);
            Color border = new Color(body.r * 0.5f, body.g * 0.5f, body.b * 0.5f, body.a);

            Texture2D texture = new Texture2D(PlaceholderSize, PlaceholderSize, TextureFormat.RGBA32, mipChain: false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[PlaceholderSize * PlaceholderSize];
            for (int y = 0; y < PlaceholderSize; y++)
            {
                for (int x = 0; x < PlaceholderSize; x++)
                {
                    bool onBorder = x == 0 || y == 0 || x == PlaceholderSize - 1 || y == PlaceholderSize - 1;
                    pixels[y * PlaceholderSize + x] = onBorder ? border : body;
                }
            }

            // Distinctive shape inside the body so similar-coloured kinds still read differently.
            ApplyKindGlyph(pixels, kind);

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(
                texture,
                new Rect(0, 0, PlaceholderSize, PlaceholderSize),
                pivot: new Vector2(0.5f, 0.5f),
                pixelsPerUnit: PlaceholderSize);
        }

        private static void ApplyKindGlyph(Color[] pixels, TileVisualKind kind)
        {
            Color glyph = new Color(1f, 1f, 1f, 1f);
            switch (kind)
            {
                case TileVisualKind.Hero:
                    // A '+' shape — distinct, centred.
                    DrawHLine(pixels, 4, 11, 7, glyph);
                    DrawVLine(pixels, 7, 4, 11, glyph);
                    break;
                case TileVisualKind.Enemy:
                case TileVisualKind.EnemyElite:
                case TileVisualKind.EnemyBoss:
                    // Two diagonal lines — an 'X'.
                    for (int i = 4; i <= 11; i++) pixels[i * PlaceholderSize + i] = glyph;
                    for (int i = 4; i <= 11; i++) pixels[i * PlaceholderSize + (15 - i)] = glyph;
                    break;
                case TileVisualKind.DungeonStairsDown:
                    // Downward arrow.
                    DrawVLine(pixels, 7, 3, 10, glyph);
                    pixels[3 * PlaceholderSize + 5] = glyph;
                    pixels[3 * PlaceholderSize + 6] = glyph;
                    pixels[3 * PlaceholderSize + 8] = glyph;
                    pixels[3 * PlaceholderSize + 9] = glyph;
                    pixels[4 * PlaceholderSize + 6] = glyph;
                    pixels[4 * PlaceholderSize + 8] = glyph;
                    break;
                case TileVisualKind.DungeonStairsUp:
                    DrawVLine(pixels, 7, 5, 12, glyph);
                    pixels[12 * PlaceholderSize + 5] = glyph;
                    pixels[12 * PlaceholderSize + 6] = glyph;
                    pixels[12 * PlaceholderSize + 8] = glyph;
                    pixels[12 * PlaceholderSize + 9] = glyph;
                    pixels[11 * PlaceholderSize + 6] = glyph;
                    pixels[11 * PlaceholderSize + 8] = glyph;
                    break;
                case TileVisualKind.DungeonPillar:
                    // Center dot block.
                    for (int y = 5; y <= 10; y++)
                    {
                        for (int x = 5; x <= 10; x++) pixels[y * PlaceholderSize + x] = glyph;
                    }
                    break;
                case TileVisualKind.TownDoor:
                    // Rectangle outline.
                    for (int y = 4; y <= 11; y++)
                    {
                        pixels[y * PlaceholderSize + 5] = glyph;
                        pixels[y * PlaceholderSize + 10] = glyph;
                    }
                    DrawHLine(pixels, 5, 10, 11, glyph);
                    break;
                case TileVisualKind.Npc:
                case TileVisualKind.TownGuardMarker:
                case TileVisualKind.TownShopMarker:
                case TileVisualKind.TownHealerMarker:
                case TileVisualKind.TownAlchemistMarker:
                case TileVisualKind.TownCacheMarker:
                case TileVisualKind.TownFountainMarker:
                case TileVisualKind.TownQuestBoardMarker:
                case TileVisualKind.TownShrineMarker:
                    // A solid centre block so markers read as "something here".
                    for (int y = 5; y <= 10; y++)
                    {
                        for (int x = 5; x <= 10; x++) pixels[y * PlaceholderSize + x] = glyph;
                    }
                    break;
                case TileVisualKind.DungeonWall:
                case TileVisualKind.TownWall:
                    // Brick-pattern hint: a single horizontal seam.
                    for (int x = 1; x < PlaceholderSize - 1; x++)
                    {
                        pixels[8 * PlaceholderSize + x] = glyph * 0.6f;
                    }
                    break;
                // DungeonFloor / TownFloor / Empty: leave plain body — they're the background.
            }
        }

        private static void DrawHLine(Color[] pixels, int x0, int x1, int y, Color color)
        {
            for (int x = x0; x <= x1; x++) pixels[y * PlaceholderSize + x] = color;
        }

        private static void DrawVLine(Color[] pixels, int x, int y0, int y1, Color color)
        {
            for (int y = y0; y <= y1; y++) pixels[y * PlaceholderSize + x] = color;
        }

        private static Color GetPlaceholderColor(TileVisualKind kind) => kind switch
        {
            TileVisualKind.DungeonFloor => new Color(0.22f, 0.22f, 0.24f),
            TileVisualKind.DungeonWall => new Color(0.10f, 0.10f, 0.13f),
            TileVisualKind.DungeonPillar => new Color(0.30f, 0.30f, 0.35f),
            TileVisualKind.DungeonStairsDown => new Color(0.55f, 0.40f, 0.18f),
            TileVisualKind.DungeonStairsUp => new Color(0.40f, 0.55f, 0.18f),

            TileVisualKind.TownFloor => new Color(0.45f, 0.42f, 0.30f),
            TileVisualKind.TownWall => new Color(0.50f, 0.30f, 0.18f),
            TileVisualKind.TownDoor => new Color(0.65f, 0.45f, 0.20f),
            TileVisualKind.TownShopMarker => new Color(0.92f, 0.74f, 0.20f),
            TileVisualKind.TownHealerMarker => new Color(0.92f, 0.30f, 0.45f),
            TileVisualKind.TownAlchemistMarker => new Color(0.80f, 0.32f, 0.78f),
            TileVisualKind.TownGuardMarker => new Color(0.30f, 0.55f, 0.90f),
            TileVisualKind.TownCacheMarker => new Color(0.72f, 0.55f, 0.25f),
            TileVisualKind.TownFountainMarker => new Color(0.30f, 0.65f, 0.95f),
            TileVisualKind.TownQuestBoardMarker => new Color(0.65f, 0.40f, 0.18f),
            TileVisualKind.TownShrineMarker => new Color(0.75f, 0.75f, 0.95f),

            TileVisualKind.Hero => new Color(0.95f, 0.92f, 0.20f),
            TileVisualKind.Enemy => new Color(0.85f, 0.20f, 0.20f),
            TileVisualKind.EnemyElite => new Color(0.95f, 0.35f, 0.10f),
            TileVisualKind.EnemyBoss => new Color(0.75f, 0.05f, 0.75f),
            TileVisualKind.Npc => new Color(0.35f, 0.80f, 0.40f),
            _ => Color.magenta
        };

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
