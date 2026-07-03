using UnityEditor;

/// The road splatmap must keep its full 4K resolution — Unity's default
/// 2048 cap would blur every road in the city.
public class TextureImportRules : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("roadsplat")) return;
        var importer = (TextureImporter)assetImporter;
        importer.maxTextureSize = 4096;
        importer.mipmapEnabled = true;
        importer.alphaIsTransparency = false;   // alpha is a blend mask, not transparency
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
