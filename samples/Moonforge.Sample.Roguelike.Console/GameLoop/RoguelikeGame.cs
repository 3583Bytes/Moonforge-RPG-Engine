using System.Collections.Generic;
using Moonforge.Sample.Roguelike.Rendering;
using Moonforge.Sample.Roguelike.Session;

namespace Moonforge.Sample.Roguelike.GameLoop;

/// <summary>
/// Console host for the roguelike sample. Owns a <see cref="RoguelikeSession"/> (the
/// headless game) and implements <see cref="IRoguelikeHost"/> by delegating every
/// render call to <see cref="ConsoleRenderer"/>. Game state, scene flow, and input
/// handling all live inside the session itself; this class is only the I/O surface.
/// </summary>
internal sealed class RoguelikeGame : IRoguelikeHost
{
    private readonly RoguelikeSession _session;

    public RoguelikeGame()
    {
        _session = new RoguelikeSession(this);
    }

    public void Run()
    {
        _session.Run();
    }

    public void RenderMap(MapRenderModel model)
    {
        ConsoleRenderer.RenderMap(model);
    }

    public void RenderBattle(BattleRenderModel model)
    {
        ConsoleRenderer.RenderBattle(model);
    }

    public void RenderMainMenu(string subtitle, bool canContinue)
    {
        ConsoleRenderer.RenderMainMenu(subtitle, canContinue);
    }

    public void RenderClassSelection(IReadOnlyList<ClassSelectionOption> options, string controls)
    {
        ConsoleRenderer.RenderClassSelection(options, controls);
    }

    public void RenderBattleSummary(BattleSummaryRenderModel model)
    {
        ConsoleRenderer.RenderBattleSummary(model);
    }

    public void RenderDialogue(DialogueRenderModel model)
    {
        ConsoleRenderer.RenderDialogue(model);
    }

    public void RenderContractNotice(string title, string body, string controls)
    {
        ConsoleRenderer.RenderContractNotice(title, body, controls);
    }

    public void RenderContractJournal(string title, IReadOnlyList<string> lines, string controls)
    {
        ConsoleRenderer.RenderContractJournal(title, lines, controls);
    }

    public void RenderGearInventory(GearInventoryRenderModel model)
    {
        ConsoleRenderer.RenderGearInventory(model);
    }
}
