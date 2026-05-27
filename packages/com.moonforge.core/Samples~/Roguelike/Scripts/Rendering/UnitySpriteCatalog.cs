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
                else
                {
                    // Loud signal that a real sprite is missing — placeholders will be
                    // generated for these, which is why the world can look uniformly grey.
                    Debug.LogWarning(
                        "[Roguelike] No sprite found at Resources/Sprites/" + entry.Value +
                        " for " + entry.Key +
                        ". Falling back to procedural placeholder. " +
                        "Check that the PNG exists, is in a Resources folder, and is imported as Sprite (2D and UI).");
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

            // Floor/Wall tiles fill the whole cell. Actors and markers should look like
            // sprites with transparent surroundings — otherwise they read as colored
            // boxes parked on top of the floor.
            bool isActorOrMarker = IsActorOrMarker(kind);
            Color baseFill = isActorOrMarker ? new Color(0f, 0f, 0f, 0f) : body;

            Color[] pixels = new Color[PlaceholderSize * PlaceholderSize];
            bool drawBorder = ShouldDrawBorder(kind);
            for (int y = 0; y < PlaceholderSize; y++)
            {
                for (int x = 0; x < PlaceholderSize; x++)
                {
                    bool onBorder = drawBorder && (x == 0 || y == 0 || x == PlaceholderSize - 1 || y == PlaceholderSize - 1);
                    pixels[y * PlaceholderSize + x] = onBorder ? border : baseFill;
                }
            }

            if (isActorOrMarker)
            {
                // Paint a centered character silhouette so the placeholder reads as a
                // creature/icon rather than a colored square.
                DrawActorSilhouette(pixels, body, border);
            }
            else
            {
                // Tile-specific surface texture for floors/walls.
                ApplyKindPattern(pixels, kind, body);
            }

            // Distinctive shape inside the body for actors / stairs / markers.
            ApplyKindGlyph(pixels, kind);

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(
                texture,
                new Rect(0, 0, PlaceholderSize, PlaceholderSize),
                pivot: new Vector2(0.5f, 0.5f),
                pixelsPerUnit: PlaceholderSize);
        }

        private static bool IsActorOrMarker(TileVisualKind kind)
        {
            return kind switch
            {
                TileVisualKind.Hero => true,
                TileVisualKind.Enemy => true,
                TileVisualKind.EnemyElite => true,
                TileVisualKind.EnemyBoss => true,
                TileVisualKind.Npc => true,
                TileVisualKind.TownGuardMarker => true,
                TileVisualKind.TownShopMarker => true,
                TileVisualKind.TownHealerMarker => true,
                TileVisualKind.TownAlchemistMarker => true,
                TileVisualKind.TownCacheMarker => true,
                TileVisualKind.TownFountainMarker => true,
                TileVisualKind.TownQuestBoardMarker => true,
                TileVisualKind.TownShrineMarker => true,
                TileVisualKind.DungeonStairsDown => true,
                TileVisualKind.DungeonStairsUp => true,
                _ => false
            };
        }

        private static void DrawActorSilhouette(Color[] pixels, Color body, Color outline)
        {
            // Stylized 16x16 character silhouette: legs at the bottom, torso in the
            // middle, head at the top. Per-row insets pulled from the bottom up.
            // y=0 is the bottom row of the texture (Unity convention).
            int sz = PlaceholderSize;
            int[] widthInset = new int[16]
            {
                16, 5, 5, 4, 4, 4, 3, 2, 3, 3, 4, 5, 5, 5, 6, 16
            };
            for (int y = 0; y < sz; y++)
            {
                int inset = widthInset[y];
                if (inset >= sz / 2) continue; // fully transparent row
                int xStart = inset;
                int xEnd = sz - inset;
                int aboveInset = y < sz - 1 ? widthInset[y + 1] : sz;
                int belowInset = y > 0 ? widthInset[y - 1] : sz;
                for (int x = xStart; x < xEnd; x++)
                {
                    bool onLeftEdge = x == xStart;
                    bool onRightEdge = x == xEnd - 1;
                    bool onBottomEdge = belowInset > inset && (x < belowInset || x >= sz - belowInset);
                    bool onTopEdge = aboveInset > inset && (x < aboveInset || x >= sz - aboveInset);
                    bool isOutline = onLeftEdge || onRightEdge || onTopEdge || onBottomEdge;
                    pixels[y * sz + x] = isOutline ? outline : body;
                }
            }
        }

        private static bool ShouldDrawBorder(TileVisualKind kind)
        {
            // Floor tiles tile against neighbours — a per-cell border makes them look like
            // a checkerboard. Skip the border for floors so the surface reads continuously.
            // Skip it for actors/markers too — they're sprites on transparent backgrounds,
            // a 1-pixel cell frame would render as a square halo around them.
            if (IsActorOrMarker(kind)) return false;
            return kind switch
            {
                TileVisualKind.DungeonFloor => false,
                TileVisualKind.TownFloor => false,
                _ => true
            };
        }

        private static void ApplyKindPattern(Color[] pixels, TileVisualKind kind, Color body)
        {
            // Cheap, deterministic surface textures so each tile reads as "stone floor" or
            // "wood wall" instead of "flat colored quad."
            switch (kind)
            {
                case TileVisualKind.TownFloor:
                    PaintTownFlagstones(pixels, body);
                    break;
                case TileVisualKind.DungeonFloor:
                    PaintDungeonCobbles(pixels, body);
                    break;
                case TileVisualKind.TownWall:
                {
                    // Wood-plank wall: warm brown body with vertical grain lines + a knot.
                    Color woodDark = MulColor(body, 0.55f);
                    Color woodLight = MulColor(body, 1.20f);
                    for (int y = 1; y < PlaceholderSize - 1; y++)
                    {
                        pixels[y * PlaceholderSize + 4] = woodDark;
                        pixels[y * PlaceholderSize + 9] = woodDark;
                        pixels[y * PlaceholderSize + 13] = woodDark;
                    }
                    // Knot
                    pixels[5 * PlaceholderSize + 6] = woodDark;
                    pixels[5 * PlaceholderSize + 7] = woodDark;
                    pixels[6 * PlaceholderSize + 6] = woodDark;
                    pixels[6 * PlaceholderSize + 7] = woodDark;
                    // Subtle highlight along the top of one plank
                    for (int x = 1; x < 4; x++) pixels[14 * PlaceholderSize + x] = woodLight;
                    for (int x = 10; x < 13; x++) pixels[14 * PlaceholderSize + x] = woodLight;
                    break;
                }
                case TileVisualKind.DungeonWall:
                {
                    // Cool-grey stone block with mortar seams in an offset brick pattern.
                    Color mortar = MulColor(body, 0.45f);
                    Color highlight = MulColor(body, 1.18f);
                    for (int x = 1; x < PlaceholderSize - 1; x++)
                    {
                        pixels[4 * PlaceholderSize + x] = mortar;
                        pixels[11 * PlaceholderSize + x] = mortar;
                    }
                    for (int y = 1; y < 4; y++) pixels[y * PlaceholderSize + 7] = mortar;
                    for (int y = 5; y < 11; y++) pixels[y * PlaceholderSize + 3] = mortar;
                    for (int y = 5; y < 11; y++) pixels[y * PlaceholderSize + 11] = mortar;
                    for (int y = 12; y < PlaceholderSize - 1; y++) pixels[y * PlaceholderSize + 7] = mortar;
                    // Top-row highlight on each brick row gives the bricks a sense of depth.
                    pixels[10 * PlaceholderSize + 5] = highlight;
                    pixels[10 * PlaceholderSize + 9] = highlight;
                    pixels[3 * PlaceholderSize + 9] = highlight;
                    pixels[3 * PlaceholderSize + 5] = highlight;
                    break;
                }
            }
        }

        private static Color MulColor(Color c, float k)
        {
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
        }

        /// <summary>
        /// 16x16 town floor: four warm tan flagstones with thick dark mortar between them
        /// and per-flagstone shade variation so the surface reads as stone tiles, not
        /// a flat color. High contrast so the pattern is visible at any zoom.
        /// </summary>
        private static void PaintTownFlagstones(Color[] pixels, Color body)
        {
            int sz = PlaceholderSize; // 16
            Color mortar = MulColor(body, 0.40f);
            Color tileA = MulColor(body, 0.92f);
            Color tileB = MulColor(body, 1.15f);
            Color speck = MulColor(body, 0.65f);

            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    // Mortar cross at the centre (x=7,8 and y=7,8) plus the outer border.
                    bool isMortar = x == 7 || x == 8 || y == 7 || y == 8 || x == 0 || y == 0;
                    if (isMortar)
                    {
                        pixels[y * sz + x] = mortar;
                        continue;
                    }
                    // Quadrant shade: opposite quadrants share a color (checkerboard of A/B).
                    bool leftHalf = x < 8;
                    bool bottomHalf = y < 8;
                    bool same = leftHalf == bottomHalf;
                    pixels[y * sz + x] = same ? tileA : tileB;
                }
            }

            // A handful of deterministic specks add per-tile texture so a wall of tiles
            // doesn't read as a perfect grid.
            pixels[2 * sz + 4] = speck;
            pixels[3 * sz + 11] = speck;
            pixels[11 * sz + 5] = speck;
            pixels[12 * sz + 12] = speck;
            pixels[5 * sz + 14] = speck;
            pixels[10 * sz + 2] = speck;
        }

        /// <summary>
        /// 16x16 dungeon floor: four round-ish cobblestones in a 2x2 arrangement with
        /// near-black mortar between them. Fake top-left highlight + bottom-right shade
        /// gives each cobble a sense of volume.
        /// </summary>
        private static void PaintDungeonCobbles(Color[] pixels, Color body)
        {
            int sz = PlaceholderSize; // 16
            Color mortar = MulColor(body, 0.30f);
            Color edgeShade = MulColor(body, 0.65f);
            Color highlight = MulColor(body, 1.30f);

            // Fill the whole cell with mortar first; cobbles overwrite.
            for (int i = 0; i < pixels.Length; i++) pixels[i] = mortar;

            // Four cobble origins (bottom-left corners) leaving a 1-pixel mortar strip
            // between each cobble and along the tile edges.
            (int x0, int y0)[] cobbles = new (int, int)[]
            {
                (1, 1), (8, 1), (1, 8), (8, 8)
            };
            const int Size = 7;
            foreach (var (x0, y0) in cobbles)
            {
                for (int dy = 0; dy < Size; dy++)
                {
                    for (int dx = 0; dx < Size; dx++)
                    {
                        // Round the corners: skip the four corner pixels of each cobble.
                        bool corner = (dx == 0 && dy == 0) || (dx == Size - 1 && dy == 0)
                                   || (dx == 0 && dy == Size - 1) || (dx == Size - 1 && dy == Size - 1);
                        if (corner) continue;

                        int px = x0 + dx;
                        int py = y0 + dy;
                        // Unity textures use Y-up. "Top" of the cobble visually is high Y.
                        bool topRow = dy == Size - 1 || dy == Size - 2;
                        bool leftCol = dx == 0 || dx == 1;
                        bool bottomRow = dy == 0 || dy == 1;
                        bool rightCol = dx == Size - 1 || dx == Size - 2;
                        Color c;
                        if (topRow || leftCol) c = highlight;
                        else if (bottomRow || rightCol) c = edgeShade;
                        else c = body;
                        pixels[py * sz + px] = c;
                    }
                }
            }
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
                    // No glyph for walls during diagnostic — the previous code did
                    // `glyph * 0.6f` which multiplies ALPHA too (Unity Color * float
                    // scales every component including alpha to 0.6). Skip it to rule
                    // out the partial-transparency row as the cause of invisibility.
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
            // Dungeon: cool blue-grey stone. Wall a touch lighter than the floor so the
            // brick pattern reads, plus distinctly darker than the highlights inside it.
            TileVisualKind.DungeonFloor => new Color(0.32f, 0.36f, 0.50f),
            TileVisualKind.DungeonWall => new Color(0.52f, 0.50f, 0.62f),
            TileVisualKind.DungeonPillar => new Color(0.30f, 0.32f, 0.42f),
            TileVisualKind.DungeonStairsDown => new Color(0.55f, 0.40f, 0.18f),
            TileVisualKind.DungeonStairsUp => new Color(0.40f, 0.55f, 0.18f),

            // Town: warm tan flagstones + brown wood walls. Wall darker than floor so
            // it reads as enclosing structure.
            TileVisualKind.TownFloor => new Color(0.74f, 0.58f, 0.32f),
            TileVisualKind.TownWall => new Color(0.48f, 0.28f, 0.14f),
            TileVisualKind.TownDoor => new Color(0.82f, 0.54f, 0.22f),
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

        // Floors and walls intentionally do NOT have PNG entries here — the bundled
        // Kenney crops shipped with this sample are best-guesses against an unlabelled
        // tilesheet and several of them are blank/transparent regions, which makes
        // walls invisible. The procedural placeholders (PaintTownFlagstones /
        // PaintDungeonCobbles / wood-grain town wall / brick dungeon wall) are
        // designed for visibility + scene-distinct identity, so we use those instead.
        //
        // If you want to override with real art, add the entries back:
        //     { TileVisualKind.DungeonFloor, "dungeon_floor" },
        //     { TileVisualKind.DungeonWall, "dungeon_wall" },
        //     { TileVisualKind.TownFloor, "town_floor" },
        //     { TileVisualKind.TownWall, "town_wall" },
        // ...and drop the PNGs into Art/Resources/Sprites/.
        public static readonly IReadOnlyDictionary<TileVisualKind, string> SpriteNames =
            new Dictionary<TileVisualKind, string>
            {
                { TileVisualKind.DungeonPillar, "dungeon_pillar" },
                { TileVisualKind.DungeonStairsDown, "stairs_down" },
                { TileVisualKind.DungeonStairsUp, "stairs_up" },

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
