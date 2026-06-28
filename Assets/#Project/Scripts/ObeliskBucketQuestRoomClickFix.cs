using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Fix simple et robuste pour débloquer la quête du seau.
/// 
/// À mettre sur le même GameObject que ObeliskBucketQuestSystem.
/// 
/// Il n'utilise PAS directement UnityEngine.InputSystem.Mouse dans le code,
/// donc il évite l'erreur "Mouse.current n'existe pas".
/// Il lit le nouveau Input System par réflexion.
/// 
/// Fonctionnement :
/// - Dans la room pickupRoomId : un clic gauche ramasse le seau.
/// - Dans la room lakeRoomId : un clic gauche remplit le seau.
/// - Dans la room obeliskRoomId : un clic gauche donne l'eau à l'obélisque.
/// 
/// C'est volontairement large : ça clique dans toute la room, pas seulement sur le petit rectangle.
/// But : débloquer la quête maintenant.
/// </summary>
[DisallowMultipleComponent]
public class ObeliskBucketQuestRoomClickFix : MonoBehaviour
{
    [Header("Référence")]
    public ObeliskBucketQuestSystem system;

    [Header("Options")]
    public bool debugLogs = true;

    [Tooltip("Si ON, le script agit dans toute la room correspondante. Recommandé pour débloquer.")]
    public bool clickAnywhereInCorrectRoom = true;

    private BackgroundManager backgroundManager;

    private Type inputMouseType;
    private PropertyInfo mouseCurrentProperty;
    private PropertyInfo mouseLeftButtonProperty;
    private PropertyInfo buttonWasPressedThisFrameProperty;

    private void Reset()
    {
        system = GetComponent<ObeliskBucketQuestSystem>();
    }

    private void Awake()
    {
        CacheReferences();
        CacheInputSystemReflection();
    }

    private void Start()
    {
        CacheReferences();

        if (debugLogs)
            Debug.Log("[BucketQuestRoomClickFix] Chargé. Clic room actif.");
    }

    private void Update()
    {
        if (system == null)
            system = GetComponent<ObeliskBucketQuestSystem>();

        if (system == null)
            return;

        if (!WasLeftMousePressedThisFrame())
            return;

        string room = GetCurrentRoomId();

        if (debugLogs)
            Debug.Log("[BucketQuestRoomClickFix] Clic détecté en room = " + room);

        if (!clickAnywhereInCorrectRoom)
            return;

        if (room == system.pickupRoomId)
        {
            if (debugLogs)
                Debug.Log("[BucketQuestRoomClickFix] Tentative PickupBucket.");

            system.PickupBucket();
            return;
        }

        if (room == system.lakeRoomId)
        {
            if (debugLogs)
                Debug.Log("[BucketQuestRoomClickFix] Tentative FillBucketAtLake.");

            system.FillBucketAtLake();
            return;
        }

        if (room == system.obeliskRoomId)
        {
            if (debugLogs)
                Debug.Log("[BucketQuestRoomClickFix] Tentative FeedObelisk.");

            system.FeedObelisk();
            return;
        }
    }

    private void CacheReferences()
    {
        if (system == null)
            system = GetComponent<ObeliskBucketQuestSystem>();

        if (backgroundManager == null)
            backgroundManager = FindAnyObjectByType<BackgroundManager>();
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
                Debug.LogWarning("[BucketQuestRoomClickFix] Type UnityEngine.InputSystem.Mouse introuvable. Le clic Input System ne sera pas lu.");

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

    private bool WasLeftMousePressedThisFrame()
    {
        // Nouveau Input System par réflexion : pas de dépendance directe à Mouse.current.
        try
        {
            if (inputMouseType == null || mouseCurrentProperty == null || mouseLeftButtonProperty == null)
                CacheInputSystemReflection();

            if (inputMouseType != null && mouseCurrentProperty != null && mouseLeftButtonProperty != null)
            {
                object mouse = mouseCurrentProperty.GetValue(null, null);

                if (mouse != null)
                {
                    object leftButton = mouseLeftButtonProperty.GetValue(mouse, null);

                    if (leftButton != null)
                    {
                        if (buttonWasPressedThisFrameProperty == null)
                        {
                            buttonWasPressedThisFrameProperty = leftButton.GetType().GetProperty(
                                "wasPressedThisFrame",
                                BindingFlags.Public | BindingFlags.Instance
                            );
                        }

                        if (buttonWasPressedThisFrameProperty != null)
                        {
                            object value = buttonWasPressedThisFrameProperty.GetValue(leftButton, null);

                            if (value is bool pressed && pressed)
                                return true;
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            if (debugLogs)
                Debug.LogWarning("[BucketQuestRoomClickFix] Lecture Input System impossible : " + e.Message);
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        // Ancien Input seulement si Unity l'autorise.
        try
        {
            if (Input.GetMouseButtonDown(0))
                return true;
        }
        catch
        {
            // Ignore volontairement.
        }
#endif

        return false;
    }
}
