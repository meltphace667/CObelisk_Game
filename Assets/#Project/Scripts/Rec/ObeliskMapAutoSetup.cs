using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ObeliskMapAutoSetup
{
    private struct RoomDef
    {
        public string id;
        public string haut;
        public string bas;
        public string gauche;
        public string droite;

        public RoomDef(string id, string haut = "", string bas = "", string gauche = "", string droite = "")
        {
            this.id = id;
            this.haut = haut;
            this.bas = bas;
            this.gauche = gauche;
            this.droite = droite;
        }
    }

    /*
        MAP V1 — TRIPLECHECK

        OB_01       : obélisque proche / start
        OB_02       : obélisque plus loin
        PRA_01      : prairie / carrefour central

        Chemin lac principal :
        LAC_A1
        LAC_A2
        LAC_01

        Chemin lac secondaire :
        FOR_L1
        FOR_L2
        LAC_01

        Après-lac / zone silencieuse / réceptacle :
        SIL_01
        SIL_02
        SIL_03      : réceptacle

        Branche château :
        FOR_01
        FOR_02
        CHA_FAR     : château vu de loin
        CHA_NEAR    : château proche
        CHA_INT_01  : intérieur château 1
        CHA_INT_02  : intérieur château 2 / cadre important
    */

    private static readonly RoomDef[] rooms =
    {
        // OBÉLISQUE
        new RoomDef("OB_01", haut: "OB_02"),
        new RoomDef("OB_02", haut: "PRA_01", bas: "OB_01"),

        // CARREFOUR PRINCIPAL
        new RoomDef("PRA_01", haut: "FOR_01", bas: "OB_02", gauche: "LAC_A1", droite: "FOR_L1"),

        // CHEMIN LAC PRINCIPAL
        new RoomDef("LAC_A1", droite: "PRA_01", haut: "LAC_A2"),
        new RoomDef("LAC_A2", bas: "LAC_A1", haut: "LAC_01"),

        // CHEMIN LAC SECONDAIRE
        new RoomDef("FOR_L1", gauche: "PRA_01", haut: "FOR_L2"),
        new RoomDef("FOR_L2", bas: "FOR_L1", droite: "LAC_01"),

        // LAC
        new RoomDef("LAC_01", bas: "LAC_A2", gauche: "FOR_L2", haut: "SIL_01"),

        // ZONE SILENCIEUSE / RÉCEPTACLE
        new RoomDef("SIL_01", bas: "LAC_01", haut: "SIL_02"),
        new RoomDef("SIL_02", bas: "SIL_01", haut: "SIL_03"),
        new RoomDef("SIL_03", bas: "SIL_02"),

        // FORÊT VERS CHÂTEAU
        new RoomDef("FOR_01", bas: "PRA_01", haut: "FOR_02"),
        new RoomDef("FOR_02", bas: "FOR_01", haut: "CHA_FAR"),

        // APPROCHE CHÂTEAU
        new RoomDef("CHA_FAR", bas: "FOR_02", haut: "CHA_NEAR"),
        new RoomDef("CHA_NEAR", bas: "CHA_FAR", haut: "CHA_INT_01"),

        // INTÉRIEUR CHÂTEAU
        new RoomDef("CHA_INT_01", bas: "CHA_NEAR", haut: "CHA_INT_02"),
        new RoomDef("CHA_INT_02", bas: "CHA_INT_01"),
    };

    private static readonly string[] oldVisualRoomsToDisable =
    {
        "Ob_01",
        "Ob_02",
        "Ch_02",
        "Ch_03"
    };

    [MenuItem("Tools/Obelisk/Créer map V1 ULTRA SIMPLE")]
    public static void CreateMapV1UltraSimple()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Stop",
                "Sors du Play Mode avant de générer la map.",
                "OK"
            );
            return;
        }

        List<string> mapProblems = ValidateMapData();

        if (mapProblems.Count > 0)
        {
            EditorUtility.DisplayDialog(
                "Erreur dans la map",
                string.Join("\n", mapProblems),
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

        BackgroundManager backgroundManager = FindSceneObject<BackgroundManager>();

        if (backgroundManager == null)
        {
            EditorUtility.DisplayDialog(
                "Erreur",
                "Aucun BackgroundManager trouvé dans la scène.",
                "OK"
            );
            return;
        }

        Transform templateTransform = FindTemplateRoom(canvas.transform);

        if (templateTransform == null)
        {
            EditorUtility.DisplayDialog(
                "Erreur",
                "Impossible de trouver une room modèle.\n\nLe script cherche d'abord :\nCanvas/Ob_01\n\nGarde ton ancien Ob_01 dans le Canvas pour servir de modèle visuel.",
                "OK"
            );
            return;
        }

        RectTransform templateRect = templateTransform.GetComponent<RectTransform>();
        Image templateImage = templateTransform.GetComponent<Image>();

        if (templateRect == null || templateImage == null)
        {
            EditorUtility.DisplayDialog(
                "Erreur",
                "La room modèle doit avoir un RectTransform et un composant Image.",
                "OK"
            );
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "Créer map V1 ULTRA SIMPLE",
            "Le script va :\n\n" +
            "- recréer Canvas/Rooms_Auto\n" +
            "- créer les 17 rooms V1\n" +
            "- copier le visuel de ton ancien Ob_01\n" +
            "- remplir BackgroundManager\n" +
            "- désactiver les anciennes rooms Ob_01 / Ob_02 / Ch_02 / Ch_03\n\n" +
            "Continuer ?",
            "Oui",
            "Annuler"
        );

        if (!confirm)
            return;

        Transform roomsRoot = GetOrCreateRoomsRoot(canvas.transform, templateTransform.gameObject.layer);

        DeleteUnexpectedGeneratedRooms(roomsRoot);

        Dictionary<string, GameObject> createdRoomObjects = new Dictionary<string, GameObject>();

        foreach (RoomDef room in rooms)
        {
            GameObject roomObject = CreateOrUpdateRoomObject(
                roomsRoot,
                room.id,
                templateTransform,
                templateRect,
                templateImage
            );

            roomObject.SetActive(room.id == "OB_01");
            createdRoomObjects[room.id] = roomObject;
        }

        FillBackgroundManager(backgroundManager, createdRoomObjects);
        ConfigureSpatializer(backgroundManager);
        DisableOldVisualRooms(canvas.transform);

        roomsRoot.SetAsFirstSibling();

        EditorUtility.SetDirty(backgroundManager);
        EditorSceneManager.MarkSceneDirty(backgroundManager.gameObject.scene);
        AssetDatabase.SaveAssets();

        Selection.activeObject = backgroundManager.gameObject;

        Debug.Log("[ObeliskMapAutoSetup] TRIPLECHECK OK : 17 rooms créées.");
        Debug.Log("[ObeliskMapAutoSetup] Réceptacle prévu : SIL_03.");
        Debug.Log("[ObeliskMapAutoSetup] Cadre important prévu : CHA_INT_02.");
        Debug.Log("[ObeliskMapAutoSetup] Start room : OB_01.");
        Debug.Log("[ObeliskMapAutoSetup] Map V1 ULTRA SIMPLE créée avec succès.");

        EditorUtility.DisplayDialog(
            "Succès",
            "Map V1 créée proprement.\n\n" +
            "Rooms créées : 17\n" +
            "Start : OB_01\n" +
            "Réceptacle : SIL_03\n" +
            "Cadre : CHA_INT_02\n\n" +
            "Les anciennes rooms sont désactivées mais pas supprimées.",
            "OK"
        );
    }

    private static List<string> ValidateMapData()
    {
        List<string> problems = new List<string>();
        HashSet<string> ids = new HashSet<string>();

        foreach (RoomDef room in rooms)
        {
            if (string.IsNullOrWhiteSpace(room.id))
            {
                problems.Add("Une room a un ID vide.");
                continue;
            }

            if (!ids.Add(room.id))
                problems.Add("Room en double : " + room.id);
        }

        foreach (RoomDef room in rooms)
        {
            CheckConnection(room.id, "haut", room.haut, ids, problems);
            CheckConnection(room.id, "bas", room.bas, ids, problems);
            CheckConnection(room.id, "gauche", room.gauche, ids, problems);
            CheckConnection(room.id, "droite", room.droite, ids, problems);
        }

        string[] requiredRooms =
        {
            "OB_01",
            "OB_02",
            "PRA_01",
            "LAC_A1",
            "LAC_A2",
            "FOR_L1",
            "FOR_L2",
            "LAC_01",
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

        foreach (string requiredRoom in requiredRooms)
        {
            if (!ids.Contains(requiredRoom))
                problems.Add("Room obligatoire manquante : " + requiredRoom);
        }

        if (rooms.Length != 17)
            problems.Add("La map devrait contenir exactement 17 rooms, mais elle en contient " + rooms.Length + ".");

        return problems;
    }

    private static void CheckConnection(string fromRoom, string directionName, string targetRoom, HashSet<string> ids, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(targetRoom))
            return;

        if (!ids.Contains(targetRoom))
            problems.Add("Connexion invalide : " + fromRoom + " / " + directionName + " -> " + targetRoom);
    }

    private static Transform FindTemplateRoom(Transform canvasTransform)
    {
        Transform template = canvasTransform.Find("Ob_01");

        if (template != null)
            return template;

        Transform roomsAuto = canvasTransform.Find("Rooms_Auto");

        if (roomsAuto != null)
        {
            Transform autoTemplate = roomsAuto.Find("OB_01");

            if (autoTemplate != null)
                return autoTemplate;
        }

        return null;
    }

    private static Transform GetOrCreateRoomsRoot(Transform canvasTransform, int layer)
    {
        Transform existing = canvasTransform.Find("Rooms_Auto");

        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            existing.gameObject.layer = layer;

            RectTransform existingRect = existing.GetComponent<RectTransform>();

            if (existingRect == null)
                existingRect = existing.gameObject.AddComponent<RectTransform>();

            StretchFullScreen(existingRect);

            return existing;
        }

        GameObject rootObject = new GameObject("Rooms_Auto");
        Undo.RegisterCreatedObjectUndo(rootObject, "Create Rooms_Auto");

        rootObject.layer = layer;
        rootObject.transform.SetParent(canvasTransform, false);

        RectTransform rootRect = rootObject.AddComponent<RectTransform>();
        StretchFullScreen(rootRect);

        return rootObject.transform;
    }

    private static void StretchFullScreen(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.anchoredPosition3D = Vector3.zero;
    }

    private static void DeleteUnexpectedGeneratedRooms(Transform roomsRoot)
    {
        HashSet<string> expectedIds = new HashSet<string>(rooms.Select(room => room.id));

        for (int i = roomsRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = roomsRoot.GetChild(i);

            if (!expectedIds.Contains(child.name))
                Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    private static GameObject CreateOrUpdateRoomObject(
        Transform roomsRoot,
        string roomId,
        Transform templateTransform,
        RectTransform templateRect,
        Image templateImage
    )
    {
        Transform existing = roomsRoot.Find(roomId);
        GameObject roomObject;

        if (existing == null)
        {
            roomObject = new GameObject(roomId);
            Undo.RegisterCreatedObjectUndo(roomObject, "Create Room " + roomId);
            roomObject.transform.SetParent(roomsRoot, false);
        }
        else
        {
            roomObject = existing.gameObject;
        }

        roomObject.name = roomId;
        roomObject.layer = templateTransform.gameObject.layer;
        roomObject.tag = templateTransform.gameObject.tag;

        RectTransform roomRect = roomObject.GetComponent<RectTransform>();

        if (roomRect == null)
            roomRect = roomObject.AddComponent<RectTransform>();

        Image roomImage = roomObject.GetComponent<Image>();

        if (roomImage == null)
            roomImage = roomObject.AddComponent<Image>();

        CopyRectTransform(templateRect, roomRect);
        CopyImageSettings(templateImage, roomImage);

        Sprite existingSprite = roomImage.sprite;
        Sprite detectedSprite = FindBestSpriteForRoom(roomId);

        if (detectedSprite != null)
            roomImage.sprite = detectedSprite;
        else if (existingSprite != null)
            roomImage.sprite = existingSprite;
        else
            roomImage.sprite = templateImage.sprite;

        roomImage.raycastTarget = false;

        EditorUtility.SetDirty(roomObject);

        return roomObject;
    }

    private static void CopyRectTransform(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;

        target.offsetMin = source.offsetMin;
        target.offsetMax = source.offsetMax;
        target.sizeDelta = source.sizeDelta;
        target.anchoredPosition = source.anchoredPosition;
        target.anchoredPosition3D = source.anchoredPosition3D;

        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
    }

    private static void CopyImageSettings(Image source, Image target)
    {
        target.color = source.color;
        target.material = source.material;
        target.maskable = source.maskable;

        target.type = source.type;
        target.preserveAspect = source.preserveAspect;
        target.useSpriteMesh = source.useSpriteMesh;

        target.fillCenter = source.fillCenter;
        target.fillMethod = source.fillMethod;
        target.fillAmount = source.fillAmount;
        target.fillClockwise = source.fillClockwise;
        target.fillOrigin = source.fillOrigin;

        target.raycastTarget = false;
    }

    private static Sprite FindBestSpriteForRoom(string roomId)
    {
        string[] candidates =
        {
            roomId,
            ToPrettyCase(roomId),
            roomId.ToLower(),
            roomId.ToUpper()
        };

        foreach (string candidate in candidates)
        {
            Sprite sprite = FindSpriteByLooseName(candidate);

            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private static Sprite FindSpriteByLooseName(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        string[] guids = AssetDatabase.FindAssets(candidate + " t:Sprite");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (Object asset in assets)
            {
                Sprite sprite = asset as Sprite;

                if (sprite == null)
                    continue;

                string spriteName = sprite.name.ToLowerInvariant();
                string candidateName = candidate.ToLowerInvariant();

                if (spriteName.Contains(candidateName) || candidateName.Contains(spriteName))
                    return sprite;

                return sprite;
            }
        }

        return null;
    }

    private static string ToPrettyCase(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return id;

        string lower = id.ToLowerInvariant();
        return char.ToUpper(lower[0]) + lower.Substring(1);
    }

    private static void FillBackgroundManager(BackgroundManager backgroundManager, Dictionary<string, GameObject> createdRoomObjects)
    {
        SerializedObject serializedObject = new SerializedObject(backgroundManager);

        SerializedProperty roomsProperty = serializedObject.FindProperty("rooms");
        SerializedProperty startRoomIdProperty = serializedObject.FindProperty("startRoomId");

        if (roomsProperty == null)
        {
            EditorUtility.DisplayDialog(
                "Erreur",
                "Impossible de trouver le champ 'rooms' dans BackgroundManager.",
                "OK"
            );
            return;
        }

        roomsProperty.ClearArray();
        roomsProperty.arraySize = rooms.Length;

        for (int i = 0; i < rooms.Length; i++)
        {
            RoomDef def = rooms[i];
            SerializedProperty roomProperty = roomsProperty.GetArrayElementAtIndex(i);

            SetString(roomProperty, "id", def.id);
            SetString(roomProperty, "haut", def.haut);
            SetString(roomProperty, "bas", def.bas);
            SetString(roomProperty, "gauche", def.gauche);
            SetString(roomProperty, "droite", def.droite);

            SerializedProperty imageProperty = roomProperty.FindPropertyRelative("image");

            if (imageProperty != null && createdRoomObjects.TryGetValue(def.id, out GameObject roomObject))
                imageProperty.objectReferenceValue = roomObject;
        }

        if (startRoomIdProperty != null)
            startRoomIdProperty.stringValue = "OB_01";

        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(backgroundManager);
    }

    private static void ConfigureSpatializer(BackgroundManager backgroundManager)
    {
        ObeliskMusicSpatializer spatializer = FindSceneObject<ObeliskMusicSpatializer>();

        if (spatializer == null)
            return;

        SerializedObject serializedObject = new SerializedObject(spatializer);

        SerializedProperty obeliskRoomIdProperty = serializedObject.FindProperty("obeliskRoomId");
        SerializedProperty backgroundManagerProperty = serializedObject.FindProperty("backgroundManager");

        if (obeliskRoomIdProperty != null)
            obeliskRoomIdProperty.stringValue = "OB_01";

        if (backgroundManagerProperty != null)
            backgroundManagerProperty.objectReferenceValue = backgroundManager;

        MusicManager musicManager = FindSceneObject<MusicManager>();
        SerializedProperty musicManagerProperty = serializedObject.FindProperty("musicManager");

        if (musicManagerProperty != null && musicManager != null)
            musicManagerProperty.objectReferenceValue = musicManager;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spatializer);
    }

    private static void DisableOldVisualRooms(Transform canvasTransform)
    {
        foreach (string oldRoomName in oldVisualRoomsToDisable)
        {
            Transform oldRoom = canvasTransform.Find(oldRoomName);

            if (oldRoom == null)
                continue;

            oldRoom.gameObject.SetActive(false);
            EditorUtility.SetDirty(oldRoom.gameObject);
        }
    }

    private static void SetString(SerializedProperty parent, string relativeName, string value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativeName);

        if (property != null)
            property.stringValue = value;
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
}