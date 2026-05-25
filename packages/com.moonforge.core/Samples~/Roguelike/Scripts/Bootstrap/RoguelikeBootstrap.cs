using System.Collections.Generic;
using System.Text;
using Moonforge.Core.Exploration;
using Moonforge.Sample.Roguelike.Input;
using Moonforge.Sample.Roguelike.Rendering;
using Moonforge.Sample.Roguelike.Session;
using TMPro;
using UnityEngine;
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

        private void Awake()
        {
            BuildCamera();
            BuildTilemap();
            BuildHeroSprite();
            BuildHud();
            _sprites.EnsureLoaded();
            _session = new RoguelikeSession(this);
            _session.Enter();
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
            HideMap();
            _headerText.text = "Moonforge — Roguelike";
            StringBuilder body = new StringBuilder();
            body.AppendLine(Strip(subtitle));
            body.AppendLine();
            body.AppendLine("[N] New run");
            if (canContinue)
            {
                body.AppendLine("[C] Continue saved run");
                body.AppendLine("[D] Delete saved run");
            }
            body.Append("[Q] Quit");
            _bodyText.text = body.ToString();
            _footerText.text = string.Empty;
            _messageText.text = string.Empty;
        }

        public void RenderClassSelection(IReadOnlyList<ClassSelectionOption> options, string controls)
        {
            HideMap();
            _headerText.text = "Choose your class";
            StringBuilder body = new StringBuilder();
            for (int i = 0; i < options.Count; i++)
            {
                ClassSelectionOption opt = options[i];
                body.Append('[').Append(opt.Hotkey).Append("] ").AppendLine(opt.Name);
                body.Append("   ").AppendLine(opt.Summary);
                body.AppendLine();
            }
            _bodyText.text = body.ToString();
            _footerText.text = controls;
            _messageText.text = string.Empty;
        }

        public void RenderMap(MapRenderModel model)
        {
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
            HideMap();
            _headerText.text = Strip(model.Outcome);
            StringBuilder body = new StringBuilder();
            body.Append("Encounter: ").AppendLine(model.EncounterTitle);
            body.AppendLine();
            body.Append("Gold ").Append(model.GoldBefore).Append(" → ").Append(model.GoldAfter)
                .Append("  (").Append(model.GoldDelta >= 0 ? "+" : "").Append(model.GoldDelta).AppendLine(")");
            body.Append("Tokens ").Append(model.TokensBefore).Append(" → ").Append(model.TokensAfter)
                .Append("  (").Append(model.TokensDelta >= 0 ? "+" : "").Append(model.TokensDelta).AppendLine(")");
            body.Append("Potions ").Append(model.PotionsBefore).Append(" → ").Append(model.PotionsAfter)
                .Append("  (").Append(model.PotionsDelta >= 0 ? "+" : "").Append(model.PotionsDelta).AppendLine(")");
            body.Append("Herbs ").Append(model.HerbsBefore).Append(" → ").Append(model.HerbsAfter)
                .Append("  (").Append(model.HerbsDelta >= 0 ? "+" : "").Append(model.HerbsDelta).AppendLine(")");
            if (model.BossRewardOptions != null && model.BossRewardOptions.Count > 0)
            {
                body.AppendLine();
                body.AppendLine("Boss reward:");
                foreach (string opt in model.BossRewardOptions)
                {
                    body.Append("  ").AppendLine(opt);
                }
                if (!string.IsNullOrWhiteSpace(model.BossRewardChosen))
                {
                    body.AppendLine();
                    body.Append("Chosen: ").AppendLine(Strip(model.BossRewardChosen));
                }
            }
            _bodyText.text = body.ToString();
            _footerText.text = Strip(model.Controls);
            _messageText.text = string.Empty;
        }

        public void RenderDialogue(DialogueRenderModel model)
        {
            HideMap();
            _headerText.text = model.NpcName;
            StringBuilder body = new StringBuilder();
            body.AppendLine(Strip(model.BodyText));
            body.AppendLine();
            if (model.Choices != null && model.Choices.Count > 0)
            {
                foreach (DialogueChoiceView choice in model.Choices)
                {
                    body.Append('[').Append(choice.Hotkey).Append("] ").AppendLine(Strip(choice.Text));
                }
            }
            _bodyText.text = body.ToString();
            _footerText.text = model.Controls;
            _messageText.text = string.Empty;
        }

        public void RenderContractNotice(string title, string body, string controls)
        {
            HideMap();
            _headerText.text = title;
            _bodyText.text = Strip(body);
            _footerText.text = controls;
            _messageText.text = string.Empty;
        }

        public void RenderContractJournal(string title, IReadOnlyList<string> lines, string controls)
        {
            HideMap();
            _headerText.text = title;
            StringBuilder body = new StringBuilder();
            foreach (string line in lines)
            {
                body.AppendLine(Strip(line));
            }
            _bodyText.text = body.ToString();
            _footerText.text = controls;
            _messageText.text = string.Empty;
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
