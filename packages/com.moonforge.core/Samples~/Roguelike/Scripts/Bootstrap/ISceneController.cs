using Moonforge.Sample.Roguelike.Input;

namespace Moonforge.Sample.Roguelike
{
    public interface ISceneController
    {
        SceneId Id { get; }

        void Enter(SceneContext context);

        SceneTransition Tick(PlayerAction action, SceneContext context);

        void Exit(SceneContext context);
    }

    public readonly struct SceneTransition
    {
        public static readonly SceneTransition Stay = new SceneTransition(stay: true, next: SceneId.MainMenu);

        private SceneTransition(bool stay, SceneId next)
        {
            StayInScene = stay;
            NextScene = next;
        }

        public bool StayInScene { get; }

        public SceneId NextScene { get; }

        public static SceneTransition To(SceneId next)
        {
            return new SceneTransition(stay: false, next: next);
        }
    }
}
