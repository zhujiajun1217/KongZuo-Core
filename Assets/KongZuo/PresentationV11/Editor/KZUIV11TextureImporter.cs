#if UNITY_EDITOR
using UnityEditor;

namespace KongZuo.EditorTools
{
    // Import only NEW V11 textures. Does not touch any original Unity material or sprite settings.
    public sealed class KZUIV11TextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/PresentationV11/Resources/KZUIV11/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.alphaIsTransparency = true;
            t.mipmapEnabled = false;
            t.npotScale = TextureImporterNPOTScale.None;
            t.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            t.filterMode = UnityEngine.FilterMode.Bilinear;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.maxTextureSize = 2048;
        }
    }
}
#endif
