#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Moonforge.Sample.Roguelike.EditorScripts
{
    /// <summary>
    /// Configures the texture import settings for the 16x16 PNG sprites bundled with
    /// this sample's <c>Art/Resources/Sprites/</c> folder. Unity's defaults import a
    /// PNG as <see cref="TextureImporterType.Default"/> at <c>spritePixelsPerUnit = 100</c>,
    /// which produces invisible-tiny placeholders when <see cref="Resources.Load"/> is
    /// called for a Sprite. This postprocessor fixes the defaults so first-time importers
    /// see the real art at the right scale.
    ///
    /// Only applies when the importer is still in its default state (textureType = Default).
    /// Once you've customized the import settings, this script becomes a no-op for those
    /// files — so re-importing the sample via the Package Manager will not stomp your
    /// changes. To restore the bundled defaults for a single sprite, set its Texture Type
    /// back to <c>Default</c> in the Inspector, then re-import that PNG.
    /// </summary>
    public sealed class RoguelikeSpriteImporter : AssetPostprocessor
    {
        private const string SpritesFolder = "/Roguelike/Art/Resources/Sprites/";
        private const float PixelsPerUnit = 16f;

        private void OnPreprocessTexture()
        {
            if (assetPath.IndexOf(SpritesFolder, System.StringComparison.Ordinal) < 0)
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;

            // Preserve any user customization. If the texture type has already been changed
            // away from Default (i.e. the user has saved their own settings), do nothing.
            if (importer.textureType != TextureImporterType.Default)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
        }
    }
}
#endif
