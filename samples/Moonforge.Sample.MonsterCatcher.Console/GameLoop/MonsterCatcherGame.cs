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
using Moonforge.Core.Party;
using Moonforge.Core.Party.Commands;
using Moonforge.Core.Progression.Commands;
using Moonforge.Core.Progression.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;
using Moonforge.Core.Exploration;
using Moonforge.Core.Exploration.Commands;
using Moonforge.Sample.MonsterCatcher.Content;
using Moonforge.Sample.MonsterCatcher.Rendering;
using Moonforge.Sample.MonsterCatcher.WorldGen;

namespace Moonforge.Sample.MonsterCatcher.GameLoop;

/// <summary>
/// Battle-focused monster-catcher sample. Demonstrates the seven engine features added for
/// monster-catcher games: multi-character party, mid-battle swap, type effectiveness chart,
/// capture, evolution, bestiary, and per-skill PP — plus a persistent overworld map built
/// on the engine's Exploration module.
///
/// Flow: pick starter → walk a procedurally-generated overworld from west to east, fighting
/// wild monsters in grass tiles and resting at PokeCenters → reach the goal tile on the
/// east edge for victory. Defeat if every party member is downed.
///
/// The class also exercises the engine's reactor pattern — capture/evolution/bestiary
/// auto-tracking and PP persistence all flow through cross-module reactors with zero glue
/// here; the game just dispatches commands and reads state.
/// </summary>
internal sealed class MonsterCatcherGame
{
    public const string PlayerActorPrefix = "actor.player";
    public const string WildActorPrefix = "actor.wild";
    public const string PlayerExplorationActor = "exploration.player";
    public const int GrassEncounterChancePercent = 18;
    public const string MapId = "map.overworld";

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

    private Overworld? _overworld;
    private int _playerX;
    private int _playerY;
    private int _zoneHighWatermark = 1;        // highest zone the player has reached
    private readonly HashSet<int> _restedAtPokeCenters = new();
    private int _playerActorCounter;
    private int _wildActorCounter;
    private int _battleSequence;
    private int _headlessTickGuard;

    public MonsterCatcherGame(ulong seed = 12345)
    {
        _defs = ContentCatalog.Build();
        _rng = new Pcg32RandomSource(seed, 0);
        _clock = new SimulationClock(0);
        _formulas = new ExpressionFormulaEvaluator();
        _sink = new InMemoryDomainEventSink();
        _gameState = new GameState();
        _dispatcher = DefaultCommandDispatcher.Create();

        // Party = 1 active mon, up to 6 in roster (Pokemon-shaped).
        DispatchOrThrow(new ConfigurePartyCommand(maxActive: 1, maxRoster: 6));
    }

    /// <summary>Run the game from title to victory or defeat. Returns the final outcome.</summary>
    public GameOutcome Run()
    {
        ShowTitle();
        string starterSpeciesId = ChooseStarter();
        string starterActorId = AddPartyMonster(starterSpeciesId, level: 5, active: true);

        Ui.Clear();
        Ui.Heading($"Off you go!");
        Ui.Line($"You set out with [bold cyan]{Ui.TypeLabel(_displayNameByActor[starterActorId])}[/].");
        Ui.PressEnter();

        BuildOverworld();

        // Main loop ticks the overworld one player action at a time. Each tick may produce
        // a final outcome (Victory / Defeat / Quit) or null to keep going.
        while (true)
        {
            GameOutcome? finished = TickOverworld();
            if (finished.HasValue)
            {
                if (finished.Value == GameOutcome.Victory)
                {
                    Ui.Clear();
                    Ui.Heading("Champion!", "green");
                    Ui.Success("You reached the eastern shore. Your bond carried you all the way.");
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

    private void BuildOverworld()
    {
        _overworld = OverworldGenerator.Generate(_rng);
        _playerX = _overworld.Spawn.X;
        _playerY = _overworld.Spawn.Y;

        // Hand the engine the same tile grid via ConfigureExplorationMapCommand so MoveActor
        // validation uses the engine's walkability rules. We don't actually call MoveActor
        // here (the sample owns input + tile-effect dispatch directly), but configuring the
        // map demonstrates the integration shape and keeps things ready if a future change
        // wants to route moves through the engine.
        ExplorationTileFlags[] flags = OverworldGenerator.ToEngineFlags(_overworld);
        DispatchOrThrow(new ConfigureExplorationMapCommand(MapId, _overworld.Width, _overworld.Height, flags));
        DispatchOrThrow(new UpsertExplorationActorCommand(PlayerExplorationActor, _playerX, _playerY, blocksMovement: false));
    }

    private GameOutcome? TickOverworld()
    {
        Ui.Clear();
        RenderOverworldScreen();

        (int dx, int dy)? input = ReadMovementInput();
        if (!input.HasValue)
        {
            // Player pressed Q (or headless safety triggered).
            return GameOutcome.Quit;
        }

        int newX = _playerX + input.Value.dx;
        int newY = _playerY + input.Value.dy;
        if (!_overworld!.IsWalkable(newX, newY))
        {
            // Blocked — silently re-render and ask again. (Headless ticks bump the guard
            // so a wall-locked headless run can't loop forever.)
            if (Console.IsInputRedirected && ++_headlessTickGuard > _overworld.Width * 2)
            {
                return GameOutcome.Quit;
            }

            return null;
        }

        _playerX = newX;
        _playerY = newY;

        // Track the deepest zone the player has reached so encounter difficulty doesn't
        // decrement if they backtrack.
        int zone = Math.Max(1, _overworld.ZoneAt(_playerX, _playerY));
        if (zone > _zoneHighWatermark) _zoneHighWatermark = zone;

        // Dispatch the move through the engine so any future map-aware reactor sees the
        // ExplorationActorMovedEvent. The engine validates walkability with its own tile
        // flags — we've already pre-validated locally so the delta is guaranteed valid.
        DispatchOrThrow(new MoveActorCommand(PlayerExplorationActor, input.Value.dx, input.Value.dy));

        // Resolve tile effects.
        OverworldTile tile = _overworld.TileAt(_playerX, _playerY);
        switch (tile)
        {
            case OverworldTile.Goal:
                return GameOutcome.Victory;

            case OverworldTile.PokeCenter:
                return HandlePokeCenter();

            case OverworldTile.Grass:
                int chance = GrassEncounterChancePercent;
                if (_rng.NextInt(100) < chance)
                {
                    return ResolveWildEncounter(zone);
                }

                return null;

            default:
                return null;
        }
    }

    private GameOutcome? HandlePokeCenter()
    {
        int idx = (_playerY * _overworld!.Width) + _playerX;
        if (_restedAtPokeCenters.Add(idx))
        {
            Ui.Note("You step onto a PokeCenter pad. Your party is healed and PP restored.");
            HealPartyAndRestorePp();
            if (!Console.IsInputRedirected) Ui.PressEnter();
        }

        return null;
    }

    private GameOutcome? ResolveWildEncounter(int zone)
    {
        BattleResult result = StartAndRunBattle(zone);
        if (result == BattleResult.PartyWiped)
        {
            return GameOutcome.Defeat;
        }

        return null;
    }

    private void RenderOverworldScreen()
    {
        if (_overworld is null) return;

        Ui.Heading($"Zone {_overworld.ZoneAt(_playerX, _playerY)} / {_overworld.ZoneCount}", "olive");
        Ui.Info("Move with arrow keys or WASD. Q to quit. Grass is dangerous. C heals. > is the goal.");
        Ui.Line();
        Ui.RenderMap(_overworld, _playerX, _playerY);
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

    private (int dx, int dy)? ReadMovementInput()
    {
        if (Console.IsInputRedirected)
        {
            // Headless smoke-test mode: always march east. The tile-effect dispatcher handles
            // walls (bouncing) and the headlessTickGuard above caps total ticks if the path
            // is genuinely blocked.
            return (1, 0);
        }

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
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

    // ----- Scenes ----------------------------------------------------------------------

    private void ShowTitle()
    {
        Ui.Clear();
        Ui.Heading("Moonforge: Monster Catcher", "magenta");
        Ui.Line();
        Ui.Info("A small Pokemon-style demo built on the Moonforge.Core engine.");
        Ui.Info("Pick a starter, walk the overworld from west to east, catch what you can,");
        Ui.Info("survive what you can't, and reach the goal tile (>) on the east edge.");
        Ui.Line();
        Ui.Info("Move with arrow keys or WASD. Q quits. Stepping into tall grass (',') may trigger");
        Ui.Info("a wild encounter; stepping on a PokeCenter (C) fully heals you.");
        Ui.PressEnter("Press Enter to begin.");
    }

    private string ChooseStarter()
    {
        Ui.Clear();
        Ui.Heading("Choose your starter", "cyan");
        List<string> options = new()
        {
            FormatStarterChoice(SpeciesIds.Emberkin),
            FormatStarterChoice(SpeciesIds.Splashling),
            FormatStarterChoice(SpeciesIds.Sproutling)
        };

        int pick = Ui.ChooseOption("Which monster will you take?", options);
        return pick switch
        {
            0 => SpeciesIds.Emberkin,
            1 => SpeciesIds.Splashling,
            _ => SpeciesIds.Sproutling
        };
    }

    private static string FormatStarterChoice(string speciesId)
    {
        MonsterSpecies sp = MonsterRoster.Get(speciesId);
        string types = string.Join("/", sp.TypeIds.Select(Ui.TypeLabel));
        return $"{sp.DisplayName} ({types}) — starting moves: {string.Join(", ", sp.StartingMoves.Select(MoveDisplayName))}";
    }

    private BattleResult StartAndRunBattle(int zone)
    {
        (string wildSpeciesId, int wildLevel) = RollWildEncounterForZone(zone);
        string wildActorId = NewWildActorId();
        _speciesByActor[wildActorId] = wildSpeciesId;
        _displayNameByActor[wildActorId] = MonsterRoster.Get(wildSpeciesId).DisplayName;
        _movesByActor[wildActorId] = new List<string>(MonsterRoster.Get(wildSpeciesId).StartingMoves);
        _currentHpByActor[wildActorId] = ComputeMaxHp(MonsterRoster.Get(wildSpeciesId), wildLevel);
        // Wild needs progression too so it carries the right level for stat scaling — the
        // engine reads ProgressionState during damage calc if a stat block formula references
        // level; we configure but never grant XP to wild actors.
        DispatchOrThrow(new ConfigureActorProgressionCommand(wildActorId, ContentCatalog.ExperienceCurveId, level: wildLevel, xp: XpFloorForLevel(wildLevel)));

        BattleActorDefinition activeMon = BuildPlayerActor(ActivePartyActorId());
        BattleActorDefinition wild = BuildWildActor(wildActorId, wildLevel);

        _battleSequence++;
        StartBattleCommand startCmd = new(
            battleId: $"battle.{_battleSequence}",
            actors: new[] { activeMon, wild },
            skills: Moves.All,
            seed: NextBattleSeed(),
            sequence: (ulong)_battleSequence);

        DispatchOrThrow(startCmd);

        // Override the engine's default-full HP with our tracked current HP for the active
        // mon (the wild starts fresh, so its full HP is correct).
        _gameState.ActiveBattle!.Actors[activeMon.ActorId].Hp = _currentHpByActor[activeMon.ActorId];

        return RunBattleLoop(wildActorId);
    }

    private BattleResult RunBattleLoop(string wildActorId)
    {
        _battleEvents.Clear();
        // Drain any pre-battle events (e.g. PartyMemberAddedEvent from setup) so they don't
        // get attributed to this battle.
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
        List<string> menu = new() { "Attack", "Swap monster", "Capture", "Flee" };
        int pick = Ui.ChooseOption("Your move:", menu);
        switch (pick)
        {
            case 0: // Attack
            {
                string? move = ChooseMoveOrCancel(activeId);
                if (move is null) return Console.IsInputRedirected ? BattleResult.Fled : null;
                DomainResult result = _dispatcher.Dispatch(_gameState, new UseBattleSkillCommand(activeId, move, wildActorId), Ctx());
                if (!result.IsSuccess)
                {
                    Ui.Failure($"Can't use that: {result.Error?.Message}");
                    Ui.PressEnter();
                    // Headless mode can't recover from a stuck menu — flee out.
                    if (Console.IsInputRedirected) { _gameState.ActiveBattle = null; return BattleResult.Fled; }
                    return null;
                }
                return null;
            }
            case 1: // Swap
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
                // Restore the swap-in's HP from our tracked store.
                if (_gameState.ActiveBattle is not null && _gameState.ActiveBattle.TryGetActor(reserve, out BattleActorState swappedIn))
                {
                    swappedIn.Hp = _currentHpByActor[reserve];
                }
                return null;
            }
            case 2: // Capture
            {
                if (_gameState.PartyState.Members.Count >= _gameState.PartyState.MaxRoster)
                {
                    Ui.Note("Your roster is full. You'd need to release one first (not in this sample).");
                    Ui.PressEnter();
                    return null;
                }
                DomainResult capture = _dispatcher.Dispatch(_gameState, new AttemptCaptureCommand(activeId, wildActorId, bonusPercent: 150), Ctx());
                if (!capture.IsSuccess)
                {
                    Ui.Failure($"Capture failed: {capture.Error?.Message}");
                    Ui.PressEnter();
                    return null;
                }
                return null;
            }
            default: // Flee
            {
                Ui.Note("You disengaged from the wild monster.");
                // Tear the battle down without victory/defeat path.
                _gameState.ActiveBattle = null;
                Ui.PressEnter();
                return BattleResult.Fled;
            }
        }
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
        // Drain any final events the loop missed (BattleEnded, XP, captures, etc.).
        DrainAndNarrate();
        SnapshotPartyHp();

        // Captured actors: attach our per-actor display name and HP, and configure evolution
        // eligibility. The engine reactor already put them in PartyState and started PP tracking.
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

        // Evolution outcomes: apply the species swap.
        foreach (EvolutionTriggeredEvent evo in _battleEvents.OfType<EvolutionTriggeredEvent>())
        {
            if (!string.IsNullOrWhiteSpace(evo.EvolvedSpeciesId)
                && _speciesByActor.ContainsKey(evo.ActorId)
                && _speciesByActor[evo.ActorId] != evo.EvolvedSpeciesId)
            {
                ApplyEvolution(evo.ActorId, evo.EvolvedSpeciesId!);
            }
        }

        // Learnset growth on level-up.
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

        Ui.Heading($"Wild {wild.DisplayName} appeared!", "red");
        Ui.Line($"[red]Wild {wild.DisplayName} (Lv {wildLevel})[/]  {Ui.HpBar(wild.Hp, wild.MaxHp)} {wild.Hp}/{wild.MaxHp}");
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
        _currentHpByActor[actorId] = ComputeMaxHp(MonsterRoster.Get(speciesId), level);

        // Engine wiring: progression curve + initial level/XP, party slot, evolution eligibility.
        DispatchOrThrow(new ConfigureActorProgressionCommand(actorId, ContentCatalog.ExperienceCurveId, level: level, xp: XpFloorForLevel(level)));
        DispatchOrThrow(new AddPartyMemberCommand(actorId, active: active));

        // Evolution eligibility — species can list one evolution path.
        MonsterSpecies sp = MonsterRoster.Get(speciesId);
        if (sp.EvolvesIntoId is not null)
        {
            string evolutionId = EvolutionIdFor(speciesId);
            DispatchOrThrow(new ConfigureActorEvolutionsCommand(actorId, new[] { evolutionId }));
        }

        return actorId;
    }

    private void ApplyEvolution(string actorId, string newSpeciesId)
    {
        string oldSpeciesId = _speciesByActor[actorId];
        _speciesByActor[actorId] = newSpeciesId;
        MonsterSpecies newSp = MonsterRoster.Get(newSpeciesId);
        _displayNameByActor[actorId] = newSp.DisplayName;

        // Re-derive moves: keep any previously learned move that's also in the new species'
        // starting kit, then merge in the new species' starting moves.
        List<string> mergedMoves = new(_movesByActor[actorId]);
        foreach (string m in newSp.StartingMoves)
        {
            if (!mergedMoves.Contains(m)) mergedMoves.Add(m);
        }
        _movesByActor[actorId] = mergedMoves;

        // Scale current HP proportionally to the new MaxHp.
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
        Ui.Info($"Furthest zone reached: {_zoneHighWatermark} / {_overworld?.ZoneCount ?? 0}.");
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

    private (string speciesId, int level) RollWildEncounterForZone(int zone)
    {
        // Per-zone pools: easier species are common in early zones, rarer species appear
        // (and stack with the easy ones) as the player walks east. Wisplet only shows up
        // mid-overworld, matching the "ghost forest" feel of the later route.
        string[] pool = zone switch
        {
            <= 2 => new[]
            {
                SpeciesIds.Charpup,
                SpeciesIds.Bubbleling,
                SpeciesIds.Featherling
            },
            <= 5 => new[]
            {
                SpeciesIds.Charpup,
                SpeciesIds.Bubbleling,
                SpeciesIds.Featherling,
                SpeciesIds.Sparkmite,
                SpeciesIds.Mudling,
                SpeciesIds.Pebblet
            },
            _ => new[]
            {
                SpeciesIds.Bubbleling,
                SpeciesIds.Featherling,
                SpeciesIds.Sparkmite,
                SpeciesIds.Mudling,
                SpeciesIds.Pebblet,
                SpeciesIds.Wisplet,
                SpeciesIds.Charpup
            }
        };

        string speciesId = pool[_rng.NextInt(pool.Length)];

        // Level scales with zone — zone 1 spawns level 3-5, zone 10 spawns level 12-15.
        int minLevel = Math.Max(3, 2 + zone);
        int maxLevel = minLevel + 2;
        int level = minLevel + _rng.NextInt(maxLevel - minLevel + 1);
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

public enum GameOutcome
{
    Quit,
    Victory,
    Defeat
}
