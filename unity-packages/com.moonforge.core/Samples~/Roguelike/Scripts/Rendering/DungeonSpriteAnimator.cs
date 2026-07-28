using UnityEngine;

namespace Moonforge.Sample.Roguelike.Rendering
{
    /// <summary>
    /// Drives a looping frame animation on a <see cref="SpriteRenderer"/> from two clips:
    /// an <c>idle</c> loop and an optional <c>run</c> loop (the 0x72 DungeonTileset II ships
    /// a 4-frame idle + 4-frame run for every character). The bootstrap attaches one of these
    /// per actor GameObject and pokes <see cref="Running"/> / <see cref="FlipX"/> each frame —
    /// <c>Running</c> from whether the actor is tweening between cells, <c>FlipX</c> from its
    /// facing / movement direction (characters only have a right-facing sheet, so left is a
    /// horizontal flip).
    ///
    /// Playback is purely cosmetic, so it runs off <see cref="Time.deltaTime"/> and is
    /// independent of the deterministic simulation clock.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonSpriteAnimator : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Sprite[] _idle;
        private Sprite[] _run;
        private float _idleFps = 6f;
        private float _runFps = 10f;

        private float _timer;
        private int _frame;
        private bool _wasRunning;

        /// <summary>Whether the actor is moving (plays the run loop when a run clip exists).</summary>
        public bool Running { get; set; }

        /// <summary>Mirror the sprite horizontally (character sheets face right by default).</summary>
        public bool FlipX
        {
            get => _renderer != null && _renderer.flipX;
            set { if (_renderer != null) _renderer.flipX = value; }
        }

        /// <summary>
        /// Wire up the renderer and clips. <paramref name="run"/> may be null — the animator
        /// then always plays <paramref name="idle"/>. Safe to call again to re-skin the actor
        /// (e.g. if its visual kind changes); the frame cursor resets to the start.
        /// </summary>
        public void Configure(SpriteRenderer renderer, Sprite[] idle, Sprite[] run, float idleFps = 6f, float runFps = 10f)
        {
            _renderer = renderer;
            _idle = idle;
            _run = (run != null && run.Length > 0) ? run : null;
            _idleFps = idleFps > 0f ? idleFps : 6f;
            _runFps = runFps > 0f ? runFps : 10f;
            _timer = 0f;
            _frame = 0;
            _wasRunning = false;
            ApplyFrame();
        }

        private void Update()
        {
            Sprite[] clip = ActiveClip();
            if (clip == null || clip.Length == 0 || _renderer == null)
            {
                return;
            }

            // Reset the cursor when switching between idle/run so a clip always starts at
            // frame 0 rather than jumping to a stale index that may be out of range.
            if (Running != _wasRunning)
            {
                _wasRunning = Running;
                _timer = 0f;
                _frame = 0;
            }

            float fps = (Running && _run != null) ? _runFps : _idleFps;
            _timer += Time.deltaTime * fps;
            if (_timer >= 1f)
            {
                _timer -= Mathf.Floor(_timer);
                _frame = (_frame + 1) % clip.Length;
            }

            ApplyFrame();
        }

        private Sprite[] ActiveClip() => (Running && _run != null) ? _run : _idle;

        private void ApplyFrame()
        {
            Sprite[] clip = ActiveClip();
            if (_renderer == null || clip == null || clip.Length == 0)
            {
                return;
            }
            if (_frame >= clip.Length)
            {
                _frame = 0;
            }
            _renderer.sprite = clip[_frame];
        }
    }
}
