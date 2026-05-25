using Moonforge.Sample.Roguelike.Rendering;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Moonforge.Sample.Roguelike
{
    public sealed class SceneContext
    {
        public SceneContext(
            Camera camera,
            Tilemap tilemap,
            UnitySpriteCatalog sprites,
            TMP_Text hudHeader,
            TMP_Text hudBody,
            TMP_Text hudFooter,
            TMP_Text messageText)
        {
            Camera = camera;
            Tilemap = tilemap;
            Sprites = sprites;
            HudHeader = hudHeader;
            HudBody = hudBody;
            HudFooter = hudFooter;
            MessageText = messageText;
        }

        public Camera Camera { get; }

        public Tilemap Tilemap { get; }

        public UnitySpriteCatalog Sprites { get; }

        public TMP_Text HudHeader { get; }

        public TMP_Text HudBody { get; }

        public TMP_Text HudFooter { get; }

        public TMP_Text MessageText { get; }
    }
}
