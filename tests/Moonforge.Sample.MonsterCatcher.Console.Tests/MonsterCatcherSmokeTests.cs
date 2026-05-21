using System;
using System.IO;
using Moonforge.Sample.MonsterCatcher.GameLoop;

namespace Moonforge.Sample.MonsterCatcher.Console.Tests;

/// <summary>
/// Smoke test that drives the headless game to completion. With <c>Console.IsInputRedirected</c>
/// true, every menu auto-picks option 0 and every "press Enter" returns immediately, so the
/// game converges either via repeated attacks or via the headless-flee safety in the player
/// turn handler. Either outcome (Victory / Defeat) confirms the loop terminates without
/// throwing and that all seven engine features wire together end-to-end.
/// </summary>
public sealed class MonsterCatcherSmokeTests
{
    [Fact]
    public void Game_Runs_To_Completion_Without_Crashing()
    {
        // Redirect stdin so ChooseOption + PressEnter take the auto-pick code paths.
        using StringReader stdin = new(string.Empty);
        TextReader original = System.Console.In;
        System.Console.SetIn(stdin);

        try
        {
            MonsterCatcherGame game = new(seed: 4242);
            GameOutcome outcome = game.Run();
            Assert.True(outcome == GameOutcome.Victory || outcome == GameOutcome.Defeat,
                $"Expected Victory or Defeat, got {outcome}.");
        }
        finally
        {
            System.Console.SetIn(original);
        }
    }
}
