using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ObeliskRoomImageAutoAssigner
{
    private static readonly string[] roomIds =
    {
        "Ob_01",
        "Ob_02",
        "PRA_01",
        "LAC_A1",
        "LAC_A2",
        "LAC_01",
        "FOR_L1",
        "FOR_L2",
        "SIL_01",
        "SIL_02",
        "SIL_03",
        "FOR_01",
        "FOR_02",
        "CHA_FAR",
        "CHA_NEAR",
        "CHA_INT_01",
        "CHA_INT_02"
    };

    [MenuItem("Tools/Obelisk/Assigner images rooms V1")]
    public static void AssignRoomImagesV1()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Stop",
                "Sors du Play Mode avant d'assigner les images.",
                "OK"
            );
            return;
        }

        Canvas canvas = FindSceneObject<Canvas>();

        if (canvas == null)
        {
            EditorUtility.DisplayDialog(
                "Erreur",
                "Aucun Canvas trouvé dans la scène.",
                "OK"
            );
            return;
        }

        Dictionary<string, GameObject> roomObjects = new Dictionary<string, GameObject>();

        List<string> missingObjects = new List<string>();
        List<string> missingSprites = new List<string>();
        List<string> assigned = new List<string>();

        foreach (string roomId in roomIds)
        {
            GameObject roomObject = FindRoomObject(canvas.transform, roomId);

            if (roomObject == null)
            {
                missingObjects.Add(roomId);
                continue;
            }

            Sprite sprite = FindOrCreateSpriteForRoom(roomId);

            if (sprite == null)
            {
                missingSprites.Add(roomId);
                continue;
            }

            Image image = roomObject.GetComponent<Image>();

            if (image == null)
                image = roomObject.AddComponent<Image>();

            Undo.RecordObject(image, "Assign room source image");

            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;

            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(roomObject);

            roomObjects[roomId] = roomObject;
            assigned.Add(roomId + "  ←  " + AssetDatabase.GetAssetPath(sprite));
        }

        UpdateBackgroundManagerImageReferences(roomObjects);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        AssetDatabase.SaveAssets();

        string message =
            "Assignation terminée.\n\n" +
            "Images assignées : " + assigned.Count + " / " + roomIds.Length + "\n";

        if (missingObjects.Count > 0)
        {
            message += "\nRooms GameObject introuvables :\n";
            message += string.Join("\n", missingObjects);
            message += "\n";
        }

        if (missingSprites.Count > 0)
        {
            message += "\nSprites/images introuvables :\n";
            message += string.Join("\n", missingSprites);
            message += "\n\nVérifie que les fichiers sont bien dans Assets et qu'ils portent exactement ces noms.";
        }

        Debug.Log("[ObeliskRoomImageAutoAssigner] Assignées :\n" + string.Join("\n", assigned));

        if (missingObjects.Count > 0)
            Debug.LogWarning("[ObeliskRoomImageAutoAssigner] Rooms introuvables : " + string.Join(", ", missingObjects));

        if (missingSprites.Count > 0)
            Debug.LogWarning("[ObeliskRoomImageAutoAssigner] Sprites introuvables : " + string.Join(", ", missingSprites));

        EditorUtility.DisplayDialog(
            "Obelisk images rooms V1",
            message,
            "OK"
        );
    }

    private static GameObject FindRoomObject(Transform canvasTransform, string roomId)
    {
        Transform[] allTransforms = canvasTransform.GetComponentsInChildren<Transform>(true);

        List<Transform> candidates = allTransforms
            .Where(t => t.name == roomId)
            .ToList();

        if (candidates.Count == 0)
            return null;

        // Priorité 1 : room directement sous Canvas, c'est le setup le plus propre actuel.
        Transform directChild = candidates.FirstOrDefault(t => t.parent == canvasTransform);

        if (directChild != null)
            return directChild.gameObject;

        // Priorité 2 : éviter Rooms_Auto si possible.
        Transform notRoomsAuto = candidates.FirstOrDefault(t => !IsInsideNamedParent(t, "Rooms_Auto"));

        if (notRoomsAuto != null)
            return notRoomsAuto.gameObject;

        // Dernier recours.
        return candidates[0].gameObject;
    }

    private static bool IsInsideNamedParent(Transform transform, string parentName)
    {
        Transform current = transform.parent;

        while (current != null)
        {
            if (current.name == parentName)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static Sprite FindOrCreateSpriteForRoom(string roomId)
    {
        string[] guids = AssetDatabase.FindAssets(roomId);

        List<string> candidatePaths = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(path))
                continue;

            string fileName = Path.GetFileNameWithoutExtension(path);

            if (!string.Equals(fileName, roomId, System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (!IsImagePath(path))
                continue;

            candidatePaths.Add(path);
        }

        candidatePaths = candidatePaths
            .OrderByDescending(GetPathPriorityScore)
            .ToList();

        foreach (string path in candidatePaths)
        {
            Sprite sprite = LoadSpriteAtPath(path);

            if (sprite != null)
                return sprite;

            TryConvertTextureToSprite(path);

            sprite = LoadSpriteAtPath(path);

            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private static bool IsImagePath(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();

        return extension == ".png" ||
               extension == ".jpg" ||
               extension == ".jpeg" ||
               extension == ".webp" ||
               extension == ".gif" ||
               extension == ".tga" ||
               extension == ".psd";
    }

    private static int GetPathPriorityScore(string path)
    {
        string lower = path.ToLowerInvariant();
        int score = 0;

        if (lower.Contains("obelisk_mapv1_selected_rooms"))
            score += 1000;

        if (lower.Contains("selected"))
            score += 500;

        if (lower.Contains("mapv1"))
            score += 250;

        if (lower.Contains("#project"))
            score += 100;

        if (lower.Contains("/images"))
            score += 50;

        string extension = Path.GetExtension(path).ToLowerInvariant();

        if (extension == ".png")
            score += 10;

        if (extension == ".jpg" || extension == ".jpeg")
            score += 5;

        return score;
    }

    private static Sprite LoadSpriteAtPath(string path)
    {
        Sprite directSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (directSprite != null)
            return directSprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

        foreach (Object asset in assets)
        {
            Sprite sprite = asset as Sprite;

            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private static void TryConvertTextureToSprite(string path)
    {
        TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;

        if (textureImporter == null)
            return;

        bool changed = false;

        if (textureImporter.textureType != TextureImporterType.Sprite)
        {
            textureImporter.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (textureImporter.spriteImportMode != SpriteImportMode.Single)
        {
            textureImporter.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (textureImporter.mipmapEnabled)
        {
            textureImporter.mipmapEnabled = false;
            changed = true;
        }

        if (textureImporter.alphaIsTransparency == false)
        {
            textureImporter.alphaIsTransparency = true;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(textureImporter);
            textureImporter.SaveAndReimport();
        }
    }

    private static void UpdateBackgroundManagerImageReferences(Dictionary<string, GameObject> roomObjects)
    {
        BackgroundManager[] backgroundManagers = FindSceneObjects<BackgroundManager>();

        foreach (BackgroundManager backgroundManager in backgroundManagers)
        {
            if (backgroundManager == null)
                continue;

            SerializedObject serializedObject = new SerializedObject(backgroundManager);
            SerializedProperty roomsProperty = serializedObject.FindProperty("rooms");

            if (roomsProperty == null || !roomsProperty.isArray)
                continue;

            bool changed = false;

            for (int i = 0; i < roomsProperty.arraySize; i++)
            {
                SerializedProperty roomProperty = roomsProperty.GetArrayElementAtIndex(i);

                SerializedProperty idProperty = roomProperty.FindPropertyRelative("id");
                SerializedProperty imageProperty = roomProperty.FindPropertyRelative("image");

                if (idProperty == null || imageProperty == null)
                    continue;

                string roomId = idProperty.stringValue;

                if (!roomObjects.TryGetValue(roomId, out GameObject roomObject))
                    continue;

                Object referenceToAssign = roomObject;

                // Selon ta version de BackgroundManager, image peut être un GameObject ou un Image.
                if (imageProperty.type.Contains("Image"))
                {
                    Image image = roomObject.GetComponent<Image>();

                    if (image != null)
                        referenceToAssign = image;
                }

                imageProperty.objectReferenceValue = referenceToAssign;
                changed = true;
            }

            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(backgroundManager);
            }
        }
    }

    private static T FindSceneObject<T>() where T : Object
    {
        T[] objects = Resources.FindObjectsOfTypeAll<T>();

        foreach (T obj in objects)
        {
            if (obj == null)
                continue;

            if (EditorUtility.IsPersistent(obj))
                continue;

            if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave)
                continue;

            return obj;
        }

        return null;
    }

    private static T[] FindSceneObjects<T>() where T : Object
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .Where(obj =>
                obj != null &&
                !EditorUtility.IsPersistent(obj) &&
                obj.hideFlags != HideFlags.NotEditable &&
                obj.hideFlags != HideFlags.HideAndDontSave
            )
            .ToArray();
    }
}
