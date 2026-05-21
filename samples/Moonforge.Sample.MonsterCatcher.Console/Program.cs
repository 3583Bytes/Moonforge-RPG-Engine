using Moonforge.Sample.MonsterCatcher.GameLoop;

ulong seed = 12345;
if (args.Length > 0 && ulong.TryParse(args[0], out ulong parsedSeed))
{
    seed = parsedSeed;
}

MonsterCatcherGame game = new(seed);
GameOutcome outcome = game.Run();

System.Environment.Exit(outcome switch
{
    GameOutcome.Victory => 0,
    GameOutcome.Defeat => 1,
    _ => 2
});
