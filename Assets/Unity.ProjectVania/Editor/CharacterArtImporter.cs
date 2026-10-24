using UnityEditor;
using UnityEngine;

namespace Unity.ProjectVania.EditorTools
{
    /// <summary>
    /// Imports the original's character strips (RECONSTRUCTION 4.8, 13) the way they are drawn: whole, unscaled (Leo's run
    /// is 257 wide and must not be rounded to a power of two), uncompressed, without mipmaps and point-filtered, so every
    /// source pixel lands on whole screen pixels. Settings are set here in code rather than by hand in the Inspector;
    /// <see cref="CharacterArt"/> cuts the frames at run time from animations.json.
    /// </summary>
    public sealed class CharacterArtImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Unity.ProjectVania/Resources/" + CharacterArt.ResourceFolder + "/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
        }
    }
}
