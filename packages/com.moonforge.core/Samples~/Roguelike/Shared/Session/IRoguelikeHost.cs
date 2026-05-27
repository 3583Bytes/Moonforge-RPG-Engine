using System.Collections.Generic;
using Moonforge.Sample.Roguelike.Rendering;

namespace Moonforge.Sample.Roguelike.Session
{

    /// <summary>
    /// Output surface for <see cref="RoguelikeSession"/>: the session decides <em>what</em>
    /// to draw on each scene tick and pushes it through this host; the host decides
    /// <em>how</em> to draw it. The console sample implements this with Spectre.Console; the
    /// Unity sample implements it with a Tilemap + Canvas overlay.
    /// </summary>
    public interface IRoguelikeHost
    {
        /// <summary>Render the Town or Dungeon tile-map scene.</summary>
        void RenderMap(MapRenderModel model);

        /// <summary>Render the active battle scene.</summary>
        void RenderBattle(BattleRenderModel model);

        /// <summary>Render the main menu (subtitle + whether a saved run can be resumed).</summary>
        void RenderMainMenu(string subtitle, bool canContinue);

        /// <summary>Render the class-selection screen.</summary>
        void RenderClassSelection(IReadOnlyList<ClassSelectionOption> options, string controls);

        /// <summary>Render the post-battle summary screen (currency/inventory deltas, log, boss reward).</summary>
        void RenderBattleSummary(BattleSummaryRenderModel model);

        /// <summary>Render an NPC dialogue node + available choices.</summary>
        void RenderDialogue(DialogueRenderModel model);

        /// <summary>Render a modal contract-status notice (start, complete, turn-in).</summary>
        void RenderContractNotice(string title, string body, string controls);

        /// <summary>
        /// Render a list-of-text overlay used by the Contract Journal, Gear Loadout,
        /// Shrine of Echoes, and Boss Reward screens.
        /// </summary>
        void RenderContractJournal(string title, IReadOnlyList<string> lines, string controls);
    }
}
