using UnityEngine;
using UnityEngine.UI;

// Important : ton projet a déjà un script Mouse.cs.
// Donc on évite le conflit avec UnityEngine.InputSystem.Mouse.
using InputMouse = UnityEngine.InputSystem.Mouse;

[RequireComponent(typeof(Image))]
public class DirectionClickZone : MonoBehaviour
{
    [Header("Direction de cette zone")]
    [SerializeField] private BackgroundManager.Direction direction;

    [Header("Debug visuel")]
    [SerializeField] private bool showZoneWhileTesting = true;

    private RectTransform rectTransform;
    private Image image;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();

        // On ne dépend pas du raycast UI.
        image.raycastTarget = false;

        ApplyDebugVisual();

        Debug.Log($"[DirectionZone] Awake '{gameObject.name}' → {direction}");
    }

    private void OnValidate()
    {
        if (image == null)
            image = GetComponent<Image>();

        if (image != null)
            ApplyDebugVisual();
    }
private void ApplyDebugVisual()
{
    if (showZoneWhileTesting)
    {
        image.enabled = true;
        image.color = new Color(1f, 1f, 0f, 0.22f);
    }
    else
    {
        image.color = new Color(1f, 1f, 1f, 0f);
        image.enabled = false;
    }
}
    private void Update()
    {
        if (InputMouse.current == null)
            return;

        // Bloque les clics pendant le tout premier fondu et pendant les transitions.
        if (ScreenFader.Instance != null && !ScreenFader.Instance.CanPlayerInteract)
            return;

        if (BackgroundManager.Instance != null && BackgroundManager.Instance.IsChangingRoom)
            return;

        if (!InputMouse.current.leftButton.wasPressedThisFrame)
            return;

        Vector2 mousePosition = InputMouse.current.position.ReadValue();

        bool inside = RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform,
            mousePosition,
            null
        );

        if (!inside)
            return;

        if (BackgroundManager.Instance == null)
        {
            Debug.LogError("[DirectionZone] BackgroundManager.Instance est NULL");
            return;
        }

        Debug.Log($"[DirectionZone] Clic sur '{gameObject.name}' → {direction}");
        BackgroundManager.Instance.TryMove(direction);
    }
}