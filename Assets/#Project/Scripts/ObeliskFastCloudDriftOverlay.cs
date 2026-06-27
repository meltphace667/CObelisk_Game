using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ObeliskFastCloudDriftOverlay : MonoBehaviour
{
    public enum CloudPreset
    {
        Subtle,
        DreamSky,
        FastTime,
        NightmareTimelapse,
        ImpossibleWeather,
        SlowDrift
    }

    [System.Serializable]
    private class RoomCloudOverride
    {
        [Header("Room")]
        public string roomId = "";
        public bool enabled = true;

        [Tooltip("Si true, cette room active le cloud même si elle n'est pas dans Affected Room Ids. Si false, cette room force le cloud OFF.")]
        public bool affectThisRoom = true;

        [Header("Preset local")]
        public bool overridePreset = false;
        public CloudPreset preset = CloudPreset.SlowDrift;

        [Header("Base / détection")]
        [Range(0f, 3f)] public float masterIntensityMultiplier = 1f;
        [Range(-1.5f, 1.5f)] public float masterIntensityAdd = 0f;

        [Range(0f, 3f)] public float skyDetectionMultiplier = 1f;
        [Range(-2f, 2f)] public float skyDetectionAdd = 0f;

        [Range(0f, 3f)] public float cloudDetectionMultiplier = 1f;
        [Range(-2f, 2f)] public float cloudDetectionAdd = 0f;

        [Range(0f, 3f)] public float treeProtectionMultiplier = 1f;
        [Range(-2f, 2f)] public float treeProtectionAdd = 0f;

        [Header("Timing local")]
        [Range(0f, 4f)] public float timeMultiplierMultiplier = 1f;
        [Range(-3f, 3f)] public float timeMultiplierAdd = 0f;

        [Range(0f, 4f)] public float cloudMotionMultiplierMultiplier = 1f;
        [Range(-2f, 2f)] public float cloudMotionMultiplierAdd = 0f;

        [Range(0f, 4f)] public float skyMotionMultiplierMultiplier = 1f;
        [Range(-2f, 2f)] public float skyMotionMultiplierAdd = 0f;

        [Range(0f, 4f)] public float trailMultiplierMultiplier = 1f;
        [Range(-2f, 2f)] public float trailMultiplierAdd = 0f;

        [Tooltip("Décale le hasard du shader seulement dans cette room.")]
        public float seedAdd = 0f;

        [Header("Look local")]
        [Range(0f, 3f)] public float intensityMultiplier = 1f;
        [Range(-1.5f, 1.5f)] public float intensityAdd = 0f;

        [Range(0f, 3f)] public float cloudSpeedMultiplier = 1f;
        [Range(-12f, 12f)] public float cloudSpeedAdd = 0f;

        [Range(0f, 3f)] public float skySpeedMultiplier = 1f;
        [Range(-6f, 6f)] public float skySpeedAdd = 0f;

        [Range(0f, 3f)] public float horizonProtectionMultiplier = 1f;
        [Range(-1f, 1f)] public float horizonProtectionAdd = 0f;

        [Range(0f, 3f)] public float trailStrengthMultiplier = 1f;
        [Range(-1f, 1f)] public float trailStrengthAdd = 0f;

        [Range(0f, 3f)] public float turbulenceMultiplier = 1f;
        [Range(-1f, 1f)] public float turbulenceAdd = 0f;

        [Range(0f, 3f)] public float skyTintStrengthMultiplier = 1f;
        [Range(-1f, 1f)] public float skyTintStrengthAdd = 0f;

        [Range(0f, 3f)] public float cloudGlowMultiplier = 1f;
        [Range(-1.5f, 1.5f)] public float cloudGlowAdd = 0f;

        [Range(0f, 3f)] public float shutterPulseMultiplier = 1f;
        [Range(-1f, 1f)] public float shutterPulseAdd = 0f;

        [Range(0f, 3f)] public float noiseMultiplier = 1f;
        [Range(-1f, 1f)] public float noiseAdd = 0f;

        [Header("Teintes locales")]
        [Range(0f, 1f)] public float tintMix = 0f;
        public Color tintA = new Color(0.42f, 0.58f, 0.72f);
        public Color tintB = new Color(0.48f, 0.62f, 0.50f);
        public Color tintC = new Color(0.62f, 0.58f, 0.68f);
    }

    [Header("Références")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private BackgroundManager backgroundManager;
    [SerializeField] private Shader cloudOverlayShader;
    [SerializeField] private Image fadeOverlay;

    [Header("Overlay créé automatiquement")]
    [SerializeField] private string overlayObjectName = "Obelisk_FastCloudDriftOverlay";
    [SerializeField] private string fadeOverlayObjectName = "FadeOverlay";
    [SerializeField] private bool createOverlayOnStart = true;
    [SerializeField] private bool keepOverlayAboveRooms = true;
    [SerializeField] private bool keepBelowFadeOverlay = true;

    [Header("Détection des rooms")]
    [SerializeField] private bool autoFindActiveRoomImage = true;
    [SerializeField] private List<Image> roomImages = new List<Image>();

    [Header("Rooms impactées par le fast cloud")]
    [SerializeField] private bool useAffectedRoomFilter = true;
    [SerializeField] private List<string> affectedRoomIds = new List<string>
    {
        "PRA_01"
    };
    [SerializeField] private bool affectUnknownRoom = false;

    [Header("Overrides par room")]
    [Tooltip("Permet de modifier le fast cloud room par room. Un override local se pose par-dessus la logique globale.")]
    [SerializeField] private bool enableRoomOverrides = true;

    [Tooltip("Sécurité : si Unity crée un nouvel override avec des multiplicateurs à 0, on les considère comme 1 pour éviter de tuer l'effet.")]
    [SerializeField] private bool zeroRoomOverrideMultiplierMeansNeutral = true;

    [SerializeField] private List<RoomCloudOverride> roomOverrides = new List<RoomCloudOverride>();

    [Header("Effet principal")]
    [SerializeField] private CloudPreset preset = CloudPreset.NightmareTimelapse;

    [Range(0f, 1.5f)]
    [SerializeField] private float masterIntensity = 0.90f;

    [Range(0f, 2f)]
    [SerializeField] private float skyDetection = 1.0f;

    [Range(0f, 2f)]
    [SerializeField] private float cloudDetection = 1.0f;

    [Range(0f, 2f)]
    [SerializeField] private float treeProtection = 1.15f;

    [Header("Timing / vitesse")]
    [SerializeField] private bool animateOnlyDuringPlay = true;

    [Tooltip("Vitesse globale de l'effet. 1 = normal, 0.25 = très lent, 0 = figé.")]
    [Range(0f, 3f)]
    [SerializeField] private float timeMultiplier = 1.0f;

    [Tooltip("Multiplie seulement le déplacement des nuages. 1 = preset normal, 0.25 = nuages 4x plus lents.")]
    [Range(0f, 2f)]
    [SerializeField] private float cloudMotionMultiplier = 1.0f;

    [Tooltip("Multiplie seulement les variations de couleur du ciel.")]
    [Range(0f, 2f)]
    [SerializeField] private float skyMotionMultiplier = 1.0f;

    [Tooltip("Réduit ou augmente les traînées de timelapse. Plus bas = moins de vitesse ressentie.")]
    [Range(0f, 2f)]
    [SerializeField] private float trailMultiplier = 1.0f;

    [SerializeField] private float seed = 91.27f;

    [Header("Sécurité rose / shader")]
    [Tooltip("Si le shader est absent/cassé, l'overlay est désactivé au lieu d'afficher un gros rectangle rose.")]
    [SerializeField] private bool disableOverlayIfShaderMissing = true;

    [Tooltip("Efface le material cassé sur l'overlay au lancement.")]
    [SerializeField] private bool repairPinkOverlayOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Image overlayImage;
    private Material runtimeMaterial;
    private Image currentSourceImage;
    private bool shaderReady = false;

    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int CloudSpeedId = Shader.PropertyToID("_CloudSpeed");
    private static readonly int SkySpeedId = Shader.PropertyToID("_SkySpeed");
    private static readonly int SkyDetectionId = Shader.PropertyToID("_SkyDetection");
    private static readonly int CloudDetectionId = Shader.PropertyToID("_CloudDetection");
    private static readonly int TreeProtectionId = Shader.PropertyToID("_TreeProtection");
    private static readonly int HorizonProtectionId = Shader.PropertyToID("_HorizonProtection");
    private static readonly int TrailStrengthId = Shader.PropertyToID("_TrailStrength");
    private static readonly int TurbulenceId = Shader.PropertyToID("_Turbulence");
    private static readonly int SkyTintStrengthId = Shader.PropertyToID("_SkyTintStrength");
    private static readonly int CloudGlowId = Shader.PropertyToID("_CloudGlow");
    private static readonly int ShutterPulseId = Shader.PropertyToID("_ShutterPulse");
    private static readonly int NoiseId = Shader.PropertyToID("_Noise");
    private static readonly int TintAId = Shader.PropertyToID("_TintA");
    private static readonly int TintBId = Shader.PropertyToID("_TintB");
    private static readonly int TintCId = Shader.PropertyToID("_TintC");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int TimeId = Shader.PropertyToID("_ObeliskTime");

    private void Awake()
    {
        Setup();
    }

    private void Start()
    {
        Setup();
        RefreshSourceImage(true);
    }

    private void LateUpdate()
    {
        if (!shaderReady)
            return;

        if (runtimeMaterial == null || overlayImage == null)
            return;

        if (backgroundManager == null)
            backgroundManager = FindAnyObjectByType<BackgroundManager>();

        EnsureOverlaySiblingOrder();
        RefreshSourceImage(false);
        ApplyPreset();

        RoomCloudOverride roomOverride = GetCurrentRoomOverride();
        float t = animateOnlyDuringPlay ? Time.time : Time.unscaledTime;
        runtimeMaterial.SetFloat(TimeId, t * GetActiveTimeMultiplier(roomOverride));
        runtimeMaterial.SetFloat(SeedId, GetActiveSeed(roomOverride));
    }

    [ContextMenu("OBELISK / Setup Cloud Overlay")]
    public void Setup()
    {
        if (targetCanvas == null)
            targetCanvas = FindAnyObjectByType<Canvas>();

        if (backgroundManager == null)
            backgroundManager = FindAnyObjectByType<BackgroundManager>();

        if (fadeOverlay == null)
            fadeOverlay = FindScreenFaderImage();

        if (fadeOverlay == null)
            fadeOverlay = FindImageByName(fadeOverlayObjectName);

        if (cloudOverlayShader == null)
            cloudOverlayShader = Shader.Find("Obelisk/UI/Fast Cloud Drift Overlay");

        if (!ValidateShader())
        {
            DisableOverlayBecauseShaderIsBroken();
            return;
        }

        if (runtimeMaterial == null)
        {
            runtimeMaterial = new Material(cloudOverlayShader);
            runtimeMaterial.name = "Obelisk_FastCloudDriftOverlay_Runtime_SAFE";
        }
        else if (runtimeMaterial.shader != cloudOverlayShader)
        {
            runtimeMaterial.shader = cloudOverlayShader;
        }

        if (createOverlayOnStart)
            CreateOrFindOverlay();

        if (autoFindActiveRoomImage)
            AutoFindRoomImages();

        ApplyPreset();
        RefreshSourceImage(true);
        EnsureOverlaySiblingOrder();

        if (debugLogs)
            Debug.Log("[ObeliskFastCloudDriftOverlay] Setup OK, shader valide.");
    }

    [ContextMenu("OBELISK / Repair Pink Overlay Now")]
    public void RepairPinkOverlayNow()
    {
        if (overlayImage == null)
            CreateOrFindOverlay();

        if (cloudOverlayShader == null)
            cloudOverlayShader = Shader.Find("Obelisk/UI/Fast Cloud Drift Overlay");

        if (!ValidateShader())
        {
            DisableOverlayBecauseShaderIsBroken();
            return;
        }

        if (runtimeMaterial == null)
        {
            runtimeMaterial = new Material(cloudOverlayShader);
            runtimeMaterial.name = "Obelisk_FastCloudDriftOverlay_Runtime_SAFE";
        }

        overlayImage.material = runtimeMaterial;
        overlayImage.color = Color.white;
        overlayImage.raycastTarget = false;
        overlayImage.enabled = IsCurrentRoomAffected();

        RefreshSourceImage(true);
        ApplyPreset();

        EnsureOverlaySiblingOrder();

        Debug.Log("[ObeliskFastCloudDriftOverlay] Pink overlay réparé.");
    }

    [ContextMenu("OBELISK / Disable Cloud Overlay")]
    public void DisableCloudOverlay()
    {
        if (overlayImage == null)
            CreateOrFindOverlay();

        if (overlayImage != null)
        {
            overlayImage.material = null;
            overlayImage.enabled = false;
        }
    }

    [ContextMenu("OBELISK / Refresh Room Images")]
    public void AutoFindRoomImages()
    {
        roomImages.RemoveAll(image => image == null);

        if (targetCanvas == null)
            targetCanvas = FindAnyObjectByType<Canvas>();

        if (targetCanvas == null)
            return;

        Image[] images = targetCanvas.GetComponentsInChildren<Image>(true);

        foreach (Image image in images)
        {
            if (image == null)
                continue;

            if (image == overlayImage)
                continue;

            if (ShouldIgnore(image))
                continue;

            if (!roomImages.Contains(image))
                roomImages.Add(image);
        }

        if (debugLogs)
            Debug.Log("[ObeliskFastCloudDriftOverlay] Images candidates trouvées : " + roomImages.Count);
    }

    private bool ValidateShader()
    {
        shaderReady = false;

        if (cloudOverlayShader == null)
        {
            Debug.LogError("[ObeliskFastCloudDriftOverlay] Shader introuvable. Le fichier shader doit commencer par : Shader \"Obelisk/UI/Fast Cloud Drift Overlay\"");
            return false;
        }

        if (!cloudOverlayShader.isSupported)
        {
            Debug.LogError("[ObeliskFastCloudDriftOverlay] Shader trouvé mais non supporté / cassé : " + cloudOverlayShader.name);
            return false;
        }

        shaderReady = true;
        return true;
    }

    private void DisableOverlayBecauseShaderIsBroken()
    {
        if (!disableOverlayIfShaderMissing)
            return;

        Transform existing = null;

        if (targetCanvas != null)
            existing = targetCanvas.transform.Find(overlayObjectName);

        if (existing != null)
            overlayImage = existing.GetComponent<Image>();

        if (overlayImage != null)
        {
            overlayImage.material = null;
            overlayImage.enabled = false;
            overlayImage.raycastTarget = false;
        }

        shaderReady = false;
    }

    private bool ShouldIgnore(Image image)
    {
        string n = image.gameObject.name.ToLowerInvariant();

        if (n.Contains("zone"))
            return true;

        if (n.Contains("fade"))
            return true;

        if (n.Contains("overlay"))
            return true;

        if (n.Contains("cursor"))
            return true;

        if (n.Contains("button"))
            return true;

        if (image.sprite == null)
            return true;

        return false;
    }

    private void CreateOrFindOverlay()
    {
        if (targetCanvas == null)
            return;

        Transform existing = targetCanvas.transform.Find(overlayObjectName);

        if (existing != null)
        {
            overlayImage = existing.GetComponent<Image>();

            if (overlayImage == null)
                overlayImage = existing.gameObject.AddComponent<Image>();
        }
        else
        {
            GameObject obj = new GameObject(overlayObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(targetCanvas.transform, false);
            overlayImage = obj.GetComponent<Image>();

            RectTransform rectTransform = obj.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        overlayImage.raycastTarget = false;
        overlayImage.color = Color.white;

        if (repairPinkOverlayOnStart)
            overlayImage.material = null;

        if (shaderReady && runtimeMaterial != null)
            overlayImage.material = runtimeMaterial;

        EnsureOverlaySiblingOrder();
    }

    private void RefreshSourceImage(bool force)
    {
        if (overlayImage == null)
            return;

        if (!IsCurrentRoomAffected())
        {
            overlayImage.enabled = false;
            currentSourceImage = null;
            return;
        }

        Image source = FindBestSourceImage();

        if (!force && source == currentSourceImage)
            return;

        currentSourceImage = source;

        if (source == null)
        {
            overlayImage.enabled = false;
            return;
        }

        overlayImage.enabled = shaderReady;
        overlayImage.sprite = source.sprite;
        overlayImage.type = source.type;
        overlayImage.preserveAspect = source.preserveAspect;
        overlayImage.color = Color.white;

        if (runtimeMaterial != null)
            overlayImage.material = runtimeMaterial;

        CopyRectTransform(source.rectTransform, overlayImage.rectTransform);
    }

    private Image FindBestSourceImage()
    {
        string currentRoomId = GetCurrentRoomIdSafe();

        if (!string.IsNullOrEmpty(currentRoomId))
        {
            for (int i = 0; i < roomImages.Count; i++)
            {
                Image image = roomImages[i];

                if (image != null && image.gameObject.name == currentRoomId && image.gameObject.activeInHierarchy)
                    return image;
            }
        }

        for (int i = 0; i < roomImages.Count; i++)
        {
            Image image = roomImages[i];

            if (image == null)
                continue;

            if (!image.gameObject.activeInHierarchy)
                continue;

            if (image.sprite == null)
                continue;

            if (image.color.a <= 0.01f)
                continue;

            return image;
        }

        return null;
    }

    private void CopyRectTransform(RectTransform source, RectTransform target)
    {
        if (source == null || target == null)
            return;

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
    }


    private Image FindScreenFaderImage()
    {
        if (ScreenFader.Instance != null)
        {
            Image image = ScreenFader.Instance.GetComponent<Image>();

            if (image != null)
                return image;
        }

        ScreenFader fader = FindAnyObjectByType<ScreenFader>();

        if (fader == null)
            return null;

        return fader.GetComponent<Image>();
    }

    private Image FindImageByName(string objectName)
    {
        if (targetCanvas == null || string.IsNullOrEmpty(objectName))
            return null;

        Image[] images = targetCanvas.GetComponentsInChildren<Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].gameObject.name == objectName)
                return images[i];
        }

        return null;
    }

    private void EnsureOverlaySiblingOrder()
    {
        if (!keepOverlayAboveRooms || overlayImage == null)
            return;

        if (targetCanvas == null)
            return;

        if (!keepBelowFadeOverlay)
        {
            overlayImage.transform.SetAsLastSibling();
            return;
        }

        if (fadeOverlay == null)
            fadeOverlay = FindScreenFaderImage();

        if (fadeOverlay == null)
            fadeOverlay = FindImageByName(fadeOverlayObjectName);

        if (fadeOverlay == null)
        {
            // Fallback : on met l'overlay au-dessus des rooms.
            // Mais sans ScreenFader trouvé, le fade peut être masqué.
            overlayImage.transform.SetAsLastSibling();
            return;
        }

        if (fadeOverlay.transform.parent != overlayImage.transform.parent)
        {
            // Cas rare : pas le même parent UI. On force quand même le ScreenFader au-dessus dans son parent.
            fadeOverlay.transform.SetAsLastSibling();
            overlayImage.transform.SetAsLastSibling();
            return;
        }

        // Ordre voulu :
        // rooms / zones / overlays visuels
        // Obelisk_FastCloudDriftOverlay
        // FadeOverlay / ScreenFader
        fadeOverlay.transform.SetAsLastSibling();

        int fadeIndex = fadeOverlay.transform.GetSiblingIndex();
        int overlayTargetIndex = Mathf.Max(0, fadeIndex - 1);

        if (overlayImage.transform.GetSiblingIndex() != overlayTargetIndex)
            overlayImage.transform.SetSiblingIndex(overlayTargetIndex);

        // Après avoir déplacé l'overlay, on remet encore le fade en dernier par sécurité.
        fadeOverlay.transform.SetAsLastSibling();
    }

    private string GetCurrentRoomIdSafe()
    {
        if (backgroundManager == null)
            backgroundManager = FindAnyObjectByType<BackgroundManager>();

        if (backgroundManager == null)
            return "";

        return backgroundManager.GetCurrentRoomId();
    }

    private bool IsCurrentRoomAffected()
    {
        string id = GetCurrentRoomIdSafe();

        RoomCloudOverride roomOverride = FindRoomOverride(id);

        if (roomOverride != null)
            return roomOverride.affectThisRoom;

        if (!useAffectedRoomFilter)
            return true;

        if (string.IsNullOrEmpty(id))
            return affectUnknownRoom;

        for (int i = 0; i < affectedRoomIds.Count; i++)
        {
            if (affectedRoomIds[i] == id)
                return true;
        }

        return false;
    }

    private RoomCloudOverride GetCurrentRoomOverride()
    {
        return FindRoomOverride(GetCurrentRoomIdSafe());
    }

    private RoomCloudOverride FindRoomOverride(string roomId)
    {
        if (!enableRoomOverrides)
            return null;

        if (string.IsNullOrEmpty(roomId))
            return null;

        for (int i = 0; i < roomOverrides.Count; i++)
        {
            RoomCloudOverride roomOverride = roomOverrides[i];

            if (roomOverride == null)
                continue;

            if (!roomOverride.enabled)
                continue;

            if (roomOverride.roomId == roomId)
                return roomOverride;
        }

        return null;
    }

    [ContextMenu("OBELISK / FastCloud Add Current Room Override")]
    public void AddCurrentRoomOverride()
    {
        string id = GetCurrentRoomIdSafe();

        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[ObeliskFastCloudDriftOverlay] Room actuelle inconnue.");
            return;
        }

        RoomCloudOverride roomOverride = GetOrCreateRoomOverride(id);
        roomOverride.affectThisRoom = true;
        roomOverride.enabled = true;

        RepairOneRoomOverrideMultipliers(roomOverride);
        ApplyPreset();
        RefreshSourceImage(true);

        Debug.Log("[ObeliskFastCloudDriftOverlay] Override fast cloud ajouté/réparé pour : " + id);
    }

    [ContextMenu("OBELISK / FastCloud Add PRA_01 Override")]
    public void AddPRA01Override()
    {
        RoomCloudOverride roomOverride = GetOrCreateRoomOverride("PRA_01");
        roomOverride.affectThisRoom = true;
        roomOverride.enabled = true;
        RepairOneRoomOverrideMultipliers(roomOverride);
        ApplyPreset();
        RefreshSourceImage(true);

        Debug.Log("[ObeliskFastCloudDriftOverlay] Override fast cloud ajouté/réparé pour PRA_01.");
    }

    [ContextMenu("OBELISK / FastCloud Add LAC_01 Override")]
    public void AddLAC01Override()
    {
        RoomCloudOverride roomOverride = GetOrCreateRoomOverride("LAC_01");
        roomOverride.affectThisRoom = true;
        roomOverride.enabled = true;
        RepairOneRoomOverrideMultipliers(roomOverride);
        ApplyPreset();
        RefreshSourceImage(true);

        Debug.Log("[ObeliskFastCloudDriftOverlay] Override fast cloud ajouté/réparé pour LAC_01.");
    }

    [ContextMenu("OBELISK / FastCloud Repair Room Override Multipliers")]
    public void RepairRoomOverrideMultipliers()
    {
        for (int i = 0; i < roomOverrides.Count; i++)
        {
            RoomCloudOverride roomOverride = roomOverrides[i];

            if (roomOverride == null)
                continue;

            RepairOneRoomOverrideMultipliers(roomOverride);
        }

        ApplyPreset();
        RefreshSourceImage(true);

        Debug.Log("[ObeliskFastCloudDriftOverlay] Multiplicateurs d'overrides réparés : 0 → 1.");
    }

    private RoomCloudOverride GetOrCreateRoomOverride(string roomId)
    {
        for (int i = 0; i < roomOverrides.Count; i++)
        {
            RoomCloudOverride existing = roomOverrides[i];

            if (existing == null)
                continue;

            if (existing.roomId == roomId)
                return existing;
        }

        RoomCloudOverride created = new RoomCloudOverride();
        created.roomId = roomId;
        created.enabled = true;
        created.affectThisRoom = true;
        roomOverrides.Add(created);

        return created;
    }

    private void RepairOneRoomOverrideMultipliers(RoomCloudOverride roomOverride)
    {
        if (roomOverride == null)
            return;

        if (Mathf.Approximately(roomOverride.masterIntensityMultiplier, 0f)) roomOverride.masterIntensityMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.skyDetectionMultiplier, 0f)) roomOverride.skyDetectionMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.cloudDetectionMultiplier, 0f)) roomOverride.cloudDetectionMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.treeProtectionMultiplier, 0f)) roomOverride.treeProtectionMultiplier = 1f;

        if (Mathf.Approximately(roomOverride.timeMultiplierMultiplier, 0f)) roomOverride.timeMultiplierMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.cloudMotionMultiplierMultiplier, 0f)) roomOverride.cloudMotionMultiplierMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.skyMotionMultiplierMultiplier, 0f)) roomOverride.skyMotionMultiplierMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.trailMultiplierMultiplier, 0f)) roomOverride.trailMultiplierMultiplier = 1f;

        if (Mathf.Approximately(roomOverride.intensityMultiplier, 0f)) roomOverride.intensityMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.cloudSpeedMultiplier, 0f)) roomOverride.cloudSpeedMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.skySpeedMultiplier, 0f)) roomOverride.skySpeedMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.horizonProtectionMultiplier, 0f)) roomOverride.horizonProtectionMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.trailStrengthMultiplier, 0f)) roomOverride.trailStrengthMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.turbulenceMultiplier, 0f)) roomOverride.turbulenceMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.skyTintStrengthMultiplier, 0f)) roomOverride.skyTintStrengthMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.cloudGlowMultiplier, 0f)) roomOverride.cloudGlowMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.shutterPulseMultiplier, 0f)) roomOverride.shutterPulseMultiplier = 1f;
        if (Mathf.Approximately(roomOverride.noiseMultiplier, 0f)) roomOverride.noiseMultiplier = 1f;
    }

    [ContextMenu("OBELISK / FastCloud Add Current Room")]
    public void AddCurrentRoomToAffectedRooms()
    {
        string id = GetCurrentRoomIdSafe();

        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[ObeliskFastCloudDriftOverlay] Room actuelle inconnue.");
            return;
        }

        if (!affectedRoomIds.Contains(id))
            affectedRoomIds.Add(id);

        Debug.Log("[ObeliskFastCloudDriftOverlay] Room ajoutée au fast cloud : " + id);
    }

    [ContextMenu("OBELISK / FastCloud Reset Affected Rooms To PRA_01")]
    public void ResetAffectedRoomsToPRA01()
    {
        affectedRoomIds.Clear();
        affectedRoomIds.Add("PRA_01");
        Debug.Log("[ObeliskFastCloudDriftOverlay] Rooms impactées reset : PRA_01");
    }

    [ContextMenu("OBELISK / FastCloud Slow Settings")]
    public void ApplySlowCloudSettings()
    {
        timeMultiplier = 0.35f;
        cloudMotionMultiplier = 0.30f;
        skyMotionMultiplier = 0.35f;
        trailMultiplier = 0.35f;
        preset = CloudPreset.SlowDrift;

        ApplyPreset();

        Debug.Log("[ObeliskFastCloudDriftOverlay] Réglages lents appliqués.");
    }

    [ContextMenu("OBELISK / FastCloud Very Slow Settings")]
    public void ApplyVerySlowCloudSettings()
    {
        timeMultiplier = 0.18f;
        cloudMotionMultiplier = 0.18f;
        skyMotionMultiplier = 0.22f;
        trailMultiplier = 0.18f;
        preset = CloudPreset.SlowDrift;

        ApplyPreset();

        Debug.Log("[ObeliskFastCloudDriftOverlay] Réglages très lents appliqués.");
    }

    [ContextMenu("OBELISK / FastCloud Normal Timing")]
    public void ApplyNormalCloudTiming()
    {
        timeMultiplier = 1.0f;
        cloudMotionMultiplier = 1.0f;
        skyMotionMultiplier = 1.0f;
        trailMultiplier = 1.0f;

        ApplyPreset();

        Debug.Log("[ObeliskFastCloudDriftOverlay] Timing normal appliqué.");
    }

    private void ApplyPreset()
    {
        if (runtimeMaterial == null)
            return;

        RoomCloudOverride roomOverride = GetCurrentRoomOverride();
        CloudPreset activePreset = preset;

        if (roomOverride != null && roomOverride.overridePreset)
            activePreset = roomOverride.preset;

        CloudLook look = GetLook(activePreset);

        if (roomOverride != null)
            look = ApplyRoomOverrideToLook(look, roomOverride);

        float activeMasterIntensity = GetActiveMasterIntensity(roomOverride);
        float activeSkyDetection = GetActiveSkyDetection(roomOverride);
        float activeCloudDetection = GetActiveCloudDetection(roomOverride);
        float activeTreeProtection = GetActiveTreeProtection(roomOverride);
        float activeCloudMotionMultiplier = GetActiveCloudMotionMultiplier(roomOverride);
        float activeSkyMotionMultiplier = GetActiveSkyMotionMultiplier(roomOverride);
        float activeTrailMultiplier = GetActiveTrailMultiplier(roomOverride);

        runtimeMaterial.SetFloat(IntensityId, Mathf.Max(0f, look.intensity * activeMasterIntensity));
        runtimeMaterial.SetFloat(CloudSpeedId, Mathf.Max(0f, look.cloudSpeed * activeCloudMotionMultiplier));
        runtimeMaterial.SetFloat(SkySpeedId, Mathf.Max(0f, look.skySpeed * activeSkyMotionMultiplier));

        runtimeMaterial.SetFloat(SkyDetectionId, Mathf.Max(0f, activeSkyDetection));
        runtimeMaterial.SetFloat(CloudDetectionId, Mathf.Max(0f, activeCloudDetection));
        runtimeMaterial.SetFloat(TreeProtectionId, Mathf.Max(0f, activeTreeProtection));
        runtimeMaterial.SetFloat(HorizonProtectionId, Mathf.Clamp01(look.horizonProtection));

        runtimeMaterial.SetFloat(TrailStrengthId, Mathf.Clamp01(look.trailStrength * activeTrailMultiplier));
        runtimeMaterial.SetFloat(TurbulenceId, Mathf.Clamp01(look.turbulence));
        runtimeMaterial.SetFloat(SkyTintStrengthId, Mathf.Clamp01(look.skyTintStrength));
        runtimeMaterial.SetFloat(CloudGlowId, Mathf.Max(0f, look.cloudGlow));
        runtimeMaterial.SetFloat(ShutterPulseId, Mathf.Clamp01(look.shutterPulse));
        runtimeMaterial.SetFloat(NoiseId, Mathf.Clamp01(look.noise));

        runtimeMaterial.SetColor(TintAId, look.tintA);
        runtimeMaterial.SetColor(TintBId, look.tintB);
        runtimeMaterial.SetColor(TintCId, look.tintC);
    }

    private CloudLook ApplyRoomOverrideToLook(CloudLook look, RoomCloudOverride roomOverride)
    {
        if (roomOverride == null)
            return look;

        look.intensity = look.intensity * GetRoomMultiplier(roomOverride.intensityMultiplier) + roomOverride.intensityAdd;
        look.cloudSpeed = look.cloudSpeed * GetRoomMultiplier(roomOverride.cloudSpeedMultiplier) + roomOverride.cloudSpeedAdd;
        look.skySpeed = look.skySpeed * GetRoomMultiplier(roomOverride.skySpeedMultiplier) + roomOverride.skySpeedAdd;
        look.horizonProtection = look.horizonProtection * GetRoomMultiplier(roomOverride.horizonProtectionMultiplier) + roomOverride.horizonProtectionAdd;
        look.trailStrength = look.trailStrength * GetRoomMultiplier(roomOverride.trailStrengthMultiplier) + roomOverride.trailStrengthAdd;
        look.turbulence = look.turbulence * GetRoomMultiplier(roomOverride.turbulenceMultiplier) + roomOverride.turbulenceAdd;
        look.skyTintStrength = look.skyTintStrength * GetRoomMultiplier(roomOverride.skyTintStrengthMultiplier) + roomOverride.skyTintStrengthAdd;
        look.cloudGlow = look.cloudGlow * GetRoomMultiplier(roomOverride.cloudGlowMultiplier) + roomOverride.cloudGlowAdd;
        look.shutterPulse = look.shutterPulse * GetRoomMultiplier(roomOverride.shutterPulseMultiplier) + roomOverride.shutterPulseAdd;
        look.noise = look.noise * GetRoomMultiplier(roomOverride.noiseMultiplier) + roomOverride.noiseAdd;

        look.tintA = Color.Lerp(look.tintA, roomOverride.tintA, roomOverride.tintMix);
        look.tintB = Color.Lerp(look.tintB, roomOverride.tintB, roomOverride.tintMix);
        look.tintC = Color.Lerp(look.tintC, roomOverride.tintC, roomOverride.tintMix);

        return look;
    }

    private float GetActiveMasterIntensity(RoomCloudOverride roomOverride)
    {
        if (roomOverride == null)
            return masterIntensity;

        return Mathf.Max(0f, masterIntensity * GetRoomMultiplier(roomOverride.masterIntensityMultiplier) + roomOverride.masterIntensityAdd);
    }

    private float GetActiveSkyDetection(RoomCloudOverride roomOverride)
    {
        if (roomOverride == null)
            return skyDetection;

        return Mathf.Max(0f, skyDetection * GetRoomMultiplier(roomOverride.skyDetectionMultiplier) + roomOverride.skyDetectionAdd);
    }

    private float GetActiveCloudDetection(RoomCloudOverride roomOverride)
    {
        if (roomOverride == null)
            return cloudDetection;

        return Mathf.Max(0f, cloudDetection * GetRoomMultiplier(roomOverride.cloudDetectionMultiplier) + roomOverride.cloudDetectionAdd);
    }

    private float GetActiveTreeProtection(RoomCloudOverride roomOverride)
    {
        if (roomOverride == null)
            return treeProtection;

        return Mathf.Max(0f, treeProtection * GetRoomMultiplier(roomOverride.treeProtectionMultiplier) + roomOverride.treeProtectionAdd);
    }

    private float GetActiveTimeMultiplier(RoomCloudOverride roomOverride)
    {
        if (roomOverride == null)
            return Mathf.Max(0f, timeMultiplier);

        return Mathf.Max(0f, timeMultiplier * GetRoomMultiplier(roomOverride.timeMultiplierMultiplier) + roomOverride.timeMultiplierAdd);
    }

    private float GetActiveCloudMotionMultiplier(RoomCloudOverride roomOverride)
    {
        if (roomOverride == null)
            return Mathf.Max(0f, cloudMotionMultiplier);

        return Mathf.Max(0f, cloudMotionMultiplier * GetRoomMultiplier(roomOverride.cloudMotionMultiplierMultiplier) + roomOverride.cloudMotionMultiplierAdd);
    }

    private float GetActiveSkyMotionMultiplier(RoomCloudOverride roomOverride)
    {
        if (roomOverride == null)
            return Mathf.Max(0f, skyMotionMultiplier);

        return Mathf.Max(0f, skyMotionMultiplier * GetRoomMultiplier(roomOverride.skyMotionMultiplierMultiplier) + roomOverride.skyMotionMultiplierAdd);
    }

    private float GetActiveTrailMultiplier(RoomCloudOverride roomOverride)
    {
        if (roomOverride == null)
            return Mathf.Max(0f, trailMultiplier);

        return Mathf.Max(0f, trailMultiplier * GetRoomMultiplier(roomOverride.trailMultiplierMultiplier) + roomOverride.trailMultiplierAdd);
    }

    private float GetActiveSeed(RoomCloudOverride roomOverride)
    {
        if (roomOverride == null)
            return seed;

        return seed + roomOverride.seedAdd;
    }

    private float GetRoomMultiplier(float value)
    {
        if (zeroRoomOverrideMultiplierMeansNeutral && Mathf.Approximately(value, 0f))
            return 1f;

        return value;
    }

    public void SetPresetSubtle()
    {
        preset = CloudPreset.Subtle;
    }

    public void SetPresetDreamSky()
    {
        preset = CloudPreset.DreamSky;
    }

    public void SetPresetFastTime()
    {
        preset = CloudPreset.FastTime;
    }

    public void SetPresetNightmareTimelapse()
    {
        preset = CloudPreset.NightmareTimelapse;
    }

    public void SetPresetImpossibleWeather()
    {
        preset = CloudPreset.ImpossibleWeather;
    }

    public void SetPresetSlowDrift()
    {
        preset = CloudPreset.SlowDrift;
    }

    public void SetMasterIntensity(float value)
    {
        masterIntensity = Mathf.Clamp(value, 0f, 1.5f);
    }

    private struct CloudLook
    {
        public float intensity;
        public float cloudSpeed;
        public float skySpeed;
        public float horizonProtection;
        public float trailStrength;
        public float turbulence;
        public float skyTintStrength;
        public float cloudGlow;
        public float shutterPulse;
        public float noise;
        public Color tintA;
        public Color tintB;
        public Color tintC;
    }

    private CloudLook GetLook(CloudPreset cloudPreset)
    {
        switch (cloudPreset)
        {
            case CloudPreset.Subtle:
                return new CloudLook
                {
                    intensity = 0.35f,
                    cloudSpeed = 2.0f,
                    skySpeed = 0.35f,
                    horizonProtection = 0.45f,
                    trailStrength = 0.25f,
                    turbulence = 0.18f,
                    skyTintStrength = 0.12f,
                    cloudGlow = 0.25f,
                    shutterPulse = 0.04f,
                    noise = 0.04f,
                    tintA = new Color(0.55f, 0.74f, 1.00f),
                    tintB = new Color(0.66f, 0.88f, 0.58f),
                    tintC = new Color(0.94f, 0.78f, 1.00f)
                };

            case CloudPreset.DreamSky:
                return new CloudLook
                {
                    intensity = 0.62f,
                    cloudSpeed = 4.8f,
                    skySpeed = 0.85f,
                    horizonProtection = 0.34f,
                    trailStrength = 0.45f,
                    turbulence = 0.30f,
                    skyTintStrength = 0.24f,
                    cloudGlow = 0.50f,
                    shutterPulse = 0.12f,
                    noise = 0.08f,
                    tintA = new Color(0.50f, 0.75f, 1.00f),
                    tintB = new Color(0.70f, 0.95f, 0.55f),
                    tintC = new Color(0.95f, 0.72f, 1.00f)
                };

            case CloudPreset.FastTime:
                return new CloudLook
                {
                    intensity = 0.78f,
                    cloudSpeed = 7.5f,
                    skySpeed = 1.25f,
                    horizonProtection = 0.30f,
                    trailStrength = 0.62f,
                    turbulence = 0.45f,
                    skyTintStrength = 0.30f,
                    cloudGlow = 0.65f,
                    shutterPulse = 0.22f,
                    noise = 0.11f,
                    tintA = new Color(0.42f, 0.70f, 1.00f),
                    tintB = new Color(0.70f, 0.95f, 0.42f),
                    tintC = new Color(1.00f, 0.70f, 0.92f)
                };

            case CloudPreset.ImpossibleWeather:
                return new CloudLook
                {
                    intensity = 1.05f,
                    cloudSpeed = 10.0f,
                    skySpeed = 2.8f,
                    horizonProtection = 0.22f,
                    trailStrength = 0.90f,
                    turbulence = 0.75f,
                    skyTintStrength = 0.55f,
                    cloudGlow = 1.15f,
                    shutterPulse = 0.48f,
                    noise = 0.18f,
                    tintA = new Color(0.20f, 0.62f, 1.00f),
                    tintB = new Color(0.72f, 1.00f, 0.30f),
                    tintC = new Color(1.00f, 0.55f, 0.92f)
                };

            case CloudPreset.SlowDrift:
                return new CloudLook
                {
                    intensity = 0.58f,
                    cloudSpeed = 1.15f,
                    skySpeed = 0.22f,
                    horizonProtection = 0.36f,
                    trailStrength = 0.18f,
                    turbulence = 0.18f,
                    skyTintStrength = 0.18f,
                    cloudGlow = 0.34f,
                    shutterPulse = 0.04f,
                    noise = 0.06f,
                    tintA = new Color(0.42f, 0.58f, 0.72f),
                    tintB = new Color(0.48f, 0.62f, 0.50f),
                    tintC = new Color(0.62f, 0.58f, 0.68f)
                };

            default:
                return new CloudLook
                {
                    intensity = 0.90f,
                    cloudSpeed = 8.5f,
                    skySpeed = 1.8f,
                    horizonProtection = 0.28f,
                    trailStrength = 0.74f,
                    turbulence = 0.56f,
                    skyTintStrength = 0.38f,
                    cloudGlow = 0.82f,
                    shutterPulse = 0.32f,
                    noise = 0.12f,
                    tintA = new Color(0.38f, 0.70f, 1.00f),
                    tintB = new Color(0.70f, 0.98f, 0.42f),
                    tintC = new Color(1.00f, 0.62f, 0.95f)
                };
        }
    }
}
