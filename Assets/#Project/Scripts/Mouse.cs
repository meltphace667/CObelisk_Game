using UnityEngine;

/// <summary>
/// Gère l'affichage et le comportement du curseur de la souris dans le jeu.
/// Attacher ce script à un GameObject dédié (ex: "MouseManager") dans la scène.
/// </summary>
public class Mouse : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // Inspecteur
    // ─────────────────────────────────────────────

    [Header("Curseur personnalisé")]
    [Tooltip("Texture utilisée comme curseur (facultatif). Si vide, le curseur système est conservé.")]
    [SerializeField] private Texture2D cursorTexture;

    [Tooltip("Point chaud du curseur (0,0 = coin supérieur gauche de la texture).")]
    [SerializeField] private Vector2 hotspot = Vector2.zero;

    [Header("Visibilité")]
    [Tooltip("Le curseur est-il visible au démarrage ?")]
    [SerializeField] private bool visibleOnStart = true;

    [Tooltip("Confiner le curseur à la fenêtre du jeu ?")]
    [SerializeField] private bool confineCursor = false;

    // ─────────────────────────────────────────────
    // Propriétés publiques
    // ─────────────────────────────────────────────

    /// <summary>Position du curseur dans l'espace écran (pixels).</summary>
    public Vector2 ScreenPosition => Input.mousePosition;

    /// <summary>Position du curseur dans l'espace monde 2D.</summary>
    public Vector2 WorldPosition => _camera != null
        ? (Vector2)_camera.ScreenToWorldPoint(Input.mousePosition)
        : Vector2.zero;

    /// <summary>Le curseur est-il actuellement visible ?</summary>
    public bool IsVisible => Cursor.visible;

    // ─────────────────────────────────────────────
    // Privé
    // ─────────────────────────────────────────────

    private Camera _camera;

    // ─────────────────────────────────────────────
    // Singleton léger (optionnel)
    // ─────────────────────────────────────────────

    public static Mouse Instance { get; private set; }

    // ─────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        // Singleton : une seule instance en scène
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Persiste entre les scènes

        // Caméra principale
        _camera = Camera.main;
    }

    private void Start()
    {
        ApplyCursorSettings();
    }

    private void Update()
    {
        // Mise à jour de la caméra si elle change de scène
        if (_camera == null)
            _camera = Camera.main;
    }

    // ─────────────────────────────────────────────
    // Méthodes publiques
    // ─────────────────────────────────────────────

    /// <summary>Affiche le curseur.</summary>
    public void Show()
    {
        Cursor.visible = true;
    }

    /// <summary>Masque le curseur.</summary>
    public void Hide()
    {
        Cursor.visible = false;
    }

    /// <summary>Bascule la visibilité du curseur.</summary>
    public void Toggle()
    {
        Cursor.visible = !Cursor.visible;
    }

    /// <summary>
    /// Change la texture du curseur à la volée.
    /// Passer null pour revenir au curseur système par défaut.
    /// </summary>
    public void SetCursorTexture(Texture2D texture, Vector2? newHotspot = null)
    {
        cursorTexture = texture;
        if (newHotspot.HasValue)
            hotspot = newHotspot.Value;

        Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);
    }

    // ─────────────────────────────────────────────
    // Privé
    // ─────────────────────────────────────────────

    private void ApplyCursorSettings()
    {
        // Visibilité initiale
        Cursor.visible = visibleOnStart;

        // Mode de confinement
        Cursor.lockState = confineCursor
            ? CursorLockMode.Confined
            : CursorLockMode.None;

        // Texture personnalisée (si assignée dans l'inspecteur)
        if (cursorTexture != null)
            Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);
    }
}