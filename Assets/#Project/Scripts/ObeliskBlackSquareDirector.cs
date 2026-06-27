using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ObeliskBlackSquareDirector : MonoBehaviour
{
    public static ObeliskBlackSquareDirector Instance { get; private set; }

    public enum BlackSquareUnlockMode
    {
        Disabled,
        BypassApproachUnlocks,
        BypassApproachPlusClickUnlocks,
        ClicksOnly
    }

    [Serializable]
    public class EntityRoomRule
    {
        [Header("Room")]
        public string roomId = "";
        public bool enabled = true;
        public bool forceSpawn = false;
        public bool neverSpawn = false;

        [Range(0f, 1f)]
        public float spawnChance = 0.12f;

        [Header("Position / PNG brut")]
        public Vector2 center = Vector2.zero;
        public Vector2 randomPositionRange = new Vector2(250f, 125f);
        public Vector2 baseSize = new Vector2(165f, 245f);
        public Vector2 randomSizeRange = new Vector2(70f, 95f);

        [Tooltip("x = silhouette plus haute, y = silhouette plus large.")]
        public Vector2 aspectRange = new Vector2(0.48f, 0.95f);

        [Range(-12f, 12f)] public float minRotation = -2f;
        [Range(-12f, 12f)] public float maxRotation = 2f;

        [Header("Pixel vomit local")]
        [Tooltip("Chance que l'apparition soit accompagnée de pixels arrachés à l'image derrière.")]
        [Range(0f, 1f)] public float pixelVomitChance = 0.28f;

        [Tooltip("Puissance locale de la défiguration. 0 = aucun effet.")]
        [Range(0f, 2f)] public float pixelVomitStrength = 0.75f;

        [Tooltip("Taille du champ de pixels vomis autour de l'entité.")]
        [Range(0.5f, 4f)] public float pixelVomitRadius = 1.45f;

        [Tooltip("Opacité max de l'effet. Garde bas pour ne pas casser VisualIdentity.")]
        [Range(0f, 1f)] public float pixelVomitAlpha = 0.72f;

        [Header("Gameplay")]
        public bool raycastTarget = true;
        public bool holdsKey = false;
    }

    private struct Manifestation
    {
        public bool visible;
        public bool pixelVomit;
        public string roomId;
        public Vector2 position;
        public Vector2 size;
        public float rotation;
        public float seed;
        public float vomitStrength;
        public float vomitRadius;
        public float vomitAlpha;
        public bool raycastTarget;
        public bool holdsKey;
    }

    [Header("Références")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private BackgroundManager backgroundManager;
    [SerializeField] private Image fadeOverlay;

    [Header("Entité PNG brute")]
    [Tooltip("Optionnel. Si tu mets un sprite ici, il sera posé comme un PNG noir brut, sans shader stylisé.")]
    [SerializeField] private Sprite entitySprite;

    [Tooltip("Si Entity Sprite est vide, le script génère la silhouette pixel classique avec chapeau, sans yeux/bouche.")]
    [SerializeField] private bool useGeneratedClassicEntityWhenNoSprite = true;

    [Tooltip("Teinte de l'entité. Noir brut recommandé.")]
    [SerializeField] private Color entityColor = Color.black;

    [Header("Pixel vomit")]
    [Tooltip("Shader local qui affiche quelques pixels de la room arrachés autour de l'entité.")]
    [SerializeField] private Shader pixelVomitShader;

    [Tooltip("Désactive automatiquement l'effet si le shader est absent, au lieu d'afficher un rectangle blanc.")]
    [SerializeField] private bool disableVomitIfShaderMissing = true;

    [Header("Objets UI")]
    [SerializeField] private string entityObjectName = "Obelisk_BlackEntity_RawPNG";
    [SerializeField] private string pixelVomitObjectName = "Obelisk_BlackEntity_PixelVomit";
    [SerializeField] private string fadeOverlayObjectName = "FadeOverlay";
    [SerializeField] private bool createOnStart = true;
    [SerializeField] private bool keepBelowFadeOverlay = true;
    [SerializeField] private bool enforceOrderEveryFrame = true;

    [Header("Spawn")]
    [Tooltip("L'entité peut apparaître rarement dans toutes les rooms sans règle dédiée.")]
    [SerializeField] private bool canSpawnOnAnyRoom = true;

    [Range(0f, 1f)]
    [SerializeField] private float defaultSpawnChance = 0.055f;

    [Tooltip("La position / taille / effet changent uniquement quand on entre dans une room.")]
    [SerializeField] private bool rerollEveryRoomEntry = true;

    [Header("Règles par room")]
    [SerializeField] private bool autoAddDefaultRulesIfEmpty = true;
    [SerializeField] private List<EntityRoomRule> roomRules = new List<EntityRoomRule>();

    [Header("Gameplay carré noir")]
    [SerializeField] private bool enableNavigationGate = true;
    [SerializeField] private string blackSquareRoomId = "LAC_01";
    [SerializeField] private string mainApproachRoomId = "LAC_A2";
    [SerializeField] private string bypassApproachRoomId = "FOR_L2";
    [SerializeField] private string blockedTargetRoomId = "SIL_01";

    [Tooltip("Par défaut, le contournement ouvre directement. Pas de softlock.")]
    [SerializeField] private BlackSquareUnlockMode unlockMode = BlackSquareUnlockMode.BypassApproachUnlocks;

    [SerializeField] private bool bypassSeenCanAlwaysOpenPath = true;

    [Range(1, 8)]
    [SerializeField] private int clicksRequiredAfterBypass = 1;

    [Header("État")]
    [SerializeField] private bool persistStateWithPlayerPrefs = false;
    [SerializeField] private string saveKeyPrefix = "OBELISK_RAW_BLACK_ENTITY_V1";

    [Header("Events Unity optionnels")]
    public UnityEvent onFirstSeen;
    public UnityEvent onMainApproachDenied;
    public UnityEvent onBypassSeen;
    public UnityEvent onEntityClicked;
    public UnityEvent onPathUnlocked;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private Image entityImage;
    private RectTransform entityRect;
    private Image pixelVomitImage;
    private RectTransform pixelVomitRect;
    private Material pixelVomitMaterial;
    private ObeliskBlackSquareHitbox hitbox;

    private Texture2D generatedEntityTexture;
    private Sprite generatedEntitySprite;

    private string currentRoomId = "";
    private string previousRoomId = "";
    private string lastObservedRoomId = "";

    private bool hasBeenSeen = false;
    private bool hasBeenSeenFromMain = false;
    private bool hasBeenSeenFromBypass = false;
    private bool silencePathUnlocked = false;
    private int clicksAfterBypass = 0;

    private Manifestation manifestation;
    private bool hasManifestation = false;

    private Coroutine delayedSyncRoutine;

    private const string SeenKey = "_SEEN";
    private const string MainKey = "_MAIN";
    private const string BypassKey = "_BYPASS";
    private const string UnlockKey = "_UNLOCK";
    private const string ClicksKey = "_CLICKS";

    private static readonly int VEntityCenter = Shader.PropertyToID("_EntityCenter");
    private static readonly int VEntitySize = Shader.PropertyToID("_EntitySize");
    private static readonly int VSeed = Shader.PropertyToID("_Seed");
    private static readonly int VTime = Shader.PropertyToID("_ObeliskTime");
    private static readonly int VStrength = Shader.PropertyToID("_Strength");
    private static readonly int VRadius = Shader.PropertyToID("_Radius");
    private static readonly int VMaxAlpha = Shader.PropertyToID("_MaxAlpha");

    public bool IsSilencePathUnlocked => silencePathUnlocked;
    public bool HasBeenSeenFromBypass => hasBeenSeenFromBypass;
    public bool IsEntityVisible => manifestation.visible;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
        LoadState();
        Setup();
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        ResolveReferences();

        if (backgroundManager != null)
            backgroundManager.OnRoomChanged += HandleRoomChanged;

        ForceRefreshRoom();

        if (string.IsNullOrEmpty(currentRoomId))
            StartDelayedSync();
    }

    private void OnDisable()
    {
        if (backgroundManager != null)
            backgroundManager.OnRoomChanged -= HandleRoomChanged;

        SaveState();
    }

    private void Start()
    {
        ResolveReferences();
        Setup();

        if (autoAddDefaultRulesIfEmpty && roomRules.Count == 0)
            AddDefaultRules();

        ForceRefreshRoom();

        if (!string.IsNullOrEmpty(currentRoomId))
        {
            RollForCurrentRoom(false);
            ApplyVisibility();
            PrintDebugStatus();
        }
        else
        {
            HideEverything();
            StartDelayedSync();
        }
    }

    private void LateUpdate()
    {
        ResolveReferences();
        DetectRoomChangeWithoutEvent();

        if (enforceOrderEveryFrame)
            EnsureOrder();

        ApplyVisibility();
        UpdatePixelVomitMaterial();
    }

    [ContextMenu("OBELISK / Raw Entity Setup")]
    public void Setup()
    {
        ResolveReferences();

        if (!createOnStart || targetCanvas == null)
            return;

        if (pixelVomitShader == null)
            pixelVomitShader = Shader.Find("Obelisk/UI/Raw Pixel Vomit");

        CreatePixelVomitMaterial();
        CreateOrFindPixelVomitImage();
        CreateOrFindEntityImage();
        EnsureOrder();
    }

    [ContextMenu("OBELISK / Raw Entity Add Default Rules")]
    public void AddDefaultRules()
    {
        roomRules.Clear();

        roomRules.Add(new EntityRoomRule
        {
            roomId = "LAC_01",
            enabled = true,
            forceSpawn = true,
            spawnChance = 1f,
            center = new Vector2(40f, -4f),
            randomPositionRange = new Vector2(230f, 120f),
            baseSize = new Vector2(145f, 225f),
            randomSizeRange = new Vector2(58f, 82f),
            aspectRange = new Vector2(0.45f, 0.78f),
            minRotation = -1.5f,
            maxRotation = 1.5f,
            pixelVomitChance = 0.36f,
            pixelVomitStrength = 0.82f,
            pixelVomitRadius = 1.55f,
            pixelVomitAlpha = 0.74f,
            raycastTarget = true,
            holdsKey = true
        });

        roomRules.Add(new EntityRoomRule
        {
            roomId = "SIL_03",
            enabled = true,
            forceSpawn = true,
            spawnChance = 1f,
            center = Vector2.zero,
            randomPositionRange = new Vector2(85f, 55f),
            baseSize = new Vector2(160f, 245f),
            randomSizeRange = new Vector2(65f, 80f),
            aspectRange = new Vector2(0.42f, 0.82f),
            minRotation = -1f,
            maxRotation = 1f,
            pixelVomitChance = 0.55f,
            pixelVomitStrength = 1.05f,
            pixelVomitRadius = 1.70f,
            pixelVomitAlpha = 0.82f,
            raycastTarget = true,
            holdsKey = false
        });

        roomRules.Add(new EntityRoomRule
        {
            roomId = "Ob_02",
            enabled = true,
            forceSpawn = false,
            spawnChance = 0.25f,
            center = new Vector2(50f, -10f),
            randomPositionRange = new Vector2(160f, 85f),
            baseSize = new Vector2(130f, 210f),
            randomSizeRange = new Vector2(55f, 75f),
            aspectRange = new Vector2(0.43f, 0.80f),
            minRotation = -1.2f,
            maxRotation = 1.2f,
            pixelVomitChance = 0.32f,
            pixelVomitStrength = 0.75f,
            pixelVomitRadius = 1.45f,
            pixelVomitAlpha = 0.66f,
            raycastTarget = true
        });

        Debug.Log("[ObeliskRawEntity] Règles par défaut ajoutées.");

        ForceRefreshRoom();

        if (!string.IsNullOrEmpty(currentRoomId))
            RollForCurrentRoom(false);

        ApplyVisibility();
    }

    [ContextMenu("OBELISK / Raw Entity Reroll Current Room")]
    public void RerollCurrentRoom()
    {
        RollForCurrentRoom(true);
        ApplyVisibility();
        PrintDebugStatus();
    }

    [ContextMenu("OBELISK / Raw Entity Force Spawn Current Room")]
    public void ForceSpawnCurrentRoom()
    {
        string id = GetCurrentRoomIdSafe();

        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[ObeliskRawEntity] CurrentRoom inconnue.");
            return;
        }

        ForceSpawnSpecificRoom(id);
    }

    [ContextMenu("OBELISK / Raw Entity Force Spawn LAC_01")]
    public void ForceSpawnLAC01()
    {
        ForceSpawnSpecificRoom("LAC_01");
    }

    [ContextMenu("OBELISK / Raw Entity Hard Reset Rules + Force LAC_01")]
    public void HardResetRulesAndForceLAC01()
    {
        roomRules.Clear();
        AddDefaultRules();
        ResetEntityState();
        ForceSpawnSpecificRoom("LAC_01");
    }

    [ContextMenu("OBELISK / Raw Entity Reset State")]
    public void ResetEntityState()
    {
        hasBeenSeen = false;
        hasBeenSeenFromMain = false;
        hasBeenSeenFromBypass = false;
        silencePathUnlocked = false;
        clicksAfterBypass = 0;
        ClearPersistedState();

        ForceRefreshRoom();

        if (!string.IsNullOrEmpty(currentRoomId))
            RollForCurrentRoom(false);
        else
            HideEverything();

        ApplyVisibility();
        Debug.Log("[ObeliskRawEntity] État reset.");
    }

    [ContextMenu("OBELISK / Raw Entity Unlock Silence Path")]
    public void UnlockSilencePath()
    {
        UnlockNow("manuel");
    }

    [ContextMenu("OBELISK / Raw Entity Print Debug Status")]
    public void PrintDebugStatus()
    {
        Debug.Log(
            "[ObeliskRawEntity] STATUS\n" +
            "CurrentRoom=" + currentRoomId + "\n" +
            "PreviousRoom=" + previousRoomId + "\n" +
            "Visible=" + manifestation.visible + "\n" +
            "PixelVomit=" + manifestation.pixelVomit + "\n" +
            "Canvas=" + (targetCanvas != null ? targetCanvas.name : "NULL") + "\n" +
            "EntityImage=" + (entityImage != null ? entityImage.name : "NULL") + "\n" +
            "EntityEnabled=" + (entityImage != null ? entityImage.enabled.ToString() : "NULL") + "\n" +
            "PixelVomitImage=" + (pixelVomitImage != null ? pixelVomitImage.name : "NULL") + "\n" +
            "PixelVomitEnabled=" + (pixelVomitImage != null ? pixelVomitImage.enabled.ToString() : "NULL") + "\n" +
            "PixelVomitMaterial=" + (pixelVomitMaterial != null ? pixelVomitMaterial.name : "NULL") + "\n" +
            "RuleForCurrent=" + (FindRule(currentRoomId) != null ? "YES" : "NO") + "\n" +
            "Unlocked=" + silencePathUnlocked + "\n" +
            "SeenFromBypass=" + hasBeenSeenFromBypass
        );
    }

    [ContextMenu("OBELISK / Raw Entity Delete Old UI Objects")]
    public void DeleteOldUiObjects()
    {
        ResolveReferences();

        if (targetCanvas == null)
            return;

        DeleteChildIfExists("Obelisk_BlackSquare_WorldCorruption");
        DeleteChildIfExists("Obelisk_BlackSquare_ProceduralCorruption");
        DeleteChildIfExists("Obelisk_BlackSquare_MalignEntity");
        DeleteChildIfExists("Obelisk_BlackSquare_Entity");
        DeleteChildIfExists("Obelisk_BlackEntity_RawPNG");
        DeleteChildIfExists("Obelisk_BlackEntity_PixelVomit");

        Setup();
        ApplyVisibility();

        Debug.Log("[ObeliskRawEntity] Anciens objets UI supprimés/recréés.");
    }

    public bool CanMove(string fromRoomId, BackgroundManager.Direction direction, string targetRoomId)
    {
        if (!enableNavigationGate)
            return true;

        if (unlockMode == BlackSquareUnlockMode.Disabled)
            return true;

        if (silencePathUnlocked)
            return true;

        if (fromRoomId != blackSquareRoomId)
            return true;

        if (targetRoomId != blockedTargetRoomId)
            return true;

        if (bypassSeenCanAlwaysOpenPath && hasBeenSeenFromBypass)
        {
            UnlockNow("sécurité : contournement déjà vu");
            return true;
        }

        if (debugLogs)
            Debug.Log("[ObeliskRawEntity] Passage bloqué : " + fromRoomId + " --" + direction + "--> " + targetRoomId);

        onMainApproachDenied?.Invoke();
        return false;
    }

    public void NotifyPointerEnter()
    {
    }

    public void NotifyPointerExit()
    {
    }

    public void NotifyPointerClick()
    {
        if (entityImage == null || !entityImage.enabled)
            return;

        onEntityClicked?.Invoke();

        if (hasBeenSeenFromBypass || manifestation.holdsKey)
        {
            clicksAfterBypass++;

            if (unlockMode == BlackSquareUnlockMode.BypassApproachPlusClickUnlocks &&
                clicksAfterBypass >= clicksRequiredAfterBypass)
            {
                UnlockNow("contournement + clic");
                return;
            }

            if (bypassSeenCanAlwaysOpenPath)
            {
                UnlockNow("clic sur entité");
                return;
            }
        }
    }

    private void StartDelayedSync()
    {
        if (delayedSyncRoutine != null)
            StopCoroutine(delayedSyncRoutine);

        delayedSyncRoutine = StartCoroutine(DelayedInitialSync());
    }

    private IEnumerator DelayedInitialSync()
    {
        for (int i = 0; i < 45; i++)
        {
            ResolveReferences();
            ForceRefreshRoom();

            if (!string.IsNullOrEmpty(currentRoomId))
            {
                if (debugLogs)
                    Debug.Log("[ObeliskRawEntity] Room détectée après init : " + currentRoomId);

                RollForCurrentRoom(false);
                ApplyVisibility();
                PrintDebugStatus();
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning("[ObeliskRawEntity] Impossible de lire la room courante après init.");
    }

    private void HandleRoomChanged(string newRoomId)
    {
        if (newRoomId == currentRoomId)
            return;

        previousRoomId = currentRoomId;
        currentRoomId = newRoomId;
        lastObservedRoomId = currentRoomId;

        if (debugLogs)
            Debug.Log("[ObeliskRawEntity] Room change : " + previousRoomId + " -> " + currentRoomId);

        Setup();

        if (rerollEveryRoomEntry)
            RollForCurrentRoom(false);

        EvaluateRoomEvent();
        ApplyVisibility();
    }

    private void DetectRoomChangeWithoutEvent()
    {
        string id = GetCurrentRoomIdSafe();

        if (string.IsNullOrEmpty(id))
            return;

        if (id == lastObservedRoomId)
            return;

        previousRoomId = lastObservedRoomId;
        currentRoomId = id;
        lastObservedRoomId = id;

        if (debugLogs)
            Debug.Log("[ObeliskRawEntity] Room change détecté : " + previousRoomId + " -> " + currentRoomId);

        Setup();

        if (rerollEveryRoomEntry)
            RollForCurrentRoom(false);

        EvaluateRoomEvent();
        ApplyVisibility();
    }

    private void ForceRefreshRoom()
    {
        string id = GetCurrentRoomIdSafe();

        if (string.IsNullOrEmpty(id))
            return;

        currentRoomId = id;
        lastObservedRoomId = id;
    }

    private void EvaluateRoomEvent()
    {
        if (manifestation.visible && !hasBeenSeen)
        {
            hasBeenSeen = true;
            SaveState();
            onFirstSeen?.Invoke();
        }

        if (currentRoomId != blackSquareRoomId)
            return;

        if (previousRoomId == mainApproachRoomId)
        {
            hasBeenSeenFromMain = true;
            SaveState();
        }

        if (previousRoomId == bypassApproachRoomId)
        {
            hasBeenSeenFromBypass = true;
            SaveState();
            onBypassSeen?.Invoke();

            if (unlockMode == BlackSquareUnlockMode.BypassApproachUnlocks)
                UnlockNow("contournement");
        }
    }

    private void UnlockNow(string reason)
    {
        if (silencePathUnlocked)
            return;

        silencePathUnlocked = true;
        SaveState();
        onPathUnlocked?.Invoke();

        if (debugLogs)
            Debug.Log("[ObeliskRawEntity] Chemin déverrouillé : " + reason);
    }

    private void ForceSpawnSpecificRoom(string id)
    {
        ResolveReferences();
        Setup();

        if (string.IsNullOrEmpty(id))
            return;

        currentRoomId = id;
        lastObservedRoomId = id;

        EntityRoomRule rule = GetOrCreateRule(id);
        rule.enabled = true;
        rule.neverSpawn = false;
        rule.forceSpawn = true;
        rule.spawnChance = 1f;
        rule.pixelVomitChance = 0.45f;
        rule.pixelVomitStrength = 0.92f;
        rule.pixelVomitRadius = 1.55f;
        rule.pixelVomitAlpha = 0.74f;
        rule.raycastTarget = true;
        rule.holdsKey = id == blackSquareRoomId;

        RollForCurrentRoom(true);
        ApplyVisibility();
        PrintDebugStatus();

        Debug.Log("[ObeliskRawEntity] Force spawn : " + id);
    }

    private void RollForCurrentRoom(bool forceVisible)
    {
        string id = GetCurrentRoomIdSafe();

        if (string.IsNullOrEmpty(id))
            id = currentRoomId;

        if (string.IsNullOrEmpty(id))
        {
            HideEverything();
            return;
        }

        currentRoomId = id;

        EntityRoomRule rule = FindRule(id);
        bool visible = false;

        if (forceVisible)
        {
            if (rule == null)
                rule = GetOrCreateRule(id);

            visible = true;
        }
        else if (rule != null)
        {
            if (!rule.enabled || rule.neverSpawn)
                visible = false;
            else if (rule.forceSpawn)
                visible = true;
            else
                visible = UnityEngine.Random.value <= rule.spawnChance;
        }
        else
        {
            visible = canSpawnOnAnyRoom && UnityEngine.Random.value <= defaultSpawnChance;
        }

        manifestation = RollManifestationFromRule(id, rule, visible);
        hasManifestation = true;

        if (debugLogs)
        {
            string ruleInfo = rule != null ? ("rule force=" + rule.forceSpawn + " chance=" + rule.spawnChance.ToString("0.00")) : "no rule";
            Debug.Log("[ObeliskRawEntity] Roll room=" + id + " visible=" + visible + " vomit=" + manifestation.pixelVomit + " " + ruleInfo);
        }
    }

    private Manifestation RollManifestationFromRule(string roomId, EntityRoomRule rule, bool visible)
    {
        Vector2 center = Vector2.zero;
        Vector2 positionRange = new Vector2(240f, 120f);
        Vector2 baseSize = new Vector2(145f, 220f);
        Vector2 sizeRange = new Vector2(60f, 85f);
        Vector2 aspectRange = new Vector2(0.45f, 0.85f);
        float minRot = -1.6f;
        float maxRot = 1.6f;
        float vomitChance = 0.18f;
        float vomitStrength = 0.65f;
        float vomitRadius = 1.35f;
        float vomitAlpha = 0.68f;
        bool raycast = true;
        bool holdsKey = false;

        if (rule != null)
        {
            center = rule.center;
            positionRange = rule.randomPositionRange;
            baseSize = rule.baseSize;
            sizeRange = rule.randomSizeRange;
            aspectRange = rule.aspectRange;
            minRot = rule.minRotation;
            maxRot = rule.maxRotation;
            vomitChance = rule.pixelVomitChance;
            vomitStrength = rule.pixelVomitStrength;
            vomitRadius = rule.pixelVomitRadius;
            vomitAlpha = rule.pixelVomitAlpha;
            raycast = rule.raycastTarget;
            holdsKey = rule.holdsKey;
        }

        Vector2 position = center + new Vector2(
            UnityEngine.Random.Range(-positionRange.x, positionRange.x),
            UnityEngine.Random.Range(-positionRange.y, positionRange.y)
        );

        Vector2 size = baseSize + new Vector2(
            UnityEngine.Random.Range(-sizeRange.x, sizeRange.x),
            UnityEngine.Random.Range(-sizeRange.y, sizeRange.y)
        );

        size.x = Mathf.Max(42f, size.x);
        size.y = Mathf.Max(64f, size.y);

        float aspect = UnityEngine.Random.Range(aspectRange.x, aspectRange.y);
        size.x = Mathf.Max(38f, size.y * aspect);

        bool vomit = visible && UnityEngine.Random.value <= vomitChance && vomitStrength > 0.01f;

        return new Manifestation
        {
            visible = visible,
            pixelVomit = vomit,
            roomId = roomId,
            position = position,
            size = size,
            rotation = UnityEngine.Random.Range(minRot, maxRot),
            seed = UnityEngine.Random.Range(10f, 99999f),
            vomitStrength = vomitStrength,
            vomitRadius = vomitRadius,
            vomitAlpha = vomitAlpha,
            raycastTarget = raycast,
            holdsKey = holdsKey
        };
    }

    private void ApplyVisibility()
    {
        if (entityImage == null || pixelVomitImage == null)
            return;

        if (!hasManifestation || string.IsNullOrEmpty(currentRoomId))
        {
            HideEverything();
            return;
        }

        if (!manifestation.visible)
        {
            entityImage.enabled = false;
            entityImage.raycastTarget = false;
            pixelVomitImage.enabled = false;
            return;
        }

        entityImage.enabled = true;
        entityImage.raycastTarget = manifestation.raycastTarget;
        entityImage.sprite = entitySprite != null ? entitySprite : GetGeneratedClassicEntitySprite();
        entityImage.color = entityColor;
        entityImage.material = null;

        entityRect.anchorMin = new Vector2(0.5f, 0.5f);
        entityRect.anchorMax = new Vector2(0.5f, 0.5f);
        entityRect.pivot = new Vector2(0.5f, 0.5f);
        entityRect.anchoredPosition = manifestation.position;
        entityRect.sizeDelta = manifestation.size;
        entityRect.localRotation = Quaternion.Euler(0f, 0f, manifestation.rotation);
        entityRect.localScale = Vector3.one;

        ApplyPixelVomitVisibility();
    }

    private void HideEverything()
    {
        if (entityImage != null)
        {
            entityImage.enabled = false;
            entityImage.raycastTarget = false;
        }

        if (pixelVomitImage != null)
            pixelVomitImage.enabled = false;

        manifestation.visible = false;
    }

    private void ApplyPixelVomitVisibility()
    {
        if (pixelVomitImage == null)
            return;

        if (!manifestation.visible || !manifestation.pixelVomit)
        {
            pixelVomitImage.enabled = false;
            return;
        }

        if (pixelVomitMaterial == null)
        {
            if (disableVomitIfShaderMissing)
            {
                pixelVomitImage.enabled = false;
                return;
            }
        }

        Image source = FindBestSourceImage();

        if (source == null || source.sprite == null)
        {
            pixelVomitImage.enabled = false;
            return;
        }

        pixelVomitImage.enabled = true;
        pixelVomitImage.sprite = source.sprite;
        pixelVomitImage.type = source.type;
        pixelVomitImage.preserveAspect = source.preserveAspect;
        pixelVomitImage.color = Color.white;
        pixelVomitImage.raycastTarget = false;

        CopyRectTransform(source.rectTransform, pixelVomitRect);

        if (pixelVomitMaterial != null)
            pixelVomitImage.material = pixelVomitMaterial;
    }

    private void UpdatePixelVomitMaterial()
    {
        if (pixelVomitMaterial == null || pixelVomitImage == null || !pixelVomitImage.enabled)
            return;

        Vector4 data = CalculateEntityUvInPixelVomit();
        pixelVomitMaterial.SetVector(VEntityCenter, new Vector4(data.x, data.y, 0f, 0f));
        pixelVomitMaterial.SetVector(VEntitySize, new Vector4(data.z, data.w, 0f, 0f));
        pixelVomitMaterial.SetFloat(VSeed, manifestation.seed);
        pixelVomitMaterial.SetFloat(VTime, Time.unscaledTime);
        pixelVomitMaterial.SetFloat(VStrength, manifestation.vomitStrength);
        pixelVomitMaterial.SetFloat(VRadius, manifestation.vomitRadius);
        pixelVomitMaterial.SetFloat(VMaxAlpha, manifestation.vomitAlpha);
    }

    private Vector4 CalculateEntityUvInPixelVomit()
    {
        if (pixelVomitRect == null || entityRect == null)
            return new Vector4(0.5f, 0.5f, 0.12f, 0.22f);

        Vector3 worldCenter = entityRect.TransformPoint(entityRect.rect.center);
        Vector2 localCenter = pixelVomitRect.InverseTransformPoint(worldCenter);
        Rect r = pixelVomitRect.rect;

        if (Mathf.Abs(r.width) < 0.01f || Mathf.Abs(r.height) < 0.01f)
            return new Vector4(0.5f, 0.5f, 0.12f, 0.22f);

        float u = Mathf.InverseLerp(r.xMin, r.xMax, localCenter.x);
        float v = Mathf.InverseLerp(r.yMin, r.yMax, localCenter.y);
        float su = Mathf.Clamp01(Mathf.Abs(manifestation.size.x / r.width));
        float sv = Mathf.Clamp01(Mathf.Abs(manifestation.size.y / r.height));

        return new Vector4(u, v, su, sv);
    }

    private void CreatePixelVomitMaterial()
    {
        if (pixelVomitMaterial != null)
            return;

        if (pixelVomitShader == null)
            pixelVomitShader = Shader.Find("Obelisk/UI/Raw Pixel Vomit");

        if (pixelVomitShader == null)
            return;

        pixelVomitMaterial = new Material(pixelVomitShader);
        pixelVomitMaterial.name = "Obelisk_RawPixelVomit_Runtime";
    }

    private void CreateOrFindEntityImage()
    {
        if (targetCanvas == null)
            return;

        Transform existing = targetCanvas.transform.Find(entityObjectName);

        if (existing != null)
        {
            entityImage = existing.GetComponent<Image>();

            if (entityImage == null)
                entityImage = existing.gameObject.AddComponent<Image>();
        }
        else
        {
            GameObject obj = new GameObject(entityObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(targetCanvas.transform, false);
            entityImage = obj.GetComponent<Image>();
        }

        entityRect = entityImage.rectTransform;
        entityRect.anchorMin = new Vector2(0.5f, 0.5f);
        entityRect.anchorMax = new Vector2(0.5f, 0.5f);
        entityRect.pivot = new Vector2(0.5f, 0.5f);
        entityRect.localScale = Vector3.one;
        entityRect.localRotation = Quaternion.identity;

        entityImage.sprite = entitySprite != null ? entitySprite : GetGeneratedClassicEntitySprite();
        entityImage.type = Image.Type.Simple;
        entityImage.preserveAspect = false;
        entityImage.raycastTarget = true;
        entityImage.color = entityColor;
        entityImage.material = null;
        entityImage.enabled = false;

        hitbox = entityImage.GetComponent<ObeliskBlackSquareHitbox>();

        if (hitbox == null)
            hitbox = entityImage.gameObject.AddComponent<ObeliskBlackSquareHitbox>();

        hitbox.SetDirector(this);
    }

    private void CreateOrFindPixelVomitImage()
    {
        if (targetCanvas == null)
            return;

        Transform existing = targetCanvas.transform.Find(pixelVomitObjectName);

        if (existing != null)
        {
            pixelVomitImage = existing.GetComponent<Image>();

            if (pixelVomitImage == null)
                pixelVomitImage = existing.gameObject.AddComponent<Image>();
        }
        else
        {
            GameObject obj = new GameObject(pixelVomitObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(targetCanvas.transform, false);
            pixelVomitImage = obj.GetComponent<Image>();
        }

        pixelVomitRect = pixelVomitImage.rectTransform;
        pixelVomitImage.type = Image.Type.Simple;
        pixelVomitImage.preserveAspect = false;
        pixelVomitImage.raycastTarget = false;
        pixelVomitImage.color = Color.white;
        pixelVomitImage.enabled = false;

        if (pixelVomitMaterial != null)
            pixelVomitImage.material = pixelVomitMaterial;
    }

    private Sprite GetGeneratedClassicEntitySprite()
    {
        if (!useGeneratedClassicEntityWhenNoSprite && entitySprite != null)
            return entitySprite;

        if (generatedEntitySprite != null)
            return generatedEntitySprite;

        int width = 96;
        int height = 128;

        generatedEntityTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        generatedEntityTexture.name = "Obelisk_Generated_V3_Classic_Filled_Entity";
        generatedEntityTexture.filterMode = FilterMode.Point;
        generatedEntityTexture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color body = Color.white;
        Color dim = new Color(0.55f, 0.55f, 0.55f, 1f);

        Color[] pixels = new Color[width * height];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;

        void SetPixel(int x, int y, Color color)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            pixels[y * width + x] = color;
        }

        void FillRect(int x0, int y0, int w, int h, Color color)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    SetPixel(x, y, color);
        }

        void FillEllipse(int cx, int cy, int rx, int ry, Color color)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            {
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    float dx = (x - cx) / (float)rx;
                    float dy = (y - cy) / (float)ry;

                    if (dx * dx + dy * dy <= 1f)
                        SetPixel(x, y, color);
                }
            }
        }

        // SPRITE V3 ORIGINAL — SHAPE CONSERVÉE.
        // Différence unique : aucun trou transparent n'est dessiné.
        // Donc plus d'yeux, plus de bouche, plus de fissures vides.
        // Le personnage est plein à 100% dans sa silhouette.

        // Corps / manteau vertical V3.
        FillEllipse(48, 47, 22, 26, body);
        FillRect(31, 30, 34, 58, body);
        FillRect(26, 18, 44, 18, body);
        FillRect(29, 10, 38, 12, body);

        // Chapeau absurde V3 : large brim + tour + antennes.
        FillRect(13, 83, 70, 8, body);
        FillRect(23, 90, 49, 8, body);
        FillRect(31, 98, 33, 14, body);
        FillRect(38, 112, 23, 8, body);
        FillRect(44, 120, 11, 5, body);

        // Asymétries / pixels parasites V3.
        // Ces zones sont remplies, pas transparentes.
        FillRect(70, 78, 8, 9, dim);
        FillRect(19, 77, 7, 6, dim);
        FillRect(64, 101, 12, 5, dim);
        FillRect(16, 94, 8, 4, dim);
        FillRect(75, 109, 5, 5, body);
        FillRect(20, 115, 4, 4, body);

        // Antennes / mauvais présage V3.
        FillRect(34, 121, 3, 7, body);
        FillRect(62, 119, 3, 9, body);
        FillRect(30, 126, 7, 2, body);
        FillRect(61, 126, 9, 2, body);

        generatedEntityTexture.SetPixels(pixels);
        generatedEntityTexture.Apply();

        generatedEntitySprite = Sprite.Create(
            generatedEntityTexture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            64f
        );

        generatedEntitySprite.name = "Obelisk_Generated_V3_Classic_Filled_Entity_Sprite";
        return generatedEntitySprite;
    }

    private Image FindBestSourceImage()
    {
        if (targetCanvas == null)
            targetCanvas = FindAnyObjectByType<Canvas>();

        if (targetCanvas == null)
            return null;

        Image[] images = targetCanvas.GetComponentsInChildren<Image>(true);
        string roomId = GetCurrentRoomIdSafe();

        if (!string.IsNullOrEmpty(roomId))
        {
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];

                if (image == null)
                    continue;

                if (ShouldIgnoreSourceImage(image))
                    continue;

                if (image.gameObject.name == roomId &&
                    image.gameObject.activeInHierarchy &&
                    image.sprite != null)
                {
                    return image;
                }
            }
        }

        Image best = null;
        float bestArea = 0f;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];

            if (image == null)
                continue;

            if (ShouldIgnoreSourceImage(image))
                continue;

            if (!image.gameObject.activeInHierarchy)
                continue;

            if (image.sprite == null)
                continue;

            RectTransform rt = image.rectTransform;
            float area = Mathf.Abs(rt.rect.width * rt.rect.height);

            if (area > bestArea)
            {
                bestArea = area;
                best = image;
            }
        }

        return best;
    }

    private bool ShouldIgnoreSourceImage(Image image)
    {
        if (image == null)
            return true;

        if (image == entityImage || image == pixelVomitImage)
            return true;

        string n = image.gameObject.name.ToLowerInvariant();

        if (n.Contains("zone")) return true;
        if (n.Contains("fade")) return true;
        if (n.Contains("overlay")) return true;
        if (n.Contains("cursor")) return true;
        if (n.Contains("button")) return true;
        if (n.Contains("black")) return true;
        if (n.Contains("entity")) return true;
        if (n.Contains("vomit")) return true;
        if (image.sprite == null) return true;

        return false;
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

    private EntityRoomRule FindRule(string roomId)
    {
        if (string.IsNullOrEmpty(roomId))
            return null;

        for (int i = 0; i < roomRules.Count; i++)
        {
            EntityRoomRule rule = roomRules[i];

            if (rule == null || !rule.enabled)
                continue;

            if (rule.roomId == roomId)
                return rule;
        }

        return null;
    }

    private EntityRoomRule GetOrCreateRule(string roomId)
    {
        EntityRoomRule existing = FindRule(roomId);

        if (existing != null)
            return existing;

        EntityRoomRule created = new EntityRoomRule();
        created.roomId = roomId;
        roomRules.Add(created);

        return created;
    }

    private void EnsureOrder()
    {
        if (pixelVomitImage != null)
            pixelVomitImage.transform.SetAsLastSibling();

        if (entityImage != null)
            entityImage.transform.SetAsLastSibling();

        if (!keepBelowFadeOverlay)
            return;

        if (fadeOverlay == null)
            fadeOverlay = FindScreenFaderImage();

        if (fadeOverlay == null)
            fadeOverlay = FindImageByName(fadeOverlayObjectName);

        if (fadeOverlay == null)
            return;

        fadeOverlay.transform.SetAsLastSibling();

        int fadeIndex = fadeOverlay.transform.GetSiblingIndex();

        if (entityImage != null && entityImage.transform.parent == fadeOverlay.transform.parent)
            entityImage.transform.SetSiblingIndex(Mathf.Max(0, fadeIndex - 1));

        if (pixelVomitImage != null && pixelVomitImage.transform.parent == fadeOverlay.transform.parent)
        {
            int entityIndex = entityImage != null ? entityImage.transform.GetSiblingIndex() : Mathf.Max(0, fadeIndex - 1);
            pixelVomitImage.transform.SetSiblingIndex(Mathf.Max(0, entityIndex - 1));
        }

        fadeOverlay.transform.SetAsLastSibling();
    }

    private void DeleteChildIfExists(string childName)
    {
        if (targetCanvas == null || string.IsNullOrEmpty(childName))
            return;

        Transform child = targetCanvas.transform.Find(childName);

        if (child == null)
            return;

        if (Application.isPlaying)
            Destroy(child.gameObject);
        else
            DestroyImmediate(child.gameObject);
    }

    private void ResolveReferences()
    {
        if (targetCanvas == null)
            targetCanvas = FindAnyObjectByType<Canvas>();

        if (backgroundManager == null)
            backgroundManager = FindAnyObjectByType<BackgroundManager>();

        if (fadeOverlay == null)
            fadeOverlay = FindScreenFaderImage();

        if (fadeOverlay == null)
            fadeOverlay = FindImageByName(fadeOverlayObjectName);
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

    private string GetCurrentRoomIdSafe()
    {
        if (backgroundManager == null)
            backgroundManager = FindAnyObjectByType<BackgroundManager>();

        if (backgroundManager == null)
            return "";

        return backgroundManager.GetCurrentRoomId();
    }

    private void SaveState()
    {
        if (!persistStateWithPlayerPrefs)
            return;

        PlayerPrefs.SetInt(saveKeyPrefix + SeenKey, hasBeenSeen ? 1 : 0);
        PlayerPrefs.SetInt(saveKeyPrefix + MainKey, hasBeenSeenFromMain ? 1 : 0);
        PlayerPrefs.SetInt(saveKeyPrefix + BypassKey, hasBeenSeenFromBypass ? 1 : 0);
        PlayerPrefs.SetInt(saveKeyPrefix + UnlockKey, silencePathUnlocked ? 1 : 0);
        PlayerPrefs.SetInt(saveKeyPrefix + ClicksKey, clicksAfterBypass);
        PlayerPrefs.Save();
    }

    private void LoadState()
    {
        if (!persistStateWithPlayerPrefs)
            return;

        hasBeenSeen = PlayerPrefs.GetInt(saveKeyPrefix + SeenKey, 0) == 1;
        hasBeenSeenFromMain = PlayerPrefs.GetInt(saveKeyPrefix + MainKey, 0) == 1;
        hasBeenSeenFromBypass = PlayerPrefs.GetInt(saveKeyPrefix + BypassKey, 0) == 1;
        silencePathUnlocked = PlayerPrefs.GetInt(saveKeyPrefix + UnlockKey, 0) == 1;
        clicksAfterBypass = PlayerPrefs.GetInt(saveKeyPrefix + ClicksKey, 0);
    }

    private void ClearPersistedState()
    {
        PlayerPrefs.DeleteKey(saveKeyPrefix + SeenKey);
        PlayerPrefs.DeleteKey(saveKeyPrefix + MainKey);
        PlayerPrefs.DeleteKey(saveKeyPrefix + BypassKey);
        PlayerPrefs.DeleteKey(saveKeyPrefix + UnlockKey);
        PlayerPrefs.DeleteKey(saveKeyPrefix + ClicksKey);
        PlayerPrefs.Save();
    }
}

public class ObeliskBlackSquareHitbox : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private ObeliskBlackSquareDirector director;

    public void SetDirector(ObeliskBlackSquareDirector newDirector)
    {
        director = newDirector;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (director == null)
            director = ObeliskBlackSquareDirector.Instance;

        if (director != null)
            director.NotifyPointerEnter();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (director == null)
            director = ObeliskBlackSquareDirector.Instance;

        if (director != null)
            director.NotifyPointerExit();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (director == null)
            director = ObeliskBlackSquareDirector.Instance;

        if (director != null)
            director.NotifyPointerClick();
    }
}
