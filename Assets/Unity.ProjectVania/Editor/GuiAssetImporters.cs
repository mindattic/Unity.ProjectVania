using UnityEditor;
using UnityEngine;

namespace Unity.ProjectVania.EditorTools
{
    /// <summary>
    /// Imports the GUI's art (RECONSTRUCTION 9, 13) whole and uncompressed, without mipmaps. XNA's SpriteBatch sampled
    /// linearly, which matters only where the GUI stretches a picture: <c>menu1</c> (543 x 742) is drawn over the whole
    /// 800 x 600 screen, so it is bilinear-filtered as the original showed it; everything else is drawn at its own size and
    /// is point-filtered like the world's art. Set here in code rather than by hand in the Inspector.
    /// </summary>
    public sealed class GuiArtImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Unity.ProjectVania/Resources/" + MainMenuView.ResourceFolder + "/";

        /// <summary>The GUI pictures the original stretched to the screen.</summary>
        public static readonly string[] Stretched = { "menu1" };

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            importer.filterMode = System.Array.IndexOf(Stretched, name) >= 0 ? FilterMode.Bilinear : FilterMode.Point;
        }
    }

    /// <summary>
    /// Imports the cue table's waves (RECONSTRUCTION 11) as the original's uncompressed 16-bit PCM, decompressed and
    /// preloaded, so a cue starts on the frame it is asked for.
    /// </summary>
    public sealed class CueAudioImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Unity.ProjectVania/Resources/" + CuePlayer.ResourceFolder + "/";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder))
            {
                return;
            }

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = false;
            var sample = importer.defaultSampleSettings;
            sample.loadType = AudioClipLoadType.DecompressOnLoad;
            sample.compressionFormat = AudioCompressionFormat.PCM;
            sample.preloadAudioData = true;
            importer.defaultSampleSettings = sample;
        }
    }
}
