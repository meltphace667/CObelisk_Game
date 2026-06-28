using UnityEngine;

/// <summary>
/// VERSION SECOURS / COMPILATION SAFE.
/// 
/// Ce script garde le même nom de classe que l'ancien fix cassé :
/// ObeliskBucketQuestDirectClickFix
///
/// Il ne lit AUCUN input, donc il ne peut pas casser le lancement du jeu.
/// But immédiat : retirer les erreurs de compilation et permettre à Unity de relancer le Play Mode.
///
/// Une fois le jeu relancé, on corrigera le clic du seau proprement depuis le script principal.
/// </summary>
[DisallowMultipleComponent]
public class ObeliskBucketQuestDirectClickFix : MonoBehaviour
{
    [Header("Référence quête")]
    public ObeliskBucketQuestSystem system;

    [Header("Zones gardées pour ne pas perdre tes références Inspector")]
    public RectTransform pickupZone;
    public RectTransform lakeZone;
    public RectTransform obeliskZone;

    [Header("Debug")]
    public bool debugLogs = true;

    private void Reset()
    {
        system = GetComponent<ObeliskBucketQuestSystem>();
        AutoFindZones();
    }

    private void Awake()
    {
        if (system == null)
            system = GetComponent<ObeliskBucketQuestSystem>();

        if (pickupZone == null || lakeZone == null || obeliskZone == null)
            AutoFindZones();
    }

    private void Start()
    {
        if (debugLogs)
            Debug.Log("[BucketQuestDirectClickFix] Version secours chargée. Aucun input lu. Compilation OK.");
    }

    private void AutoFindZones()
    {
        GameObject pickup = GameObject.Find("Interact_BucketPickup");
        GameObject lake = GameObject.Find("Interact_LakeWater");
        GameObject obelisk = GameObject.Find("Interact_ObeliskFeed");

        if (pickup != null)
            pickupZone = pickup.GetComponent<RectTransform>();

        if (lake != null)
            lakeZone = lake.GetComponent<RectTransform>();

        if (obelisk != null)
            obeliskZone = obelisk.GetComponent<RectTransform>();
    }

#if UNITY_EDITOR
    [ContextMenu("OBELISK / Auto Fill Zones")]
    private void EditorAutoFillZones()
    {
        system = GetComponent<ObeliskBucketQuestSystem>();
        AutoFindZones();

        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log("[BucketQuestDirectClickFix] Auto Fill Zones terminé.");
    }
#endif
}
