using System.Collections.Generic;
using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Encounters;
using Moonforge.Core.Encounters.Queries;
using Moonforge.Core.Runtime.Random;

namespace Moonforge.Core.Tests;

public sealed class EncounterTableTests
{
    private const string Goblin = "enemy.goblin";
    private const string Wolf = "enemy.wolf";
    private const string Shaman = "enemy.shaman";

    [Fact]
    public void PickOne_Returns_Exactly_One_Spawn_With_Count_In_Range()
    {
        EncounterTableDefinition table = new(
            "enc.test",
            EncounterRollMode.PickOne,
            [
                new EncounterEntryDefinition(Goblin, weight: 1, minCount: 2, maxCount: 4)
            ]);

        EncounterRollResult r = EncounterResolver.Roll(new Pcg32RandomSource(1, 1), table);

        Assert.Single(r.Spawns);
        Assert.Equal(Goblin, r.Spawns[0].ActorId);
        Assert.InRange(r.Spawns[0].Count, 2, 4);
    }

    [Fact]
    public void PickOne_Weighted_Distribution_Matches_Within_Tolerance()
    {
        EncounterTableDefinition table = new(
            "enc.weighted",
            EncounterRollMode.PickOne,
            [
                new EncounterEntryDefinition(Goblin, weight: 80),
                new EncounterEntryDefinition(Wolf,   weight: 20)
            ]);

        Pcg32RandomSource rng = new(99, 1);
        int goblins = 0, wolves = 0;
        for (int i = 0; i < 10_000; i++)
        {
            EncounterRollResult r = EncounterResolver.Roll(rng, table);
            if (r.Spawns[0].ActorId == Goblin) goblins++;
            else wolves++;
        }

        Assert.InRange(goblins, 7700, 8300);
        Assert.InRange(wolves,  1700, 2300);
    }

    [Fact]
    public void Same_Seed_Produces_Identical_Spawn_Sequence()
    {
        EncounterTableDefinition table = new(
            "enc.det",
            EncounterRollMode.PickOne,
            [
                new EncounterEntryDefinition(Goblin, weight: 1, minCount: 1, maxCount: 5),
                new EncounterEntryDefinition(Wolf,   weight: 1, minCount: 1, maxCount: 5),
                new EncounterEntryDefinition(Shaman, weight: 1, minCount: 1, maxCount: 5)
            ]);

        List<string> a = RollSequence(table, seed: 2024, count: 25);
        List<string> b = RollSequence(table, seed: 2024, count: 25);

        Assert.Equal(a, b);
    }

    [Fact]
    public void RollEach_Includes_Every_Entry_When_Chance_Is_Hundred()
    {
        EncounterTableDefinition table = new(
            "enc.each",
            EncounterRollMode.RollEach,
            [
                new EncounterEntryDefinition(Goblin, chancePercent: 100, minCount: 2, maxCount: 2),
                new EncounterEntryDefinition(Wolf,   chancePercent: 100, minCount: 1, maxCount: 1)
            ]);

        EncounterRollResult r = EncounterResolver.Roll(new Pcg32RandomSource(1, 1), table);

        Assert.Equal(2, r.Spawns.Count);
        Assert.Contains(r.Spawns, s => s.ActorId == Goblin && s.Count == 2);
        Assert.Contains(r.Spawns, s => s.ActorId == Wolf && s.Count == 1);
    }

    [Fact]
    public void RollEach_Zero_Chance_Entries_Are_Skipped()
    {
        EncounterTableDefinition table = new(
            "enc.zero",
            EncounterRollMode.RollEach,
            [
                new EncounterEntryDefinition(Goblin, chancePercent: 0),
                new EncounterEntryDefinition(Wolf,   chancePercent: 100)
            ]);

        EncounterRollResult r = EncounterResolver.Roll(new Pcg32RandomSource(1, 1), table);

        Assert.Single(r.Spawns);
        Assert.Equal(Wolf, r.Spawns[0].ActorId);
    }

    [Fact]
    public void Empty_Or_Zero_Weight_Table_Returns_Empty_Result()
    {
        EncounterTableDefinition empty = new("enc.empty", EncounterRollMode.PickOne, []);
        EncounterTableDefinition zeroes = new(
            "enc.zeroes",
            EncounterRollMode.PickOne,
            [
                new EncounterEntryDefinition(Goblin, weight: 0)
            ]);

        Assert.True(EncounterResolver.Roll(new Pcg32RandomSource(1, 1), empty).IsEmpty);
        Assert.True(EncounterResolver.Roll(new Pcg32RandomSource(1, 1), zeroes).IsEmpty);
    }

    [Fact]
    public void Query_Returns_Empty_For_Unknown_Table_Id()
    {
        InMemoryGameDefinitionCatalog defs = new();
        RollEncounterTableQueryHandler handler = new(defs, new Pcg32RandomSource(1, 1));

        EncounterRollResult r = handler.Query(new GameState(), new RollEncounterTableQuery("enc.missing"));

        Assert.True(r.IsEmpty);
    }

    [Fact]
    public void Query_Returns_Resolved_Spawns_When_Table_Registered()
    {
        InMemoryGameDefinitionCatalog defs = new();
        defs.AddEncounterTable(new EncounterTableDefinition(
            "enc.registered",
            EncounterRollMode.PickOne,
            [
                new EncounterEntryDefinition(Goblin, weight: 1, minCount: 3, maxCount: 3)
            ]));
        RollEncounterTableQueryHandler handler = new(defs, new Pcg32RandomSource(1, 1));

        EncounterRollResult r = handler.Query(new GameState(), new RollEncounterTableQuery("enc.registered"));

        Assert.Single(r.Spawns);
        Assert.Equal(Goblin, r.Spawns[0].ActorId);
        Assert.Equal(3, r.Spawns[0].Count);
    }

    private static List<string> RollSequence(EncounterTableDefinition table, ulong seed, int count)
    {
        Pcg32RandomSource rng = new(seed, 1);
        List<string> log = new();
        for (int i = 0; i < count; i++)
        {
            EncounterRollResult r = EncounterResolver.Roll(rng, table);
            foreach (EncounterSpawn s in r.Spawns) log.Add($"{s.ActorId}x{s.Count}");
        }

        return log;
    }
}
