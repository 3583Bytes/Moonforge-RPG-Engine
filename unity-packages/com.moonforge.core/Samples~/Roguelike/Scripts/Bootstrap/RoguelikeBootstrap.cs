using System.Collections.Generic;
using System.Text;
using Moonforge.Core.Exploration;
using Moonforge.Sample.Roguelike.Input;
using Moonforge.Sample.Roguelike.Rendering;
using Moonforge.Sample.Roguelike.Session;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

using System;
namespace Moonforge.Sample.Roguelike
{
    /// <summary>
    /// Unity host for the roguelike sample. Implements <see cref="IRoguelikeHost"/> by
    /// painting Town/Dungeon scenes onto a runtime-built Tilemap (with a SpriteRenderer
    /// for the hero and one per marker), and showing everything else as TMP HUD text.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoguelikeBootstrap : MonoBehaviour, IRoguelikeHost
    {
        [Header("Camera")]
        [Tooltip("Vertical view size in world units when looking at the tilemap.")]
        [SerializeField] private float _orthographicSize = 10f;
        [SerializeField] private Color _backgroundColor = new Color(0.04f, 0.04f, 0.06f, 1f);
        [Tooltip("Camera background while in the town scene — dark grass green frames the flagstones like a lawn around a courtyard.")]
        [SerializeField] private Color _townBackgroundColor = new Color(0.12f, 0.22f, 0.08f, 1f);
        [Tooltip("Camera background while in the dungeon scene — dark cool navy reinforces the cool tile palette.")]
        [SerializeField] private Color _dungeonBackgroundColor = new Color(0.05f, 0.06f, 0.12f, 1f);

        [Header("Tilemap")]
        [SerializeField] private float _cellSize = 1f;

        [Header("Debug")]
        [Tooltip("When on, paints colored quads over every wall (red), marker (orange), and the hero's cell (green). Useful for diagnosing layout / coordinate bugs; turn off for normal play.")]
        [SerializeField] private bool _showDebugOverlay = false;

        [Header("Sprite Slots")]
        [Tooltip("Optional per-kind sprite overrides. Any slot left null falls back to the bundled PNG at Resources/Sprites/<name>.png, then to a procedural placeholder. Drag-drop your own art here to override without touching code.")]
        [SerializeField] private SpriteSlots _spriteSlots = new SpriteSlots();

        private readonly PlayerInputAdapter _input = new PlayerInputAdapter();
        private readonly UnitySpriteCatalog _sprites = new UnitySpriteCatalog();
        private readonly List<GameObject> _markerSprites = new List<GameObject>();
        private readonly List<GameObject> _menuButtons = new List<GameObject>();
        private RoguelikeSession _session;
        private Camera _camera;
        private Grid _grid;
        private Tilemap _tilemap;
        private Canvas _canvas;
        private TMP_Text _headerText;
        private TMP_Text _bodyText;
        private TMP_Text _footerText;
        private TMP_Text _messageText;
        private GameObject _menuPanel;
        private TMP_Text _menuTitle;
        private TMP_Text _menuBodyText;
        private GameObject _menuSeparator;
        private RectTransform _menuButtonContainer;

        // ---- Battle UI -----------------------------------------------------------------
        private GameObject _battlePanel;
        private RectTransform _battleEnemyRow;
        private TMP_Text _battleLogText;
        private TMP_Text _battleTitleText;
        private TMP_Text _battleTurnText;
        private BattleActorCardHandle _battleHeroCard;
        private GameObject _battleHeroCardGo;

        private sealed class BattleActorCardHandle
        {
            public string ActorId;
            public GameObject GameObject;
            public Image Portrait;
            public Color PortraitBaseColor = Color.white;
            public Image HpBarFill;
            public Image FocusBarFill;
            public TMP_Text HpText;
            public TMP_Text FocusText;
            public TMP_Text NameText;
            public Image TurnIndicator;
            public int LastHp;
            public float HitFlashRemaining;
        }

        private sealed class DamageNumberInstance
        {
            public GameObject GameObject;
            public RectTransform Rect;
            public TMP_Text Text;
            public Vector2 StartPos;
            public float Elapsed;
        }

        private readonly Dictionary<string, BattleActorCardHandle> _battleCards = new Dictionary<string, BattleActorCardHandle>(System.StringComparer.Ordinal);
        private readonly List<DamageNumberInstance> _damageNumbers = new List<DamageNumberInstance>();

        private const float HitFlashDuration = 0.22f;
        private const float DamageNumberLifetime = 0.95f;
        private const float DamageNumberFloatDistance = 80f;

        // When the killing blow lands, the session renders one final battle frame with
        // HP=0 and then transitions to BattleSummary. We hold the battle panel open for
        // a moment so the death animation (damage number + hit flash + dimmed portrait)
        // actually plays before the summary menu appears.
        private const float BattleOutroHoldSeconds = 1.4f;
        private float _battleOutroHoldRemaining;
        private BattleSummaryRenderModel _pendingBattleSummary;
        private GameObject _dpadPanel;
        private GameObject _actionBar;
        private RectTransform _actionBarContainer;
        private readonly List<GameObject> _actionBarButtons = new List<GameObject>();

#if UNITY_EDITOR
        // Fired by Unity when the component is first added to a GameObject (or
        // when the user picks "Reset" from the component's gear-icon menu).
        // Pre-fills the Sprite Slots with whichever bundled defaults can be
        // found in any Resources/Sprites/<name>.png — same lookup path the
        // catalog uses at runtime, so the Inspector mirrors what the game
        // would render anyway. Slots without a bundled PNG (walls + inventory
        // icons) stay null and fall through to procedural placeholders.
        private void Reset()
        {
            PopulateSpriteSlotsWithDefaults();
        }

        // Context-menu shortcut so existing components in a scene can pull in
        // the bundled defaults without doing a full Reset (which would also
        // clobber non-sprite fields). Right-click the Roguelike Bootstrap
        // component header → "Populate Sprite Slots with Defaults".
        [ContextMenu("Populate Sprite Slots with Defaults")]
        private void PopulateSpriteSlotsWithDefaults()
        {
            _spriteSlots ??= new SpriteSlots();
            _spriteSlots.DungeonFloor = Resources.Load<Sprite>("Sprites/dungeon_floor");
            _spriteSlots.DungeonPillar = Resources.Load<Sprite>("Sprites/dungeon_pillar");
            _spriteSlots.DungeonStairsDown = Resources.Load<Sprite>("Sprites/stairs_down");
            _spriteSlots.DungeonStairsUp = Resources.Load<Sprite>("Sprites/stairs_up");
            _spriteSlots.TownFloor = Resources.Load<Sprite>("Sprites/town_floor");
            _spriteSlots.TownDoor = Resources.Load<Sprite>("Sprites/town_door");
            _spriteSlots.ShopMarker = Resources.Load<Sprite>("Sprites/marker_shop");
            _spriteSlots.HealerMarker = Resources.Load<Sprite>("Sprites/marker_healer");
            _spriteSlots.AlchemistMarker = Resources.Load<Sprite>("Sprites/marker_alchemist");
            _spriteSlots.GuardMarker = Resources.Load<Sprite>("Sprites/marker_guard");
            _spriteSlots.CacheMarker = Resources.Load<Sprite>("Sprites/marker_cache");
            _spriteSlots.FountainMarker = Resources.Load<Sprite>("Sprites/marker_fountain");
            _spriteSlots.QuestBoardMarker = Resources.Load<Sprite>("Sprites/marker_questboard");
            _spriteSlots.ShrineMarker = Resources.Load<Sprite>("Sprites/marker_shrine");
            _spriteSlots.Hero = Resources.Load<Sprite>("Sprites/hero");
            _spriteSlots.Enemy = Resources.Load<Sprite>("Sprites/enemy");
            _spriteSlots.EnemyElite = Resources.Load<Sprite>("Sprites/enemy_elite");
            _spriteSlots.EnemyBoss = Resources.Load<Sprite>("Sprites/enemy_boss");
            _spriteSlots.Npc = Resources.Load<Sprite>("Sprites/npc");
            // TownWall, DungeonWall, WeaponIcon, ArmorIcon, AccessoryIcon
            // are procedural-only — no bundled PNGs to load. Leaving them null
            // means the catalog draws them at runtime.
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void Awake()
        {
            EnsureEventSystem();
            BuildCamera();
            BuildTilemap();
            BuildHud();
            BuildMenuPanel();
            BuildBattlePanel();
            BuildGearPanel();
            BuildDpad();
            BuildActionBar();
            // Push Inspector-assigned sprites into the catalog FIRST so the
            // Resources.Load pass that EnsureLoaded triggers can skip kinds
            // the user has already overridden in the Inspector.
            _sprites.ApplyOverrides(_spriteSlots);
            _sprites.EnsureLoaded();
            _session = new RoguelikeSession(this);
            _session.Enter();
        }

        private static void EnsureEventSystem()
        {
            // The Canvas buttons need an EventSystem in the scene to receive pointer
            // events. We build the scene at runtime, so create one if the user's empty
            // scene doesn't already have it.
            if (EventSystem.current != null)
            {
                return;
            }
            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private void Update()
        {
            if (_session.CurrentScene == SceneId.Exit)
            {
                QuitGame();
                return;
            }

            // Battle AI turns auto-advance — no input required.
            if (_session.IsBattleAiTurn)
            {
                _session.Tick(PlayerAction.None);
            }
            else
            {
                PlayerAction action = _input.PollScene(_session.CurrentScene);

                // Hold-to-continue: if the per-frame poll returned nothing AND
                // the hero's tween has caught up to its target AND we're in a
                // free-walk scene, also try the held-key poll, then fall back
                // to the held D-pad button if no key is down. Tap = one cell
                // (handled by the GetKeyDown / D-pad PointerDown paths), hold =
                // continuous glide at ActorMoveSpeed cells/sec.
                if (action == PlayerAction.None
                    && IsHeroTweenIdle()
                    && (_session.CurrentScene == SceneId.Town || _session.CurrentScene == SceneId.Dungeon))
                {
                    action = _input.PollHeldMovement();
                    if (action == PlayerAction.None && _heldDpadAction != PlayerAction.None)
                    {
                        action = _heldDpadAction;
                    }
                }

                if (action != PlayerAction.None)
                {
                    _session.Tick(action);
                }
            }

            // Smooth movement and marker bob run every frame, regardless of whether the
            // session ticked. This decouples animation speed from input frequency.
            SmoothMovementAndCamera();
            BobMarkers();
            UpdateBattleAnimations();

            // Auto-hide the battle panel whenever the session is in a non-battle scene,
            // UNLESS we're in the outro hold playing the kill animation.
            if (_battlePanel != null && _battlePanel.activeSelf
                && _session.CurrentScene != SceneId.Battle
                && _battleOutroHoldRemaining <= 0f)
            {
                HideBattlePanel();
            }
        }

        // ---- IRoguelikeHost ------------------------------------------------------------

        public void RenderMainMenu(string subtitle, bool canContinue)
        {
            BeginMenu("Moonforge — Roguelike");
            SetMenuBody(subtitle);
            AddMenuButton("[N]  New run", PlayerAction.NewRun);
            AddMenuButton("[C]  Continue saved run", PlayerAction.ContinueRun, enabled: canContinue);
            AddMenuButton("[D]  Delete saved run", PlayerAction.DeleteSave, enabled: canContinue);
            AddMenuButton("[Q]  Quit", PlayerAction.Quit);
            EndMenu(controlsHint: null);
        }

        public void RenderClassSelection(IReadOnlyList<ClassSelectionOption> options, string controls)
        {
            BeginMenu("Choose your class");
            SetMenuBody("Each class has a unique basic attack, signature abilities, and starting stats. Pick one to begin a new run.");
            for (int i = 0; i < options.Count && i < 3; i++)
            {
                ClassSelectionOption opt = options[i];
                PlayerAction action = i switch
                {
                    0 => PlayerAction.Digit1,
                    1 => PlayerAction.Digit2,
                    2 => PlayerAction.Digit3,
                    _ => PlayerAction.None
                };
                AddMenuButton("[" + opt.Hotkey + "]  " + opt.Name, action);
                AddMenuInfoRow(opt.Summary);
            }
            AddMenuButton("[Esc]  Back to menu", PlayerAction.Cancel);
            EndMenu(controls);
        }

        public void RenderMap(MapRenderModel model)
        {
            HideMenu();
            HideGearPanel();
            ShowMap();
            ShowMovementControls();
            PopulateMapActionBar(model);
            PaintMapTiles(model);
            PaintActors(model.Actors);
            PaintMarkers(model.Markers);
            PaintDebugOverlay(model);

            _headerText.text = Strip(model.Title);

            StringBuilder body = new StringBuilder();
            body.Append("Gold ").Append(model.Gold).AppendLine();
            body.Append("Tokens ").Append(model.Tokens).AppendLine();
            body.Append("Potions ").Append(model.Potions).AppendLine();
            body.Append("Floor ").Append(model.Depth).AppendLine();
            body.AppendLine();
            if (!string.IsNullOrWhiteSpace(model.ContractInfo))
            {
                body.AppendLine(Strip(model.ContractInfo));
            }
            AppendDebugText(body, model);
            _bodyText.text = body.ToString();
            _footerText.text = Strip(model.Controls);
            _messageText.text = Strip(model.LastMessage);
        }

        // ---- Debug overlay -------------------------------------------------------------
        // Always-on text + sprite overlay that surfaces the engine's view of the world so
        // visual / logical mismatches are easy to spot. Each entry shows the GridPosition
        // the engine has stored, not the world-space position the renderer puts the sprite
        // at.

        private readonly List<GameObject> _debugOverlaySprites = new List<GameObject>();

        private static string FormatFlags(ExplorationTileFlags flags)
        {
            if (flags == ExplorationTileFlags.None)
            {
                return "Empty";
            }
            StringBuilder sb = new StringBuilder();
            if ((flags & ExplorationTileFlags.Walkable) != 0) sb.Append("Walkable+");
            else sb.Append("WALL+");
            if ((flags & ExplorationTileFlags.Interactable) != 0) sb.Append("Interact+");
            if ((flags & ExplorationTileFlags.BlocksLineOfSight) != 0) sb.Append("LoS+");
            if ((flags & ExplorationTileFlags.EncounterAllowed) != 0) sb.Append("Enc+");
            if (sb.Length > 0 && sb[sb.Length - 1] == '+') sb.Length--;
            return sb.ToString();
        }

        private static string DescribeTile(ExplorationMapState map, int x, int y)
        {
            if (!map.TryGetTileFlags(new GridPosition(x, y), out ExplorationTileFlags flags))
            {
                return "OOB";
            }
            return FormatFlags(flags);
        }

        private void AppendDebugText(StringBuilder body, MapRenderModel model)
        {
            if (!_showDebugOverlay) return;
            body.AppendLine();
            body.AppendLine("<size=80%><color=#7ad9ff>DEBUG</color></size>");
            if (!model.HeroPosition.HasValue || model.Map is null)
            {
                body.AppendLine("<size=70%>(no hero / no map)</size>");
                return;
            }
            int hx = model.HeroPosition.Value.X;
            int hy = model.HeroPosition.Value.Y;
            body.Append("<size=75%>Hero grid: (").Append(hx).Append(", ").Append(hy).AppendLine(")</size>");
            body.Append("<size=70%>Here: ").Append(DescribeTile(model.Map, hx, hy)).AppendLine("</size>");
            body.Append("<size=70%>North (W): ").Append(DescribeTile(model.Map, hx, hy - 1)).AppendLine("</size>");
            body.Append("<size=70%>South (S): ").Append(DescribeTile(model.Map, hx, hy + 1)).AppendLine("</size>");
            body.Append("<size=70%>East  (D): ").Append(DescribeTile(model.Map, hx + 1, hy)).AppendLine("</size>");
            body.Append("<size=70%>West  (A): ").Append(DescribeTile(model.Map, hx - 1, hy)).AppendLine("</size>");
            body.AppendLine();
            body.AppendLine("<size=70%><color=#ffd87a>Markers (gridX, gridY)</color></size>");
            if (model.Markers != null)
            {
                foreach (MapMarker marker in model.Markers)
                {
                    bool heroOn = marker.Position.X == hx && marker.Position.Y == hy;
                    string prefix = heroOn ? "<color=#7aff7a>* " : "<color=#aaaaaa>  ";
                    body.Append("<size=65%>").Append(prefix).Append(marker.Symbol).Append(" ")
                        .Append(marker.Label).Append(" (").Append(marker.Position.X).Append(", ")
                        .Append(marker.Position.Y).Append(") ")
                        .Append(DescribeTile(model.Map, marker.Position.X, marker.Position.Y))
                        .AppendLine("</color></size>");
                }
            }
        }

        private void PaintDebugOverlay(MapRenderModel model)
        {
            // Tear down the previous frame's debug sprites and rebuild — markers can move
            // (dungeon regen) and tiles can change.
            foreach (GameObject go in _debugOverlaySprites)
            {
                if (go != null) Destroy(go);
            }
            _debugOverlaySprites.Clear();
            if (!_showDebugOverlay) return;

            // Red quad on every non-walkable cell the engine knows about. This is the
            // ground truth for blocking — if a tile lights up red but you don't see a
            // wall sprite there, the engine is correctly blocking you and the rendering
            // is failing (likely a bad PNG crop). If a tile blocks you but ISN'T red,
            // there's a coordinate bug in either rendering or movement.
            ExplorationMapState map = model.Map;
            if (map != null && map.IsConfigured)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        if (!map.TryGetTileFlags(new GridPosition(x, y), out ExplorationTileFlags flags)) continue;
                        if ((flags & ExplorationTileFlags.Walkable) != 0) continue;
                        _debugOverlaySprites.Add(CreateDebugQuad(
                            new GridPosition(x, y), new Color(1f, 0f, 0f, 0.45f), sortingOrder: 3, label: "DebugWall"));
                    }
                }
            }

            if (model.HeroPosition.HasValue)
            {
                // Tint the cell the engine thinks the hero is on — should sit dead-centre
                // on the hero sprite if rendering and logic agree.
                _debugOverlaySprites.Add(CreateDebugQuad(
                    model.HeroPosition.Value, new Color(0f, 1f, 0f, 0.35f), sortingOrder: 6, label: "DebugHeroCell"));
            }
            if (model.Markers != null)
            {
                foreach (MapMarker marker in model.Markers)
                {
                    _debugOverlaySprites.Add(CreateDebugQuad(
                        marker.Position, new Color(1f, 0.6f, 0.1f, 0.45f), sortingOrder: 4, label: "DebugMarker " + marker.Label));
                }
            }
        }

        private GameObject CreateDebugQuad(GridPosition pos, Color tint, int sortingOrder, string label)
        {
            GameObject go = new GameObject(label);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = GridToWorld(pos.X, pos.Y) + new Vector3(0f, 0f, -0.02f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetDebugQuadSprite();
            sr.color = tint;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        private static Sprite _debugQuadSprite;
        private static Sprite GetDebugQuadSprite()
        {
            if (_debugQuadSprite != null) return _debugQuadSprite;
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _debugQuadSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit: 1f);
            return _debugQuadSprite;
        }

        public void RenderBattle(BattleRenderModel model)
        {
            HideMenu();
            HideMap();
            HideMovementControls();
            PopulateBattleActionBar();
            ShowBattlePanel();

            _battleTitleText.text = Strip(model.Title);
            _battleTurnText.text = string.IsNullOrEmpty(model.CurrentTurnActorId)
                ? string.Empty
                : "Turn: <b>" + Strip(model.CurrentTurnActorId) + "</b>";

            // Build combat log (last 5 entries, newest at bottom).
            if (model.RecentLog != null && model.RecentLog.Count > 0)
            {
                StringBuilder logSb = new StringBuilder();
                int start = System.Math.Max(0, model.RecentLog.Count - 5);
                for (int i = start; i < model.RecentLog.Count; i++)
                {
                    float age = (model.RecentLog.Count - 1 - i);
                    int alpha = System.Math.Max(120, 255 - (int)(age * 35));
                    logSb.Append("<alpha=#").Append(alpha.ToString("X2")).Append(">");
                    logSb.AppendLine(Strip(model.RecentLog[i].Text));
                }
                _battleLogText.text = logSb.ToString();
            }
            else
            {
                _battleLogText.text = string.Empty;
            }

            // Hero card and enemy cards.
            if (model.Battle != null)
            {
                SyncBattleActorCards(model.Battle, model.CurrentTurnActorId);
            }

            // Keep these HUD strips populated since the battle panel is overlayed but the
            // map/menu HUD text is hidden — message + footer are still useful.
            _headerText.text = string.Empty;
            _bodyText.text = string.Empty;
            _footerText.text = Strip(model.Controls);
            _messageText.text = Strip(model.LastMessage);
        }

        // ---- Battle panel construction --------------------------------------------------

        private void BuildBattlePanel()
        {
            _battlePanel = new GameObject("Battle Panel");
            _battlePanel.transform.SetParent(_canvas.transform, worldPositionStays: false);
            RectTransform panelRect = _battlePanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image bg = _battlePanel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.96f);

            // === Title strip at top ===
            GameObject titleStrip = new GameObject("Battle Title Strip");
            titleStrip.transform.SetParent(_battlePanel.transform, worldPositionStays: false);
            RectTransform titleRect = titleStrip.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 64f);
            titleRect.anchoredPosition = Vector2.zero;
            Image titleBg = titleStrip.AddComponent<Image>();
            titleBg.color = new Color(0.55f, 0.18f, 0.20f, 0.92f);

            GameObject titleTextGo = new GameObject("Title");
            titleTextGo.transform.SetParent(titleStrip.transform, worldPositionStays: false);
            RectTransform titleTextRect = titleTextGo.AddComponent<RectTransform>();
            titleTextRect.anchorMin = Vector2.zero;
            titleTextRect.anchorMax = Vector2.one;
            titleTextRect.offsetMin = new Vector2(28f, 0f);
            titleTextRect.offsetMax = new Vector2(-28f, 0f);
            _battleTitleText = titleTextGo.AddComponent<TextMeshProUGUI>();
            _battleTitleText.alignment = TextAlignmentOptions.MidlineLeft;
            _battleTitleText.fontSize = 30f;
            _battleTitleText.fontStyle = FontStyles.Bold;
            _battleTitleText.color = Color.white;

            GameObject turnTextGo = new GameObject("Turn");
            turnTextGo.transform.SetParent(titleStrip.transform, worldPositionStays: false);
            RectTransform turnRect = turnTextGo.AddComponent<RectTransform>();
            turnRect.anchorMin = Vector2.zero;
            turnRect.anchorMax = Vector2.one;
            turnRect.offsetMin = new Vector2(28f, 0f);
            turnRect.offsetMax = new Vector2(-28f, 0f);
            _battleTurnText = turnTextGo.AddComponent<TextMeshProUGUI>();
            _battleTurnText.alignment = TextAlignmentOptions.MidlineRight;
            _battleTurnText.fontSize = 22f;
            _battleTurnText.color = new Color(1f, 0.95f, 0.8f);
            _battleTurnText.richText = true;

            // === Enemy row (upper half) ===
            GameObject enemyRowGo = new GameObject("Enemy Row");
            enemyRowGo.transform.SetParent(_battlePanel.transform, worldPositionStays: false);
            _battleEnemyRow = enemyRowGo.AddComponent<RectTransform>();
            _battleEnemyRow.anchorMin = new Vector2(0f, 1f);
            _battleEnemyRow.anchorMax = new Vector2(1f, 1f);
            _battleEnemyRow.pivot = new Vector2(0.5f, 1f);
            _battleEnemyRow.sizeDelta = new Vector2(0f, 320f);
            _battleEnemyRow.anchoredPosition = new Vector2(0f, -80f);
            HorizontalLayoutGroup hlg = enemyRowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 28f;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.padding = new RectOffset(48, 48, 16, 16);

            // === Combat log (middle band) ===
            GameObject logGo = new GameObject("Combat Log");
            logGo.transform.SetParent(_battlePanel.transform, worldPositionStays: false);
            RectTransform logRect = logGo.AddComponent<RectTransform>();
            logRect.anchorMin = new Vector2(0f, 0.5f);
            logRect.anchorMax = new Vector2(1f, 0.5f);
            logRect.pivot = new Vector2(0.5f, 0.5f);
            logRect.sizeDelta = new Vector2(0f, 140f);
            logRect.anchoredPosition = new Vector2(0f, -40f);
            Image logBg = logGo.AddComponent<Image>();
            logBg.color = new Color(0f, 0f, 0f, 0.35f);

            GameObject logTextGo = new GameObject("Log Text");
            logTextGo.transform.SetParent(logGo.transform, worldPositionStays: false);
            RectTransform logTextRect = logTextGo.AddComponent<RectTransform>();
            logTextRect.anchorMin = Vector2.zero;
            logTextRect.anchorMax = Vector2.one;
            logTextRect.offsetMin = new Vector2(28f, 8f);
            logTextRect.offsetMax = new Vector2(-28f, -8f);
            _battleLogText = logTextGo.AddComponent<TextMeshProUGUI>();
            _battleLogText.alignment = TextAlignmentOptions.BottomLeft;
            _battleLogText.fontSize = 18f;
            _battleLogText.color = new Color(0.85f, 0.88f, 0.94f);
            _battleLogText.richText = true;
            _battleLogText.enableWordWrapping = true;

            // === Hero card (bottom, positioned to right of action bar) ===
            _battleHeroCardGo = new GameObject("Hero Card");
            _battleHeroCardGo.transform.SetParent(_battlePanel.transform, worldPositionStays: false);
            RectTransform heroRect = _battleHeroCardGo.AddComponent<RectTransform>();
            heroRect.anchorMin = new Vector2(0.5f, 0f);
            heroRect.anchorMax = new Vector2(0.5f, 0f);
            heroRect.pivot = new Vector2(0.5f, 0f);
            heroRect.sizeDelta = new Vector2(320f, 240f);
            heroRect.anchoredPosition = new Vector2(120f, 28f);
            _battleHeroCard = BuildBattleActorCard(_battleHeroCardGo, isHero: true);

            _battlePanel.SetActive(false);
        }

        private BattleActorCardHandle BuildBattleActorCard(GameObject cardGo, bool isHero)
        {
            BattleActorCardHandle handle = new BattleActorCardHandle
            {
                GameObject = cardGo
            };

            Image cardBg = cardGo.AddComponent<Image>();
            cardBg.color = isHero
                ? new Color(0.10f, 0.16f, 0.26f, 0.92f)
                : new Color(0.20f, 0.08f, 0.10f, 0.92f);

            // Turn indicator (yellow ring) — toggled active when actor is current turn.
            GameObject ringGo = new GameObject("Turn Ring");
            ringGo.transform.SetParent(cardGo.transform, worldPositionStays: false);
            RectTransform ringRect = ringGo.AddComponent<RectTransform>();
            ringRect.anchorMin = Vector2.zero;
            ringRect.anchorMax = Vector2.one;
            ringRect.offsetMin = new Vector2(-4f, -4f);
            ringRect.offsetMax = new Vector2(4f, 4f);
            handle.TurnIndicator = ringGo.AddComponent<Image>();
            handle.TurnIndicator.color = new Color(1f, 0.85f, 0.25f, 0.85f);
            ringGo.SetActive(false);

            // Name (top of card)
            GameObject nameGo = new GameObject("Name");
            nameGo.transform.SetParent(cardGo.transform, worldPositionStays: false);
            RectTransform nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.sizeDelta = new Vector2(0f, 32f);
            nameRect.anchoredPosition = new Vector2(0f, -4f);
            handle.NameText = nameGo.AddComponent<TextMeshProUGUI>();
            handle.NameText.alignment = TextAlignmentOptions.Center;
            handle.NameText.fontSize = isHero ? 22f : 18f;
            handle.NameText.fontStyle = FontStyles.Bold;
            handle.NameText.color = Color.white;

            // Portrait area
            GameObject portraitGo = new GameObject("Portrait");
            portraitGo.transform.SetParent(cardGo.transform, worldPositionStays: false);
            RectTransform portraitRect = portraitGo.AddComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.5f, 1f);
            portraitRect.anchorMax = new Vector2(0.5f, 1f);
            portraitRect.pivot = new Vector2(0.5f, 1f);
            float portraitSize = isHero ? 128f : 96f;
            portraitRect.sizeDelta = new Vector2(portraitSize, portraitSize);
            portraitRect.anchoredPosition = new Vector2(0f, -40f);
            handle.Portrait = portraitGo.AddComponent<Image>();
            handle.Portrait.preserveAspect = true;
            handle.PortraitBaseColor = Color.white;

            // HP bar background
            GameObject hpBgGo = new GameObject("HP Bar Bg");
            hpBgGo.transform.SetParent(cardGo.transform, worldPositionStays: false);
            RectTransform hpBgRect = hpBgGo.AddComponent<RectTransform>();
            hpBgRect.anchorMin = new Vector2(0f, 0f);
            hpBgRect.anchorMax = new Vector2(1f, 0f);
            hpBgRect.pivot = new Vector2(0.5f, 0f);
            hpBgRect.sizeDelta = new Vector2(-24f, 18f);
            hpBgRect.anchoredPosition = new Vector2(0f, isHero ? 44f : 36f);
            Image hpBgImg = hpBgGo.AddComponent<Image>();
            hpBgImg.color = new Color(0.15f, 0.05f, 0.05f, 0.9f);

            GameObject hpFillGo = new GameObject("HP Fill");
            hpFillGo.transform.SetParent(hpBgGo.transform, worldPositionStays: false);
            RectTransform hpFillRect = hpFillGo.AddComponent<RectTransform>();
            hpFillRect.anchorMin = Vector2.zero;
            hpFillRect.anchorMax = Vector2.one;
            hpFillRect.offsetMin = new Vector2(2f, 2f);
            hpFillRect.offsetMax = new Vector2(-2f, -2f);
            handle.HpBarFill = hpFillGo.AddComponent<Image>();
            handle.HpBarFill.color = new Color(0.85f, 0.30f, 0.25f);
            handle.HpBarFill.type = Image.Type.Filled;
            handle.HpBarFill.fillMethod = Image.FillMethod.Horizontal;
            handle.HpBarFill.fillAmount = 1f;

            GameObject hpTextGo = new GameObject("HP Text");
            hpTextGo.transform.SetParent(hpBgGo.transform, worldPositionStays: false);
            RectTransform hpTextRect = hpTextGo.AddComponent<RectTransform>();
            hpTextRect.anchorMin = Vector2.zero;
            hpTextRect.anchorMax = Vector2.one;
            hpTextRect.offsetMin = Vector2.zero;
            hpTextRect.offsetMax = Vector2.zero;
            handle.HpText = hpTextGo.AddComponent<TextMeshProUGUI>();
            handle.HpText.alignment = TextAlignmentOptions.Center;
            handle.HpText.fontSize = 14f;
            handle.HpText.fontStyle = FontStyles.Bold;
            handle.HpText.color = Color.white;

            // Focus bar — hero only, sits above the HP bar.
            if (isHero)
            {
                GameObject focusBgGo = new GameObject("Focus Bar Bg");
                focusBgGo.transform.SetParent(cardGo.transform, worldPositionStays: false);
                RectTransform fbgRect = focusBgGo.AddComponent<RectTransform>();
                fbgRect.anchorMin = new Vector2(0f, 0f);
                fbgRect.anchorMax = new Vector2(1f, 0f);
                fbgRect.pivot = new Vector2(0.5f, 0f);
                fbgRect.sizeDelta = new Vector2(-24f, 14f);
                fbgRect.anchoredPosition = new Vector2(0f, 22f);
                Image fbgImg = focusBgGo.AddComponent<Image>();
                fbgImg.color = new Color(0.05f, 0.10f, 0.20f, 0.9f);

                GameObject focusFillGo = new GameObject("Focus Fill");
                focusFillGo.transform.SetParent(focusBgGo.transform, worldPositionStays: false);
                RectTransform ffRect = focusFillGo.AddComponent<RectTransform>();
                ffRect.anchorMin = Vector2.zero;
                ffRect.anchorMax = Vector2.one;
                ffRect.offsetMin = new Vector2(2f, 2f);
                ffRect.offsetMax = new Vector2(-2f, -2f);
                handle.FocusBarFill = focusFillGo.AddComponent<Image>();
                handle.FocusBarFill.color = new Color(0.35f, 0.65f, 1f);
                handle.FocusBarFill.type = Image.Type.Filled;
                handle.FocusBarFill.fillMethod = Image.FillMethod.Horizontal;
                handle.FocusBarFill.fillAmount = 0f;

                GameObject focusTextGo = new GameObject("Focus Text");
                focusTextGo.transform.SetParent(focusBgGo.transform, worldPositionStays: false);
                RectTransform ftRect = focusTextGo.AddComponent<RectTransform>();
                ftRect.anchorMin = Vector2.zero;
                ftRect.anchorMax = Vector2.one;
                ftRect.offsetMin = Vector2.zero;
                ftRect.offsetMax = Vector2.zero;
                handle.FocusText = focusTextGo.AddComponent<TextMeshProUGUI>();
                handle.FocusText.alignment = TextAlignmentOptions.Center;
                handle.FocusText.fontSize = 12f;
                handle.FocusText.color = Color.white;
            }

            return handle;
        }

        private void ShowBattlePanel()
        {
            if (_battlePanel != null) _battlePanel.SetActive(true);
        }

        private void HideBattlePanel()
        {
            if (_battlePanel != null) _battlePanel.SetActive(false);
            // Tear down enemy cards so the next battle starts clean.
            foreach (KeyValuePair<string, BattleActorCardHandle> kv in _battleCards)
            {
                if (kv.Value.GameObject != null && kv.Value.GameObject != _battleHeroCardGo)
                {
                    Destroy(kv.Value.GameObject);
                }
            }
            _battleCards.Clear();
            foreach (DamageNumberInstance dn in _damageNumbers)
            {
                if (dn.GameObject != null) Destroy(dn.GameObject);
            }
            _damageNumbers.Clear();
        }

        private void SyncBattleActorCards(Moonforge.Core.Combat.BattleState battle, string currentTurnActorId)
        {
            HashSet<string> seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (KeyValuePair<string, Moonforge.Core.Combat.BattleActorState> kv in battle.Actors)
            {
                string id = kv.Key;
                Moonforge.Core.Combat.BattleActorState actor = kv.Value;
                seen.Add(id);

                BattleActorCardHandle handle = ResolveBattleCard(id, actor.Faction);
                bool firstUpdate = handle.LastHp == 0 && actor.Hp == actor.MaxHp;
                bool isCurrentTurn = string.Equals(id, currentTurnActorId, System.StringComparison.Ordinal);

                // Detect HP drop → hit flash + damage number.
                if (!firstUpdate && actor.Hp < handle.LastHp)
                {
                    int delta = handle.LastHp - actor.Hp;
                    handle.HitFlashRemaining = HitFlashDuration;
                    SpawnDamageNumber(handle.GameObject, delta.ToString(), new Color(1f, 0.45f, 0.35f));
                    // If this hit just killed an enemy, hold the battle panel open so the
                    // death animation actually plays before the summary menu appears.
                    if (actor.Hp <= 0 && handle.LastHp > 0 && actor.Faction != Moonforge.Core.Combat.CombatFaction.Party)
                    {
                        _battleOutroHoldRemaining = BattleOutroHoldSeconds;
                    }
                }
                else if (!firstUpdate && actor.Hp > handle.LastHp)
                {
                    int delta = actor.Hp - handle.LastHp;
                    SpawnDamageNumber(handle.GameObject, "+" + delta, new Color(0.55f, 1f, 0.55f));
                }
                handle.LastHp = actor.Hp;

                // Update name + portrait + bars.
                handle.NameText.text = string.IsNullOrEmpty(actor.DisplayName) ? id : actor.DisplayName;
                TileVisualKind portraitKind = ResolvePortraitKind(id, actor.Faction);
                Sprite portraitSprite = _sprites.GetSprite(portraitKind);
                if (portraitSprite != null) handle.Portrait.sprite = portraitSprite;

                float hpPct = actor.MaxHp > 0 ? (float)System.Math.Max(0, actor.Hp) / actor.MaxHp : 0f;
                handle.HpBarFill.fillAmount = hpPct;
                handle.HpBarFill.color = ResolveHpColor(hpPct);
                handle.HpText.text = System.Math.Max(0, actor.Hp) + " / " + actor.MaxHp;
                if (actor.IsDowned)
                {
                    handle.NameText.color = new Color(0.6f, 0.6f, 0.6f);
                    handle.PortraitBaseColor = new Color(0.4f, 0.4f, 0.4f);
                }
                else
                {
                    handle.NameText.color = Color.white;
                    handle.PortraitBaseColor = Color.white;
                }

                // Hero focus bar.
                if (handle.FocusBarFill != null)
                {
                    int focus = actor.Resources != null && actor.Resources.TryGetValue("focus", out int f) ? f : 0;
                    int maxFocus = 6;
                    handle.FocusBarFill.fillAmount = maxFocus > 0 ? (float)focus / maxFocus : 0f;
                    if (handle.FocusText != null) handle.FocusText.text = "Focus " + focus + " / " + maxFocus;
                }

                handle.TurnIndicator.gameObject.SetActive(isCurrentTurn);
            }

            // Cull cards for actors no longer in battle.
            List<string> stale = new List<string>();
            foreach (string id in _battleCards.Keys)
            {
                if (!seen.Contains(id)) stale.Add(id);
            }
            foreach (string id in stale)
            {
                if (_battleCards[id].GameObject != null && _battleCards[id].GameObject != _battleHeroCardGo)
                {
                    Destroy(_battleCards[id].GameObject);
                }
                _battleCards.Remove(id);
            }
        }

        private BattleActorCardHandle ResolveBattleCard(string actorId, Moonforge.Core.Combat.CombatFaction faction)
        {
            if (_battleCards.TryGetValue(actorId, out BattleActorCardHandle existing)) return existing;

            BattleActorCardHandle handle;
            if (faction == Moonforge.Core.Combat.CombatFaction.Party)
            {
                // The hero card was pre-built. Adopt it.
                handle = _battleHeroCard;
                handle.ActorId = actorId;
            }
            else
            {
                GameObject enemyGo = new GameObject("Enemy Card " + actorId);
                enemyGo.transform.SetParent(_battleEnemyRow, worldPositionStays: false);
                RectTransform enemyRect = enemyGo.AddComponent<RectTransform>();
                enemyRect.sizeDelta = new Vector2(180f, 280f);
                LayoutElement le = enemyGo.AddComponent<LayoutElement>();
                le.preferredWidth = 180f;
                le.preferredHeight = 280f;
                handle = BuildBattleActorCard(enemyGo, isHero: false);
                handle.ActorId = actorId;
            }
            handle.LastHp = 0; // marker for "first update"
            _battleCards[actorId] = handle;
            return handle;
        }

        private static TileVisualKind ResolvePortraitKind(string actorId, Moonforge.Core.Combat.CombatFaction faction)
        {
            if (faction == Moonforge.Core.Combat.CombatFaction.Party) return TileVisualKind.Hero;
            if (actorId.IndexOf("boss", System.StringComparison.OrdinalIgnoreCase) >= 0) return TileVisualKind.EnemyBoss;
            if (actorId.IndexOf("elite", System.StringComparison.OrdinalIgnoreCase) >= 0) return TileVisualKind.EnemyElite;
            return TileVisualKind.Enemy;
        }

        private static Color ResolveHpColor(float pct)
        {
            if (pct > 0.6f) return new Color(0.40f, 0.85f, 0.40f);
            if (pct > 0.3f) return new Color(0.95f, 0.78f, 0.20f);
            return new Color(0.90f, 0.30f, 0.25f);
        }

        // ---- Battle animations (run from Update) ----------------------------------------

        private void SpawnDamageNumber(GameObject anchor, string text, Color color)
        {
            if (anchor == null || _canvas == null) return;
            GameObject go = new GameObject("DamageNumber");
            go.transform.SetParent(_canvas.transform, worldPositionStays: false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160f, 60f);

            // Position above the anchor (screen-space; uses anchor's RectTransform world corners).
            RectTransform anchorRect = anchor.GetComponent<RectTransform>();
            if (anchorRect != null)
            {
                Vector3[] corners = new Vector3[4];
                anchorRect.GetWorldCorners(corners);
                // Top-center of the anchor in world space.
                Vector3 top = (corners[1] + corners[2]) * 0.5f;
                rect.position = top + new Vector3(0f, 16f, 0f);
            }

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 40f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;

            DamageNumberInstance dn = new DamageNumberInstance
            {
                GameObject = go,
                Rect = rect,
                Text = tmp,
                StartPos = rect.anchoredPosition,
                Elapsed = 0f
            };
            _damageNumbers.Add(dn);
        }

        private void UpdateBattleAnimations()
        {
            float dt = Time.deltaTime;

            // Outro hold: keep the battle panel visible while the killing-blow animation
            // plays, then flush the deferred summary.
            if (_battleOutroHoldRemaining > 0f)
            {
                _battleOutroHoldRemaining -= dt;
                if (_battleOutroHoldRemaining <= 0f && _pendingBattleSummary != null)
                {
                    BattleSummaryRenderModel pending = _pendingBattleSummary;
                    _pendingBattleSummary = null;
                    RenderBattleSummaryNow(pending);
                }
            }

            // Hit flashes: actor portraits tint red briefly then lerp back to base.
            foreach (KeyValuePair<string, BattleActorCardHandle> kv in _battleCards)
            {
                BattleActorCardHandle h = kv.Value;
                if (h.Portrait == null) continue;
                if (h.HitFlashRemaining > 0f)
                {
                    h.HitFlashRemaining = System.Math.Max(0f, h.HitFlashRemaining - dt);
                    float t = h.HitFlashRemaining / HitFlashDuration;
                    h.Portrait.color = Color.Lerp(h.PortraitBaseColor, new Color(1f, 0.25f, 0.25f), t);
                }
                else
                {
                    h.Portrait.color = h.PortraitBaseColor;
                }
            }

            // Damage numbers float up and fade.
            for (int i = _damageNumbers.Count - 1; i >= 0; i--)
            {
                DamageNumberInstance dn = _damageNumbers[i];
                if (dn.GameObject == null) { _damageNumbers.RemoveAt(i); continue; }
                dn.Elapsed += dt;
                float t = dn.Elapsed / DamageNumberLifetime;
                if (t >= 1f)
                {
                    Destroy(dn.GameObject);
                    _damageNumbers.RemoveAt(i);
                    continue;
                }
                // Ease-out float up, then fade in last 30%.
                float eased = 1f - (1f - t) * (1f - t);
                dn.Rect.position += new Vector3(0f, DamageNumberFloatDistance * dt / DamageNumberLifetime, 0f);
                float alpha = t < 0.7f ? 1f : 1f - ((t - 0.7f) / 0.3f);
                Color c = dn.Text.color;
                c.a = alpha;
                dn.Text.color = c;
                // Slight scale punch at start.
                float scale = 1f + (1f - t) * 0.4f;
                dn.Rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        public void RenderBattleSummary(BattleSummaryRenderModel model)
        {
            // If the session just transitioned here on a killing blow, the battle panel
            // is still mid-death-animation. Stash the summary and render it after the
            // outro hold completes (driven from UpdateBattleAnimations).
            if (_battleOutroHoldRemaining > 0f)
            {
                _pendingBattleSummary = model;
                return;
            }
            RenderBattleSummaryNow(model);
        }

        private void RenderBattleSummaryNow(BattleSummaryRenderModel model)
        {
            BeginMenu(Strip(model.Outcome));
            StringBuilder header = new StringBuilder();
            header.AppendLine(model.EncounterTitle);
            header.AppendLine();
            header.Append("<color=#cfd4dd>Gold</color>     ").Append(model.GoldBefore).Append(" -> ")
                .Append(model.GoldAfter).Append("   ").Append(FormatDelta(model.GoldDelta)).AppendLine();
            header.Append("<color=#cfd4dd>Tokens</color>   ").Append(model.TokensBefore).Append(" -> ")
                .Append(model.TokensAfter).Append("   ").Append(FormatDelta(model.TokensDelta)).AppendLine();
            header.Append("<color=#cfd4dd>Potions</color>  ").Append(model.PotionsBefore).Append(" -> ")
                .Append(model.PotionsAfter).Append("   ").Append(FormatDelta(model.PotionsDelta)).AppendLine();
            header.Append("<color=#cfd4dd>Herbs</color>    ").Append(model.HerbsBefore).Append(" -> ")
                .Append(model.HerbsAfter).Append("   ").Append(FormatDelta(model.HerbsDelta));
            SetMenuBody(header.ToString());

            bool needsBossReward = model.BossRewardOptions != null
                && model.BossRewardOptions.Count > 0
                && string.IsNullOrWhiteSpace(model.BossRewardChosen);
            if (needsBossReward)
            {
                for (int i = 0; i < model.BossRewardOptions.Count && i < 3; i++)
                {
                    PlayerAction action = i switch
                    {
                        0 => PlayerAction.Digit1,
                        1 => PlayerAction.Digit2,
                        2 => PlayerAction.Digit3,
                        _ => PlayerAction.None
                    };
                    AddMenuButton(model.BossRewardOptions[i], action);
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(model.BossRewardChosen))
                {
                    AddMenuInfoRow("Chosen: " + Strip(model.BossRewardChosen));
                }
                AddMenuButton("[Enter]  Continue", PlayerAction.Confirm);
            }
            EndMenu(Strip(model.Controls));
        }

        public void RenderDialogue(DialogueRenderModel model)
        {
            BeginMenu(model.NpcName);
            SetMenuBody(model.BodyText);
            if (model.Choices != null && model.Choices.Count > 0)
            {
                for (int i = 0; i < model.Choices.Count && i < 5; i++)
                {
                    DialogueChoiceView choice = model.Choices[i];
                    PlayerAction action = i switch
                    {
                        0 => PlayerAction.Digit1,
                        1 => PlayerAction.Digit2,
                        2 => PlayerAction.Digit3,
                        3 => PlayerAction.Digit4,
                        4 => PlayerAction.Digit5,
                        _ => PlayerAction.None
                    };
                    AddMenuButton("[" + choice.Hotkey + "]  " + Strip(choice.Text), action);
                }
            }
            AddMenuButton("[Esc]  Step away", PlayerAction.Cancel);
            EndMenu(model.Controls);
        }

        public void RenderContractNotice(string title, string body, string controls)
        {
            BeginMenu(title);
            SetMenuBody(body);
            AddMenuButton("[Enter]  Continue", PlayerAction.Confirm);
            EndMenu(controls);
        }

        public void RenderContractJournal(string title, IReadOnlyList<string> lines, string controls)
        {
            BeginMenu(title);
            // List-style screens (Contract Journal, Gear, Shrine, Boss Reward Chest, town
            // interaction menus). Each `1. Foo`-prefixed line becomes a button; the next
            // non-prefixed line(s) become a small description row directly below.
            //
            // The very first description block — those before any hotkey appears — gets
            // promoted into the panel's body text so it reads as the screen's intro
            // instead of as orphan info rows above the buttons.
            StringBuilder pendingInfo = new StringBuilder();
            bool anyButtonAdded = false;
            foreach (string line in lines)
            {
                string stripped = Strip(line);
                PlayerAction action = TryParseHotkeyAction(stripped, out string hotkey);
                if (action == PlayerAction.None)
                {
                    if (!string.IsNullOrWhiteSpace(stripped))
                    {
                        pendingInfo.AppendLine(stripped.TrimStart());
                    }
                }
                else
                {
                    if (pendingInfo.Length > 0 && !anyButtonAdded)
                    {
                        // Pre-button info becomes the body text intro.
                        SetMenuBody(pendingInfo.ToString().TrimEnd());
                        pendingInfo.Clear();
                    }
                    else if (pendingInfo.Length > 0)
                    {
                        AddMenuInfoRow(pendingInfo.ToString().TrimEnd());
                        pendingInfo.Clear();
                    }
                    AddMenuButton(stripped, action);
                    anyButtonAdded = true;
                }
            }
            if (pendingInfo.Length > 0)
            {
                if (!anyButtonAdded) SetMenuBody(pendingInfo.ToString().TrimEnd());
                else AddMenuInfoRow(pendingInfo.ToString().TrimEnd());
            }
            // Always include a return option so list screens can be exited by mouse.
            AddMenuButton("[Enter]  Return", PlayerAction.Confirm);
            EndMenu(controls);
        }

        private static PlayerAction TryParseHotkeyAction(string line, out string hotkey)
        {
            hotkey = null;
            if (string.IsNullOrEmpty(line)) return PlayerAction.None;
            // Match a leading "<digit>." (e.g. "1. Field Rations [UNLOCKED]" used by the
            // Shrine and Boss Reward list-style screens).
            int dotIndex = line.IndexOf('.');
            if (dotIndex >= 1 && dotIndex <= 2 && char.IsDigit(line[0]))
            {
                hotkey = line.Substring(0, dotIndex);
                return hotkey switch
                {
                    "1" => PlayerAction.Digit1,
                    "2" => PlayerAction.Digit2,
                    "3" => PlayerAction.Digit3,
                    "4" => PlayerAction.Digit4,
                    "5" => PlayerAction.Digit5,
                    "6" => PlayerAction.Digit6,
                    _ => PlayerAction.None
                };
            }
            return PlayerAction.None;
        }

        // ---- Tilemap painting ----------------------------------------------------------

        private void PaintMapTiles(MapRenderModel model)
        {
            _tilemap.ClearAllTiles();
            ReleaseWallSprites();
            ReleaseFloorSprites();
            ExplorationMapState map = model.Map;
            if (!map.IsConfigured)
            {
                return;
            }

            bool isTown = !string.IsNullOrEmpty(model.Title) && model.Title.IndexOf("Town", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (_camera != null)
            {
                _camera.backgroundColor = isTown ? _townBackgroundColor : _dungeonBackgroundColor;
            }

            // Both floors and walls render as SpriteRenderer GameObjects rather than
            // going through Unity's Tilemap. The Tilemap path silently drops tiles for
            // a number of reasons (chunk culling, sprite-import races, ScriptableObject
            // caching) and produced "solid color floor" reports even when the underlying
            // sprite was correct. The SpriteRenderer path is reliable and the cost is
            // trivial at this map size.
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    if (!map.TryGetTileFlags(new GridPosition(x, y), out ExplorationTileFlags flags))
                    {
                        continue;
                    }

                    TileVisualKind kind = ResolveTileKind(flags, isTown);
                    if (kind == TileVisualKind.DungeonWall || kind == TileVisualKind.TownWall)
                    {
                        PaintWallSprite(x, y, kind);
                    }
                    else
                    {
                        PaintFloorSprite(x, y, kind);
                    }
                }
            }
        }

        private void PaintWallSprite(int gridX, int gridY, TileVisualKind kind)
        {
            Sprite sprite = _sprites.GetSprite(kind);
            if (sprite == null) return;
            GameObject go = new GameObject("Wall " + kind);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = GridToWorld(gridX, gridY);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            // sortingOrder 2: above the floor (0), below markers (5) and actors (10+).
            sr.sortingOrder = 2;
            _wallSprites.Add(go);
        }

        private void PaintFloorSprite(int gridX, int gridY, TileVisualKind kind)
        {
            Sprite sprite = _sprites.GetSprite(kind);
            if (sprite == null) return;
            GameObject go = new GameObject("Floor " + kind);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = GridToWorld(gridX, gridY);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            // sortingOrder 0: below walls (2), markers (5), actors (10+).
            sr.sortingOrder = 0;
            _floorSprites.Add(go);
        }

        private void ReleaseWallSprites()
        {
            foreach (GameObject go in _wallSprites)
            {
                if (go != null) Destroy(go);
            }
            _wallSprites.Clear();
        }

        private void ReleaseFloorSprites()
        {
            foreach (GameObject go in _floorSprites)
            {
                if (go != null) Destroy(go);
            }
            _floorSprites.Clear();
        }

        private static TileVisualKind ResolveTileKind(ExplorationTileFlags flags, bool isTown)
        {
            bool walkable = (flags & ExplorationTileFlags.Walkable) == ExplorationTileFlags.Walkable;
            if (!walkable)
            {
                return isTown ? TileVisualKind.TownWall : TileVisualKind.DungeonWall;
            }
            // Walkable. A stair tile is marked Interactable in DungeonGenerator; town doesn't
            // use Interactable on the floor, so we don't have to disambiguate up vs down at
            // this layer — markers render arrows on top instead.
            return isTown ? TileVisualKind.TownFloor : TileVisualKind.DungeonFloor;
        }

        // ---- Actor + Marker rendering -------------------------------------------------
        // Actors (hero, guard, enemies) and markers (interactable landmarks, stairs)
        // are kept as persistent sprite handles so we can smoothly tween them between
        // grid cells and apply per-frame effects like the marker bob.

        private sealed class ActorSpriteHandle
        {
            public GameObject GameObject;
            public SpriteRenderer Renderer;
            public Vector3 TargetPosition;
            public Vector3 CurrentPosition;
            public TileVisualKind Kind;
        }

        private sealed class MarkerSpriteHandle
        {
            public GameObject GameObject;
            public Vector3 BasePosition;
            public float PhaseOffset;
        }

        private readonly Dictionary<string, ActorSpriteHandle> _actorSprites = new Dictionary<string, ActorSpriteHandle>(System.StringComparer.Ordinal);
        private readonly List<MarkerSpriteHandle> _markerHandles = new List<MarkerSpriteHandle>();
        // Walls bypass the Tilemap entirely — when painted via Tilemap.SetTile they
        // rendered invisibly even though the sprite has correct opaque pixel data.
        // Render them as individual SpriteRenderer GameObjects instead.
        private readonly List<GameObject> _wallSprites = new List<GameObject>();
        private readonly List<GameObject> _floorSprites = new List<GameObject>();
        private Vector3 _cameraTargetXY;
        private bool _hasCameraTarget;
        private float _markerBobTime;

        // Tile-cells-per-second. 12 = traversing a cell in ~85ms, which is fast enough that
        // input feels responsive but slow enough that the eye sees the movement instead
        // of a teleport.
        private const float ActorMoveSpeed = 12f;
        private const float CameraFollowSpeed = 14f;
        private const float MarkerBobAmplitude = 0.08f;
        private const float MarkerBobFrequency = 2.0f;

        private void PaintActors(IReadOnlyList<MapActor> actors)
        {
            if (actors == null)
            {
                return;
            }

            HashSet<string> seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (MapActor actor in actors)
            {
                seen.Add(actor.ActorId);
                Vector3 worldPos = GridToWorld(actor.Position.X, actor.Position.Y);
                TileVisualKind visualKind = ResolveActorVisualKind(actor.Kind);

                if (!_actorSprites.TryGetValue(actor.ActorId, out ActorSpriteHandle handle))
                {
                    handle = new ActorSpriteHandle
                    {
                        GameObject = new GameObject("Actor " + actor.DisplayName),
                        TargetPosition = worldPos,
                        CurrentPosition = worldPos,
                        Kind = visualKind
                    };
                    handle.GameObject.transform.SetParent(transform, worldPositionStays: false);
                    handle.GameObject.transform.position = worldPos;
                    handle.Renderer = handle.GameObject.AddComponent<SpriteRenderer>();
                    handle.Renderer.sortingOrder = actor.Kind == MapActorKind.Hero ? 12 : 10;
                    handle.Renderer.sprite = _sprites.GetSprite(visualKind);
                    _actorSprites[actor.ActorId] = handle;
                }
                else
                {
                    if (handle.Kind != visualKind)
                    {
                        handle.Renderer.sprite = _sprites.GetSprite(visualKind);
                        handle.Kind = visualKind;
                    }
                    handle.TargetPosition = worldPos;
                }

                if (actor.Kind == MapActorKind.Hero)
                {
                    _cameraTargetXY = new Vector3(worldPos.x, worldPos.y, -10f);
                    _hasCameraTarget = true;
                }
            }

            // Cull actors no longer present (e.g. left a dungeon floor, guard removed, etc.).
            if (_actorSprites.Count != seen.Count)
            {
                List<string> stale = new List<string>();
                foreach (string id in _actorSprites.Keys)
                {
                    if (!seen.Contains(id)) stale.Add(id);
                }
                foreach (string id in stale)
                {
                    if (_actorSprites[id].GameObject != null) Destroy(_actorSprites[id].GameObject);
                    _actorSprites.Remove(id);
                }
            }
        }

        private static TileVisualKind ResolveActorVisualKind(MapActorKind kind) => kind switch
        {
            MapActorKind.Hero => TileVisualKind.Hero,
            MapActorKind.Guard => TileVisualKind.TownGuardMarker,
            MapActorKind.Enemy => TileVisualKind.Enemy,
            MapActorKind.EliteEnemy => TileVisualKind.EnemyElite,
            MapActorKind.BossEnemy => TileVisualKind.EnemyBoss,
            _ => TileVisualKind.Npc
        };

        private void PaintMarkers(IReadOnlyList<MapMarker> markers)
        {
            ReleaseMarkerSprites();
            if (markers == null)
            {
                return;
            }
            for (int i = 0; i < markers.Count; i++)
            {
                MapMarker marker = markers[i];
                TileVisualKind kind = ResolveMarkerKind(marker.Symbol);
                Sprite sprite = _sprites.GetSprite(kind);
                if (sprite == null)
                {
                    continue;
                }
                GameObject go = new GameObject("Marker " + marker.Label);
                go.transform.SetParent(transform, worldPositionStays: false);
                Vector3 basePos = GridToWorld(marker.Position.X, marker.Position.Y) + new Vector3(0f, 0f, -0.01f);
                go.transform.position = basePos;
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 5;
                _markerSprites.Add(go);
                _markerHandles.Add(new MarkerSpriteHandle
                {
                    GameObject = go,
                    BasePosition = basePos,
                    // Stagger the bob phase per marker so they don't move in lockstep —
                    // looks more like a lived-in town.
                    PhaseOffset = i * 0.7f
                });
            }
        }

        // Squared distance below which the hero is considered "done" sliding
        // between cells and ready to accept the next held-movement command.
        // 0.0004 = ~0.02 cells, well inside one frame of tween at 12 cells/sec.
        private const float HeroIdleEpsilonSqr = 0.0004f;

        private bool IsHeroTweenIdle()
        {
            foreach (KeyValuePair<string, ActorSpriteHandle> kv in _actorSprites)
            {
                if (kv.Value.Kind == TileVisualKind.Hero)
                {
                    Vector3 delta = kv.Value.CurrentPosition - kv.Value.TargetPosition;
                    return delta.sqrMagnitude < HeroIdleEpsilonSqr;
                }
            }
            // No hero painted yet (boot, scene transition). Don't block input.
            return true;
        }

        private void SmoothMovementAndCamera()
        {
            float dt = Time.deltaTime;
            float step = ActorMoveSpeed * dt;
            foreach (KeyValuePair<string, ActorSpriteHandle> kv in _actorSprites)
            {
                ActorSpriteHandle handle = kv.Value;
                if (handle.GameObject == null) continue;
                handle.CurrentPosition = Vector3.MoveTowards(handle.CurrentPosition, handle.TargetPosition, step);
                handle.GameObject.transform.position = handle.CurrentPosition;
            }

            if (_hasCameraTarget && _camera != null)
            {
                Vector3 cur = _camera.transform.position;
                Vector3 next = Vector3.MoveTowards(cur, _cameraTargetXY, CameraFollowSpeed * dt);
                _camera.transform.position = new Vector3(next.x, next.y, -10f);
            }
        }

        private void BobMarkers()
        {
            _markerBobTime += Time.deltaTime;
            for (int i = 0; i < _markerHandles.Count; i++)
            {
                MarkerSpriteHandle handle = _markerHandles[i];
                if (handle.GameObject == null) continue;
                float y = Mathf.Sin((_markerBobTime * MarkerBobFrequency) + handle.PhaseOffset) * MarkerBobAmplitude;
                handle.GameObject.transform.position = handle.BasePosition + new Vector3(0f, y, 0f);
            }
        }

        private void ReleaseActorSprites()
        {
            foreach (KeyValuePair<string, ActorSpriteHandle> kv in _actorSprites)
            {
                if (kv.Value.GameObject != null) Destroy(kv.Value.GameObject);
            }
            _actorSprites.Clear();
            _hasCameraTarget = false;
        }

        private static TileVisualKind ResolveMarkerKind(char symbol) => symbol switch
        {
            'G' => TileVisualKind.TownGuardMarker,
            'H' => TileVisualKind.TownHealerMarker,
            'A' => TileVisualKind.TownAlchemistMarker,
            'C' => TileVisualKind.TownCacheMarker,
            'F' => TileVisualKind.TownFountainMarker,
            'Q' => TileVisualKind.TownQuestBoardMarker,
            'S' => TileVisualKind.TownShrineMarker,
            '>' => TileVisualKind.DungeonStairsDown,
            '<' => TileVisualKind.DungeonStairsUp,
            _ => TileVisualKind.Npc
        };

        private void ReleaseMarkerSprites()
        {
            foreach (GameObject go in _markerSprites)
            {
                if (go != null)
                {
                    Destroy(go);
                }
            }
            _markerSprites.Clear();
            _markerHandles.Clear();
        }

        private Vector3Int GridToCell(int gridX, int gridY)
        {
            // Grid Y points down (top-left origin); Unity world Y points up. Flip on the way in.
            return new Vector3Int(gridX, -gridY, 0);
        }

        private Vector3 GridToWorld(int gridX, int gridY)
        {
            // Centre on the cell — sprites use centre pivots and tiles span [cell, cell+1).
            return new Vector3(gridX * _cellSize + _cellSize * 0.5f, -gridY * _cellSize + _cellSize * 0.5f, 0f);
        }

        private void ShowMap()
        {
            if (_grid != null) _grid.gameObject.SetActive(true);
        }

        private void HideMap()
        {
            if (_grid != null) _grid.gameObject.SetActive(false);
            ReleaseMarkerSprites();
            ReleaseActorSprites();
            ReleaseWallSprites();
        }

        // ---- helpers -------------------------------------------------------------------

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Strip Spectre.Console markup so it doesn't show as literal text in Unity.</summary>
        private static string Strip(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            StringBuilder sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '[')
                {
                    int end = text.IndexOf(']', i + 1);
                    if (end > 0 && end - i < 30)
                    {
                        string inner = text.Substring(i + 1, end - i - 1);
                        if (inner == "/" || inner.StartsWith("/") || IsLikelyMarkup(inner))
                        {
                            i = end + 1;
                            continue;
                        }
                    }
                }
                sb.Append(text[i]);
                i++;
            }
            return sb.ToString();
        }

        private static bool IsLikelyMarkup(string tag)
        {
            // Spectre.Console tags are short colour/style names like grey, red, bold, etc.
            // Hotkey tags like [N] or [1/2/3] are short and we don't want to strip them.
            if (tag.Length == 0 || tag.Length > 24) return false;
            for (int i = 0; i < tag.Length; i++)
            {
                char c = tag[i];
                if (!char.IsLetter(c) && c != ' ' && c != '#') return false;
            }
            // If it's just one or two letters, treat as a hotkey label — KEEP it.
            if (tag.Length <= 2) return false;
            return true;
        }

        private void BuildCamera()
        {
            GameObject go = new GameObject("Roguelike Camera");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = new Vector3(0f, 0f, -10f);
            _camera = go.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = _orthographicSize;
            _camera.backgroundColor = _backgroundColor;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 50f;
        }

        private void BuildTilemap()
        {
            GameObject gridGo = new GameObject("Roguelike Grid");
            gridGo.transform.SetParent(transform, worldPositionStays: false);
            _grid = gridGo.AddComponent<Grid>();
            _grid.cellSize = new Vector3(_cellSize, _cellSize, 0f);
            _grid.cellLayout = GridLayout.CellLayout.Rectangle;

            GameObject tmGo = new GameObject("World Tilemap");
            tmGo.transform.SetParent(gridGo.transform, worldPositionStays: false);
            _tilemap = tmGo.AddComponent<Tilemap>();
            TilemapRenderer tr = tmGo.AddComponent<TilemapRenderer>();
            tr.sortOrder = TilemapRenderer.SortOrder.TopRight;
        }

        // ---- Menu panel (click-to-tick buttons) ----------------------------------------

        private void BuildMenuPanel()
        {
            // === Outer panel ===
            // Layout: title band (top, accent color) → body text region → thin separator
            // → button list (bottom). All children pinned to fixed regions of the panel
            // so the layout never falls apart, and the panel itself is a deliberate
            // dialog box rather than scattered UI elements.
            _menuPanel = new GameObject("Menu Panel");
            _menuPanel.transform.SetParent(_canvas.transform, worldPositionStays: false);
            RectTransform panelRect = _menuPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 680f);
            Image bg = _menuPanel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);

            // === Title band (top) ===
            GameObject titleBand = new GameObject("Title Band");
            titleBand.transform.SetParent(_menuPanel.transform, worldPositionStays: false);
            RectTransform titleBandRect = titleBand.AddComponent<RectTransform>();
            titleBandRect.anchorMin = new Vector2(0f, 1f);
            titleBandRect.anchorMax = new Vector2(1f, 1f);
            titleBandRect.pivot = new Vector2(0.5f, 1f);
            titleBandRect.sizeDelta = new Vector2(0f, 58f);
            titleBandRect.anchoredPosition = Vector2.zero;
            Image titleBg = titleBand.AddComponent<Image>();
            titleBg.color = new Color(0.18f, 0.26f, 0.40f, 0.95f);

            GameObject titleGo = new GameObject("Title Text");
            titleGo.transform.SetParent(titleBand.transform, worldPositionStays: false);
            RectTransform titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(24f, 0f);
            titleRect.offsetMax = new Vector2(-24f, 0f);
            _menuTitle = titleGo.AddComponent<TextMeshProUGUI>();
            _menuTitle.alignment = TextAlignmentOptions.MidlineLeft;
            _menuTitle.fontSize = 28f;
            _menuTitle.fontStyle = FontStyles.Bold;
            _menuTitle.color = new Color(0.95f, 0.95f, 1f);
            _menuTitle.richText = true;

            // === Body text region (below title) ===
            // Holds the NPC's spoken text, dialog body, or summary text. ~220px tall is
            // enough for ~6 lines of wrapped text at 22pt.
            GameObject bodyGo = new GameObject("Body Text");
            bodyGo.transform.SetParent(_menuPanel.transform, worldPositionStays: false);
            RectTransform bodyRect = bodyGo.AddComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            bodyRect.sizeDelta = new Vector2(0f, 220f);
            bodyRect.anchoredPosition = new Vector2(0f, -76f); // 58 title + 18 gap
            // Pad inside the panel
            RectTransform bodyPad = bodyRect;
            bodyPad.offsetMin = new Vector2(28f, bodyPad.offsetMin.y);
            bodyPad.offsetMax = new Vector2(-28f, bodyPad.offsetMax.y);
            _menuBodyText = bodyGo.AddComponent<TextMeshProUGUI>();
            _menuBodyText.alignment = TextAlignmentOptions.TopLeft;
            _menuBodyText.fontSize = 22f;
            _menuBodyText.color = new Color(0.86f, 0.88f, 0.94f);
            _menuBodyText.richText = true;
            _menuBodyText.enableWordWrapping = true;

            // === Separator line ===
            _menuSeparator = new GameObject("Separator");
            _menuSeparator.transform.SetParent(_menuPanel.transform, worldPositionStays: false);
            RectTransform sepRect = _menuSeparator.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0f, 1f);
            sepRect.anchorMax = new Vector2(1f, 1f);
            sepRect.pivot = new Vector2(0.5f, 1f);
            sepRect.sizeDelta = new Vector2(-56f, 2f);
            sepRect.anchoredPosition = new Vector2(0f, -312f); // 58 + 18 + 220 + 16
            Image sepImg = _menuSeparator.AddComponent<Image>();
            sepImg.color = new Color(0.30f, 0.36f, 0.48f, 0.6f);

            // === Button list (fills remaining space below separator) ===
            GameObject containerGo = new GameObject("Buttons");
            containerGo.transform.SetParent(_menuPanel.transform, worldPositionStays: false);
            _menuButtonContainer = containerGo.AddComponent<RectTransform>();
            _menuButtonContainer.anchorMin = new Vector2(0f, 0f);
            _menuButtonContainer.anchorMax = new Vector2(1f, 1f);
            _menuButtonContainer.pivot = new Vector2(0.5f, 1f);
            _menuButtonContainer.offsetMin = new Vector2(28f, 24f);
            _menuButtonContainer.offsetMax = new Vector2(-28f, -326f); // below separator
            VerticalLayoutGroup vlg = containerGo.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 6f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            _menuPanel.SetActive(false);
        }

        private void BeginMenu(string title)
        {
            HideMap();
            HideMovementControls();
            HideActionBar();
            ClearMenuButtons();
            if (_battlePanel != null && _battlePanel.activeSelf) HideBattlePanel();
            HideGearPanel();
            if (_menuTitle != null) _menuTitle.text = title;
            if (_menuBodyText != null) _menuBodyText.text = string.Empty;
            _menuPanel.SetActive(true);
            // Hide the top/right HUD text strips while a menu is open — the menu panel
            // is the focus and the floating text was disconnected and noisy.
            _headerText.text = string.Empty;
            _bodyText.text = string.Empty;
            _messageText.text = string.Empty;
        }

        private void SetMenuBody(string text)
        {
            if (_menuBodyText != null)
            {
                _menuBodyText.text = Strip(text ?? string.Empty);
            }
        }

        private static string FormatDelta(long delta)
        {
            if (delta > 0) return "<color=#7aff7a>+" + delta + "</color>";
            if (delta < 0) return "<color=#ff8a7a>" + delta + "</color>";
            return "<color=#888888>±0</color>";
        }

        private static string FormatDelta(int delta) => FormatDelta((long)delta);

        private void EndMenu(string controlsHint)
        {
            _footerText.text = string.IsNullOrEmpty(controlsHint)
                ? "Use the mouse to click an option, or press the highlighted key."
                : controlsHint + "  |  click or press the highlighted key";
            _messageText.text = string.Empty;
        }

        private void AddMenuButton(string label, PlayerAction action, bool enabled = true)
        {
            GameObject go = new GameObject("Btn " + label);
            go.transform.SetParent(_menuButtonContainer, worldPositionStays: false);
            Image bg = go.AddComponent<Image>();
            bg.color = enabled
                ? new Color(0.16f, 0.22f, 0.32f, 0.92f)
                : new Color(0.10f, 0.12f, 0.16f, 0.65f);
            Button btn = go.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.42f, 0.62f, 0.92f);
            colors.pressedColor = new Color(0.30f, 0.50f, 0.80f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f);
            btn.colors = colors;
            btn.interactable = enabled;
            btn.targetGraphic = bg;

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 44f;
            le.minHeight = 44f;

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, worldPositionStays: false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 2f);
            textRect.offsetMax = new Vector2(-18f, -2f);
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.fontSize = 20f;
            tmp.color = enabled ? new Color(0.96f, 0.96f, 1f) : new Color(0.7f, 0.7f, 0.72f);
            tmp.richText = true;

            if (enabled)
            {
                PlayerAction capturedAction = action;
                btn.onClick.AddListener(() => OnMenuButtonClicked(capturedAction));
            }

            _menuButtons.Add(go);
        }

        /// <summary>
        /// Adds a passive, non-clickable info row to the button list. Used for
        /// descriptive lines that accompany choices (Contract Journal, Gear, Shrine,
        /// Boss Reward). Replaces the previous "render info as a disabled button" hack
        /// so info lines don't look like dead buttons.
        /// </summary>
        private void AddMenuInfoRow(string text)
        {
            GameObject go = new GameObject("Info");
            go.transform.SetParent(_menuButtonContainer, worldPositionStays: false);
            // No Image background — info rows blend with the panel.

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 28f;
            le.minHeight = 24f;

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, worldPositionStays: false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(36f, 0f);
            textRect.offsetMax = new Vector2(-18f, 0f);
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.fontSize = 16f;
            tmp.color = new Color(0.65f, 0.70f, 0.80f);
            tmp.richText = true;
            tmp.enableWordWrapping = true;

            _menuButtons.Add(go);
        }

        private void OnMenuButtonClicked(PlayerAction action)
        {
            if (_session == null)
            {
                return;
            }
            _session.Tick(action);
        }

        private void ClearMenuButtons()
        {
            foreach (GameObject go in _menuButtons)
            {
                if (go != null) Destroy(go);
            }
            _menuButtons.Clear();
        }

        private void HideMenu()
        {
            ClearMenuButtons();
            if (_menuPanel != null) _menuPanel.SetActive(false);
        }

        // ---- Gear Inventory two-pane panel ----------------------------------------------

        private GameObject _gearPanel;
        private TMP_Text _gearTitleText;
        private TMP_Text _gearWeaponCardText;
        private TMP_Text _gearArmorCardText;
        private TMP_Text _gearAccessoryCardText;
        private Image _gearWeaponCardIcon;
        private Image _gearArmorCardIcon;
        private Image _gearAccessoryCardIcon;
        private RectTransform _gearFilterContainer;
        private RectTransform _gearInventoryContainer;
        private TMP_Text _gearMessageText;
        private TMP_Text _gearFooterText;
        private readonly List<GameObject> _gearFilterButtons = new List<GameObject>();
        private readonly List<GameObject> _gearInventoryRows = new List<GameObject>();

        private void BuildGearPanel()
        {
            // Wider than the standard menu panel — we need room for a left "loadout"
            // column and a right inventory list side-by-side.
            _gearPanel = new GameObject("Gear Panel");
            _gearPanel.transform.SetParent(_canvas.transform, worldPositionStays: false);
            RectTransform rect = _gearPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(960f, 680f);
            Image bg = _gearPanel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);

            // Title band.
            GameObject titleBand = new GameObject("Title Band");
            titleBand.transform.SetParent(_gearPanel.transform, worldPositionStays: false);
            RectTransform titleBandRect = titleBand.AddComponent<RectTransform>();
            titleBandRect.anchorMin = new Vector2(0f, 1f);
            titleBandRect.anchorMax = new Vector2(1f, 1f);
            titleBandRect.pivot = new Vector2(0.5f, 1f);
            titleBandRect.sizeDelta = new Vector2(0f, 54f);
            titleBandRect.anchoredPosition = Vector2.zero;
            Image titleBg = titleBand.AddComponent<Image>();
            titleBg.color = new Color(0.18f, 0.26f, 0.40f, 0.95f);
            GameObject titleGo = new GameObject("Title Text");
            titleGo.transform.SetParent(titleBand.transform, worldPositionStays: false);
            RectTransform titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(24f, 0f);
            titleRect.offsetMax = new Vector2(-24f, 0f);
            _gearTitleText = titleGo.AddComponent<TextMeshProUGUI>();
            _gearTitleText.alignment = TextAlignmentOptions.MidlineLeft;
            _gearTitleText.fontSize = 26f;
            _gearTitleText.fontStyle = FontStyles.Bold;
            _gearTitleText.color = new Color(0.95f, 0.95f, 1f);
            _gearTitleText.richText = true;

            // Filter tab row.
            GameObject filterRow = new GameObject("Filter Row");
            filterRow.transform.SetParent(_gearPanel.transform, worldPositionStays: false);
            RectTransform filterRect = filterRow.AddComponent<RectTransform>();
            filterRect.anchorMin = new Vector2(0f, 1f);
            filterRect.anchorMax = new Vector2(1f, 1f);
            filterRect.pivot = new Vector2(0.5f, 1f);
            filterRect.sizeDelta = new Vector2(-48f, 36f);
            filterRect.anchoredPosition = new Vector2(0f, -68f);
            HorizontalLayoutGroup hlg = filterRow.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 6f;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            _gearFilterContainer = filterRect;

            // Two-pane body region (left = equipped cards, right = inventory list).
            const float topReserved = 54f + 14f + 36f + 12f; // title + gap + filter + gap
            const float bottomReserved = 36f + 28f;          // footer + message strip
            float bodyHeight = 680f - topReserved - bottomReserved;

            // Left pane: equipped loadout cards.
            GameObject leftPane = new GameObject("Loadout Pane");
            leftPane.transform.SetParent(_gearPanel.transform, worldPositionStays: false);
            RectTransform leftRect = leftPane.AddComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0f, 1f);
            leftRect.anchorMax = new Vector2(0f, 1f);
            leftRect.pivot = new Vector2(0f, 1f);
            leftRect.sizeDelta = new Vector2(340f, bodyHeight);
            leftRect.anchoredPosition = new Vector2(24f, -topReserved);
            VerticalLayoutGroup leftVlg = leftPane.AddComponent<VerticalLayoutGroup>();
            leftVlg.spacing = 12f;
            leftVlg.childAlignment = TextAnchor.UpperLeft;
            leftVlg.childForceExpandWidth = true;
            leftVlg.childForceExpandHeight = false;
            leftVlg.childControlWidth = true;
            leftVlg.childControlHeight = true;

            _gearWeaponCardText = BuildSlotCard(leftPane.transform, "Weapon", out _gearWeaponCardIcon);
            _gearArmorCardText = BuildSlotCard(leftPane.transform, "Armor", out _gearArmorCardIcon);
            _gearAccessoryCardText = BuildSlotCard(leftPane.transform, "Accessory", out _gearAccessoryCardIcon);

            // Right pane: scrollable inventory list.
            GameObject rightPane = new GameObject("Inventory Pane");
            rightPane.transform.SetParent(_gearPanel.transform, worldPositionStays: false);
            RectTransform rightRect = rightPane.AddComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(1f, 1f);
            rightRect.anchorMax = new Vector2(1f, 1f);
            rightRect.pivot = new Vector2(1f, 1f);
            rightRect.sizeDelta = new Vector2(560f, bodyHeight);
            rightRect.anchoredPosition = new Vector2(-24f, -topReserved);
            Image rightBg = rightPane.AddComponent<Image>();
            rightBg.color = new Color(0.10f, 0.12f, 0.18f, 0.55f);

            GameObject rightInner = new GameObject("Inventory Inner");
            rightInner.transform.SetParent(rightPane.transform, worldPositionStays: false);
            RectTransform rightInnerRect = rightInner.AddComponent<RectTransform>();
            rightInnerRect.anchorMin = Vector2.zero;
            rightInnerRect.anchorMax = Vector2.one;
            rightInnerRect.offsetMin = new Vector2(12f, 12f);
            rightInnerRect.offsetMax = new Vector2(-12f, -12f);
            VerticalLayoutGroup invVlg = rightInner.AddComponent<VerticalLayoutGroup>();
            invVlg.spacing = 6f;
            invVlg.childAlignment = TextAnchor.UpperLeft;
            invVlg.childForceExpandWidth = true;
            invVlg.childForceExpandHeight = false;
            invVlg.childControlWidth = true;
            invVlg.childControlHeight = true;
            _gearInventoryContainer = rightInnerRect;

            // Last-message strip + footer text.
            GameObject msgGo = new GameObject("Message Strip");
            msgGo.transform.SetParent(_gearPanel.transform, worldPositionStays: false);
            RectTransform msgRect = msgGo.AddComponent<RectTransform>();
            msgRect.anchorMin = new Vector2(0f, 0f);
            msgRect.anchorMax = new Vector2(1f, 0f);
            msgRect.pivot = new Vector2(0.5f, 0f);
            msgRect.sizeDelta = new Vector2(-48f, 24f);
            msgRect.anchoredPosition = new Vector2(0f, 36f);
            _gearMessageText = msgGo.AddComponent<TextMeshProUGUI>();
            _gearMessageText.alignment = TextAlignmentOptions.MidlineLeft;
            _gearMessageText.fontSize = 16f;
            _gearMessageText.color = new Color(0.78f, 0.84f, 0.92f);
            _gearMessageText.richText = true;

            GameObject footerGo = new GameObject("Footer");
            footerGo.transform.SetParent(_gearPanel.transform, worldPositionStays: false);
            RectTransform footerRect = footerGo.AddComponent<RectTransform>();
            footerRect.anchorMin = new Vector2(0f, 0f);
            footerRect.anchorMax = new Vector2(1f, 0f);
            footerRect.pivot = new Vector2(0.5f, 0f);
            footerRect.sizeDelta = new Vector2(-48f, 28f);
            footerRect.anchoredPosition = new Vector2(0f, 8f);
            _gearFooterText = footerGo.AddComponent<TextMeshProUGUI>();
            _gearFooterText.alignment = TextAlignmentOptions.Center;
            _gearFooterText.fontSize = 15f;
            _gearFooterText.color = new Color(0.55f, 0.62f, 0.78f);
            _gearFooterText.richText = true;

            _gearPanel.SetActive(false);
        }

        private TMP_Text BuildSlotCard(Transform parent, string slotLabel, out Image iconImage)
        {
            GameObject card = new GameObject("Slot Card " + slotLabel);
            card.transform.SetParent(parent, worldPositionStays: false);
            Image cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0.12f, 0.16f, 0.22f, 0.92f);
            LayoutElement le = card.AddComponent<LayoutElement>();
            le.preferredHeight = 96f;
            le.minHeight = 96f;

            // Icon on the left.
            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(card.transform, worldPositionStays: false);
            RectTransform iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(72f, 72f);
            iconRect.anchoredPosition = new Vector2(12f, 0f);
            iconImage = iconGo.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.color = new Color(1f, 1f, 1f, 0f); // hidden until populated

            // Text on the right of the icon.
            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(card.transform, worldPositionStays: false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(96f, 6f);
            textRect.offsetMax = new Vector2(-12f, -6f);
            TMP_Text tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.fontSize = 17f;
            tmp.color = new Color(0.94f, 0.95f, 1f);
            tmp.richText = true;
            tmp.enableWordWrapping = true;
            tmp.text = "<b>" + slotLabel + "</b>\n<color=#7a8398>(empty)</color>";
            return tmp;
        }

        public void RenderGearInventory(GearInventoryRenderModel model)
        {
            HideMap();
            HideMovementControls();
            HideActionBar();
            HideMenu();
            if (_battlePanel != null && _battlePanel.activeSelf) HideBattlePanel();
            ClearGearChildren();
            _headerText.text = string.Empty;
            _bodyText.text = string.Empty;
            _messageText.text = string.Empty;

            _gearPanel.SetActive(true);
            _gearTitleText.text = "Gear Loadout — <color=#9bbcff>" + Strip(model.ClassName) + "</color>";

            BuildFilterTabs(model.Filter);
            PopulateSlotCard(_gearWeaponCardText, _gearWeaponCardIcon, model.WeaponSlot, TileVisualKind.WeaponIcon);
            PopulateSlotCard(_gearArmorCardText, _gearArmorCardIcon, model.ArmorSlot, TileVisualKind.ArmorIcon);
            PopulateSlotCard(_gearAccessoryCardText, _gearAccessoryCardIcon, model.AccessorySlot, TileVisualKind.AccessoryIcon);
            PopulateInventoryRows(model);

            _gearMessageText.text = string.IsNullOrWhiteSpace(model.LastMessage) ? string.Empty : Strip(model.LastMessage);
            _gearFooterText.text = Strip(model.Controls);
        }

        private void BuildFilterTabs(GearFilter active)
        {
            BuildFilterTab("All", GearFilter.All, active);
            BuildFilterTab("Weapons", GearFilter.Weapon, active);
            BuildFilterTab("Armor", GearFilter.Armor, active);
            BuildFilterTab("Accessories", GearFilter.Accessory, active);
        }

        private void BuildFilterTab(string label, GearFilter filter, GearFilter active)
        {
            bool isActive = filter == active;
            GameObject go = new GameObject("Tab " + label);
            go.transform.SetParent(_gearFilterContainer, worldPositionStays: false);
            Image bg = go.AddComponent<Image>();
            bg.color = isActive ? new Color(0.30f, 0.50f, 0.85f, 0.95f) : new Color(0.14f, 0.18f, 0.26f, 0.85f);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => _session?.Tick(PlayerAction.FilterCycle));

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 110f;
            le.minWidth = 90f;

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, worldPositionStays: false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);
            TMP_Text tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Midline;
            tmp.fontSize = 15f;
            tmp.fontStyle = isActive ? FontStyles.Bold : FontStyles.Normal;
            tmp.color = isActive ? Color.white : new Color(0.70f, 0.76f, 0.88f);

            _gearFilterButtons.Add(go);
        }

        private void PopulateSlotCard(TMP_Text textTarget, Image iconTarget, GearSlotView slot, TileVisualKind iconKind)
        {
            if (slot.Equipped is null)
            {
                textTarget.text = "<b>" + slot.SlotLabel + "</b>\n<color=#7a8398>(empty)</color>";
                iconTarget.color = new Color(1f, 1f, 1f, 0f);
                return;
            }

            GearInventoryEntry entry = slot.Equipped;
            Color tierColor = GearTierColor(entry.Tier);
            iconTarget.sprite = _sprites.GetSprite(iconKind);
            iconTarget.color = new Color(tierColor.r, tierColor.g, tierColor.b, 1f);

            StringBuilder sb = new StringBuilder();
            sb.Append("<b>").Append(slot.SlotLabel).Append("</b>\n");
            sb.Append("<color=#")
                .Append(ColorToHex(tierColor))
                .Append("><b>")
                .Append(entry.Name)
                .Append("</b></color>  <size=12><color=#7a8398>")
                .Append(entry.Tier)
                .Append("</color></size>\n");
            sb.Append("<size=13>").Append(FormatStatList(entry.Stats)).Append("</size>");
            if (entry.GrantedSkillIds.Count > 0)
            {
                sb.Append("\n<size=12><color=#a2bfff>grants ")
                    .Append(string.Join(", ", entry.GrantedSkillIds))
                    .Append("</color></size>");
            }
            textTarget.text = sb.ToString();
        }

        private void PopulateInventoryRows(GearInventoryRenderModel model)
        {
            if (model.Entries.Count == 0)
            {
                GameObject empty = new GameObject("Empty");
                empty.transform.SetParent(_gearInventoryContainer, worldPositionStays: false);
                LayoutElement le = empty.AddComponent<LayoutElement>();
                le.preferredHeight = 60f;
                TMP_Text emptyText = empty.AddComponent<TextMeshProUGUI>();
                emptyText.text = model.Filter == GearFilter.All
                    ? "<i>No gear yet — find drops or boss rewards.</i>"
                    : "<i>No items match this filter. Press Tab to switch.</i>";
                emptyText.color = new Color(0.65f, 0.70f, 0.80f);
                emptyText.alignment = TextAlignmentOptions.Midline;
                emptyText.fontSize = 16f;
                _gearInventoryRows.Add(empty);
                return;
            }

            string lastSlot = null;
            foreach (GearInventoryEntry entry in model.Entries)
            {
                if (lastSlot != entry.SlotId)
                {
                    AddInventorySectionHeader(entry.SlotLabel);
                    lastSlot = entry.SlotId;
                }
                AddInventoryRow(entry);
            }
        }

        private void AddInventorySectionHeader(string label)
        {
            GameObject header = new GameObject("Section " + label);
            header.transform.SetParent(_gearInventoryContainer, worldPositionStays: false);
            LayoutElement le = header.AddComponent<LayoutElement>();
            le.preferredHeight = 22f;
            le.minHeight = 22f;
            TMP_Text tmp = header.AddComponent<TextMeshProUGUI>();
            tmp.text = "<size=13><color=#7a90b8><b>── " + label + " ──</b></color></size>";
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.fontSize = 13f;
            tmp.richText = true;
            _gearInventoryRows.Add(header);
        }

        private void AddInventoryRow(GearInventoryEntry entry)
        {
            GameObject row = new GameObject("Row " + entry.ItemId);
            row.transform.SetParent(_gearInventoryContainer, worldPositionStays: false);
            Image bg = row.AddComponent<Image>();
            bg.color = entry.IsEquipped
                ? new Color(0.16f, 0.28f, 0.20f, 0.92f)
                : new Color(0.16f, 0.22f, 0.32f, 0.88f);

            Button btn = row.AddComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.42f, 0.62f, 0.92f);
            colors.pressedColor = new Color(0.30f, 0.50f, 0.80f);
            btn.colors = colors;

            // Digit hotkey -> PlayerAction.DigitN (1-6).
            PlayerAction action = entry.HotkeyIndex switch
            {
                1 => PlayerAction.Digit1,
                2 => PlayerAction.Digit2,
                3 => PlayerAction.Digit3,
                4 => PlayerAction.Digit4,
                5 => PlayerAction.Digit5,
                6 => PlayerAction.Digit6,
                _ => PlayerAction.None
            };
            if (action != PlayerAction.None)
            {
                PlayerAction captured = action;
                btn.onClick.AddListener(() => OnMenuButtonClicked(captured));
            }
            else
            {
                btn.interactable = false;
            }

            LayoutElement le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 56f;
            le.minHeight = 56f;

            // Icon on the left.
            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(row.transform, worldPositionStays: false);
            RectTransform iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(40f, 40f);
            iconRect.anchoredPosition = new Vector2(10f, 0f);
            Image icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            TileVisualKind iconKind = entry.SlotId switch
            {
                _ when string.Equals(entry.SlotId, "weapon", System.StringComparison.OrdinalIgnoreCase) => TileVisualKind.WeaponIcon,
                _ when string.Equals(entry.SlotId, "armor", System.StringComparison.OrdinalIgnoreCase) => TileVisualKind.ArmorIcon,
                _ => TileVisualKind.AccessoryIcon
            };
            icon.sprite = _sprites.GetSprite(iconKind);
            Color tierColor = GearTierColor(entry.Tier);
            icon.color = tierColor;

            // Body text on the right of the icon (rich text — name, tier, stats, deltas).
            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(row.transform, worldPositionStays: false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(58f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);
            TMP_Text tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.fontSize = 16f;
            tmp.color = new Color(0.96f, 0.96f, 1f);
            tmp.richText = true;

            StringBuilder sb = new StringBuilder();
            string hotkeyChip = entry.HotkeyIndex > 0
                ? "<color=#a2bfff>[" + entry.HotkeyIndex + "]</color>  "
                : "";
            string equippedBadge = entry.IsEquipped
                ? "  <color=#7aff7a><b>EQUIPPED</b></color>"
                : (entry.Quantity > 1 ? "  <color=#9aa8c4>×" + entry.Quantity + "</color>" : "");
            sb.Append(hotkeyChip)
                .Append("<color=#")
                .Append(ColorToHex(tierColor))
                .Append("><b>")
                .Append(entry.Name)
                .Append("</b></color>  <size=11><color=#7a8398>")
                .Append(entry.Tier)
                .Append("</color></size>")
                .Append(equippedBadge)
                .AppendLine();
            sb.Append("<size=13>").Append(FormatStatList(entry.Stats));
            if (entry.Deltas.Count > 0)
            {
                sb.Append("    <color=#9aa8c4>vs equipped:</color> ").Append(FormatDeltaList(entry.Deltas));
            }
            sb.Append("</size>");
            tmp.text = sb.ToString();

            _gearInventoryRows.Add(row);
        }

        private static string FormatStatList(IReadOnlyList<GearStatLine> stats)
        {
            if (stats.Count == 0) return "<color=#7a8398>(no stats)</color>";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < stats.Count; i++)
            {
                if (i > 0) sb.Append("  ");
                int v = stats[i].Value;
                string sign = v > 0 ? "+" : "";
                sb.Append("<color=#dcdfe8>").Append(stats[i].Label).Append(" ").Append(sign).Append(v).Append("</color>");
            }
            return sb.ToString();
        }

        private static string FormatDeltaList(IReadOnlyList<GearStatDelta> deltas)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < deltas.Count; i++)
            {
                if (i > 0) sb.Append("  ");
                int v = deltas[i].Delta;
                string color = v > 0 ? "#7aff7a" : "#ff8a7a";
                string arrow = v > 0 ? "▲" : "▼";
                string sign = v > 0 ? "+" : "";
                sb.Append("<color=").Append(color).Append(">")
                    .Append(deltas[i].Label).Append(" ").Append(sign).Append(v).Append(arrow)
                    .Append("</color>");
            }
            return sb.ToString();
        }

        private static Color GearTierColor(GearTier tier) => tier switch
        {
            GearTier.Common => new Color(0.82f, 0.86f, 0.92f),
            GearTier.Uncommon => new Color(0.55f, 0.92f, 0.55f),
            GearTier.Rare => new Color(0.40f, 0.66f, 0.98f),
            GearTier.Epic => new Color(0.86f, 0.55f, 0.98f),
            _ => Color.white
        };

        private static string ColorToHex(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            return r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        private void ClearGearChildren()
        {
            foreach (GameObject go in _gearFilterButtons) if (go != null) Destroy(go);
            _gearFilterButtons.Clear();
            foreach (GameObject go in _gearInventoryRows) if (go != null) Destroy(go);
            _gearInventoryRows.Clear();
        }

        private void HideGearPanel()
        {
            ClearGearChildren();
            if (_gearPanel != null) _gearPanel.SetActive(false);
        }

        // ---- On-screen movement & action controls (mobile-friendly) --------------------

        private void BuildDpad()
        {
            _dpadPanel = new GameObject("D-Pad");
            _dpadPanel.transform.SetParent(_canvas.transform, worldPositionStays: false);
            RectTransform panelRect = _dpadPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.sizeDelta = new Vector2(280f, 280f);
            panelRect.anchoredPosition = new Vector2(-32f, 120f);

            // Up / Left / Right / Down arranged in a plus.
            // Use WASD letter labels rather than Unicode arrow glyphs (▲ ◀ ▶ ▼). Unity's
            // default LiberationSans SDF font only includes ASCII; the arrows render as
            // placeholder squares and spam the console with "character not found" warnings.
            // WASD doubles as a hint about which keys do the same thing.
            AddDpadButton(panelRect, "W", PlayerAction.MoveNorth, new Vector2(0.5f, 1f), new Vector2(0f, -16f));
            AddDpadButton(panelRect, "A", PlayerAction.MoveWest, new Vector2(0f, 0.5f), new Vector2(16f, 0f));
            AddDpadButton(panelRect, "D", PlayerAction.MoveEast, new Vector2(1f, 0.5f), new Vector2(-16f, 0f));
            AddDpadButton(panelRect, "S", PlayerAction.MoveSouth, new Vector2(0.5f, 0f), new Vector2(0f, 16f));

            _dpadPanel.SetActive(false);
        }

        private void AddDpadButton(RectTransform parent, string label, PlayerAction action, Vector2 anchor, Vector2 offset)
        {
            GameObject go = new GameObject("D-Pad " + label);
            go.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(88f, 88f);
            rect.anchoredPosition = offset;

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.20f, 0.30f, 0.45f, 0.88f);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 0.75f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.6f);
            btn.colors = colors;

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, worldPositionStays: false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 44f;
            text.color = Color.white;

            // Hold-to-continue: D-pad presses behave like keyboard movement —
            // a tap fires once, a hold glides the hero continuously between
            // cells. We use EventTrigger for PointerDown/Up/Exit instead of
            // Button.onClick (which only fires on release). PointerExit
            // releases the press when the user drags a finger off the button,
            // so a stray slide doesn't strand the hero auto-walking.
            PlayerAction capturedAction = action;
            EventTrigger trigger = go.AddComponent<EventTrigger>();
            AddDpadTrigger(trigger, EventTriggerType.PointerDown, _ => OnDpadDown(capturedAction));
            AddDpadTrigger(trigger, EventTriggerType.PointerUp, _ => OnDpadUp(capturedAction));
            AddDpadTrigger(trigger, EventTriggerType.PointerExit, _ => OnDpadUp(capturedAction));
        }

        private static void AddDpadTrigger(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> handler)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(data => handler(data));
            trigger.triggers.Add(entry);
        }

        // Currently-held D-pad direction (or PlayerAction.None when no D-pad
        // button is being pressed). Polled by Update() the same way
        // PollHeldMovement reads keyboard hold state.
        private PlayerAction _heldDpadAction = PlayerAction.None;

        private void OnDpadDown(PlayerAction action)
        {
            _heldDpadAction = action;
            // Fire immediately on press so taps feel responsive — mirrors how
            // GetKeyDown fires on the press frame.
            if (_session != null)
            {
                _session.Tick(action);
            }
        }

        private void OnDpadUp(PlayerAction action)
        {
            if (_heldDpadAction == action)
            {
                _heldDpadAction = PlayerAction.None;
            }
        }

        private void BuildActionBar()
        {
            _actionBar = new GameObject("Action Bar");
            _actionBar.transform.SetParent(_canvas.transform, worldPositionStays: false);
            RectTransform panelRect = _actionBar.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.sizeDelta = new Vector2(360f, 440f);
            panelRect.anchoredPosition = new Vector2(24f, 120f);

            GameObject container = new GameObject("Buttons");
            container.transform.SetParent(_actionBar.transform, worldPositionStays: false);
            _actionBarContainer = container.AddComponent<RectTransform>();
            _actionBarContainer.anchorMin = Vector2.zero;
            _actionBarContainer.anchorMax = Vector2.one;
            _actionBarContainer.offsetMin = Vector2.zero;
            _actionBarContainer.offsetMax = Vector2.zero;
            VerticalLayoutGroup vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.LowerLeft;
            vlg.spacing = 6f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            _actionBar.SetActive(false);
        }

        private void AddActionBarButton(string label, PlayerAction action, bool enabled = true)
        {
            GameObject go = new GameObject("ActionBar " + label);
            go.transform.SetParent(_actionBarContainer, worldPositionStays: false);
            Image bg = go.AddComponent<Image>();
            bg.color = enabled
                ? new Color(0.15f, 0.20f, 0.30f, 0.92f)
                : new Color(0.15f, 0.15f, 0.18f, 0.85f);
            Button btn = go.AddComponent<Button>();
            btn.interactable = enabled;
            btn.targetGraphic = bg;

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 50f;
            le.minHeight = 50f;

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, worldPositionStays: false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 4f);
            textRect.offsetMax = new Vector2(-14f, -4f);
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.fontSize = 20f;
            tmp.color = Color.white;

            if (enabled)
            {
                PlayerAction capturedAction = action;
                btn.onClick.AddListener(() => OnMenuButtonClicked(capturedAction));
            }

            _actionBarButtons.Add(go);
        }

        private void ClearActionBar()
        {
            foreach (GameObject go in _actionBarButtons)
            {
                if (go != null) Destroy(go);
            }
            _actionBarButtons.Clear();
        }

        private void ShowMovementControls()
        {
            if (_dpadPanel != null) _dpadPanel.SetActive(true);
        }

        private void HideMovementControls()
        {
            if (_dpadPanel != null) _dpadPanel.SetActive(false);
        }

        private void ShowActionBar()
        {
            if (_actionBar != null) _actionBar.SetActive(true);
        }

        private void HideActionBar()
        {
            ClearActionBar();
            if (_actionBar != null) _actionBar.SetActive(false);
        }

        // Track which set of action-bar buttons is currently mounted so per-tick re-renders
        // don't tear down and rebuild identical buttons every frame (would flicker during
        // battle AI loops).
        private string _currentActionBarSet;

        private void EnsureActionBarSet(string id, System.Action populate)
        {
            if (_currentActionBarSet == id && _actionBar != null && _actionBar.activeSelf)
            {
                return;
            }
            ClearActionBar();
            populate();
            _currentActionBarSet = id;
            ShowActionBar();
        }

        private void PopulateMapActionBar(MapRenderModel model)
        {
            bool isTown = !string.IsNullOrEmpty(model.Title)
                && model.Title.IndexOf("Town", System.StringComparison.OrdinalIgnoreCase) >= 0;
            string id = isTown ? "town" : "dungeon";
            EnsureActionBarSet(id, () =>
            {
                if (isTown)
                {
                    AddActionBarButton("[E]  Interact", PlayerAction.Interact);
                    AddActionBarButton("[J]  Journal", PlayerAction.OpenJournal);
                    AddActionBarButton("[I]  Gear", PlayerAction.OpenGearInventory);
                    AddActionBarButton("[B]  Buy potion", PlayerAction.BuyPotion);
                    AddActionBarButton("[S]  Sell herb", PlayerAction.SellHerb);
                    AddActionBarButton("[M]  Main menu", PlayerAction.OpenMenu);
                }
                else
                {
                    AddActionBarButton("[E]  Use stairs", PlayerAction.UseStairs);
                    AddActionBarButton("[J]  Journal", PlayerAction.OpenJournal);
                    AddActionBarButton("[I]  Gear", PlayerAction.OpenGearInventory);
                    AddActionBarButton("[T]  Town portal", PlayerAction.TownPortal);
                    AddActionBarButton("[M]  Main menu", PlayerAction.OpenMenu);
                }
            });
        }

        private void PopulateBattleActionBar()
        {
            EnsureActionBarSet("battle", () =>
            {
                AddActionBarButton("[A]  Attack", PlayerAction.Attack);
                AddActionBarButton("[1]  Class skill 1", PlayerAction.ClassSkill1);
                AddActionBarButton("[2]  Class skill 2", PlayerAction.ClassSkill2);
                AddActionBarButton("[P]  Drink potion", PlayerAction.UsePotion);
                AddActionBarButton("[Q]  Retreat", PlayerAction.Retreat);
            });
        }

        private void BuildHud()
        {
            GameObject canvasGo = new GameObject("Roguelike Canvas");
            canvasGo.transform.SetParent(transform, worldPositionStays: false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Header strip at the top edge.
            _headerText = CreateHudText(canvasGo.transform, "HUD Header",
                anchorMin: new Vector2(0f, 1f),
                anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 1f),
                offsetMin: new Vector2(24f, -80f),
                offsetMax: new Vector2(-24f, -16f),
                alignment: TextAlignmentOptions.Center,
                fontSize: 36f);

            // Body: right-edge sidebar above the on-screen D-pad. Bottom edge raised to 440
            // so it sits above the D-pad (top at y=400) with breathing room.
            _bodyText = CreateHudText(canvasGo.transform, "HUD Body",
                anchorMin: new Vector2(1f, 0f),
                anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(1f, 0.5f),
                offsetMin: new Vector2(-500f, 440f),
                offsetMax: new Vector2(-24f, -100f),
                alignment: TextAlignmentOptions.TopLeft,
                fontSize: 22f);

            _footerText = CreateHudText(canvasGo.transform, "HUD Footer",
                anchorMin: new Vector2(0f, 0f),
                anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(0.5f, 0f),
                offsetMin: new Vector2(24f, 16f),
                offsetMax: new Vector2(-24f, 56f),
                alignment: TextAlignmentOptions.Center,
                fontSize: 20f);

            _messageText = CreateHudText(canvasGo.transform, "Message",
                anchorMin: new Vector2(0f, 0f),
                anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(0.5f, 0f),
                offsetMin: new Vector2(24f, 64f),
                offsetMax: new Vector2(-24f, 96f),
                alignment: TextAlignmentOptions.Center,
                fontSize: 20f);
        }

        private static TMP_Text CreateHudText(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 offsetMin,
            Vector2 offsetMax,
            TextAlignmentOptions alignment,
            float fontSize)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.richText = true;
            text.text = string.Empty;
            return text;
        }
    }
}
