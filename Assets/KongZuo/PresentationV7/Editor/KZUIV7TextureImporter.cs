#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace KongZuo.EditorTools
{
    // Applies only to the two new UI V7 textures. No theatre/scene assets touched.
    public sealed class KZUIV7TextureImporter : AssetPostprocessor
    {
        private const string Prefix = "Assets/KongZuo/PresentationV7/Resources/KZUIV7/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Prefix, StringComparison.Ordinal)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
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
