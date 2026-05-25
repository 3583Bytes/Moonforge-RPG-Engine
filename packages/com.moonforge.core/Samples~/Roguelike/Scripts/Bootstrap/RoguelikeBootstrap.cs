using System.Collections.Generic;
using Moonforge.Sample.Roguelike.Input;
using Moonforge.Sample.Roguelike.Rendering;
using Moonforge.Sample.Roguelike.Scenes;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Moonforge.Sample.Roguelike
{
    /// <summary>
    /// Entry point for the Unity roguelike sample. Attach this to a single GameObject in an
    /// otherwise-empty scene and press Play — the bootstrap creates the Grid, Tilemap, Camera,
    /// and Canvas at runtime, then drives the active <see cref="ISceneController"/>.
    /// </summary>
    /// <remarks>
    /// Scene controllers are registered in <see cref="BuildSceneRegistry"/>. The sample is
    /// being ported scene-by-scene from <c>samples/Moonforge.Sample.Console</c>; until a real
    /// controller for a given <see cref="SceneId"/> ships, that scene is bound to a
    /// <see cref="PlaceholderSceneController"/>.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RoguelikeBootstrap : MonoBehaviour
    {
        [Header("Tilemap")]
        [Tooltip("World-space size of one tile. Match this to the pixels-per-unit setting on your sprites.")]
        [SerializeField] private float _cellSize = 1f;

        [Header("Camera")]
        [Tooltip("Vertical view size of the orthographic camera in world units.")]
        [SerializeField] private float _orthographicSize = 14f;

        [Tooltip("Solid colour drawn behind the tilemap when the camera doesn't fully overlap it.")]
        [SerializeField] private Color _backgroundColor = new Color(0.04f, 0.04f, 0.06f, 1f);

        [Header("Starting Scene")]
        [SerializeField] private SceneId _startingScene = SceneId.MainMenu;

        private readonly PlayerInputAdapter _input = new PlayerInputAdapter();
        private UnitySpriteCatalog _sprites;
        private SceneContext _context;
        private Dictionary<SceneId, ISceneController> _scenes;
        private ISceneController _activeScene;
        private Camera _camera;
        private Grid _grid;
        private Tilemap _tilemap;
        private Canvas _canvas;
        private TMP_Text _hudHeader;
        private TMP_Text _hudBody;
        private TMP_Text _hudFooter;
        private TMP_Text _messageText;

        private void Awake()
        {
            BuildCamera();
            BuildTilemap();
            BuildHud();

            _sprites = new UnitySpriteCatalog();
            _sprites.EnsureLoaded();

            _context = new SceneContext(
                camera: _camera,
                tilemap: _tilemap,
                sprites: _sprites,
                hudHeader: _hudHeader,
                hudBody: _hudBody,
                hudFooter: _hudFooter,
                messageText: _messageText);

            _scenes = BuildSceneRegistry();
            TransitionTo(_startingScene);
        }

        private void Update()
        {
            if (_activeScene == null)
            {
                return;
            }

            PlayerAction action = _input.PollScene(_activeScene.Id);
            SceneTransition transition = _activeScene.Tick(action, _context);
            if (transition.StayInScene)
            {
                return;
            }

            if (transition.NextScene == SceneId.Exit)
            {
                QuitGame();
                return;
            }

            TransitionTo(transition.NextScene);
        }

        private void TransitionTo(SceneId next)
        {
            if (_activeScene != null)
            {
                _activeScene.Exit(_context);
            }

            if (!_scenes.TryGetValue(next, out ISceneController controller))
            {
                controller = new PlaceholderSceneController(next, phaseLabel: "unknown");
                _scenes[next] = controller;
            }

            _activeScene = controller;
            _activeScene.Enter(_context);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static Dictionary<SceneId, ISceneController> BuildSceneRegistry()
        {
            // Each SceneId starts bound to a PlaceholderSceneController; replace these as
            // the per-scene ports ship in Phases 2–4.
            Dictionary<SceneId, ISceneController> map = new Dictionary<SceneId, ISceneController>();
            map[SceneId.MainMenu] = new PlaceholderSceneController(SceneId.MainMenu, "Phase 3");
            map[SceneId.ClassSelect] = new PlaceholderSceneController(SceneId.ClassSelect, "Phase 3");
            map[SceneId.Town] = new PlaceholderSceneController(SceneId.Town, "Phase 2");
            map[SceneId.Dungeon] = new PlaceholderSceneController(SceneId.Dungeon, "Phase 2");
            map[SceneId.Battle] = new PlaceholderSceneController(SceneId.Battle, "Phase 2");
            map[SceneId.BattleSummary] = new PlaceholderSceneController(SceneId.BattleSummary, "Phase 4");
            map[SceneId.ContractNotice] = new PlaceholderSceneController(SceneId.ContractNotice, "Phase 4");
            map[SceneId.ContractJournal] = new PlaceholderSceneController(SceneId.ContractJournal, "Phase 3");
            map[SceneId.GearInventory] = new PlaceholderSceneController(SceneId.GearInventory, "Phase 3");
            map[SceneId.MetaShrine] = new PlaceholderSceneController(SceneId.MetaShrine, "Phase 3");
            map[SceneId.BossReward] = new PlaceholderSceneController(SceneId.BossReward, "Phase 4");
            map[SceneId.Dialogue] = new PlaceholderSceneController(SceneId.Dialogue, "Phase 4");
            return map;
        }

        private void BuildCamera()
        {
            GameObject cameraGo = new GameObject("Roguelike Camera");
            cameraGo.transform.SetParent(transform, worldPositionStays: false);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            _camera = cameraGo.AddComponent<Camera>();
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
            _grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            GameObject tilemapGo = new GameObject("World Tilemap");
            tilemapGo.transform.SetParent(gridGo.transform, worldPositionStays: false);
            _tilemap = tilemapGo.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapGo.AddComponent<TilemapRenderer>();
            renderer.sortOrder = TilemapRenderer.SortOrder.TopRight;
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

            _hudHeader = CreateHudText(canvasGo.transform, "HUD Header",
                anchorMin: new Vector2(0f, 1f),
                anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 1f),
                offsetMin: new Vector2(24f, -64f),
                offsetMax: new Vector2(-24f, -16f),
                alignment: TextAlignmentOptions.Center,
                fontSize: 32f);

            _hudBody = CreateHudText(canvasGo.transform, "HUD Body",
                anchorMin: new Vector2(0f, 0f),
                anchorMax: new Vector2(0f, 1f),
                pivot: new Vector2(0f, 1f),
                offsetMin: new Vector2(24f, 96f),
                offsetMax: new Vector2(424f, -96f),
                alignment: TextAlignmentOptions.TopLeft,
                fontSize: 22f);

            _hudFooter = CreateHudText(canvasGo.transform, "HUD Footer",
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
