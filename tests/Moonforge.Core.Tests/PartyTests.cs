using System.Collections.Generic;
using System.Linq;
using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Party;
using Moonforge.Core.Party.Commands;
using Moonforge.Core.Party.Events;
using Moonforge.Core.Party.Queries;
using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class PartyTests
{
    [Fact]
    public void Default_Party_Behaves_Like_Single_Hero()
    {
        // Engine ships with MaxActive=1, MaxRoster=1 so games that ignore the Party module
        // keep their pre-Party single-hero semantics.
        PartyState party = new();
        Assert.Equal(1, party.MaxActive);
        Assert.Equal(1, party.MaxRoster);
        Assert.Empty(party.Members);
    }

    [Fact]
    public void Configure_Sets_Caps_And_Emits_Event()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();

        Assert.True(dispatcher.Dispatch(gameState, new ConfigurePartyCommand(maxActive: 3, maxRoster: 6), CreateContext(sink, defs)).IsSuccess);

        Assert.Equal(3, gameState.PartyState.MaxActive);
        Assert.Equal(6, gameState.PartyState.MaxRoster);
        Assert.Single(sink.Events.OfType<PartyConfiguredEvent>(), e => e.MaxActive == 3 && e.MaxRoster == 6);
    }

    [Fact]
    public void Configure_Rejects_Invalid_Caps()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();

        DomainResult zero = dispatcher.Dispatch(gameState, new ConfigurePartyCommand(0, 6), CreateContext(sink, defs));
        DomainResult inverted = dispatcher.Dispatch(gameState, new ConfigurePartyCommand(maxActive: 5, maxRoster: 3), CreateContext(sink, defs));

        Assert.False(zero.IsSuccess);
        Assert.False(inverted.IsSuccess);
        Assert.Equal(DomainErrorCode.ValidationFailed, inverted.Error!.Code);
    }

    [Fact]
    public void Add_Member_Fills_Active_Then_Reserves()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        ConfigureForMonsterTeam(gameState, dispatcher, defs, sink);

        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.starter", active: true), CreateContext(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.second", active: false), CreateContext(sink, defs)).IsSuccess);

        Assert.Equal(2, gameState.PartyState.Members.Count);
        Assert.Equal(1, gameState.PartyState.ActiveCount);
        Assert.Equal(2, sink.Events.OfType<PartyMemberAddedEvent>().Count());
    }

    [Fact]
    public void Add_Duplicate_Member_Fails()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        ConfigureForMonsterTeam(gameState, dispatcher, defs, sink);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.starter"), CreateContext(sink, defs)).IsSuccess);

        DomainResult dup = dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.starter"), CreateContext(sink, defs));

        Assert.False(dup.IsSuccess);
        Assert.Single(gameState.PartyState.Members);
    }

    [Fact]
    public void Add_When_Roster_Full_Fails()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigurePartyCommand(maxActive: 1, maxRoster: 2), CreateContext(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.a", active: true), CreateContext(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.b", active: false), CreateContext(sink, defs)).IsSuccess);

        DomainResult third = dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.c", active: false), CreateContext(sink, defs));

        Assert.False(third.IsSuccess);
        Assert.Equal(2, gameState.PartyState.Members.Count);
    }

    [Fact]
    public void Add_When_Active_Slots_Full_Fails_But_Reserve_Still_Allowed()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigurePartyCommand(maxActive: 1, maxRoster: 6), CreateContext(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.a", active: true), CreateContext(sink, defs)).IsSuccess);

        DomainResult activeFull = dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.b", active: true), CreateContext(sink, defs));
        DomainResult reserve = dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.b", active: false), CreateContext(sink, defs));

        Assert.False(activeFull.IsSuccess);
        Assert.True(reserve.IsSuccess);
        Assert.Equal(1, gameState.PartyState.ActiveCount);
        Assert.Equal(2, gameState.PartyState.Members.Count);
    }

    [Fact]
    public void Remove_Member_Drops_From_Roster()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        ConfigureForMonsterTeam(gameState, dispatcher, defs, sink);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.x"), CreateContext(sink, defs)).IsSuccess);

        Assert.True(dispatcher.Dispatch(gameState, new RemovePartyMemberCommand("mon.x"), CreateContext(sink, defs)).IsSuccess);

        Assert.Empty(gameState.PartyState.Members);
        Assert.Single(sink.Events.OfType<PartyMemberRemovedEvent>(), e => e.ActorId == "mon.x");
    }

    [Fact]
    public void Remove_Missing_Member_Fails()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        ConfigureForMonsterTeam(gameState, dispatcher, defs, sink);

        DomainResult result = dispatcher.Dispatch(gameState, new RemovePartyMemberCommand("mon.ghost"), CreateContext(sink, defs));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.NotFound, result.Error!.Code);
    }

    [Fact]
    public void Toggle_Active_Respects_Cap()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigurePartyCommand(maxActive: 2, maxRoster: 6), CreateContext(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.a", active: true), CreateContext(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.b", active: true), CreateContext(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.c", active: false), CreateContext(sink, defs)).IsSuccess);

        // Promote-while-full fails until we bench someone first.
        DomainResult denied = dispatcher.Dispatch(gameState, new SetPartyMemberActiveCommand("mon.c", true), CreateContext(sink, defs));
        Assert.False(denied.IsSuccess);

        Assert.True(dispatcher.Dispatch(gameState, new SetPartyMemberActiveCommand("mon.b", false), CreateContext(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new SetPartyMemberActiveCommand("mon.c", true), CreateContext(sink, defs)).IsSuccess);

        Assert.Equal(2, gameState.PartyState.ActiveCount);
        Assert.True(gameState.PartyState.TryGet("mon.c", out PartyMember c) && c.IsActive);
        Assert.True(gameState.PartyState.TryGet("mon.b", out PartyMember b) && !b.IsActive);
    }

    [Fact]
    public void Toggle_Same_State_Is_Noop_With_No_Event()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        ConfigureForMonsterTeam(gameState, dispatcher, defs, sink);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.a", active: true), CreateContext(sink, defs)).IsSuccess);
        int eventsBefore = sink.Events.OfType<PartyMemberActiveChangedEvent>().Count();

        Assert.True(dispatcher.Dispatch(gameState, new SetPartyMemberActiveCommand("mon.a", true), CreateContext(sink, defs)).IsSuccess);

        Assert.Equal(eventsBefore, sink.Events.OfType<PartyMemberActiveChangedEvent>().Count());
    }

    [Fact]
    public void GetPartyMembersQuery_Filters_To_Active()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigurePartyCommand(maxActive: 3, maxRoster: 6), CreateContext(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.a", active: true), CreateContext(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.b", active: true), CreateContext(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.c", active: false), CreateContext(sink, defs)).IsSuccess);

        GetPartyMembersQueryHandler handler = new();
        IReadOnlyList<PartyMember> all = handler.Query(gameState, new GetPartyMembersQuery(activeOnly: false));
        IReadOnlyList<PartyMember> active = handler.Query(gameState, new GetPartyMembersQuery(activeOnly: true));

        Assert.Equal(3, all.Count);
        Assert.Equal(2, active.Count);
        Assert.All(active, m => Assert.True(m.IsActive));
    }

    [Fact]
    public void GameState_Clone_Preserves_Party()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        ConfigureForMonsterTeam(gameState, dispatcher, defs, sink);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.a", active: true), CreateContext(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.b", active: false), CreateContext(sink, defs)).IsSuccess);

        GameState clone = gameState.Clone();
        // Mutate original — clone must be unaffected.
        Assert.True(gameState.PartyState.TryRemove("mon.a"));

        Assert.Equal(2, clone.PartyState.Members.Count);
        Assert.True(clone.PartyState.Contains("mon.a"));
    }

    [Fact]
    public void Save_RoundTrip_Preserves_Party()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        ConfigureForMonsterTeam(gameState, dispatcher, defs, sink);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.a", active: true), CreateContext(sink, defs)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand("mon.b", active: false), CreateContext(sink, defs)).IsSuccess);

        JsonGameStateSerializer serializer = new();
        string json = serializer.Serialize(GameStateSnapshotMapper.Capture(gameState));
        GameStateSnapshot decoded = serializer.Deserialize(json);

        GameState rebuilt = new();
        GameStateSnapshotMapper.Apply(rebuilt, decoded);

        Assert.Equal(2, rebuilt.PartyState.MaxActive);
        Assert.Equal(6, rebuilt.PartyState.MaxRoster);
        Assert.Equal(2, rebuilt.PartyState.Members.Count);
        Assert.True(rebuilt.PartyState.TryGet("mon.a", out PartyMember a) && a.IsActive);
        Assert.True(rebuilt.PartyState.TryGet("mon.b", out PartyMember b) && !b.IsActive);
    }

    [Fact]
    public void Loading_Save_Without_Party_Field_Yields_Empty_Default_Roster()
    {
        // A v3-era payload has no "party" field. With no migration registered, the engine
        // still deserializes cleanly because GameStateSnapshot.Party defaults to a fresh
        // PartyStateSnapshot (empty roster, MaxActive=1, MaxRoster=1).
        const string legacyPayload = "{\"schemaVersion\":4,\"contentVersion\":\"v1\",\"simulationMinutes\":0}";

        JsonGameStateSerializer serializer = new();
        GameStateSnapshot decoded = serializer.Deserialize(legacyPayload);

        Assert.NotNull(decoded.Party);
        Assert.Empty(decoded.Party.Members);
        Assert.Equal(1, decoded.Party.MaxActive);
        Assert.Equal(1, decoded.Party.MaxRoster);
    }

    private static void ConfigureForMonsterTeam(GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink)
    {
        Assert.True(dispatcher.Dispatch(gameState, new ConfigurePartyCommand(maxActive: 2, maxRoster: 6), CreateContext(sink, defs)).IsSuccess);
    }

    private static (GameState, CommandDispatcher, InMemoryGameDefinitionCatalog, InMemoryDomainEventSink) CreateWorld()
    {
        GameState gameState = new();
        InMemoryGameDefinitionCatalog defs = new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition("currency.gold", 999));
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();
        return (gameState, dispatcher, defs, sink);
    }

    private static CommandContext CreateContext(InMemoryDomainEventSink sink, IGameDefinitionCatalog defs)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 1, sequence: 1),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            defs);
    }
}
