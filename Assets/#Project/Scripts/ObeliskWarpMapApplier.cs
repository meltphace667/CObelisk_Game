using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class ObeliskWarpMapApplier : MonoBehaviour
{
    private struct Connection
    {
        public string id;
        public string haut;
        public string bas;
        public string gauche;
        public string droite;

        public Connection(string id, string haut, string bas, string gauche, string droite)
        {
            this.id = id;
            this.haut = haut;
            this.bas = bas;
            this.gauche = gauche;
            this.droite = droite;
        }
    }

    [Header("Référence")]
    [SerializeField] private BackgroundManager backgroundManager;

    [Header("Options")]
    [SerializeField] private bool setStartRoomToo = true;
    [SerializeField] private string startRoomId = "PRA_01";
    [SerializeField] private bool printMapAfterApply = true;
    [SerializeField] private bool copyMermaidToClipboard = true;

    [Header("Sécurité")]
    [SerializeField] private bool abortIfAnyRoomIsMissing = true;
    [SerializeField] private bool leaveUnknownRoomsUntouched = true;

    [Header("Notes")]
    [TextArea(5, 12)]
    [SerializeField] private string designNotes =
        "V5 est la version recommandée : pas de sortie Bas depuis le hub, mais on garde deux chemins vers le lac.\n" +
        "Le contournement du carré noir se fait dans la branche lac, depuis LAC_A1, pas depuis PRA_01.\n" +
        "V4 garde le contournement depuis le hub par Bas, mais ça force trop de clics Bas.\n" +
        "V3 supprime le raccourci vers le lac.\n" +
        "V2 garde l'ancien raccourci moins clair.\n" +
        "Original restaure exactement la map exportée avant les changements.";

    [ContextMenu("OBELISK / Apply V5 RECOMMENDED - No Hub Down Spam")]
    public void ApplyV5NoHubDownSpam()
    {
        ApplyMap(BuildV5NoHubDownSpamMap(), "V5 RECOMMENDED - NO HUB DOWN SPAM");
    }

    [ContextMenu("OBELISK / Apply V4 RECOMMENDED - Black Square Bypass")]
    public void ApplyV4BlackSquareBypass()
    {
        ApplyMap(BuildV4BlackSquareBypassMap(), "V4 RECOMMENDED - BLACK SQUARE BYPASS");
    }

    [ContextMenu("OBELISK / Apply V3 Simple - No Lake Bypass")]
    public void ApplyV3SimpleNoLakeBypass()
    {
        ApplyMap(BuildV3SimpleNoLakeBypassMap(), "V3 SIMPLE - NO LAKE BYPASS");
    }

    [ContextMenu("OBELISK / Apply V2 Lateral - Old Two Paths")]
    public void ApplyV2LateralOldTwoPaths()
    {
        ApplyMap(BuildV2LateralOldTwoPathsMap(), "V2 LATERAL - OLD TWO PATHS");
    }

    [ContextMenu("OBELISK / Restore V1 Original Exported Warps")]
    public void RestoreV1OriginalExportedWarps()
    {
        ApplyMap(BuildV1OriginalExportedMap(), "V1 ORIGINAL EXPORTED");
    }

    [ContextMenu("OBELISK / Print Current Warp Map")]
    public void PrintCurrentWarpMap()
    {
        if (!ResolveBackgroundManager())
            return;

        IList rooms = GetRoomsList();

        if (rooms == null)
        {
            Debug.LogError("[ObeliskWarpMapApplier] Impossible de lire la liste privée 'rooms'.");
            return;
        }

        string report = BuildCurrentTextReport(rooms);
        string mermaid = BuildCurrentMermaidGraph(rooms);

        Debug.Log(report);
        Debug.Log(mermaid);

        if (copyMermaidToClipboard)
        {
            GUIUtility.systemCopyBuffer = mermaid;
            Debug.Log("[ObeliskWarpMapApplier] Mermaid copié dans le presse-papiers.");
        }
    }

    [ContextMenu("OBELISK / Validate Current Warp Map")]
    public void ValidateCurrentWarpMap()
    {
        if (!ResolveBackgroundManager())
            return;

        IList rooms = GetRoomsList();

        if (rooms == null)
        {
            Debug.LogError("[ObeliskWarpMapApplier] Impossible de lire la liste privée 'rooms'.");
            return;
        }

        HashSet<string> ids = GetRoomIds(rooms);
        int problems = 0;

        for (int i = 0; i < rooms.Count; i++)
        {
            object room = rooms[i];

            if (room == null)
                continue;

            string id = GetStringField(room, "id");

            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[ObeliskWarpMapApplier] Room sans ID à l'index " + i);
                problems++;
                continue;
            }

            problems += ValidateTarget(ids, id, "haut", GetStringField(room, "haut"));
            problems += ValidateTarget(ids, id, "bas", GetStringField(room, "bas"));
            problems += ValidateTarget(ids, id, "gauche", GetStringField(room, "gauche"));
            problems += ValidateTarget(ids, id, "droite", GetStringField(room, "droite"));
        }

        if (problems == 0)
            Debug.Log("[ObeliskWarpMapApplier] Validation OK : aucune connexion vers une room inexistante.");
        else
            Debug.LogWarning("[ObeliskWarpMapApplier] Validation terminée avec " + problems + " problème(s).");
    }

    private int ValidateTarget(HashSet<string> ids, string from, string direction, string target)
    {
        if (string.IsNullOrEmpty(target))
            return 0;

        if (ids.Contains(target))
            return 0;

        Debug.LogWarning("[ObeliskWarpMapApplier] " + from + " / " + direction + " pointe vers une room inexistante : " + target);
        return 1;
    }

    private void ApplyMap(Dictionary<string, Connection> map, string label)
    {
        if (!ResolveBackgroundManager())
            return;

        IList rooms = GetRoomsList();

        if (rooms == null)
        {
            Debug.LogError("[ObeliskWarpMapApplier] Impossible de lire la liste privée 'rooms'.");
            return;
        }

        HashSet<string> existingRoomIds = GetRoomIds(rooms);
        List<string> missingRoomIds = new List<string>();

        foreach (string id in map.Keys)
        {
            if (!existingRoomIds.Contains(id))
                missingRoomIds.Add(id);
        }

        if (missingRoomIds.Count > 0)
        {
            Debug.LogWarning("[ObeliskWarpMapApplier] Rooms manquantes pour appliquer '" + label + "' : " + string.Join(", ", missingRoomIds));

            if (abortIfAnyRoomIsMissing)
            {
                Debug.LogError("[ObeliskWarpMapApplier] Application annulée. Désactive 'Abort If Any Room Is Missing' si tu veux forcer.");
                return;
            }
        }

#if UNITY_EDITOR
        Undo.RecordObject(backgroundManager, "Apply Obelisk Warp Map " + label);
#endif

        int changed = 0;

        for (int i = 0; i < rooms.Count; i++)
        {
            object room = rooms[i];

            if (room == null)
                continue;

            string id = GetStringField(room, "id");

            if (string.IsNullOrEmpty(id))
                continue;

            if (!map.TryGetValue(id, out Connection connection))
            {
                if (!leaveUnknownRoomsUntouched)
                {
                    SetStringField(room, "haut", "");
                    SetStringField(room, "bas", "");
                    SetStringField(room, "gauche", "");
                    SetStringField(room, "droite", "");
                    changed++;
                }

                continue;
            }

            SetStringField(room, "haut", connection.haut);
            SetStringField(room, "bas", connection.bas);
            SetStringField(room, "gauche", connection.gauche);
            SetStringField(room, "droite", connection.droite);
            changed++;
        }

        if (setStartRoomToo)
            SetStartRoomId(startRoomId);

#if UNITY_EDITOR
        EditorUtility.SetDirty(backgroundManager);
        EditorSceneManager.MarkSceneDirty(backgroundManager.gameObject.scene);
#endif

        string modeWarning = Application.isPlaying
            ? "\nATTENTION : tu es en Play Mode. Les changements risquent de disparaître quand tu stop Play."
            : "";

        Debug.Log("[ObeliskWarpMapApplier] Map appliquée : " + label + " / rooms modifiées : " + changed + modeWarning);

        if (printMapAfterApply)
            PrintCurrentWarpMap();
    }

    // =========================================================================
    // V5 RECOMMANDÉE
    // Le hub ne force plus le joueur à cliquer Bas pour rejoindre le lac.
    // Deux chemins vers LAC_01 restent possibles :
    // 1) Chemin principal : PRA_01 -> gauche -> LAC_A1 -> gauche -> LAC_A2 -> gauche -> LAC_01
    // 2) Contournement :   LAC_A1 -> bas -> FOR_L1 -> gauche -> FOR_L2 -> haut -> LAC_01
    // Le détour du carré noir existe toujours, mais il appartient à la zone lac,
    // pas au hub. PRA_01 Bas est vide pour éviter la frustration.
    // =========================================================================
    private Dictionary<string, Connection> BuildV5NoHubDownSpamMap()
    {
        Dictionary<string, Connection> map = new Dictionary<string, Connection>();

        Add(map, new Connection("Ob_02", "", "Ob_01", "", ""));
        Add(map, new Connection("Ob_01", "Ob_02", "PRA_01", "", ""));

        // Hub ultra lisible :
        // Haut = obélisque
        // Gauche = lac / silence
        // Droite = château
        // Bas = rien, pour éviter le spam Bas depuis le hub
        Add(map, new Connection("PRA_01", "Ob_01", "", "LAC_A1", "FOR_01"));

        // Branche lac principale.
        // Depuis LAC_A1, le joueur peut continuer à gauche OU descendre vers le contournement.
        Add(map, new Connection("LAC_A1", "", "FOR_L1", "LAC_A2", "PRA_01"));
        Add(map, new Connection("LAC_A2", "", "", "LAC_01", "LAC_A1"));

        // LAC_01 reste le noeud du lac :
        // Droite = retour chemin principal
        // Bas = retour/entrée du contournement
        // Gauche = silence
        Add(map, new Connection("LAC_01", "", "FOR_L2", "SIL_01", "LAC_A2"));

        // Silence : gauche pour avancer, droite pour revenir.
        Add(map, new Connection("SIL_01", "", "", "SIL_02", "LAC_01"));
        Add(map, new Connection("SIL_02", "", "", "SIL_03", "SIL_01"));
        Add(map, new Connection("SIL_03", "", "", "", "SIL_02"));

        // Contournement du carré noir :
        // on descend depuis LAC_A1, on longe par la gauche, puis on remonte dans LAC_01.
        Add(map, new Connection("FOR_L1", "LAC_A1", "", "FOR_L2", ""));
        Add(map, new Connection("FOR_L2", "LAC_01", "", "", "FOR_L1"));

        // Château : droite pour avancer, gauche pour revenir.
        Add(map, new Connection("FOR_01", "", "", "PRA_01", "FOR_02"));
        Add(map, new Connection("FOR_02", "", "", "FOR_01", "CHA_FAR"));
        Add(map, new Connection("CHA_FAR", "", "", "FOR_02", "CHA_NEAR"));
        Add(map, new Connection("CHA_NEAR", "", "", "CHA_FAR", "CHA_INT_01"));
        Add(map, new Connection("CHA_INT_01", "", "", "CHA_NEAR", "CHA_INT_02"));
        Add(map, new Connection("CHA_INT_02", "", "", "CHA_INT_01", ""));

        return map;
    }

    private Dictionary<string, Connection> BuildV4BlackSquareBypassMap()
    {
        Dictionary<string, Connection> map = new Dictionary<string, Connection>();

        Add(map, new Connection("Ob_02", "", "Ob_01", "", ""));
        Add(map, new Connection("Ob_01", "Ob_02", "PRA_01", "", ""));
        Add(map, new Connection("PRA_01", "Ob_01", "FOR_L1", "LAC_A1", "FOR_01"));

        Add(map, new Connection("LAC_A1", "", "", "LAC_A2", "PRA_01"));
        Add(map, new Connection("LAC_A2", "", "", "LAC_01", "LAC_A1"));
        Add(map, new Connection("LAC_01", "FOR_L2", "", "SIL_01", "LAC_A2"));

        Add(map, new Connection("SIL_01", "", "", "SIL_02", "LAC_01"));
        Add(map, new Connection("SIL_02", "", "", "SIL_03", "SIL_01"));
        Add(map, new Connection("SIL_03", "", "", "", "SIL_02"));

        // Détour / contournement du carré noir : descente continue jusqu'au lac.
        Add(map, new Connection("FOR_L1", "PRA_01", "FOR_L2", "", ""));
        Add(map, new Connection("FOR_L2", "FOR_L1", "LAC_01", "", ""));

        Add(map, new Connection("FOR_01", "", "", "PRA_01", "FOR_02"));
        Add(map, new Connection("FOR_02", "", "", "FOR_01", "CHA_FAR"));
        Add(map, new Connection("CHA_FAR", "", "", "FOR_02", "CHA_NEAR"));
        Add(map, new Connection("CHA_NEAR", "", "", "CHA_FAR", "CHA_INT_01"));
        Add(map, new Connection("CHA_INT_01", "", "", "CHA_NEAR", "CHA_INT_02"));
        Add(map, new Connection("CHA_INT_02", "", "", "CHA_INT_01", ""));

        return map;
    }

    private Dictionary<string, Connection> BuildV3SimpleNoLakeBypassMap()
    {
        Dictionary<string, Connection> map = new Dictionary<string, Connection>();

        Add(map, new Connection("Ob_02", "", "Ob_01", "", ""));
        Add(map, new Connection("Ob_01", "Ob_02", "PRA_01", "", ""));
        Add(map, new Connection("PRA_01", "Ob_01", "FOR_L1", "LAC_A1", "FOR_01"));

        Add(map, new Connection("LAC_A1", "", "", "LAC_A2", "PRA_01"));
        Add(map, new Connection("LAC_A2", "", "", "LAC_01", "LAC_A1"));
        Add(map, new Connection("LAC_01", "", "", "SIL_01", "LAC_A2"));

        Add(map, new Connection("SIL_01", "", "", "SIL_02", "LAC_01"));
        Add(map, new Connection("SIL_02", "", "", "SIL_03", "SIL_01"));
        Add(map, new Connection("SIL_03", "", "", "", "SIL_02"));

        Add(map, new Connection("FOR_L1", "PRA_01", "FOR_L2", "", ""));
        Add(map, new Connection("FOR_L2", "FOR_L1", "", "", ""));

        Add(map, new Connection("FOR_01", "", "", "PRA_01", "FOR_02"));
        Add(map, new Connection("FOR_02", "", "", "FOR_01", "CHA_FAR"));
        Add(map, new Connection("CHA_FAR", "", "", "FOR_02", "CHA_NEAR"));
        Add(map, new Connection("CHA_NEAR", "", "", "CHA_FAR", "CHA_INT_01"));
        Add(map, new Connection("CHA_INT_01", "", "", "CHA_NEAR", "CHA_INT_02"));
        Add(map, new Connection("CHA_INT_02", "", "", "CHA_INT_01", ""));

        return map;
    }

    private Dictionary<string, Connection> BuildV2LateralOldTwoPathsMap()
    {
        Dictionary<string, Connection> map = new Dictionary<string, Connection>();

        Add(map, new Connection("Ob_02", "", "Ob_01", "", ""));
        Add(map, new Connection("Ob_01", "Ob_02", "PRA_01", "", ""));
        Add(map, new Connection("PRA_01", "Ob_01", "FOR_L1", "LAC_A1", "FOR_01"));

        Add(map, new Connection("LAC_A1", "", "", "LAC_A2", "PRA_01"));
        Add(map, new Connection("LAC_A2", "", "", "LAC_01", "LAC_A1"));
        Add(map, new Connection("LAC_01", "FOR_L2", "", "SIL_01", "LAC_A2"));

        Add(map, new Connection("SIL_01", "", "", "SIL_02", "LAC_01"));
        Add(map, new Connection("SIL_02", "", "", "SIL_03", "SIL_01"));
        Add(map, new Connection("SIL_03", "", "", "", "SIL_02"));

        Add(map, new Connection("FOR_L1", "PRA_01", "", "", "FOR_L2"));
        Add(map, new Connection("FOR_L2", "", "LAC_01", "FOR_L1", ""));

        Add(map, new Connection("FOR_01", "", "", "PRA_01", "FOR_02"));
        Add(map, new Connection("FOR_02", "", "", "FOR_01", "CHA_FAR"));
        Add(map, new Connection("CHA_FAR", "", "", "FOR_02", "CHA_NEAR"));
        Add(map, new Connection("CHA_NEAR", "", "", "CHA_FAR", "CHA_INT_01"));
        Add(map, new Connection("CHA_INT_01", "", "", "CHA_NEAR", "CHA_INT_02"));
        Add(map, new Connection("CHA_INT_02", "", "", "CHA_INT_01", ""));

        return map;
    }

    private Dictionary<string, Connection> BuildV1OriginalExportedMap()
    {
        Dictionary<string, Connection> map = new Dictionary<string, Connection>();

        Add(map, new Connection("Ob_02", "", "Ob_01", "", ""));
        Add(map, new Connection("Ob_01", "Ob_02", "PRA_01", "", ""));
        Add(map, new Connection("PRA_01", "Ob_01", "FOR_01", "LAC_A1", "FOR_L1"));

        Add(map, new Connection("LAC_A1", "", "", "LAC_A2", "PRA_01"));
        Add(map, new Connection("LAC_A2", "", "", "LAC_01", "LAC_A1"));
        Add(map, new Connection("FOR_L1", "", "", "PRA_01", "FOR_L2"));
        Add(map, new Connection("FOR_L2", "", "LAC_01", "FOR_L1", ""));

        Add(map, new Connection("LAC_01", "FOR_L2", "SIL_01", "", "LAC_A2"));
        Add(map, new Connection("SIL_01", "LAC_01", "SIL_02", "", ""));
        Add(map, new Connection("SIL_02", "SIL_01", "SIL_03", "", ""));
        Add(map, new Connection("SIL_03", "SIL_02", "", "", ""));

        Add(map, new Connection("FOR_01", "PRA_01", "FOR_02", "", ""));
        Add(map, new Connection("FOR_02", "FOR_01", "CHA_FAR", "", ""));
        Add(map, new Connection("CHA_FAR", "FOR_02", "CHA_NEAR", "", ""));
        Add(map, new Connection("CHA_NEAR", "CHA_FAR", "CHA_INT_01", "", ""));
        Add(map, new Connection("CHA_INT_01", "CHA_NEAR", "CHA_INT_02", "", ""));
        Add(map, new Connection("CHA_INT_02", "CHA_INT_01", "", "", ""));

        return map;
    }

    private bool ResolveBackgroundManager()
    {
        if (backgroundManager == null)
            backgroundManager = FindAnyObjectByType<BackgroundManager>();

        if (backgroundManager == null)
        {
            Debug.LogError("[ObeliskWarpMapApplier] Aucun BackgroundManager trouvé.");
            return false;
        }

        return true;
    }

    private IList GetRoomsList()
    {
        FieldInfo roomsField = typeof(BackgroundManager).GetField(
            "rooms",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        if (roomsField == null)
            return null;

        return roomsField.GetValue(backgroundManager) as IList;
    }

    private void SetStartRoomId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        FieldInfo startField = typeof(BackgroundManager).GetField(
            "startRoomId",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        if (startField == null)
        {
            Debug.LogWarning("[ObeliskWarpMapApplier] Champ privé startRoomId introuvable.");
            return;
        }

        startField.SetValue(backgroundManager, id);
    }

    private HashSet<string> GetRoomIds(IList rooms)
    {
        HashSet<string> ids = new HashSet<string>();

        for (int i = 0; i < rooms.Count; i++)
        {
            object room = rooms[i];

            if (room == null)
                continue;

            string id = GetStringField(room, "id");

            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }

        return ids;
    }

    private string BuildCurrentTextReport(IList rooms)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine("===== OBELISK — WARP MAP ACTUELLE =====");
        builder.AppendLine("");

        for (int i = 0; i < rooms.Count; i++)
        {
            object room = rooms[i];

            if (room == null)
                continue;

            string id = GetStringField(room, "id");

            if (string.IsNullOrEmpty(id))
                continue;

            builder.AppendLine(id);
            AppendDirection(builder, "  Haut  ", GetStringField(room, "haut"));
            AppendDirection(builder, "  Bas   ", GetStringField(room, "bas"));
            AppendDirection(builder, "  Gauche", GetStringField(room, "gauche"));
            AppendDirection(builder, "  Droite", GetStringField(room, "droite"));
            builder.AppendLine("");
        }

        return builder.ToString();
    }

    private void AppendDirection(StringBuilder builder, string label, string target)
    {
        if (string.IsNullOrEmpty(target))
            return;

        builder.AppendLine(label + " -> " + target);
    }

    private string BuildCurrentMermaidGraph(IList rooms)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine("```mermaid");
        builder.AppendLine("flowchart LR");
        builder.AppendLine("    %% OBELISK — carte des warps exportée depuis BackgroundManager");
        builder.AppendLine("");

        if (!string.IsNullOrEmpty(startRoomId))
            builder.AppendLine("    START((START)) --> " + SafeNode(startRoomId) + "[" + startRoomId + "]");

        builder.AppendLine("");

        for (int i = 0; i < rooms.Count; i++)
        {
            object room = rooms[i];

            if (room == null)
                continue;

            string id = GetStringField(room, "id");

            if (string.IsNullOrEmpty(id))
                continue;

            AppendMermaid(builder, id, "haut", GetStringField(room, "haut"));
            AppendMermaid(builder, id, "bas", GetStringField(room, "bas"));
            AppendMermaid(builder, id, "gauche", GetStringField(room, "gauche"));
            AppendMermaid(builder, id, "droite", GetStringField(room, "droite"));
        }

        builder.AppendLine("```");

        return builder.ToString();
    }

    private void AppendMermaid(StringBuilder builder, string from, string direction, string to)
    {
        if (string.IsNullOrEmpty(to))
            return;

        builder.AppendLine("    " + SafeNode(from) + "[" + from + "] -- " + direction + " --> " + SafeNode(to) + "[" + to + "]");
    }

    private string SafeNode(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "EMPTY";

        return id
            .Replace("-", "_")
            .Replace(" ", "_")
            .Replace(".", "_")
            .Replace("/", "_");
    }

    private string GetStringField(object target, string fieldName)
    {
        if (target == null)
            return "";

        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (field == null)
            return "";

        object value = field.GetValue(target);

        if (value == null)
            return "";

        return value.ToString();
    }

    private void SetStringField(object target, string fieldName, string value)
    {
        if (target == null)
            return;

        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (field == null)
        {
            Debug.LogWarning("[ObeliskWarpMapApplier] Champ introuvable : " + fieldName);
            return;
        }

        field.SetValue(target, value);
    }

    private void Add(Dictionary<string, Connection> map, Connection connection)
    {
        map[connection.id] = connection;
    }
}
