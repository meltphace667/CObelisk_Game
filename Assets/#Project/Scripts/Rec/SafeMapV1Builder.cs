using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SafeMapV1Builder
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
        MAP V1 SAFE

        Ob_01 = start
        Ob_02 = room la plus proche de l'obélisque / source sonore

        Distances depuis Ob_02 :
        Ob_02 = 0
        Ob_01 = 1
        PRA_01 = 2

        Réceptacle :
        SIL_03 = distance 8

        Cadre important :
        CHA_INT_02 = distance 8
    */

    private static readonly RoomDef[] rooms =
    {
        // OBÉLISQUE / START
        new RoomDef("Ob_02", bas: "Ob_01"),
        new RoomDef("Ob_01", haut: "Ob_02", bas: "PRA_01"),

        // CARREFOUR
        new RoomDef("PRA_01", haut: "Ob_01", bas: "FOR_01", gauche: "LAC_A1", droite: "FOR_L1"),

        // CHEMIN LAC PRINCIPAL
        new RoomDef("LAC_A1", gauche: "LAC_A2", droite: "PRA_01"),
        new RoomDef("LAC_A2", gauche: "LAC_01", droite: "LAC_A1"),

        // CHEMIN LAC SECONDAIRE
        new RoomDef("FOR_L1", gauche: "PRA_01", droite: "FOR_L2"),
        new RoomDef("FOR_L2", gauche: "FOR_L1", bas: "LAC_01"),

        // LAC
        new RoomDef("LAC_01", droite: "LAC_A2", haut: "FOR_L2", bas: "SIL_01"),

        // APRÈS-LAC / ZONE SILENCIEUSE / RÉCEPTACLE
        new RoomDef("SIL_01", haut: "LAC_01", bas: "SIL_02"),
        new RoomDef("SIL_02", haut: "SIL_01", bas: "SIL_03"),
        new RoomDef("SIL_03", haut: "SIL_02"),

        // FORÊT VERS CHÂTEAU
        new RoomDef("FOR_01", haut: "PRA_01", bas: "FOR_02"),
        new RoomDef("FOR_02", haut: "FOR_01", bas: "CHA_FAR"),

        // CHÂTEAU EXTÉRIEUR
        new RoomDef("CHA_FAR", haut: "FOR_02", bas: "CHA_NEAR"),
        new RoomDef("CHA_NEAR", haut: "CHA_FAR", bas: "CHA_INT_01"),

        // CHÂTEAU INTÉRIEUR
        new RoomDef("CHA_INT_01", haut: "CHA_NEAR", bas: "CHA_INT_02"),
        new RoomDef("CHA_INT_02", haut: "CHA_INT_01"),
    };

    private static readonly string[] oldUnusedRoomsToDisable =
    {
        "Ch_02",
        "Ch_03"
    };

    [MenuItem("Tools/Obelisk/SAFE - Construire chemins map V1")]
    public static void BuildSafeMapV1()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Stop",
                "Sors du Play Mode avant de construire la map.",
                "OK"
            );
            return;
        }

        List<string> problems = ValidateMap();

        if (problems.Count > 0)
        {
            EditorUtility.DisplayDialog(
                "Erreur map",
                string.Join("\n", problems),
                "OK"
            );
            return;
        }

        Canvas canvas = FindSceneObject<Canvas>();

        if (canvas == null)
        {
            EditorUtility.DisplayDialog(
                "Erreur",
                "Aucun Canvas trouvé.",
                "OK"
            );
            return;
        }

        Transform template = canvas.transform.Find("Ob_01");

        if (template == null)
        {
            EditorUtility.DisplayDialog(
                "Erreur",
                "Impossible de trouver Canvas/Ob_01.\n\nOb_01 doit exister car il sert de modèle visuel.",
                "OK"
            );
            return;
        }

        Image templateImage = template.GetComponent<Image>();
        RectTransform templateRect = template.GetComponent<RectTransform>();

        if (templateImage == null || templateRect == null)
        {
            EditorUtility.DisplayDialog(
                "Erreur",
                "Ob_01 doit avoir un RectTransform et un composant Image.",
                "OK"
            );
            return;
        }

        Transform ob02 = canvas.transform.Find("Ob_02");

        if (ob02 == null)
        {
            EditorUtility.DisplayDialog(
                "Erreur",
                "Impossible de trouver Canvas/Ob_02.\n\nOb_02 doit exister car c'est la room la plus proche de l'obélisque.",
                "OK"
            );
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "Construire map V1 safe",
            "Le script va :\n\n" +
            "- garder tes vrais Ob_01 et Ob_02\n" +
            "- créer les rooms manquantes directement sous Canvas\n" +
            "- remplir BackgroundManager avec tous les chemins\n" +
            "- mettre Start Room Id = Ob_01\n" +
            "- mettre l'obélisque sonore sur Ob_02\n\n" +
            "Il ne supprime rien.\n\nContinuer ?",
            "Oui",
            "Annuler"
        );

        if (!confirm)
            return;

        DisableRoomsAutoIfExists(canvas.transform);

        Dictionary<string, GameObject> roomObjects = new Dictionary<string, GameObject>();

        foreach (RoomDef room in rooms)
        {
            GameObject roomObject = GetOrCreateRoomObject(canvas.transform, room.id, template.gameObject);
            roomObject.SetActive(room.id == "Ob_01");

            Image image = roomObject.GetComponent<Image>();

            if (image != null)
                image.raycastTarget = false;

            roomObjects[room.id] = roomObject;
        }

        DisableOldUnusedRooms(canvas.transform);

        BackgroundManager backgroundManager = FindSceneObject<BackgroundManager>();

        if (backgroundManager == null)
        {
            EditorUtility.DisplayDialog(
                "Erreur",
                "Aucun BackgroundManager trouvé.",
                "OK"
            );
            return;
        }

        FillBackgroundManager(backgroundManager, roomObjects);
        ConfigureSpatializer(backgroundManager);

        EditorUtility.SetDirty(backgroundManager);
        EditorSceneManager.MarkSceneDirty(backgroundManager.gameObject.scene);

        Debug.Log("[SafeMapV1Builder] Map V1 SAFE construite.");
        Debug.Log("[SafeMapV1Builder] Start : Ob_01");
        Debug.Log("[SafeMapV1Builder] Source obélisque : Ob_02");
        Debug.Log("[SafeMapV1Builder] Réceptacle : SIL_03");
        Debug.Log("[SafeMapV1Builder] Cadre : CHA_INT_02");

        Selection.activeObject = backgroundManager.gameObject;

        EditorUtility.DisplayDialog(
            "Map V1 prête",
            "Chemins créés.\n\n" +
            "Start : Ob_01\n" +
            "Obélisque source : Ob_02\n" +
            "Réceptacle : SIL_03\n" +
            "Cadre : CHA_INT_02\n\n" +
            "Tu peux maintenant juste remplacer les images des rooms créées.",
            "OK"
        );
    }

    private static GameObject GetOrCreateRoomObject(Transform canvasTransform, string roomId, GameObject templateObject)
    {
        Transform existing = canvasTransform.Find(roomId);

        if (existing != null)
            return existing.gameObject;

        GameObject newRoom = Object.Instantiate(templateObject, canvasTransform);
        Undo.RegisterCreatedObjectUndo(newRoom, "Create room " + roomId);

        newRoom.name = roomId;
        newRoom.SetActive(false);

        RectTransform newRect = newRoom.GetComponent<RectTransform>();
        RectTransform templateRect = templateObject.GetComponent<RectTransform>();

        if (newRect != null && templateRect != null)
            CopyRectTransform(templateRect, newRect);

        Image image = newRoom.GetComponent<Image>();

        if (image != null)
        {
            image.raycastTarget = false;
            image.color = Color.white;
        }

        newRoom.layer = templateObject.layer;

        return newRoom;
    }

    private static void CopyRectTransform(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;

        target.anchoredPosition = source.anchoredPosition;
        target.anchoredPosition3D = source.anchoredPosition3D;
        target.sizeDelta = source.sizeDelta;
        target.offsetMin = source.offsetMin;
        target.offsetMax = source.offsetMax;

        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
    }

    private static void FillBackgroundManager(BackgroundManager backgroundManager, Dictionary<string, GameObject> roomObjects)
    {
        SerializedObject serializedObject = new SerializedObject(backgroundManager);

        SerializedProperty roomsProperty = serializedObject.FindProperty("rooms");
        SerializedProperty startRoomIdProperty = serializedObject.FindProperty("startRoomId");

        if (roomsProperty == null)
        {
            EditorUtility.DisplayDialog(
                "Erreur",
                "Champ 'rooms' introuvable dans BackgroundManager.",
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

            if (imageProperty != null && roomObjects.TryGetValue(def.id, out GameObject roomObject))
                imageProperty.objectReferenceValue = roomObject;
        }

        if (startRoomIdProperty != null)
            startRoomIdProperty.stringValue = "Ob_01";

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
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
            obeliskRoomIdProperty.stringValue = "Ob_02";

        if (backgroundManagerProperty != null)
            backgroundManagerProperty.objectReferenceValue = backgroundManager;

        MusicManager musicManager = FindSceneObject<MusicManager>();
        SerializedProperty musicManagerProperty = serializedObject.FindProperty("musicManager");

        if (musicManagerProperty != null && musicManager != null)
            musicManagerProperty.objectReferenceValue = musicManager;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spatializer);
    }

    private static void DisableRoomsAutoIfExists(Transform canvasTransform)
    {
        Transform roomsAuto = canvasTransform.Find("Rooms_Auto");

        if (roomsAuto != null)
            roomsAuto.gameObject.SetActive(false);
    }

    private static void DisableOldUnusedRooms(Transform canvasTransform)
    {
        foreach (string roomName in oldUnusedRoomsToDisable)
        {
            Transform room = canvasTransform.Find(roomName);

            if (room == null)
                continue;

            room.gameObject.SetActive(false);
        }
    }

    private static List<string> ValidateMap()
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

        string[] required =
        {
            "Ob_01",
            "Ob_02",
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

        foreach (string requiredId in required)
        {
            if (!ids.Contains(requiredId))
                problems.Add("Room obligatoire manquante : " + requiredId);
        }

        if (rooms.Length != 17)
            problems.Add("La map devrait avoir 17 rooms, mais elle en a " + rooms.Length + ".");

        return problems;
    }

    private static void CheckConnection(string fromRoom, string direction, string targetRoom, HashSet<string> ids, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(targetRoom))
            return;

        if (!ids.Contains(targetRoom))
            problems.Add("Connexion invalide : " + fromRoom + " / " + direction + " -> " + targetRoom);
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