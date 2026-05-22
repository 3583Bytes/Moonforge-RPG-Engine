using System;
using System.Collections.Generic;
using System.Linq;
using Moonforge.Core;
using Moonforge.Core.Bestiary;
using Moonforge.Core.Bestiary.Events;
using Moonforge.Core.Combat;
using Moonforge.Core.Combat.Commands;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Evolution.Commands;
using Moonforge.Core.Evolution.Events;
using Moonforge.Core.Exploration;
using Moonforge.Core.Exploration.Commands;
using Moonforge.Core.Economy;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Inventory;
using Moonforge.Core.Inventory.Commands;
using Moonforge.Core.Party;
using Moonforge.Core.Party.Commands;
using Moonforge.Core.Progression.Commands;
using Moonforge.Core.Quests;
using Moonforge.Core.Quests.Commands;
using Moonforge.Core.Shops.Commands;
using Moonforge.Core.Progression.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;
using Moonforge.Sample.MonsterCatcher.Content;
using Moonforge.Sample.MonsterCatcher.Rendering;
using Moonforge.Sample.MonsterCatcher.WorldGen;

namespace Moonforge.Sample.MonsterCatcher.GameLoop;

/// <summary>
/// Battle-focused monster-catcher sample. The world is a chain of procedurally-generated
/// screens (see <see cref="World"/>) — biomes scale in difficulty as you walk east, gyms
/// gate every 5th screen, and the Champion's Hall sits past the 8th gym. Phase 1 builds
/// the multi-screen foundation; gyms, economy, and the quest line layer on in later phases.
/// </summary>
internal sealed class MonsterCatcherGame
{
    public const string PlayerActorPrefix = "actor.player";
    public const string WildActorPrefix = "actor.wild";
    public const string LeaderActorPrefix = "actor.leader";
    public const string PlayerExplorationActor = "exploration.player";
    public const string MapId = "map.world";

    private readonly GameState _gameState;
    private readonly CommandDispatcher _dispatcher;
    private readonly InMemoryDomainEventSink _sink;
    private readonly InMemoryGameDefinitionCatalog _defs;
    private readonly IRandomSource _rng;
    private readonly SimulationClock _clock;
    private readonly IFormulaEvaluator _formulas;

    // Per-actor state the engine doesn't model: which species each actor is, current HP
    // (engine HP only exists inside a battle), and the per-actor move set (engine has no
    // learnset concept).
    private readonly Dictionary<string, string> _speciesByActor = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _currentHpByActor = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _movesByActor = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _displayNameByActor = new(StringComparer.Ordinal);

    private readonly List<DomainEvent> _battleEvents = new();

    private World? _world;
    private int _currentScreenIndex;
    private int _playerX;
    private int _playerY;
    private int _screensReached;
    private int _playerActorCounter;
    private int _wildActorCounter;
    private int _leaderActorCounter;
    private int _battleSequence;
    private int _headlessTickGuard;
    private int _faintWarpCount;
    private OverworldTile _lastTile;
    private bool _inTrainerBattle;
    private bool _championDefeated;
    private readonly HashSet<string> _badgesEarned = new(StringComparer.Ordinal);

    // Faint-warp respawn: tracks the most recently visited heal pad. Defaults to the
    // starting spawn (screen 0 west entry). Updated every time the player steps on a pad.
    private int _lastHealScreenIndex;
    private int _lastHealX;
    private int _lastHealY;

    public MonsterCatcherGame(ulong seed = 12345)
    {
        _defs = ContentCatalog.Build();
        _rng = new Pcg32RandomSource(seed, 0);
        _clock = new SimulationClock(0);
        _formulas = new ExpressionFormulaEvaluator();
        _sink = new InMemoryDomainEventSink();
        _gameState = new GameState();
        _dispatcher = DefaultCommandDispatcher.Create();
        WorldSeed = seed;

        // Party = 1 active mon, up to 6 in roster (Pokemon-shaped).
        DispatchOrThrow(new ConfigurePartyCommand(maxActive: 1, maxRoster: 6));

        // Inventory: 20 slots is enough for the catalog tiers (3 ball, 3 heal, 1 revive)
        // plus headroom for hoarding pokeballs. Grant the starter pack.
        DispatchOrThrow(new ConfigureInventoryCapacityCommand(capacitySlots: 20));
        foreach ((string itemId, int qty) in TownShop.StartingInventory)
        {
            DispatchOrThrow(new AddInventoryItemCommand(itemId, qty));
        }

        // Start the main quest — "Defeat the eight Wardens." Gym victories emit signals
        // that auto-advance the single Kill objective.
        DispatchOrThrow(new StartQuestCommand(MainQuest.QuestId));
    }

    public ulong WorldSeed { get; }

    /// <summary>Run the game from title to victory or defeat. Returns the final outcome.</summary>
    public GameOutcome Run()
    {
        ShowTitle();
        string starterSpeciesId = ChooseStarter();
        // Starter begins at level 8 — just past evolution threshold — so the early game has
        // some teeth without the player getting one-shot on screen 0 before they reach a
        // heal pad. Phase 3's potion items will let us drop this back to level 5.
        string starterActorId = AddPartyMonster(starterSpeciesId, level: 8, active: true);

        Ui.Clear();
        Ui.Heading("Off you go!");
        Ui.Line($"You set out with [bold cyan]{Ui.TypeLabel(_displayNameByActor[starterActorId])}[/].");
        Ui.PressEnter();

        BuildWorld();

        while (true)
        {
            GameOutcome? finished = TickWorld();
            if (finished.HasValue)
            {
                if (finished.Value == GameOutcome.Victory)
                {
                    Ui.Clear();
                    Ui.Heading("Champion!", "green");
                    Ui.Success("You stood at the Champion's Hall as the eighth-and-final test. You are the champion.");
                }
                else if (finished.Value == GameOutcome.Defeat)
                {
                    Ui.Clear();
                    Ui.Heading("Defeated", "red");
                    Ui.Failure("Your party has fallen. The wilds claimed another aspirant.");
                }

                ShowFinalStats();
                return finished.Value;
            }
        }
    }

    // ----- World / screen orchestration ------------------------------------------------

    private void BuildWorld()
    {
        _world = new World(WorldSeed);
        _currentScreenIndex = 0;
        WorldScreen first = _world.GetOrGenerate(_currentScreenIndex);
        _playerX = first.WestEntry.X;
        _playerY = first.WestEntry.Y;
        _lastTile = first.TileAt(_playerX, _playerY);
        _screensReached = 1;

        // Default respawn point — the spawn tile — until the player visits a heal pad.
        _lastHealScreenIndex = 0;
        _lastHealX = _playerX;
        _lastHealY = _playerY;

        ApplyExplorationMapToEngine(first);
    }

    /// <summary>Reconfigure the engine's exploration map and actor for the active screen.</summary>
    private void ApplyExplorationMapToEngine(WorldScreen screen)
    {
        ExplorationTileFlags[] flags = screen.ToEngineFlags();
        DispatchOrThrow(new ConfigureExplorationMapCommand(MapId, screen.Width, screen.Height, flags));
        DispatchOrThrow(new UpsertExplorationActorCommand(PlayerExplorationActor, _playerX, _playerY, blocksMovement: false));
    }

    private GameOutcome? TickWorld()
    {
        WorldScreen screen = _world!.GetOrGenerate(_currentScreenIndex);

        Ui.Clear();
        RenderWorldScreen(screen);

        (int dx, int dy)? input = ReadMovementInput();
        if (!input.HasValue) return GameOutcome.Quit;

        int newX = _playerX + input.Value.dx;
        int newY = _playerY + input.Value.dy;

        // ---- Screen transitions (stepping off the east or west edge from the entry/exit tile) ----
        if (newX >= screen.Width && _playerX == screen.EastExit.X && _playerY == screen.EastExit.Y && input.Value.dx > 0)
        {
            return TryTransitionEast(screen);
        }
        if (newX < 0 && _playerX == screen.WestEntry.X && _playerY == screen.WestEntry.Y && input.Value.dx < 0)
        {
            return TryTransitionWest(screen);
        }

        // ---- Normal walkability ----
        if (!screen.IsWalkable(newX, newY))
        {
            if (Console.IsInputRedirected && ++_headlessTickGuard > screen.Width * 4)
            {
                // Headless test got pinned by walls/locked-gates with no other progress —
                // treat that as a Defeat so smoke tests always finish on a real outcome
                // rather than Quit.
                return GameOutcome.Defeat;
            }

            return null;
        }

        _playerX = newX;
        _playerY = newY;

        // Tell the engine the actor moved. ConfigureExplorationMap pre-registered walkability,
        // so this can't fail unless the engine and our local view drift — we crash in that case.
        DispatchOrThrow(new MoveActorCommand(PlayerExplorationActor, input.Value.dx, input.Value.dy));

        OverworldTile prevTile = _lastTile;
        OverworldTile currentTile = screen.TileAt(_playerX, _playerY);
        _lastTile = currentTile;

        return ResolveTileEffect(screen, prevTile, currentTile);
    }

    private GameOutcome? TryTransitionEast(WorldScreen screen)
    {
        if (!screen.EastGateOpen)
        {
            Ui.Note("A guard blocks the way east. \"Beat the gym leader first.\"");
            if (!Console.IsInputRedirected) Ui.PressEnter();
            // Headless mode would otherwise mash east into a closed gate forever; cap retries.
            if (Console.IsInputRedirected && ++_headlessTickGuard > screen.Width * 4)
            {
                // Headless test got pinned by walls/locked-gates with no other progress —
                // treat that as a Defeat so smoke tests always finish on a real outcome
                // rather than Quit.
                return GameOutcome.Defeat;
            }
            return null;
        }

        // Crossing into a new screen gives the party a light breather: top up HP to at
        // least 60% of MaxHp. This stops cumulative-damage runs from being a death sentence
        // and approximates "the road gives you a moment between zones." Doesn't refill PP.
        RestPartyOnScreenTransition();

        int nextIndex = _currentScreenIndex + 1;
        if (nextIndex > _world!.LastScreenIndex)
        {
            // Past the end — shouldn't reach here because Champion's Goal tile ends the game.
            return null;
        }

        _currentScreenIndex = nextIndex;
        if (nextIndex >= _screensReached) _screensReached = nextIndex + 1;

        WorldScreen next = _world!.GetOrGenerate(_currentScreenIndex);
        _playerX = next.WestEntry.X;
        _playerY = next.WestEntry.Y;
        _lastTile = next.TileAt(_playerX, _playerY);
        ApplyExplorationMapToEngine(next);

        AnnounceScreen(next, fromWest: true);
        return ResolveTileEffect(next, OverworldTile.Wall, _lastTile);
    }

    private GameOutcome? TryTransitionWest(WorldScreen screen)
    {
        if (_currentScreenIndex == 0)
        {
            Ui.Info("The road behind you ends at the world's edge.");
            return null;
        }

        _currentScreenIndex--;
        WorldScreen prev = _world!.GetOrGenerate(_currentScreenIndex);
        _playerX = prev.EastExit.X;
        _playerY = prev.EastExit.Y;
        _lastTile = prev.TileAt(_playerX, _playerY);
        ApplyExplorationMapToEngine(prev);

        AnnounceScreen(prev, fromWest: false);
        return null;
    }

    private void AnnounceScreen(WorldScreen screen, bool fromWest)
    {
        BiomeProfile profile = BiomeRegistry.Get(screen.Biome);
        // Only narrate biome flavor the first time we encounter any biome of this kind, so
        // backtracking doesn't spam the same tagline.
        // (Cheap heuristic — track first-seen biomes by their kind.)
        if (_firstSeenBiomes.Add(screen.Biome))
        {
            Ui.Note($"Entering {profile.DisplayName}: {profile.Tagline}");
        }
    }

    private readonly HashSet<BiomeKind> _firstSeenBiomes = new();

    private GameOutcome? ResolveTileEffect(WorldScreen screen, OverworldTile prevTile, OverworldTile tile)
    {
        switch (tile)
        {
            case OverworldTile.Goal:
                return HandleChampionTile();

            case OverworldTile.HealPad:
                if (prevTile != OverworldTile.HealPad)
                {
                    HandleHealPad(screen);
                }
                return null;

            case OverworldTile.ShopPad:
                if (prevTile != OverworldTile.ShopPad)
                {
                    HandleShopPad();
                }
                return null;

            case OverworldTile.GymPad:
                if (prevTile != OverworldTile.GymPad)
                {
                    return HandleGymPad(screen);
                }
                return null;

            case OverworldTile.Grass:
                BiomeProfile profile = BiomeRegistry.Get(screen.Biome);
                if (_rng.NextInt(100) < profile.EncounterChancePercent)
                {
                    return ResolveWildEncounter(screen);
                }

                return null;

            default:
                return null;
        }
    }

    private void HandleHealPad(WorldScreen screen)
    {
        int idx = screen.IndexOf(_playerX, _playerY);
        Ui.Note(screen.RestedAtPads.Add(idx)
            ? "You step onto a healing pad. Your party is restored and PP refilled."
            : "Healing pad — your party is restored.");
        HealPartyAndRestorePp();
        // Remember this pad as the faint-warp respawn point.
        _lastHealScreenIndex = _currentScreenIndex;
        _lastHealX = _playerX;
        _lastHealY = _playerY;
        if (!Console.IsInputRedirected) Ui.PressEnter();
    }

    private GameOutcome? HandleGymPad(WorldScreen screen)
    {
        int gymNumber = _world!.GymNumberFor(_currentScreenIndex);
        if (gymNumber == 0) return null;

        if (screen.GymCleared)
        {
            Ui.Note("The leader's mat sits quiet. You've already taken this badge.");
            if (!Console.IsInputRedirected) Ui.PressEnter();
            return null;
        }

        GymLeader leader = GymLeaders.ForGymNumber(gymNumber);
        Ui.Clear();
        Ui.Heading($"Gym {gymNumber}: {leader.DisplayName}, {leader.Title}", "magenta");
        Ui.Line();
        Ui.Note(leader.IntroLine);
        Ui.Info($"Roster: {string.Join(", ", leader.Roster.Select(r => $"{MonsterRoster.Get(r.speciesId).DisplayName} Lv {r.level}"))}");
        Ui.Line();
        if (!Console.IsInputRedirected) Ui.PressEnter("Press Enter to accept the challenge.");

        GymBattleOutcome outcome = RunGymBattle(leader);
        if (outcome == GymBattleOutcome.LeaderDefeated)
        {
            screen.GymCleared = true;
            screen.EastGateOpen = true;
            _badgesEarned.Add(leader.BadgeId);
            DispatchOrThrow(new GrantCurrencyCommand(ContentCatalog.CurrencyGold, leader.GoldReward));
            // Signal the main quest. The tracking reactor advances the warden count and,
            // because the quest is declared with AutoClaim=true, also dispatches the
            // reward grant in the same transaction once the 8th warden falls.
            DispatchOrThrow(new EmitQuestSignalCommand(QuestSignalType.Kill, MainQuest.WardenSignalTarget));
            if (_gameState.QuestState.TryGet(MainQuest.QuestId, out QuestInstanceState q)
                && q.Status == QuestStatus.Rewarded)
            {
                Ui.Success("The Eight Wardens quest is complete — the bonus payment hits your wallet.");
            }

            int wardenProgress = MainQuestProgress();
            Ui.Clear();
            Ui.Heading($"Gym {gymNumber} Cleared!", "green");
            Ui.Note(leader.VictoryLine);
            Ui.Success($"You earned the {leader.BadgeName}.");
            Ui.Info($"+{leader.GoldReward} gold.   Total badges: {_badgesEarned.Count}/{World.GymCount}.   Wardens defeated: {wardenProgress}/{World.GymCount}.");
            Ui.Info("The path east is now open.");
            if (!Console.IsInputRedirected) Ui.PressEnter();
            return null;
        }

        if (outcome == GymBattleOutcome.PlayerWiped)
        {
            return HandlePartyWipeOrGameOver();
        }

        return null;
    }

    private GymBattleOutcome RunGymBattle(GymLeader leader)
    {
        _inTrainerBattle = true;
        try
        {
            for (int monIndex = 0; monIndex < leader.Roster.Count; monIndex++)
            {
                (string speciesId, int level) = leader.Roster[monIndex];

                // Skip if the player's whole party is already down (shouldn't normally happen
                // — the inner battle loop returns PartyWiped first — but cheap to check).
                if (_gameState.PartyState.Members.All(m => _currentHpByActor.GetValueOrDefault(m.ActorId, 0) <= 0))
                {
                    return GymBattleOutcome.PlayerWiped;
                }

                BattleResult sub = RunGymSubBattle(leader, speciesId, level, monIndex);
                if (sub == BattleResult.PartyWiped) return GymBattleOutcome.PlayerWiped;
                if (sub == BattleResult.Fled) return GymBattleOutcome.Fled;
            }

            return GymBattleOutcome.LeaderDefeated;
        }
        finally
        {
            _inTrainerBattle = false;
        }
    }

    private BattleResult RunGymSubBattle(GymLeader leader, string speciesId, int level, int monIndex)
    {
        string leaderActorId = NewLeaderActorId();
        _speciesByActor[leaderActorId] = speciesId;
        _displayNameByActor[leaderActorId] = $"{leader.DisplayName}'s {MonsterRoster.Get(speciesId).DisplayName}";
        _movesByActor[leaderActorId] = new List<string>(MonsterRoster.Get(speciesId).StartingMoves);
        _currentHpByActor[leaderActorId] = ComputeMaxHp(MonsterRoster.Get(speciesId), level);
        DispatchOrThrow(new ConfigureActorProgressionCommand(leaderActorId, ContentCatalog.ExperienceCurveId, level: level, xp: XpFloorForLevel(level)));

        BattleActorDefinition activeMon = BuildPlayerActor(ActivePartyActorId());
        BattleActorDefinition leaderMon = BuildLeaderActor(leaderActorId, level);

        _battleSequence++;
        DispatchOrThrow(new StartBattleCommand(
            battleId: $"gym.{leader.GymNumber}.{monIndex}.{_battleSequence}",
            actors: new[] { activeMon, leaderMon },
            skills: Moves.All,
            seed: NextBattleSeed(),
            sequence: (ulong)_battleSequence));

        _gameState.ActiveBattle!.Actors[activeMon.ActorId].Hp = _currentHpByActor[activeMon.ActorId];

        if (!Console.IsInputRedirected)
        {
            Ui.Clear();
            string ordinal = monIndex == 0 ? "first" : monIndex == 1 ? "second" : monIndex == 2 ? "third" : $"#{monIndex + 1}";
            Ui.Note($"{leader.DisplayName} sends out their {ordinal} monster — {MonsterRoster.Get(speciesId).DisplayName} (Lv {level}).");
            Ui.PressEnter();
        }

        return RunBattleLoop(leaderActorId);
    }

    private string NewLeaderActorId()
    {
        _leaderActorCounter++;
        return $"{LeaderActorPrefix}.{_leaderActorCounter}";
    }

    private BattleActorDefinition BuildLeaderActor(string actorId, int level)
    {
        MonsterSpecies sp = MonsterRoster.Get(_speciesByActor[actorId]);
        return new BattleActorDefinition(
            actorId: actorId,
            displayName: _displayNameByActor[actorId],
            faction: CombatFaction.Enemy,
            maxHp: ComputeMaxHp(sp, level),
            atk: ScaleStat(sp.BaseAtk, level),
            def: ScaleStat(sp.BaseDef, level),
            matk: ScaleStat(sp.BaseMatk, level),
            mdef: ScaleStat(sp.BaseMdef, level),
            initiative: ScaleStat(sp.BaseInitiative, level),
            skillIds: sp.StartingMoves.ToArray(),
            playerControlled: false,
            xpReward: 12 + level * 5,
            defenderTypeIds: sp.TypeIds.ToArray(),
            captureBaseRate: 0,
            speciesId: sp.Id);
    }

    /// <summary>
    /// Capture-level cap based on badges earned. Reflects the Pokemon-style obedience cap —
    /// players can't catch wild monsters meaningfully above their badge count.
    /// </summary>
    private int LevelCap => _badgesEarned.Count >= World.GymCount ? int.MaxValue : 10 + (_badgesEarned.Count * 6);

    // ----- Faint warp + Champion -------------------------------------------------------

    /// <summary>
    /// Called when the player's whole party hits 0 HP — either to a wild encounter or
    /// inside a gym/Champion battle. Pokemon-style: warp to the last visited heal pad,
    /// fully restore the party, deduct 25% of the player's gold as a penalty.
    /// </summary>
    private void HandlePartyWipe()
    {
        _faintWarpCount++;
        long gold = WalletBalance(ContentCatalog.CurrencyGold);
        long penalty = gold / 4;
        if (penalty > 0)
        {
            DispatchOrThrow(new SpendCurrencyCommand(ContentCatalog.CurrencyGold, penalty));
        }
        HealPartyAndRestorePp();

        _currentScreenIndex = _lastHealScreenIndex;
        _playerX = _lastHealX;
        _playerY = _lastHealY;
        WorldScreen screen = _world!.GetOrGenerate(_currentScreenIndex);
        _lastTile = screen.TileAt(_playerX, _playerY);
        ApplyExplorationMapToEngine(screen);

        Ui.Clear();
        Ui.Heading("You blacked out…", "red");
        Ui.Note("Your party was overwhelmed. You scurry back to the last healing pad.");
        if (penalty > 0)
        {
            Ui.Failure($"Lost {penalty} gold along the way.");
        }
        if (!Console.IsInputRedirected) Ui.PressEnter();
    }

    /// <summary>
    /// Stepping on the Goal tile in the Champion's Hall triggers a real battle — Auriel's
    /// five-mon team. Win = real Victory. Lose = faint warp (already handled).
    /// </summary>
    private GameOutcome? HandleChampionTile()
    {
        if (_championDefeated)
        {
            // Stepping on the goal again after the win — nothing left to do.
            return GameOutcome.Victory;
        }

        if (_badgesEarned.Count < World.GymCount)
        {
            // Sanity-check — the player shouldn't have reached the Goal without 8 badges
            // because gym screens lock the east gate. If we somehow do, refuse the fight.
            Ui.Note("The Champion's Hall hums with a presence that won't acknowledge you. You need all eight badges first.");
            if (!Console.IsInputRedirected) Ui.PressEnter();
            return null;
        }

        GymLeader champion = Champion.Encounter;
        Ui.Clear();
        Ui.Heading($"The Champion's Hall — {champion.DisplayName}, {champion.Title}", "magenta");
        Ui.Line();
        Ui.Note(champion.IntroLine);
        Ui.Info($"Roster: {string.Join(", ", champion.Roster.Select(r => $"{MonsterRoster.Get(r.speciesId).DisplayName} Lv {r.level}"))}");
        Ui.Line();
        if (!Console.IsInputRedirected) Ui.PressEnter("Press Enter to begin the final battle.");

        GymBattleOutcome outcome = RunGymBattle(champion);
        if (outcome == GymBattleOutcome.LeaderDefeated)
        {
            _championDefeated = true;
            DispatchOrThrow(new GrantCurrencyCommand(ContentCatalog.CurrencyGold, champion.GoldReward));
            Ui.Clear();
            Ui.Heading("Champion!", "green");
            Ui.Note(champion.VictoryLine);
            Ui.Success($"+{champion.GoldReward} gold.");
            if (!Console.IsInputRedirected) Ui.PressEnter();
            return GameOutcome.Victory;
        }

        if (outcome == GymBattleOutcome.PlayerWiped)
        {
            return HandlePartyWipeOrGameOver();
        }

        return null;
    }

    /// <summary>
    /// Wraps <see cref="HandlePartyWipe"/> with a hard ceiling — past a certain number of
    /// total faints we declare a run-ending Defeat. Without this the headless smoke test
    /// could ping-pong between a gym it can't beat and a heal pad forever.
    /// </summary>
    private const int MaxFaintsBeforeGameOver = 5;

    private GameOutcome? HandlePartyWipeOrGameOver()
    {
        HandlePartyWipe();
        if (_faintWarpCount >= MaxFaintsBeforeGameOver)
        {
            Ui.Failure("Your party has fainted too many times. Your journey ends here.");
            return GameOutcome.Defeat;
        }
        return null;
    }

    private int MainQuestProgress()
    {
        if (_gameState.QuestState.TryGet(MainQuest.QuestId, out QuestInstanceState quest))
        {
            return quest.GetObjectiveProgress(MainQuest.ObjectiveId);
        }
        return 0;
    }

    // ----- Shop --------------------------------------------------------------------------

    private void HandleShopPad()
    {
        if (Console.IsInputRedirected)
        {
            // Headless mode: silently skip the shop so the smoke test doesn't deadlock.
            Ui.Note("A shop counter. (Skipping in headless mode.)");
            return;
        }

        OpenShopMenu();
    }

    private void OpenShopMenu()
    {
        while (true)
        {
            Ui.Clear();
            Ui.Heading("Town Shop", "yellow");
            long gold = WalletBalance(ContentCatalog.CurrencyGold);
            Ui.Info($"You have [bold yellow]{gold} gold[/].   Bag: {InventorySlotsUsed()} / {InventoryCapacity()} slots.");
            Ui.Line();

            List<string> labels = new();
            foreach (Item it in Items.All)
            {
                int owned = OwnedQuantity(it.Id);
                labels.Add($"{it.DisplayName} — {it.BuyPrice} gold   (owned: {owned})   — {it.Description}");
            }
            labels.Add("← Leave shop");

            int pick = Ui.ChooseOption("What'll it be?", labels);
            if (pick >= Items.All.Count) return;

            Item item = Items.All[pick];
            if (gold < item.BuyPrice)
            {
                Ui.Failure("Not enough gold.");
                Ui.PressEnter();
                continue;
            }

            DomainResult result = _dispatcher.Dispatch(_gameState, new BuyFromShopCommand(TownShop.ShopId, item.Id, quantity: 1, priceOptionIndex: 0), Ctx());
            if (!result.IsSuccess)
            {
                Ui.Failure($"Purchase failed: {result.Error?.Message}");
                Ui.PressEnter();
                continue;
            }

            Ui.Success($"Bought 1× {item.DisplayName} for {item.BuyPrice} gold.");
            Ui.PressEnter();
        }
    }

    private long WalletBalance(string currencyId) => _gameState.CurrencyWallet.GetBalance(currencyId);

    private int OwnedQuantity(string itemId)
    {
        int total = 0;
        foreach (InventoryStack stack in _gameState.InventoryBag.Stacks)
        {
            if (stack.ItemId == itemId) total += stack.Quantity;
        }
        return total;
    }

    private int InventorySlotsUsed() => _gameState.InventoryBag.Stacks.Count;

    private int InventoryCapacity() => _gameState.InventoryBag.CapacitySlots;

    // ----- In-battle item use ----------------------------------------------------------

    /// <summary>
    /// Player picked "Items" in the battle menu. Lists owned items; selecting one consumes
    /// it and applies its effect. Returns true when an item was successfully used (so the
    /// caller can treat that as the turn action), false if the player backed out.
    /// </summary>
    private bool OpenBattleItemsMenu(string opponentActorId)
    {
        List<Item> owned = new();
        foreach (Item it in Items.All)
        {
            int qty = OwnedQuantity(it.Id);
            if (qty <= 0) continue;
            // Capture balls aren't useful in trainer battles — leader mons can't be caught.
            if (_inTrainerBattle && it.Kind == ItemKind.CaptureBall) continue;
            owned.Add(it);
        }

        if (owned.Count == 0)
        {
            Ui.Note("Your bag has no usable items.");
            Ui.PressEnter();
            return false;
        }

        List<string> labels = new();
        foreach (Item it in owned)
        {
            labels.Add($"{it.DisplayName} ×{OwnedQuantity(it.Id)}  — {it.Description}");
        }
        labels.Add("← Back");

        int pick = Ui.ChooseOption("Use which item?", labels);
        if (pick >= owned.Count) return false;

        return UseItemInBattle(owned[pick], opponentActorId);
    }

    private bool UseItemInBattle(Item item, string opponentActorId)
    {
        switch (item.Kind)
        {
            case ItemKind.CaptureBall:
                return TryCaptureWithBall(item, opponentActorId);
            case ItemKind.HealHp:
                return UseHealItem(item, item.Magnitude);
            case ItemKind.FullRestoreHp:
                return UseHealItem(item, int.MaxValue);
            case ItemKind.Revive:
                return UseReviveItem(item);
            default:
                return false;
        }
    }

    private bool TryCaptureWithBall(Item ball, string opponentActorId)
    {
        if (_inTrainerBattle)
        {
            Ui.Failure("You can't capture a trainer's monster.");
            Ui.PressEnter();
            return false;
        }
        if (_gameState.PartyState.Members.Count >= _gameState.PartyState.MaxRoster)
        {
            Ui.Note("Your roster is full.");
            Ui.PressEnter();
            return false;
        }
        int wildLevel = _gameState.ProgressionState.TryGet(opponentActorId, out var prog) ? prog.Level : 5;
        if (wildLevel > LevelCap)
        {
            Ui.Note($"Your badges only command monsters up to level {LevelCap}. This one is level {wildLevel}.");
            Ui.PressEnter();
            return false;
        }

        DomainResult consume = _dispatcher.Dispatch(_gameState, new ConsumeInventoryItemCommand(ball.Id, 1), Ctx());
        if (!consume.IsSuccess)
        {
            Ui.Failure($"Out of {ball.DisplayName}.");
            Ui.PressEnter();
            return false;
        }

        string activeId = ActivePartyActorId();
        DomainResult capture = _dispatcher.Dispatch(_gameState, new AttemptCaptureCommand(activeId, opponentActorId, bonusPercent: ball.Magnitude), Ctx());
        if (!capture.IsSuccess)
        {
            Ui.Failure($"Capture failed: {capture.Error?.Message}");
            Ui.PressEnter();
            return false;
        }

        return true;
    }

    private bool UseHealItem(Item potion, int amount)
    {
        if (_gameState.ActiveBattle is null) return false;
        string activeId = ActivePartyActorId();
        BattleActorState actor = _gameState.ActiveBattle.Actors[activeId];
        if (actor.Hp >= actor.MaxHp)
        {
            Ui.Note($"{actor.DisplayName} is already at full HP.");
            Ui.PressEnter();
            return false;
        }

        DomainResult consume = _dispatcher.Dispatch(_gameState, new ConsumeInventoryItemCommand(potion.Id, 1), Ctx());
        if (!consume.IsSuccess) return false;

        int before = actor.Hp;
        int healed = Math.Min(actor.MaxHp - before, amount);
        actor.Hp = before + healed;
        _currentHpByActor[activeId] = actor.Hp;
        Ui.Success($"Used {potion.DisplayName}. {actor.DisplayName} recovered {healed} HP.");
        Ui.PressEnter();
        return true;
    }

    private bool UseReviveItem(Item revive)
    {
        List<PartyMember> fainted = _gameState.PartyState.Members
            .Where(m => _currentHpByActor.GetValueOrDefault(m.ActorId, 0) <= 0)
            .ToList();
        if (fainted.Count == 0)
        {
            Ui.Note("No fainted monsters to revive.");
            Ui.PressEnter();
            return false;
        }

        List<string> labels = new();
        foreach (PartyMember m in fainted)
        {
            MonsterSpecies sp = MonsterRoster.Get(_speciesByActor[m.ActorId]);
            int lvl = _gameState.ProgressionState.TryGet(m.ActorId, out var prog) ? prog.Level : 5;
            labels.Add($"{sp.DisplayName} (Lv {lvl})");
        }
        labels.Add("← Back");

        int pick = Ui.ChooseOption("Revive which monster?", labels);
        if (pick >= fainted.Count) return false;

        PartyMember target = fainted[pick];
        DomainResult consume = _dispatcher.Dispatch(_gameState, new ConsumeInventoryItemCommand(revive.Id, 1), Ctx());
        if (!consume.IsSuccess) return false;

        MonsterSpecies sp2 = MonsterRoster.Get(_speciesByActor[target.ActorId]);
        int level = _gameState.ProgressionState.TryGet(target.ActorId, out var pr) ? pr.Level : 5;
        int restored = (ComputeMaxHp(sp2, level) * revive.Magnitude) / 100;
        if (restored < 1) restored = 1;
        _currentHpByActor[target.ActorId] = restored;
        if (_gameState.ActiveBattle is not null && _gameState.ActiveBattle.TryGetActor(target.ActorId, out BattleActorState ba))
        {
            ba.Hp = restored;
        }
        Ui.Success($"{sp2.DisplayName} revived with {restored} HP.");
        Ui.PressEnter();
        return true;
    }

    private GameOutcome? ResolveWildEncounter(WorldScreen screen)
    {
        BattleResult result = StartAndRunBattle(screen);
        if (result == BattleResult.PartyWiped)
        {
            return HandlePartyWipeOrGameOver();
        }

        return null;
    }

    // ----- Rendering -------------------------------------------------------------------

    private void RenderWorldScreen(WorldScreen screen)
    {
        BiomeProfile profile = BiomeRegistry.Get(screen.Biome);
        int gym = _world!.GymNumberFor(_currentScreenIndex);
        string headline = gym > 0
            ? $"Screen {_currentScreenIndex + 1} / {_world!.LastScreenIndex + 1}   {profile.DisplayName}   [bold magenta]Gym {gym}[/]"
            : $"Screen {_currentScreenIndex + 1} / {_world!.LastScreenIndex + 1}   {profile.DisplayName}";
        Ui.Heading(headline, "olive");
        long gold = WalletBalance(ContentCatalog.CurrencyGold);
        Ui.Info($"Arrows/WASD move. Q quits. Wardens: {MainQuestProgress()}/{World.GymCount}   Gold: {gold}");
        Ui.Line();
        Ui.RenderScreen(screen, _playerX, _playerY);
        Ui.Line();
        RenderPartyOneLine();
    }

    private void RenderPartyOneLine()
    {
        foreach (PartyMember m in _gameState.PartyState.Members)
        {
            MonsterSpecies sp = MonsterRoster.Get(_speciesByActor[m.ActorId]);
            int level = _gameState.ProgressionState.TryGet(m.ActorId, out var prog) ? prog.Level : 5;
            int hp = _currentHpByActor.GetValueOrDefault(m.ActorId, 0);
            int max = ComputeMaxHp(sp, level);
            string activeFlag = m.IsActive ? "[bold cyan][[active]][/]" : "[grey][[reserve]][/]";
            Ui.Line($" {activeFlag} {sp.DisplayName} Lv {level}  HP {hp}/{max}");
        }
    }

    // ----- Input -----------------------------------------------------------------------

    private (int dx, int dy)? ReadMovementInput()
    {
        if (Console.IsInputRedirected)
        {
            // Headless smoke-test mode: always march east. Transition logic + headlessTickGuard
            // keep the test from looping forever.
            return (1, 0);
        }

        while (true)
        {
            ConsoleKeyInfo key;
            try
            {
                key = Console.ReadKey(intercept: true);
            }
            catch (InvalidOperationException)
            {
                // No interactive console (xUnit / piped runner where IsInputRedirected
                // still reports false for non-Console.In stdin). Fall back to march-east
                // so the smoke test can converge instead of throwing.
                return (1, 0);
            }

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.W:
                case ConsoleKey.K:
                    return (0, -1);
                case ConsoleKey.DownArrow:
                case ConsoleKey.S:
                case ConsoleKey.J:
                    return (0, 1);
                case ConsoleKey.LeftArrow:
                case ConsoleKey.A:
                case ConsoleKey.H:
                    return (-1, 0);
                case ConsoleKey.RightArrow:
                case ConsoleKey.D:
                case ConsoleKey.L:
                    return (1, 0);
                case ConsoleKey.Q:
                case ConsoleKey.Escape:
                    return null;
            }
        }
    }

    private void HealPartyAndRestorePp()
    {
        foreach (PartyMember member in _gameState.PartyState.Members)
        {
            MonsterSpecies sp = MonsterRoster.Get(_speciesByActor[member.ActorId]);
            int level = _gameState.ProgressionState.TryGet(member.ActorId, out var prog) ? prog.Level : 5;
            _currentHpByActor[member.ActorId] = ComputeMaxHp(sp, level);

            foreach (string moveId in _movesByActor[member.ActorId])
            {
                BattleSkillDefinition moveDef = Moves.All.First(x => x.Id == moveId);
                DispatchOrThrow(new RestoreSkillPpCommand(member.ActorId, moveId, amount: moveDef.MaxPp));
            }
        }
    }

    /// <summary>
    /// Top up each party member's HP to MaxHp when crossing into a new screen east. Doesn't
    /// refill PP (PP recovery is gated behind heal pads). Skips downed members — only a
    /// heal pad can revive a fully-fainted mon. Phase 3 will replace this generous auto-heal
    /// with explicit potion use from the inventory bag.
    /// </summary>
    private void RestPartyOnScreenTransition()
    {
        foreach (PartyMember member in _gameState.PartyState.Members)
        {
            int current = _currentHpByActor.GetValueOrDefault(member.ActorId, 0);
            if (current <= 0) continue;
            MonsterSpecies sp = MonsterRoster.Get(_speciesByActor[member.ActorId]);
            int level = _gameState.ProgressionState.TryGet(member.ActorId, out var prog) ? prog.Level : 5;
            _currentHpByActor[member.ActorId] = ComputeMaxHp(sp, level);
        }
    }

    // ----- Scenes ----------------------------------------------------------------------

    private void ShowTitle()
    {
        Ui.Clear();
        Ui.Heading("Moonforge: Monster Catcher", "magenta");
        Ui.Line();
        Ui.Info("A small Pokemon-style game built on the Moonforge.Core engine.");
        Ui.Info($"Pick a starter, walk a procedurally-generated world from west to east through {World.ChampionScreenIndex + 1} screens,");
        Ui.Info($"clear {World.GymCount} gyms gating the road, and reach the Champion's Hall on the far side.");
        Ui.Line();
        Ui.Info("Move with arrow keys or WASD. Q quits. Tall grass (',') may trigger a wild encounter;");
        Ui.Info("step on a healing pad ('+') to fully restore your party.");
        Ui.PressEnter("Press Enter to begin.");
    }

    private string ChooseStarter()
    {
        Ui.Clear();
        Ui.Heading("Choose your starter", "cyan");
        // Splashling is listed first because it's the safest type matchup against the
        // first gym (Water > Rock). The headless smoke test always picks option 0, so
        // making it the most-balanced opener keeps the auto-test winnable. Interactively,
        // the player can still pick whichever they want.
        List<string> options = new()
        {
            FormatStarterChoice(SpeciesIds.Splashling),
            FormatStarterChoice(SpeciesIds.Emberkin),
            FormatStarterChoice(SpeciesIds.Sproutling)
        };

        int pick = Ui.ChooseOption("Which monster will you take?", options);
        return pick switch
        {
            0 => SpeciesIds.Splashling,
            1 => SpeciesIds.Emberkin,
            _ => SpeciesIds.Sproutling
        };
    }

    private static string FormatStarterChoice(string speciesId)
    {
        MonsterSpecies sp = MonsterRoster.Get(speciesId);
        string types = string.Join("/", sp.TypeIds.Select(Ui.TypeLabel));
        return $"{sp.DisplayName} ({types}) — starting moves: {string.Join(", ", sp.StartingMoves.Select(MoveDisplayName))}";
    }

    // ----- Battle (mostly unchanged from the pre-multi-screen version) -----------------

    private BattleResult StartAndRunBattle(WorldScreen screen)
    {
        (string wildSpeciesId, int wildLevel) = RollWildEncounter(screen);
        string wildActorId = NewWildActorId();
        _speciesByActor[wildActorId] = wildSpeciesId;
        _displayNameByActor[wildActorId] = MonsterRoster.Get(wildSpeciesId).DisplayName;
        _movesByActor[wildActorId] = new List<string>(MonsterRoster.Get(wildSpeciesId).StartingMoves);
        _currentHpByActor[wildActorId] = ComputeMaxHp(MonsterRoster.Get(wildSpeciesId), wildLevel);
        DispatchOrThrow(new ConfigureActorProgressionCommand(wildActorId, ContentCatalog.ExperienceCurveId, level: wildLevel, xp: XpFloorForLevel(wildLevel)));

        BattleActorDefinition activeMon = BuildPlayerActor(ActivePartyActorId());
        BattleActorDefinition wild = BuildWildActor(wildActorId, wildLevel);

        _battleSequence++;
        // Wild kills drop a small amount of gold scaled to level — buys a Potion every few
        // fights, an Ultraball after a couple dozen.
        int goldReward = 5 + wildLevel * 3;
        StartBattleCommand startCmd = new(
            battleId: $"battle.{_battleSequence}",
            actors: new[] { activeMon, wild },
            skills: Moves.All,
            seed: NextBattleSeed(),
            sequence: (ulong)_battleSequence,
            rewardCurrency: new[] { new CurrencyDelta(ContentCatalog.CurrencyGold, goldReward) });

        DispatchOrThrow(startCmd);

        _gameState.ActiveBattle!.Actors[activeMon.ActorId].Hp = _currentHpByActor[activeMon.ActorId];

        return RunBattleLoop(wildActorId);
    }

    private BattleResult RunBattleLoop(string wildActorId)
    {
        _battleEvents.Clear();
        _sink.DrainNewEvents();

        while (_gameState.ActiveBattle is not null && _gameState.ActiveBattle.Status == BattleStatus.Active)
        {
            BattleActorState currentActor = _gameState.ActiveBattle.Actors[_gameState.ActiveBattle.TurnOrder[_gameState.ActiveBattle.TurnIndex]];

            Ui.Clear();
            RenderBattle(wildActorId);

            if (currentActor.PlayerControlled)
            {
                BattleResult? earlyExit = TakePlayerTurn(wildActorId);
                DrainAndNarrate();
                if (earlyExit.HasValue) return earlyExit.Value;
            }
            else
            {
                TakeEnemyTurn(currentActor);
                DrainAndNarrate();
            }

            if (Console.IsInputRedirected) continue;
            if (_gameState.ActiveBattle is not null && _gameState.ActiveBattle.Status == BattleStatus.Active)
            {
                Ui.PressEnter("Press Enter for the next turn.");
            }
        }

        return AfterBattle(wildActorId);
    }

    private void DrainAndNarrate()
    {
        foreach (DomainEvent ev in _sink.DrainNewEvents())
        {
            _battleEvents.Add(ev);
            NarrateOne(ev);
        }
    }

    private BattleResult? TakePlayerTurn(string wildActorId)
    {
        string activeId = ActivePartyActorId();
        // Trainer battles disable Flee — the gym is an honor-bound fight you can't walk out
        // of. Captures are also gated out by hiding ball items inside the Items submenu.
        List<string> menu = _inTrainerBattle
            ? new List<string> { "Attack", "Items", "Swap monster" }
            : new List<string> { "Attack", "Items", "Swap monster", "Flee" };
        int pick = Ui.ChooseOption("Your move:", menu);

        // Attack always lives at index 0; rest of menu order varies by battle type.
        if (pick == 0)
        {
            string? move = ChooseMoveOrCancel(activeId);
            if (move is null) return Console.IsInputRedirected ? BattleResult.Fled : null;
            DomainResult result = _dispatcher.Dispatch(_gameState, new UseBattleSkillCommand(activeId, move, wildActorId), Ctx());
            if (!result.IsSuccess)
            {
                Ui.Failure($"Can't use that: {result.Error?.Message}");
                Ui.PressEnter();
                if (Console.IsInputRedirected) { _gameState.ActiveBattle = null; return BattleResult.Fled; }
                return null;
            }
            return null;
        }

        if (pick == 1)
        {
            // Items submenu — returns true when an item was successfully used. Whether it was
            // or not, control returns to the same player so they can pick something else
            // (items don't currently consume a turn — a deliberate simplification).
            OpenBattleItemsMenu(wildActorId);
            return null;
        }

        if (pick == 2)
        {
            string? reserve = ChooseReserveOrCancel();
            if (reserve is null) return Console.IsInputRedirected ? BattleResult.Fled : null;
            BattleActorDefinition inActor = BuildPlayerActor(reserve);
            DomainResult swap = _dispatcher.Dispatch(_gameState, new SwapBattleActorCommand(activeId, inActor), Ctx());
            if (!swap.IsSuccess)
            {
                Ui.Failure($"Swap failed: {swap.Error?.Message}");
                Ui.PressEnter();
                return null;
            }
            if (_gameState.ActiveBattle is not null && _gameState.ActiveBattle.TryGetActor(reserve, out BattleActorState swappedIn))
            {
                swappedIn.Hp = _currentHpByActor[reserve];
            }
            return null;
        }

        // Pick == 3 → Flee (only available in wild battles).
        Ui.Note("You disengaged from the wild monster.");
        _gameState.ActiveBattle = null;
        Ui.PressEnter();
        return BattleResult.Fled;
    }

    private void TakeEnemyTurn(BattleActorState enemy)
    {
        DomainResult result = _dispatcher.Dispatch(_gameState, new ExecuteAiTurnCommand(), Ctx());
        if (!result.IsSuccess)
        {
            Ui.Failure($"Enemy turn error: {result.Error?.Message}");
        }
    }

    private BattleResult AfterBattle(string wildActorId)
    {
        DrainAndNarrate();
        SnapshotPartyHp();

        foreach (BattleActorCapturedEvent cap in _battleEvents.OfType<BattleActorCapturedEvent>())
        {
            _displayNameByActor[cap.CapturedActorId] = MonsterRoster.Get(_speciesByActor[cap.CapturedActorId]).DisplayName;
            _currentHpByActor[cap.CapturedActorId] = cap.HpAtCapture;

            MonsterSpecies sp = MonsterRoster.Get(_speciesByActor[cap.CapturedActorId]);
            if (sp.EvolvesIntoId is not null)
            {
                DispatchOrThrow(new ConfigureActorEvolutionsCommand(cap.CapturedActorId, new[] { EvolutionIdFor(sp.Id) }));
            }
        }

        foreach (EvolutionTriggeredEvent evo in _battleEvents.OfType<EvolutionTriggeredEvent>())
        {
            if (!string.IsNullOrWhiteSpace(evo.EvolvedSpeciesId)
                && _speciesByActor.ContainsKey(evo.ActorId)
                && _speciesByActor[evo.ActorId] != evo.EvolvedSpeciesId)
            {
                ApplyEvolution(evo.ActorId, evo.EvolvedSpeciesId!);
            }
        }

        foreach (LevelUpEvent up in _battleEvents.OfType<LevelUpEvent>())
        {
            if (!_speciesByActor.TryGetValue(up.ActorId, out string? speciesId)) continue;
            if (!MonsterRoster.Get(speciesId).LearnedMoves.TryGetValue(up.ToLevel, out string? newMove)) continue;
            if (_movesByActor[up.ActorId].Contains(newMove)) continue;
            _movesByActor[up.ActorId].Add(newMove);
            Ui.Success($"{_displayNameByActor[up.ActorId]} learned {MoveDisplayName(newMove)}!");
        }

        bool playerWiped = _gameState.PartyState.Members.All(m => _currentHpByActor.GetValueOrDefault(m.ActorId, 0) <= 0);

        if (_gameState.ActiveBattle is not null) _gameState.ActiveBattle = null;

        if (!Console.IsInputRedirected)
        {
            Ui.PressEnter("Press Enter to continue.");
        }

        return playerWiped ? BattleResult.PartyWiped : BattleResult.Cleared;
    }

    // ----- Battle UI helpers -----------------------------------------------------------

    private void RenderBattle(string wildActorId)
    {
        if (_gameState.ActiveBattle is null) return;
        BattleState battle = _gameState.ActiveBattle;
        string activeId = ActivePartyActorId();
        BattleActorState player = battle.Actors[activeId];
        BattleActorState wild = battle.Actors[wildActorId];
        int playerLevel = _gameState.ProgressionState.TryGet(activeId, out var pp) ? pp.Level : 5;
        int wildLevel = _gameState.ProgressionState.TryGet(wildActorId, out var wp) ? wp.Level : 5;

        string opponentTitle = _inTrainerBattle ? wild.DisplayName : $"Wild {wild.DisplayName}";
        Ui.Heading($"{opponentTitle} appeared!", "red");
        Ui.Line($"[red]{opponentTitle} (Lv {wildLevel})[/]  {Ui.HpBar(wild.Hp, wild.MaxHp)} {wild.Hp}/{wild.MaxHp}");
        Ui.Line($"  Types: {FormatTypeLine(wildActorId)}");
        Ui.Line();
        Ui.Line($"[cyan]{player.DisplayName} (Lv {playerLevel})[/]  {Ui.HpBar(player.Hp, player.MaxHp)} {player.Hp}/{player.MaxHp}");
        Ui.Line($"  Types: {FormatTypeLine(activeId)}");
        Ui.Line();
    }

    private string? ChooseMoveOrCancel(string actorId)
    {
        BattleActorState actor = _gameState.ActiveBattle!.Actors[actorId];
        List<string> moves = _movesByActor[actorId];
        List<string> labels = new();
        foreach (string moveId in moves)
        {
            BattleSkillDefinition def = Moves.All.First(x => x.Id == moveId);
            int pp = actor.SkillPp.TryGetValue(moveId, out int p) ? p : def.MaxPp;
            string typeLabel = def.DamageTypeId is null ? "—" : Ui.TypeLabel(def.DamageTypeId);
            labels.Add($"{def.DisplayName ?? def.Id} [{typeLabel}, power {def.Power}, PP {pp}/{def.MaxPp}]");
        }

        labels.Add("← Back");
        int pick = Ui.ChooseOption("Choose a move:", labels);
        if (pick >= moves.Count) return null;
        return moves[pick];
    }

    private string? ChooseReserveOrCancel()
    {
        List<PartyMember> reserves = _gameState.PartyState.Members.Where(m => !m.IsActive).ToList();
        reserves = reserves.Where(m => _currentHpByActor.GetValueOrDefault(m.ActorId, 0) > 0).ToList();
        if (reserves.Count == 0)
        {
            Ui.Note("No conscious reserves to swap in.");
            Ui.PressEnter();
            return null;
        }

        List<string> labels = new();
        foreach (PartyMember m in reserves)
        {
            MonsterSpecies sp = MonsterRoster.Get(_speciesByActor[m.ActorId]);
            int lvl = _gameState.ProgressionState.TryGet(m.ActorId, out var prog) ? prog.Level : 5;
            int hp = _currentHpByActor[m.ActorId];
            int max = ComputeMaxHp(sp, lvl);
            labels.Add($"{sp.DisplayName} (Lv {lvl})  HP {hp}/{max}");
        }

        labels.Add("← Back");
        int pick = Ui.ChooseOption("Swap to which monster?", labels);
        if (pick >= reserves.Count) return null;
        return reserves[pick].ActorId;
    }

    private void NarrateOne(DomainEvent ev)
    {
        switch (ev)
        {
            case BattleActionResolvedEvent action when action.WasHeal:
                Ui.Line($"[green]{ResolveDisplayName(action.ActorId)} healed {ResolveDisplayName(action.TargetActorId)} for {action.Amount}.[/]");
                break;
            case BattleActionResolvedEvent action:
                string crit = action.WasCritical ? " [yellow](critical!)[/]" : string.Empty;
                Ui.Line($"{ResolveDisplayName(action.ActorId)} → {action.SkillId.Replace("move.", "")} on {ResolveDisplayName(action.TargetActorId)} dealt {action.Amount}.{crit}");
                break;
            case BattleActionMissedEvent miss:
                Ui.Info($"{ResolveDisplayName(miss.ActorId)}'s {miss.SkillId.Replace("move.", "")} missed.");
                break;
            case BattleActorCapturedEvent cap:
                Ui.Success($"Caught {ResolveDisplayName(cap.CapturedActorId)}! Added to your party as a reserve.");
                break;
            case CaptureAttemptFailedEvent fail:
                Ui.Note($"The capture failed (chance was {fail.RolledChancePercent}%).");
                break;
            case SpeciesFirstEncounteredEvent enc:
                Ui.Info($"Bestiary: new species encountered — {Ui.TypeLabel(enc.SpeciesId.Replace("species.", ""))}.");
                break;
            case SpeciesFirstCapturedEvent ccap:
                Ui.Success($"Bestiary: new species captured — {Ui.TypeLabel(ccap.SpeciesId.Replace("species.", ""))}.");
                break;
            case ExperienceGrantedEvent xp:
                Ui.Info($"{ResolveDisplayName(xp.ActorId)} gained {xp.Amount} XP.");
                break;
            case LevelUpEvent up:
                Ui.Success($"{ResolveDisplayName(up.ActorId)} grew to level {up.ToLevel}!");
                break;
            case EvolutionTriggeredEvent evo:
                Ui.Success($"{ResolveDisplayName(evo.ActorId)} is evolving!");
                break;
            case BattleEndedEvent end:
                // BattleEndedEvent.FinalActorHp is the engine's post-battle HP snapshot —
                // copy it into our cross-battle tracking dict here, where the battle is
                // already gone (ActiveBattle == null) and SnapshotPartyHp can no longer
                // read from it. This is the load-bearing step for party-wipe detection
                // and for the next battle's HP-restore override.
                foreach (KeyValuePair<string, int> kv in end.FinalActorHp)
                {
                    _currentHpByActor[kv.Key] = kv.Value;
                }
                string color = end.Status == BattleStatus.Victory ? "green" : "red";
                Ui.Line($"[bold {color}]Battle {end.Status.ToString().ToLowerInvariant()}.[/]");
                break;
        }
    }

    // ----- Party management ------------------------------------------------------------

    private string AddPartyMonster(string speciesId, int level, bool active)
    {
        _playerActorCounter++;
        string actorId = $"{PlayerActorPrefix}.{_playerActorCounter}";
        _speciesByActor[actorId] = speciesId;
        _displayNameByActor[actorId] = MonsterRoster.Get(speciesId).DisplayName;
        _movesByActor[actorId] = new List<string>(MonsterRoster.Get(speciesId).StartingMoves);

        // Configure progression at level 1, then GrantExperienceCommand to climb to the
        // target level — this fires LevelUpEvent for each level crossed, which triggers
        // the engine's evolution reactor and the sample's learnset additions. Setting
        // level directly via ConfigureActorProgressionCommand would bypass both because
        // no LevelUpEvent ever fires.
        DispatchOrThrow(new ConfigureActorProgressionCommand(actorId, ContentCatalog.ExperienceCurveId, level: 1, xp: 0));
        DispatchOrThrow(new AddPartyMemberCommand(actorId, active: active));

        // Evolution eligibility must be configured BEFORE the XP grant so the evolution
        // reactor sees it when it processes the level-up cascade.
        MonsterSpecies sp = MonsterRoster.Get(speciesId);
        if (sp.EvolvesIntoId is not null)
        {
            string evolutionId = EvolutionIdFor(speciesId);
            DispatchOrThrow(new ConfigureActorEvolutionsCommand(actorId, new[] { evolutionId }));
        }

        if (level > 1)
        {
            DispatchOrThrow(new GrantExperienceCommand(actorId, XpFloorForLevel(level)));
            // Apply any evolution / learnset events the cascade emitted before they leak
            // into the next battle's event drain. Mirrors what AfterBattle does for
            // post-battle level-ups, but scoped to setup so we don't disturb _battleEvents.
            DrainAndApplyProgression();
        }

        _currentHpByActor[actorId] = ComputeMaxHp(MonsterRoster.Get(_speciesByActor[actorId]), level);
        return actorId;
    }

    /// <summary>
    /// Drains the event sink and applies any progression-related events (level-up
    /// learnsets, evolutions) outside of a battle. Used during initial party setup so the
    /// cascade emitted by <see cref="GrantExperienceCommand"/> isn't observed as part of
    /// the next battle's events.
    /// </summary>
    private void DrainAndApplyProgression()
    {
        foreach (DomainEvent ev in _sink.DrainNewEvents())
        {
            switch (ev)
            {
                case EvolutionTriggeredEvent evo:
                    if (!string.IsNullOrWhiteSpace(evo.EvolvedSpeciesId)
                        && _speciesByActor.ContainsKey(evo.ActorId)
                        && _speciesByActor[evo.ActorId] != evo.EvolvedSpeciesId)
                    {
                        ApplyEvolution(evo.ActorId, evo.EvolvedSpeciesId!);
                    }
                    break;
                case LevelUpEvent up:
                    if (_speciesByActor.TryGetValue(up.ActorId, out string? speciesId)
                        && MonsterRoster.Get(speciesId).LearnedMoves.TryGetValue(up.ToLevel, out string? newMove)
                        && !_movesByActor[up.ActorId].Contains(newMove))
                    {
                        _movesByActor[up.ActorId].Add(newMove);
                    }
                    break;
            }
        }
    }

    private void ApplyEvolution(string actorId, string newSpeciesId)
    {
        string oldSpeciesId = _speciesByActor[actorId];
        _speciesByActor[actorId] = newSpeciesId;
        MonsterSpecies newSp = MonsterRoster.Get(newSpeciesId);
        _displayNameByActor[actorId] = newSp.DisplayName;

        List<string> mergedMoves = new(_movesByActor[actorId]);
        foreach (string m in newSp.StartingMoves)
        {
            if (!mergedMoves.Contains(m)) mergedMoves.Add(m);
        }
        _movesByActor[actorId] = mergedMoves;

        int level = _gameState.ProgressionState.TryGet(actorId, out var prog) ? prog.Level : 5;
        int oldMax = ComputeMaxHp(MonsterRoster.Get(oldSpeciesId), level);
        int newMax = ComputeMaxHp(newSp, level);
        int currentHp = _currentHpByActor.GetValueOrDefault(actorId, oldMax);
        _currentHpByActor[actorId] = oldMax > 0
            ? Math.Max(1, (int)Math.Round((double)currentHp / oldMax * newMax))
            : newMax;

        Ui.Success($"{newSp.DisplayName} evolved from {MonsterRoster.Get(oldSpeciesId).DisplayName}! Max HP grew to {newMax}.");
    }

    private void SnapshotPartyHp()
    {
        // Captures HP from the engine's battle state for actors still in the battle.
        // After the battle ends, ActiveBattle is null and this is a no-op — the
        // BattleEndedEvent case in NarrateOne takes over via the event's FinalActorHp
        // snapshot.
        if (_gameState.ActiveBattle is null) return;
        foreach (PartyMember m in _gameState.PartyState.Members)
        {
            if (_gameState.ActiveBattle.TryGetActor(m.ActorId, out BattleActorState actor))
            {
                _currentHpByActor[m.ActorId] = actor.Hp;
            }
        }
    }

    private void RenderParty()
    {
        foreach (PartyMember m in _gameState.PartyState.Members)
        {
            MonsterSpecies sp = MonsterRoster.Get(_speciesByActor[m.ActorId]);
            int level = _gameState.ProgressionState.TryGet(m.ActorId, out var prog) ? prog.Level : 5;
            int hp = _currentHpByActor.GetValueOrDefault(m.ActorId, 0);
            int max = ComputeMaxHp(sp, level);
            string activeFlag = m.IsActive ? "[bold cyan][[active]][/]" : "[grey][[reserve]][/]";
            Ui.Line($" {activeFlag} {sp.DisplayName} Lv {level}  {Ui.HpBar(hp, max, 14)} {hp}/{max}");
        }
    }

    private void ShowFinalStats()
    {
        Ui.Line();
        Ui.Heading("Final standings", "magenta");
        RenderParty();
        Ui.Line();
        Ui.Info($"Furthest screen reached: {_screensReached} / {(_world?.LastScreenIndex ?? 0) + 1}.");
        Ui.Info($"Wardens defeated: {MainQuestProgress()} / {World.GymCount}.   Champion: {(_championDefeated ? "defeated" : "standing")}.");
        Ui.Info($"Gold: {WalletBalance(ContentCatalog.CurrencyGold)}.   Faints: {_faintWarpCount}.");
        Ui.Info($"Bestiary — encountered: {_gameState.BestiaryState.EncounteredSpeciesCount}, captured: {_gameState.BestiaryState.CapturedSpeciesCount}.");
    }

    // ----- Actor construction ----------------------------------------------------------

    private BattleActorDefinition BuildPlayerActor(string actorId)
    {
        MonsterSpecies sp = MonsterRoster.Get(_speciesByActor[actorId]);
        int level = _gameState.ProgressionState.TryGet(actorId, out var prog) ? prog.Level : 5;
        return BuildActor(actorId, sp, level, faction: CombatFaction.Party, playerControlled: true, captureBaseRate: 0);
    }

    private BattleActorDefinition BuildWildActor(string actorId, int level)
    {
        MonsterSpecies sp = MonsterRoster.Get(_speciesByActor[actorId]);
        return BuildActor(actorId, sp, level, faction: CombatFaction.Enemy, playerControlled: false, captureBaseRate: sp.CaptureBaseRate);
    }

    private BattleActorDefinition BuildActor(string actorId, MonsterSpecies sp, int level, CombatFaction faction, bool playerControlled, int captureBaseRate)
    {
        return new BattleActorDefinition(
            actorId: actorId,
            displayName: sp.DisplayName,
            faction: faction,
            maxHp: ComputeMaxHp(sp, level),
            atk: ScaleStat(sp.BaseAtk, level),
            def: ScaleStat(sp.BaseDef, level),
            matk: ScaleStat(sp.BaseMatk, level),
            mdef: ScaleStat(sp.BaseMdef, level),
            initiative: ScaleStat(sp.BaseInitiative, level),
            skillIds: faction == CombatFaction.Party && _movesByActor.TryGetValue(actorId, out var moves)
                ? moves.ToArray()
                : sp.StartingMoves.ToArray(),
            playerControlled: playerControlled,
            xpReward: faction == CombatFaction.Enemy ? 8 + level * 4 : 0,
            defenderTypeIds: sp.TypeIds.ToArray(),
            captureBaseRate: captureBaseRate,
            speciesId: sp.Id);
    }

    private static int ComputeMaxHp(MonsterSpecies sp, int level)
    {
        return sp.BaseMaxHp + Math.Max(0, level - 5) * 4;
    }

    private static int ScaleStat(int baseStat, int level)
    {
        return baseStat + Math.Max(0, level - 5);
    }

    // ----- Encounter generation --------------------------------------------------------

    private (string speciesId, int level) RollWildEncounter(WorldScreen screen)
    {
        BiomeProfile profile = BiomeRegistry.Get(screen.Biome);
        string speciesId = profile.WildPool[_rng.NextInt(profile.WildPool.Count)];

        // Level scales with screen index: ~level 3-5 at screen 0, ~25 at screen 40+.
        int baseLevel = 3 + (_currentScreenIndex / 2);
        int level = baseLevel + _rng.NextInt(3);
        return (speciesId, level);
    }

    // ----- Helpers ---------------------------------------------------------------------

    private string ActivePartyActorId()
    {
        return _gameState.PartyState.Members.First(m => m.IsActive).ActorId;
    }

    private string NewWildActorId()
    {
        _wildActorCounter++;
        return $"{WildActorPrefix}.{_wildActorCounter}";
    }

    private ulong NextBattleSeed()
    {
        return (ulong)_rng.NextInt(int.MaxValue);
    }

    private string ResolveDisplayName(string actorId)
    {
        return _displayNameByActor.TryGetValue(actorId, out string? n) ? n : actorId;
    }

    private string FormatTypeLine(string actorId)
    {
        MonsterSpecies sp = MonsterRoster.Get(_speciesByActor[actorId]);
        return string.Join(" / ", sp.TypeIds.Select(t => $"[{Ui.TypeColor(t)}]{Ui.TypeLabel(t)}[/]"));
    }

    private static string MoveDisplayName(string moveId)
    {
        BattleSkillDefinition? def = Moves.All.FirstOrDefault(x => x.Id == moveId);
        return def?.DisplayName ?? moveId;
    }

    private static string EvolutionIdFor(string speciesId) => speciesId switch
    {
        SpeciesIds.Emberkin => EvolutionIds.EmberkinToCinderfox,
        SpeciesIds.Splashling => EvolutionIds.SplashlingToHydrofin,
        SpeciesIds.Sproutling => EvolutionIds.SproutlingToLeafwing,
        _ => string.Empty
    };

    private long XpFloorForLevel(int level)
    {
        if (level <= 1) return 0;
        if (!_defs.TryGetExperienceCurve(ContentCatalog.ExperienceCurveId, out ExperienceCurveDefinition curve)) return 0;
        if (level - 2 >= curve.XpThresholds.Count) return curve.XpThresholds[curve.XpThresholds.Count - 1];
        return curve.XpThresholds[level - 2];
    }

    private CommandContext Ctx() => new(_rng, _clock, _formulas, _sink, _defs);

    private void DispatchOrThrow<TCommand>(TCommand command) where TCommand : ICommand
    {
        DomainResult result = _dispatcher.Dispatch(_gameState, command, Ctx());
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Dispatch failed for {typeof(TCommand).Name}: {result.Error?.Code} — {result.Error?.Message}");
        }
    }
}

internal enum BattleResult
{
    Cleared,
    Fled,
    PartyWiped
}

internal enum GymBattleOutcome
{
    LeaderDefeated,
    PlayerWiped,
    Fled
}

public enum GameOutcome
{
    Quit,
    Victory,
    Defeat
}
