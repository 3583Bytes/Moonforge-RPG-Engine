using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Moonforge.Sample.Roguelike.Rendering
{
    /// <summary>
    /// Inspector-friendly bag of optional Sprite overrides — exposed on
    /// <c>RoguelikeBootstrap</c> so the user can drag-drop their own art per
    /// tile kind without touching code. Any slot left null falls through to the
    /// catalog's normal load chain: Resources/Sprites/&lt;name&gt;.png, then
    /// procedural placeholder.
    /// </summary>
    [Serializable]
    public sealed class SpriteSlots
    {
        [Header("Dungeon")]
        public Sprite DungeonFloor;
        public Sprite DungeonWall;
        public Sprite DungeonPillar;
        public Sprite DungeonStairsDown;
        public Sprite DungeonStairsUp;

        [Header("Town")]
        public Sprite TownFloor;
        public Sprite TownWall;
        public Sprite TownDoor;

        [Header("Town Markers")]
        public Sprite ShopMarker;
        public Sprite HealerMarker;
        public Sprite AlchemistMarker;
        public Sprite GuardMarker;
        public Sprite CacheMarker;
        public Sprite FountainMarker;
        public Sprite QuestBoardMarker;
        public Sprite ShrineMarker;

        [Header("Characters")]
        public Sprite Hero;
        public Sprite Enemy;
        public Sprite EnemyElite;
        public Sprite EnemyBoss;
        public Sprite Npc;

        [Header("Per-Class Hero Overrides (optional — fall back to Hero)")]
        [Tooltip("One entry per class id (Knight, Ranger, Arcanist, or your own). " +
                 "Any class without an entry falls back to Resources/Sprites/hero_<classid>.png, then Hero.")]
        public List<HeroClassSpriteSlot> HeroByClass = new List<HeroClassSpriteSlot>();

        [Header("Inventory Icons (optional — leave null for procedural)")]
        public Sprite WeaponIcon;
        public Sprite ArmorIcon;
        public Sprite AccessoryIcon;
    }

    /// <summary>
    /// One per-class hero sprite override: the <see cref="ClassId"/> matches a
    /// <c>PlayerClass</c> enum name (case-insensitive), e.g. "Knight".
    /// <see cref="Sprite"/> is the any-direction default; the four directional slots are
    /// optional — a missing Left/Right falls back to mirroring the other side, and any
    /// missing direction falls back to <see cref="Sprite"/>, then the global Hero chain.
    /// </summary>
    [Serializable]
    public sealed class HeroClassSpriteSlot
    {
        public string ClassId;
        public Sprite Sprite;

        [Header("Directional (optional)")]
        public Sprite Down;
        public Sprite Up;
        public Sprite Left;
        public Sprite Right;
    }

    /// <summary>
    /// Source of sprites + tile assets keyed by <see cref="TileVisualKind"/>.
    /// Resolution order: Inspector-assigned overrides (via
    /// <see cref="ApplyOverrides"/>) → PNG at <c>Resources/Sprites/&lt;name&gt;.png</c>
    /// → runtime-generated procedural placeholder.
    /// </summary>
    public sealed class UnitySpriteCatalog
    {
        private const int PlaceholderSize = 16;

        private readonly Dictionary<TileVisualKind, Sprite> _sprites = new Dictionary<TileVisualKind, Sprite>();
        private readonly Dictionary<TileVisualKind, Tile> _tiles = new Dictionary<TileVisualKind, Tile>();
        // Hero sprite cache keyed by "<classid>", "<classid>|<facing>", or "|<facing>"
        // (classless directional). A null value means "looked up the matching
        // Resources/Sprites PNG, nothing there — fall through the resolution chain".
        private readonly Dictionary<string, Sprite> _heroClassSprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
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

        /// <summary>
        /// Hero sprite for the given class id (a <c>PlayerClass</c> enum name, e.g. "Knight"),
        /// facing Down. See the facing overload for the full resolution chain.
        /// </summary>
        public Sprite GetHeroSprite(string classId)
        {
            return GetHeroSprite(classId, FacingDirection.Down, out _);
        }

        /// <summary>
        /// Hero sprite for the given class id and facing. Resolution order (first hit wins);
        /// every step checks the Inspector override (SpriteSlots.HeroByClass), then
        /// <c>Resources/Sprites/&lt;name&gt;.png</c>:
        /// <list type="number">
        /// <item>class + facing — <c>hero_&lt;classid&gt;_&lt;facing&gt;.png</c> (e.g. hero_knight_left.png)</item>
        /// <item>class + mirrored side (Left↔Right) with <paramref name="flipX"/> = true</item>
        /// <item>class default — <c>hero_&lt;classid&gt;.png</c></item>
        /// <item>classless facing — <c>hero_&lt;facing&gt;.png</c> (e.g. hero_left.png)</item>
        /// <item>classless mirrored side with <paramref name="flipX"/> = true</item>
        /// <item>the regular Hero sprite (hero.png → procedural placeholder)</item>
        /// </list>
        /// So a single side-view sprite covers both Left and Right, and all art is optional.
        /// </summary>
        public Sprite GetHeroSprite(string classId, FacingDirection facing, out bool flipX)
        {
            EnsureLoaded();
            flipX = false;

            string face = FacingName(facing);
            string mirror = facing == FacingDirection.Left ? "right"
                : facing == FacingDirection.Right ? "left"
                : null;

            if (!string.IsNullOrEmpty(classId))
            {
                string cls = classId.ToLowerInvariant();
                if (TryResolveHero(cls + "|" + face, "hero_" + cls + "_" + face, out Sprite sprite))
                {
                    return sprite;
                }

                if (mirror != null && TryResolveHero(cls + "|" + mirror, "hero_" + cls + "_" + mirror, out sprite))
                {
                    flipX = true;
                    return sprite;
                }

                if (TryResolveHero(cls, "hero_" + cls, out sprite))
                {
                    return sprite;
                }
            }

            if (TryResolveHero("|" + face, "hero_" + face, out Sprite generic))
            {
                return generic;
            }

            if (mirror != null && TryResolveHero("|" + mirror, "hero_" + mirror, out generic))
            {
                flipX = true;
                return generic;
            }

            return GetSprite(TileVisualKind.Hero);
        }

        private static string FacingName(FacingDirection facing) => facing switch
        {
            FacingDirection.Up => "up",
            FacingDirection.Left => "left",
            FacingDirection.Right => "right",
            _ => "down"
        };

        /// <summary>
        /// Checks the hero-sprite cache for <paramref name="key"/>; on first miss attempts
        /// <c>Resources/Sprites/&lt;resourceName&gt;.png</c> and caches the result either way
        /// (null = checked, not present) so repeated lookups never re-hit Resources.
        /// </summary>
        private bool TryResolveHero(string key, string resourceName, out Sprite sprite)
        {
            if (!_heroClassSprites.TryGetValue(key, out sprite))
            {
                sprite = Resources.Load<Sprite>("Sprites/" + resourceName);
                _heroClassSprites[key] = sprite;
            }

            return sprite != null;
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

        /// <summary>
        /// Push any non-null Inspector-assigned sprites into the catalog. Must be
        /// called BEFORE <see cref="EnsureLoaded"/> so the Resources.Load pass
        /// (and any subsequent placeholder generation) can skip kinds the user
        /// has explicitly assigned.
        /// </summary>
        public void ApplyOverrides(SpriteSlots slots)
        {
            if (slots == null) return;
            SetOverride(TileVisualKind.DungeonFloor, slots.DungeonFloor);
            SetOverride(TileVisualKind.DungeonWall, slots.DungeonWall);
            SetOverride(TileVisualKind.DungeonPillar, slots.DungeonPillar);
            SetOverride(TileVisualKind.DungeonStairsDown, slots.DungeonStairsDown);
            SetOverride(TileVisualKind.DungeonStairsUp, slots.DungeonStairsUp);
            SetOverride(TileVisualKind.TownFloor, slots.TownFloor);
            SetOverride(TileVisualKind.TownWall, slots.TownWall);
            SetOverride(TileVisualKind.TownDoor, slots.TownDoor);
            SetOverride(TileVisualKind.TownShopMarker, slots.ShopMarker);
            SetOverride(TileVisualKind.TownHealerMarker, slots.HealerMarker);
            SetOverride(TileVisualKind.TownAlchemistMarker, slots.AlchemistMarker);
            SetOverride(TileVisualKind.TownGuardMarker, slots.GuardMarker);
            SetOverride(TileVisualKind.TownCacheMarker, slots.CacheMarker);
            SetOverride(TileVisualKind.TownFountainMarker, slots.FountainMarker);
            SetOverride(TileVisualKind.TownQuestBoardMarker, slots.QuestBoardMarker);
            SetOverride(TileVisualKind.TownShrineMarker, slots.ShrineMarker);
            SetOverride(TileVisualKind.Hero, slots.Hero);
            SetOverride(TileVisualKind.Enemy, slots.Enemy);
            SetOverride(TileVisualKind.EnemyElite, slots.EnemyElite);
            SetOverride(TileVisualKind.EnemyBoss, slots.EnemyBoss);
            SetOverride(TileVisualKind.Npc, slots.Npc);
            SetOverride(TileVisualKind.WeaponIcon, slots.WeaponIcon);
            SetOverride(TileVisualKind.ArmorIcon, slots.ArmorIcon);
            SetOverride(TileVisualKind.AccessoryIcon, slots.AccessoryIcon);

            if (slots.HeroByClass != null)
            {
                foreach (HeroClassSpriteSlot slot in slots.HeroByClass)
                {
                    if (slot == null || string.IsNullOrEmpty(slot.ClassId))
                    {
                        continue;
                    }

                    SetHeroOverride(slot.ClassId, slot.Sprite);
                    SetHeroOverride(slot.ClassId + "|down", slot.Down);
                    SetHeroOverride(slot.ClassId + "|up", slot.Up);
                    SetHeroOverride(slot.ClassId + "|left", slot.Left);
                    SetHeroOverride(slot.ClassId + "|right", slot.Right);
                }
            }
        }

        private void SetHeroOverride(string key, Sprite sprite)
        {
            if (sprite != null)
            {
                _heroClassSprites[key] = sprite;
            }
        }

        private void SetOverride(TileVisualKind kind, Sprite sprite)
        {
            if (sprite != null)
            {
                _sprites[kind] = sprite;
            }
        }

        private void LoadFromResources()
        {
            foreach (KeyValuePair<TileVisualKind, string> entry in SpriteNames)
            {
                // Skip if an Inspector override already populated this kind.
                if (_sprites.TryGetValue(entry.Key, out Sprite existing) && existing != null)
                {
                    continue;
                }

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
                        "Check that the PNG exists, is in a Resources folder, and is imported as Sprite (2D and UI), " +
                        "or assign one in the Roguelike Bootstrap inspector under Sprite Slots.");
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

            // Floor/Wall tiles fill the whole cell. Actors, markers, and inventory
            // item icons should look like sprites with transparent surroundings —
            // otherwise they read as colored boxes parked on top of the background.
            bool isItemIcon = IsItemIcon(kind);
            bool isActorOrMarker = !isItemIcon && IsActorOrMarker(kind);
            Color baseFill = (isActorOrMarker || isItemIcon) ? new Color(0f, 0f, 0f, 0f) : body;

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

            if (isItemIcon)
            {
                // Inventory-screen item silhouette — sword/shield/ring drawn in the body
                // color. No separate glyph pass; the icon shape is the entire visual.
                DrawItemIcon(pixels, kind, body, border);
            }
            else if (isActorOrMarker)
            {
                // Paint a centered character silhouette so the placeholder reads as a
                // creature/icon rather than a colored square.
                DrawActorSilhouette(pixels, body, border);
                // Distinctive shape inside the body for actors / stairs / markers.
                ApplyKindGlyph(pixels, kind);
            }
            else
            {
                // Tile-specific surface texture for floors/walls.
                ApplyKindPattern(pixels, kind, body);
                ApplyKindGlyph(pixels, kind);
            }

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

        private static bool IsItemIcon(TileVisualKind kind) => kind switch
        {
            TileVisualKind.WeaponIcon => true,
            TileVisualKind.ArmorIcon => true,
            TileVisualKind.AccessoryIcon => true,
            _ => false
        };

        private static void DrawItemIcon(Color[] pixels, TileVisualKind kind, Color body, Color outline)
        {
            // 16x16, y=0 at the bottom. Shapes are pixel-art silhouettes — recognisable
            // without being detailed. Body = tier colour, outline = shaded.
            switch (kind)
            {
                case TileVisualKind.WeaponIcon:
                    // Sword pointing up: long 2-wide blade, horizontal crossguard, short hilt.
                    for (int y = 4; y <= 13; y++)
                    {
                        pixels[y * PlaceholderSize + 7] = body;
                        pixels[y * PlaceholderSize + 8] = body;
                    }
                    pixels[14 * PlaceholderSize + 7] = outline; // blade tip shading
                    pixels[14 * PlaceholderSize + 8] = outline;
                    for (int x = 4; x <= 11; x++)
                    {
                        pixels[3 * PlaceholderSize + x] = outline;
                    }
                    pixels[2 * PlaceholderSize + 7] = body;
                    pixels[2 * PlaceholderSize + 8] = body;
                    pixels[1 * PlaceholderSize + 7] = outline;
                    pixels[1 * PlaceholderSize + 8] = outline;
                    break;

                case TileVisualKind.ArmorIcon:
                    // Heraldic shield: wide flat top, tapers to a point at the bottom.
                    for (int x = 3; x <= 12; x++)
                    {
                        pixels[13 * PlaceholderSize + x] = outline;
                    }
                    for (int y = 6; y <= 12; y++)
                    {
                        for (int x = 3; x <= 12; x++)
                        {
                            pixels[y * PlaceholderSize + x] = body;
                        }
                    }
                    // Taper toward bottom.
                    for (int x = 4; x <= 11; x++) pixels[5 * PlaceholderSize + x] = body;
                    for (int x = 5; x <= 10; x++) pixels[4 * PlaceholderSize + x] = body;
                    for (int x = 6; x <= 9; x++) pixels[3 * PlaceholderSize + x] = body;
                    for (int x = 7; x <= 8; x++) pixels[2 * PlaceholderSize + x] = body;
                    // Outline the sides.
                    for (int y = 6; y <= 12; y++)
                    {
                        pixels[y * PlaceholderSize + 3] = outline;
                        pixels[y * PlaceholderSize + 12] = outline;
                    }
                    pixels[5 * PlaceholderSize + 4] = outline;
                    pixels[5 * PlaceholderSize + 11] = outline;
                    pixels[4 * PlaceholderSize + 5] = outline;
                    pixels[4 * PlaceholderSize + 10] = outline;
                    pixels[3 * PlaceholderSize + 6] = outline;
                    pixels[3 * PlaceholderSize + 9] = outline;
                    pixels[2 * PlaceholderSize + 7] = outline;
                    pixels[2 * PlaceholderSize + 8] = outline;
                    break;

                case TileVisualKind.AccessoryIcon:
                    // Ring: hollow circle (band) centred. Outer radius ~5, inner ~3.
                    int cx = 8, cy = 8;
                    for (int y = 0; y < PlaceholderSize; y++)
                    {
                        for (int x = 0; x < PlaceholderSize; x++)
                        {
                            int dx = x - cx;
                            int dy = y - cy;
                            int d2 = dx * dx + dy * dy;
                            if (d2 <= 30 && d2 >= 10)
                            {
                                pixels[y * PlaceholderSize + x] = body;
                            }
                            else if (d2 <= 36 && d2 >= 6)
                            {
                                pixels[y * PlaceholderSize + x] = outline;
                            }
                        }
                    }
                    // Gem at the top.
                    pixels[13 * PlaceholderSize + 7] = outline;
                    pixels[13 * PlaceholderSize + 8] = outline;
                    pixels[14 * PlaceholderSize + 7] = body;
                    pixels[14 * PlaceholderSize + 8] = body;
                    pixels[15 * PlaceholderSize + 7] = outline;
                    pixels[15 * PlaceholderSize + 8] = outline;
                    break;
            }
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
            // 16x16 brick-path texture: four rows of bricks, alternating offset
            // every other row. Strong mortar contrast + per-brick shade variation
            // + top-edge sheen so the bricks read as real masonry instead of a
            // tinted square. Each brick is 3 pixels tall + 1 pixel mortar.
            int sz = PlaceholderSize;
            Color mortar = MulColor(body, 0.28f);
            Color brickLight = MulColor(body, 1.10f);
            Color brickMid = MulColor(body, 0.95f);
            Color brickDark = MulColor(body, 0.78f);
            Color edgeHighlight = MulColor(body, 1.35f);
            Color edgeShadow = MulColor(body, 0.55f);
            Color speck = MulColor(body, 0.45f);

            // Fill cell with mortar; bricks overwrite.
            for (int i = 0; i < pixels.Length; i++) pixels[i] = mortar;

            // Row 0 (y=0..2), normal layout.
            DrawTownBrick(pixels, sz, x0: 0, x1: 6, yStart: 0, body: brickLight, top: edgeHighlight, bot: edgeShadow);
            DrawTownBrick(pixels, sz, x0: 8, x1: 14, yStart: 0, body: brickMid, top: edgeHighlight, bot: edgeShadow);
            // Row 1 (y=4..6), offset layout.
            DrawTownBrick(pixels, sz, x0: 0, x1: 2, yStart: 4, body: brickMid, top: edgeHighlight, bot: edgeShadow);
            DrawTownBrick(pixels, sz, x0: 4, x1: 10, yStart: 4, body: brickDark, top: edgeHighlight, bot: edgeShadow);
            DrawTownBrick(pixels, sz, x0: 12, x1: 15, yStart: 4, body: brickLight, top: edgeHighlight, bot: edgeShadow);
            // Row 2 (y=8..10), normal layout.
            DrawTownBrick(pixels, sz, x0: 0, x1: 6, yStart: 8, body: brickDark, top: edgeHighlight, bot: edgeShadow);
            DrawTownBrick(pixels, sz, x0: 8, x1: 14, yStart: 8, body: brickLight, top: edgeHighlight, bot: edgeShadow);
            // Row 3 (y=12..14), offset layout.
            DrawTownBrick(pixels, sz, x0: 0, x1: 2, yStart: 12, body: brickLight, top: edgeHighlight, bot: edgeShadow);
            DrawTownBrick(pixels, sz, x0: 4, x1: 10, yStart: 12, body: brickMid, top: edgeHighlight, bot: edgeShadow);
            DrawTownBrick(pixels, sz, x0: 12, x1: 15, yStart: 12, body: brickDark, top: edgeHighlight, bot: edgeShadow);

            // Deterministic specks for "weathered" detail — same across every cell,
            // so a wall of these tiles reads as a continuous textured surface rather
            // than identical-looking instances.
            pixels[1 * sz + 5] = speck;
            pixels[5 * sz + 9] = speck;
            pixels[9 * sz + 3] = speck;
            pixels[13 * sz + 7] = speck;
        }

        private static void DrawTownBrick(Color[] pixels, int sz, int x0, int x1, int yStart, Color body, Color top, Color bot)
        {
            // 3-pixel-tall brick: bottom row shaded, middle plain, top row highlighted.
            for (int y = yStart; y <= yStart + 2; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Color c;
                    if (y == yStart + 2) c = top;
                    else if (y == yStart) c = bot;
                    else c = body;
                    pixels[y * sz + x] = c;
                }
            }
        }

        /// <summary>
        /// 16x16 dungeon floor: four round-ish cobblestones in a 2x2 arrangement with
        /// near-black mortar between them. Fake top-left highlight + bottom-right shade
        /// gives each cobble a sense of volume.
        /// </summary>
        private static void PaintDungeonCobbles(Color[] pixels, Color body)
        {
            // 16x16 cobblestone texture with strong contrast and per-cobble shade
            // variation: four round-ish stones in a 2x2 grid, dark mortar between,
            // top-left highlight + bottom-right shadow for volume, plus a few crack
            // pixels so a wall of these tiles reads as worn stone.
            int sz = PlaceholderSize;
            Color mortar = MulColor(body, 0.20f);
            Color edgeShade = MulColor(body, 0.55f);
            Color highlight = MulColor(body, 1.45f);
            Color crack = MulColor(body, 0.15f);

            // Each cobble gets a slightly different mid-tone for variety.
            Color[] cobbleBodies = new Color[]
            {
                MulColor(body, 1.00f),
                MulColor(body, 0.88f),
                MulColor(body, 1.06f),
                MulColor(body, 0.92f)
            };

            // Fill the whole cell with mortar first; cobbles overwrite.
            for (int i = 0; i < pixels.Length; i++) pixels[i] = mortar;

            (int x0, int y0)[] cobbles = new (int, int)[]
            {
                (1, 1), (8, 1), (1, 8), (8, 8)
            };
            const int Size = 7;
            for (int idx = 0; idx < cobbles.Length; idx++)
            {
                (int x0, int y0) = cobbles[idx];
                Color cobbleBody = cobbleBodies[idx];
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
                        bool topRow = dy == Size - 1 || dy == Size - 2;
                        bool leftCol = dx == 0 || dx == 1;
                        bool bottomRow = dy == 0 || dy == 1;
                        bool rightCol = dx == Size - 1 || dx == Size - 2;
                        Color c;
                        if (topRow || leftCol) c = highlight;
                        else if (bottomRow || rightCol) c = edgeShade;
                        else c = cobbleBody;
                        pixels[py * sz + px] = c;
                    }
                }
            }

            // A few cracks across cobbles to break up the regular pattern.
            pixels[4 * sz + 10] = crack;
            pixels[5 * sz + 11] = crack;
            pixels[11 * sz + 4] = crack;
            pixels[12 * sz + 5] = crack;
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

            // Inventory item icons. The bootstrap overrides the rendered sprite's
            // colour per-tier at draw time, so these fallbacks only matter when an
            // icon is rendered uncoloured (which shouldn't happen via the gear screen).
            TileVisualKind.WeaponIcon => new Color(0.78f, 0.80f, 0.86f),
            TileVisualKind.ArmorIcon => new Color(0.62f, 0.66f, 0.78f),
            TileVisualKind.AccessoryIcon => new Color(0.92f, 0.78f, 0.32f),

            _ => Color.magenta
        };

        // Floor PNGs (town_floor / dungeon_floor) come from Kenney's Roguelike
        // RPG Pack (CC0) — picked from the seamless-center positions of each
        // ground-tile group on the spritesheet so they tile cleanly. Walls
        // intentionally stay on the procedural path (PaintTownFlagstones-equivalent
        // wood-grain / dungeon brick) because every "wall" tile in the Roguelike
        // RPG Pack is a top-half-only sprite designed to pair with a wall-face
        // tile below — they don't work as standalone single-cell wall blocks.
        // Character sprites (hero/enemy/npc/guard) still come from the 1-Bit Pack
        // because the Roguelike RPG Pack ships environment art only.
        //
        // If a PNG is missing or fails to import as a Sprite, the catalog falls
        // back to the procedural placeholder so the sample remains playable with
        // zero asset setup.
        public static readonly IReadOnlyDictionary<TileVisualKind, string> SpriteNames =
            new Dictionary<TileVisualKind, string>
            {
                { TileVisualKind.DungeonFloor, "dungeon_floor" },
                { TileVisualKind.DungeonPillar, "dungeon_pillar" },
                { TileVisualKind.DungeonStairsDown, "stairs_down" },
                { TileVisualKind.DungeonStairsUp, "stairs_up" },

                { TileVisualKind.TownFloor, "town_floor" },
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
