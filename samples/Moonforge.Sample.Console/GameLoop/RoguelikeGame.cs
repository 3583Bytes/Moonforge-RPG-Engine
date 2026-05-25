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
using Moonforge.Sample.ConsoleApp.Rendering;
using Moonforge.Sample.Roguelike.Rendering;
using Moonforge.Sample.Roguelike.Content;
using static Moonforge.Sample.Roguelike.Content.RoguelikeContent;
using Moonforge.Sample.Roguelike;
using Moonforge.Sample.Roguelike.Session;
using Moonforge.Sample.Roguelike.WorldGen;

namespace Moonforge.Sample.ConsoleApp.GameLoop;

internal sealed class RoguelikeGame
{

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
    private SceneId _scene = SceneId.MainMenu;
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
    private readonly List<MapMarker> _townMarkers = [];
    private readonly List<MapMarker> _dungeonMarkers = [];
    private readonly Dictionary<GridPosition, char> _townWallDecorations = [];
    private readonly Dictionary<GridPosition, char> _townFloorDecorations = [];
    private readonly Dictionary<GridPosition, char> _dungeonWallDecorations = [];
    private readonly Dictionary<int, DungeonFloorBlueprint> _dungeonFloors = [];
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
    private readonly HashSet<string> _contractsReadyForTurnIn = [];
    private readonly HashSet<MetaUnlockId> _unlockedMetaUnlocks = [];
    private readonly HashSet<int> _clearedBossFloors = [];
    private string? _activeContractQuestId;
    private string? _activeDialogueId;
    private SceneId _dialogueReturnScene = SceneId.Town;
    private string? _lastEncounterThemeId;
    private int _lastEncounterEnemyCount;
    private ClassProfile _selectedClass = ClassProfiles[0];
    private string? _pendingEncounterGearDropItemId;
    private int? _activeBossFloor;

    public RoguelikeGame()
    {
        _saveStore = new RoguelikeSaveStore();
        _definitions = RoguelikeContent.BuildCatalog();
        _dialogueChoicesQueryHandler = new GetAvailableDialogueChoicesQueryHandler(_definitions);
        _context = CreateContext(seed: 1, _eventSink);

        _dispatcher = DefaultCommandDispatcher.Create();

        LoadMetaUnlocksFromSave();
    }

    public void Run()
    {
        while (_scene != SceneId.Exit)
        {
            switch (_scene)
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
                    _scene = SceneId.Exit;
                    break;
            }
        }
    }

    private void RunMainMenu()
    {
        bool canContinue = TryPeekContinueRun();
        ConsoleRenderer.RenderMainMenu("[grey]Static town + seeded dungeon + real turn battles.[/]", canContinue);
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        if (canContinue && key.Key is ConsoleKey.C)
        {
            if (TryContinueSavedRun(out string? loadError))
            {
                _lastMessage = "Loaded previous run.";
            }
            else
            {
                _lastMessage = $"Continue failed: {loadError}";
            }

            return;
        }

        if (canContinue && key.Key is ConsoleKey.D)
        {
            ConsoleRenderer.RenderContractNotice(
                "Delete Save",
                "Delete existing save data?\nThis cannot be undone.",
                "Press Y to confirm, any other key to cancel");
            ConsoleKeyInfo confirm = Console.ReadKey(intercept: true);
            if (confirm.Key is ConsoleKey.Y)
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

            return;
        }

        if (key.Key is ConsoleKey.N)
        {
            _scene = SceneId.ClassSelect;
            return;
        }

        if (key.Key is ConsoleKey.Q or ConsoleKey.Escape)
        {
            _scene = SceneId.Exit;
        }
    }

    private void RunClassSelect()
    {
        List<ClassSelectionOption> options = [];
        for (int i = 0; i < ClassProfiles.Length; i++)
        {
            ClassProfile profile = ClassProfiles[i];
            options.Add(new ClassSelectionOption(
                (i + 1).ToString(),
                profile.Name,
                profile.Summary));
        }

        ConsoleRenderer.RenderClassSelection(options, "Press 1/2/3 to choose, Esc to cancel");
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        ClassProfile? selected = key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => ClassProfiles[0],
            ConsoleKey.D2 or ConsoleKey.NumPad2 => ClassProfiles[1],
            ConsoleKey.D3 or ConsoleKey.NumPad3 => ClassProfiles[2],
            _ => null
        };

        if (selected is null)
        {
            if (key.Key == ConsoleKey.Escape)
            {
                _scene = SceneId.MainMenu;
            }

            return;
        }

        StartNewRun(selected);
        _scene = SceneId.Town;
    }

    private void RunTown()
    {
        if (TryEnterPendingContractNotice(SceneId.Town))
        {
            return;
        }

        ConsoleRenderer.RenderMap(new MapRenderModel(
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
            FloorDecorations: _townFloorDecorations));

        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        switch (key.Key)
        {
            case ConsoleKey.B:
                TryBuyPotion();
                return;
            case ConsoleKey.S:
                TrySellHerb();
                return;
            case ConsoleKey.J:
                _journalReturnScene = SceneId.Town;
                _scene = SceneId.ContractJournal;
                return;
            case ConsoleKey.I:
                _gearReturnScene = SceneId.Town;
                _scene = SceneId.GearInventory;
                return;
            case ConsoleKey.E:
                TryTownInteract();
                return;
            case ConsoleKey.M:
            case ConsoleKey.Escape:
                _scene = SceneId.MainMenu;
                _lastMessage = "Returned to main menu.";
                return;
            default:
                HandleMovementInput(key);
                return;
        }
    }

    private void RunDungeon()
    {
        if (TryEnterPendingContractNotice(SceneId.Dungeon))
        {
            return;
        }

        ConsoleRenderer.RenderMap(new MapRenderModel(
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
            WallDecorations: _dungeonWallDecorations));

        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        switch (key.Key)
        {
            case ConsoleKey.E:
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
            case ConsoleKey.J:
                _journalReturnScene = SceneId.Dungeon;
                _scene = SceneId.ContractJournal;
                return;
            case ConsoleKey.I:
                _gearReturnScene = SceneId.Dungeon;
                _scene = SceneId.GearInventory;
                return;
            case ConsoleKey.T:
                ReturnToTownViaPortal();
                return;
            case ConsoleKey.M:
            case ConsoleKey.Escape:
                _scene = SceneId.MainMenu;
                _lastMessage = "Returned to main menu.";
                return;
            default:
                HandleMovementInput(key);
                TryResolveDungeonStep();
                return;
        }
    }

    private void RunBattle()
    {
        BattleStatus? status = _battleStatusQueryHandler.Query(_gameState, new GetActiveBattleStatusQuery());
        if (status is null || status != BattleStatus.Active)
        {
            HandleBattleCompletion();
            return;
        }

        string? turnActorId = _turnActorQueryHandler.Query(_gameState, new GetCurrentBattleTurnActorQuery());
        ConsoleRenderer.RenderBattle(new BattleRenderModel(
            Title: "Battle",
            Battle: _gameState.ActiveBattle!,
            CurrentTurnActorId: turnActorId,
            Controls: BuildBattleControls(),
            ClassActionInfo: BuildBattleClassActionInfo(),
            RecentLog: _battleLog.ToArray(),
            LastMessage: _lastMessage,
            MessageTone: _lastMessageTone));

        if (turnActorId is null || !_gameState.ActiveBattle!.TryGetActor(turnActorId, out BattleActorState turnActor))
        {
            _lastMessage = "Turn state is invalid.";
            _scene = SceneId.Dungeon;
            return;
        }

        if (!turnActor.PlayerControlled)
        {
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

        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        if (key.Key is ConsoleKey.Q or ConsoleKey.Escape)
        {
            BuildTownMap();
            _scene = SceneId.Town;
            _lastMessage = "You fled and reappeared in town.";
            _gameState.ActiveBattle = null;
            _activeBattleSnapshot = null;
            _pendingBattleSummary = null;
            _pendingBossReward = null;
            _activeBossFloor = null;
            _battleLog.Clear();
            return;
        }

        if (key.Key is ConsoleKey.P)
        {
            TryUseBattlePotion(turnActor);
            if (_gameState.ActiveBattle is null)
            {
                HandleBattleCompletion();
            }

            return;
        }

        int classAbilityIndex = key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => 0,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => 1,
            _ => -1
        };
        if (classAbilityIndex >= 0)
        {
            TryUseClassBattleAbility(classAbilityIndex);
            if (_gameState.ActiveBattle is null)
            {
                HandleBattleCompletion();
            }

            return;
        }

        if (key.Key is not ConsoleKey.A)
        {
            _lastMessage = "Use A attack, 1/2 class skills, P potion, or Q retreat.";
            return;
        }

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
        List<string> parts = [$"Focus: {focus}/{MaxFocus}"];
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

    private void RunBattleSummary()
    {
        if (_pendingBattleSummary is null)
        {
            _scene = _postBattleScene;
            return;
        }

        string controls = _pendingBossReward is not null && string.IsNullOrWhiteSpace(_pendingBattleSummary.BossRewardChosen)
            ? "Press 1/2/3 to claim boss reward, then Enter/Space"
            : "Press Enter/Space to continue";

        ConsoleRenderer.RenderBattleSummary(new BattleSummaryRenderModel(
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

        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        int rewardChoiceIndex = key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => 0,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => 1,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => 2,
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

        if (key.Key is not (ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.Escape))
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
            _scene = SceneId.ContractNotice;
        }
        else
        {
            _scene = _postBattleScene;
        }

        _activeBattleSnapshot = null;
        _pendingBattleSummary = null;
        _battleLog.Clear();
    }

    private void RunContractNotice()
    {
        if (_pendingContractNotice is null)
        {
            _scene = _resumeSceneAfterContractNotice;
            return;
        }

        ConsoleRenderer.RenderContractNotice(
            _pendingContractNotice.Title,
            _pendingContractNotice.Body,
            "Press Enter/Space to continue");

        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        if (key.Key is not (ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.Escape))
        {
            return;
        }

        _pendingContractNotice = null;
        _scene = _resumeSceneAfterContractNotice;
    }

    private void RunContractJournal()
    {
        List<string> lines = BuildJournalLines();
        bool canAbandon = !string.IsNullOrWhiteSpace(_activeContractQuestId)
            && !IsQuestRewarded(_activeContractQuestId!);
        string controls = canAbandon
            ? "A: Abandon active contract | Enter/Esc/J: Return"
            : "Enter/Esc/J: Return";
        ConsoleRenderer.RenderContractJournal("Contract Journal", lines, controls);
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        if (canAbandon && key.Key is ConsoleKey.A)
        {
            TryAbandonActiveContract();
            return;
        }

        if (key.Key is ConsoleKey.Enter or ConsoleKey.Escape or ConsoleKey.J or ConsoleKey.Spacebar)
        {
            _scene = _journalReturnScene;
        }
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

    private void RunGearInventory()
    {
        List<string> lines = BuildGearMenuLines();
        ConsoleRenderer.RenderContractJournal("Gear Loadout", lines, "Press 1-6 to equip/toggle, U to unequip all, Enter/Esc/I to return");
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        switch (key.Key)
        {
            case ConsoleKey.D1:
            case ConsoleKey.NumPad1:
                TryToggleGear(0);
                return;
            case ConsoleKey.D2:
            case ConsoleKey.NumPad2:
                TryToggleGear(1);
                return;
            case ConsoleKey.D3:
            case ConsoleKey.NumPad3:
                TryToggleGear(2);
                return;
            case ConsoleKey.D4:
            case ConsoleKey.NumPad4:
                TryToggleGear(3);
                return;
            case ConsoleKey.D5:
            case ConsoleKey.NumPad5:
                TryToggleGear(4);
                return;
            case ConsoleKey.D6:
            case ConsoleKey.NumPad6:
                TryToggleGear(5);
                return;
            case ConsoleKey.U:
                TryUnequipAll();
                return;
            case ConsoleKey.Enter:
            case ConsoleKey.Escape:
            case ConsoleKey.I:
            case ConsoleKey.Spacebar:
                _scene = _gearReturnScene;
                return;
            default:
                return;
        }
    }

    private void RunMetaShrine()
    {
        List<string> lines = BuildMetaShrineLines();
        ConsoleRenderer.RenderContractJournal("Shrine of Echoes", lines, "Press 1-4 to unlock perks, Enter/Esc to return");
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        switch (key.Key)
        {
            case ConsoleKey.D1:
            case ConsoleKey.NumPad1:
                TryPurchaseMetaUnlock(0);
                return;
            case ConsoleKey.D2:
            case ConsoleKey.NumPad2:
                TryPurchaseMetaUnlock(1);
                return;
            case ConsoleKey.D3:
            case ConsoleKey.NumPad3:
                TryPurchaseMetaUnlock(2);
                return;
            case ConsoleKey.D4:
            case ConsoleKey.NumPad4:
                TryPurchaseMetaUnlock(3);
                return;
            case ConsoleKey.Enter:
            case ConsoleKey.Escape:
            case ConsoleKey.Spacebar:
                _scene = SceneId.Town;
                return;
            default:
                return;
        }
    }

    private void RunBossReward()
    {
        if (_pendingBossReward is null)
        {
            if (_pendingContractNotice is not null)
            {
                _resumeSceneAfterContractNotice = _postBattleScene;
                _scene = SceneId.ContractNotice;
            }
            else
            {
                _scene = _postBattleScene;
            }

            return;
        }

        List<string> lines = BuildBossRewardLines(_pendingBossReward);
        ConsoleRenderer.RenderContractJournal(
            "Boss Reward Chest",
            lines,
            "Press 1/2/3 to choose your reward");
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        switch (key.Key)
        {
            case ConsoleKey.D1:
            case ConsoleKey.NumPad1:
                TryClaimBossRewardChoice(0, returnToPostSceneOnSuccess: true);
                return;
            case ConsoleKey.D2:
            case ConsoleKey.NumPad2:
                TryClaimBossRewardChoice(1, returnToPostSceneOnSuccess: true);
                return;
            case ConsoleKey.D3:
            case ConsoleKey.NumPad3:
                TryClaimBossRewardChoice(2, returnToPostSceneOnSuccess: true);
                return;
            default:
                return;
        }
    }

    private List<string> BuildJournalLines()
    {
        List<string> lines = [];
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
        List<string> lines = [];
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
        List<string> lines = [];
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

    private List<string> BuildGearMenuLines()
    {
        _visibleGear.Clear();
        _visibleGear.AddRange(BuildVisibleGearList());

        List<string> lines = [];
        lines.Add($"Class: {_selectedClass.Name}");
        lines.Add(string.Empty);
        lines.Add("Equipped:");
        lines.Add($"- Weapon: {GetEquippedLabel(SlotWeapon)}");
        lines.Add($"- Armor: {GetEquippedLabel(SlotArmor)}");
        lines.Add($"- Accessory: {GetEquippedLabel(SlotAccessory)}");
        lines.Add(string.Empty);

        if (_visibleGear.Count == 0)
        {
            lines.Add("Inventory Gear:");
            lines.Add("  - no gear yet - find drops or boss rewards.");
            return lines;
        }

        lines.Add("Inventory Gear:");
        for (int i = 0; i < _visibleGear.Count; i++)
        {
            GearMetadata gear = _visibleGear[i];
            int qty = _inventoryQuantityQueryHandler.Query(_gameState, new GetInventoryItemQuantityQuery(gear.ItemId));
            string? slotItemId = _equippedItemQueryHandler.Query(_gameState, new GetEquippedItemQuery(gear.SlotId));
            bool equipped = string.Equals(slotItemId, gear.ItemId, StringComparison.Ordinal);
            string marker = equipped ? "[E]" : "[ ]";
            string qtyLabel = equipped ? "equipped" : $"Qty:{qty}";
            lines.Add($"{i + 1}. {marker} {gear.Name} ({gear.SlotId}) {qtyLabel}  +ATK {gear.Atk}, +DEF {gear.Def}, +MATK {gear.Matk}, +MDEF {gear.Mdef}, +INIT {gear.Initiative}");
        }

        return lines;
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
        Dispatch(new UpsertExplorationActorCommand(GuardActorId, guardSpawn.X, guardSpawn.Y, blocksMovement: true));
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
    }

    private void EnterDungeon()
    {
        _currentDungeonFloor = 1;
        RecordDeepestFloor(_currentDungeonFloor);
        EnsureDungeonFloorLoaded(_currentDungeonFloor, entryFromBelow: false);
        _scene = SceneId.Dungeon;
        _lastMessage = "Entered dungeon floor 1.";
    }

    private void ReturnToTownFromDungeon()
    {
        int floorJustLeft = _currentDungeonFloor;
        int reward = 10 + (floorJustLeft * 3);
        Dispatch(new GrantCurrencyCommand(GoldCurrencyId, reward));
        _currentDungeonFloor = 0;
        BuildTownMap();
        _scene = SceneId.Town;
        _lastMessage = $"Returned to town with {reward} gold from floor {floorJustLeft}.";
        SaveProgress("returned to town");
    }

    private void ReturnToTownViaPortal()
    {
        int reward = Math.Max(3, _currentDungeonFloor);
        Dispatch(new GrantCurrencyCommand(GoldCurrencyId, reward));
        _currentDungeonFloor = 0;
        BuildTownMap();
        _scene = SceneId.Town;
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
            _scene = SceneId.Battle;
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
        _scene = SceneId.Battle;
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
        if (_activeBattleSnapshot is null)
        {
            _scene = _lastBattleStatus == BattleStatus.Victory ? SceneId.Dungeon : SceneId.Town;
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
        _scene = SceneId.BattleSummary;
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

        RunTownInteractionMenu(interaction.Value);
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

    private void RunTownInteractionMenu(TownInteractionKind kind)
    {
        List<string> lines = BuildTownInteractionLines(kind);
        ConsoleRenderer.RenderContractJournal(
            $"Town - {GetTownInteractionTitle(kind)}",
            lines,
            "Press 1/2/3 for actions, Esc to cancel");
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        switch (kind)
        {
            case TownInteractionKind.Guard:
                if (key.Key is ConsoleKey.D1 or ConsoleKey.NumPad1)
                {
                    TryTalkToGuard();
                }
                else if (key.Key is ConsoleKey.D2 or ConsoleKey.NumPad2)
                {
                    _lastMessage = "Guard: A alchemist, H healer, Q board, S shrine, > dungeon.";
                }

                break;
            case TownInteractionKind.Alchemist:
                if (key.Key is ConsoleKey.D1 or ConsoleKey.NumPad1)
                {
                    TryBrewAtAlchemist();
                }
                else if (key.Key is ConsoleKey.D2 or ConsoleKey.NumPad2)
                {
                    TryBuyPotion();
                }

                break;
            case TownInteractionKind.Healer:
                if (key.Key is ConsoleKey.D1 or ConsoleKey.NumPad1)
                {
                    TryUseHealer();
                }

                break;
            case TownInteractionKind.Cache:
                if (key.Key is ConsoleKey.D1 or ConsoleKey.NumPad1)
                {
                    TryOpenTownCache();
                }

                break;
            case TownInteractionKind.Fountain:
                if (key.Key is ConsoleKey.D1 or ConsoleKey.NumPad1)
                {
                    TryInspectFountain();
                }

                break;
            case TownInteractionKind.QuestBoard:
                if (key.Key is ConsoleKey.D1 or ConsoleKey.NumPad1)
                {
                    TryQuestBoardPrimaryAction();
                }
                else if (key.Key is ConsoleKey.D2 or ConsoleKey.NumPad2)
                {
                    TryQuestBoardSecondaryAction();
                }

                break;
            case TownInteractionKind.Shrine:
                if (key.Key is ConsoleKey.D1 or ConsoleKey.NumPad1)
                {
                    _scene = SceneId.MetaShrine;
                    _lastMessage = "Shrine: spend tokens to unlock permanent perks.";
                }

                break;
            case TownInteractionKind.DungeonEntrance:
                if (key.Key is ConsoleKey.D1 or ConsoleKey.NumPad1)
                {
                    EnterDungeon();
                }

                break;
        }
    }

    private List<string> BuildTownInteractionLines(TownInteractionKind kind)
    {
        List<string> lines = [];
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
        _scene = SceneId.Dialogue;
    }

    private void RunDialogue()
    {
        if (string.IsNullOrWhiteSpace(_activeDialogueId)
            || !_definitions.TryGetDialogue(_activeDialogueId!, out DialogueDefinition definition)
            || !_gameState.DialogueState.TryGet(_activeDialogueId!, out DialogueInstanceState state)
            || string.IsNullOrWhiteSpace(state.CurrentNodeId))
        {
            ExitDialogue();
            return;
        }

        DialogueNodeDefinition? node = FindNode(definition, state.CurrentNodeId!);
        if (node is null)
        {
            ExitDialogue();
            return;
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

        string body = ResolveDialogueText(node.TextKey);
        ConsoleRenderer.RenderDialogue(new DialogueRenderModel(
            NpcName: ResolveNpcName(_activeDialogueId!),
            BodyText: body,
            Choices: visibleChoices,
            Controls: visibleChoices.Count == 0
                ? "Press any key to step away"
                : "Press 1-" + visibleChoices.Count + " to choose, Esc to step away"));

        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        if (key.Key is ConsoleKey.Escape)
        {
            ExitDialogue();
            return;
        }

        if (visibleChoices.Count == 0)
        {
            ExitDialogue();
            return;
        }

        int index = key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => 0,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => 1,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => 2,
            ConsoleKey.D4 or ConsoleKey.NumPad4 => 3,
            ConsoleKey.D5 or ConsoleKey.NumPad5 => 4,
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

    private void ExitDialogue()
    {
        _activeDialogueId = null;
        _scene = _dialogueReturnScene;
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
        List<QuestDefinition> available = [];
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

        if (_scene is not SceneId.BattleSummary and not SceneId.ContractNotice)
        {
            _resumeSceneAfterContractNotice = _scene;
            _scene = SceneId.ContractNotice;
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
        List<BossRewardChoice> choices =
        [
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
        ];

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
            _scene = SceneId.ContractNotice;
        }
        else
        {
            _scene = _postBattleScene;
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

        List<string> segments = [];
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
        _scene = SceneId.ContractNotice;
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

    private void HandleMovementInput(ConsoleKeyInfo key)
    {
        int deltaX = 0;
        int deltaY = 0;
        switch (key.Key)
        {
            case ConsoleKey.W:
            case ConsoleKey.UpArrow:
                deltaY = -1;
                break;
            case ConsoleKey.S:
            case ConsoleKey.DownArrow:
                deltaY = 1;
                break;
            case ConsoleKey.A:
            case ConsoleKey.LeftArrow:
                deltaX = -1;
                break;
            case ConsoleKey.D:
            case ConsoleKey.RightArrow:
                deltaX = 1;
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

        List<string> skillIds =
        [
            _selectedClass.BasicSkillId,
            "skill.potion"
        ];
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
            ? []
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

        if (_gameState.ActiveBattle is null && _scene is not SceneId.BattleSummary and not SceneId.ContractNotice)
        {
            _resumeSceneAfterContractNotice = _scene;
            _scene = SceneId.ContractNotice;
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
        return new CommandContext(
            new Pcg32RandomSource(seed, sequence: 54),
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

        GameStateSnapshot engineSnapshot = GameStateSnapshotMapper.Capture(_gameState);
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
        SeedHeroStatBlock();

        if (_currentDungeonFloor <= 0)
        {
            BuildTownMap();
            _scene = SceneId.Town;
        }
        else
        {
            EnsureDungeonFloorLoaded(_currentDungeonFloor, entryFromBelow: false);
            _scene = SceneId.Dungeon;
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
