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

        [Header("Tilemap")]
        [SerializeField] private float _cellSize = 1f;

        private readonly PlayerInputAdapter _input = new PlayerInputAdapter();
        private readonly UnitySpriteCatalog _sprites = new UnitySpriteCatalog();
        private readonly List<GameObject> _markerSprites = new List<GameObject>();
        private readonly List<GameObject> _menuButtons = new List<GameObject>();
        private RoguelikeSession _session;
        private Camera _camera;
        private Grid _grid;
        private Tilemap _tilemap;
        private GameObject _heroSpriteGo;
        private SpriteRenderer _heroSpriteRenderer;
        private Canvas _canvas;
        private TMP_Text _headerText;
        private TMP_Text _bodyText;
        private TMP_Text _footerText;
        private TMP_Text _messageText;
        private GameObject _menuPanel;
        private TMP_Text _menuTitle;
        private RectTransform _menuButtonContainer;

        private void Awake()
        {
            EnsureEventSystem();
            BuildCamera();
            BuildTilemap();
            BuildHeroSprite();
            BuildHud();
            BuildMenuPanel();
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
                return;
            }

            PlayerAction action = _input.PollScene(_session.CurrentScene);
            if (action != PlayerAction.None)
            {
                _session.Tick(action);
            }
        }

        // ---- IRoguelikeHost ------------------------------------------------------------

        public void RenderMainMenu(string subtitle, bool canContinue)
        {
            BeginMenu("Moonforge — Roguelike");
            _headerText.text = Strip(subtitle);
            AddMenuButton("[N]  New run", PlayerAction.NewRun);
            AddMenuButton("[C]  Continue saved run", PlayerAction.ContinueRun, enabled: canContinue);
            AddMenuButton("[D]  Delete saved run", PlayerAction.DeleteSave, enabled: canContinue);
            AddMenuButton("[Q]  Quit", PlayerAction.Quit);
            EndMenu(controlsHint: null);
        }

        public void RenderClassSelection(IReadOnlyList<ClassSelectionOption> options, string controls)
        {
            BeginMenu("Choose your class");
            _headerText.text = string.Empty;
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
                AddMenuButton("[" + opt.Hotkey + "]  " + opt.Name + "  —  <size=70%>" + opt.Summary + "</size>", action);
            }
            AddMenuButton("[Esc]  Back to menu", PlayerAction.Cancel);
            EndMenu(controls);
        }

        public void RenderMap(MapRenderModel model)
        {
            HideMenu();
            ShowMap();
            PaintMapTiles(model);
            PaintHero(model.HeroPosition);
            PaintMarkers(model.Markers);
            FollowCamera(model.HeroPosition);

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
            _bodyText.text = body.ToString();
            _footerText.text = Strip(model.Controls);
            _messageText.text = Strip(model.LastMessage);
        }

        public void RenderBattle(BattleRenderModel model)
        {
            HideMenu();
            HideMap();
            _headerText.text = Strip(model.Title);
            StringBuilder body = new StringBuilder();
            body.Append("Turn: ").AppendLine(model.CurrentTurnActorId ?? "?");
            body.AppendLine();
            body.AppendLine(model.ClassActionInfo);
            body.AppendLine();
            if (model.RecentLog != null && model.RecentLog.Count > 0)
            {
                body.AppendLine("Recent:");
                foreach (BattleLogEntry entry in model.RecentLog)
                {
                    body.Append("  ").AppendLine(Strip(entry.Text));
                }
            }
            _bodyText.text = body.ToString();
            _footerText.text = Strip(model.Controls);
            _messageText.text = Strip(model.LastMessage);
        }

        public void RenderBattleSummary(BattleSummaryRenderModel model)
        {
            BeginMenu(Strip(model.Outcome));
            StringBuilder header = new StringBuilder();
            header.Append(model.EncounterTitle).AppendLine();
            header.Append("Gold ").Append(model.GoldBefore).Append(" → ").Append(model.GoldAfter)
                .Append("  (").Append(model.GoldDelta >= 0 ? "+" : "").Append(model.GoldDelta).Append(")  •  ");
            header.Append("Tokens ").Append(model.TokensBefore).Append(" → ").Append(model.TokensAfter)
                .Append("  (").Append(model.TokensDelta >= 0 ? "+" : "").Append(model.TokensDelta).AppendLine(")");
            header.Append("Potions ").Append(model.PotionsBefore).Append(" → ").Append(model.PotionsAfter)
                .Append("  (").Append(model.PotionsDelta >= 0 ? "+" : "").Append(model.PotionsDelta).Append(")  •  ");
            header.Append("Herbs ").Append(model.HerbsBefore).Append(" → ").Append(model.HerbsAfter)
                .Append("  (").Append(model.HerbsDelta >= 0 ? "+" : "").Append(model.HerbsDelta).Append(")");
            _headerText.text = header.ToString();

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
                    AddMenuButton("<size=80%>Chosen: " + Strip(model.BossRewardChosen) + "</size>", PlayerAction.None, enabled: false);
                }
                AddMenuButton("[Enter]  Continue", PlayerAction.Confirm);
            }
            EndMenu(Strip(model.Controls));
        }

        public void RenderDialogue(DialogueRenderModel model)
        {
            BeginMenu(model.NpcName);
            _headerText.text = Strip(model.BodyText);
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
            _headerText.text = Strip(body);
            AddMenuButton("[Enter]  Continue", PlayerAction.Confirm);
            EndMenu(controls);
        }

        public void RenderContractJournal(string title, IReadOnlyList<string> lines, string controls)
        {
            BeginMenu(title);
            // For list-style screens (Contract Journal, Gear, Shrine, Boss Reward Chest),
            // each text line becomes a button — its leading "[<key>]" prefix doubles as
            // the click action. Any line without a parseable hotkey renders as a passive
            // info row.
            _headerText.text = string.Empty;
            StringBuilder info = new StringBuilder();
            foreach (string line in lines)
            {
                string stripped = Strip(line);
                PlayerAction action = TryParseHotkeyAction(stripped, out string hotkey);
                if (action == PlayerAction.None)
                {
                    info.AppendLine(stripped);
                }
                else
                {
                    if (info.Length > 0)
                    {
                        AddMenuButton("<size=70%>" + info.ToString().TrimEnd() + "</size>", PlayerAction.None, enabled: false);
                        info.Clear();
                    }
                    AddMenuButton(stripped, action);
                }
            }
            if (info.Length > 0)
            {
                AddMenuButton("<size=70%>" + info.ToString().TrimEnd() + "</size>", PlayerAction.None, enabled: false);
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
            ExplorationMapState map = model.Map;
            if (!map.IsConfigured)
            {
                return;
            }

            bool isTown = !string.IsNullOrEmpty(model.Title) && model.Title.IndexOf("Town", System.StringComparison.OrdinalIgnoreCase) >= 0;

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    if (!map.TryGetTileFlags(new GridPosition(x, y), out ExplorationTileFlags flags))
                    {
                        continue;
                    }

                    TileVisualKind kind = ResolveTileKind(flags, isTown);
                    TileBase tile = _sprites.GetTile(kind);
                    if (tile != null)
                    {
                        _tilemap.SetTile(GridToCell(x, y), tile);
                    }
                }
            }
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

        private void PaintHero(GridPosition? heroPosition)
        {
            if (!heroPosition.HasValue)
            {
                _heroSpriteGo.SetActive(false);
                return;
            }
            _heroSpriteGo.SetActive(true);
            _heroSpriteRenderer.sprite = _sprites.GetSprite(TileVisualKind.Hero);
            _heroSpriteGo.transform.position = GridToWorld(heroPosition.Value.X, heroPosition.Value.Y);
        }

        private void PaintMarkers(IReadOnlyList<MapMarker> markers)
        {
            ReleaseMarkerSprites();
            if (markers == null)
            {
                return;
            }
            foreach (MapMarker marker in markers)
            {
                TileVisualKind kind = ResolveMarkerKind(marker.Symbol);
                Sprite sprite = _sprites.GetSprite(kind);
                if (sprite == null)
                {
                    continue;
                }
                GameObject go = new GameObject("Marker " + marker.Label);
                go.transform.SetParent(transform, worldPositionStays: false);
                go.transform.position = GridToWorld(marker.Position.X, marker.Position.Y) + new Vector3(0f, 0f, -0.01f);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 5;
                _markerSprites.Add(go);
            }
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
        }

        private void FollowCamera(GridPosition? heroPosition)
        {
            if (!heroPosition.HasValue || _camera == null)
            {
                return;
            }
            Vector3 target = GridToWorld(heroPosition.Value.X, heroPosition.Value.Y);
            _camera.transform.position = new Vector3(target.x, target.y, -10f);
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
            if (_heroSpriteGo != null) _heroSpriteGo.SetActive(true);
        }

        private void HideMap()
        {
            if (_grid != null) _grid.gameObject.SetActive(false);
            if (_heroSpriteGo != null) _heroSpriteGo.SetActive(false);
            ReleaseMarkerSprites();
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

        private void BuildHeroSprite()
        {
            _heroSpriteGo = new GameObject("Hero Sprite");
            _heroSpriteGo.transform.SetParent(transform, worldPositionStays: false);
            _heroSpriteRenderer = _heroSpriteGo.AddComponent<SpriteRenderer>();
            _heroSpriteRenderer.sortingOrder = 10;
            _heroSpriteGo.SetActive(false);
        }

        // ---- Menu panel (click-to-tick buttons) ----------------------------------------

        private void BuildMenuPanel()
        {
            _menuPanel = new GameObject("Menu Panel");
            _menuPanel.transform.SetParent(_canvas.transform, worldPositionStays: false);
            RectTransform panelRect = _menuPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720f, 800f);
            Image bg = _menuPanel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            GameObject titleGo = new GameObject("Menu Title");
            titleGo.transform.SetParent(_menuPanel.transform, worldPositionStays: false);
            RectTransform titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(24f, -100f);
            titleRect.offsetMax = new Vector2(-24f, -16f);
            _menuTitle = titleGo.AddComponent<TextMeshProUGUI>();
            _menuTitle.alignment = TextAlignmentOptions.Center;
            _menuTitle.fontSize = 36f;
            _menuTitle.color = Color.white;
            _menuTitle.richText = true;

            GameObject containerGo = new GameObject("Buttons");
            containerGo.transform.SetParent(_menuPanel.transform, worldPositionStays: false);
            _menuButtonContainer = containerGo.AddComponent<RectTransform>();
            _menuButtonContainer.anchorMin = new Vector2(0f, 0f);
            _menuButtonContainer.anchorMax = new Vector2(1f, 1f);
            _menuButtonContainer.pivot = new Vector2(0.5f, 0.5f);
            _menuButtonContainer.offsetMin = new Vector2(48f, 48f);
            _menuButtonContainer.offsetMax = new Vector2(-48f, -110f);
            VerticalLayoutGroup vlg = containerGo.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 10f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            ContentSizeFitter csf = containerGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _menuPanel.SetActive(false);
        }

        private void BeginMenu(string title)
        {
            HideMap();
            ClearMenuButtons();
            if (_menuTitle != null) _menuTitle.text = title;
            _menuPanel.SetActive(true);
            // The body sidebar is noisy in menu mode; hide it.
            _bodyText.text = string.Empty;
        }

        private void EndMenu(string controlsHint)
        {
            _footerText.text = string.IsNullOrEmpty(controlsHint)
                ? "Use the mouse to click an option, or press the highlighted key."
                : controlsHint + "  •  click or press the highlighted key";
            _messageText.text = string.Empty;
        }

        private void AddMenuButton(string label, PlayerAction action, bool enabled = true)
        {
            GameObject go = new GameObject("Btn " + label);
            go.transform.SetParent(_menuButtonContainer, worldPositionStays: false);
            Image bg = go.AddComponent<Image>();
            bg.color = enabled
                ? new Color(0.20f, 0.25f, 0.35f, 0.95f)
                : new Color(0.15f, 0.15f, 0.18f, 0.85f);
            Button btn = go.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 0.7f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.6f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f);
            btn.colors = colors;
            btn.interactable = enabled;
            btn.targetGraphic = bg;

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 56f;
            le.minHeight = 56f;

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, worldPositionStays: false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 4f);
            textRect.offsetMax = new Vector2(-16f, -4f);
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.fontSize = 24f;
            tmp.color = Color.white;
            tmp.richText = true;

            if (enabled)
            {
                PlayerAction capturedAction = action;
                btn.onClick.AddListener(() => OnMenuButtonClicked(capturedAction));
            }

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

            // Body: right-edge sidebar so the tilemap is visible to the left of it.
            _bodyText = CreateHudText(canvasGo.transform, "HUD Body",
                anchorMin: new Vector2(1f, 0f),
                anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(1f, 0.5f),
                offsetMin: new Vector2(-500f, 120f),
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
