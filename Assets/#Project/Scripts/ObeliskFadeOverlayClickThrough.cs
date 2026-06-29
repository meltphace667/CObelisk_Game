using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// À mettre sur l'objet du fade noir / ScreenFader si besoin.
/// Rend le fade overlay totalement "click-through".
/// Le fade reste visible, mais ne bloque jamais la souris.
/// </summary>
[DisallowMultipleComponent]
public class ObeliskFadeOverlayClickThrough : MonoBehaviour
{
    public bool disableGraphicRaycasts = true;
    public bool disableCanvasGroupBlocking = true;
    public bool keepCheckingAtRuntime = true;

    private int framesChecked;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Start()
    {
        Apply();
    }

    private void LateUpdate()
    {
        if (!keepCheckingAtRuntime)
            return;

        if (framesChecked > 60)
            return;

        framesChecked++;
        Apply();
    }

    [ContextMenu("OBELISK / Apply Click Through")]
    public void Apply()
    {
        if (disableGraphicRaycasts)
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].raycastTarget = false;
            }
        }

        if (disableCanvasGroupBlocking)
        {
            CanvasGroup[] groups = GetComponentsInChildren<CanvasGroup>(true);

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null)
                    continue;

                groups[i].blocksRaycasts = false;
                groups[i].interactable = false;
            }
        }
    }
}
