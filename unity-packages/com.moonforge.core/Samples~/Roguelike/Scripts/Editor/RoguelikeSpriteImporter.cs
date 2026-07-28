#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Moonforge.Sample.Roguelike.EditorScripts
{
    /// <summary>
    /// Configures texture import settings for the art bundled with this sample so first-time
    /// importers see it at the right scale. Handles two very different kinds of art:
    ///
    /// <list type="bullet">
    /// <item><b>0x72 DungeonTileset II</b> under <c>Art/Resources/DungeonTilesetII/</c> (CC0) —
    /// crisp 16x16 pixel art. Imported at <c>pixelsPerUnit = 16</c> (one tile = one world unit),
    /// Point filter, no compression, Clamp wrap. Character frames (…_idle_anim / _run_anim /
    /// _hit_anim…) are taller than the grid and get a bottom-centre pivot so they stand on the
    /// floor cell; everything else stays centred.</item>
    /// <item><b>Town ground textures</b> <c>Grass.tga</c> / <c>Ground.tga</c> at the
    /// <c>Art/Resources/</c> root — large seamless tiles. Imported as FullRect sprites with
    /// Repeat wrap + Bilinear + mipmaps so the bootstrap can paint them as tiled ground planes.</item>
    /// </list>
    ///
    /// Only applies when the importer is still in its default state (textureType = Default), so
    /// customizing a texture's settings makes this a no-op for that file and re-importing the
    /// sample won't stomp your changes.
    /// </summary>
    public sealed class RoguelikeSpriteImporter : AssetPostprocessor
    {
        private const string TilesetFolder = "/Roguelike/Art/Resources/DungeonTilesetII/";
        private const string ResourcesFolder = "/Roguelike/Art/Resources/";
        private const float PixelsPerUnit = 16f;
        // 1024px town textures / 256 = 4 world units (cells) per seamless copy.
        private const float TownGroundPixelsPerUnit = 256f;

        private void OnPreprocessTexture()
        {
            bool inTileset = assetPath.IndexOf(TilesetFolder, System.StringComparison.Ordinal) >= 0;
            bool isTownGround = IsTownGroundTexture(assetPath);
            bool isUiPanel = IsUiPanelTexture(assetPath);
            if (!inTileset && !isTownGround && !isUiPanel)
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;

            // Preserve any user customization.
            if (importer.textureType != TextureImporterType.Default)
            {
                return;
            }

            if (isUiPanel)
            {
                ConfigureUiPanel(importer);
            }
            else if (isTownGround)
            {
                ConfigureTownGround(importer);
            }
            else
            {
                ConfigureTilesetFrame(importer);
            }
        }

        // The wood UI panel is a 9-slice frame: bolt corners stay fixed, edges/centre stretch.
        // Border insets are set so the corners don't distort when the panel is stretched.
        private static void ConfigureUiPanel(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f; // canvas reference PPU → border renders near native size
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            // 9-slice border (left, bottom, right, top) — the wood frame thickness on the 71x72
            // source. Tune here if the corners look clipped or the frame too thick.
            settings.spriteBorder = new Vector4(14f, 14f, 14f, 14f);
            settings.spritePixelsPerUnit = 100f;
            settings.filterMode = FilterMode.Point;
            importer.SetTextureSettings(settings);
        }

        private static void ConfigureTilesetFrame(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;

            // Character animation frames are taller than the 16x16 grid — pivot them at the
            // bottom centre so their feet sit on the floor cell. Tiles/props stay centred.
            SpriteAlignment alignment = IsCharacterFrame(importer.assetPath)
                ? SpriteAlignment.BottomCenter
                : SpriteAlignment.Center;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)alignment;
            settings.spritePixelsPerUnit = PixelsPerUnit;
            settings.filterMode = FilterMode.Point;
            // FullRect so a tile can be used as a tiled UI Image (e.g. the battle backdrop);
            // harmless for the world SpriteRenderers that draw these frames.
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        private static void ConfigureTownGround(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TownGroundPixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.alphaIsTransparency = true;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            // FullRect mesh + Repeat wrap are required for SpriteRenderer tiled/continuous draw.
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePixelsPerUnit = TownGroundPixelsPerUnit;
            settings.filterMode = FilterMode.Bilinear;
            settings.wrapMode = TextureWrapMode.Repeat;
            settings.mipmapEnabled = true;
            importer.SetTextureSettings(settings);
        }

        private static bool IsTownGroundTexture(string path)
        {
            if (path.IndexOf(ResourcesFolder, System.StringComparison.Ordinal) < 0)
            {
                return false;
            }
            return path.EndsWith("/Grass.tga", System.StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/Ground.tga", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUiPanelTexture(string path)
        {
            return path.IndexOf(ResourcesFolder, System.StringComparison.Ordinal) >= 0
                && path.EndsWith("/UIBack.png", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True for the 0x72 walking/standing character frames — the only sprites that are
        /// taller than the 16x16 grid and therefore want a bottom-centre pivot. Keyed on the
        /// animation suffixes so single-cell prop animations (coin, bomb, spikes, …) stay
        /// centred.
        /// </summary>
        private static bool IsCharacterFrame(string path)
        {
            return path.IndexOf("_idle_anim", System.StringComparison.Ordinal) >= 0
                || path.IndexOf("_run_anim", System.StringComparison.Ordinal) >= 0
                || path.IndexOf("_hit_anim", System.StringComparison.Ordinal) >= 0;
        }
    }
}
#endif
