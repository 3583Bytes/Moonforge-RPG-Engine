using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Moonforge.Sample.Roguelike.Rendering
{
    /// <summary>
    /// Source of sprites + tile assets keyed by <see cref="TileVisualKind"/>.
    /// Resolution order: named DungeonTilesetII frame → runtime-generated procedural
    /// placeholder. To customize the art, overwrite the PNGs in
    /// <c>Art/Resources/DungeonTilesetII/</c> or repoint the frame names in this class
    /// (<see cref="ResolveCharacterId"/>, <see cref="SpriteNames"/>, the floor/wall arrays).
    /// </summary>
    public sealed class UnitySpriteCatalog
    {
        private const int PlaceholderSize = 16;

        private readonly Dictionary<TileVisualKind, Sprite> _sprites = new Dictionary<TileVisualKind, Sprite>();
        private readonly Dictionary<TileVisualKind, Tile> _tiles = new Dictionary<TileVisualKind, Tile>();
        private bool _loaded;

        // 0x72 DungeonTileset II lives under Art/Resources/DungeonTilesetII/ as one PNG per
        // named frame, loaded by name. Two caches keep repeated lookups off Resources: single
        // frames (floors/walls/props) and per-character animation clips (idle/run frame sets).
        private const string TilesetPath = "DungeonTilesetII/";
        private readonly Dictionary<string, Sprite> _staticCache =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite[]> _clipCache =
            new Dictionary<string, Sprite[]>(StringComparer.Ordinal);

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
            if (_sprites.TryGetValue(kind, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            // Character kinds (hero / enemy tiers / npc / guard) resolve to the idle f0 of
            // their mapped tileset character — this is the still used by battle portraits and
            // any other non-animated path. On the map the actors are animated instead, via
            // GetActorClip, so this default (a fixed representative per kind) only surfaces for
            // portraits.
            Sprite actor = GetActorStatic(kind, string.Empty);
            if (actor != null)
            {
                _sprites[kind] = actor;
                return actor;
            }

            sprite = GeneratePlaceholderSprite(kind);
            _sprites[kind] = sprite;
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
        /// Still hero sprite for the given class id and facing — the idle frame 0 of the
        /// mapped tileset character (Knight → knight_m, Ranger → elf_m, Arcanist → wizzard_m).
        /// The character sheets face right, so <paramref name="flipX"/> is set for a Left
        /// facing and the caller mirrors the sprite. On the map the hero is animated via
        /// GetActorClip; this still is used for the battle portrait and the animator fallback.
        /// </summary>
        public Sprite GetHeroSprite(string classId, FacingDirection facing, out bool flipX)
        {
            EnsureLoaded();
            flipX = facing == FacingDirection.Left;

            Sprite tileset = GetActorStatic(TileVisualKind.Hero, classId);
            return tileset != null ? tileset : GetSprite(TileVisualKind.Hero);
        }

        // ---- 0x72 DungeonTileset II: characters, floors, walls ------------------------

        private static readonly string[] NormalEnemyChars = { "goblin", "skelet", "imp", "tiny_zombie", "masked_orc" };
        private static readonly string[] EliteEnemyChars = { "orc_warrior", "chort", "wogol", "orc_shaman" };
        private static readonly string[] BossEnemyChars = { "big_demon", "ogre", "big_zombie" };
        private static readonly string[] NpcChars = { "dwarf_m", "dwarf_f", "doc", "pumpkin_dude" };

        private static string HeroCharacter(string classId)
        {
            switch ((classId ?? string.Empty).ToLowerInvariant())
            {
                case "ranger": return "elf_m";
                case "arcanist": return "wizzard_m";
                case "knight":
                default: return "knight_m";
            }
        }

        // Deterministic, non-negative hash (no System.Random) so the same actor id always maps
        // to the same character. Used to spread enemies/NPCs across their character pools.
        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                if (value != null)
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash = (hash * 31) + value[i];
                    }
                }
                return hash & 0x7fffffff;
            }
        }

        private static string Pick(string[] pool, string variantKey) => pool[StableHash(variantKey) % pool.Length];

        /// <summary>
        /// Maps an actor visual kind (plus a per-actor variant key for variety) to a tileset
        /// character id, or null when the kind is not a character (floor/wall/marker/icon).
        /// </summary>
        public string ResolveCharacterId(TileVisualKind kind, string variantKey)
        {
            switch (kind)
            {
                case TileVisualKind.Hero: return HeroCharacter(variantKey);
                case TileVisualKind.Enemy: return Pick(NormalEnemyChars, variantKey);
                case TileVisualKind.EnemyElite: return Pick(EliteEnemyChars, variantKey);
                case TileVisualKind.EnemyBoss: return Pick(BossEnemyChars, variantKey);
                case TileVisualKind.Npc: return Pick(NpcChars, variantKey);
                case TileVisualKind.TownGuardMarker: return "knight_f";
                default: return null;
            }
        }

        public bool IsCharacterKind(TileVisualKind kind) => ResolveCharacterId(kind, string.Empty) != null;

        /// <summary>
        /// Idle (or run) animation frames for the character mapped from <paramref name="kind"/>
        /// and <paramref name="variantKey"/>; null if the kind is not a character or no frames
        /// were found. Cached per (character, clip).
        /// </summary>
        public Sprite[] GetActorClip(TileVisualKind kind, string variantKey, bool run)
        {
            string id = ResolveCharacterId(kind, variantKey);
            return id != null ? LoadClip(id, run) : null;
        }

        /// <summary>First idle frame of the mapped character — the still portrait frame.</summary>
        public Sprite GetActorStatic(TileVisualKind kind, string variantKey)
        {
            Sprite[] idle = GetActorClip(kind, variantKey, false);
            return (idle != null && idle.Length > 0) ? idle[0] : null;
        }

        private Sprite[] LoadClip(string characterId, bool run)
        {
            string key = characterId + (run ? "|run" : "|idle");
            if (_clipCache.TryGetValue(key, out Sprite[] cached))
            {
                return cached;
            }

            string suffix = run ? "_run_anim_f" : "_idle_anim_f";
            List<Sprite> frames = new List<Sprite>();
            for (int i = 0; i < 16; i++)
            {
                Sprite frame = Resources.Load<Sprite>(TilesetPath + characterId + suffix + i);
                if (frame == null) break;
                frames.Add(frame);
            }

            Sprite[] result = frames.Count > 0 ? frames.ToArray() : null;
            _clipCache[key] = result;
            return result;
        }

        // Floor variety: a few worn variants keyed on cell position so a floor reads as a
        // surface, not one repeated tile. Dungeon uses the full (partly cracked) set biased to
        // the clean floor_1; town stays on the tidy variants. Duplicates weight the odds.
        private static readonly string[] DungeonFloorVariants =
            { "floor_1", "floor_1", "floor_1", "floor_2", "floor_3", "floor_4", "floor_6", "floor_8" };
        private static readonly string[] TownFloorVariants = { "floor_1", "floor_2", "floor_3", "floor_4" };

        /// <summary>Position-seeded floor sprite; falls back to the procedural floor.</summary>
        public Sprite GetFloorSprite(bool isTown, int x, int y)
        {
            string[] pool = isTown ? TownFloorVariants : DungeonFloorVariants;
            int idx = StableHash(x + "," + y) % pool.Length;
            Sprite s = LoadStatic(pool[idx]);
            return s != null ? s : GetSprite(isTown ? TileVisualKind.TownFloor : TileVisualKind.DungeonFloor);
        }

        /// <summary>
        /// Picks a wall face from the 4-neighbour floor mask: a wall bordering a room to the
        /// south shows a brick front face (<c>wall_mid</c>), side walls use the vertical edge
        /// pieces, and bulk/back walls use the flat top (<c>wall_top_mid</c>). Falls back to the
        /// procedural wall if a frame is missing.
        /// </summary>
        public Sprite GetWallSprite(bool floorBelow, bool floorAbove, bool floorLeft, bool floorRight, bool isTown)
        {
            string name;
            if (floorBelow) name = "wall_mid";        // faces a room to the south -> front brick face
            else if (floorRight) name = "wall_left";  // room to the east -> this is its west wall
            else if (floorLeft) name = "wall_right";  // room to the west -> this is its east wall
            else name = "wall_top_mid";               // bulk / back wall -> flat top

            Sprite s = LoadStatic(name);
            return s != null ? s : GetSprite(isTown ? TileVisualKind.TownWall : TileVisualKind.DungeonWall);
        }

        // Weapon icons scale with gear tier (Common → Uncommon → Rare → Epic). The 0x72 set has
        // no armor or ring art, so only the weapon slot uses a tileset icon; armor/accessory
        // keep the procedural silhouette. These sprites are full-colour — render them white.
        private static readonly string[] WeaponTierSprites =
            { "weapon_rusty_sword", "weapon_regular_sword", "weapon_knight_sword", "weapon_golden_sword" };

        /// <summary>
        /// A weapon icon that gets fancier as <paramref name="tierIndex"/> rises (0 = lowest).
        /// Falls back to the procedural weapon silhouette if the frame is missing.
        /// </summary>
        public Sprite GetWeaponIcon(int tierIndex)
        {
            if (WeaponTierSprites.Length == 0)
            {
                return GetSprite(TileVisualKind.WeaponIcon);
            }
            int i = tierIndex < 0 ? 0
                : (tierIndex >= WeaponTierSprites.Length ? WeaponTierSprites.Length - 1 : tierIndex);
            Sprite s = LoadStatic(WeaponTierSprites[i]);
            return s != null ? s : GetSprite(TileVisualKind.WeaponIcon);
        }

        private Sprite LoadStatic(string frameName)
        {
            if (_staticCache.TryGetValue(frameName, out Sprite cached))
            {
                return cached;
            }
            Sprite s = Resources.Load<Sprite>(TilesetPath + frameName);
            _staticCache[frameName] = s;
            return s;
        }

        // Town ground textures — large seamless tiles (Grass.tga / Ground.tga) at the Resources
        // root, painted as tiled surface planes rather than per-cell (see RoguelikeBootstrap).
        // Null if the texture isn't present, so the town falls back to per-cell 0x72 floors.
        public Sprite GetTownGroundSprite() => LoadResourceSprite("Grass");

        public Sprite GetTownRoadSprite() => LoadResourceSprite("Ground");

        /// <summary>
        /// A one-cell slice of the Ground texture for cell (<paramref name="gridX"/>,
        /// <paramref name="gridY"/>). Neighbouring cells sample neighbouring slices, so a run of
        /// road cells tiles continuously (wrapping every <c>texWidth / PPU</c> cells) instead of
        /// each cell showing the whole texture. Used to paint the road only on walkable cells so
        /// it conforms to the courtyard and never renders under a building. Cached per slice.
        /// </summary>
        public Sprite GetTownRoadCellSprite(int gridX, int gridY)
        {
            Sprite full = GetTownRoadSprite();
            Texture2D tex = full != null ? full.texture : null;
            if (tex == null)
            {
                return null;
            }

            int cellsPerCopy = Mathf.Max(1, Mathf.RoundToInt(tex.width / full.pixelsPerUnit));
            int cellPx = Mathf.Max(1, tex.width / cellsPerCopy);
            int col = ((gridX % cellsPerCopy) + cellsPerCopy) % cellsPerCopy;
            int row = ((gridY % cellsPerCopy) + cellsPerCopy) % cellsPerCopy;

            string key = "roadcell:" + col + ":" + row + ":" + cellPx;
            if (_staticCache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Rect rect = new Rect(col * cellPx, row * cellPx, cellPx, cellPx);
            Sprite s = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), cellPx);
            _staticCache[key] = s;
            return s;
        }

        private Sprite LoadResourceSprite(string resourcePath)
        {
            string key = "root:" + resourcePath;
            if (_staticCache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }
            Sprite s = Resources.Load<Sprite>(resourcePath);
            _staticCache[key] = s;
            return s;
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
                // Skip if already cached (e.g. from an earlier GetSprite call).
                if (_sprites.TryGetValue(entry.Key, out Sprite existing) && existing != null)
                {
                    continue;
                }

                Sprite sprite = Resources.Load<Sprite>(TilesetPath + entry.Value);
                if (sprite != null)
                {
                    _sprites[entry.Key] = sprite;
                }
                else
                {
                    // Loud signal that a real sprite is missing — placeholders will be
                    // generated for these, which is why the world can look uniformly grey.
                    Debug.LogWarning(
                        "[Roguelike] No sprite found at Resources/" + TilesetPath + entry.Value +
                        " for " + entry.Key +
                        ". Falling back to procedural placeholder. " +
                        "Check that the PNG exists under Art/Resources/DungeonTilesetII/ and is imported as Sprite (2D and UI).");
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

        // Static single-frame kinds map to named 0x72 DungeonTileset II frames under
        // Art/Resources/DungeonTilesetII/ (CC0). Loaded once in LoadFromResources.
        //
        // NOT listed here, by design:
        // - Floors and walls are resolved per-cell in the bootstrap (GetFloorSprite /
        //   GetWallSprite) so they can vary by position and neighbourhood.
        // - Character kinds (Hero / Enemy / EnemyElite / EnemyBoss / Npc / TownGuardMarker)
        //   are resolved via the animated GetActorClip path, with GetSprite falling back to
        //   their idle f0 for portraits.
        //
        // If a PNG is missing or fails to import as a Sprite, the catalog falls back to the
        // procedural placeholder so the sample stays playable with zero asset setup.
        public static readonly IReadOnlyDictionary<TileVisualKind, string> SpriteNames =
            new Dictionary<TileVisualKind, string>
            {
                { TileVisualKind.DungeonPillar, "column" },
                { TileVisualKind.DungeonStairsDown, "floor_stairs" },
                { TileVisualKind.DungeonStairsUp, "floor_ladder" },

                { TileVisualKind.TownDoor, "doors_leaf_closed" },
                { TileVisualKind.TownShopMarker, "crate" },
                { TileVisualKind.TownHealerMarker, "flask_big_red" },
                { TileVisualKind.TownAlchemistMarker, "flask_big_green" },
                { TileVisualKind.TownCacheMarker, "chest_full_open_anim_f0" },
                { TileVisualKind.TownFountainMarker, "wall_fountain_top_1" },
                { TileVisualKind.TownQuestBoardMarker, "wall_banner_blue" },
                { TileVisualKind.TownShrineMarker, "wall_banner_yellow" },
            };
    }
}
