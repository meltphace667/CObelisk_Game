using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Header("Début du jeu")]
    [Tooltip("Temps pendant lequel l'écran reste noir avant de révéler l'image.")]
    [SerializeField] private float delayBeforeFirstReveal = 1f;

    [Tooltip("Durée du fade d'ouverture.")]
    [SerializeField] private float firstFadeDuration = 8f;

    [Header("Transitions entre rooms")]
    [SerializeField] private float fadeToBlackDuration = 1.6f;
    [SerializeField] private float blackHoldDuration = 0.25f;
    [SerializeField] private float fadeFromBlackDuration = 2.6f;

    [Header("Courbe visuelle")]
    [Tooltip("1 = normal. Plus haut = départ plus lent. Conseillé : 1.15 à 1.35.")]
    [SerializeField] private float visualCurvePower = 1.25f;

    [Header("Sécurité UI")]
    [Tooltip("Force le FadeOverlay à rester au-dessus de tous les autres éléments UI, même si un autre script crée un overlay après lui.")]
    [SerializeField] private bool alwaysStayOnTop = true;

    [Tooltip("Si activé, le FadeOverlay se remet au-dessus à chaque frame. Utile avec les overlays visuels comme FastCloud.")]
    [SerializeField] private bool enforceTopEveryFrame = true;

    [Tooltip("Si activé, le fade bloque les clics pendant les transitions.")]
    [SerializeField] private bool blockRaycastsDuringFade = true;

    private Image fadeImage;
    private bool isFading = true;

    public bool IsFading => isFading;
    public bool CanPlayerInteract => !isFading;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        fadeImage = GetComponent<Image>();

        fadeImage.raycastTarget = true;

        // Le jeu commence noir.
        SetAlpha(1f);
        ForceOnTop();

        Debug.Log("[Fade] Awake OK");
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        if (fadeImage == null)
            fadeImage = GetComponent<Image>();

        ForceOnTop();
    }

    private void Start()
    {
        ForceOnTop();
        StartCoroutine(FirstFadeRoutine());
    }

    private void LateUpdate()
    {
        if (alwaysStayOnTop && enforceTopEveryFrame)
            ForceOnTop();
    }

    [ContextMenu("OBELISK / Force Fade Overlay On Top")]
    public void ForceOnTop()
    {
        if (!alwaysStayOnTop)
            return;

        if (transform.parent == null)
            return;

        int lastIndex = transform.parent.childCount - 1;

        if (transform.GetSiblingIndex() != lastIndex)
            transform.SetAsLastSibling();
    }

    private IEnumerator FirstFadeRoutine()
    {
        Debug.Log("[Fade] Écran noir au lancement, la musique peut déjà jouer.");

        isFading = true;
        SetRaycastBlocking(true);
        SetAlpha(1f);
        ForceOnTop();

        if (delayBeforeFirstReveal > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeFirstReveal);

        Debug.Log("[Fade] Début révélation de l'image.");

        yield return FadeAlpha(1f, 0f, firstFadeDuration);

        SetAlpha(0f);
        SetRaycastBlocking(false);
        isFading = false;
        ForceOnTop();

        Debug.Log("[Fade] Première apparition terminée.");
    }

    public IEnumerator FadeToBlackRoutine()
    {
        Debug.Log("[Fade] Début fade vers noir");

        isFading = true;
        SetRaycastBlocking(true);
        ForceOnTop();

        yield return FadeAlpha(GetAlpha(), 1f, fadeToBlackDuration);

        SetAlpha(1f);
        ForceOnTop();

        Debug.Log("[Fade] Fin fade vers noir");
    }

    public IEnumerator HoldBlackRoutine()
    {
        SetAlpha(1f);
        ForceOnTop();

        if (blackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(blackHoldDuration);
    }

    public IEnumerator FadeFromBlackRoutine()
    {
        Debug.Log("[Fade] Début fade depuis noir");

        isFading = true;
        SetRaycastBlocking(true);
        ForceOnTop();

        yield return FadeAlpha(GetAlpha(), 0f, fadeFromBlackDuration);

        SetAlpha(0f);
        SetRaycastBlocking(false);
        isFading = false;
        ForceOnTop();

        Debug.Log("[Fade] Fin fade depuis noir");
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(to);
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / duration);

            float smoothT = SmootherStep(t);
            smoothT = Mathf.Pow(smoothT, visualCurvePower);

            float alpha = Mathf.Lerp(from, to, smoothT);
            SetAlpha(alpha);
            ForceOnTop();

            yield return null;
        }

        SetAlpha(to);
        ForceOnTop();
    }

    private float SmootherStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private float GetAlpha()
    {
        if (fadeImage == null)
            fadeImage = GetComponent<Image>();

        return fadeImage.color.a;
    }

    private void SetAlpha(float alpha)
    {
        if (fadeImage == null)
            fadeImage = GetComponent<Image>();

        Color color = fadeImage.color;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        color.a = Mathf.Clamp01(alpha);

        fadeImage.color = color;
    }

    private void SetRaycastBlocking(bool active)
    {
        if (fadeImage == null)
            fadeImage = GetComponent<Image>();

        fadeImage.raycastTarget = blockRaycastsDuringFade && active;
    }
}
