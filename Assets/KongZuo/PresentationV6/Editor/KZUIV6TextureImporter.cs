#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace KongZuo.EditorTools
{
    // Strictly limited to V5 UI sprites. Does NOT reimport theatre meshes or teammate materials.
    public sealed class KZUIV6TextureImporter : AssetPostprocessor
    {
        private const string Root = "Assets/KongZuo/PresentationV6/Resources/KZUIV6/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default; // GUI.DrawTexture expects Texture2D
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
        }

    }
}
#endif
