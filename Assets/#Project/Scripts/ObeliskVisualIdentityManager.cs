using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class ObeliskVisualIdentityManager : MonoBehaviour
{
    public enum VisualPreset
    {
        Normal,
        NearObelisk,
        FarFromObelisk,
        Silence,
        CorruptedSignal,
        ChateauTruth,
        GlitchEvent,
        AfterDisc
    }

    [Serializable]
    private struct Look
    {
        public float oldDigital;
        public float contrast;
        public float saturation;
        public float darkCrush;

        public Color dreamColor;
        public float dreamStrength;
        public float skyAbnormal;
        public float greenPoison;

        public float grain;
        public float vignette;
        public float chromatic;
        public float microCut;
        public float anomaly;
        public float posterize;
        public float breath;
    }

    [Serializable]
    private class RoomVisualOverride
    {
        [Tooltip("Nom exact de la room, par exemple Ob_02, PRA_01, CHA_FAR.")]
        public string roomId = "Ob_02";

        public bool enabled = true;

        [Header("Preset local")]
        public bool overridePreset = false;
        public VisualPreset preset = VisualPreset.Normal;

        [Range(0f, 3f)]
        public float masterIntensityMultiplier = 1f;

        [Header("Multiplicateurs locaux")]
        [Range(0f, 3f)] public float oldDigitalMultiplier = 1f;
        [Range(0f, 3f)] public float contrastMultiplier = 1f;
        [Range(0f, 3f)] public float saturationMultiplier = 1f;
        [Range(0f, 3f)] public float darkCrushMultiplier = 1f;

        [Range(0f, 5f)] public float dreamStrengthMultiplier = 1f;
        [Range(0f, 5f)] public float skyAbnormalMultiplier = 1f;
        [Range(0f, 5f)] public float greenPoisonMultiplier = 1f;

        [Range(0f, 5f)] public float grainMultiplier = 1f;
        [Range(0f, 5f)] public float vignetteMultiplier = 1f;
        [Range(0f, 5f)] public float chromaticMultiplier = 1f;
        [Range(0f, 5f)] public float microCutMultiplier = 1f;
        [Range(0f, 5f)] public float anomalyMultiplier = 1f;
        [Range(0f, 5f)] public float posterizeMultiplier = 1f;
        [Range(0f, 5f)] public float breathMultiplier = 1f;

        [Header("Boosts locaux")]
        [Range(-1f, 1f)] public float oldDigitalAdd = 0f;
        [Range(-1f, 1f)] public float contrastAdd = 0f;
        [Range(-1f, 1f)] public float saturationAdd = 0f;
        [Range(-1f, 1f)] public float darkCrushAdd = 0f;

        [Range(-1f, 1f)] public float dreamStrengthAdd = 0f;
        [Range(-1f, 1f)] public float skyAbnormalAdd = 0f;
        [Range(-1f, 1f)] public float greenPoisonAdd = 0f;

        [Range(-1f, 1f)] public float grainAdd = 0f;
        [Range(-1f, 1f)] public float vignetteAdd = 0f;
        [Range(-1f, 1f)] public float chromaticAdd = 0f;
        [Range(-1f, 1f)] public float microCutAdd = 0f;
        [Range(-1f, 1f)] public float anomalyAdd = 0f;
        [Range(-1f, 1f)] public float posterizeAdd = 0f;
        [Range(-1f, 1f)] public float breathAdd = 0f;

        [Header("Couleur locale")]
        public Color dreamColor = new Color(1f, 0.86f, 0.42f, 1f);

        [Range(0f, 1f)]
        public float dreamColorMix = 0f;
    }

    [Header("Shader / Material")]
    [SerializeField] private Shader roomUnifiedLookShader;
    [SerializeField] private bool createMaterialOnStart = true;
    [SerializeField] private bool restoreOriginalMaterialsOnDisable = true;

    [Header("Images à styliser")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private bool autoFindRoomImages = true;
    [SerializeField] private bool onlyKnownRoomNames = true;
    [SerializeField] private List<Image> roomImages = new List<Image>();

    [Header("Identité visuelle")]
    [SerializeField] private VisualPreset preset = VisualPreset.Normal;

    [Range(0.1f, 12f)]
    [SerializeField] private float transitionSpeed = 3.5f;

    [Range(0f, 3f)]
    [SerializeField] private float masterIntensity = 1.0f;

    [SerializeField] private int randomSeed = 12345;

    [Header("Customisation avancée - multiplicateurs")]
    [Tooltip("Active les sliders ci-dessous. Désactive-le pour revenir aux presets bruts.")]
    [SerializeField] private bool enableAdvancedCustomization = true;

    [Range(0f, 3f)] [SerializeField] private float oldDigitalMultiplier = 1f;
    [Range(0f, 3f)] [SerializeField] private float contrastMultiplier = 1f;
    [Range(0f, 3f)] [SerializeField] private float saturationMultiplier = 1f;
    [Range(0f, 3f)] [SerializeField] private float darkCrushMultiplier = 1f;

    [Range(0f, 5f)] [SerializeField] private float dreamStrengthMultiplier = 1f;
    [Range(0f, 5f)] [SerializeField] private float skyAbnormalMultiplier = 1f;
    [Range(0f, 5f)] [SerializeField] private float greenPoisonMultiplier = 1f;

    [Range(0f, 5f)] [SerializeField] private float grainMultiplier = 1f;
    [Range(0f, 5f)] [SerializeField] private float vignetteMultiplier = 1f;
    [Range(0f, 5f)] [SerializeField] private float chromaticMultiplier = 1f;
    [Range(0f, 5f)] [SerializeField] private float microCutMultiplier = 1f;
    [Range(0f, 5f)] [SerializeField] private float anomalyMultiplier = 1f;
    [Range(0f, 5f)] [SerializeField] private float posterizeMultiplier = 1f;
    [Range(0f, 5f)] [SerializeField] private float breathMultiplier = 1f;

    [Header("Customisation avancée - boosts additifs")]
    [Range(-1f, 1f)] [SerializeField] private float oldDigitalAdd = 0f;
    [Range(-1f, 1f)] [SerializeField] private float contrastAdd = 0f;
    [Range(-1f, 1f)] [SerializeField] private float saturationAdd = 0f;
    [Range(-1f, 1f)] [SerializeField] private float darkCrushAdd = 0f;

    [Range(-1f, 1f)] [SerializeField] private float dreamStrengthAdd = 0f;
    [Range(-1f, 1f)] [SerializeField] private float skyAbnormalAdd = 0f;
    [Range(-1f, 1f)] [SerializeField] private float greenPoisonAdd = 0f;

    [Range(-1f, 1f)] [SerializeField] private float grainAdd = 0f;
    [Range(-1f, 1f)] [SerializeField] private float vignetteAdd = 0f;
    [Range(-1f, 1f)] [SerializeField] private float chromaticAdd = 0f;
    [Range(-1f, 1f)] [SerializeField] private float microCutAdd = 0f;
    [Range(-1f, 1f)] [SerializeField] private float anomalyAdd = 0f;
    [Range(-1f, 1f)] [SerializeField] private float posterizeAdd = 0f;
    [Range(-1f, 1f)] [SerializeField] private float breathAdd = 0f;

    [Header("Customisation avancée - couleur")]
    [SerializeField] private Color customDreamColor = new Color(1.0f, 0.86f, 0.42f, 1f);

    [Range(0f, 1f)]
    [SerializeField] private float customDreamColorMix = 0f;

    [Header("Auto selon la room actuelle")]
    [SerializeField] private bool autoReadCurrentRoom = true;
    [SerializeField] private bool autoPresetFromRoomId = true;
    [SerializeField] private MonoBehaviour backgroundManagerLike;
    [SerializeField] private string currentRoomId = "";

    [Header("Overrides par room")]
    [Tooltip("Active les réglages locaux par room. Les sliders globaux restent la base, puis l'override de la room actuelle s'ajoute par-dessus.")]
    [SerializeField] private bool enableRoomOverrides = true;

    [Tooltip("Sécurité : si Unity met les multiplicateurs d'un nouvel override à 0, on les considère comme 1 pour éviter une room blanche/grise.")]
    [SerializeField] private bool zeroRoomOverrideMultiplierMeansNeutral = true;

    [Tooltip("Chaque entrée ne s'applique que quand Current Room Id correspond au Room Id.")]
    [SerializeField] private List<RoomVisualOverride> roomOverrides = new List<RoomVisualOverride>();

    [Header("Debug")]
    [SerializeField] private bool logFoundImages = true;

    private Material runtimeMaterial;
    private readonly Dictionary<Image, Material> originalMaterials = new Dictionary<Image, Material>();

    private Look currentLook;
    private Look targetLook;

    private static readonly string[] KnownRoomIds =
    {
        "Ob_01",
        "Ob_02",
        "PRA_01",
        "LAC_A1",
        "LAC_A2",
        "LAC_01",
        "FOR_L1",
        "FOR_L2",
        "SIL_01",
        "SIL_02",
        "SIL_03",
        "FOR_01",
        "FOR_02",
        "CHA_FAR",
        "CHA_NEAR",
        "CHA_INT_01",
        "CHA_INT_02"
    };

    private static readonly int OldDigitalId = Shader.PropertyToID("_OldDigital");
    private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
    private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    private static readonly int DarkCrushId = Shader.PropertyToID("_DarkCrush");

    private static readonly int DreamColorId = Shader.PropertyToID("_DreamColor");
    private static readonly int DreamStrengthId = Shader.PropertyToID("_DreamStrength");
    private static readonly int SkyAbnormalId = Shader.PropertyToID("_SkyAbnormal");
    private static readonly int GreenPoisonId = Shader.PropertyToID("_GreenPoison");

    private static readonly int GrainId = Shader.PropertyToID("_Grain");
    private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
    private static readonly int ChromaticId = Shader.PropertyToID("_Chromatic");
    private static readonly int MicroCutId = Shader.PropertyToID("_MicroCut");
    private static readonly int AnomalyId = Shader.PropertyToID("_Anomaly");
    private static readonly int PosterizeId = Shader.PropertyToID("_Posterize");

    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int ObeliskTimeId = Shader.PropertyToID("_ObeliskTime");
    private static readonly int BreathId = Shader.PropertyToID("_Breath");

    private void Awake()
    {
        Setup();
    }

    private void OnEnable()
    {
        Setup();
    }

    private void Start()
    {
        Setup();
        targetLook = BuildTargetLookForCurrentRoom();
        currentLook = targetLook;
        ApplyLookToMaterial(currentLook);
    }

    private void OnDisable()
    {
        if (!restoreOriginalMaterialsOnDisable)
            return;

        foreach (KeyValuePair<Image, Material> pair in originalMaterials)
        {
            if (pair.Key != null)
                pair.Key.material = pair.Value;
        }
    }

    private void Update()
    {
        if (runtimeMaterial == null)
            return;

        if (autoReadCurrentRoom)
            TryUpdateCurrentRoomId();

        targetLook = BuildTargetLookForCurrentRoom();

        float t = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);
        currentLook = LerpLook(currentLook, targetLook, t);

        ApplyLookToMaterial(currentLook);

        runtimeMaterial.SetFloat(SeedId, randomSeed);
        runtimeMaterial.SetFloat(ObeliskTimeId, Application.isPlaying ? Time.time : 0f);
    }

    [ContextMenu("OBELISK / Setup Visual Identity")]
    public void Setup()
    {
        if (roomUnifiedLookShader == null)
            roomUnifiedLookShader = Shader.Find("Obelisk/UI/Room Unified Look");

        if (roomUnifiedLookShader == null)
        {
            Debug.LogError("[ObeliskVisualIdentityManager] Shader introuvable. Vérifie que ObeliskUIRoomUnifiedLook.shader est dans Assets.");
            return;
        }

        if (targetCanvas == null)
            targetCanvas = FindAnyObjectByType<Canvas>();

        if (autoFindRoomImages)
            AutoFindImages();

        if (createMaterialOnStart && runtimeMaterial == null)
        {
            runtimeMaterial = new Material(roomUnifiedLookShader);
            runtimeMaterial.name = "Obelisk_RoomUnifiedLook_Runtime";
        }

        ApplyMaterialToImages();

        if (backgroundManagerLike == null)
            backgroundManagerLike = FindBackgroundManagerLike();

        targetLook = BuildTargetLookForCurrentRoom();
        currentLook = targetLook;

        ApplyLookToMaterial(currentLook);
    }

    [ContextMenu("OBELISK / Refresh Room Image List")]
    public void AutoFindImages()
    {
        roomImages.RemoveAll(image => image == null);

        if (targetCanvas == null)
            targetCanvas = FindAnyObjectByType<Canvas>();

        if (targetCanvas == null)
        {
            Debug.LogWarning("[ObeliskVisualIdentityManager] Aucun Canvas trouvé.");
            return;
        }

        Image[] foundImages = targetCanvas.GetComponentsInChildren<Image>(true);

        foreach (Image image in foundImages)
        {
            if (image == null)
                continue;

            if (ShouldIgnoreImage(image))
                continue;

            if (onlyKnownRoomNames && !IsKnownRoomName(image.gameObject.name))
                continue;

            if (!roomImages.Contains(image))
                roomImages.Add(image);
        }

        if (logFoundImages)
            Debug.Log("[ObeliskVisualIdentityManager] Images room trouvées : " + roomImages.Count);
    }

    [ContextMenu("OBELISK / Apply Material To Rooms")]
    public void ApplyMaterialToImages()
    {
        if (runtimeMaterial == null)
            return;

        foreach (Image image in roomImages)
        {
            if (image == null)
                continue;

            if (!originalMaterials.ContainsKey(image))
                originalMaterials.Add(image, image.material);

            image.material = runtimeMaterial;
        }
    }

    public void SetCurrentRoomId(string roomId)
    {
        currentRoomId = roomId;
    }

    public void SetPresetNormal()
    {
        preset = VisualPreset.Normal;
        autoPresetFromRoomId = false;
    }

    public void SetPresetGlitchEvent()
    {
        preset = VisualPreset.GlitchEvent;
        autoPresetFromRoomId = false;
    }

    public void SetPresetAfterDisc()
    {
        preset = VisualPreset.AfterDisc;
        autoPresetFromRoomId = false;
    }

    public void SetMasterIntensity(float value)
    {
        masterIntensity = Mathf.Max(0f, value);
    }

    private bool ShouldIgnoreImage(Image image)
    {
        string n = image.gameObject.name.ToLowerInvariant();

        if (n.Contains("zone")) return true;
        if (n.Contains("fade")) return true;
        if (n.Contains("overlay")) return true;
        if (n.Contains("button")) return true;
        if (n.Contains("cursor")) return true;

        return false;
    }

    private bool IsKnownRoomName(string objectName)
    {
        for (int i = 0; i < KnownRoomIds.Length; i++)
        {
            if (objectName == KnownRoomIds[i])
                return true;
        }

        return false;
    }

    private MonoBehaviour FindBackgroundManagerLike()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            MethodInfo method = behaviour.GetType().GetMethod("GetCurrentRoomId", BindingFlags.Public | BindingFlags.Instance);

            if (method != null && method.ReturnType == typeof(string))
                return behaviour;
        }

        return null;
    }

    private void TryUpdateCurrentRoomId()
    {
        if (backgroundManagerLike == null)
            return;

        MethodInfo method = backgroundManagerLike.GetType().GetMethod("GetCurrentRoomId", BindingFlags.Public | BindingFlags.Instance);

        if (method == null)
            return;

        object result = method.Invoke(backgroundManagerLike, null);

        if (result is string id)
            currentRoomId = id;
    }

    private VisualPreset GuessPresetFromRoomId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return preset;

        if (id == "Ob_02")
            return VisualPreset.NearObelisk;

        if (id.StartsWith("SIL_", StringComparison.Ordinal))
            return VisualPreset.Silence;

        if (id == "CHA_INT_01" || id == "CHA_INT_02")
            return VisualPreset.ChateauTruth;

        if (id == "CHA_FAR" || id == "CHA_NEAR")
            return VisualPreset.FarFromObelisk;

        if (id == "LAC_01")
            return VisualPreset.FarFromObelisk;

        return VisualPreset.Normal;
    }

    private Look GetLookForPreset(VisualPreset visualPreset)
    {
        switch (visualPreset)
        {
            case VisualPreset.NearObelisk:
                return new Look
                {
                    oldDigital = 1.05f,
                    contrast = 1.14f,
                    saturation = 1.12f,
                    darkCrush = 0.26f,
                    dreamColor = new Color(1.0f, 0.82f, 0.38f, 1f),
                    dreamStrength = 0.13f,
                    skyAbnormal = 0.09f,
                    greenPoison = 0.05f,
                    grain = 0.14f,
                    vignette = 0.24f,
                    chromatic = 0.012f,
                    microCut = 0.0f,
                    anomaly = 0.0f,
                    posterize = 0.0f,
                    breath = 0.35f
                };

            case VisualPreset.FarFromObelisk:
                return new Look
                {
                    oldDigital = 1.10f,
                    contrast = 1.12f,
                    saturation = 1.00f,
                    darkCrush = 0.31f,
                    dreamColor = new Color(0.55f, 0.70f, 0.82f, 1f),
                    dreamStrength = 0.10f,
                    skyAbnormal = 0.16f,
                    greenPoison = 0.08f,
                    grain = 0.22f,
                    vignette = 0.34f,
                    chromatic = 0.020f,
                    microCut = 0.012f,
                    anomaly = 0.015f,
                    posterize = 0.00f,
                    breath = 0.22f
                };

            case VisualPreset.Silence:
                return new Look
                {
                    oldDigital = 1.20f,
                    contrast = 1.05f,
                    saturation = 0.78f,
                    darkCrush = 0.40f,
                    dreamColor = new Color(0.47f, 0.55f, 0.62f, 1f),
                    dreamStrength = 0.14f,
                    skyAbnormal = 0.20f,
                    greenPoison = 0.04f,
                    grain = 0.30f,
                    vignette = 0.46f,
                    chromatic = 0.018f,
                    microCut = 0.018f,
                    anomaly = 0.035f,
                    posterize = 0.04f,
                    breath = 0.10f
                };

            case VisualPreset.CorruptedSignal:
                return new Look
                {
                    oldDigital = 1.25f,
                    contrast = 1.20f,
                    saturation = 1.22f,
                    darkCrush = 0.33f,
                    dreamColor = new Color(0.95f, 0.80f, 0.24f, 1f),
                    dreamStrength = 0.20f,
                    skyAbnormal = 0.36f,
                    greenPoison = 0.24f,
                    grain = 0.24f,
                    vignette = 0.36f,
                    chromatic = 0.045f,
                    microCut = 0.050f,
                    anomaly = 0.12f,
                    posterize = 0.05f,
                    breath = 0.55f
                };

            case VisualPreset.ChateauTruth:
                return new Look
                {
                    oldDigital = 1.05f,
                    contrast = 1.08f,
                    saturation = 0.86f,
                    darkCrush = 0.30f,
                    dreamColor = new Color(0.76f, 0.78f, 0.70f, 1f),
                    dreamStrength = 0.08f,
                    skyAbnormal = 0.04f,
                    greenPoison = 0.00f,
                    grain = 0.18f,
                    vignette = 0.38f,
                    chromatic = 0.010f,
                    microCut = 0.010f,
                    anomaly = 0.025f,
                    posterize = 0.02f,
                    breath = 0.04f
                };

            case VisualPreset.GlitchEvent:
                return new Look
                {
                    oldDigital = 1.35f,
                    contrast = 1.25f,
                    saturation = 1.35f,
                    darkCrush = 0.42f,
                    dreamColor = new Color(0.70f, 0.92f, 0.42f, 1f),
                    dreamStrength = 0.26f,
                    skyAbnormal = 0.62f,
                    greenPoison = 0.38f,
                    grain = 0.34f,
                    vignette = 0.45f,
                    chromatic = 0.085f,
                    microCut = 0.16f,
                    anomaly = 0.36f,
                    posterize = 0.14f,
                    breath = 0.85f
                };

            case VisualPreset.AfterDisc:
                return new Look
                {
                    oldDigital = 0.90f,
                    contrast = 1.00f,
                    saturation = 0.60f,
                    darkCrush = 0.18f,
                    dreamColor = new Color(0.86f, 0.88f, 0.80f, 1f),
                    dreamStrength = 0.04f,
                    skyAbnormal = 0.00f,
                    greenPoison = 0.00f,
                    grain = 0.10f,
                    vignette = 0.18f,
                    chromatic = 0.000f,
                    microCut = 0.000f,
                    anomaly = 0.000f,
                    posterize = 0.00f,
                    breath = 0.0f
                };

            default:
                return new Look
                {
                    oldDigital = 1.00f,
                    contrast = 1.12f,
                    saturation = 1.08f,
                    darkCrush = 0.25f,
                    dreamColor = new Color(1.0f, 0.86f, 0.42f, 1f),
                    dreamStrength = 0.08f,
                    skyAbnormal = 0.07f,
                    greenPoison = 0.04f,
                    grain = 0.18f,
                    vignette = 0.30f,
                    chromatic = 0.010f,
                    microCut = 0.000f,
                    anomaly = 0.000f,
                    posterize = 0.000f,
                    breath = 0.20f
                };
        }
    }


    private Look BuildTargetLookForCurrentRoom()
    {
        VisualPreset activePreset = preset;
        RoomVisualOverride roomOverride = FindRoomOverride(currentRoomId);

        if (autoPresetFromRoomId)
            activePreset = GuessPresetFromRoomId(currentRoomId);

        if (roomOverride != null && roomOverride.enabled && roomOverride.overridePreset)
            activePreset = roomOverride.preset;

        Look look = GetLookForPreset(activePreset);
        look = ApplyMasterIntensity(look, masterIntensity);
        look = ApplyCustomSliders(look);
        look = ApplyRoomOverride(look, roomOverride);

        return ClampLook(look);
    }

    private RoomVisualOverride FindRoomOverride(string roomId)
    {
        if (!enableRoomOverrides)
            return null;

        if (string.IsNullOrEmpty(roomId))
            return null;

        for (int i = 0; i < roomOverrides.Count; i++)
        {
            RoomVisualOverride roomOverride = roomOverrides[i];

            if (roomOverride == null)
                continue;

            if (!roomOverride.enabled)
                continue;

            if (roomOverride.roomId == roomId)
                return roomOverride;
        }

        return null;
    }

    private Look ApplyRoomOverride(Look look, RoomVisualOverride roomOverride)
    {
        if (roomOverride == null)
            return look;

        look = ApplyMasterIntensity(look, GetRoomMultiplier(roomOverride.masterIntensityMultiplier));

        look.oldDigital = look.oldDigital * GetRoomMultiplier(roomOverride.oldDigitalMultiplier) + roomOverride.oldDigitalAdd;
        look.contrast = look.contrast * GetRoomMultiplier(roomOverride.contrastMultiplier) + roomOverride.contrastAdd;
        look.saturation = look.saturation * GetRoomMultiplier(roomOverride.saturationMultiplier) + roomOverride.saturationAdd;
        look.darkCrush = look.darkCrush * GetRoomMultiplier(roomOverride.darkCrushMultiplier) + roomOverride.darkCrushAdd;

        look.dreamStrength = look.dreamStrength * GetRoomMultiplier(roomOverride.dreamStrengthMultiplier) + roomOverride.dreamStrengthAdd;
        look.skyAbnormal = look.skyAbnormal * GetRoomMultiplier(roomOverride.skyAbnormalMultiplier) + roomOverride.skyAbnormalAdd;
        look.greenPoison = look.greenPoison * GetRoomMultiplier(roomOverride.greenPoisonMultiplier) + roomOverride.greenPoisonAdd;

        look.grain = look.grain * GetRoomMultiplier(roomOverride.grainMultiplier) + roomOverride.grainAdd;
        look.vignette = look.vignette * GetRoomMultiplier(roomOverride.vignetteMultiplier) + roomOverride.vignetteAdd;
        look.chromatic = look.chromatic * GetRoomMultiplier(roomOverride.chromaticMultiplier) + roomOverride.chromaticAdd;
        look.microCut = look.microCut * GetRoomMultiplier(roomOverride.microCutMultiplier) + roomOverride.microCutAdd;
        look.anomaly = look.anomaly * GetRoomMultiplier(roomOverride.anomalyMultiplier) + roomOverride.anomalyAdd;
        look.posterize = look.posterize * GetRoomMultiplier(roomOverride.posterizeMultiplier) + roomOverride.posterizeAdd;
        look.breath = look.breath * GetRoomMultiplier(roomOverride.breathMultiplier) + roomOverride.breathAdd;

        look.dreamColor = Color.Lerp(look.dreamColor, roomOverride.dreamColor, roomOverride.dreamColorMix);

        return ClampLook(look);
    }

    private float GetRoomMultiplier(float value)
    {
        if (zeroRoomOverrideMultiplierMeansNeutral && Mathf.Approximately(value, 0f))
            return 1f;

        return value;
    }

    [ContextMenu("OBELISK / Add Current Room Override")]
    public void AddCurrentRoomOverride()
    {
        if (autoReadCurrentRoom)
            TryUpdateCurrentRoomId();

        if (string.IsNullOrEmpty(currentRoomId))
        {
            Debug.LogWarning("[ObeliskVisualIdentityManager] Room actuelle inconnue. Lance le jeu ou renseigne Current Room Id.");
            return;
        }

        RoomVisualOverride existing = FindRoomOverride(currentRoomId);

        if (existing != null)
        {
            Debug.Log("[ObeliskVisualIdentityManager] Override existe déjà pour : " + currentRoomId);
            return;
        }

        RoomVisualOverride roomOverride = new RoomVisualOverride();
        roomOverride.roomId = currentRoomId;
        roomOverride.enabled = true;
        roomOverride.overridePreset = false;
        roomOverride.preset = autoPresetFromRoomId ? GuessPresetFromRoomId(currentRoomId) : preset;
        roomOverride.dreamColor = customDreamColor;

        roomOverrides.Add(roomOverride);

        Debug.Log("[ObeliskVisualIdentityManager] Override ajouté pour : " + currentRoomId);
    }

    [ContextMenu("OBELISK / Add Ob_02 Override")]
    public void AddOb02Override()
    {
        AddSpecificRoomOverride("Ob_02");
    }

    [ContextMenu("OBELISK / Add PRA_01 Override")]
    public void AddPRA01Override()
    {
        AddSpecificRoomOverride("PRA_01");
    }

    private void AddSpecificRoomOverride(string roomId)
    {
        if (string.IsNullOrEmpty(roomId))
            return;

        for (int i = 0; i < roomOverrides.Count; i++)
        {
            if (roomOverrides[i] != null && roomOverrides[i].roomId == roomId)
            {
                Debug.Log("[ObeliskVisualIdentityManager] Override existe déjà pour : " + roomId);
                return;
            }
        }

        RoomVisualOverride roomOverride = new RoomVisualOverride();
        roomOverride.roomId = roomId;
        roomOverride.enabled = true;
        roomOverride.overridePreset = false;
        roomOverride.preset = GuessPresetFromRoomId(roomId);
        roomOverride.dreamColor = customDreamColor;

        roomOverrides.Add(roomOverride);

        Debug.Log("[ObeliskVisualIdentityManager] Override ajouté pour : " + roomId);
    }

    [ContextMenu("OBELISK / Repair Room Override Multipliers")]
    public void RepairRoomOverrideMultipliers()
    {
        for (int i = 0; i < roomOverrides.Count; i++)
        {
            RoomVisualOverride roomOverride = roomOverrides[i];

            if (roomOverride == null)
                continue;

            if (Mathf.Approximately(roomOverride.masterIntensityMultiplier, 0f)) roomOverride.masterIntensityMultiplier = 1f;

            if (Mathf.Approximately(roomOverride.oldDigitalMultiplier, 0f)) roomOverride.oldDigitalMultiplier = 1f;
            if (Mathf.Approximately(roomOverride.contrastMultiplier, 0f)) roomOverride.contrastMultiplier = 1f;
            if (Mathf.Approximately(roomOverride.saturationMultiplier, 0f)) roomOverride.saturationMultiplier = 1f;
            if (Mathf.Approximately(roomOverride.darkCrushMultiplier, 0f)) roomOverride.darkCrushMultiplier = 1f;

            if (Mathf.Approximately(roomOverride.dreamStrengthMultiplier, 0f)) roomOverride.dreamStrengthMultiplier = 1f;
            if (Mathf.Approximately(roomOverride.skyAbnormalMultiplier, 0f)) roomOverride.skyAbnormalMultiplier = 1f;
            if (Mathf.Approximately(roomOverride.greenPoisonMultiplier, 0f)) roomOverride.greenPoisonMultiplier = 1f;

            if (Mathf.Approximately(roomOverride.grainMultiplier, 0f)) roomOverride.grainMultiplier = 1f;
            if (Mathf.Approximately(roomOverride.vignetteMultiplier, 0f)) roomOverride.vignetteMultiplier = 1f;
            if (Mathf.Approximately(roomOverride.chromaticMultiplier, 0f)) roomOverride.chromaticMultiplier = 1f;
            if (Mathf.Approximately(roomOverride.microCutMultiplier, 0f)) roomOverride.microCutMultiplier = 1f;
            if (Mathf.Approximately(roomOverride.anomalyMultiplier, 0f)) roomOverride.anomalyMultiplier = 1f;
            if (Mathf.Approximately(roomOverride.posterizeMultiplier, 0f)) roomOverride.posterizeMultiplier = 1f;
            if (Mathf.Approximately(roomOverride.breathMultiplier, 0f)) roomOverride.breathMultiplier = 1f;
        }

        Debug.Log("[ObeliskVisualIdentityManager] Multiplicateurs d'overrides réparés : 0 → 1.");
    }

    private Look ApplyCustomSliders(Look look)
    {
        if (!enableAdvancedCustomization)
            return ClampLook(look);

        look.oldDigital = look.oldDigital * oldDigitalMultiplier + oldDigitalAdd;
        look.contrast = look.contrast * contrastMultiplier + contrastAdd;
        look.saturation = look.saturation * saturationMultiplier + saturationAdd;
        look.darkCrush = look.darkCrush * darkCrushMultiplier + darkCrushAdd;

        look.dreamStrength = look.dreamStrength * dreamStrengthMultiplier + dreamStrengthAdd;
        look.skyAbnormal = look.skyAbnormal * skyAbnormalMultiplier + skyAbnormalAdd;
        look.greenPoison = look.greenPoison * greenPoisonMultiplier + greenPoisonAdd;

        look.grain = look.grain * grainMultiplier + grainAdd;
        look.vignette = look.vignette * vignetteMultiplier + vignetteAdd;
        look.chromatic = look.chromatic * chromaticMultiplier + chromaticAdd;
        look.microCut = look.microCut * microCutMultiplier + microCutAdd;
        look.anomaly = look.anomaly * anomalyMultiplier + anomalyAdd;
        look.posterize = look.posterize * posterizeMultiplier + posterizeAdd;
        look.breath = look.breath * breathMultiplier + breathAdd;

        look.dreamColor = Color.Lerp(look.dreamColor, customDreamColor, customDreamColorMix);

        return ClampLook(look);
    }

    private Look ClampLook(Look look)
    {
        look.oldDigital = Mathf.Clamp(look.oldDigital, 0f, 2f);
        look.contrast = Mathf.Clamp(look.contrast, 0f, 2f);
        look.saturation = Mathf.Clamp(look.saturation, 0f, 2f);
        look.darkCrush = Mathf.Clamp01(look.darkCrush);

        look.dreamStrength = Mathf.Clamp01(look.dreamStrength);
        look.skyAbnormal = Mathf.Clamp01(look.skyAbnormal);
        look.greenPoison = Mathf.Clamp01(look.greenPoison);

        look.grain = Mathf.Clamp01(look.grain);
        look.vignette = Mathf.Clamp01(look.vignette);
        look.chromatic = Mathf.Clamp01(look.chromatic);
        look.microCut = Mathf.Clamp01(look.microCut);
        look.anomaly = Mathf.Clamp01(look.anomaly);
        look.posterize = Mathf.Clamp01(look.posterize);
        look.breath = Mathf.Clamp01(look.breath);

        return look;
    }

    [ContextMenu("OBELISK / Reset Advanced Custom Sliders")]
    public void ResetAdvancedCustomSliders()
    {
        oldDigitalMultiplier = 1f;
        contrastMultiplier = 1f;
        saturationMultiplier = 1f;
        darkCrushMultiplier = 1f;

        dreamStrengthMultiplier = 1f;
        skyAbnormalMultiplier = 1f;
        greenPoisonMultiplier = 1f;

        grainMultiplier = 1f;
        vignetteMultiplier = 1f;
        chromaticMultiplier = 1f;
        microCutMultiplier = 1f;
        anomalyMultiplier = 1f;
        posterizeMultiplier = 1f;
        breathMultiplier = 1f;

        oldDigitalAdd = 0f;
        contrastAdd = 0f;
        saturationAdd = 0f;
        darkCrushAdd = 0f;

        dreamStrengthAdd = 0f;
        skyAbnormalAdd = 0f;
        greenPoisonAdd = 0f;

        grainAdd = 0f;
        vignetteAdd = 0f;
        chromaticAdd = 0f;
        microCutAdd = 0f;
        anomalyAdd = 0f;
        posterizeAdd = 0f;
        breathAdd = 0f;

        customDreamColorMix = 0f;
    }

    private Look ApplyMasterIntensity(Look look, float intensity)
    {
        intensity = Mathf.Max(0f, intensity);

        look.dreamStrength *= intensity;
        look.skyAbnormal *= intensity;
        look.greenPoison *= intensity;
        look.grain *= Mathf.Lerp(1f, intensity, 0.5f);
        look.vignette *= Mathf.Lerp(1f, intensity, 0.35f);
        look.chromatic *= intensity;
        look.microCut *= intensity;
        look.anomaly *= intensity;
        look.posterize *= intensity;
        look.breath *= intensity;

        return look;
    }

    private Look LerpLook(Look a, Look b, float t)
    {
        return new Look
        {
            oldDigital = Mathf.Lerp(a.oldDigital, b.oldDigital, t),
            contrast = Mathf.Lerp(a.contrast, b.contrast, t),
            saturation = Mathf.Lerp(a.saturation, b.saturation, t),
            darkCrush = Mathf.Lerp(a.darkCrush, b.darkCrush, t),

            dreamColor = Color.Lerp(a.dreamColor, b.dreamColor, t),
            dreamStrength = Mathf.Lerp(a.dreamStrength, b.dreamStrength, t),
            skyAbnormal = Mathf.Lerp(a.skyAbnormal, b.skyAbnormal, t),
            greenPoison = Mathf.Lerp(a.greenPoison, b.greenPoison, t),

            grain = Mathf.Lerp(a.grain, b.grain, t),
            vignette = Mathf.Lerp(a.vignette, b.vignette, t),
            chromatic = Mathf.Lerp(a.chromatic, b.chromatic, t),
            microCut = Mathf.Lerp(a.microCut, b.microCut, t),
            anomaly = Mathf.Lerp(a.anomaly, b.anomaly, t),
            posterize = Mathf.Lerp(a.posterize, b.posterize, t),
            breath = Mathf.Lerp(a.breath, b.breath, t)
        };
    }

    private void ApplyLookToMaterial(Look look)
    {
        if (runtimeMaterial == null)
            return;

        runtimeMaterial.SetFloat(OldDigitalId, look.oldDigital);
        runtimeMaterial.SetFloat(ContrastId, look.contrast);
        runtimeMaterial.SetFloat(SaturationId, look.saturation);
        runtimeMaterial.SetFloat(DarkCrushId, look.darkCrush);

        runtimeMaterial.SetColor(DreamColorId, look.dreamColor);
        runtimeMaterial.SetFloat(DreamStrengthId, look.dreamStrength);
        runtimeMaterial.SetFloat(SkyAbnormalId, look.skyAbnormal);
        runtimeMaterial.SetFloat(GreenPoisonId, look.greenPoison);

        runtimeMaterial.SetFloat(GrainId, look.grain);
        runtimeMaterial.SetFloat(VignetteId, look.vignette);
        runtimeMaterial.SetFloat(ChromaticId, look.chromatic);
        runtimeMaterial.SetFloat(MicroCutId, look.microCut);
        runtimeMaterial.SetFloat(AnomalyId, look.anomaly);
        runtimeMaterial.SetFloat(PosterizeId, look.posterize);
        runtimeMaterial.SetFloat(BreathId, look.breath);
    }
}
