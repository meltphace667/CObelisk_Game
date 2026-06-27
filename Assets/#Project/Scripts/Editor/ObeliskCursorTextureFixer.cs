using UnityEditor;
using UnityEngine;

public static class ObeliskCursorTextureFixer
{
    [MenuItem("Tools/Obelisk/Fix Selected Cursor Textures")]
    public static void FixSelectedCursorTextures()
    {
        Object[] selectedObjects = Selection.objects;

        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Obelisk Cursor Fixer",
                "Sélectionne d'abord tes textures de curseur dans le Project.",
                "OK"
            );
            return;
        }

        int fixedCount = 0;
        int skippedCount = 0;

        foreach (Object selectedObject in selectedObjects)
        {
            string path = AssetDatabase.GetAssetPath(selectedObject);

            if (string.IsNullOrEmpty(path))
            {
                skippedCount++;
                continue;
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {
                skippedCount++;
                continue;
            }

            importer.textureType = TextureImporterType.Cursor;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;

            TextureImporterPlatformSettings defaultSettings = importer.GetDefaultPlatformTextureSettings();
            defaultSettings.overridden = true;
            defaultSettings.format = TextureImporterFormat.RGBA32;
            defaultSettings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(defaultSettings);

            TextureImporterPlatformSettings standaloneSettings = importer.GetPlatformTextureSettings("Standalone");
            standaloneSettings.overridden = true;
            standaloneSettings.format = TextureImporterFormat.RGBA32;
            standaloneSettings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(standaloneSettings);

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            fixedCount++;
            Debug.Log("[ObeliskCursorTextureFixer] Cursor fixed: " + path);
        }

        EditorUtility.DisplayDialog(
            "Obelisk Cursor Fixer",
            "Terminé.\n\nTextures corrigées : " + fixedCount + "\nIgnorées : " + skippedCount,
            "OK"
        );
    }
}