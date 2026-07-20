using System.Collections.Generic;
using Moonforge.Sample.Roguelike;
using Moonforge.Sample.Roguelike.Input;
using Moonforge.Sample.Roguelike.Rendering;
using Moonforge.Sample.Roguelike.Session;

namespace Moonforge.Sample.Roguelike.Console.Tests;

public sealed class RoguelikeSessionFacingTests
{
    [Fact]
    public void HeroFacing_Defaults_To_Down_And_Tracks_Movement_Input()
    {
        string savePath = Path.Combine(Path.GetTempPath(), $"rpgengine-facing-{Guid.NewGuid():N}.json");
        try
        {
            RoguelikeSession session = new(new NullHost(), savePath);
            Assert.Equal(FacingDirection.Down, session.HeroFacing);

            session.Tick(PlayerAction.NewRun);
            Assert.Equal(SceneId.ClassSelect, session.CurrentScene);
            session.Tick(PlayerAction.Digit1);
            Assert.Equal(SceneId.Town, session.CurrentScene);
            Assert.Equal(FacingDirection.Down, session.HeroFacing);

            session.Tick(PlayerAction.MoveEast);
            Assert.Equal(FacingDirection.Right, session.HeroFacing);
            session.Tick(PlayerAction.MoveNorth);
            Assert.Equal(FacingDirection.Up, session.HeroFacing);
            session.Tick(PlayerAction.MoveWest);
            Assert.Equal(FacingDirection.Left, session.HeroFacing);
            session.Tick(PlayerAction.MoveSouth);
            Assert.Equal(FacingDirection.Down, session.HeroFacing);
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    [Fact]
    public void HeroFacing_Updates_Even_When_The_Step_Is_Blocked()
    {
        string savePath = Path.Combine(Path.GetTempPath(), $"rpgengine-facing-{Guid.NewGuid():N}.json");
        try
        {
            RoguelikeSession session = new(new NullHost(), savePath);
            session.Tick(PlayerAction.NewRun);
            session.Tick(PlayerAction.Digit1);

            // Walk west until the hero is pinned against the town's edge wall, then keep
            // pressing west: position stops changing but the facing must stay/turn Left.
            for (int i = 0; i < 64; i++)
            {
                session.Tick(PlayerAction.MoveWest);
            }

            Assert.Equal(FacingDirection.Left, session.HeroFacing);

            // Bump again from a known-blocked spot — the turn alone must register.
            session.Tick(PlayerAction.MoveNorth);
            Assert.Equal(FacingDirection.Up, session.HeroFacing);
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    private sealed class NullHost : IRoguelikeHost
    {
        public void RenderMap(MapRenderModel model)
        {
        }

        public void RenderBattle(BattleRenderModel model)
        {
        }

        public void RenderMainMenu(string subtitle, bool canContinue)
        {
        }

        public void RenderClassSelection(IReadOnlyList<ClassSelectionOption> options, string controls)
        {
        }

        public void RenderBattleSummary(BattleSummaryRenderModel model)
        {
        }

        public void RenderDialogue(DialogueRenderModel model)
        {
        }

        public void RenderContractNotice(string title, string body, string controls)
        {
        }

        public void RenderContractJournal(string title, IReadOnlyList<string> lines, string controls)
        {
        }

        public void RenderGearInventory(GearInventoryRenderModel model)
        {
        }
    }
}
