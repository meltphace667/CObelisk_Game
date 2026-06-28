using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// V3 robuste du fix de clic room pour la quête du seau.
/// 
/// À mettre sur BucketQuestSystem.
/// 
/// IMPORTANT :
/// - Cette V3 désactive automatiquement l'ancien composant ObeliskBucketQuestRoomClickFix
///   s'il est encore présent sur la scène.
/// - Elle ignore les clics sur les zones de déplacement.
/// - Elle utilise Ob_02 comme room d'offrande par défaut.
/// 
/// Fichier : ObeliskBucketQuestRoomClickFixV3.cs
/// Classe : ObeliskBucketQuestRoomClickFixV3
/// </summary>
[DisallowMultipleComponent]
public class ObeliskBucketQuestRoomClickFixV3 : MonoBehaviour
{
    [Header("Référence quête")]
    public ObeliskBucketQuestSystem system;

    [Header("Rooms")]
    [Tooltip("Force la room d'offrande à Ob_02. Recommandé, car Ob_01 sert surtout de passage vers Ob_02.")]
    public bool overrideObeliskRoomId = true;

    public string obeliskRoomIdOverride = "Ob_02";

    [Header("Options")]
    public bool debugLogs = true;

    [Tooltip("Ignore les clics sur Zone_Haut / Zone_Bas / Zone_Gauche / Zone_Droite.")]
    public bool ignoreDirectionZones = true;

    [Tooltip("Sécurité en plus : ignore les clics sur les bords de l'écran, même si les RectTransform des zones ne sont pas trouvés.")]
    public bool ignoreScreenEdges = true;

    [Range(0.05f, 0.4f)]
    public float edgeMarginPercent = 0.18f;

    [Tooltip("Évite que plusieurs actions se déclenchent trop vite.")]
    public float actionCooldown = 0.25f;

    [Header("Zones directionnelles")]
    public RectTransform zoneHaut;
    public RectTransform zoneBas;
    public RectTransform zoneGauche;
    public RectTransform zoneDroite;

    private BackgroundManager backgroundManager;
    private Canvas canvas;
    private Camera uiCamera;

    private Type inputMouseType;
    private PropertyInfo mouseCurrentProperty;
    private PropertyInfo mouseLeftButtonProperty;
    private PropertyInfo mousePositionProperty;
    private PropertyInfo buttonWasPressedThisFrameProperty;
    private MethodInfo positionReadValueMethod;

    private float lastActionTime = -999f;

    private void Reset()
    {
        system = GetComponent<ObeliskBucketQuestSystem>();
        AutoFindDirectionZones();
    }

    private void Awake()
    {
        DisableOldRoomFixes();
        CacheReferences();
        CacheInputSystemReflection();
    }

    private void Start()
    {
        DisableOldRoomFixes();
        CacheReferences();

        if (debugLogs)
            Debug.Log("[BucketQuestRoomClickFixV3] ACTIVE. ObeliskRoom=" + GetObeliskRoomId());
    }

    private void Update()
    {
        if (system == null)
            system = GetComponent<ObeliskBucketQuestSystem>();

        if (system == null)
            return;

        Vector2 mousePos;
        if (!WasLeftMousePressedThisFrame(out mousePos))
            return;

        string room = GetCurrentRoomId();

        if (debugLogs)
            Debug.Log("[BucketQuestRoomClickFixV3] Clic room=" + room + " mouse=" + mousePos);

        if (ignoreDirectionZones && IsDirectionClick(mousePos))
        {
            if (debugLogs)
                Debug.Log("[BucketQuestRoomClickFixV3] Clic ignoré : zone de déplacement.");

            return;
        }

        if (Time.unscaledTime - lastActionTime < actionCooldown)
        {
            if (debugLogs)
                Debug.Log("[BucketQuestRoomClickFixV3] Clic ignoré : cooldown.");

            return;
        }

        if (room == system.pickupRoomId)
        {
            lastActionTime = Time.unscaledTime;

            if (debugLogs)
                Debug.Log("[BucketQuestRoomClickFixV3] PickupBucket.");

            system.PickupBucket();
            return;
        }

        if (room == system.lakeRoomId)
        {
            lastActionTime = Time.unscaledTime;

            if (debugLogs)
                Debug.Log("[BucketQuestRoomClickFixV3] FillBucketAtLake.");

            system.FillBucketAtLake();
            return;
        }

        if (room == GetObeliskRoomId())
        {
            lastActionTime = Time.unscaledTime;

            if (debugLogs)
                Debug.Log("[BucketQuestRoomClickFixV3] FeedObelisk.");

            system.FeedObelisk();
            return;
        }
    }

    private string GetObeliskRoomId()
    {
        if (overrideObeliskRoomId && !string.IsNullOrWhiteSpace(obeliskRoomIdOverride))
            return obeliskRoomIdOverride;

        return system != null ? system.obeliskRoomId : "";
    }

    private void DisableOldRoomFixes()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null)
                continue;

            if (behaviour == this)
                continue;

            Type type = behaviour.GetType();

            if (type == null)
                continue;

            if (type.Name == "ObeliskBucketQuestRoomClickFix")
            {
                behaviour.enabled = false;

                if (debugLogs)
                    Debug.Log("[BucketQuestRoomClickFixV3] Ancien ObeliskBucketQuestRoomClickFix désactivé sur " + behaviour.gameObject.name);
            }
        }
    }

    private void CacheReferences()
    {
        if (system == null)
            system = GetComponent<ObeliskBucketQuestSystem>();

        if (backgroundManager == null)
            backgroundManager = FindAnyObjectByType<BackgroundManager>();

        if (zoneHaut == null || zoneBas == null || zoneGauche == null || zoneDroite == null)
            AutoFindDirectionZones();

        if (canvas == null)
        {
            if (zoneHaut != null)
                canvas = zoneHaut.GetComponentInParent<Canvas>();

            if (canvas == null)
                canvas = FindAnyObjectByType<Canvas>();
        }

        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                uiCamera = null;
            else
                uiCamera = canvas.worldCamera;
        }
    }

    private void AutoFindDirectionZones()
    {
        GameObject haut = GameObject.Find("Zone_Haut");
        GameObject bas = GameObject.Find("Zone_Bas");
        GameObject gauche = GameObject.Find("Zone_Gauche");
        GameObject droite = GameObject.Find("Zone_Droite");

        if (haut != null)
            zoneHaut = haut.GetComponent<RectTransform>();

        if (bas != null)
            zoneBas = bas.GetComponent<RectTransform>();

        if (gauche != null)
            zoneGauche = gauche.GetComponent<RectTransform>();

        if (droite != null)
            zoneDroite = droite.GetComponent<RectTransform>();
    }

    private bool IsDirectionClick(Vector2 mousePos)
    {
        CacheReferences();

        if (IsInside(zoneHaut, mousePos))
            return true;

        if (IsInside(zoneBas, mousePos))
            return true;

        if (IsInside(zoneGauche, mousePos))
            return true;

        if (IsInside(zoneDroite, mousePos))
            return true;

        if (ignoreScreenEdges)
        {
            float marginX = Screen.width * edgeMarginPercent;
            float marginY = Screen.height * edgeMarginPercent;

            if (mousePos.x <= marginX)
                return true;

            if (mousePos.x >= Screen.width - marginX)
                return true;

            if (mousePos.y <= marginY)
                return true;

            if (mousePos.y >= Screen.height - marginY)
                return true;
        }

        return false;
    }

    private bool IsInside(RectTransform zone, Vector2 mousePos)
    {
        if (zone == null)
            return false;

        if (!zone.gameObject.activeInHierarchy)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(zone, mousePos, uiCamera);
    }

    private string GetCurrentRoomId()
    {
        if (backgroundManager == null)
            backgroundManager = FindAnyObjectByType<BackgroundManager>();

        if (backgroundManager == null)
            return "";

        try
        {
            return backgroundManager.GetCurrentRoomId();
        }
        catch
        {
            MethodInfo method = typeof(BackgroundManager).GetMethod(
                "GetCurrentRoomId",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (method == null)
                return "";

            object value = method.Invoke(backgroundManager, null);
            return value != null ? value.ToString() : "";
        }
    }

    private void CacheInputSystemReflection()
    {
        inputMouseType = FindTypeInLoadedAssemblies("UnityEngine.InputSystem.Mouse");

        if (inputMouseType == null)
        {
            if (debugLogs)
                Debug.LogWarning("[BucketQuestRoomClickFixV3] UnityEngine.InputSystem.Mouse introuvable.");

            return;
        }

        mouseCurrentProperty = inputMouseType.GetProperty(
            "current",
            BindingFlags.Public | BindingFlags.Static
        );

        mouseLeftButtonProperty = inputMouseType.GetProperty(
            "leftButton",
            BindingFlags.Public | BindingFlags.Instance
        );

        mousePositionProperty = inputMouseType.GetProperty(
            "position",
            BindingFlags.Public | BindingFlags.Instance
        );
    }

    private Type FindTypeInLoadedAssemblies(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(fullName);
            if (type != null)
                return type;
        }

        return null;
    }

    private bool WasLeftMousePressedThisFrame(out Vector2 mousePos)
    {
        mousePos = Vector2.zero;

        try
        {
            if (inputMouseType == null || mouseCurrentProperty == null || mouseLeftButtonProperty == null || mousePositionProperty == null)
                CacheInputSystemReflection();

            if (inputMouseType == null || mouseCurrentProperty == null || mouseLeftButtonProperty == null)
                return false;

            object mouse = mouseCurrentProperty.GetValue(null, null);

            if (mouse == null)
                return false;

            object leftButton = mouseLeftButtonProperty.GetValue(mouse, null);

            if (leftButton == null)
                return false;

            if (buttonWasPressedThisFrameProperty == null)
            {
                buttonWasPressedThisFrameProperty = leftButton.GetType().GetProperty(
                    "wasPressedThisFrame",
                    BindingFlags.Public | BindingFlags.Instance
                );
            }

            if (buttonWasPressedThisFrameProperty == null)
                return false;

            object pressedValue = buttonWasPressedThisFrameProperty.GetValue(leftButton, null);

            bool pressed = pressedValue is bool b && b;

            if (!pressed)
                return false;

            mousePos = ReadMousePosition(mouse);
            return true;
        }
        catch (Exception e)
        {
            if (debugLogs)
                Debug.LogWarning("[BucketQuestRoomClickFixV3] Lecture Input System impossible : " + e.Message);

            return false;
        }
    }

    private Vector2 ReadMousePosition(object mouse)
    {
        try
        {
            if (mousePositionProperty == null)
                return Vector2.zero;

            object positionControl = mousePositionProperty.GetValue(mouse, null);

            if (positionControl == null)
                return Vector2.zero;

            if (positionReadValueMethod == null)
            {
                positionReadValueMethod = positionControl.GetType().GetMethod(
                    "ReadValue",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null
                );
            }

            if (positionReadValueMethod == null)
                return Vector2.zero;

            object value = positionReadValueMethod.Invoke(positionControl, null);

            if (value is Vector2 vector)
                return vector;
        }
        catch
        {
            // Ignore.
        }

        return Vector2.zero;
    }

#if UNITY_EDITOR
    [ContextMenu("OBELISK / Auto Fill Direction Zones")]
    private void EditorAutoFillDirectionZones()
    {
        AutoFindDirectionZones();
        CacheReferences();

        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log("[BucketQuestRoomClickFixV3] Auto Fill Direction Zones terminé.");
    }
#endif
}
