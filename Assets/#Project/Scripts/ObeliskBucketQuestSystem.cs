using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Système complet de quête du seau pour Obelisk.
/// À mettre sur un GameObject nommé BucketQuestSystem.
/// Gère : récupération du seau, HUD, remplissage au lac, offrande à l'obélisque, progression 5/5.
/// </summary>
[DisallowMultipleComponent]
public class ObeliskBucketQuestSystem : MonoBehaviour
{
    public enum BucketAction { PickupBucket, FillAtLake, FeedObelisk }

    [Header("Rooms / IDs BackgroundManager")]
    public string pickupRoomId = "CHA_INT_01";
    public string lakeRoomId = "LAC_01";
    public string obeliskRoomId = "Ob_01";
    public bool requireCorrectRoom = true;

    [Header("État")]
    [SerializeField] private bool hasBucket;
    [SerializeField] private bool bucketIsFull;
    [SerializeField] private int offerings;
    [SerializeField] private bool questComplete;
    [Min(1)] public int maxOfferings = 5;

    [Header("UI - Parents")]
    public GameObject bucketQuestRoot;
    public GameObject bucketProgressRoot;
    public GameObject carriedBucketRoot;

    [Header("UI - 5 mini seaux de progression")]
    public Image[] progressBuckets = new Image[5];
    public Sprite progressEmptySprite;
    public Sprite progressFullSprite;
    public bool tintIfSpritesMissing = true;
    public Color emptyTint = Color.white;
    public Color fullTint = new Color(0.45f, 0.85f, 1f, 1f);

    [Header("UI - Gros seau porté")]
    public Image carriedBucketImage;
    public Sprite carriedEmptySprite;
    public Sprite carriedFullSprite;
    public Sprite carriedPouringSprite;
    public float pourDuration = 0.55f;

    [Header("Objet seau dans le décor")]
    [Tooltip("Optionnel : l'objet visuel du seau à cacher après pickup.")]
    public GameObject pickupBucketVisualToHide;

    [Header("Zones cliquables invisibles")]
    public RectTransform pickupZone;
    public RectTransform lakeZone;
    public RectTransform obeliskZone;
    public bool manageZoneVisibility = true;
    public bool forceZonesRaycastable = true;
    [Range(0f, 0.05f)] public float zoneAlpha = 0.001f;

    [Header("Audio optionnel")]
    public AudioSource audioSource;
    public AudioClip pickupSound;
    public AudioClip fillSound;
    public AudioClip feedSound;
    public AudioClip completeSound;

    [Header("Event final")]
    public List<GameObject> activateOnComplete = new List<GameObject>();
    public List<GameObject> deactivateOnComplete = new List<GameObject>();
    public UnityEvent onQuestComplete;

    [Header("Debug")]
    public bool debugLogs = true;
    [Tooltip("Pour tester sans lire BackgroundManager. Laisse vide normalement.")]
    public string debugRoomOverride = "";

    private BackgroundManager backgroundManager;
    private bool isPouring;
    private string lastRoomId = "";

    public bool HasBucket => hasBucket;
    public bool BucketIsFull => bucketIsFull;
    public int Offerings => offerings;
    public bool QuestComplete => questComplete;

    private void Awake()
    {
        CacheRefs();
        PrepareZones();
        RefreshAll();
    }

    private void OnEnable()
    {
        CacheRefs();
        PrepareZones();
        RefreshAll();
    }

    private void Start()
    {
        CacheRefs();
        PrepareZones();
        RefreshAll();
    }

    private void Update()
    {
        CacheRefs();

        string room = GetCurrentRoomId();
        if (room != lastRoomId)
        {
            lastRoomId = room;
            if (debugLogs) Debug.Log("[BucketQuest] Room actuelle = " + room);
            RefreshAll();
        }

        if (manageZoneVisibility)
            UpdateZones();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxOfferings = Mathf.Max(1, maxOfferings);
        offerings = Mathf.Clamp(offerings, 0, maxOfferings);
        if (!Application.isPlaying) RefreshAll();
    }
#endif

    private void CacheRefs()
    {
        if (backgroundManager == null)
            backgroundManager = FindAnyObjectByType<BackgroundManager>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    [ContextMenu("OBELISK / Bucket Quest Auto Find UI + Zones")]
    public void AutoFindUIAndZones()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Transform quest = FindDeep(canvas.transform, "BucketQuestUI");
        if (quest != null) bucketQuestRoot = quest.gameObject;

        Transform gauge = FindDeep(canvas.transform, "BucketGauge");
        if (gauge != null) bucketProgressRoot = gauge.gameObject;

        Transform inv = FindDeep(canvas.transform, "BucketInventory");
        if (inv != null) carriedBucketRoot = inv.gameObject;

        AutoFindProgressImages();

        if (carriedBucketImage == null && carriedBucketRoot != null)
            carriedBucketImage = carriedBucketRoot.GetComponentInChildren<Image>(true);

        GameObject pickup = GameObject.Find("Interact_BucketPickup");
        if (pickup != null) pickupZone = pickup.GetComponent<RectTransform>();

        GameObject lake = GameObject.Find("Interact_LakeWater");
        if (lake != null) lakeZone = lake.GetComponent<RectTransform>();

        GameObject obelisk = GameObject.Find("Interact_ObeliskFeed");
        if (obelisk != null) obeliskZone = obelisk.GetComponent<RectTransform>();

        PrepareZones();
        RefreshAll();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("OBELISK / Bucket Quest Create Missing Zones")]
    public void CreateMissingZones()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Transform root = FindDeep(canvas.transform, "InteractZones");
        if (root == null)
        {
            GameObject r = new GameObject("InteractZones", typeof(RectTransform));
            r.transform.SetParent(canvas.transform, false);
            root = r.transform;
            StretchToCanvas(r.GetComponent<RectTransform>());
        }

        if (pickupZone == null) pickupZone = CreateZone("Interact_BucketPickup", root, new Vector2(220f, 160f));
        if (lakeZone == null) lakeZone = CreateZone("Interact_LakeWater", root, new Vector2(520f, 260f));
        if (obeliskZone == null) obeliskZone = CreateZone("Interact_ObeliskFeed", root, new Vector2(250f, 420f));

        PrepareZones();
        RefreshAll();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private RectTransform CreateZone(string name, Transform parent, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform r = obj.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0.5f);
        r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = Vector2.zero;
        r.localScale = Vector3.one;
        Image img = obj.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, zoneAlpha);
        img.raycastTarget = true;
        return r;
    }

    private void StretchToCanvas(RectTransform r)
    {
        if (r == null) return;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        r.localScale = Vector3.one;
    }

    private void AutoFindProgressImages()
    {
        if (bucketProgressRoot == null) return;

        Image[] imgs = bucketProgressRoot.GetComponentsInChildren<Image>(true);
        List<Image> list = new List<Image>();
        for (int i = 0; i < imgs.Length; i++)
            if (imgs[i] != null) list.Add(imgs[i]);

        list.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        progressBuckets = new Image[Mathf.Max(maxOfferings, 5)];
        for (int i = 0; i < Mathf.Min(progressBuckets.Length, list.Count); i++)
            progressBuckets[i] = list[i];
    }

    private Transform FindDeep(Transform parent, string wanted)
    {
        if (parent == null) return null;
        if (parent.name == wanted) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), wanted);
            if (found != null) return found;
        }
        return null;
    }

    private void PrepareZones()
    {
        PrepareZone(pickupZone, BucketAction.PickupBucket);
        PrepareZone(lakeZone, BucketAction.FillAtLake);
        PrepareZone(obeliskZone, BucketAction.FeedObelisk);
        UpdateZones();
    }

    private void PrepareZone(RectTransform zone, BucketAction action)
    {
        if (zone == null) return;

        if (forceZonesRaycastable)
        {
            Image img = zone.GetComponent<Image>();
            if (img == null) img = zone.gameObject.AddComponent<Image>();
            img.raycastTarget = true;
            Color c = img.color;
            c.a = zoneAlpha;
            img.color = c;
        }

        ObeliskBucketQuestZone z = zone.GetComponent<ObeliskBucketQuestZone>();
        if (z == null) z = zone.gameObject.AddComponent<ObeliskBucketQuestZone>();
        z.system = this;
        z.action = action;
    }

    private void UpdateZones()
    {
        if (!manageZoneVisibility) return;
        SetZone(pickupZone, CanPickup());
        SetZone(lakeZone, CanFill());
        SetZone(obeliskZone, CanFeed());
    }

    private void SetZone(RectTransform zone, bool active)
    {
        if (zone == null) return;
        if (zone.gameObject.activeSelf != active)
            zone.gameObject.SetActive(active);
    }

    public void HandleZoneClick(BucketAction action)
    {
        if (action == BucketAction.PickupBucket) PickupBucket();
        else if (action == BucketAction.FillAtLake) FillBucketAtLake();
        else if (action == BucketAction.FeedObelisk) FeedObelisk();
    }

    public void PickupBucket()
    {
        if (!CanPickup()) { Blocked("pickup refusé"); return; }
        hasBucket = true;
        bucketIsFull = false;
        questComplete = false;
        if (pickupBucketVisualToHide != null) pickupBucketVisualToHide.SetActive(false);
        Play(pickupSound);
        RefreshAll();
        if (debugLogs) Debug.Log("[BucketQuest] Seau récupéré.");
    }

    public void FillBucketAtLake()
    {
        if (!CanFill()) { Blocked("fill refusé"); return; }
        bucketIsFull = true;
        Play(fillSound);
        RefreshAll();
        if (debugLogs) Debug.Log("[BucketQuest] Seau rempli au lac.");
    }

    public void FeedObelisk()
    {
        if (isPouring) return;
        if (!CanFeed()) { Blocked("feed refusé"); return; }
        StartCoroutine(FeedRoutine());
    }

    private IEnumerator FeedRoutine()
    {
        isPouring = true;
        Play(feedSound);

        if (carriedBucketImage != null && carriedPouringSprite != null)
        {
            carriedBucketImage.enabled = true;
            carriedBucketImage.sprite = carriedPouringSprite;
        }

        if (pourDuration > 0f)
            yield return new WaitForSeconds(pourDuration);

        bucketIsFull = false;
        offerings = Mathf.Clamp(offerings + 1, 0, maxOfferings);

        if (offerings >= maxOfferings)
            CompleteQuest();

        isPouring = false;
        RefreshAll();

        if (debugLogs) Debug.Log("[BucketQuest] Offrande : " + offerings + "/" + maxOfferings);
    }

    private void CompleteQuest()
    {
        if (questComplete) return;
        questComplete = true;
        bucketIsFull = false;
        offerings = maxOfferings;
        Play(completeSound);

        for (int i = 0; i < deactivateOnComplete.Count; i++)
            if (deactivateOnComplete[i] != null) deactivateOnComplete[i].SetActive(false);

        for (int i = 0; i < activateOnComplete.Count; i++)
            if (activateOnComplete[i] != null) activateOnComplete[i].SetActive(true);

        onQuestComplete?.Invoke();
        if (debugLogs) Debug.Log("[BucketQuest] QUÊTE COMPLÈTE 5/5.");
    }

    private bool CanPickup()
    {
        return !hasBucket && !questComplete && IsInRoom(pickupRoomId);
    }

    private bool CanFill()
    {
        return hasBucket && !bucketIsFull && !questComplete && IsInRoom(lakeRoomId);
    }

    private bool CanFeed()
    {
        return hasBucket && bucketIsFull && !questComplete && offerings < maxOfferings && IsInRoom(obeliskRoomId);
    }

    private bool IsInRoom(string roomId)
    {
        if (!requireCorrectRoom) return true;
        if (string.IsNullOrEmpty(roomId)) return false;
        return GetCurrentRoomId() == roomId;
    }

    private string GetCurrentRoomId()
    {
        if (!string.IsNullOrWhiteSpace(debugRoomOverride)) return debugRoomOverride;
        if (backgroundManager == null) backgroundManager = FindAnyObjectByType<BackgroundManager>();
        if (backgroundManager == null) return "";

        try { return backgroundManager.GetCurrentRoomId(); }
        catch
        {
            MethodInfo m = typeof(BackgroundManager).GetMethod("GetCurrentRoomId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null) return "";
            object v = m.Invoke(backgroundManager, null);
            return v != null ? v.ToString() : "";
        }
    }

    private void RefreshAll()
    {
        maxOfferings = Mathf.Max(1, maxOfferings);
        offerings = Mathf.Clamp(offerings, 0, maxOfferings);

        if (bucketQuestRoot != null) bucketQuestRoot.SetActive(hasBucket || questComplete);
        if (bucketProgressRoot != null) bucketProgressRoot.SetActive(hasBucket || questComplete);
        if (carriedBucketRoot != null) carriedBucketRoot.SetActive(hasBucket && !questComplete);

        RefreshProgress();
        RefreshCarried();
        UpdateZones();
    }

    private void RefreshProgress()
    {
        if (progressBuckets == null) return;
        int count = Mathf.Min(progressBuckets.Length, maxOfferings);
        for (int i = 0; i < count; i++)
        {
            Image img = progressBuckets[i];
            if (img == null) continue;
            bool full = i < offerings;
            img.enabled = hasBucket || questComplete;
            if (full && progressFullSprite != null) img.sprite = progressFullSprite;
            else if (!full && progressEmptySprite != null) img.sprite = progressEmptySprite;
            img.color = tintIfSpritesMissing ? (full ? fullTint : emptyTint) : Color.white;
            img.preserveAspect = true;
        }
    }

    private void RefreshCarried()
    {
        if (carriedBucketImage == null) return;
        carriedBucketImage.enabled = hasBucket && !questComplete;
        carriedBucketImage.raycastTarget = false;
        carriedBucketImage.preserveAspect = true;
        if (!hasBucket || questComplete) return;
        if (isPouring && carriedPouringSprite != null) carriedBucketImage.sprite = carriedPouringSprite;
        else if (bucketIsFull && carriedFullSprite != null) carriedBucketImage.sprite = carriedFullSprite;
        else if (!bucketIsFull && carriedEmptySprite != null) carriedBucketImage.sprite = carriedEmptySprite;
    }

    private void Play(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource != null) audioSource.PlayOneShot(clip);
        else AudioSource.PlayClipAtPoint(clip, Vector3.zero);
    }

    private void Blocked(string reason)
    {
        if (!debugLogs) return;
        Debug.Log("[BucketQuest] " + reason + " | room=" + GetCurrentRoomId() + " hasBucket=" + hasBucket + " full=" + bucketIsFull + " offerings=" + offerings + "/" + maxOfferings);
    }

    [ContextMenu("OBELISK / Bucket Quest Reset State")]
    public void ResetQuestState()
    {
        hasBucket = false;
        bucketIsFull = false;
        offerings = 0;
        questComplete = false;
        isPouring = false;
        if (pickupBucketVisualToHide != null) pickupBucketVisualToHide.SetActive(true);
        RefreshAll();
    }

    [ContextMenu("OBELISK / Bucket Quest Debug Give Bucket")]
    public void DebugGiveBucket()
    {
        hasBucket = true;
        bucketIsFull = false;
        questComplete = false;
        RefreshAll();
    }

    [ContextMenu("OBELISK / Bucket Quest Debug Fill Bucket")]
    public void DebugFillBucket()
    {
        hasBucket = true;
        bucketIsFull = true;
        RefreshAll();
    }

    [ContextMenu("OBELISK / Bucket Quest Debug Feed Once")]
    public void DebugFeedOnce()
    {
        hasBucket = true;
        bucketIsFull = false;
        offerings = Mathf.Clamp(offerings + 1, 0, maxOfferings);
        if (offerings >= maxOfferings) CompleteQuest();
        RefreshAll();
    }

    [ContextMenu("OBELISK / Set Pickup Room = Current")]
    public void SetPickupRoomCurrent() { pickupRoomId = GetCurrentRoomId(); }

    [ContextMenu("OBELISK / Set Lake Room = Current")]
    public void SetLakeRoomCurrent() { lakeRoomId = GetCurrentRoomId(); }

    [ContextMenu("OBELISK / Set Obelisk Room = Current")]
    public void SetObeliskRoomCurrent() { obeliskRoomId = GetCurrentRoomId(); }
}

[AddComponentMenu("")]
public class ObeliskBucketQuestZone : MonoBehaviour, IPointerClickHandler
{
    public ObeliskBucketQuestSystem system;
    public ObeliskBucketQuestSystem.BucketAction action;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (system != null)
            system.HandleZoneClick(action);
    }
}
