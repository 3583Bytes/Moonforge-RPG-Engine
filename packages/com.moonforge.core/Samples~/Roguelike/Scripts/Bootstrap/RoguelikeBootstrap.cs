using System.Collections.Generic;
using System.Text;
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
    /// painting render-model snapshots into a TextMeshPro HUD; drives the shared
    /// <see cref="RoguelikeSession"/> from <see cref="Update"/>.
    /// </summary>
    /// <remarks>
    /// This first-pass implementation renders every scene as TMP text in the body panel —
    /// the same information the console sample shows, just inside Unity's Canvas. A future
    /// step will paint the Town/Dungeon scenes onto the runtime-built Tilemap with the
    /// Kenney sprite atlas; the engine boundary (this MonoBehaviour) doesn't change when
    /// that lands — only the Render* method bodies grow.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RoguelikeBootstrap : MonoBehaviour, IRoguelikeHost
    {
        [Header("Camera")]
        [SerializeField] private float _orthographicSize = 14f;
        [SerializeField] private Color _backgroundColor = new Color(0.04f, 0.04f, 0.06f, 1f);

        [Header("Tilemap")]
        [SerializeField] private float _cellSize = 1f;

        private readonly PlayerInputAdapter _input = new PlayerInputAdapter();
        private RoguelikeSession _session;
        private Camera _camera;
        private Grid _grid;
        private Tilemap _tilemap;
        private Canvas _canvas;
        private TMP_Text _headerText;
        private TMP_Text _bodyText;
        private TMP_Text _footerText;
        private TMP_Text _messageText;

        private void Awake()
        {
            BuildCamera();
            BuildTilemap();
            BuildHud();
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
            _headerText.text = Strip(model.Title);
            StringBuilder body = new StringBuilder();
            if (model.HeroPosition.HasValue)
            {
                body.Append("Hero @ ").Append(model.HeroPosition.Value.X).Append(",").Append(model.HeroPosition.Value.Y).AppendLine();
            }
            body.Append("Gold ").Append(model.Gold)
                .Append("  Tokens ").Append(model.Tokens)
                .Append("  Potions ").Append(model.Potions)
                .Append("  Floor ").Append(model.Depth).AppendLine();
            body.AppendLine();
            if (model.Markers != null && model.Markers.Count > 0)
            {
                body.AppendLine("Landmarks:");
                foreach (MapMarker marker in model.Markers)
                {
                    body.Append("  ").Append(marker.Symbol).Append(' ').Append(marker.Label)
                        .Append(" @ ").Append(marker.Position.X).Append(",").Append(marker.Position.Y).AppendLine();
                }
                body.AppendLine();
            }
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
            _headerText.text = Strip(model.Outcome);
            StringBuilder body = new StringBuilder();
            body.Append("Encounter: ").AppendLine(model.EncounterTitle);
            body.AppendLine();
            body.Append("Gold ").Append(model.GoldBefore).Append(" -> ").Append(model.GoldAfter)
                .Append("  (").Append(model.GoldDelta >= 0 ? "+" : "").Append(model.GoldDelta).AppendLine(")");
            body.Append("Tokens ").Append(model.TokensBefore).Append(" -> ").Append(model.TokensAfter)
                .Append("  (").Append(model.TokensDelta >= 0 ? "+" : "").Append(model.TokensDelta).AppendLine(")");
            body.Append("Potions ").Append(model.PotionsBefore).Append(" -> ").Append(model.PotionsAfter)
                .Append("  (").Append(model.PotionsDelta >= 0 ? "+" : "").Append(model.PotionsDelta).AppendLine(")");
            body.Append("Herbs ").Append(model.HerbsBefore).Append(" -> ").Append(model.HerbsAfter)
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
            _headerText.text = title;
            _bodyText.text = Strip(body);
            _footerText.text = controls;
            _messageText.text = string.Empty;
        }

        public void RenderContractJournal(string title, IReadOnlyList<string> lines, string controls)
        {
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
                    // Skip until matching ']' — Spectre.Console tags like [grey], [/], [red bold]
                    int end = text.IndexOf(']', i + 1);
                    if (end > 0 && end - i < 30 && !text.Substring(i + 1, end - i - 1).Contains(' ') == false)
                    {
                        // Looks like a markup tag; skip it.
                        i = end + 1;
                        continue;
                    }
                }
                sb.Append(text[i]);
                i++;
            }
            return sb.ToString();
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

            _headerText = CreateHudText(canvasGo.transform, "HUD Header",
                anchorMin: new Vector2(0f, 1f),
                anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 1f),
                offsetMin: new Vector2(24f, -80f),
                offsetMax: new Vector2(-24f, -16f),
                alignment: TextAlignmentOptions.Center,
                fontSize: 36f);

            _bodyText = CreateHudText(canvasGo.transform, "HUD Body",
                anchorMin: new Vector2(0f, 0f),
                anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 0.5f),
                offsetMin: new Vector2(48f, 120f),
                offsetMax: new Vector2(-48f, -100f),
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
