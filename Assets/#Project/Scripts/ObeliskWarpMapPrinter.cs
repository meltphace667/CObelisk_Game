using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

public class ObeliskWarpMapPrinter : MonoBehaviour
{
    [Header("Référence")]
    [SerializeField] private BackgroundManager backgroundManager;

    [Header("Options")]
    [SerializeField] private bool copyMermaidToClipboard = true;
    [SerializeField] private bool includeEmptyDirections = false;
    [SerializeField] private bool includeStartMarker = true;

    [Tooltip("À renseigner si tu veux marquer visuellement le start dans le graphe. Exemple : PRA_01")]
    [SerializeField] private string startRoomIdHint = "PRA_01";

    [ContextMenu("OBELISK / Print Warp Map")]
    public void PrintWarpMap()
    {
        if (backgroundManager == null)
            backgroundManager = FindAnyObjectByType<BackgroundManager>();

        if (backgroundManager == null)
        {
            Debug.LogError("[ObeliskWarpMapPrinter] Aucun BackgroundManager trouvé.");
            return;
        }

        IList rooms = GetRoomsList(backgroundManager);

        if (rooms == null)
        {
            Debug.LogError("[ObeliskWarpMapPrinter] Impossible de lire la liste privée 'rooms' du BackgroundManager.");
            return;
        }

        string report = BuildTextReport(rooms);
        string mermaid = BuildMermaidGraph(rooms);

        Debug.Log(report);
        Debug.Log(mermaid);

        if (copyMermaidToClipboard)
        {
            GUIUtility.systemCopyBuffer = mermaid;
            Debug.Log("[ObeliskWarpMapPrinter] Graphe Mermaid copié dans le presse-papiers.");
        }
    }

    private IList GetRoomsList(BackgroundManager manager)
    {
        FieldInfo roomsField = typeof(BackgroundManager).GetField(
            "rooms",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        if (roomsField == null)
            return null;

        return roomsField.GetValue(manager) as IList;
    }

    private string BuildTextReport(IList rooms)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine("===== OBELISK — WARP MAP EXACTE =====");
        builder.AppendLine("Cette liste vient directement des champs Haut / Bas / Gauche / Droite du BackgroundManager.");
        builder.AppendLine("");

        for (int i = 0; i < rooms.Count; i++)
        {
            object room = rooms[i];

            if (room == null)
                continue;

            string id = GetStringField(room, "id");
            string haut = GetStringField(room, "haut");
            string bas = GetStringField(room, "bas");
            string gauche = GetStringField(room, "gauche");
            string droite = GetStringField(room, "droite");

            if (string.IsNullOrEmpty(id))
                continue;

            builder.AppendLine(id);

            AppendDirection(builder, "  Haut  ", haut);
            AppendDirection(builder, "  Bas   ", bas);
            AppendDirection(builder, "  Gauche", gauche);
            AppendDirection(builder, "  Droite", droite);

            builder.AppendLine("");
        }

        return builder.ToString();
    }

    private void AppendDirection(StringBuilder builder, string label, string target)
    {
        if (string.IsNullOrEmpty(target))
        {
            if (includeEmptyDirections)
                builder.AppendLine(label + " -> rien");

            return;
        }

        builder.AppendLine(label + " -> " + target);
    }

    private string BuildMermaidGraph(IList rooms)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine("```mermaid");
        builder.AppendLine("flowchart LR");
        builder.AppendLine("    %% OBELISK — carte exacte des warps exportée depuis BackgroundManager");
        builder.AppendLine("");

        if (includeStartMarker && !string.IsNullOrEmpty(startRoomIdHint))
        {
            string safeStart = SafeNode(startRoomIdHint);
            builder.AppendLine("    START((START)) --> " + safeStart + "[" + startRoomIdHint + "]");
            builder.AppendLine("");
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            object room = rooms[i];

            if (room == null)
                continue;

            string id = GetStringField(room, "id");

            if (string.IsNullOrEmpty(id))
                continue;

            AppendMermaidConnection(builder, id, "haut", GetStringField(room, "haut"));
            AppendMermaidConnection(builder, id, "bas", GetStringField(room, "bas"));
            AppendMermaidConnection(builder, id, "gauche", GetStringField(room, "gauche"));
            AppendMermaidConnection(builder, id, "droite", GetStringField(room, "droite"));
        }

        builder.AppendLine("");
        builder.AppendLine("    classDef obelisk fill:#3b3018,stroke:#d4aa3a,color:#fff;");
        builder.AppendLine("    classDef lake fill:#123d3f,stroke:#48b8b0,color:#fff;");
        builder.AppendLine("    classDef silence fill:#303030,stroke:#b9b9b9,color:#fff;");
        builder.AppendLine("    classDef forest fill:#18351f,stroke:#7aaa6a,color:#fff;");
        builder.AppendLine("    classDef chateau fill:#3d1e18,stroke:#d26b55,color:#fff;");
        builder.AppendLine("");
        builder.AppendLine("    class Ob_01,Ob_02 obelisk;");
        builder.AppendLine("    class LAC_A1,LAC_A2,LAC_01 lake;");
        builder.AppendLine("    class SIL_01,SIL_02,SIL_03 silence;");
        builder.AppendLine("    class FOR_L1,FOR_L2,FOR_01,FOR_02 forest;");
        builder.AppendLine("    class CHA_FAR,CHA_NEAR,CHA_INT_01,CHA_INT_02 chateau;");
        builder.AppendLine("```");

        return builder.ToString();
    }

    private void AppendMermaidConnection(StringBuilder builder, string from, string direction, string to)
    {
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
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
}
