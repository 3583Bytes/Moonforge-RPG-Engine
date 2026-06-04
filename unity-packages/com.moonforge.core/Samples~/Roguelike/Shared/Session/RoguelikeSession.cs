using System;
using Moonforge.Core;
using Moonforge.Core.Combat;
using Moonforge.Core.Combat.Commands;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Crafting.Commands;
using Moonforge.Core.Crafting.Events;
using Moonforge.Core.Dialogue;
using Moonforge.Core.Dialogue.Commands;
using Moonforge.Core.Dialogue.Events;
using Moonforge.Core.Dialogue.Queries;
using Moonforge.Core.World;
using Moonforge.Core.World.Commands;
using Moonforge.Core.World.Queries;
using Moonforge.Core.Combat.Queries;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Economy.Events;
using Moonforge.Core.Economy.Queries;
using Moonforge.Core.Equipment;
using Moonforge.Core.Equipment.Commands;
using Moonforge.Core.Equipment.Queries;
using Moonforge.Core.Exploration;
using Moonforge.Core.Exploration.Commands;
using Moonforge.Core.Exploration.Persistence;
using Moonforge.Core.Exploration.Queries;
using Moonforge.Core.Interactables;
using Moonforge.Core.Interactables.Commands;
using Moonforge.Core.Inventory.Commands;
using Moonforge.Core.Inventory.Queries;
using Moonforge.Core.Loot;
using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;
using Moonforge.Core.Progression;
using Moonforge.Core.Progression.Commands;
using Moonforge.Core.Progression.Events;
using Moonforge.Core.Quests;
using Moonforge.Core.Quests.Commands;
using Moonforge.Core.Quests.Events;
using Moonforge.Core.Quests.Queries;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;
using Moonforge.Core.Shops.Commands;
using Moonforge.Core.Stats;
using Moonforge.Core.Stats.Commands;
using Moonforge.Sample.Roguelike.Persistence;
using Moonforge.Sample.Roguelike.Rendering;
using Moonforge.Sample.Roguelike.Content;
using static Moonforge.Sample.Roguelike.Content.RoguelikeContent;
using Moonforge.Sample.Roguelike;
using Moonforge.Sample.Roguelike.WorldGen;

using Moonforge.Sample.Roguelike.Input;

using System.Collections.Generic;
using System.Linq;
namespace Moonforge.Sample.Roguelike.Session
{

    public sealed class RoguelikeSession
    {

        private readonly IRoguelikeHost _host;
        private readonly IGameDefinitionCatalog _definitions;
        private readonly RoguelikeSaveStore _saveStore;
        private readonly CommandDispatcher _dispatcher;
        private readonly GetCurrencyBalanceQueryHandler _currencyQueryHandler = new();
        private readonly GetExplorationActorPositionQueryHandler _actorPositionQueryHandler = new();
        private readonly CanMoveActorQueryHandler _canMoveActorQueryHandler = new();
        private readonly GetInventoryItemQuantityQueryHandler _inventoryQuantityQueryHandler = new();
        private readonly GetCurrentBattleTurnActorQueryHandler _turnActorQueryHandler = new();
        private readonly GetActiveBattleStatusQueryHandler _battleStatusQueryHandler = new();
        private readonly GetQuestStatusQueryHandler _questStatusQueryHandler = new();
        private readonly GetQuestObjectiveProgressQueryHandler _questObjectiveProgressQueryHandler = new();
        private readonly GetEquippedItemQueryHandler _equippedItemQueryHandler = new();
        private readonly GetAvailableDialogueChoicesQueryHandler _dialogueChoicesQueryHandler;

        private GameState _gameState = new();
        private InMemoryDomainEventSink _eventSink = new();
        private CommandContext _context;
        public SceneId CurrentScene { get; private set; } = SceneId.MainMenu;

        /// <summary>
        /// Class chosen for the current run (defaults to the first profile before a run
        /// starts). Hosts use this to pick per-class hero art.
        /// </summary>
        public PlayerClass SelectedClassId => _selectedClass.ClassId;

        /// <summary>
        /// Direction the hero last tried to move in — updated even when the step is
        /// blocked (bumping a wall still turns the character). Hosts with directional
        /// art use this to pick the facing sprite. Resets to Down on new run and load.
        /// </summary>
        public FacingDirection HeroFacing { get; private set; } = FacingDirection.Down;
        private string _lastMessage = "Press N to start a new run.";
        private int _currentDungeonFloor;
        private readonly GetWorldVariableQueryHandler _worldVariableQueryHandler = new();
        private ulong _runSeed;
        private GridPosition _townStairs = new(0, 0);
        private GridPosition _townGuard = new(0, 0);
        private GridPosition _townAlchemist = new(0, 0);
        private GridPosition _townHealer = new(0, 0);
        private GridPosition _townCache = new(0, 0);
        private GridPosition _townFountain = new(0, 0);
        private GridPosition _townQuestBoard = new(0, 0);
        private GridPosition _townShrine = new(0, 0);
        private GridPosition _dungeonUpStairs = new(0, 0);
        private GridPosition _dungeonDownStairs = new(0, 0);
        private readonly List<MapMarker> _townMarkers = new();
        private readonly List<MapMarker> _dungeonMarkers = new();
        private readonly Dictionary<GridPosition, char> _townWallDecorations = new();
        private readonly Dictionary<GridPosition, char> _townFloorDecorations = new();
        private readonly Dictionary<GridPosition, char> _dungeonWallDecorations = new();
        private readonly Dictionary<int, DungeonFloorBlueprint> _dungeonFloors = new();
        private int _battleSequence;
        private BattleStatus? _lastBattleStatus;
        private readonly Queue<BattleLogEntry> _battleLog = new();
        private MessageTone _lastMessageTone = MessageTone.Info;
        private BattleSnapshot? _activeBattleSnapshot;
        private BattleSummarySnapshot? _pendingBattleSummary;
        private SceneId _postBattleScene = SceneId.Dungeon;
        private SceneId _journalReturnScene = SceneId.Town;
        private SceneId _gearReturnScene = SceneId.Town;
        private ContractNoticeSnapshot? _pendingContractNotice;
        private SceneId _resumeSceneAfterContractNotice = SceneId.Town;
        private BossRewardSnapshot? _pendingBossReward;
        private readonly HashSet<string> _contractsReadyForTurnIn = new();
        private readonly HashSet<MetaUnlockId> _unlockedMetaUnlocks = new();
        private readonly HashSet<int> _clearedBossFloors = new();
        // When the player presses Interact on a town landmark, the session renders the
        // interaction menu via the host and stores the kind here. The next Tick is treated
        // as the menu choice (Digit1/Digit2/.../Cancel). Replaces the old blocking
        // `Console.ReadKey` flow so Unity's per-frame Tick can drive the menu too.
        private TownInteractionKind? _pendingTownInteraction;
        // Snapshot of the battle state captured when BattleEndedEvent fires (final HPs,
        // including any actor at 0). The engine nulls ActiveBattle as soon as the
        // command handler returns, so HandleBattleCompletion has to render from this
        // clone instead of from live state.
        private Moonforge.Core.Combat.BattleState? _finalBattleSnapshot;
        // Clone of the most recently rendered ActiveBattle. We keep this around so that
        // when BattleEndedEvent fires (after ActiveBattle has been nulled inside the
        // engine command handler), we can overlay the event's FinalActorHp onto a real
        // live clone to produce a renderable post-kill snapshot.
        private Moonforge.Core.Combat.BattleState? _liveBattleClone;
        private string? _activeContractQuestId;
        private string? _activeDialogueId;
        private SceneId _dialogueReturnScene = SceneId.Town;
        private string? _lastEncounterThemeId;
        private int _lastEncounterEnemyCount;
        private ClassProfile _selectedClass = ClassProfiles[0];
        private string? _pendingEncounterGearDropItemId;
        private int? _activeBossFloor;
        private bool _mainMenuDeleteConfirmPending;

        public RoguelikeSession(IRoguelikeHost host)
            : this(host, savePath: null)
        {
        }

        /// <summary>
        /// Overload for hosts/tests that want the save file somewhere other than the
        /// default per-user location.
        /// </summary>
        public RoguelikeSession(IRoguelikeHost host, string? savePath)
        {
            _host = host;
            _saveStore = new RoguelikeSaveStore(savePath);
            _definitions = RoguelikeContent.BuildCatalog();
            _dialogueChoicesQueryHandler = new GetAvailableDialogueChoicesQueryHandler(_definitions);
            _context = CreateContext(seed: 1, _eventSink);

            _dispatcher = DefaultCommandDispatcher.Create();

            LoadMetaUnlocksFromSave();
        }

        public void Run()
        {
            while (CurrentScene != SceneId.Exit)
            {
                switch (CurrentScene)
                {
                    case SceneId.MainMenu:
                        RunMainMenu();
                        break;
                    case SceneId.ClassSelect:
                        RunClassSelect();
                        break;
                    case SceneId.Town:
                        RunTown();
                        break;
                    case SceneId.Dungeon:
                        RunDungeon();
                        break;
                    case SceneId.Battle:
                        RunBattle();
                        break;
                    case SceneId.BattleSummary:
                        RunBattleSummary();
                        break;
                    case SceneId.ContractNotice:
                        RunContractNotice();
                        break;
                    case SceneId.ContractJournal:
                        RunContractJournal();
                        break;
                    case SceneId.GearInventory:
                        RunGearInventory();
                        break;
                    case SceneId.MetaShrine:
                        RunMetaShrine();
                        break;
                    case SceneId.BossReward:
                        RunBossReward();
                        break;
                    case SceneId.Dialogue:
                        RunDialogue();
                        break;
                    default:
                        CurrentScene = SceneId.Exit;
                        break;
                }
            }
        }

        /// <summary>
        /// Render the current scene through <see cref="IRoguelikeHost"/>. Call once at startup
        /// and after any external state change. Subsequent <see cref="Tick"/> calls re-render
        /// automatically on scene transitions, so explicit Enter() is only needed at boot.
        /// </summary>
        /// <remarks>
        /// Migration in progress (B2.2+): only migrated scenes call their EnterX() handler;
        /// non-migrated scenes are still driven by the Console <see cref="Run"/> loop.
        /// </remarks>
        public void Enter()
        {
            switch (CurrentScene)
            {
                case SceneId.MainMenu:
                    EnterMainMenu();
                    break;
                case SceneId.ClassSelect:
                    EnterClassSelect();
                    break;
                case SceneId.Town:
                    EnterTown();
                    break;
                case SceneId.Dungeon:
                    EnterDungeon();
                    break;
                case SceneId.Battle:
                    EnterBattle();
                    break;
                case SceneId.BattleSummary:
                    EnterBattleSummary();
                    break;
                case SceneId.ContractNotice:
                    EnterContractNotice();
                    break;
                case SceneId.ContractJournal:
                    EnterContractJournal();
                    break;
                case SceneId.GearInventory:
                    EnterGearInventory();
                    break;
                case SceneId.MetaShrine:
                    EnterMetaShrine();
                    break;
                case SceneId.BossReward:
                    EnterBossReward();
                    break;
                case SceneId.Dialogue:
                    EnterDialogue();
                    break;
                default:
                    // Not yet migrated to Enter/Tick. Console host still uses Run().
                    break;
            }
        }

        /// <summary>
        /// Apply one player input frame to the current scene's state machine. If
        /// <see cref="CurrentScene"/> changes as a result, the new scene is rendered
        /// automatically before the call returns.
        /// </summary>
        /// <remarks>
        /// Migration in progress (B2.2+): currently only <see cref="SceneId.MainMenu"/> is
        /// driven by the Enter/Tick pattern; other scenes still go through <see cref="Run"/>.
        /// </remarks>
        public void Tick(PlayerAction action)
        {
            switch (CurrentScene)
            {
                case SceneId.MainMenu:
                    TickMainMenu(action);
                    break;
                case SceneId.ClassSelect:
                    TickClassSelect(action);
                    break;
                case SceneId.Town:
                    TickTown(action);
                    break;
                case SceneId.Dungeon:
                    TickDungeon(action);
                    break;
                case SceneId.Battle:
                    TickBattle(action);
                    break;
                case SceneId.BattleSummary:
                    TickBattleSummary(action);
                    break;
                case SceneId.ContractNotice:
                    TickContractNotice(action);
                    break;
                case SceneId.ContractJournal:
                    TickContractJournal(action);
                    break;
                case SceneId.GearInventory:
                    TickGearInventory(action);
                    break;
                case SceneId.MetaShrine:
                    TickMetaShrine(action);
                    break;
                case SceneId.BossReward:
                    TickBossReward(action);
                    break;
                case SceneId.Dialogue:
                    TickDialogue(action);
                    break;
                default:
                    // Not yet migrated to Enter/Tick. Console host still uses Run().
                    break;
            }

            // Always re-render after a tick. This catches same-scene state changes (battle
            // AI moves, contract progress, etc.) that won't trigger a scene transition.
            Enter();
        }

        private void EnterMainMenu()
        {
            if (_mainMenuDeleteConfirmPending)
            {
                _host.RenderContractNotice(
                    "Delete Save",
                    "Delete existing save data?\nThis cannot be undone.",
                    "Press Y to confirm, any other key to cancel");
            }
            else
            {
                bool canContinue = TryPeekContinueRun();
                _host.RenderMainMenu("[grey]Static town + seeded dungeon + real turn battles.[/]", canContinue);
            }
        }

        private void TickMainMenu(PlayerAction action)
        {
            if (_mainMenuDeleteConfirmPending)
            {
                if (action == PlayerAction.Confirm)
                {
                    if (_saveStore.TryDelete(out string? deleteError))
                    {
                        _lastMessage = "Save deleted.";
                    }
                    else
                    {
                        _lastMessage = $"Delete failed: {deleteError}";
                    }
                }
                else
                {
                    _lastMessage = "Delete canceled.";
                }

                _mainMenuDeleteConfirmPending = false;
                return;
            }

            bool canContinue = TryPeekContinueRun();
            switch (action)
            {
                case PlayerAction.ContinueRun:
                    if (canContinue)
                    {
                        if (TryContinueSavedRun(out string? loadError))
                        {
                            _lastMessage = "Loaded previous run.";
                        }
                        else
                        {
                            _lastMessage = $"Continue failed: {loadError}";
                        }
                    }
                    break;
                case PlayerAction.DeleteSave:
                    if (canContinue)
                    {
                        _mainMenuDeleteConfirmPending = true;
                    }
                    break;
                case PlayerAction.NewRun:
                    CurrentScene = SceneId.ClassSelect;
                    break;
                case PlayerAction.Quit:
                    CurrentScene = SceneId.Exit;
                    break;
            }
        }

        private static PlayerAction MapMainMenuConsoleKey(ConsoleKeyInfo key) => key.Key switch
        {
            ConsoleKey.N => PlayerAction.NewRun,
            ConsoleKey.C => PlayerAction.ContinueRun,
            ConsoleKey.D => PlayerAction.DeleteSave,
            ConsoleKey.Y => PlayerAction.Confirm,
            ConsoleKey.Q or ConsoleKey.Escape => PlayerAction.Quit,
            _ => PlayerAction.Cancel
        };

        private void RunMainMenu()
        {
            EnterMainMenu();
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            TickMainMenu(MapMainMenuConsoleKey(key));
        }

        private void EnterClassSelect()
        {
            List<ClassSelectionOption> options = new();
            for (int i = 0; i < ClassProfiles.Length; i++)
            {
                ClassProfile profile = ClassProfiles[i];
                options.Add(new ClassSelectionOption(
                    (i + 1).ToString(),
                    profile.Name,
                    profile.Summary));
            }

            _host.RenderClassSelection(options, "Press 1/2/3 to choose, Esc to cancel");
        }

        private void TickClassSelect(PlayerAction action)
        {
            ClassProfile? selected = action switch
            {
                PlayerAction.Digit1 => ClassProfiles[0],
                PlayerAction.Digit2 => ClassProfiles[1],
                PlayerAction.Digit3 => ClassProfiles[2],
                _ => null
            };

            if (selected is null)
            {
                if (action == PlayerAction.Cancel)
                {
                    CurrentScene = SceneId.MainMenu;
                }

                return;
            }

            StartNewRun(selected);
            CurrentScene = SceneId.Town;
        }

        private static PlayerAction MapClassSelectConsoleKey(ConsoleKeyInfo key) => key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => PlayerAction.Digit1,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => PlayerAction.Digit2,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => PlayerAction.Digit3,
            ConsoleKey.Escape => PlayerAction.Cancel,
            _ => PlayerAction.None
        };

        private void RunClassSelect()
        {
            EnterClassSelect();
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            TickClassSelect(MapClassSelectConsoleKey(key));
        }

        private void EnterTown()
        {
            // If we just opened a landmark-interaction menu (via TryTownInteract), keep it
            // on screen. Tick() calls Enter() after every dispatch as a "always re-render"
            // pass; without this guard that re-render would paint the map back over the
            // menu in the same frame, so nothing would visibly happen on E.
            if (_pendingTownInteraction.HasValue)
            {
                RenderTownInteractionMenu(_pendingTownInteraction.Value);
                return;
            }

            if (TryEnterPendingContractNotice(SceneId.Town))
            {
                Enter();
                return;
            }

            _host.RenderMap(new MapRenderModel(
                Title: $"Town Hub (Seed {_runSeed})",
                Map: _gameState.ExplorationState.Map,
                HeroPosition: GetHeroPosition(),
                GuardPosition: _townGuard,
                Markers: _townMarkers,
                Gold: _currencyQueryHandler.Query(_gameState, new GetCurrencyBalanceQuery(GoldCurrencyId)),
                Tokens: _currencyQueryHandler.Query(_gameState, new GetCurrencyBalanceQuery(TokenCurrencyId)),
                Potions: _inventoryQuantityQueryHandler.Query(_gameState, new GetInventoryItemQuantityQuery(MediumPotionItemId)),
                Depth: GetDeepestFloor(),
                ContractInfo: BuildActiveContractHudText(),
                Controls: "WASD: Move | E: Interact | J: Journal | I: Gear | B: Buy potion | S: Sell herb | M: Menu",
                LastMessage: _lastMessage,
                MessageTone: _lastMessageTone,
                WallDecorations: _townWallDecorations,
                FloorDecorations: _townFloorDecorations,
                Actors: BuildActorViews()));
        }

        /// <summary>
        /// Snapshot every actor the engine knows about into typed <see cref="MapActor"/>
        /// views. Consumers (Unity bootstrap, console renderer) render sprites for each
        /// entry — so adding a blocking actor that the renderer forgot to paint is no
        /// longer possible, by construction.
        /// </summary>
        private IReadOnlyList<MapActor> BuildActorViews()
        {
            List<MapActor> actors = new();
            foreach (KeyValuePair<string, Moonforge.Core.Exploration.ExplorationActorState> entry in _gameState.ExplorationState.Actors)
            {
                MapActorKind kind = ResolveActorKind(entry.Key);
                string displayName = ResolveActorDisplayName(entry.Key);
                actors.Add(new MapActor(entry.Key, entry.Value.Position, kind, displayName));
            }
            return actors;
        }

        private static MapActorKind ResolveActorKind(string actorId)
        {
            if (actorId == HeroActorId) return MapActorKind.Hero;
            if (actorId == GuardActorId) return MapActorKind.Guard;
            // Convention used by EncounterGenerator: enemy IDs start with "enemy."
            if (actorId.StartsWith("enemy.boss", System.StringComparison.OrdinalIgnoreCase)) return MapActorKind.BossEnemy;
            if (actorId.StartsWith("enemy.elite", System.StringComparison.OrdinalIgnoreCase)) return MapActorKind.EliteEnemy;
            if (actorId.StartsWith("enemy.", System.StringComparison.OrdinalIgnoreCase)) return MapActorKind.Enemy;
            return MapActorKind.Npc;
        }

        private static string ResolveActorDisplayName(string actorId)
        {
            if (actorId == HeroActorId) return "Hero";
            if (actorId == GuardActorId) return "Guard";
            return actorId;
        }

        private void TickTown(PlayerAction action)
        {
            // A pending town-interaction menu consumes the next input as its choice
            // (Digit1/Digit2/Cancel), regardless of which scene-level binding it would
            // normally trigger.
            if (_pendingTownInteraction.HasValue)
            {
                ResolvePendingTownInteraction(action);
                return;
            }

            switch (action)
            {
                case PlayerAction.BuyPotion: TryBuyPotion(); return;
                case PlayerAction.SellHerb: TrySellHerb(); return;
                case PlayerAction.OpenJournal:
                    _journalReturnScene = SceneId.Town;
                    CurrentScene = SceneId.ContractJournal;
                    return;
                case PlayerAction.OpenGearInventory:
                    _gearReturnScene = SceneId.Town;
                    CurrentScene = SceneId.GearInventory;
                    return;
                case PlayerAction.Interact: TryTownInteract(); return;
                case PlayerAction.OpenMenu:
                case PlayerAction.Cancel:
                    CurrentScene = SceneId.MainMenu;
                    _lastMessage = "Returned to main menu.";
                    return;
                case PlayerAction.MoveNorth:
                case PlayerAction.MoveSouth:
                case PlayerAction.MoveEast:
                case PlayerAction.MoveWest:
                    HandleMovementAction(action);
                    return;
            }
        }

        private PlayerAction MapTownConsoleKey(ConsoleKeyInfo key)
        {
            // When a landmark-interaction menu is pending, the digits are the menu choices
            // and need to take precedence over the normal town bindings (e.g. 'D' = East).
            if (_pendingTownInteraction.HasValue)
            {
                return key.Key switch
                {
                    ConsoleKey.D1 or ConsoleKey.NumPad1 => PlayerAction.Digit1,
                    ConsoleKey.D2 or ConsoleKey.NumPad2 => PlayerAction.Digit2,
                    ConsoleKey.D3 or ConsoleKey.NumPad3 => PlayerAction.Digit3,
                    ConsoleKey.D4 or ConsoleKey.NumPad4 => PlayerAction.Digit4,
                    ConsoleKey.D5 or ConsoleKey.NumPad5 => PlayerAction.Digit5,
                    ConsoleKey.D6 or ConsoleKey.NumPad6 => PlayerAction.Digit6,
                    ConsoleKey.Escape => PlayerAction.Cancel,
                    _ => PlayerAction.None
                };
            }
            return key.Key switch
            {
                ConsoleKey.B => PlayerAction.BuyPotion,
                ConsoleKey.S => PlayerAction.SellHerb,
                ConsoleKey.J => PlayerAction.OpenJournal,
                ConsoleKey.I => PlayerAction.OpenGearInventory,
                ConsoleKey.E => PlayerAction.Interact,
                ConsoleKey.M => PlayerAction.OpenMenu,
                ConsoleKey.Escape => PlayerAction.Cancel,
                ConsoleKey.W or ConsoleKey.UpArrow => PlayerAction.MoveNorth,
                // Note: 'S' is ambiguous (sell herb vs. south movement). The town's MapKey above
                // prefers SellHerb for S; movement south uses the down arrow.
                ConsoleKey.DownArrow => PlayerAction.MoveSouth,
                ConsoleKey.A or ConsoleKey.LeftArrow => PlayerAction.MoveWest,
                ConsoleKey.D or ConsoleKey.RightArrow => PlayerAction.MoveEast,
                _ => PlayerAction.None
            };
        }

        private void RunTown()
        {
            if (_pendingTownInteraction.HasValue)
            {
                // Re-render the pending menu so the previous Run iteration's map paint
                // doesn't leave the player without a visible choice. EnterTown is skipped
                // here so the town view doesn't blow the menu away.
                RenderTownInteractionMenu(_pendingTownInteraction.Value);
            }
            else
            {
                EnterTown();
                if (CurrentScene != SceneId.Town)
                {
                    return;
                }
            }
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            TickTown(MapTownConsoleKey(key));
        }

        private void EnterDungeon()
        {
            if (TryEnterPendingContractNotice(SceneId.Dungeon))
            {
                Enter();
                return;
            }

            _host.RenderMap(new MapRenderModel(
                Title: $"Dungeon Floor {_currentDungeonFloor}",
                Map: _gameState.ExplorationState.Map,
                HeroPosition: GetHeroPosition(),
                GuardPosition: null,
                Markers: _dungeonMarkers,
                Gold: _currencyQueryHandler.Query(_gameState, new GetCurrencyBalanceQuery(GoldCurrencyId)),
                Tokens: _currencyQueryHandler.Query(_gameState, new GetCurrencyBalanceQuery(TokenCurrencyId)),
                Potions: _inventoryQuantityQueryHandler.Query(_gameState, new GetInventoryItemQuantityQuery(MediumPotionItemId)),
                Depth: _currentDungeonFloor,
                ContractInfo: BuildActiveContractHudText(),
                Controls: "WASD: Move | E: Use stairs | J: Journal | I: Gear | T: Town portal | M: Menu",
                LastMessage: _lastMessage,
                MessageTone: _lastMessageTone,
                WallDecorations: _dungeonWallDecorations,
                Actors: BuildActorViews()));
        }

        private void TickDungeon(PlayerAction action)
        {
            switch (action)
            {
                case PlayerAction.UseStairs:
                    if (IsHeroAt(_dungeonDownStairs))
                    {
                        DescendDungeonFloor();
                    }
                    else if (IsHeroAt(_dungeonUpStairs))
                    {
                        AscendOrReturnTownFromDungeon();
                    }
                    else
                    {
                        _lastMessage = "Find an up '<' or down '>' stair.";
                    }
                    return;
                case PlayerAction.OpenJournal:
                    _journalReturnScene = SceneId.Dungeon;
                    CurrentScene = SceneId.ContractJournal;
                    return;
                case PlayerAction.OpenGearInventory:
                    _gearReturnScene = SceneId.Dungeon;
                    CurrentScene = SceneId.GearInventory;
                    return;
                case PlayerAction.TownPortal:
                    ReturnToTownViaPortal();
                    return;
                case PlayerAction.OpenMenu:
                case PlayerAction.Cancel:
                    CurrentScene = SceneId.MainMenu;
                    _lastMessage = "Returned to main menu.";
                    return;
                case PlayerAction.MoveNorth:
                case PlayerAction.MoveSouth:
                case PlayerAction.MoveEast:
                case PlayerAction.MoveWest:
                    HandleMovementAction(action);
                    TryResolveDungeonStep();
                    return;
            }
        }

        private static PlayerAction MapDungeonConsoleKey(ConsoleKeyInfo key) => key.Key switch
        {
            ConsoleKey.E => PlayerAction.UseStairs,
            ConsoleKey.J => PlayerAction.OpenJournal,
            ConsoleKey.I => PlayerAction.OpenGearInventory,
            ConsoleKey.T => PlayerAction.TownPortal,
            ConsoleKey.M => PlayerAction.OpenMenu,
            ConsoleKey.Escape => PlayerAction.Cancel,
            ConsoleKey.W or ConsoleKey.UpArrow => PlayerAction.MoveNorth,
            ConsoleKey.S or ConsoleKey.DownArrow => PlayerAction.MoveSouth,
            ConsoleKey.A or ConsoleKey.LeftArrow => PlayerAction.MoveWest,
            ConsoleKey.D or ConsoleKey.RightArrow => PlayerAction.MoveEast,
            _ => PlayerAction.None
        };

        private void RunDungeon()
        {
            EnterDungeon();
            if (CurrentScene != SceneId.Dungeon)
            {
                return;
            }
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            TickDungeon(MapDungeonConsoleKey(key));
        }

        private void EnterBattle()
        {
            BattleStatus? status = _battleStatusQueryHandler.Query(_gameState, new GetActiveBattleStatusQuery());
            if (status is null || status != BattleStatus.Active)
            {
                HandleBattleCompletion();
                Enter();
                return;
            }

            string? turnActorId = _turnActorQueryHandler.Query(_gameState, new GetCurrentBattleTurnActorQuery());
            _host.RenderBattle(new BattleRenderModel(
                Title: "Battle",
                Battle: _gameState.ActiveBattle!,
                CurrentTurnActorId: turnActorId,
                Controls: BuildBattleControls(),
                ClassActionInfo: BuildBattleClassActionInfo(),
                RecentLog: _battleLog.ToArray(),
                LastMessage: _lastMessage,
                MessageTone: _lastMessageTone));
            // Keep a live clone around so that when the engine nulls ActiveBattle on
            // battle-end, the BattleEndedEvent reactor can overlay FinalActorHp onto a
            // real BattleState and we can render one last frame at HP=0.
            _liveBattleClone = _gameState.ActiveBattle.Clone();
        }

        /// <summary>True when the current battle is waiting on the AI to take its turn.</summary>
        public bool IsBattleAiTurn
        {
            get
            {
                if (CurrentScene != SceneId.Battle || _gameState.ActiveBattle is null)
                {
                    return false;
                }
                string? turnActorId = _turnActorQueryHandler.Query(_gameState, new GetCurrentBattleTurnActorQuery());
                if (turnActorId is null || !_gameState.ActiveBattle.TryGetActor(turnActorId, out BattleActorState turnActor))
                {
                    return false;
                }
                return !turnActor.PlayerControlled;
            }
        }

        private void TickBattle(PlayerAction action)
        {
            BattleStatus? status = _battleStatusQueryHandler.Query(_gameState, new GetActiveBattleStatusQuery());
            if (status is null || status != BattleStatus.Active)
            {
                HandleBattleCompletion();
                return;
            }

            string? turnActorId = _turnActorQueryHandler.Query(_gameState, new GetCurrentBattleTurnActorQuery());
            if (turnActorId is null || !_gameState.ActiveBattle!.TryGetActor(turnActorId, out BattleActorState turnActor))
            {
                _lastMessage = "Turn state is invalid.";
                CurrentScene = SceneId.Dungeon;
                return;
            }

            if (!turnActor.PlayerControlled)
            {
                // AI turn — action is ignored; advance one AI move.
                DomainResult aiResult = Dispatch(new ExecuteAiTurnCommand());
                if (!aiResult.IsSuccess)
                {
                    _lastMessage = aiResult.Error?.Message ?? "AI action failed.";
                }
                if (_gameState.ActiveBattle is null)
                {
                    HandleBattleCompletion();
                }
                return;
            }

            switch (action)
            {
                case PlayerAction.Retreat:
                    BuildTownMap();
                    CurrentScene = SceneId.Town;
                    _lastMessage = "You fled and reappeared in town.";
                    _gameState.ActiveBattle = null;
                    _activeBattleSnapshot = null;
                    _pendingBattleSummary = null;
                    _pendingBossReward = null;
                    _activeBossFloor = null;
                    _battleLog.Clear();
                    return;

                case PlayerAction.UsePotion:
                    TryUseBattlePotion(turnActor);
                    if (_gameState.ActiveBattle is null)
                    {
                        HandleBattleCompletion();
                    }
                    return;

                case PlayerAction.ClassSkill1:
                    TryUseClassBattleAbility(0);
                    if (_gameState.ActiveBattle is null)
                    {
                        HandleBattleCompletion();
                    }
                    return;

                case PlayerAction.ClassSkill2:
                    TryUseClassBattleAbility(1);
                    if (_gameState.ActiveBattle is null)
                    {
                        HandleBattleCompletion();
                    }
                    return;

                case PlayerAction.Attack:
                    string? targetActorId = ResolveAttackTarget();
                    if (targetActorId is null)
                    {
                        _lastMessage = "No valid enemy target.";
                        return;
                    }
                    DomainResult playerResult = Dispatch(new UseBattleSkillCommand(HeroActorId, _selectedClass.BasicSkillId, targetActorId));
                    if (!playerResult.IsSuccess)
                    {
                        _lastMessage = playerResult.Error?.Message ?? "Player action failed.";
                        return;
                    }
                    if (_gameState.ActiveBattle is null)
                    {
                        HandleBattleCompletion();
                    }
                    return;

                default:
                    _lastMessage = "Use A attack, 1/2 class skills, P potion, or Q retreat.";
                    return;
            }
        }

        private static PlayerAction MapBattleConsoleKey(ConsoleKeyInfo key) => key.Key switch
        {
            ConsoleKey.A => PlayerAction.Attack,
            ConsoleKey.D1 or ConsoleKey.NumPad1 => PlayerAction.ClassSkill1,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => PlayerAction.ClassSkill2,
            ConsoleKey.P => PlayerAction.UsePotion,
            ConsoleKey.Q or ConsoleKey.Escape => PlayerAction.Retreat,
            _ => PlayerAction.None
        };

        private void RunBattle()
        {
            EnterBattle();
            if (CurrentScene != SceneId.Battle)
            {
                return;
            }
            if (IsBattleAiTurn)
            {
                TickBattle(PlayerAction.None);
                return;
            }
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            TickBattle(MapBattleConsoleKey(key));
        }

        private string BuildBattleControls()
        {
            return "A: Attack | 1/2: Class skills | P: Drink potion | Q: Retreat to town";
        }

        private string BuildBattleClassActionInfo()
        {
            IReadOnlyList<ClassAbilityDefinition> abilities = GetClassAbilities(_selectedClass.ClassId);
            if (abilities.Count == 0 || _gameState.ActiveBattle is null || !_gameState.ActiveBattle.TryGetActor(HeroActorId, out BattleActorState hero))
            {
                return $"Focus: 0/{MaxFocus}";
            }

            int focus = hero.Resources.TryGetValue(FocusResourceId, out int focusValue) ? focusValue : 0;
            List<string> parts = new() { $"Focus: {focus}/{MaxFocus}" };
            for (int i = 0; i < abilities.Count && i < 2; i++)
            {
                ClassAbilityDefinition ability = abilities[i];
                int cooldown = hero.Cooldowns.TryGetValue(ability.SkillId, out int cooldownValue) ? cooldownValue : 0;
                string status = cooldown > 0 ? $"CD {cooldown}" : "Ready";
                parts.Add($"{i + 1}:{ability.Name} C{ability.FocusCost} {status}");
            }

            return string.Join(" | ", parts);
        }

        private void TryUseClassBattleAbility(int abilityIndex)
        {
            IReadOnlyList<ClassAbilityDefinition> abilities = GetClassAbilities(_selectedClass.ClassId);
            if (abilityIndex < 0 || abilityIndex >= abilities.Count)
            {
                _lastMessage = "That ability slot is not available.";
                return;
            }

            if (_gameState.ActiveBattle is null)
            {
                return;
            }

            ClassAbilityDefinition ability = abilities[abilityIndex];
            string? targetActorId = ability.TargetSelf ? HeroActorId : ResolveAttackTarget();
            if (string.IsNullOrWhiteSpace(targetActorId))
            {
                _lastMessage = "No valid target.";
                return;
            }

            DomainResult result = Dispatch(new UseBattleSkillCommand(HeroActorId, ability.SkillId, targetActorId));
            if (!result.IsSuccess)
            {
                _lastMessage = result.Error?.Message ?? $"Failed to use {ability.Name}.";
            }
        }

        private void EnterBattleSummary()
        {
            if (_pendingBattleSummary is null)
            {
                CurrentScene = _postBattleScene;
                Enter();
                return;
            }

            string controls = _pendingBossReward is not null && string.IsNullOrWhiteSpace(_pendingBattleSummary.BossRewardChosen)
                ? "Press 1/2/3 to claim boss reward, then Enter/Space"
                : "Press Enter/Space to continue";

            _host.RenderBattleSummary(new BattleSummaryRenderModel(
                Outcome: _pendingBattleSummary.Outcome,
                EncounterTitle: _pendingBattleSummary.EncounterTitle,
                GoldBefore: _pendingBattleSummary.GoldBefore,
                GoldAfter: _pendingBattleSummary.GoldAfter,
                GoldDelta: _pendingBattleSummary.GoldAfter - _pendingBattleSummary.GoldBefore,
                TokensBefore: _pendingBattleSummary.TokensBefore,
                TokensAfter: _pendingBattleSummary.TokensAfter,
                TokensDelta: _pendingBattleSummary.TokensAfter - _pendingBattleSummary.TokensBefore,
                PotionsBefore: _pendingBattleSummary.PotionsBefore,
                PotionsAfter: _pendingBattleSummary.PotionsAfter,
                PotionsDelta: _pendingBattleSummary.PotionsAfter - _pendingBattleSummary.PotionsBefore,
                HerbsBefore: _pendingBattleSummary.HerbsBefore,
                HerbsAfter: _pendingBattleSummary.HerbsAfter,
                HerbsDelta: _pendingBattleSummary.HerbsAfter - _pendingBattleSummary.HerbsBefore,
                BossRewardOptions: _pendingBattleSummary.BossRewardOptions,
                BossRewardChosen: _pendingBattleSummary.BossRewardChosen,
                RecentLog: _pendingBattleSummary.RecentLog,
                Controls: controls));
        }

        private void TickBattleSummary(PlayerAction action)
        {
            if (_pendingBattleSummary is null)
            {
                CurrentScene = _postBattleScene;
                return;
            }

            int rewardChoiceIndex = action switch
            {
                PlayerAction.Digit1 => 0,
                PlayerAction.Digit2 => 1,
                PlayerAction.Digit3 => 2,
                _ => -1
            };
            if (rewardChoiceIndex >= 0
                && _pendingBossReward is not null
                && string.IsNullOrWhiteSpace(_pendingBattleSummary.BossRewardChosen))
            {
                bool claimed = TryClaimBossRewardChoice(rewardChoiceIndex, returnToPostSceneOnSuccess: false);
                if (claimed && _activeBattleSnapshot is not null && _pendingBattleSummary is not null)
                {
                    BattleSummarySnapshot refreshed = BuildBattleSummary(_activeBattleSnapshot, _pendingBattleSummary.Outcome);
                    _pendingBattleSummary = refreshed with { BossRewardChosen = _lastMessage };
                }
                return;
            }

            if (action is not (PlayerAction.Confirm or PlayerAction.Cancel))
            {
                return;
            }

            if (_pendingBossReward is not null && string.IsNullOrWhiteSpace(_pendingBattleSummary.BossRewardChosen))
            {
                return;
            }

            if (_postBattleScene == SceneId.Town)
            {
                BuildTownMap();
                _lastMessage = "You limp back to town after the fight.";
            }
            else
            {
                _lastMessage = "You push deeper into the dungeon.";
            }

            if (_pendingContractNotice is not null)
            {
                _resumeSceneAfterContractNotice = _postBattleScene;
                CurrentScene = SceneId.ContractNotice;
            }
            else
            {
                CurrentScene = _postBattleScene;
            }

            _activeBattleSnapshot = null;
            _pendingBattleSummary = null;
            _battleLog.Clear();
        }

        private static PlayerAction MapBattleSummaryConsoleKey(ConsoleKeyInfo key) => key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => PlayerAction.Digit1,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => PlayerAction.Digit2,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => PlayerAction.Digit3,
            ConsoleKey.Enter or ConsoleKey.Spacebar => PlayerAction.Confirm,
            ConsoleKey.Escape => PlayerAction.Cancel,
            _ => PlayerAction.None
        };

        private void RunBattleSummary()
        {
            EnterBattleSummary();
            if (CurrentScene != SceneId.BattleSummary)
            {
                return;
            }
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            TickBattleSummary(MapBattleSummaryConsoleKey(key));
        }

        private void EnterContractNotice()
        {
            if (_pendingContractNotice is null)
            {
                CurrentScene = _resumeSceneAfterContractNotice;
                Enter();
                return;
            }

            _host.RenderContractNotice(
                _pendingContractNotice.Title,
                _pendingContractNotice.Body,
                "Press Enter/Space to continue");
        }

        private void TickContractNotice(PlayerAction action)
        {
            if (action is not (PlayerAction.Confirm or PlayerAction.Cancel))
            {
                return;
            }

            _pendingContractNotice = null;
            CurrentScene = _resumeSceneAfterContractNotice;
        }

        private static PlayerAction MapContractNoticeConsoleKey(ConsoleKeyInfo key) => key.Key switch
        {
            ConsoleKey.Enter or ConsoleKey.Spacebar => PlayerAction.Confirm,
            ConsoleKey.Escape => PlayerAction.Cancel,
            _ => PlayerAction.None
        };

        private void RunContractNotice()
        {
            EnterContractNotice();
            // If EnterContractNotice transitioned away (no pending notice), Enter() already ran on the new scene.
            if (CurrentScene != SceneId.ContractNotice)
            {
                return;
            }
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            TickContractNotice(MapContractNoticeConsoleKey(key));
        }

        private void EnterContractJournal()
        {
            List<string> lines = BuildJournalLines();
            bool canAbandon = !string.IsNullOrWhiteSpace(_activeContractQuestId)
                && !IsQuestRewarded(_activeContractQuestId!);
            string controls = canAbandon
                ? "A: Abandon active contract | Enter/Esc/J: Return"
                : "Enter/Esc/J: Return";
            _host.RenderContractJournal("Contract Journal", lines, controls);
        }

        private void TickContractJournal(PlayerAction action)
        {
            if (action == PlayerAction.Attack)
            {
                bool canAbandon = !string.IsNullOrWhiteSpace(_activeContractQuestId)
                    && !IsQuestRewarded(_activeContractQuestId!);
                if (canAbandon)
                {
                    TryAbandonActiveContract();
                }
                return;
            }

            if (action is PlayerAction.Confirm or PlayerAction.Cancel)
            {
                CurrentScene = _journalReturnScene;
            }
        }

        private static PlayerAction MapContractJournalConsoleKey(ConsoleKeyInfo key) => key.Key switch
        {
            ConsoleKey.A => PlayerAction.Attack,
            ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.J => PlayerAction.Confirm,
            ConsoleKey.Escape => PlayerAction.Cancel,
            _ => PlayerAction.None
        };

        private void RunContractJournal()
        {
            EnterContractJournal();
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            TickContractJournal(MapContractJournalConsoleKey(key));
        }

        private void TryAbandonActiveContract()
        {
            if (string.IsNullOrWhiteSpace(_activeContractQuestId))
            {
                return;
            }

            string questId = _activeContractQuestId!;
            string title = GetContractTitle(questId);
            DomainResult result = Dispatch(new AbandonQuestCommand(questId));
            if (!result.IsSuccess)
            {
                SetMessage(result.Error?.Message ?? "Could not abandon contract.", MessageTone.Error);
                return;
            }

            _activeContractQuestId = null;
            _contractsReadyForTurnIn.Remove(questId);
            SetMessage($"Abandoned contract: {title}.", MessageTone.Warning);
        }

        private void EnterGearInventory()
        {
            _host.RenderGearInventory(BuildGearInventoryModel());
        }

        private void TickGearInventory(PlayerAction action)
        {
            switch (action)
            {
                case PlayerAction.Digit1: TryToggleGear(0); return;
                case PlayerAction.Digit2: TryToggleGear(1); return;
                case PlayerAction.Digit3: TryToggleGear(2); return;
                case PlayerAction.Digit4: TryToggleGear(3); return;
                case PlayerAction.Digit5: TryToggleGear(4); return;
                case PlayerAction.Digit6: TryToggleGear(5); return;
                case PlayerAction.UnequipAll: TryUnequipAll(); return;
                case PlayerAction.FilterCycle: CycleGearFilter(); return;
                case PlayerAction.Confirm:
                case PlayerAction.Cancel:
                    CurrentScene = _gearReturnScene;
                    return;
            }
        }

        private static PlayerAction MapGearInventoryConsoleKey(ConsoleKeyInfo key) => key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => PlayerAction.Digit1,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => PlayerAction.Digit2,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => PlayerAction.Digit3,
            ConsoleKey.D4 or ConsoleKey.NumPad4 => PlayerAction.Digit4,
            ConsoleKey.D5 or ConsoleKey.NumPad5 => PlayerAction.Digit5,
            ConsoleKey.D6 or ConsoleKey.NumPad6 => PlayerAction.Digit6,
            ConsoleKey.U => PlayerAction.UnequipAll,
            ConsoleKey.Tab or ConsoleKey.F => PlayerAction.FilterCycle,
            ConsoleKey.Enter or ConsoleKey.I or ConsoleKey.Spacebar => PlayerAction.Confirm,
            ConsoleKey.Escape => PlayerAction.Cancel,
            _ => PlayerAction.None
        };

        private void RunGearInventory()
        {
            EnterGearInventory();
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            TickGearInventory(MapGearInventoryConsoleKey(key));
        }

        private void EnterMetaShrine()
        {
            List<string> lines = BuildMetaShrineLines();
            _host.RenderContractJournal("Shrine of Echoes", lines, "Press 1-4 to unlock perks, Enter/Esc to return");
        }

        private void TickMetaShrine(PlayerAction action)
        {
            switch (action)
            {
                case PlayerAction.Digit1:
                    TryPurchaseMetaUnlock(0);
                    return;
                case PlayerAction.Digit2:
                    TryPurchaseMetaUnlock(1);
                    return;
                case PlayerAction.Digit3:
                    TryPurchaseMetaUnlock(2);
                    return;
                case PlayerAction.Digit4:
                    TryPurchaseMetaUnlock(3);
                    return;
                case PlayerAction.Confirm:
                case PlayerAction.Cancel:
                    CurrentScene = SceneId.Town;
                    return;
            }
        }

        private static PlayerAction MapMetaShrineConsoleKey(ConsoleKeyInfo key) => key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => PlayerAction.Digit1,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => PlayerAction.Digit2,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => PlayerAction.Digit3,
            ConsoleKey.D4 or ConsoleKey.NumPad4 => PlayerAction.Digit4,
            ConsoleKey.Enter or ConsoleKey.Spacebar => PlayerAction.Confirm,
            ConsoleKey.Escape => PlayerAction.Cancel,
            _ => PlayerAction.None
        };

        private void RunMetaShrine()
        {
            EnterMetaShrine();
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            TickMetaShrine(MapMetaShrineConsoleKey(key));
        }

        private void EnterBossReward()
        {
            if (_pendingBossReward is null)
            {
                if (_pendingContractNotice is not null)
                {
                    _resumeSceneAfterContractNotice = _postBattleScene;
                    CurrentScene = SceneId.ContractNotice;
                }
                else
                {
                    CurrentScene = _postBattleScene;
                }
                Enter();
                return;
            }

            List<string> lines = BuildBossRewardLines(_pendingBossReward);
            _host.RenderContractJournal(
                "Boss Reward Chest",
                lines,
                "Press 1/2/3 to choose your reward");
        }

        private void TickBossReward(PlayerAction action)
        {
            switch (action)
            {
                case PlayerAction.Digit1:
                    TryClaimBossRewardChoice(0, returnToPostSceneOnSuccess: true);
                    return;
                case PlayerAction.Digit2:
                    TryClaimBossRewardChoice(1, returnToPostSceneOnSuccess: true);
                    return;
                case PlayerAction.Digit3:
                    TryClaimBossRewardChoice(2, returnToPostSceneOnSuccess: true);
                    return;
            }
        }

        private static PlayerAction MapBossRewardConsoleKey(ConsoleKeyInfo key) => key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => PlayerAction.Digit1,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => PlayerAction.Digit2,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => PlayerAction.Digit3,
            _ => PlayerAction.None
        };

        private void RunBossReward()
        {
            EnterBossReward();
            if (CurrentScene != SceneId.BossReward)
            {
                return;
            }
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            TickBossReward(MapBossRewardConsoleKey(key));
        }

        private List<string> BuildJournalLines()
        {
            List<string> lines = new();
            lines.Add($"Class: {_selectedClass.Name}");
            lines.Add($"Deepest Floor: {GetDeepestFloor()}");
            lines.Add($"Boss Floors Cleared: {_clearedBossFloors.Count}");
            lines.Add(string.Empty);

            QuestDefinition? active = string.IsNullOrWhiteSpace(_activeContractQuestId)
                ? null
                : GetContractDef(_activeContractQuestId!);
            if (active is null)
            {
                lines.Add("Active Contract: None");
            }
            else
            {
                QuestStatus activeStatus = _questStatusQueryHandler.Query(_gameState, new GetQuestStatusQuery(active.Id));
                lines.Add($"Active Contract: {active.DisplayName ?? active.Id}");
                lines.Add($"- {active.Description ?? string.Empty}");
                if (activeStatus == QuestStatus.Completed)
                {
                    lines.Add("- Ready to turn in at the quest board.");
                }

                foreach (QuestObjectiveDefinition objective in GetLeafObjectives(active))
                {
                    int progress = _questObjectiveProgressQueryHandler.Query(
                        _gameState,
                        new GetQuestObjectiveProgressQuery(active.Id, objective.Id));
                    int clamped = Math.Min(progress, objective.RequiredCount);
                    lines.Add($"  * {objective.DisplayName ?? objective.Id}: {clamped}/{objective.RequiredCount}");
                }
            }

            lines.Add(string.Empty);
            lines.Add("Turned In Contracts:");
            int completedCount = 0;
            for (int i = 0; i < ContractQuestIds.Length; i++)
            {
                string questId = ContractQuestIds[i];
                if (!IsQuestRewarded(questId))
                {
                    continue;
                }

                completedCount++;
                lines.Add($"- {GetContractTitle(questId)}");
            }

            if (completedCount == 0)
            {
                lines.Add("- none -");
            }

            lines.Add(string.Empty);
            lines.Add("Meta Unlocks:");
            if (_unlockedMetaUnlocks.Count == 0)
            {
                lines.Add("- none -");
            }
            else
            {
                for (int i = 0; i < MetaUnlockDefinitions.Length; i++)
                {
                    MetaUnlockDefinition unlock = MetaUnlockDefinitions[i];
                    if (_unlockedMetaUnlocks.Contains(unlock.Id))
                    {
                        lines.Add($"- {unlock.Name}");
                    }
                }
            }

            return lines;
        }

        private List<string> BuildMetaShrineLines()
        {
            List<string> lines = new();
            long tokens = _currencyQueryHandler.Query(_gameState, new GetCurrencyBalanceQuery(TokenCurrencyId));
            lines.Add("Spend tokens to unlock permanent run bonuses.");
            lines.Add($"Tokens: {tokens}");
            lines.Add(string.Empty);

            for (int i = 0; i < MetaUnlockDefinitions.Length; i++)
            {
                MetaUnlockDefinition unlock = MetaUnlockDefinitions[i];
                bool unlocked = _unlockedMetaUnlocks.Contains(unlock.Id);
                string status = unlocked ? "UNLOCKED" : $"Cost: {unlock.TokenCost} token(s)";
                lines.Add($"{i + 1}. {unlock.Name} [{status}]");
                lines.Add($"   {unlock.Description}");
            }

            return lines;
        }

        private static List<string> BuildBossRewardLines(BossRewardSnapshot snapshot)
        {
            List<string> lines = new();
            lines.Add($"Floor {snapshot.Floor} boss defeated. Choose one reward:");
            lines.Add(string.Empty);
            for (int i = 0; i < snapshot.Choices.Count; i++)
            {
                BossRewardChoice choice = snapshot.Choices[i];
                lines.Add($"{i + 1}. {choice.Label}");
                lines.Add($"   {choice.Description}");
            }

            return lines;
        }

        private readonly List<GearMetadata> _visibleGear = new();
        private GearFilter _gearFilter = GearFilter.All;

        private List<GearMetadata> BuildVisibleGearList()
        {
            List<GearMetadata> visible = new();
            for (int i = 0; i < GearCatalog.Length; i++)
            {
                GearMetadata gear = GearCatalog[i];
                int qty = _inventoryQuantityQueryHandler.Query(_gameState, new GetInventoryItemQuantityQuery(gear.ItemId));
                string? slotItemId = _equippedItemQueryHandler.Query(_gameState, new GetEquippedItemQuery(gear.SlotId));
                bool equipped = string.Equals(slotItemId, gear.ItemId, StringComparison.Ordinal);
                if (equipped || qty > 0)
                {
                    visible.Add(gear);
                }
            }

            return visible;
        }

        private void CycleGearFilter()
        {
            _gearFilter = _gearFilter switch
            {
                GearFilter.All => GearFilter.Weapon,
                GearFilter.Weapon => GearFilter.Armor,
                GearFilter.Armor => GearFilter.Accessory,
                _ => GearFilter.All
            };
        }

        private GearInventoryRenderModel BuildGearInventoryModel()
        {
            // Sort: slot order (weapon → armor → accessory), then catalog order
            // inside a slot. The list is stable across re-renders so digit
            // hotkeys map to the same item the player saw last frame.
            List<GearMetadata> visible = BuildVisibleGearList();
            visible.Sort(CompareGearForDisplay);

            _visibleGear.Clear();
            _visibleGear.AddRange(visible);

            // Build entries for every visible item. Each gets a digit hotkey
            // matching its index in _visibleGear so 1-6 still toggles directly.
            List<GearInventoryEntry> entries = new();
            for (int i = 0; i < _visibleGear.Count; i++)
            {
                bool passesFilter = _gearFilter == GearFilter.All || SlotMatchesFilter(_visibleGear[i].SlotId, _gearFilter);
                if (!passesFilter)
                {
                    continue;
                }
                entries.Add(BuildGearEntry(_visibleGear[i], hotkeyIndex: i + 1));
            }

            // Equipped slot cards: each shows the currently-equipped item (or
            // null if the slot is empty). These come from _visibleGear so we
            // reuse the same metadata source and stat formatting.
            GearSlotView weaponSlot = BuildSlotView(SlotWeapon, "Weapon");
            GearSlotView armorSlot = BuildSlotView(SlotArmor, "Armor");
            GearSlotView accessorySlot = BuildSlotView(SlotAccessory, "Accessory");

            string controls = "Press 1-6 to equip/toggle  |  Tab cycle filter  |  U unequip all  |  Enter/Esc return";
            return new GearInventoryRenderModel(
                ClassName: _selectedClass.Name,
                WeaponSlot: weaponSlot,
                ArmorSlot: armorSlot,
                AccessorySlot: accessorySlot,
                Entries: entries,
                Filter: _gearFilter,
                Controls: controls,
                LastMessage: _lastMessage ?? string.Empty,
                MessageTone: _lastMessageTone);
        }

        private static bool SlotMatchesFilter(string slotId, GearFilter filter) => filter switch
        {
            GearFilter.Weapon => string.Equals(slotId, SlotWeapon, StringComparison.Ordinal),
            GearFilter.Armor => string.Equals(slotId, SlotArmor, StringComparison.Ordinal),
            GearFilter.Accessory => string.Equals(slotId, SlotAccessory, StringComparison.Ordinal),
            _ => true
        };

        private static int CompareGearForDisplay(GearMetadata a, GearMetadata b)
        {
            int slotA = SlotSortOrder(a.SlotId);
            int slotB = SlotSortOrder(b.SlotId);
            if (slotA != slotB) return slotA.CompareTo(slotB);
            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        }

        private static int SlotSortOrder(string slotId)
        {
            if (string.Equals(slotId, SlotWeapon, StringComparison.Ordinal)) return 0;
            if (string.Equals(slotId, SlotArmor, StringComparison.Ordinal)) return 1;
            if (string.Equals(slotId, SlotAccessory, StringComparison.Ordinal)) return 2;
            return 99;
        }

        private GearSlotView BuildSlotView(string slotId, string slotLabel)
        {
            string? equippedItemId = _equippedItemQueryHandler.Query(_gameState, new GetEquippedItemQuery(slotId));
            if (string.IsNullOrWhiteSpace(equippedItemId))
            {
                return new GearSlotView(slotId, slotLabel, Equipped: null);
            }

            GearMetadata? gear = FindGearMetadata(equippedItemId!);
            if (gear is null)
            {
                return new GearSlotView(slotId, slotLabel, Equipped: null);
            }

            return new GearSlotView(slotId, slotLabel, Equipped: BuildGearEntry(gear, hotkeyIndex: -1));
        }

        private GearInventoryEntry BuildGearEntry(GearMetadata gear, int hotkeyIndex)
        {
            int qty = _inventoryQuantityQueryHandler.Query(_gameState, new GetInventoryItemQuantityQuery(gear.ItemId));
            string? slotItemId = _equippedItemQueryHandler.Query(_gameState, new GetEquippedItemQuery(gear.SlotId));
            bool isEquipped = string.Equals(slotItemId, gear.ItemId, StringComparison.Ordinal);

            // Stats: only include nonzero values so common gear doesn't display
            // five zeros. Order is the same as the GearMetadata ctor for
            // visual consistency between similar items.
            List<GearStatLine> stats = new();
            if (gear.Atk != 0) stats.Add(new GearStatLine("ATK", gear.Atk));
            if (gear.Def != 0) stats.Add(new GearStatLine("DEF", gear.Def));
            if (gear.Matk != 0) stats.Add(new GearStatLine("MATK", gear.Matk));
            if (gear.Mdef != 0) stats.Add(new GearStatLine("MDEF", gear.Mdef));
            if (gear.Initiative != 0) stats.Add(new GearStatLine("INIT", gear.Initiative));
            if (gear.Crit != 0) stats.Add(new GearStatLine("CRIT", gear.Crit));
            if (gear.Acc != 0) stats.Add(new GearStatLine("ACC", gear.Acc));
            if (gear.Eva != 0) stats.Add(new GearStatLine("EVA", gear.Eva));
            if (gear.CritDmg != 0) stats.Add(new GearStatLine("CRITDMG", gear.CritDmg));

            // Deltas: only computed for unequipped items, comparing against
            // whatever's currently in the same slot. Equipped items show no
            // delta (it'd be ±0 across the board, just noise).
            List<GearStatDelta> deltas = new();
            if (!isEquipped && !string.IsNullOrWhiteSpace(slotItemId))
            {
                GearMetadata? equipped = FindGearMetadata(slotItemId!);
                if (equipped is not null)
                {
                    AddDeltaIfRelevant(deltas, "ATK", gear.Atk - equipped.Atk);
                    AddDeltaIfRelevant(deltas, "DEF", gear.Def - equipped.Def);
                    AddDeltaIfRelevant(deltas, "MATK", gear.Matk - equipped.Matk);
                    AddDeltaIfRelevant(deltas, "MDEF", gear.Mdef - equipped.Mdef);
                    AddDeltaIfRelevant(deltas, "INIT", gear.Initiative - equipped.Initiative);
                    AddDeltaIfRelevant(deltas, "CRIT", gear.Crit - equipped.Crit);
                    AddDeltaIfRelevant(deltas, "ACC", gear.Acc - equipped.Acc);
                    AddDeltaIfRelevant(deltas, "EVA", gear.Eva - equipped.Eva);
                    AddDeltaIfRelevant(deltas, "CRITDMG", gear.CritDmg - equipped.CritDmg);
                }
            }

            return new GearInventoryEntry(
                HotkeyIndex: hotkeyIndex,
                ItemId: gear.ItemId,
                Name: gear.Name,
                SlotId: gear.SlotId,
                SlotLabel: SlotDisplayLabel(gear.SlotId),
                Tier: DeriveGearTier(gear),
                Quantity: qty,
                IsEquipped: isEquipped,
                Stats: stats,
                Deltas: deltas,
                GrantedSkillIds: gear.GrantedSkillIds ?? Array.Empty<string>());
        }

        private static void AddDeltaIfRelevant(List<GearStatDelta> deltas, string label, int delta)
        {
            if (delta != 0)
            {
                deltas.Add(new GearStatDelta(label, delta));
            }
        }

        private static string SlotDisplayLabel(string slotId)
        {
            if (string.Equals(slotId, SlotWeapon, StringComparison.Ordinal)) return "Weapon";
            if (string.Equals(slotId, SlotArmor, StringComparison.Ordinal)) return "Armor";
            if (string.Equals(slotId, SlotAccessory, StringComparison.Ordinal)) return "Accessory";
            return slotId;
        }

        // Tier is derived, not authored. Sum the absolute value of every stat
        // bonus, then bump up one tier if the item also grants a skill
        // (those are rare-by-design — Oak Wand's skill.bolt is the only one
        // in the starter catalog).
        private static GearTier DeriveGearTier(GearMetadata gear)
        {
            int score = Math.Abs(gear.Atk) + Math.Abs(gear.Def) + Math.Abs(gear.Matk)
                + Math.Abs(gear.Mdef) + Math.Abs(gear.Initiative)
                + Math.Abs(gear.Crit) + Math.Abs(gear.Acc) + Math.Abs(gear.Eva) + Math.Abs(gear.CritDmg);

            GearTier tier;
            if (score >= 13) tier = GearTier.Epic;
            else if (score >= 8) tier = GearTier.Rare;
            else if (score >= 4) tier = GearTier.Uncommon;
            else tier = GearTier.Common;

            if (gear.GrantedSkillIds is { Count: > 0 } && tier < GearTier.Epic)
            {
                tier = (GearTier)(((int)tier) + 1);
            }

            return tier;
        }

        private static GearMetadata? FindGearMetadata(string itemId)
        {
            for (int i = 0; i < GearCatalog.Length; i++)
            {
                if (string.Equals(GearCatalog[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    return GearCatalog[i];
                }
            }
            return null;
        }

        private void ApplyStarterLoadout()
        {
            (string weapon, string armor, string accessory) starter = _selectedClass.ClassId switch
            {
                PlayerClass.Knight => (ItemBronzeBlade, ItemLeatherVest, ItemIronRing),
                PlayerClass.Ranger => (ItemBronzeBlade, ItemLeatherVest, ItemLuckyCharm),
                PlayerClass.Arcanist => (ItemOakWand, ItemMysticRobe, ItemLuckyCharm),
                _ => (ItemBronzeBlade, ItemLeatherVest, ItemIronRing)
            };

            EquipStarterPiece(starter.weapon);
            EquipStarterPiece(starter.armor);
            EquipStarterPiece(starter.accessory);
        }

        private void EquipStarterPiece(string itemId)
        {
            DomainResult addResult = Dispatch(new AddInventoryItemCommand(itemId, 1));
            if (!addResult.IsSuccess)
            {
                return;
            }

            Dispatch(new EquipItemCommand(itemId, HeroActorId));
        }

        private void TryToggleGear(int gearIndex)
        {
            if (gearIndex < 0 || gearIndex >= _visibleGear.Count)
            {
                return;
            }

            GearMetadata target = _visibleGear[gearIndex];
            string? currentlyEquipped = _equippedItemQueryHandler.Query(_gameState, new GetEquippedItemQuery(target.SlotId));
            if (string.Equals(currentlyEquipped, target.ItemId, StringComparison.Ordinal))
            {
                TryUnequipSlot(target.SlotId);
                return;
            }

            DomainResult equipResult = Dispatch(new EquipItemCommand(target.ItemId, HeroActorId));
            _lastMessage = equipResult.IsSuccess
                ? $"Equipped {target.Name}."
                : equipResult.Error?.Message ?? $"Unable to equip {target.Name}.";
        }

        private void TryUnequipSlot(string slotId)
        {
            DomainResult result = Dispatch(new UnequipItemCommand(slotId, HeroActorId));
            if (!result.IsSuccess)
            {
                _lastMessage = result.Error?.Message ?? $"Unable to unequip {slotId}.";
                return;
            }

            _lastMessage = $"Unequipped {slotId}.";
        }

        private void TryUnequipAll()
        {
            if (_gameState.EquipmentState.IsSlotOccupied(SlotWeapon))
            {
                TryUnequipSlot(SlotWeapon);
            }

            if (_gameState.EquipmentState.IsSlotOccupied(SlotArmor))
            {
                TryUnequipSlot(SlotArmor);
            }

            if (_gameState.EquipmentState.IsSlotOccupied(SlotAccessory))
            {
                TryUnequipSlot(SlotAccessory);
            }
        }

        private string GetEquippedLabel(string slotId)
        {
            string? itemId = _equippedItemQueryHandler.Query(_gameState, new GetEquippedItemQuery(slotId));
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return "<none>";
            }

            return GetGearDisplayName(itemId!) ?? itemId!;
        }

        private string? GetGearDisplayName(string itemId)
        {
            if (_definitions.TryGetEquipment(itemId, out EquipmentDefinition equipment))
            {
                return equipment.DisplayName;
            }

            return null;
        }

        private void StartNewRun(ClassProfile selectedClass)
        {
            _selectedClass = selectedClass;
            _runSeed = (ulong)DateTime.UtcNow.Ticks;
            _gameState = new GameState();
            _eventSink = new InMemoryDomainEventSink();
            _context = CreateContext(_runSeed, _eventSink);
            _currentDungeonFloor = 0;
            _dungeonFloors.Clear();
            _battleSequence = 0;
            HeroFacing = FacingDirection.Down;
            _lastBattleStatus = null;
            _activeBattleSnapshot = null;
            _pendingBattleSummary = null;
            _pendingContractNotice = null;
            _pendingBossReward = null;
            _contractsReadyForTurnIn.Clear();
            _activeContractQuestId = null;
            _clearedBossFloors.Clear();
            _activeBossFloor = null;
            _lastEncounterThemeId = null;
            _lastEncounterEnemyCount = 0;
            _pendingEncounterGearDropItemId = null;
            _battleLog.Clear();
            _lastMessage = $"New run started as {_selectedClass.Name}. Reach the dungeon entrance.";

            int baseCapacity = 16 + (HasMetaUnlock(MetaUnlockId.DeepPockets) ? 4 : 0);
            int startPotionCount = 2 + (HasMetaUnlock(MetaUnlockId.FieldRations) ? 1 : 0);
            Dispatch(new ConfigureInventoryCapacityCommand(baseCapacity));
            Dispatch(new GrantCurrencyCommand(GoldCurrencyId, 75));
            Dispatch(new GrantCurrencyCommand(TokenCurrencyId, 2));
            Dispatch(new AddInventoryItemCommand(MediumPotionItemId, startPotionCount));
            Dispatch(new ConfigureActorProgressionCommand(HeroActorId, HeroCurveId, level: 1, xp: 0));
            ApplyStarterLoadout();
            SeedHeroStatBlock();
            BuildTownMap();
            SaveProgress("new run");
        }

        private void BuildTownMap()
        {
            TownBlueprint blueprint = TownLayout.Build();
            int width = blueprint.Width;
            int height = blueprint.Height;

            _townWallDecorations.Clear();
            foreach (KeyValuePair<GridPosition, char> entry in blueprint.WallDecorations)
            {
                _townWallDecorations[entry.Key] = entry.Value;
            }

            _townFloorDecorations.Clear();
            foreach (KeyValuePair<GridPosition, char> entry in blueprint.FloorDecorations)
            {
                _townFloorDecorations[entry.Key] = entry.Value;
            }

            GridPosition heroSpawn = blueprint.HeroSpawn;
            GridPosition guardSpawn = blueprint.Landmarks['G'];
            GridPosition stairsPosition = blueprint.Landmarks['>'];
            GridPosition alchemistPosition = blueprint.Landmarks['A'];
            GridPosition healerPosition = blueprint.Landmarks['H'];
            GridPosition cachePosition = blueprint.Landmarks['C'];
            GridPosition fountainPosition = blueprint.Landmarks['F'];
            GridPosition boardPosition = blueprint.Landmarks['Q'];
            GridPosition shrinePosition = blueprint.Landmarks['S'];

            Dispatch(new ConfigureExplorationMapCommand("town.hub", width, height, blueprint.Tiles));
            Dispatch(new UpsertExplorationActorCommand(HeroActorId, heroSpawn.X, heroSpawn.Y, blocksMovement: true));
            // Guard sits on his landmark tile. He must NOT block movement, because the
            // landmark-interaction system requires the hero to step onto his tile and
            // press E — same pattern as every other town landmark.
            Dispatch(new UpsertExplorationActorCommand(GuardActorId, guardSpawn.X, guardSpawn.Y, blocksMovement: false));
            PlaceTownInteractables(cachePosition, fountainPosition);
            _townStairs = stairsPosition;
            _townGuard = guardSpawn;
            _townAlchemist = alchemistPosition;
            _townHealer = healerPosition;
            _townCache = cachePosition;
            _townFountain = fountainPosition;
            _townQuestBoard = boardPosition;
            _townShrine = shrinePosition;
            _townMarkers.Clear();
            _townMarkers.Add(new MapMarker(alchemistPosition, 'A', "Alchemist"));
            _townMarkers.Add(new MapMarker(healerPosition, 'H', "Healer"));
            _townMarkers.Add(new MapMarker(cachePosition, 'C', "Cache"));
            _townMarkers.Add(new MapMarker(fountainPosition, 'F', "Fountain"));
            _townMarkers.Add(new MapMarker(boardPosition, 'Q', "Quest Board"));
            _townMarkers.Add(new MapMarker(shrinePosition, 'S', "Shrine"));
            _townMarkers.Add(new MapMarker(stairsPosition, '>', "Dungeon Entrance"));
            _townMarkers.Add(new MapMarker(guardSpawn, 'G', "Guard"));
        }

        private void StartDungeonRun()
        {
            _currentDungeonFloor = 1;
            RecordDeepestFloor(_currentDungeonFloor);
            EnsureDungeonFloorLoaded(_currentDungeonFloor, entryFromBelow: false);
            CurrentScene = SceneId.Dungeon;
            _lastMessage = "Entered dungeon floor 1.";
        }

        private void ReturnToTownFromDungeon()
        {
            int floorJustLeft = _currentDungeonFloor;
            int reward = 10 + (floorJustLeft * 3);
            Dispatch(new GrantCurrencyCommand(GoldCurrencyId, reward));
            _currentDungeonFloor = 0;
            BuildTownMap();
            CurrentScene = SceneId.Town;
            _lastMessage = $"Returned to town with {reward} gold from floor {floorJustLeft}.";
            SaveProgress("returned to town");
        }

        private void ReturnToTownViaPortal()
        {
            int reward = Math.Max(3, _currentDungeonFloor);
            Dispatch(new GrantCurrencyCommand(GoldCurrencyId, reward));
            _currentDungeonFloor = 0;
            BuildTownMap();
            CurrentScene = SceneId.Town;
            _lastMessage = $"Town portal used. Gained {reward} consolation gold.";
            SaveProgress("town portal");
        }

        private void DescendDungeonFloor()
        {
            _currentDungeonFloor++;
            RecordDeepestFloor(_currentDungeonFloor);
            EnsureDungeonFloorLoaded(_currentDungeonFloor, entryFromBelow: false);
            _lastMessage = IsBossFloorPending()
                ? $"You descend to floor {_currentDungeonFloor}. A boss presence stirs."
                : $"You descend to floor {_currentDungeonFloor}.";
        }

        private void AscendOrReturnTownFromDungeon()
        {
            if (_currentDungeonFloor <= 1)
            {
                ReturnToTownFromDungeon();
                return;
            }

            _currentDungeonFloor--;
            EnsureDungeonFloorLoaded(_currentDungeonFloor, entryFromBelow: true);
            _lastMessage = $"You ascend to floor {_currentDungeonFloor}.";
        }

        private void EnsureDungeonFloorLoaded(int floor, bool entryFromBelow)
        {
            if (!_dungeonFloors.TryGetValue(floor, out DungeonFloorBlueprint? blueprint))
            {
                blueprint = DungeonGenerator.Generate(_context.RandomSource, floor);
                _dungeonFloors[floor] = blueprint;
            }

            Dispatch(new ConfigureExplorationMapCommand($"dungeon.floor.{floor}", blueprint.Width, blueprint.Height, blueprint.Tiles));
            GridPosition heroPosition = entryFromBelow ? blueprint.Stairs : blueprint.Spawn;
            Dispatch(new UpsertExplorationActorCommand(HeroActorId, heroPosition.X, heroPosition.Y, blocksMovement: true));

            _dungeonUpStairs = blueprint.Spawn;
            _dungeonDownStairs = blueprint.Stairs;
            _dungeonMarkers.Clear();
            _dungeonMarkers.Add(new MapMarker(_dungeonUpStairs, '<', "Stairs Up"));
            _dungeonMarkers.Add(new MapMarker(_dungeonDownStairs, '>', "Stairs Down"));

            // Pillars carry a distinct glyph so they read as obstacles inside rooms rather than
            // just more outer wall. Outer walls keep the default '#'.
            _dungeonWallDecorations.Clear();
            for (int i = 0; i < blueprint.Pillars.Count; i++)
            {
                _dungeonWallDecorations[blueprint.Pillars[i]] = '○';
            }
        }

        private void TryResolveDungeonStep()
        {
            if (_gameState.ActiveBattle is not null)
            {
                CurrentScene = SceneId.Battle;
                return;
            }

            GridPosition? heroPosition = GetHeroPosition();
            if (!heroPosition.HasValue)
            {
                return;
            }

            if (!_gameState.ExplorationState.Map.TryGetTileFlags(heroPosition.Value, out ExplorationTileFlags flags))
            {
                return;
            }

            if ((flags & ExplorationTileFlags.EncounterAllowed) != ExplorationTileFlags.EncounterAllowed)
            {
                return;
            }

            if (IsBossFloorPending())
            {
                StartDungeonBossBattle();
                return;
            }

            if (_context.RandomSource.NextInt(100) >= 12)
            {
                return;
            }

            StartDungeonEncounterBattle();
        }

        private void StartDungeonEncounterBattle()
        {
            _battleSequence++;
            _lastBattleStatus = null;
            EncounterBlueprint encounter = EncounterGenerator.Generate(_currentDungeonFloor, _battleSequence, _context.RandomSource, _definitions);
            StartEncounterBattle(encounter, isBossBattle: false);
        }

        private void StartDungeonBossBattle()
        {
            _battleSequence++;
            _lastBattleStatus = null;
            EncounterBlueprint encounter = EncounterGenerator.GenerateBoss(_currentDungeonFloor, _battleSequence, _context.RandomSource, _definitions);
            StartEncounterBattle(encounter, isBossBattle: true);
        }

        private void StartEncounterBattle(EncounterBlueprint encounter, bool isBossBattle)
        {
            List<BattleActorDefinition> actors = encounter.Actors
                .Where(x => !string.Equals(x.ActorId, HeroActorId, StringComparison.Ordinal))
                .ToList();
            actors.Insert(0, CreateHeroActorForClass(_currentDungeonFloor));
            IReadOnlyList<BattleSkillDefinition> battleSkills = BuildBattleSkillList(encounter.Skills);
            List<InventoryDelta> rewardInventory = encounter.RewardInventory.ToList();
            _pendingEncounterGearDropItemId = isBossBattle
                ? null
                : RollEncounterGearDrop(_currentDungeonFloor);
            if (!string.IsNullOrWhiteSpace(_pendingEncounterGearDropItemId))
            {
                rewardInventory.Add(new InventoryDelta(_pendingEncounterGearDropItemId!, 1));
            }
            BattleSnapshot preBattle = CaptureBattleSnapshot(encounter.IntroText);
            _lastEncounterThemeId = encounter.ThemeId;
            _lastEncounterEnemyCount = actors.Count(x => x.Faction == CombatFaction.Enemy);

            DomainResult startResult = Dispatch(new StartBattleCommand(
                battleId: $"battle.floor.{_currentDungeonFloor}.{_battleSequence}",
                actors: actors,
                skills: battleSkills,
                seed: _runSeed + (ulong)(_currentDungeonFloor * 1000) + (ulong)_battleSequence,
                rewardCurrency: encounter.RewardCurrency,
                rewardInventory: rewardInventory,
                rewardLootTableId: encounter.RewardLootTableId));

            if (!startResult.IsSuccess)
            {
                _lastMessage = startResult.Error?.Message ?? "Failed to start battle.";
                return;
            }

            SeedEnemyResistances(encounter);
            _activeBossFloor = isBossBattle ? _currentDungeonFloor : null;
            _activeBattleSnapshot = preBattle;
            CurrentScene = SceneId.Battle;
            _battleLog.Clear();
            PushBattleLog(encounter.IntroText, BattleLogKind.Intro);
            SetMessage(encounter.IntroText, MessageTone.Info);
        }

        private bool IsBossFloorPending()
        {
            return _currentDungeonFloor > 0
                && _currentDungeonFloor % 3 == 0
                && !_clearedBossFloors.Contains(_currentDungeonFloor);
        }

        /// <summary>
        /// Dispatches <see cref="SetStatBaseCommand"/> for each per-actor resistance the encounter
        /// produced. Resistances live on the actor's <see cref="StatBlock"/> (e.g. <c>res.fire = 100</c>
        /// for the Inferno boss) and are read by the engine's damage-type pipeline during skill
        /// resolution. Dispatching commands here — instead of poking the stat block directly —
        /// also exercises the canonical stat-mutation path.
        /// </summary>
        private void SeedEnemyResistances(EncounterBlueprint encounter)
        {
            if (encounter.ActorResistances is null)
            {
                return;
            }

            foreach (KeyValuePair<string, IReadOnlyDictionary<string, int>> actor in encounter.ActorResistances)
            {
                foreach (KeyValuePair<string, int> stat in actor.Value)
                {
                    Dispatch(new SetStatBaseCommand(actor.Key, stat.Key, stat.Value));
                }
            }
        }

        private IReadOnlyList<BattleSkillDefinition> BuildBattleSkillList(IReadOnlyList<BattleSkillDefinition> encounterSkills)
        {
            Dictionary<string, BattleSkillDefinition> merged = new(StringComparer.Ordinal);
            for (int i = 0; i < encounterSkills.Count; i++)
            {
                BattleSkillDefinition skill = encounterSkills[i];
                merged[skill.Id] = skill;
            }

            IReadOnlyList<ClassAbilityDefinition> abilities = GetClassAbilities(_selectedClass.ClassId);
            for (int i = 0; i < abilities.Count; i++)
            {
                ClassAbilityDefinition ability = abilities[i];
                Dictionary<string, int> costs = ability.FocusCost > 0
                    ? new Dictionary<string, int>(StringComparer.Ordinal) { [FocusResourceId] = ability.FocusCost }
                    : new Dictionary<string, int>(StringComparer.Ordinal);
                merged[ability.SkillId] = new BattleSkillDefinition(
                    ability.SkillId,
                    ability.EffectType,
                    ability.Power,
                    ability.CooldownTurns,
                    costs,
                    displayName: ability.Name,
                    damageTypeId: ability.DamageTypeId,
                    targetMode: ability.TargetMode);
            }

            return merged.Values.ToList();
        }

        private static IReadOnlyList<ClassAbilityDefinition> GetClassAbilities(PlayerClass classId)
        {
            return ClassAbilities.Where(x => x.ClassId == classId).ToList();
        }

        private void HandleBattleCompletion()
        {
            // Render the final battle frame so the host sees enemies at 0 HP before the
            // scene transitions away. We use the snapshot captured by the BattleEndedEvent
            // reactor because the engine nulls `ActiveBattle` the moment the command
            // handler that ended the battle returns.
            Moonforge.Core.Combat.BattleState? finalBattle = _finalBattleSnapshot ?? _gameState.ActiveBattle;
            if (finalBattle is not null)
            {
                _host.RenderBattle(new BattleRenderModel(
                    Title: "Battle",
                    Battle: finalBattle,
                    CurrentTurnActorId: null,
                    Controls: BuildBattleControls(),
                    ClassActionInfo: BuildBattleClassActionInfo(),
                    RecentLog: _battleLog.ToArray(),
                    LastMessage: _lastMessage,
                    MessageTone: _lastMessageTone));
            }
            _finalBattleSnapshot = null;

            if (_activeBattleSnapshot is null)
            {
                CurrentScene = _lastBattleStatus == BattleStatus.Victory ? SceneId.Dungeon : SceneId.Town;
                return;
            }

            bool victory = _lastBattleStatus == BattleStatus.Victory;
            if (victory && !string.IsNullOrWhiteSpace(_lastEncounterThemeId) && _lastEncounterEnemyCount > 0)
            {
                Dispatch(new EmitQuestSignalCommand(
                    QuestSignalType.Kill,
                    $"theme.{_lastEncounterThemeId}",
                    _lastEncounterEnemyCount));
            }

            if (_activeBossFloor.HasValue && victory)
            {
                int clearedFloor = _activeBossFloor.Value;
                if (_clearedBossFloors.Add(clearedFloor))
                {
                    PushBattleLog($"Boss of floor {clearedFloor} defeated.", BattleLogKind.Victory);
                    _lastMessage = $"Boss defeated on floor {clearedFloor}. Descent path secured.";
                    _pendingBossReward = BuildBossRewardSnapshot(clearedFloor);
                    SaveProgress("boss floor cleared");
                }
            }

            _postBattleScene = victory ? SceneId.Dungeon : SceneId.Town;
            _pendingBattleSummary = BuildBattleSummary(_activeBattleSnapshot, victory ? "Victory" : "Defeat");
            CurrentScene = SceneId.BattleSummary;
            _lastEncounterThemeId = null;
            _lastEncounterEnemyCount = 0;
            _activeBossFloor = null;
        }

        private void TryBuyPotion()
        {
            DomainResult result = Dispatch(new BuyFromShopCommand(TownShopId, MediumPotionItemId, quantity: 1, priceOptionIndex: 0));
            if (result.IsSuccess)
            {
                SetMessage("Bought one medium potion.", MessageTone.Success);
            }
            else
            {
                SetMessage(result.Error?.Message ?? "Shop transaction failed.", MessageTone.Error);
            }
        }

        private void TrySellHerb()
        {
            int herbCount = _inventoryQuantityQueryHandler.Query(_gameState, new GetInventoryItemQuantityQuery(HerbItemId));
            if (herbCount <= 0)
            {
                SetMessage("You have no herbs to sell.", MessageTone.Warning);
                return;
            }

            DomainResult result = Dispatch(new SellToShopCommand(TownShopId, HerbItemId, quantity: 1));
            if (result.IsSuccess)
            {
                SetMessage("Sold one herb to the town shop.", MessageTone.Success);
            }
            else
            {
                SetMessage(result.Error?.Message ?? "Sell failed.", MessageTone.Error);
            }
        }

        private void TryTownInteract()
        {
            TownInteractionKind? interaction = ResolveTownInteraction();
            if (!interaction.HasValue)
            {
                _lastMessage = "Nothing to interact with here.";
                return;
            }

            RenderTownInteractionMenu(interaction.Value);
            _pendingTownInteraction = interaction.Value;
        }

        private TownInteractionKind? ResolveTownInteraction()
        {
            if (IsHeroAt(_townQuestBoard))
            {
                return TownInteractionKind.QuestBoard;
            }

            if (IsHeroAt(_townShrine))
            {
                return TownInteractionKind.Shrine;
            }

            if (IsHeroAt(_townStairs))
            {
                return TownInteractionKind.DungeonEntrance;
            }

            if (IsHeroAt(_townGuard))
            {
                return TownInteractionKind.Guard;
            }

            if (IsHeroAt(_townAlchemist))
            {
                return TownInteractionKind.Alchemist;
            }

            if (IsHeroAt(_townHealer))
            {
                return TownInteractionKind.Healer;
            }

            if (IsHeroAt(_townCache))
            {
                return TownInteractionKind.Cache;
            }

            if (IsHeroAt(_townFountain))
            {
                return TownInteractionKind.Fountain;
            }

            return null;
        }

        private void RenderTownInteractionMenu(TownInteractionKind kind)
        {
            List<string> lines = BuildTownInteractionLines(kind);
            _host.RenderContractJournal(
                $"Town - {GetTownInteractionTitle(kind)}",
                lines,
                "Press 1/2/3 for actions, Esc to cancel");
        }

        private void ResolvePendingTownInteraction(PlayerAction action)
        {
            TownInteractionKind kind = _pendingTownInteraction!.Value;
            _pendingTownInteraction = null;

            switch (kind)
            {
                case TownInteractionKind.Guard:
                    if (action == PlayerAction.Digit1)
                    {
                        TryTalkToGuard();
                    }
                    else if (action == PlayerAction.Digit2)
                    {
                        _lastMessage = "Guard: A alchemist, H healer, Q board, S shrine, > dungeon.";
                    }
                    break;
                case TownInteractionKind.Alchemist:
                    if (action == PlayerAction.Digit1)
                    {
                        TryBrewAtAlchemist();
                    }
                    else if (action == PlayerAction.Digit2)
                    {
                        TryBuyPotion();
                    }
                    break;
                case TownInteractionKind.Healer:
                    if (action == PlayerAction.Digit1)
                    {
                        TryUseHealer();
                    }
                    break;
                case TownInteractionKind.Cache:
                    if (action == PlayerAction.Digit1)
                    {
                        TryOpenTownCache();
                    }
                    break;
                case TownInteractionKind.Fountain:
                    if (action == PlayerAction.Digit1)
                    {
                        TryInspectFountain();
                    }
                    break;
                case TownInteractionKind.QuestBoard:
                    if (action == PlayerAction.Digit1)
                    {
                        TryQuestBoardPrimaryAction();
                    }
                    else if (action == PlayerAction.Digit2)
                    {
                        TryQuestBoardSecondaryAction();
                    }
                    break;
                case TownInteractionKind.Shrine:
                    if (action == PlayerAction.Digit1)
                    {
                        CurrentScene = SceneId.MetaShrine;
                        _lastMessage = "Shrine: spend tokens to unlock permanent perks.";
                    }
                    break;
                case TownInteractionKind.DungeonEntrance:
                    if (action == PlayerAction.Digit1)
                    {
                        StartDungeonRun();
                    }
                    break;
            }

            // If we're still in town (the choice didn't transition us elsewhere), re-render
            // the town map so the menu UI is dismissed in the Unity host. The console main
            // loop will re-paint via RunTown on its next iteration; calling EnterTown here
            // is a no-op for that path other than a slightly redundant render.
            if (CurrentScene == SceneId.Town)
            {
                EnterTown();
            }
        }

        private List<string> BuildTownInteractionLines(TownInteractionKind kind)
        {
            List<string> lines = new();
            switch (kind)
            {
                case TownInteractionKind.Guard:
                    lines.Add("1. Talk");
                    lines.Add("   Receive patrol tips and dialogue.");
                    lines.Add("2. Ask Directions");
                    lines.Add("   Review town landmark meanings.");
                    lines.Add("3. Leave");
                    break;
                case TownInteractionKind.Alchemist:
                    lines.Add("1. Brew Potion");
                    lines.Add("   Cost: 2 herbs + 5 gold. Success chance: 75%.");
                    lines.Add("2. Buy Potion");
                    lines.Add("   Cost: 18 gold for 1 medium potion.");
                    lines.Add("3. Leave");
                    break;
                case TownInteractionKind.Healer:
                    lines.Add("1. Blessing");
                    lines.Add("   Cost: 8 gold. Reward: 1 token.");
                    lines.Add("2. Leave");
                    break;
                case TownInteractionKind.Cache:
                    lines.Add("1. Search Cache");
                    lines.Add("   One-time stash per run.");
                    lines.Add("2. Leave");
                    break;
                case TownInteractionKind.Fountain:
                    lines.Add("1. Inspect Fountain");
                    lines.Add("   Progresses fountain-related tasks.");
                    lines.Add("2. Leave");
                    break;
                case TownInteractionKind.QuestBoard:
                    BuildQuestBoardMenuLines(lines);
                    break;
                case TownInteractionKind.Shrine:
                    lines.Add("1. Open Shrine of Echoes");
                    lines.Add("   Spend tokens for permanent unlocks.");
                    lines.Add("2. Leave");
                    break;
                case TownInteractionKind.DungeonEntrance:
                    lines.Add("1. Enter Dungeon");
                    lines.Add("   Descend to floor 1.");
                    lines.Add("2. Leave");
                    break;
            }

            return lines;
        }

        private static string GetTownInteractionTitle(TownInteractionKind kind)
        {
            return kind switch
            {
                TownInteractionKind.Guard => "Guard",
                TownInteractionKind.Alchemist => "Alchemist",
                TownInteractionKind.Healer => "Healer",
                TownInteractionKind.Cache => "Cache",
                TownInteractionKind.Fountain => "Fountain",
                TownInteractionKind.QuestBoard => "Quest Board",
                TownInteractionKind.Shrine => "Shrine",
                TownInteractionKind.DungeonEntrance => "Dungeon Entrance",
                _ => "Interaction"
            };
        }

        private void TryTalkToGuard()
        {
            DomainResult result = Dispatch(new StartDialogueCommand(GuardDialogueId));
            if (!result.IsSuccess)
            {
                SetMessage(result.Error?.Message ?? "Guard turns away.", MessageTone.Error);
                return;
            }

            _activeDialogueId = GuardDialogueId;
            _dialogueReturnScene = SceneId.Town;
            CurrentScene = SceneId.Dialogue;
        }

        private List<DialogueChoiceView> ComputeVisibleDialogueChoices()
        {
            if (string.IsNullOrWhiteSpace(_activeDialogueId)
                || !_definitions.TryGetDialogue(_activeDialogueId!, out DialogueDefinition definition)
                || !_gameState.DialogueState.TryGet(_activeDialogueId!, out DialogueInstanceState state)
                || string.IsNullOrWhiteSpace(state.CurrentNodeId))
            {
                return new List<DialogueChoiceView>();
            }

            DialogueNodeDefinition? node = FindNode(definition, state.CurrentNodeId!);
            if (node is null)
            {
                return new List<DialogueChoiceView>();
            }

            IReadOnlyList<string> visibleChoiceIds = _dialogueChoicesQueryHandler.Query(
                _gameState,
                new GetAvailableDialogueChoicesQuery(_activeDialogueId!));
            HashSet<string> visibleSet = new(visibleChoiceIds, StringComparer.Ordinal);

            List<DialogueChoiceView> visibleChoices = new();
            foreach (DialogueChoiceDefinition choice in node.Choices)
            {
                if (!visibleSet.Contains(choice.Id))
                {
                    continue;
                }

                visibleChoices.Add(new DialogueChoiceView(
                    Hotkey: (visibleChoices.Count + 1).ToString(),
                    ChoiceId: choice.Id,
                    Text: ResolveDialogueText(choice.TextKey)));
            }
            return visibleChoices;
        }

        private void EnterDialogue()
        {
            if (string.IsNullOrWhiteSpace(_activeDialogueId)
                || !_definitions.TryGetDialogue(_activeDialogueId!, out DialogueDefinition definition)
                || !_gameState.DialogueState.TryGet(_activeDialogueId!, out DialogueInstanceState state)
                || string.IsNullOrWhiteSpace(state.CurrentNodeId))
            {
                ExitDialogue();
                Enter();
                return;
            }

            DialogueNodeDefinition? node = FindNode(definition, state.CurrentNodeId!);
            if (node is null)
            {
                ExitDialogue();
                Enter();
                return;
            }

            List<DialogueChoiceView> visibleChoices = ComputeVisibleDialogueChoices();
            string body = ResolveDialogueText(node.TextKey);
            _host.RenderDialogue(new DialogueRenderModel(
                NpcName: ResolveNpcName(_activeDialogueId!),
                BodyText: body,
                Choices: visibleChoices,
                Controls: visibleChoices.Count == 0
                    ? "Press any key to step away"
                    : "Press 1-" + visibleChoices.Count + " to choose, Esc to step away"));
        }

        private void TickDialogue(PlayerAction action)
        {
            if (action == PlayerAction.Cancel)
            {
                ExitDialogue();
                return;
            }

            List<DialogueChoiceView> visibleChoices = ComputeVisibleDialogueChoices();
            if (visibleChoices.Count == 0)
            {
                ExitDialogue();
                return;
            }

            int index = action switch
            {
                PlayerAction.Digit1 => 0,
                PlayerAction.Digit2 => 1,
                PlayerAction.Digit3 => 2,
                PlayerAction.Digit4 => 3,
                PlayerAction.Digit5 => 4,
                _ => -1
            };

            if (index < 0 || index >= visibleChoices.Count)
            {
                return;
            }

            DialogueChoiceView selected = visibleChoices[index];
            DomainResult choiceResult = Dispatch(new ChooseDialogueChoiceCommand(_activeDialogueId!, selected.ChoiceId));
            if (!choiceResult.IsSuccess)
            {
                SetMessage(choiceResult.Error?.Message ?? "Choice failed.", MessageTone.Error);
            }
        }

        private static PlayerAction MapDialogueConsoleKey(ConsoleKeyInfo key) => key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => PlayerAction.Digit1,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => PlayerAction.Digit2,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => PlayerAction.Digit3,
            ConsoleKey.D4 or ConsoleKey.NumPad4 => PlayerAction.Digit4,
            ConsoleKey.D5 or ConsoleKey.NumPad5 => PlayerAction.Digit5,
            ConsoleKey.Escape => PlayerAction.Cancel,
            _ => PlayerAction.Confirm // any other key counts as "press any key to step away" when no choices
        };

        private void RunDialogue()
        {
            EnterDialogue();
            if (CurrentScene != SceneId.Dialogue)
            {
                return;
            }
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            TickDialogue(MapDialogueConsoleKey(key));
        }

        private void ExitDialogue()
        {
            _activeDialogueId = null;
            CurrentScene = _dialogueReturnScene;
        }

        private static DialogueNodeDefinition? FindNode(DialogueDefinition definition, string nodeId)
        {
            for (int i = 0; i < definition.Nodes.Count; i++)
            {
                if (definition.Nodes[i].Id == nodeId)
                {
                    return definition.Nodes[i];
                }
            }

            return null;
        }

        private static string ResolveDialogueText(string textKey)
        {
            return DialogueText.TryGetValue(textKey, out string? text) ? text : textKey;
        }

        private static string ResolveNpcName(string dialogueId)
        {
            return dialogueId switch
            {
                GuardDialogueId => "Guard",
                _ => dialogueId
            };
        }

        private void TryInspectFountain()
        {
            // The fountain is an interactable; interacting emits InteractionSignalEvent
            // ("town.fountain.touched"). The quest "Visit fountain" objective still uses the
            // existing QuestSignal mechanism, so we forward it here. Once a host-side reactor
            // bridges interaction signals to quest signals, this manual forward can be removed.
            DomainResult interact = Dispatch(new InteractWithCommand(HeroActorId, FountainInteractableInstanceId));
            if (!interact.IsSuccess)
            {
                _lastMessage = interact.Error?.Message ?? "Could not inspect the fountain.";
                return;
            }

            Dispatch(new EmitQuestSignalCommand(QuestSignalType.Visit, VisitFountainTargetId));
            _lastMessage = "The fountain water is cold and clear. You regain focus.";
        }

        private void TryBrewAtAlchemist()
        {
            int herbCount = _inventoryQuantityQueryHandler.Query(_gameState, new GetInventoryItemQuantityQuery(HerbItemId));
            long gold = _currencyQueryHandler.Query(_gameState, new GetCurrencyBalanceQuery(GoldCurrencyId));
            if (herbCount < 2 || gold < 5)
            {
                SetMessage("Alchemist: Bring me 2 herbs and 5 gold to brew a potion.", MessageTone.Warning);
                return;
            }

            DomainResult result = Dispatch(new AttemptCraftCommand(AlchemistBrewRecipeId, crafterSkill: 1));
            if (!result.IsSuccess)
            {
                SetMessage(result.Error?.Message ?? "Alchemist refused to brew.", MessageTone.Error);
            }
        }

        private void TryUseHealer()
        {
            Dispatch(new EmitQuestSignalCommand(QuestSignalType.Visit, VisitHealerTargetId));
            DomainResult spendResult = Dispatch(new SpendCurrencyCommand(GoldCurrencyId, 8));
            if (!spendResult.IsSuccess)
            {
                _lastMessage = "Healer: Blessing service costs 8 gold.";
                return;
            }

            Dispatch(new GrantCurrencyCommand(TokenCurrencyId, 1));
            _lastMessage = "Healer: You feel renewed. Received 1 token blessing.";
        }

        private void TryOpenTownCache()
        {
            if (IsCacheConsumed())
            {
                _lastMessage = "Cache is empty for now.";
                return;
            }

            DomainResult result = Dispatch(new InteractWithCommand(HeroActorId, CacheInteractableInstanceId));
            if (!result.IsSuccess)
            {
                _lastMessage = result.Error?.Message ?? "Cache is out of reach.";
                return;
            }

            _lastMessage = "You found 12 gold and 2 herbs in the cache.";
        }

        private bool IsCacheConsumed()
        {
            return _gameState.InteractablesState.TryGet(CacheInteractableInstanceId, out InteractableInstance cache)
                && cache.Status == InteractableStatus.Consumed;
        }

        private void BuildQuestBoardMenuLines(List<string> lines)
        {
            QuestDefinition? active = string.IsNullOrWhiteSpace(_activeContractQuestId)
                ? null
                : GetContractDef(_activeContractQuestId!);
            if (active is null)
            {
                lines.Add("1. Take Posted Contract");
                lines.Add("   Accept a random available contract.");
                lines.Add("2. Review Board");
                lines.Add("   View completion status summary.");
                lines.Add("3. Leave");
                return;
            }

            QuestStatus status = _questStatusQueryHandler.Query(_gameState, new GetQuestStatusQuery(active.Id));
            if (status == QuestStatus.Completed)
            {
                lines.Add("1. Turn In Contract");
                long gold = GetCurrencyReward(active, GoldCurrencyId);
                long tokens = GetCurrencyReward(active, TokenCurrencyId);
                lines.Add($"   Reward preview: {gold} gold, {tokens} token(s).");
                lines.Add("2. Review Progress");
                lines.Add("   Check objective completion details.");
                lines.Add("3. Leave");
                return;
            }

            lines.Add("1. Review Active Contract");
            lines.Add("   Check objective progress details.");
            lines.Add("2. Review Board");
            lines.Add("   View completion status summary.");
            lines.Add("3. Leave");
        }

        private void TryQuestBoardPrimaryAction()
        {
            QuestDefinition? active = string.IsNullOrWhiteSpace(_activeContractQuestId)
                ? null
                : GetContractDef(_activeContractQuestId!);
            if (active is null)
            {
                TryQuestBoardTakeContract();
                return;
            }

            QuestStatus status = _questStatusQueryHandler.Query(_gameState, new GetQuestStatusQuery(active.Id));
            if (status == QuestStatus.Completed)
            {
                TryTurnInActiveContract(active);
                return;
            }

            _lastMessage = BuildContractProgressText(active);
        }

        private void TryQuestBoardSecondaryAction()
        {
            QuestDefinition? active = string.IsNullOrWhiteSpace(_activeContractQuestId)
                ? null
                : GetContractDef(_activeContractQuestId!);
            if (active is not null)
            {
                _lastMessage = BuildContractProgressText(active);
                return;
            }

            int turnedIn = 0;
            for (int i = 0; i < ContractQuestIds.Length; i++)
            {
                if (IsQuestRewarded(ContractQuestIds[i]))
                {
                    turnedIn++;
                }
            }
            _lastMessage = $"Quest board: {turnedIn}/{ContractQuestIds.Length} contracts turned in.";
        }

        private void TryQuestBoardInteract()
        {
            if (!string.IsNullOrWhiteSpace(_activeContractQuestId))
            {
                QuestDefinition? active = GetContractDef(_activeContractQuestId!);
                if (active is not null)
                {
                    QuestStatus status = _questStatusQueryHandler.Query(_gameState, new GetQuestStatusQuery(active.Id));
                    if (status == QuestStatus.Completed)
                    {
                        TryTurnInActiveContract(active);
                        return;
                    }

                    _lastMessage = BuildContractProgressText(active);
                    return;
                }
            }

            TryQuestBoardTakeContract();
        }

        private void TryQuestBoardTakeContract()
        {
            List<QuestDefinition> available = new();
            foreach (string questId in ContractQuestIds)
            {
                QuestStatus status = _questStatusQueryHandler.Query(_gameState, new GetQuestStatusQuery(questId));
                if (status == QuestStatus.NotStarted)
                {
                    QuestDefinition? def = GetContractDef(questId);
                    if (def is not null)
                    {
                        available.Add(def);
                    }
                }
            }

            if (available.Count == 0)
            {
                _lastMessage = "Quest board: all posted contracts are complete.";
                return;
            }

            QuestDefinition selected = available[_context.RandomSource.NextInt(available.Count)];
            DomainResult result = Dispatch(new StartQuestCommand(selected.Id));
            if (!result.IsSuccess)
            {
                _lastMessage = result.Error?.Message ?? "Quest board could not start the contract.";
                return;
            }

            _activeContractQuestId = selected.Id;
            _lastMessage = $"New contract: {selected.DisplayName ?? selected.Id} - {selected.Description ?? string.Empty}";
        }

        private void TryTurnInActiveContract(QuestDefinition contract)
        {
            string title = contract.DisplayName ?? contract.Id;
            if (IsQuestRewarded(contract.Id))
            {
                _activeContractQuestId = null;
                SetMessage($"Contract already turned in: {title}.", MessageTone.Info);
                return;
            }

            DomainResult result = Dispatch(new ClaimQuestRewardsCommand(contract.Id));
            if (!result.IsSuccess)
            {
                SetMessage(result.Error?.Message ?? "Could not claim contract rewards.", MessageTone.Error);
                return;
            }

            _contractsReadyForTurnIn.Remove(contract.Id);
            _activeContractQuestId = null;
            long gold = GetCurrencyReward(contract, GoldCurrencyId);
            long tokens = GetCurrencyReward(contract, TokenCurrencyId);
            string message = $"Contract turned in: {title}. Reward {gold} gold, {tokens} token(s).";
            SetMessage(message, MessageTone.Success);
            _pendingContractNotice = new ContractNoticeSnapshot(
                "Contract Turned In",
                $"{title}\n{contract.Description ?? string.Empty}\n\n{message}");
            SaveProgress("contract turned in");

            if (CurrentScene is not SceneId.BattleSummary and not SceneId.ContractNotice)
            {
                _resumeSceneAfterContractNotice = CurrentScene;
                CurrentScene = SceneId.ContractNotice;
            }
        }

        private bool IsQuestRewarded(string questId)
        {
            return _questStatusQueryHandler.Query(_gameState, new GetQuestStatusQuery(questId)) == QuestStatus.Rewarded;
        }

        private QuestDefinition? GetContractDef(string questId)
        {
            return _definitions.TryGetQuest(questId, out QuestDefinition def) ? def : null;
        }

        private string GetContractTitle(string questId)
        {
            return GetContractDef(questId)?.DisplayName ?? questId;
        }

        private string GetContractSummary(string questId)
        {
            return GetContractDef(questId)?.Description ?? string.Empty;
        }

        private static long GetCurrencyReward(QuestDefinition def, string currencyId)
        {
            long total = 0;
            for (int i = 0; i < def.RewardCurrency.Count; i++)
            {
                if (string.Equals(def.RewardCurrency[i].CurrencyId, currencyId, StringComparison.Ordinal))
                {
                    total += def.RewardCurrency[i].Amount;
                }
            }
            return total;
        }

        private static IEnumerable<QuestObjectiveDefinition> GetLeafObjectives(QuestDefinition def)
        {
            for (int i = 0; i < def.Objectives.Count; i++)
            {
                QuestObjectiveDefinition obj = def.Objectives[i];
                if (obj.ObjectiveType != QuestObjectiveType.CompositeAnd && obj.ObjectiveType != QuestObjectiveType.CompositeOr)
                {
                    yield return obj;
                }
            }
        }

        private void TryPurchaseMetaUnlock(int unlockIndex)
        {
            if (unlockIndex < 0 || unlockIndex >= MetaUnlockDefinitions.Length)
            {
                return;
            }

            MetaUnlockDefinition unlock = MetaUnlockDefinitions[unlockIndex];
            if (_unlockedMetaUnlocks.Contains(unlock.Id))
            {
                _lastMessage = $"{unlock.Name} is already unlocked.";
                return;
            }

            DomainResult spendResult = Dispatch(new SpendCurrencyCommand(TokenCurrencyId, unlock.TokenCost));
            if (!spendResult.IsSuccess)
            {
                _lastMessage = spendResult.Error?.Message ?? $"Not enough tokens for {unlock.Name}.";
                return;
            }

            _unlockedMetaUnlocks.Add(unlock.Id);
            ApplyMetaUnlockImmediateEffects(unlock.Id);
            _lastMessage = $"Unlocked {unlock.Name}.";
            SaveProgress("meta unlock purchased");
        }

        private void ApplyMetaUnlockImmediateEffects(MetaUnlockId unlockId)
        {
            switch (unlockId)
            {
                case MetaUnlockId.FieldRations:
                    Dispatch(new AddInventoryItemCommand(MediumPotionItemId, 1));
                    break;
                case MetaUnlockId.DeepPockets:
                    Dispatch(new ConfigureInventoryCapacityCommand(20));
                    break;
            }
        }

        private bool HasMetaUnlock(MetaUnlockId unlockId)
        {
            return _unlockedMetaUnlocks.Contains(unlockId);
        }

        private BossRewardSnapshot BuildBossRewardSnapshot(int floor)
        {
            ulong seed = _runSeed + (ulong)(floor * 7919) + (ulong)(_battleSequence * 2971);
            IRandomSource random = new Pcg32RandomSource(seed, sequence: 777);
            string gearItemId = ResolveBossRewardGearItem(floor, random);
            string? gearName = GetGearDisplayName(gearItemId);
            int goldAmount = 28 + (floor * 7) + random.NextInt(16);
            int tokenAmount = 1 + (floor / 3) + random.NextInt(2);
            List<BossRewardChoice> choices = new()
            {
                new BossRewardChoice(
                    BossRewardKind.Gear,
                    "Forge Cache",
                    $"Receive 1x {(gearName ?? gearItemId)}.",
                    ItemId: gearItemId,
                    Amount: 1),
                new BossRewardChoice(
                    BossRewardKind.Gold,
                    "Royal Purse",
                    $"Receive {goldAmount} gold.",
                    ItemId: null,
                    Amount: goldAmount),
                new BossRewardChoice(
                    BossRewardKind.Tokens,
                    "Arcane Sigils",
                    $"Receive {tokenAmount} token(s).",
                    ItemId: null,
                    Amount: tokenAmount)
            };

            return new BossRewardSnapshot(floor, choices);
        }

        private static string ResolveBossRewardGearItem(int floor, IRandomSource random)
        {
            int roll = random.NextInt(100);
            if (floor <= 3)
            {
                return roll < 40 ? ItemBronzeBlade : roll < 75 ? ItemLeatherVest : ItemIronRing;
            }

            if (floor <= 6)
            {
                return roll < 25 ? ItemBronzeBlade
                    : roll < 50 ? ItemOakWand
                    : roll < 70 ? ItemLeatherVest
                    : roll < 90 ? ItemMysticRobe
                    : ItemLuckyCharm;
            }

            return roll < 30 ? ItemOakWand
                : roll < 55 ? ItemMysticRobe
                : roll < 80 ? ItemLuckyCharm
                : ItemIronRing;
        }

        private bool TryClaimBossRewardChoice(int choiceIndex, bool returnToPostSceneOnSuccess)
        {
            if (_pendingBossReward is null || choiceIndex < 0 || choiceIndex >= _pendingBossReward.Choices.Count)
            {
                return false;
            }

            BossRewardChoice choice = _pendingBossReward.Choices[choiceIndex];
            switch (choice.Kind)
            {
                case BossRewardKind.Gear:
                    if (string.IsNullOrWhiteSpace(choice.ItemId))
                    {
                        _lastMessage = "Boss reward could not be resolved.";
                        return false;
                    }

                    DomainResult gearResult = Dispatch(new AddInventoryItemCommand(choice.ItemId, 1));
                    if (!gearResult.IsSuccess)
                    {
                        long fallbackGold = 20 + (_pendingBossReward.Floor * 4);
                        DomainResult fallbackResult = Dispatch(new GrantCurrencyCommand(GoldCurrencyId, fallbackGold));
                        if (!fallbackResult.IsSuccess)
                        {
                            _lastMessage = gearResult.Error?.Message ?? "Could not claim gear reward.";
                            return false;
                        }

                        _lastMessage = $"Inventory full. Boss reward converted to {fallbackGold} gold.";
                    }
                    else
                    {
                        string? claimedGearName = GetGearDisplayName(choice.ItemId);
                        _lastMessage = $"Claimed boss reward: {claimedGearName ?? choice.ItemId}.";
                    }

                    break;
                case BossRewardKind.Gold:
                    DomainResult goldResult = Dispatch(new GrantCurrencyCommand(GoldCurrencyId, choice.Amount));
                    if (!goldResult.IsSuccess)
                    {
                        _lastMessage = goldResult.Error?.Message ?? "Could not claim gold reward.";
                        return false;
                    }

                    _lastMessage = $"Claimed boss reward: {choice.Amount} gold.";
                    break;
                case BossRewardKind.Tokens:
                    DomainResult tokenResult = Dispatch(new GrantCurrencyCommand(TokenCurrencyId, choice.Amount));
                    if (!tokenResult.IsSuccess)
                    {
                        _lastMessage = tokenResult.Error?.Message ?? "Could not claim token reward.";
                        return false;
                    }

                    _lastMessage = $"Claimed boss reward: {choice.Amount} token(s).";
                    break;
                default:
                    return false;
            }

            _pendingBossReward = null;
            if (!returnToPostSceneOnSuccess)
            {
                return true;
            }

            if (_pendingContractNotice is not null)
            {
                _resumeSceneAfterContractNotice = _postBattleScene;
                CurrentScene = SceneId.ContractNotice;
            }
            else
            {
                CurrentScene = _postBattleScene;
            }

            SaveProgress("boss reward claimed");
            return true;
        }

        private string BuildContractProgressText(QuestDefinition contract)
        {
            string title = contract.DisplayName ?? contract.Id;
            QuestStatus status = _questStatusQueryHandler.Query(_gameState, new GetQuestStatusQuery(contract.Id));
            if (status == QuestStatus.Rewarded)
            {
                return $"Contract turned in: {title}.";
            }

            if (status == QuestStatus.Completed)
            {
                return $"Contract ready to turn in: {title}. Interact with the quest board.";
            }

            if (status != QuestStatus.Active)
            {
                return $"Contract is not active: {title}.";
            }

            List<string> segments = new();
            foreach (QuestObjectiveDefinition objective in GetLeafObjectives(contract))
            {
                int progress = _questObjectiveProgressQueryHandler.Query(
                    _gameState,
                    new GetQuestObjectiveProgressQuery(contract.Id, objective.Id));
                int clamped = Math.Min(progress, objective.RequiredCount);
                segments.Add($"{objective.DisplayName ?? objective.Id} {clamped}/{objective.RequiredCount}");
            }

            return $"Contract progress [{title}] {string.Join(", ", segments)}";
        }

        private string BuildActiveContractHudText()
        {
            if (string.IsNullOrWhiteSpace(_activeContractQuestId))
            {
                return "None";
            }

            QuestDefinition? active = GetContractDef(_activeContractQuestId);
            if (active is null)
            {
                return "None";
            }

            string title = active.DisplayName ?? active.Id;
            QuestStatus status = _questStatusQueryHandler.Query(_gameState, new GetQuestStatusQuery(active.Id));
            if (status == QuestStatus.Rewarded)
            {
                return $"{title} (Turned In)";
            }

            if (status == QuestStatus.Completed)
            {
                return $"{title} (Turn in at board)";
            }

            if (status != QuestStatus.Active)
            {
                return "None";
            }

            int done = 0;
            int total = 0;
            foreach (QuestObjectiveDefinition objective in GetLeafObjectives(active))
            {
                total++;
                int progress = _questObjectiveProgressQueryHandler.Query(
                    _gameState,
                    new GetQuestObjectiveProgressQuery(active.Id, objective.Id));
                if (progress >= objective.RequiredCount)
                {
                    done++;
                }
            }

            return $"{title} ({done}/{total})";
        }

        private bool TryEnterPendingContractNotice(SceneId resumeScene)
        {
            if (_pendingContractNotice is null)
            {
                return false;
            }

            _resumeSceneAfterContractNotice = resumeScene;
            CurrentScene = SceneId.ContractNotice;
            return true;
        }

        private string? RollEncounterGearDrop(int floor)
        {
            int dropChance = Math.Min(60, 10 + (floor * 3) + (HasMetaUnlock(MetaUnlockId.LuckyFinds) ? 10 : 0));
            if (_context.RandomSource.NextInt(100) >= dropChance)
            {
                return null;
            }

            int roll = _context.RandomSource.NextInt(100);
            if (floor <= 2)
            {
                return roll < 40 ? ItemBronzeBlade : roll < 75 ? ItemLeatherVest : ItemIronRing;
            }

            if (floor <= 5)
            {
                return roll < 25 ? ItemBronzeBlade
                    : roll < 50 ? ItemOakWand
                    : roll < 70 ? ItemLeatherVest
                    : roll < 90 ? ItemMysticRobe
                    : ItemLuckyCharm;
            }

            return roll < 20 ? ItemOakWand
                : roll < 40 ? ItemBronzeBlade
                : roll < 65 ? ItemMysticRobe
                : roll < 85 ? ItemLuckyCharm
                : ItemIronRing;
        }


        private void TryUseBattlePotion(BattleActorState turnActor)
        {
            if (turnActor.Hp >= turnActor.MaxHp)
            {
                _lastMessage = "You are already at full HP.";
                return;
            }

            int potions = _inventoryQuantityQueryHandler.Query(_gameState, new GetInventoryItemQuantityQuery(MediumPotionItemId));
            if (potions <= 0)
            {
                _lastMessage = "No medium potions available.";
                return;
            }

            DomainResult consumeResult = Dispatch(new ConsumeInventoryItemCommand(MediumPotionItemId, 1));
            if (!consumeResult.IsSuccess)
            {
                _lastMessage = consumeResult.Error?.Message ?? "Could not consume potion.";
                return;
            }

            DomainResult skillResult = Dispatch(new UseBattleSkillCommand(HeroActorId, "skill.potion", HeroActorId));
            if (skillResult.IsSuccess)
            {
                return;
            }

            Dispatch(new AddInventoryItemCommand(MediumPotionItemId, 1));
            _lastMessage = skillResult.Error?.Message ?? "Potion action failed.";
        }

        private void HandleMovementAction(PlayerAction action)
        {
            int deltaX = 0;
            int deltaY = 0;
            switch (action)
            {
                case PlayerAction.MoveNorth:
                    deltaY = -1;
                    HeroFacing = FacingDirection.Up;
                    break;
                case PlayerAction.MoveSouth:
                    deltaY = 1;
                    HeroFacing = FacingDirection.Down;
                    break;
                case PlayerAction.MoveWest:
                    deltaX = -1;
                    HeroFacing = FacingDirection.Left;
                    break;
                case PlayerAction.MoveEast:
                    deltaX = 1;
                    HeroFacing = FacingDirection.Right;
                    break;
                default:
                    return;
            }

            GridPosition? current = GetHeroPosition();
            if (!current.HasValue)
            {
                _lastMessage = "Hero position unavailable.";
                return;
            }

            int targetX = current.Value.X + deltaX;
            int targetY = current.Value.Y + deltaY;
            bool canMove = _canMoveActorQueryHandler.Query(_gameState, new CanMoveActorQuery(HeroActorId, targetX, targetY));
            if (!canMove)
            {
                _lastMessage = "Movement blocked.";
                return;
            }

            DomainResult moveResult = Dispatch(new MoveActorCommand(HeroActorId, deltaX, deltaY));
            if (!moveResult.IsSuccess)
            {
                _lastMessage = moveResult.Error?.Message ?? "Unable to move.";
                return;
            }

            _lastMessage = string.Empty;
        }

        private GridPosition? GetHeroPosition()
        {
            return _actorPositionQueryHandler.Query(_gameState, new GetExplorationActorPositionQuery(HeroActorId));
        }

        private bool IsHeroAt(GridPosition target)
        {
            GridPosition? heroPosition = GetHeroPosition();
            return heroPosition.HasValue && heroPosition.Value.X == target.X && heroPosition.Value.Y == target.Y;
        }

        private string? ResolveAttackTarget()
        {
            if (_gameState.ActiveBattle is null)
            {
                return null;
            }

            return _gameState.ActiveBattle.Actors.Values
                .Where(x => x.Faction == CombatFaction.Enemy && !x.IsDowned)
                .OrderBy(x => x.Hp)
                .ThenBy(x => x.ActorId, StringComparer.Ordinal)
                .Select(x => x.ActorId)
                .FirstOrDefault();
        }

        private BattleActorDefinition CreateHeroActorForClass(int floor)
        {
            // Ensure the hero's StatBlock reflects the class bases, meta-unlocks, and currently
            // equipped gear. Each call is idempotent — re-seeding will not stack modifiers.
            SeedHeroStatBlock();

            // Floor- and level-based scaling are recomputed for every encounter; clear the
            // previous battle's transient modifiers before re-applying for this depth.
            StatBlock block = _gameState.ActorStatsState.GetOrCreate(HeroActorId);
            block.RemoveModifiersBySource(StatSourceEncounter, StatSourceIdDepth);
            block.RemoveModifiersBySource(StatSourceProgression, StatSourceIdLevel);

            int heroLevel = _gameState.ProgressionState.TryGet(HeroActorId, out ActorProgression heroProg) ? heroProg.Level : 1;
            int levelBonus = heroLevel - 1; // level 1 is the baseline
            AddFlatMod(block, StandardStats.MaxHp, floor, StatSourceEncounter, StatSourceIdDepth);
            AddFlatMod(block, StandardStats.Attack, floor / 3, StatSourceEncounter, StatSourceIdDepth);
            AddFlatMod(block, StandardStats.Defense, floor / 4, StatSourceEncounter, StatSourceIdDepth);
            AddFlatMod(block, StandardStats.MagicAttack, floor / 3, StatSourceEncounter, StatSourceIdDepth);
            AddFlatMod(block, StandardStats.MagicDefense, floor / 4, StatSourceEncounter, StatSourceIdDepth);
            AddFlatMod(block, StandardStats.Initiative, floor / 6, StatSourceEncounter, StatSourceIdDepth);
            AddFlatMod(block, StandardStats.MaxHp, levelBonus * 4, StatSourceProgression, StatSourceIdLevel);
            AddFlatMod(block, StandardStats.Attack, levelBonus, StatSourceProgression, StatSourceIdLevel);
            AddFlatMod(block, StandardStats.Defense, levelBonus / 2, StatSourceProgression, StatSourceIdLevel);
            AddFlatMod(block, StandardStats.MagicAttack, levelBonus, StatSourceProgression, StatSourceIdLevel);
            AddFlatMod(block, StandardStats.MagicDefense, levelBonus / 2, StatSourceProgression, StatSourceIdLevel);

            int hp = block.Get(StandardStats.MaxHp, _definitions, _context.FormulaEvaluator);
            int atk = block.Get(StandardStats.Attack, _definitions, _context.FormulaEvaluator);
            int def = block.Get(StandardStats.Defense, _definitions, _context.FormulaEvaluator);
            int matk = block.Get(StandardStats.MagicAttack, _definitions, _context.FormulaEvaluator);
            int mdef = block.Get(StandardStats.MagicDefense, _definitions, _context.FormulaEvaluator);
            int initiative = block.Get(StandardStats.Initiative, _definitions, _context.FormulaEvaluator);

            List<string> skillIds = new()
            {
                _selectedClass.BasicSkillId,
                "skill.potion"
            };
            IReadOnlyList<ClassAbilityDefinition> abilities = GetClassAbilities(_selectedClass.ClassId);
            for (int i = 0; i < abilities.Count; i++)
            {
                skillIds.Add(abilities[i].SkillId);
            }

            // Equipment-granted skills come last so the basic attack stays in slot 0. Dedupe
            // because a granted skill might already be a class ability.
            IReadOnlyList<string> grantedSkills = new GetEquipmentGrantedSkillsQueryHandler(_definitions)
                .Query(_gameState, new GetEquipmentGrantedSkillsQuery());
            for (int i = 0; i < grantedSkills.Count; i++)
            {
                if (!skillIds.Contains(grantedSkills[i]))
                {
                    skillIds.Add(grantedSkills[i]);
                }
            }

            Dictionary<string, int> focusMaxes = new(StringComparer.Ordinal) { [FocusResourceId] = MaxFocus };
            Dictionary<string, int> focusRefresh = new(StringComparer.Ordinal) { [FocusResourceId] = 1 };
            return new BattleActorDefinition(
                actorId: HeroActorId,
                displayName: _selectedClass.Name,
                faction: CombatFaction.Party,
                maxHp: hp,
                atk: atk,
                def: def,
                matk: matk,
                mdef: mdef,
                initiative: initiative,
                skillIds: skillIds,
                playerControlled: true,
                resourceMaxes: focusMaxes,
                startingResources: focusMaxes,
                resourceRefreshPerTurn: focusRefresh);
        }

        private const string StatSourceMetaUnlock = "meta_unlock";
        private const string StatSourceIdCombatDrills = "combat_drills";
        private const string StatSourceProgression = "progression";
        private const string StatSourceIdLevel = "level";
        private const string StatSourceEncounter = "encounter";
        private const string StatSourceIdDepth = "depth";

        /// <summary>
        /// Establishes the hero's <see cref="StatBlock"/> with stored class bases plus
        /// non-equipment modifiers (meta-unlocks). Equipment modifiers are re-derived from the
        /// current <see cref="EquipmentState"/> so the call is safe after loading a save where
        /// the engine snapshot may not have included actor stats.
        /// </summary>
        private void SeedHeroStatBlock()
        {
            StatBlock block = _gameState.ActorStatsState.GetOrCreate(HeroActorId);
            // Set Vitality (the stored primary stat). MaxHp is registered as derived from
            // "vit + (level - 1) * 4" so it scales automatically as the hero levels up.
            block.SetBase(StandardStats.Vitality, _selectedClass.MaxHpBase);
            block.SetBase(StandardStats.Attack, _selectedClass.AtkBase);
            block.SetBase(StandardStats.Defense, _selectedClass.DefBase);
            block.SetBase(StandardStats.MagicAttack, _selectedClass.MatkBase);
            block.SetBase(StandardStats.MagicDefense, _selectedClass.MdefBase);
            block.SetBase(StandardStats.Initiative, _selectedClass.InitiativeBase);

            block.RemoveModifiersBySource(StatSourceMetaUnlock, StatSourceIdCombatDrills);
            if (HasMetaUnlock(MetaUnlockId.CombatDrills))
            {
                AddFlatMod(block, StandardStats.Attack, 2, StatSourceMetaUnlock, StatSourceIdCombatDrills);
                AddFlatMod(block, StandardStats.MagicAttack, 2, StatSourceMetaUnlock, StatSourceIdCombatDrills);
                AddFlatMod(block, StandardStats.Defense, 1, StatSourceMetaUnlock, StatSourceIdCombatDrills);
            }

            // Re-derive equipment modifiers from current EquipmentState. This is a no-op for
            // freshly-equipped gear (handlers already pushed the modifiers) but recovers state
            // when loading a save from before ActorStatsState existed in the snapshot.
            foreach (KeyValuePair<string, string> equipped in _gameState.EquipmentState.EquippedItems)
            {
                block.RemoveModifiersBySource(EquipmentStatSource.Kind, EquipmentStatSource.Id(equipped.Key, equipped.Value));
            }

            foreach (KeyValuePair<string, string> equipped in _gameState.EquipmentState.EquippedItems)
            {
                if (!_definitions.TryGetEquipment(equipped.Value, out EquipmentDefinition gear))
                {
                    continue;
                }

                string sourceId = EquipmentStatSource.Id(equipped.Key, equipped.Value);
                foreach (KeyValuePair<string, int> bonus in gear.StatBonuses)
                {
                    AddFlatMod(block, bonus.Key, bonus.Value, EquipmentStatSource.Kind, sourceId);
                }
            }
        }

        private static void AddFlatMod(StatBlock block, string statId, int value, string sourceKind, string sourceId)
        {
            if (value == 0)
            {
                return;
            }

            block.AddModifier(new StatModifier(statId, StatModifierBucket.Flat, value, sourceKind, sourceId));
        }

        /// <summary>
        /// Places the town's interactables when the map is built. Each placement is idempotent:
        /// if an instance already exists (from a loaded save), it's left alone so persisted state
        /// (looted/unlooted, locked/unlocked) survives round-trips.
        /// </summary>
        private void PlaceTownInteractables(GridPosition cachePosition, GridPosition fountainPosition)
        {
            if (!_gameState.InteractablesState.TryGet(CacheInteractableInstanceId, out _))
            {
                Dispatch(new PlaceInteractableCommand(CacheInteractableInstanceId, CacheInteractableDefId, cachePosition));
            }

            if (!_gameState.InteractablesState.TryGet(FountainInteractableInstanceId, out _))
            {
                Dispatch(new PlaceInteractableCommand(FountainInteractableInstanceId, FountainInteractableDefId, fountainPosition));
            }
        }


        private BattleSnapshot CaptureBattleSnapshot(string encounterTitle)
        {
            return new BattleSnapshot(
                encounterTitle,
                _currencyQueryHandler.Query(_gameState, new GetCurrencyBalanceQuery(GoldCurrencyId)),
                _currencyQueryHandler.Query(_gameState, new GetCurrencyBalanceQuery(TokenCurrencyId)),
                _inventoryQuantityQueryHandler.Query(_gameState, new GetInventoryItemQuantityQuery(MediumPotionItemId)),
                _inventoryQuantityQueryHandler.Query(_gameState, new GetInventoryItemQuantityQuery(HerbItemId)));
        }

        private BattleSummarySnapshot BuildBattleSummary(BattleSnapshot before, string outcome)
        {
            long goldAfter = _currencyQueryHandler.Query(_gameState, new GetCurrencyBalanceQuery(GoldCurrencyId));
            long tokensAfter = _currencyQueryHandler.Query(_gameState, new GetCurrencyBalanceQuery(TokenCurrencyId));
            int potionsAfter = _inventoryQuantityQueryHandler.Query(_gameState, new GetInventoryItemQuantityQuery(MediumPotionItemId));
            int herbsAfter = _inventoryQuantityQueryHandler.Query(_gameState, new GetInventoryItemQuantityQuery(HerbItemId));
            IReadOnlyList<string> bossRewardOptions = _pendingBossReward is null
                ? Array.Empty<string>()
                : _pendingBossReward.Choices
                    .Select((choice, index) => $"{index + 1}. {choice.Label} - {choice.Description}")
                    .ToArray();
            return new BattleSummarySnapshot(
                outcome,
                before.EncounterTitle,
                before.Gold,
                goldAfter,
                before.Tokens,
                tokensAfter,
                before.Potions,
                potionsAfter,
                before.Herbs,
                herbsAfter,
                _battleLog.ToArray(),
                bossRewardOptions,
                BossRewardChosen: null);
        }

        private DomainResult Dispatch<TCommand>(TCommand command) where TCommand : ICommand
        {
            DomainResult result = _dispatcher.Dispatch(_gameState, command, _context);
            ProcessNewEvents();
            return result;
        }

        private void ProcessNewEvents()
        {
            foreach (DomainEvent domainEvent in _eventSink.DrainNewEvents())
            {
                switch (domainEvent)
                {
                    case BattleActionResolvedEvent action:
                        string actionText;
                        BattleLogKind actionKind;
                        if (action.WasHeal)
                        {
                            actionText = $"{ResolveDisplayName(action.ActorId)} heals {ResolveDisplayName(action.TargetActorId)} for {action.Amount}.";
                            actionKind = BattleLogKind.Heal;
                        }
                        else if (action.Amount == 0)
                        {
                            // Engine's damage pipeline returns exactly 0 only when the target's
                            // resistance for the skill's damage type is ≥ 100 (hard immunity).
                            actionText = $"{ResolveDisplayName(action.TargetActorId)} is immune to {ResolveSkillName(action.SkillId)}!";
                            actionKind = BattleLogKind.Info;
                        }
                        else if (action.WasCritical)
                        {
                            actionText = $"{ResolveDisplayName(action.ActorId)} lands a critical hit on {ResolveDisplayName(action.TargetActorId)} for {action.Amount}!";
                            actionKind = BattleLogKind.Damage;
                        }
                        else
                        {
                            actionText = $"{ResolveDisplayName(action.ActorId)} hits {ResolveDisplayName(action.TargetActorId)} for {action.Amount}.";
                            actionKind = BattleLogKind.Damage;
                        }

                        SetMessage(actionText, actionKind == BattleLogKind.Heal ? MessageTone.Success : MessageTone.Info);
                        PushBattleLog(actionText, actionKind);
                        break;
                    case BattleActionMissedEvent missed:
                        {
                            string missText = $"{ResolveDisplayName(missed.ActorId)}'s {ResolveSkillName(missed.SkillId)} misses {ResolveDisplayName(missed.TargetActorId)}.";
                            SetMessage(missText, MessageTone.Warning);
                            PushBattleLog(missText, BattleLogKind.Info);
                        }
                        break;
                    case BattleEndedEvent ended:
                        _lastBattleStatus = ended.Status;
                        // ActiveBattle is already null by the time this reactor runs (the
                        // engine command handler nulls it before reactors fire — confirmed
                        // by BattleEndedEvent's docs). Build the final snapshot from our
                        // most-recently-rendered clone, overlaying FinalActorHp/MaxHp from
                        // the event payload so dead enemies show up at HP=0.
                        if (_liveBattleClone is not null)
                        {
                            foreach (KeyValuePair<string, int> hpEntry in ended.FinalActorHp)
                            {
                                if (_liveBattleClone.TryGetActor(hpEntry.Key, out Moonforge.Core.Combat.BattleActorState actorRef))
                                {
                                    actorRef.Hp = hpEntry.Value;
                                }
                            }
                            foreach (KeyValuePair<string, int> maxEntry in ended.FinalActorMaxHp)
                            {
                                if (_liveBattleClone.TryGetActor(maxEntry.Key, out Moonforge.Core.Combat.BattleActorState actorRef))
                                {
                                    actorRef.MaxHp = maxEntry.Value;
                                }
                            }
                            _finalBattleSnapshot = _liveBattleClone;
                            _liveBattleClone = null;
                        }
                        bool victory = ended.Status == BattleStatus.Victory;
                        string endText = victory ? "Battle won. Rewards applied." : "Battle lost.";
                        if (victory && !string.IsNullOrWhiteSpace(_pendingEncounterGearDropItemId))
                        {
                            string? droppedName = GetGearDisplayName(_pendingEncounterGearDropItemId!);
                            if (droppedName is not null)
                            {
                                endText += $" Loot found: {droppedName}.";
                            }
                        }
                        SetMessage(endText, victory ? MessageTone.Success : MessageTone.Error);
                        PushBattleLog(endText, victory ? BattleLogKind.Victory : BattleLogKind.Defeat);
                        _pendingEncounterGearDropItemId = null;
                        break;
                    case QuestCompletedEvent completed:
                        HandleContractCompletion(completed.QuestId);
                        break;
                    case DialogueCompletedEvent dlgDone when dlgDone.DialogueId == _activeDialogueId:
                        ExitDialogue();
                        break;
                    case StatusAppliedEvent statusApplied:
                        {
                            string actorName = ResolveDisplayName(statusApplied.ActorId);
                            string statusLabel = ResolveStatusLabel(statusApplied.StatusId);
                            bool selfApplied = string.Equals(statusApplied.SourceActorId, statusApplied.ActorId, StringComparison.Ordinal);
                            string msg = selfApplied
                                ? $"{actorName} draws on {statusLabel}."
                                : $"{actorName} is afflicted with {statusLabel}.";
                            SetMessage(msg, selfApplied ? MessageTone.Info : MessageTone.Warning);
                            PushBattleLog(msg, BattleLogKind.Info);
                        }
                        break;
                    case StatusExpiredEvent statusExpired:
                        {
                            string actorName = ResolveDisplayName(statusExpired.ActorId);
                            string statusLabel = ResolveStatusLabel(statusExpired.StatusId);
                            string msg = $"{actorName} shakes off {statusLabel}.";
                            PushBattleLog(msg, BattleLogKind.Info);
                        }
                        break;
                    case StatusTickedEvent statusTicked when statusTicked.HpDelta != 0:
                        {
                            string actorName = ResolveDisplayName(statusTicked.ActorId);
                            string statusLabel = ResolveStatusLabel(statusTicked.StatusId);
                            int amount = Math.Abs(statusTicked.HpDelta);
                            bool isHeal = statusTicked.HpDelta > 0;
                            string msg = isHeal
                                ? $"{actorName} regains {amount} HP from {statusLabel}."
                                : $"{actorName} takes {amount} damage from {statusLabel}.";
                            PushBattleLog(msg, isHeal ? BattleLogKind.Heal : BattleLogKind.Damage);
                        }
                        break;
                    case StatusPreventedActionEvent prevented:
                        {
                            string actorName = ResolveDisplayName(prevented.ActorId);
                            string statusLabel = ResolveStatusLabel(prevented.StatusId);
                            string msg = $"{actorName} cannot act ({statusLabel}).";
                            SetMessage(msg, MessageTone.Warning);
                            PushBattleLog(msg, BattleLogKind.Info);
                        }
                        break;
                    case ExperienceGrantedEvent xpGranted:
                        {
                            string actorName = ResolveDisplayName(xpGranted.ActorId);
                            string msg = $"{actorName} gained {xpGranted.Amount} XP (level {xpGranted.Level}).";
                            PushBattleLog(msg, BattleLogKind.Info);
                        }
                        break;
                    case LevelUpEvent levelUp:
                        {
                            string actorName = ResolveDisplayName(levelUp.ActorId);
                            string msg = $"{actorName} reached level {levelUp.ToLevel}!";
                            SetMessage(msg, MessageTone.Success);
                            PushBattleLog(msg, BattleLogKind.Victory);
                        }
                        break;
                    case CurrencyOverflowClampedEvent overflow:
                        SetMessage($"{overflow.CurrencyId} capped at {overflow.ClampedTo}.", MessageTone.Warning);
                        break;
                    case WarningEvent warning:
                        SetMessage($"Warning ({warning.Code}): {warning.Message}", MessageTone.Warning);
                        break;
                    case CraftAttemptedEvent craft when craft.RecipeId == AlchemistBrewRecipeId:
                        if (craft.Success)
                        {
                            SetMessage("Alchemist: Fresh batch ready. You got 1 medium potion.", MessageTone.Success);
                        }
                        else
                        {
                            SetMessage("Alchemist: Batch spoiled. Materials consumed.", MessageTone.Error);
                        }
                        break;
                }
            }
        }

        private string ResolveDisplayName(string actorId)
        {
            if (_gameState.ActiveBattle is not null
                && _gameState.ActiveBattle.TryGetActor(actorId, out BattleActorState actor))
            {
                return actor.DisplayName;
            }

            return actorId;
        }

        private string ResolveStatusLabel(string statusId)
        {
            if (_definitions.TryGetStatusEffect(statusId, out StatusEffectDefinition def))
            {
                return def.DisplayName ?? statusId;
            }

            return statusId;
        }

        private string ResolveSkillName(string skillId)
        {
            if (_gameState.ActiveBattle is not null
                && _gameState.ActiveBattle.Skills.TryGetValue(skillId, out BattleSkillDefinition? skill)
                && skill is not null
                && !string.IsNullOrWhiteSpace(skill.DisplayName))
            {
                return skill.DisplayName!;
            }

            int lastDot = skillId.LastIndexOf('.');
            return lastDot >= 0 && lastDot < skillId.Length - 1 ? skillId.Substring(lastDot + 1) : skillId;
        }

        private void HandleContractCompletion(string questId)
        {
            QuestDefinition? contract = GetContractDef(questId);
            if (contract is null)
            {
                return;
            }

            if (IsQuestRewarded(questId) || !_contractsReadyForTurnIn.Add(questId))
            {
                return;
            }

            string title = contract.DisplayName ?? contract.Id;
            string message = $"Contract complete: {title}. Return to the quest board to turn it in.";
            _lastMessage = message;
            _pendingContractNotice = new ContractNoticeSnapshot(
                "Contract Ready",
                $"{title}\n{contract.Description ?? string.Empty}\n\n{message}");

            if (_gameState.ActiveBattle is null && CurrentScene is not SceneId.BattleSummary and not SceneId.ContractNotice)
            {
                _resumeSceneAfterContractNotice = CurrentScene;
                CurrentScene = SceneId.ContractNotice;
            }
        }

        private void PushBattleLog(string line, BattleLogKind kind = BattleLogKind.Info)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            _battleLog.Enqueue(new BattleLogEntry(line, kind));
            while (_battleLog.Count > 5)
            {
                _battleLog.Dequeue();
            }
        }

        private void SetMessage(string message, MessageTone tone = MessageTone.Info)
        {
            _lastMessage = message;
            _lastMessageTone = tone;
        }

        private CommandContext CreateContext(ulong seed, InMemoryDomainEventSink sink)
        {
            return CreateContext(new Pcg32RandomSource(seed, sequence: 54), sink);
        }

        private CommandContext CreateContext(IRandomSource randomSource, InMemoryDomainEventSink sink)
        {
            return new CommandContext(
                randomSource,
                new SimulationClock(0),
                new ExpressionFormulaEvaluator(),
                sink,
                _definitions);
        }

        /// <summary>
        /// Dispatches a <see cref="SetWorldVariableCommand"/> so the deepest-floor counter is part
        /// of the engine's <see cref="WorldState"/> and rides along automatically in save snapshots.
        /// </summary>
        private void RecordDeepestFloor(int floor)
        {
            int current = GetDeepestFloor();
            if (floor <= current)
            {
                return;
            }

            Dispatch(new SetWorldVariableCommand(WorldVarDeepestFloor, WorldVariableValue.FromInt(floor)));
        }

        private int GetDeepestFloor()
        {
            WorldVariableValue? value = _worldVariableQueryHandler.Query(
                _gameState,
                new GetWorldVariableQuery(WorldVarDeepestFloor));
            return value is not null && value.TryGetInt(out int floor) ? floor : 0;
        }

        private bool TryPeekContinueRun()
        {
            if (!_saveStore.TryLoad(out RoguelikeSaveFile? saveFile, out _))
            {
                return false;
            }

            return saveFile?.Run is not null;
        }

        private void LoadMetaUnlocksFromSave()
        {
            if (!_saveStore.TryLoad(out RoguelikeSaveFile? saveFile, out _))
            {
                return;
            }

            _unlockedMetaUnlocks.Clear();
            if (saveFile is null)
            {
                return;
            }

            foreach (string id in saveFile.UnlockedMetaUnlockIds)
            {
                if (Enum.TryParse(id, ignoreCase: true, out MetaUnlockId unlock))
                {
                    _unlockedMetaUnlocks.Add(unlock);
                }
            }
        }

        private void SaveProgress(string reason)
        {
            RoguelikeRunSaveData? run = BuildRunSaveData();
            RoguelikeSaveFile saveFile = new(
                SaveSchemaVersion,
                _unlockedMetaUnlocks.Select(x => x.ToString()).ToList(),
                run);
            if (_saveStore.TrySave(saveFile, out string? error))
            {
                return;
            }

            _lastMessage = $"Autosave failed ({reason}): {error}";
        }

        private RoguelikeRunSaveData? BuildRunSaveData()
        {
            if (_runSeed == 0 || !_gameState.ExplorationState.Map.IsConfigured)
            {
                return null;
            }

            GridPosition heroPosition = GetHeroPosition() ?? new GridPosition(1, 1);
            Dictionary<int, DungeonFloorSaveData> floors = _dungeonFloors.ToDictionary(
                x => x.Key,
                x => DungeonFloorSaveMapper.ToSaveData(x.Value));
            string resumeScene = _currentDungeonFloor > 0 ? SceneId.Dungeon.ToString() : SceneId.Town.ToString();

            // Capture the live RNG stream position so random draws resume exactly where they
            // left off after Continue — recreating the source from _runSeed would rewind it.
            GameStateSnapshot engineSnapshot = GameStateSnapshotMapper.Capture(
                _gameState,
                _context.RandomSource as Pcg32RandomSource);
            string engineJson = _saveStore.SerializeEngineSnapshot(engineSnapshot);

            return new RoguelikeRunSaveData(
                _runSeed,
                _currentDungeonFloor,
                _battleSequence,
                _selectedClass.ClassId.ToString(),
                _activeContractQuestId,
                _contractsReadyForTurnIn.ToList(),
                _clearedBossFloors.ToList(),
                heroPosition.X,
                heroPosition.Y,
                resumeScene,
                _lastMessage,
                _pendingBossReward?.Floor,
                floors,
                engineJson);
        }

        private bool TryContinueSavedRun(out string? error)
        {
            error = null;
            if (!_saveStore.TryLoad(out RoguelikeSaveFile? saveFile, out string? loadError))
            {
                error = loadError ?? "No save file available.";
                return false;
            }

            if (saveFile is null)
            {
                error = "Save file was empty.";
                return false;
            }

            _unlockedMetaUnlocks.Clear();
            foreach (string id in saveFile.UnlockedMetaUnlockIds)
            {
                if (Enum.TryParse(id, ignoreCase: true, out MetaUnlockId unlock))
                {
                    _unlockedMetaUnlocks.Add(unlock);
                }
            }

            if (saveFile.Run is null)
            {
                error = "Save file has no active run to continue.";
                return false;
            }

            return TryApplyRunSaveData(saveFile.Run, out error);
        }

        private bool TryApplyRunSaveData(RoguelikeRunSaveData run, out string? error)
        {
            error = null;
            if (!Enum.TryParse(run.SelectedClass, ignoreCase: true, out PlayerClass savedClass))
            {
                error = $"Unknown saved class '{run.SelectedClass}'.";
                return false;
            }

            _selectedClass = ClassProfiles.FirstOrDefault(x => x.ClassId == savedClass) ?? ClassProfiles[0];
            _runSeed = run.RunSeed;
            _gameState = new GameState();
            _eventSink = new InMemoryDomainEventSink();
            _context = CreateContext(_runSeed, _eventSink);
            HeroFacing = FacingDirection.Down;
            _lastBattleStatus = null;
            _activeBattleSnapshot = null;
            _pendingBattleSummary = null;
            _pendingContractNotice = null;
            _pendingBossReward = null;
            _activeBossFloor = null;
            _lastEncounterThemeId = null;
            _lastEncounterEnemyCount = 0;
            _pendingEncounterGearDropItemId = null;
            _battleLog.Clear();

            _currentDungeonFloor = run.CurrentDungeonFloor;
            _battleSequence = run.BattleSequence;
            _activeContractQuestId = run.ActiveContractQuestId;
            _contractsReadyForTurnIn.Clear();
            foreach (string questId in run.ContractsReadyForTurnIn)
            {
                _contractsReadyForTurnIn.Add(questId);
            }

            _clearedBossFloors.Clear();
            foreach (int floor in run.ClearedBossFloors)
            {
                _clearedBossFloors.Add(floor);
            }

            _dungeonFloors.Clear();
            foreach ((int floor, DungeonFloorSaveData floorData) in run.DungeonFloors)
            {
                _dungeonFloors[floor] = DungeonFloorSaveMapper.ToBlueprint(floorData);
            }

            GameStateSnapshot engineSnapshot;
            try
            {
                engineSnapshot = _saveStore.DeserializeEngineSnapshot(run.EngineStateJson);
            }
            catch (Exception ex)
            {
                error = $"Failed to deserialize engine state: {ex.Message}";
                return false;
            }

            GameStateSnapshotMapper.Apply(_gameState, engineSnapshot);

            // Resume the RNG stream where the save left it; pre-v8 saves carry no RNG state
            // and keep the freshly seeded context created above.
            Pcg32RandomSource? restoredRng = GameStateSnapshotMapper.RestoreRandomSource(engineSnapshot);
            if (restoredRng is not null)
            {
                _context = CreateContext(restoredRng, _eventSink);
            }

            SeedHeroStatBlock();

            if (_currentDungeonFloor <= 0)
            {
                BuildTownMap();
                CurrentScene = SceneId.Town;
            }
            else
            {
                EnsureDungeonFloorLoaded(_currentDungeonFloor, entryFromBelow: false);
                CurrentScene = SceneId.Dungeon;
            }

            Dispatch(new UpsertExplorationActorCommand(HeroActorId, run.HeroX, run.HeroY, blocksMovement: true));
            if (run.PendingBossRewardFloor.HasValue)
            {
                _pendingBossReward = BuildBossRewardSnapshot(run.PendingBossRewardFloor.Value);
            }

            _lastMessage = string.IsNullOrWhiteSpace(run.LastMessage)
                ? "Run loaded."
                : run.LastMessage;
            return true;
        }


    }
}
