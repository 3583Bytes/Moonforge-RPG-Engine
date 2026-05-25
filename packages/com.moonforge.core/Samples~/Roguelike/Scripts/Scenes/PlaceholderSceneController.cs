using Moonforge.Sample.Roguelike.Input;

namespace Moonforge.Sample.Roguelike.Scenes
{
    /// <summary>
    /// Stand-in scene used until the real port lands. Each SceneId starts the sample wired to
    /// this controller; replace the binding in <c>RoguelikeBootstrap</c> as each phase ships.
    /// </summary>
    public sealed class PlaceholderSceneController : ISceneController
    {
        public PlaceholderSceneController(SceneId id, string phaseLabel)
        {
            Id = id;
            _phaseLabel = phaseLabel;
        }

        private readonly string _phaseLabel;

        public SceneId Id { get; }

        public void Enter(SceneContext context)
        {
            if (context.Tilemap != null)
            {
                context.Tilemap.ClearAllTiles();
            }

            if (context.HudHeader != null)
            {
                context.HudHeader.text = "Moonforge Roguelike — " + Id;
            }

            if (context.HudBody != null)
            {
                context.HudBody.text =
                    Id + " scene is not yet implemented.\n" +
                    "This sample is being ported scene-by-scene from\n" +
                    "samples/Moonforge.Sample.Console.\n\n" +
                    "Expected phase: " + _phaseLabel + ".";
            }

            if (context.HudFooter != null)
            {
                context.HudFooter.text = "Press Esc to quit play mode.";
            }

            if (context.MessageText != null)
            {
                context.MessageText.text = string.Empty;
            }
        }

        public SceneTransition Tick(PlayerAction action, SceneContext context)
        {
            if (action == PlayerAction.Cancel || action == PlayerAction.Quit)
            {
                return SceneTransition.To(SceneId.Exit);
            }
            return SceneTransition.Stay;
        }

        public void Exit(SceneContext context)
        {
        }
    }
}
