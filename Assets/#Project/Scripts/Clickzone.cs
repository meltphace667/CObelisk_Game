using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Image))]
public class ClickZone : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private string destinationId;

    [Header("Condition d'activation")]
    [SerializeField] private string visibleOnlyInRoom;

    [Header("Debug")]
    [SerializeField] private bool showZoneWhileTesting = true;
    [SerializeField] private bool spaceKeyAlsoChangesRoom = true;

    private RectTransform rectTransform;
    private Image image;

    private float nextAliveLogTime = 0f;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();

        image.raycastTarget = false;

        if (showZoneWhileTesting)
            image.color = new Color(1f, 1f, 0f, 0.35f); // jaune visible
        else
            image.color = new Color(1f, 1f, 1f, 0.01f);

        Debug.Log($"[CZ] Awake sur '{gameObject.name}'");
    }

    private void Start()
    {
        Debug.Log($"[CZ] Start sur '{gameObject.name}'");

        if (string.IsNullOrEmpty(destinationId))
            Debug.LogError($"[CZ] destinationId vide sur '{gameObject.name}'");
        else
            Debug.Log($"[CZ] '{gameObject.name}' → destination : '{destinationId}'");

        if (BackgroundManager.Instance == null)
            Debug.LogError("[CZ] BackgroundManager.Instance est NULL");
        else
            Debug.Log("[CZ] BackgroundManager OK");

        if (!string.IsNullOrEmpty(visibleOnlyInRoom))
            Debug.Log($"[CZ] Actif seulement dans la room : '{visibleOnlyInRoom}'");

        Debug.Log($"[CZ] Mouse.current au Start = {(UnityEngine.InputSystem.Mouse.current == null ? "NULL" : "OK")}");
    }

    private void Update()
    {
        // Log une fois par seconde pour vérifier que Update tourne vraiment.
        if (Time.time >= nextAliveLogTime)
        {
            nextAliveLogTime = Time.time + 1f;
            Debug.Log($"[CZ] Update vivant sur '{gameObject.name}' | Mouse = {(UnityEngine.InputSystem.Mouse.current == null ? "NULL" : "OK")}");
        }

        // Test clavier : si Espace marche, le problème vient seulement du clic souris.
        if (spaceKeyAlsoChangesRoom &&
            Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Debug.Log("[CZ] ESPACE détecté → test changement de room");
            TryChangeRoom();
            return;
        }

        if (UnityEngine.InputSystem.Mouse.current == null)
            return;

        if (!UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            return;

        Vector2 mousePosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        Debug.Log($"[CZ] Clic souris détecté. Position écran = {mousePosition}");

        if (!IsAllowedInCurrentRoom())
            return;

        bool inside = RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform,
            mousePosition,
            null
        );

        Debug.Log($"[CZ] Souris dans '{gameObject.name}' ? {inside}");

        if (!inside)
            return;

        TryChangeRoom();
    }

    private bool IsAllowedInCurrentRoom()
    {
        if (BackgroundManager.Instance == null)
        {
            Debug.LogError("[CZ] BackgroundManager.Instance est NULL");
            return false;
        }

        if (string.IsNullOrEmpty(visibleOnlyInRoom))
            return true;

        string currentRoom = BackgroundManager.Instance.GetCurrentRoomId();

        if (currentRoom != visibleOnlyInRoom)
        {
            Debug.Log($"[CZ] Clic ignoré : room actuelle '{currentRoom}', room requise '{visibleOnlyInRoom}'");
            return false;
        }

        return true;
    }

    private void TryChangeRoom()
    {
        if (BackgroundManager.Instance == null)
        {
            Debug.LogError("[CZ] Impossible de changer : BackgroundManager.Instance est NULL");
            return;
        }

        if (string.IsNullOrEmpty(destinationId))
        {
            Debug.LogError("[CZ] Impossible de changer : destinationId vide");
            return;
        }

if (BackgroundManager.Instance.IsChangingRoom)
    return;
    
        Debug.Log($"[CZ] ✅ Changement vers '{destinationId}'");
        BackgroundManager.Instance.ChangeTo(destinationId);
    }
}