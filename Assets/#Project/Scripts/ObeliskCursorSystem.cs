using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using InputMouse = UnityEngine.InputSystem.Mouse;

public class ObeliskCursorSystem : MonoBehaviour
{
    private enum CursorStyle
    {
        Normal,
        Impossible,
        Action,
        Attack,
        ObeliskSurprise
    }

    [Header("Curseurs originaux")]
    [SerializeField] private Texture2D normalCursor;
    [SerializeField] private Texture2D impossibleCursor;

    [Tooltip("Curseur qui indique au joueur qu'il peut faire une action : examiner, prendre, cliquer, activer, etc.")]
    [SerializeField] private Texture2D actionCursor;

    [Tooltip("Curseur pour une action agressive / spéciale, par exemple attaquer l'obélisque.")]
    [SerializeField] private Texture2D attackCursor;

    [Header("Curseur surprise obélisque")]
    [Tooltip("Grosse image de l'obélisque qui peut remplacer le curseur rarement pendant quelques secondes.")]
    [SerializeField] private Texture2D obeliskSurpriseCursor;

    [SerializeField] private bool enableObeliskSurprise = true;

    [Tooltip("Si activé, la surprise peut remplacer tous les autres curseurs pendant sa durée.")]
    [SerializeField] private bool obeliskSurpriseOverridesEverything = true;

    [Tooltip("Si activé, la surprise n'arrive que quand le joueur peut interagir.")]
    [SerializeField] private bool obeliskSurpriseOnlyWhenPlayerCanInteract = true;

    [Tooltip("Délai minimum avant le premier possible événement surprise.")]
    [SerializeField] private float surpriseInitialDelayMin = 45f;

    [Tooltip("Délai maximum avant le premier possible événement surprise.")]
    [SerializeField] private float surpriseInitialDelayMax = 120f;

    [Tooltip("Fréquence minimum entre deux checks aléatoires.")]
    [SerializeField] private float surpriseCheckIntervalMin = 8f;

    [Tooltip("Fréquence maximum entre deux checks aléatoires.")]
    [SerializeField] private float surpriseCheckIntervalMax = 20f;

    [Range(0f, 1f)]
    [Tooltip("Chance qu'un check déclenche la surprise. 0.04 = très rare, 0.15 = plus fréquent.")]
    [SerializeField] private float surpriseChancePerCheck = 0.075f;

    [Tooltip("Durée minimum de la surprise.")]
    [SerializeField] private float surpriseDurationMin = 1.15f;

    [Tooltip("Durée maximum de la surprise.")]
    [SerializeField] private float surpriseDurationMax = 2.80f;

    [Tooltip("Après une surprise, temps minimum avant qu'une autre puisse arriver.")]
    [SerializeField] private float surpriseCooldownAfterTrigger = 180f;

    [Header("Audio surprise obélisque")]
    [SerializeField] private bool enableSurpriseAudioEvents = true;

    [Range(0f, 1f)]
    [Tooltip("Chance que la musique soit coupée net pendant l'apparition.")]
    [SerializeField] private float surpriseMuteMusicChance = 0.35f;

    [Range(0f, 1f)]
    [Tooltip("Chance qu'un son/screamer soit joué pendant l'apparition.")]
    [SerializeField] private float surpriseScreamerChance = 0.18f;

    [Tooltip("Si désactivé, le mute et le screamer ne peuvent pas arriver en même temps.")]
    [SerializeField] private bool allowMuteAndScreamerTogether = false;

    [Tooltip("Si la liste est vide, le script essaie de trouver les AudioSource de musique tout seul.")]
    [SerializeField] private bool autoFindMusicSourcesToMute = true;

    [Tooltip("Mets ici l'AudioSource de ta musique si tu veux être précis.")]
    [SerializeField] private List<AudioSource> musicSourcesToMute = new List<AudioSource>();

    [Tooltip("AudioSource dédiée au screamer. Optionnel : le script peut en créer une si tu donnes un clip.")]
    [SerializeField] private AudioSource screamerAudioSource;

    [Tooltip("Son à jouer si l'event screamer est choisi.")]
    [SerializeField] private AudioClip screamerClip;

    [Range(0f, 2f)]
    [SerializeField] private float screamerVolume = 1f;

    [Tooltip("Stoppe le screamer à la fin de l'apparition. Désactive si tu veux laisser le son finir.")]
    [SerializeField] private bool stopScreamerWhenSurpriseEnds = false;

    [Header("Silence total pendant le mute")]
    [Tooltip("Si activé, l'event mute coupe TOUT le son via AudioListener.volume = 0. C'est le plus fiable.")]
    [SerializeField] private bool useGlobalAudioListenerMute = true;

    [Tooltip("Compatibilité ancienne version. Le son revient maintenant dès que le curseur surprise disparaît.")]
    [SerializeField] private float surpriseMuteMinimumDuration = 2.25f;

    [Tooltip("Ajoute un petit silence après la disparition du curseur surprise.")]
    [SerializeField] private float surpriseMuteExtraSecondsAfterCursor = 0f;

    [Header("Impact visuel mute - sombre / fin")]
    [SerializeField] private bool enableMuteVisualGlitch = true;

    [Tooltip("Durée totale du micro-accident visuel.")]
    [SerializeField] private float muteGlitchDuration = 0.18f;

    [Tooltip("Nombre de fines coupures horizontales.")]
    [SerializeField] private int muteGlitchBarCount = 12;

    [Tooltip("Force globale. 0.35-0.55 = subtil, 0.75 = plus visible.")]
    [Range(0f, 1f)]
    [SerializeField] private float muteGlitchIntensity = 0.48f;

    [Tooltip("Si activé, l'impact passe au-dessus du FadeOverlay pendant une fraction de seconde.")]
    [SerializeField] private bool muteGlitchAboveEverything = true;

    [Tooltip("Flash sombre très bref, pas rose/cyan.")]
    [Range(0f, 1f)]
    [SerializeField] private float muteGlitchHardFlashChance = 0.18f;

    [Tooltip("Timbre visuel sombre : noir bleuté.")]
    [SerializeField] private Color muteGlitchColorA = new Color(0.015f, 0.020f, 0.028f, 1f);

    [Tooltip("Timbre visuel sombre : vert malade très désaturé.")]
    [SerializeField] private Color muteGlitchColorB = new Color(0.045f, 0.075f, 0.055f, 1f);

    [Tooltip("Timbre visuel sombre : gris froid.")]
    [SerializeField] private Color muteGlitchColorC = new Color(0.18f, 0.20f, 0.22f, 1f);

    [Range(0f, 1f)]
    [SerializeField] private float muteGlitchBlackFrameStrength = 0.42f;

    [Range(0f, 1f)]
    [SerializeField] private float muteGlitchThinLineStrength = 0.60f;

    [Range(0f, 1f)]
    [SerializeField] private float muteGlitchMicroJitterStrength = 0.30f;

    [Header("Mute brut anti-tail + glitch audio syncro")]
    [Tooltip("Coupe vraiment les AudioSource pendant le mute, pas juste leur volume. Ça élimine les tails/reverbs/delays perceptibles.")]
    [SerializeField] private bool hardStopAudioSourcesDuringMute = true;

    [Tooltip("Si activé, le script coupe toutes les AudioSource de la scène pendant le mute. C'est le plus brutal et le plus sûr.")]
    [SerializeField] private bool hardStopAllAudioSourcesDuringMute = true;

    [Tooltip("Relance les AudioSource qui jouaient avant le mute quand le curseur surprise disparaît.")]
    [SerializeField] private bool resumeAudioSourcesAfterHardStop = true;

    [Tooltip("Pour les musiques en loop, avance leur position comme si le temps avait continué pendant le silence.")]
    [SerializeField] private bool advanceLoopingAudioTimeDuringMute = true;

    [Tooltip("Joue un micro-glitch audio au même instant que le glitch visuel. Les autres sons sont coupés avant.")]
    [SerializeField] private bool playSyncedMuteAudioGlitch = true;

    [Tooltip("Optionnel : son glitch custom. Si vide, le script génère un micro son sombre automatiquement.")]
    [SerializeField] private AudioClip syncedMuteGlitchClip;

    [Range(0f, 1f)]
    [SerializeField] private float syncedMuteGlitchVolume = 0.62f;

    [Tooltip("Durée du glitch audio généré si aucun clip custom n'est donné.")]
    [SerializeField] private float generatedMuteGlitchDuration = 0.11f;

    [Tooltip("Après le micro-glitch, force un vrai silence global. Active si tu entends encore des queues.")]
    [SerializeField] private bool globalMuteAfterGlitchClick = true;

    [Header("Hotspots en pixels depuis le coin haut-gauche")]
    [SerializeField] private Vector2 normalHotspot = Vector2.zero;
    [SerializeField] private Vector2 impossibleHotspot = Vector2.zero;
    [SerializeField] private Vector2 actionHotspot = Vector2.zero;
    [SerializeField] private Vector2 attackHotspot = Vector2.zero;

    [Tooltip("Pour la grosse image obélisque, tu peux mettre le hotspot au centre de l'image source si tu veux.")]
    [SerializeField] private Vector2 obeliskSurpriseHotspot = Vector2.zero;

    [Header("Taille des curseurs")]
    [Range(0.25f, 12f)]
    [Tooltip("Slider principal. 1 = taille originale, 2 = deux fois plus grand, 4 = quatre fois plus grand.")]
    [SerializeField] private float cursorScale = 2.5f;

    [Range(0.25f, 20f)]
    [Tooltip("Taille séparée pour la grosse image surprise de l'obélisque.")]
    [SerializeField] private float obeliskSurpriseScale = 5.0f;

    [Header("Zones directionnelles")]
    [SerializeField] private bool autoFindDirectionZones = true;
    [SerializeField] private RectTransform zoneHaut;
    [SerializeField] private RectTransform zoneBas;
    [SerializeField] private RectTransform zoneGauche;
    [SerializeField] private RectTransform zoneDroite;

    [Header("Zones spéciales")]
    [Tooltip("Zones où le curseur devient Action : examiner, prendre, activer, inspecter.")]
    [SerializeField] private List<RectTransform> actionZones = new List<RectTransform>();

    [Tooltip("Zones où le curseur devient Attack, par exemple une zone invisible au-dessus de l'obélisque.")]
    [SerializeField] private List<RectTransform> attackZones = new List<RectTransform>();

    [Tooltip("Zones où le curseur devient Impossible.")]
    [SerializeField] private List<RectTransform> impossibleZones = new List<RectTransform>();

    [Header("Réglages")]
    [SerializeField] private CursorMode cursorMode = CursorMode.ForceSoftware;
    [SerializeField] private bool forceSoftwareCursor = true;
    [SerializeField] private bool impossibleWhenFading = true;
    [SerializeField] private bool useSystemCursorIfTextureMissing = true;
    [SerializeField] private bool debugLogs = false;

    private Texture2D safeNormalCursor;
    private Texture2D safeImpossibleCursor;
    private Texture2D safeActionCursor;
    private Texture2D safeAttackCursor;
    private Texture2D safeObeliskSurpriseCursor;

    private CursorStyle currentStyle = CursorStyle.Normal;
    private bool hasAppliedCursor = false;

    private Canvas cachedCanvas;
    private BackgroundManager backgroundManager;

    private float lastUsedCursorScale = -1f;
    private float lastUsedObeliskSurpriseScale = -1f;

    private Texture2D lastNormalSource;
    private Texture2D lastImpossibleSource;
    private Texture2D lastActionSource;
    private Texture2D lastAttackSource;
    private Texture2D lastObeliskSurpriseSource;

    private bool obeliskSurpriseActive = false;
    private float obeliskSurpriseEndTime = -1f;
    private float nextSurpriseCheckTime = -1f;
    private float surpriseCooldownUntil = -1f;

    private readonly Dictionary<AudioSource, float> mutedAudioSourcesOriginalVolumes = new Dictionary<AudioSource, float>();
    private bool surpriseMutedMusic = false;
    private bool surprisePlayedScreamer = false;
    private AudioSource runtimeScreamerAudioSource;

    private bool surpriseMuteActive = false;
    private bool globalAudioListenerMuteActive = false;
    private float audioListenerVolumeBeforeMute = 1f;
    private float surpriseMuteRestoreTime = -1f;

    private Canvas muteGlitchCanvas;
    private RectTransform muteGlitchRoot;
    private Image muteGlitchFlashImage;
    private readonly List<Image> muteGlitchBars = new List<Image>();
    private bool muteGlitchActive = false;
    private float muteGlitchEndTime = -1f;
    private float muteGlitchNextFrameTime = -1f;

    private readonly List<HardStoppedAudioSourceState> hardStoppedAudioStates = new List<HardStoppedAudioSourceState>();
    private float hardMuteStartTime = -1f;

    private AudioSource syncedMuteGlitchSource;
    private AudioClip generatedMuteGlitchClip;
    private bool syncedMuteGlitchPlaying = false;
    private float syncedMuteGlitchEndTime = -1f;

    private class HardStoppedAudioSourceState
    {
        public AudioSource source;
        public bool wasPlaying;
        public bool wasMuted;
        public float volume;
        public float time;
        public int timeSamples;
        public AudioClip clip;
        public bool loop;
    }

    private void Awake()
    {
        cachedCanvas = FindAnyObjectByType<Canvas>();
        backgroundManager = FindAnyObjectByType<BackgroundManager>();

        PrepareCursorTextures();

        if (autoFindDirectionZones)
            AutoFindDirectionZones();

        ScheduleFirstSurpriseCheck();
        ApplyCursor(CursorStyle.Normal, true);
    }

    private void OnEnable()
    {
        PrepareCursorTextures();

        if (nextSurpriseCheckTime < 0f)
            ScheduleFirstSurpriseCheck();

        ApplyCursor(CursorStyle.Normal, true);
    }

    private void OnDisable()
    {
        RestoreSurpriseMuteNow();
        Cursor.SetCursor(null, Vector2.zero, GetCursorMode());
    }

    private void Update()
    {
        if (InputMouse.current == null)
            return;

        if (backgroundManager == null)
            backgroundManager = FindAnyObjectByType<BackgroundManager>();

        if (NeedsRebuildSafeTextures())
        {
            PrepareCursorTextures();
            ApplyCursor(currentStyle, true);
        }

        UpdateObeliskSurprise();
        EnforceSurpriseAudioMute();
        UpdateMuteVisualGlitch();

        CursorStyle wantedStyle = DecideCursorStyle();
        ApplyCursor(wantedStyle, false);
    }

    [ContextMenu("OBELISK / Rebuild Safe Cursor Textures")]
    private void PrepareCursorTextures()
    {
        cursorScale = Mathf.Max(0.1f, cursorScale);
        obeliskSurpriseScale = Mathf.Max(0.1f, obeliskSurpriseScale);

        DestroySafeTexture(safeNormalCursor);
        DestroySafeTexture(safeImpossibleCursor);
        DestroySafeTexture(safeActionCursor);
        DestroySafeTexture(safeAttackCursor);
        DestroySafeTexture(safeObeliskSurpriseCursor);

        safeNormalCursor = MakeCursorSafeTexture(normalCursor, "Normal", cursorScale);
        safeImpossibleCursor = MakeCursorSafeTexture(impossibleCursor, "Impossible", cursorScale);
        safeActionCursor = MakeCursorSafeTexture(actionCursor, "Action", cursorScale);
        safeAttackCursor = MakeCursorSafeTexture(attackCursor, "Attack", cursorScale);
        safeObeliskSurpriseCursor = MakeCursorSafeTexture(obeliskSurpriseCursor, "ObeliskSurprise", obeliskSurpriseScale);

        lastUsedCursorScale = cursorScale;
        lastUsedObeliskSurpriseScale = obeliskSurpriseScale;

        lastNormalSource = normalCursor;
        lastImpossibleSource = impossibleCursor;
        lastActionSource = actionCursor;
        lastAttackSource = attackCursor;
        lastObeliskSurpriseSource = obeliskSurpriseCursor;
    }

    [ContextMenu("OBELISK / Auto Find Direction Zones")]
    private void AutoFindDirectionZones()
    {
        zoneHaut = FindRectTransformByName("Zone_Haut");
        zoneBas = FindRectTransformByName("Zone_Bas");
        zoneGauche = FindRectTransformByName("Zone_Gauche");
        zoneDroite = FindRectTransformByName("Zone_Droite");

        if (debugLogs)
        {
            Debug.Log("[ObeliskCursorSystem] Zone_Haut = " + NameOrNull(zoneHaut));
            Debug.Log("[ObeliskCursorSystem] Zone_Bas = " + NameOrNull(zoneBas));
            Debug.Log("[ObeliskCursorSystem] Zone_Gauche = " + NameOrNull(zoneGauche));
            Debug.Log("[ObeliskCursorSystem] Zone_Droite = " + NameOrNull(zoneDroite));
        }
    }

    [ContextMenu("OBELISK / Trigger Obelisk Surprise Now")]
    private void TriggerObeliskSurpriseNowFromMenu()
    {
        TriggerObeliskSurprise();
    }

    [ContextMenu("OBELISK / Stop Obelisk Surprise")]
    private void StopObeliskSurpriseFromMenu()
    {
        StopObeliskSurprise();
    }

    private CursorStyle DecideCursorStyle()
    {
        if (obeliskSurpriseActive && obeliskSurpriseOverridesEverything)
            return CursorStyle.ObeliskSurprise;

        if (IsPlayerBlockedByFade())
            return CursorStyle.Impossible;

        if (IsRoomTransitionRunning())
            return CursorStyle.Impossible;

        if (obeliskSurpriseActive)
            return CursorStyle.ObeliskSurprise;

        Vector2 mousePosition = InputMouse.current.position.ReadValue();

        // Priorité : Attack > Action > Impossible > Directions > Normal.
        if (IsMouseInsideAny(attackZones, mousePosition))
            return CursorStyle.Attack;

        if (IsMouseInsideAny(actionZones, mousePosition))
            return CursorStyle.Action;

        if (IsMouseInsideAny(impossibleZones, mousePosition))
            return CursorStyle.Impossible;

        if (IsMouseInside(zoneHaut, mousePosition))
            return CanMove("haut") ? CursorStyle.Normal : CursorStyle.Impossible;

        if (IsMouseInside(zoneBas, mousePosition))
            return CanMove("bas") ? CursorStyle.Normal : CursorStyle.Impossible;

        if (IsMouseInside(zoneGauche, mousePosition))
            return CanMove("gauche") ? CursorStyle.Normal : CursorStyle.Impossible;

        if (IsMouseInside(zoneDroite, mousePosition))
            return CanMove("droite") ? CursorStyle.Normal : CursorStyle.Impossible;

        return CursorStyle.Normal;
    }

    private void UpdateObeliskSurprise()
    {
        if (!enableObeliskSurprise)
        {
            if (obeliskSurpriseActive)
                StopObeliskSurprise();

            return;
        }

        if (safeObeliskSurpriseCursor == null)
            return;

        if (obeliskSurpriseActive)
        {
            if (Time.time >= obeliskSurpriseEndTime)
                StopObeliskSurprise();

            return;
        }

        if (Time.time < surpriseCooldownUntil)
            return;

        if (Time.time < nextSurpriseCheckTime)
            return;

        if (!CanObeliskSurpriseHappenNow())
        {
            ScheduleNextSurpriseCheck();
            return;
        }

        if (UnityEngine.Random.value <= Mathf.Clamp01(surpriseChancePerCheck))
        {
            TriggerObeliskSurprise();
        }
        else
        {
            ScheduleNextSurpriseCheck();
        }
    }

    private bool CanObeliskSurpriseHappenNow()
    {
        if (!Application.isPlaying)
            return false;

        if (obeliskSurpriseOnlyWhenPlayerCanInteract)
        {
            if (IsPlayerBlockedByFade())
                return false;

            if (IsRoomTransitionRunning())
                return false;
        }

        return true;
    }

    private void TriggerObeliskSurprise()
    {
        if (safeObeliskSurpriseCursor == null)
            return;

        obeliskSurpriseActive = true;

        float minDuration = Mathf.Max(0.1f, Mathf.Min(surpriseDurationMin, surpriseDurationMax));
        float maxDuration = Mathf.Max(minDuration, surpriseDurationMax);
        float duration = UnityEngine.Random.Range(minDuration, maxDuration);

        obeliskSurpriseEndTime = Time.time + duration;
        surpriseCooldownUntil = Time.time + duration + Mathf.Max(0f, surpriseCooldownAfterTrigger);

        ApplyCursor(CursorStyle.ObeliskSurprise, true);
        StartSurpriseAudioEvents();

        if (debugLogs)
            Debug.Log("[ObeliskCursorSystem] SURPRISE OBELISK CURSOR pendant " + duration.ToString("0.00") + " secondes.");
    }

    private void StopObeliskSurprise()
    {
        StopSurpriseAudioEvents();

        obeliskSurpriseActive = false;
        obeliskSurpriseEndTime = -1f;

        ScheduleNextSurpriseCheck();
        ApplyCursor(DecideCursorStyle(), true);

        if (debugLogs)
            Debug.Log("[ObeliskCursorSystem] Fin surprise obélisque. Prochain check à t=" + nextSurpriseCheckTime.ToString("0.00"));
    }

    private void ScheduleFirstSurpriseCheck()
    {
        float minDelay = Mathf.Max(0f, Mathf.Min(surpriseInitialDelayMin, surpriseInitialDelayMax));
        float maxDelay = Mathf.Max(minDelay, surpriseInitialDelayMax);
        nextSurpriseCheckTime = Time.time + UnityEngine.Random.Range(minDelay, maxDelay);
    }

    private void ScheduleNextSurpriseCheck()
    {
        float minDelay = Mathf.Max(0.1f, Mathf.Min(surpriseCheckIntervalMin, surpriseCheckIntervalMax));
        float maxDelay = Mathf.Max(minDelay, surpriseCheckIntervalMax);
        nextSurpriseCheckTime = Time.time + UnityEngine.Random.Range(minDelay, maxDelay);
    }


    private void TriggerMuteVisualGlitch()
    {
        if (!enableMuteVisualGlitch)
            return;

        if (muteGlitchDuration <= 0f)
            return;

        EnsureMuteGlitchOverlayExists();

        if (muteGlitchRoot == null)
            return;

        muteGlitchActive = true;
        muteGlitchEndTime = Time.unscaledTime + muteGlitchDuration;
        muteGlitchNextFrameTime = -1f;

        muteGlitchRoot.gameObject.SetActive(true);
        PlaceMuteGlitchOverlay();

        RandomizeMuteGlitchFrame(true);
    }

    private void UpdateMuteVisualGlitch()
    {
        if (!muteGlitchActive)
            return;

        if (Time.unscaledTime >= muteGlitchEndTime)
        {
            StopMuteVisualGlitch();
            return;
        }

        PlaceMuteGlitchOverlay();

        if (Time.unscaledTime >= muteGlitchNextFrameTime)
        {
            RandomizeMuteGlitchFrame(false);
            muteGlitchNextFrameTime = Time.unscaledTime + UnityEngine.Random.Range(0.018f, 0.038f);
        }
    }

    private void StopMuteVisualGlitch()
    {
        muteGlitchActive = false;
        muteGlitchEndTime = -1f;

        if (muteGlitchRoot != null)
            muteGlitchRoot.gameObject.SetActive(false);
    }

    private void EnsureMuteGlitchOverlayExists()
    {
        if (muteGlitchRoot != null)
            return;

        muteGlitchCanvas = FindAnyObjectByType<Canvas>();

        if (muteGlitchCanvas == null)
            return;

        GameObject root = new GameObject("Obelisk_MuteGlitchCut", typeof(RectTransform));
        root.transform.SetParent(muteGlitchCanvas.transform, false);

        muteGlitchRoot = root.GetComponent<RectTransform>();
        muteGlitchRoot.anchorMin = Vector2.zero;
        muteGlitchRoot.anchorMax = Vector2.one;
        muteGlitchRoot.offsetMin = Vector2.zero;
        muteGlitchRoot.offsetMax = Vector2.zero;
        muteGlitchRoot.localScale = Vector3.one;

        GameObject flashObject = new GameObject("DarkCut", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        flashObject.transform.SetParent(muteGlitchRoot, false);

        RectTransform flashRect = flashObject.GetComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;
        flashRect.localScale = Vector3.one;

        muteGlitchFlashImage = flashObject.GetComponent<Image>();
        muteGlitchFlashImage.raycastTarget = false;
        muteGlitchFlashImage.color = new Color(0f, 0f, 0f, 0f);

        int safeBarCount = Mathf.Clamp(muteGlitchBarCount, 1, 64);

        for (int i = 0; i < safeBarCount; i++)
        {
            GameObject barObject = new GameObject("DarkLine_" + i.ToString("00"), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barObject.transform.SetParent(muteGlitchRoot, false);

            Image barImage = barObject.GetComponent<Image>();
            barImage.raycastTarget = false;
            barImage.color = Color.clear;

            muteGlitchBars.Add(barImage);
        }

        muteGlitchRoot.gameObject.SetActive(false);
    }

    private void PlaceMuteGlitchOverlay()
    {
        if (muteGlitchRoot == null)
            return;

        if (muteGlitchAboveEverything)
        {
            muteGlitchRoot.SetAsLastSibling();
            return;
        }

        ScreenFader fader = ScreenFader.Instance;

        if (fader == null)
        {
            muteGlitchRoot.SetAsLastSibling();
            return;
        }

        RectTransform faderTransform = fader.GetComponent<RectTransform>();

        if (faderTransform == null || faderTransform.parent != muteGlitchRoot.parent)
        {
            muteGlitchRoot.SetAsLastSibling();
            return;
        }

        faderTransform.SetAsLastSibling();

        int fadeIndex = faderTransform.GetSiblingIndex();
        muteGlitchRoot.SetSiblingIndex(Mathf.Max(0, fadeIndex - 1));

        faderTransform.SetAsLastSibling();
    }

    private void RandomizeMuteGlitchFrame(bool firstFrame)
    {
        if (muteGlitchRoot == null)
            return;

        float intensity = Mathf.Clamp01(muteGlitchIntensity);

        if (muteGlitchFlashImage != null)
        {
            bool darkCut = firstFrame || UnityEngine.Random.value < muteGlitchHardFlashChance;
            float alpha = darkCut ? muteGlitchBlackFrameStrength * intensity : UnityEngine.Random.Range(0.03f, 0.12f) * intensity;
            muteGlitchFlashImage.color = new Color(0f, 0f, 0f, alpha);
        }

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        for (int i = 0; i < muteGlitchBars.Count; i++)
        {
            Image bar = muteGlitchBars[i];

            if (bar == null)
                continue;

            RectTransform rect = bar.rectTransform;

            float activeChance = Mathf.Lerp(0.18f, 0.62f, intensity);

            if (UnityEngine.Random.value > activeChance)
            {
                bar.color = Color.clear;
                continue;
            }

            float thinLine = UnityEngine.Random.value;
            float height;

            if (thinLine < 0.78f)
                height = UnityEngine.Random.Range(1.0f, 7.0f) * Mathf.Lerp(0.7f, 1.6f, muteGlitchThinLineStrength);
            else
                height = UnityEngine.Random.Range(8f, 24f) * Mathf.Lerp(0.7f, 1.25f, intensity);

            float y = UnityEngine.Random.Range(-screenHeight * 0.5f, screenHeight * 0.5f);
            float xOffset = UnityEngine.Random.Range(-screenWidth * 0.055f, screenWidth * 0.055f) * muteGlitchMicroJitterStrength;
            float width = screenWidth * UnityEngine.Random.Range(0.72f, 1.04f);

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xOffset, y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            Color color = PickMuteGlitchColor();

            if (UnityEngine.Random.value < 0.46f)
                color = Color.black;

            float alpha = UnityEngine.Random.Range(0.10f, 0.38f) * intensity;

            if (thinLine < 0.78f)
                alpha *= 0.75f;

            bar.color = new Color(color.r, color.g, color.b, alpha);
        }
    }

    private Color PickMuteGlitchColor()
    {
        float r = UnityEngine.Random.value;

        if (r < 0.45f)
            return muteGlitchColorA;

        if (r < 0.78f)
            return muteGlitchColorB;

        return muteGlitchColorC;
    }

    private void PlaySyncedMuteAudioGlitchNow()
    {
        EnsureSyncedMuteGlitchSourceExists();

        if (syncedMuteGlitchSource == null)
            return;

        AudioClip clipToPlay = syncedMuteGlitchClip;

        if (clipToPlay == null)
            clipToPlay = GetOrCreateGeneratedMuteGlitchClip();

        if (clipToPlay == null)
            return;

        syncedMuteGlitchSource.Stop();
        syncedMuteGlitchSource.clip = clipToPlay;
        syncedMuteGlitchSource.volume = syncedMuteGlitchVolume;
        syncedMuteGlitchSource.mute = false;
        syncedMuteGlitchSource.loop = false;
        syncedMuteGlitchSource.spatialBlend = 0f;
        syncedMuteGlitchSource.playOnAwake = false;
        syncedMuteGlitchSource.ignoreListenerPause = true;
        syncedMuteGlitchSource.Play();

        syncedMuteGlitchPlaying = true;
        syncedMuteGlitchEndTime = Time.time + clipToPlay.length;
    }

    private void UpdateSyncedMuteAudioGlitch()
    {
        if (!syncedMuteGlitchPlaying)
            return;

        if (syncedMuteGlitchSource == null)
        {
            syncedMuteGlitchPlaying = false;
            return;
        }

        if (Time.time >= syncedMuteGlitchEndTime || !syncedMuteGlitchSource.isPlaying)
        {
            syncedMuteGlitchSource.Stop();
            syncedMuteGlitchPlaying = false;

            if (useGlobalAudioListenerMute && globalAudioListenerMuteActive && globalMuteAfterGlitchClick)
                AudioListener.volume = 0f;
        }
    }

    private void StopSyncedMuteAudioGlitchNow()
    {
        syncedMuteGlitchPlaying = false;
        syncedMuteGlitchEndTime = -1f;

        if (syncedMuteGlitchSource != null)
            syncedMuteGlitchSource.Stop();
    }

    private void EnsureSyncedMuteGlitchSourceExists()
    {
        if (syncedMuteGlitchSource != null)
            return;

        GameObject obj = new GameObject("Obelisk_SyncedMuteAudioCut");
        obj.transform.SetParent(transform, false);

        syncedMuteGlitchSource = obj.AddComponent<AudioSource>();
        syncedMuteGlitchSource.playOnAwake = false;
        syncedMuteGlitchSource.loop = false;
        syncedMuteGlitchSource.spatialBlend = 0f;
        syncedMuteGlitchSource.ignoreListenerPause = true;
    }

    private AudioClip GetOrCreateGeneratedMuteGlitchClip()
    {
        if (generatedMuteGlitchClip != null)
            return generatedMuteGlitchClip;

        int sampleRate = 44100;
        float duration = Mathf.Clamp(generatedMuteGlitchDuration, 0.035f, 0.35f);
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
        float[] data = new float[sampleCount];

        float seedOffset = UnityEngine.Random.Range(0f, 1000f);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float n = i / (float)sampleCount;

            // Enveloppe très sèche : attaque immédiate, extinction rapide.
            float env = Mathf.Exp(-n * 18f);
            float lowThump = Mathf.Sin(2f * Mathf.PI * 58f * t) * 0.30f;
            float midBite = Mathf.Sin(2f * Mathf.PI * 137f * t + seedOffset) * 0.18f;
            float digital = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 910f * t)) * 0.08f;
            float noise = (UnityEngine.Random.value * 2f - 1f) * 0.16f;

            // Petit trou au milieu, comme une bande qui décroche.
            float gate = (n > 0.42f && n < 0.54f) ? 0.15f : 1f;

            data[i] = Mathf.Clamp((lowThump + midBite + digital + noise) * env * gate, -1f, 1f);
        }

        generatedMuteGlitchClip = AudioClip.Create("Obelisk_Generated_DarkMuteCut", sampleCount, 1, sampleRate, false);
        generatedMuteGlitchClip.SetData(data, 0);

        return generatedMuteGlitchClip;
    }


    private void HardStopAudioSourcesNow()
    {
        if (!hardStopAudioSourcesDuringMute)
            return;

        hardMuteStartTime = Time.time;

        List<AudioSource> sources = GetAudioSourcesToHardStop();

        for (int i = 0; i < sources.Count; i++)
        {
            HardStopSingleAudioSource(sources[i]);
        }
    }

    private void EnforceHardStoppedAudioSources()
    {
        if (!hardStopAudioSourcesDuringMute)
            return;

        if (!surpriseMuteActive)
            return;

        List<AudioSource> sources = GetAudioSourcesToHardStop();

        for (int i = 0; i < sources.Count; i++)
        {
            AudioSource source = sources[i];

            if (source == null)
                continue;

            // Si MusicManager / Spatializer / autre script relance un son pendant le mute,
            // on le recoupe tout de suite. Ça évite les tails et les reprises parasites.
            if (source.isPlaying)
                HardStopSingleAudioSource(source);

            source.volume = 0f;
            source.mute = true;
        }
    }

    private void RestoreHardStoppedAudioSources()
    {
        if (!hardStopAudioSourcesDuringMute)
            return;

        float mutedDuration = 0f;

        if (hardMuteStartTime >= 0f)
            mutedDuration = Mathf.Max(0f, Time.time - hardMuteStartTime);

        for (int i = 0; i < hardStoppedAudioStates.Count; i++)
        {
            HardStoppedAudioSourceState state = hardStoppedAudioStates[i];

            if (state == null)
                continue;

            AudioSource source = state.source;

            if (source == null)
                continue;

            source.mute = state.wasMuted;
            source.volume = state.volume;

            if (!resumeAudioSourcesAfterHardStop)
                continue;

            if (!state.wasPlaying)
                continue;

            if (state.clip == null)
                continue;

            if (source.clip != state.clip)
                source.clip = state.clip;

            RestoreAudioSourceTime(source, state, mutedDuration);

            if (!source.isPlaying)
                source.Play();
        }

        hardStoppedAudioStates.Clear();
        hardMuteStartTime = -1f;
    }

    private List<AudioSource> GetAudioSourcesToHardStop()
    {
        if (!hardStopAllAudioSourcesDuringMute)
            return GetMusicSourcesToMute();

        List<AudioSource> result = new List<AudioSource>();
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude);

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];

            if (source == null)
                continue;

            if (source == screamerAudioSource)
                continue;

            if (source == runtimeScreamerAudioSource)
                continue;

            if (source == syncedMuteGlitchSource)
                continue;

            if (!result.Contains(source))
                result.Add(source);
        }

        return result;
    }

    private void HardStopSingleAudioSource(AudioSource source)
    {
        if (source == null)
            return;

        if (source == screamerAudioSource)
            return;

        if (source == runtimeScreamerAudioSource)
            return;

        if (source == syncedMuteGlitchSource)
            return;

        HardStoppedAudioSourceState state = FindHardStoppedState(source);

        if (state == null)
        {
            state = new HardStoppedAudioSourceState
            {
                source = source,
                wasPlaying = source.isPlaying,
                wasMuted = source.mute,
                volume = source.volume,
                time = SafeGetAudioSourceTime(source),
                timeSamples = SafeGetAudioSourceTimeSamples(source),
                clip = source.clip,
                loop = source.loop
            };

            hardStoppedAudioStates.Add(state);
        }

        // Vrai hard cut : on coupe la source elle-même, pas juste son volume.
        source.mute = true;
        source.volume = 0f;

        if (source.isPlaying)
            source.Stop();
    }

    private HardStoppedAudioSourceState FindHardStoppedState(AudioSource source)
    {
        for (int i = 0; i < hardStoppedAudioStates.Count; i++)
        {
            HardStoppedAudioSourceState state = hardStoppedAudioStates[i];

            if (state != null && state.source == source)
                return state;
        }

        return null;
    }

    private float SafeGetAudioSourceTime(AudioSource source)
    {
        if (source == null)
            return 0f;

        try
        {
            return source.time;
        }
        catch
        {
            return 0f;
        }
    }

    private int SafeGetAudioSourceTimeSamples(AudioSource source)
    {
        if (source == null)
            return 0;

        try
        {
            return source.timeSamples;
        }
        catch
        {
            return 0;
        }
    }

    private void RestoreAudioSourceTime(AudioSource source, HardStoppedAudioSourceState state, float mutedDuration)
    {
        if (source == null)
            return;

        if (state == null)
            return;

        if (source.clip == null)
            return;

        try
        {
            float targetTime = state.time;

            if (advanceLoopingAudioTimeDuringMute && state.loop && source.clip.length > 0.01f)
                targetTime = Mathf.Repeat(state.time + mutedDuration, source.clip.length);

            targetTime = Mathf.Clamp(targetTime, 0f, Mathf.Max(0f, source.clip.length - 0.05f));
            source.time = targetTime;
        }
        catch
        {
            try
            {
                source.timeSamples = state.timeSamples;
            }
            catch
            {
                // Certains clips streaming/compressés refusent le seek.
                // Dans ce cas, source.Play() reprendra simplement selon Unity.
            }
        }
    }

    private void StartSurpriseAudioEvents()
    {
        surpriseMutedMusic = false;
        surprisePlayedScreamer = false;

        if (!enableSurpriseAudioEvents)
            return;

        bool wantsMute = UnityEngine.Random.value <= Mathf.Clamp01(surpriseMuteMusicChance);
        bool wantsScreamer = UnityEngine.Random.value <= Mathf.Clamp01(surpriseScreamerChance);

        if (!allowMuteAndScreamerTogether && wantsMute && wantsScreamer)
        {
            if (UnityEngine.Random.value < 0.5f)
                wantsScreamer = false;
            else
                wantsMute = false;
        }

        // Si l'event choisi est "mute total", alors il ne doit vraiment plus y avoir aucun son.
        // Donc on annule le screamer pour cette occurrence-là.
        // Le screamer peut toujours arriver sur une autre occurrence, quand le mute n'est pas choisi.
        if (wantsMute && useGlobalAudioListenerMute)
            wantsScreamer = false;

        if (wantsMute)
            StartSurpriseMuteNow();

        if (wantsScreamer)
            PlayScreamerNow();

        if (debugLogs)
        {
            string info = "[ObeliskCursorSystem] Surprise audio : ";
            info += surpriseMutedMusic ? "FULL_MUTE " : "";
            info += surprisePlayedScreamer ? "SCREAMER " : "";
            if (!surpriseMutedMusic && !surprisePlayedScreamer)
                info += "NONE";
            Debug.Log(info);
        }
    }

    private void StopSurpriseAudioEvents()
    {
        if (stopScreamerWhenSurpriseEnds)
        {
            if (screamerAudioSource != null && screamerAudioSource.isPlaying)
                screamerAudioSource.Stop();

            if (runtimeScreamerAudioSource != null && runtimeScreamerAudioSource.isPlaying)
                runtimeScreamerAudioSource.Stop();
        }

        // Fix sec :
        // dès que le curseur surprise disparaît, le son revient immédiatement.
        // Pas de queue, pas de minimum qui continue après, pas de fade.
        if (surpriseMuteActive)
            RestoreSurpriseMuteNow();

        surprisePlayedScreamer = false;
    }

    private void StartSurpriseMuteNow()
    {
        surpriseMutedMusic = true;
        surpriseMuteActive = true;

        // Le mute suit le curseur surprise.
        // Le son revient instantanément dans StopSurpriseAudioEvents().
        // Le son revient quand le curseur surprise disparaît.
        // La variable surpriseMuteMinimumDuration est conservée pour compatibilité inspecteur.
        surpriseMuteMinimumDuration = Mathf.Max(0f, surpriseMuteMinimumDuration);
        surpriseMuteRestoreTime = obeliskSurpriseEndTime + Mathf.Max(0f, surpriseMuteExtraSecondsAfterCursor);

        // Ordre précis de l'impact :
        // 1. stop brutal de toutes les sources existantes pour tuer les tails
        // 2. micro-glitch audio dédié, très court, si demandé
        // 3. glitch visuel syncro
        // 4. silence total maintenu jusqu'à disparition du curseur
        MuteMusicSourcesNow();
        HardStopAudioSourcesNow();

        bool willPlayGlitchAudio = playSyncedMuteAudioGlitch;
        if (willPlayGlitchAudio)
        {
            PlaySyncedMuteAudioGlitchNow();

            // Si on veut entendre le micro-glitch, on ne met PAS AudioListener.volume à 0 tout de suite.
            // Après le micro-glitch, EnforceSurpriseAudioMute() passera AudioListener.volume à 0.
            if (useGlobalAudioListenerMute && !globalAudioListenerMuteActive)
            {
                audioListenerVolumeBeforeMute = AudioListener.volume;
                globalAudioListenerMuteActive = true;
                AudioListener.volume = 1f;
            }
        }
        else if (useGlobalAudioListenerMute)
        {
            if (!globalAudioListenerMuteActive)
            {
                audioListenerVolumeBeforeMute = AudioListener.volume;
                globalAudioListenerMuteActive = true;
            }

            AudioListener.volume = 0f;
        }

        TriggerMuteVisualGlitch();

        if (debugLogs)
            Debug.Log("[ObeliskCursorSystem] HARD MUTE SYNC jusqu'à t=" + surpriseMuteRestoreTime.ToString("0.00"));
    }

    private void EnforceSurpriseAudioMute()
    {
        if (!surpriseMuteActive)
            return;

        UpdateSyncedMuteAudioGlitch();

        bool glitchStillAudible = syncedMuteGlitchPlaying && Time.time < syncedMuteGlitchEndTime;

        if (useGlobalAudioListenerMute && globalAudioListenerMuteActive)
        {
            if (glitchStillAudible)
                AudioListener.volume = 1f;
            else if (globalMuteAfterGlitchClick)
                AudioListener.volume = 0f;
        }

        foreach (KeyValuePair<AudioSource, float> pair in mutedAudioSourcesOriginalVolumes)
        {
            if (pair.Key != null)
                pair.Key.volume = 0f;
        }

        EnforceHardStoppedAudioSources();

        if (Time.time >= surpriseMuteRestoreTime && !obeliskSurpriseActive)
            RestoreSurpriseMuteNow();
    }

    private void RestoreSurpriseMuteNow()
    {
        StopSyncedMuteAudioGlitchNow();
        RestoreMutedMusicSources();
        RestoreHardStoppedAudioSources();

        if (globalAudioListenerMuteActive)
        {
            AudioListener.volume = audioListenerVolumeBeforeMute;
            globalAudioListenerMuteActive = false;
        }

        surpriseMuteActive = false;
        surpriseMutedMusic = false;
        surpriseMuteRestoreTime = -1f;
    }

    private void MuteMusicSourcesNow()
    {
        List<AudioSource> sources = GetMusicSourcesToMute();

        for (int i = 0; i < sources.Count; i++)
        {
            AudioSource source = sources[i];

            if (source == null)
                continue;

            if (source == screamerAudioSource || source == runtimeScreamerAudioSource || source == syncedMuteGlitchSource)
                continue;

            if (!mutedAudioSourcesOriginalVolumes.ContainsKey(source))
                mutedAudioSourcesOriginalVolumes.Add(source, source.volume);

            source.volume = 0f;
            surpriseMutedMusic = true;
        }
    }

    private void RestoreMutedMusicSources()
    {
        foreach (KeyValuePair<AudioSource, float> pair in mutedAudioSourcesOriginalVolumes)
        {
            if (pair.Key != null)
                pair.Key.volume = pair.Value;
        }

        mutedAudioSourcesOriginalVolumes.Clear();
    }

    private List<AudioSource> GetMusicSourcesToMute()
    {
        List<AudioSource> result = new List<AudioSource>();

        for (int i = 0; i < musicSourcesToMute.Count; i++)
        {
            if (musicSourcesToMute[i] != null && !result.Contains(musicSourcesToMute[i]))
                result.Add(musicSourcesToMute[i]);
        }

        if (result.Count > 0 || !autoFindMusicSourcesToMute)
            return result;

        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude);

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];

            if (source == null)
                continue;

            if (source == screamerAudioSource || source == runtimeScreamerAudioSource || source == syncedMuteGlitchSource)
                continue;

            string objectName = source.gameObject.name.ToLowerInvariant();
            bool looksLikeMusic = objectName.Contains("music") || objectName.Contains("audio") || objectName.Contains("ambience") || objectName.Contains("ambient");

            if (source.loop || source.isPlaying || looksLikeMusic)
            {
                if (!result.Contains(source))
                    result.Add(source);
            }
        }

        return result;
    }

    private void PlayScreamerNow()
    {
        AudioSource targetSource = screamerAudioSource;

        if (targetSource == null && screamerClip != null)
        {
            if (runtimeScreamerAudioSource == null)
            {
                GameObject obj = new GameObject("Obelisk_RuntimeScreamerAudio");
                obj.transform.SetParent(transform, false);
                runtimeScreamerAudioSource = obj.AddComponent<AudioSource>();
                runtimeScreamerAudioSource.playOnAwake = false;
                runtimeScreamerAudioSource.loop = false;
                runtimeScreamerAudioSource.spatialBlend = 0f;
            }

            targetSource = runtimeScreamerAudioSource;
        }

        if (targetSource == null)
            return;

        targetSource.volume = screamerVolume;

        if (screamerClip != null)
        {
            targetSource.Stop();
            targetSource.clip = screamerClip;
            targetSource.loop = false;
            targetSource.Play();
        }
        else
        {
            targetSource.Play();
        }

        surprisePlayedScreamer = true;
    }

    private bool IsPlayerBlockedByFade()
    {
        if (!impossibleWhenFading)
            return false;

        if (ScreenFader.Instance == null)
            return false;

        return !ScreenFader.Instance.CanPlayerInteract;
    }

    private bool IsRoomTransitionRunning()
    {
        if (backgroundManager == null)
            backgroundManager = FindAnyObjectByType<BackgroundManager>();

        if (backgroundManager == null)
            return false;

        return backgroundManager.IsChangingRoom;
    }

    private bool CanMove(string directionFieldName)
    {
        if (backgroundManager == null)
            return false;

        if (backgroundManager.IsChangingRoom)
            return false;

        string currentRoomId = backgroundManager.GetCurrentRoomId();

        if (string.IsNullOrEmpty(currentRoomId))
            return false;

        string targetRoomId = GetTargetRoomId(currentRoomId, directionFieldName);

        return !string.IsNullOrEmpty(targetRoomId);
    }

    private string GetTargetRoomId(string currentRoomId, string directionFieldName)
    {
        if (backgroundManager == null)
            return "";

        FieldInfo roomsField = typeof(BackgroundManager).GetField(
            "rooms",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (roomsField == null)
        {
            if (debugLogs)
                Debug.LogWarning("[ObeliskCursorSystem] Champ 'rooms' introuvable dans BackgroundManager.");

            return "";
        }

        object roomsObject = roomsField.GetValue(backgroundManager);
        IEnumerable rooms = roomsObject as IEnumerable;

        if (rooms == null)
            return "";

        foreach (object room in rooms)
        {
            if (room == null)
                continue;

            string id = GetStringField(room, "id");

            if (id != currentRoomId)
                continue;

            return GetStringField(room, directionFieldName);
        }

        return "";
    }

    private string GetStringField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (field == null)
            return "";

        object value = field.GetValue(target);

        if (value == null)
            return "";

        return value.ToString();
    }

    private bool IsMouseInsideAny(List<RectTransform> rects, Vector2 mousePosition)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            if (IsMouseInside(rects[i], mousePosition))
                return true;
        }

        return false;
    }

    private bool IsMouseInside(RectTransform rectTransform, Vector2 mousePosition)
    {
        if (rectTransform == null)
            return false;

        if (!rectTransform.gameObject.activeInHierarchy)
            return false;

        Camera eventCamera = GetEventCamera(rectTransform);

        return RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform,
            mousePosition,
            eventCamera
        );
    }

    private Camera GetEventCamera(RectTransform rectTransform)
    {
        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();

        if (canvas == null)
            canvas = cachedCanvas;

        if (canvas == null)
            return null;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    private RectTransform FindRectTransformByName(string objectName)
    {
        GameObject found = GameObject.Find(objectName);

        if (found == null)
            return null;

        return found.GetComponent<RectTransform>();
    }

    private string NameOrNull(UnityEngine.Object obj)
    {
        if (obj == null)
            return "NULL";

        return obj.name;
    }

    private void ApplyCursor(CursorStyle style, bool force)
    {
        if (!force && hasAppliedCursor && currentStyle == style)
            return;

        Texture2D texture = GetSafeTexture(style);
        Vector2 hotspot = GetScaledHotspot(style);

        if (texture == null && useSystemCursorIfTextureMissing)
        {
            Cursor.SetCursor(null, Vector2.zero, GetCursorMode());
        }
        else
        {
            Cursor.SetCursor(texture, hotspot, GetCursorMode());
        }

        currentStyle = style;
        hasAppliedCursor = true;

        if (debugLogs)
            Debug.Log("[ObeliskCursorSystem] Cursor = " + style + " / Texture = " + NameOrNull(texture));
    }

    private CursorMode GetCursorMode()
    {
        if (forceSoftwareCursor)
            return CursorMode.ForceSoftware;

        return cursorMode;
    }

    private Texture2D GetSafeTexture(CursorStyle style)
    {
        switch (style)
        {
            case CursorStyle.Impossible:
                return safeImpossibleCursor;

            case CursorStyle.Action:
                return safeActionCursor;

            case CursorStyle.Attack:
                return safeAttackCursor;

            case CursorStyle.ObeliskSurprise:
                return safeObeliskSurpriseCursor;

            default:
                return safeNormalCursor;
        }
    }

    private Vector2 GetHotspot(CursorStyle style)
    {
        switch (style)
        {
            case CursorStyle.Impossible:
                return impossibleHotspot;

            case CursorStyle.Action:
                return actionHotspot;

            case CursorStyle.Attack:
                return attackHotspot;

            case CursorStyle.ObeliskSurprise:
                return obeliskSurpriseHotspot;

            default:
                return normalHotspot;
        }
    }

    private float GetScale(CursorStyle style)
    {
        if (style == CursorStyle.ObeliskSurprise)
            return Mathf.Max(0.1f, obeliskSurpriseScale);

        return Mathf.Max(0.1f, cursorScale);
    }

    private Vector2 GetScaledHotspot(CursorStyle style)
    {
        return GetHotspot(style) * GetScale(style);
    }

    private bool NeedsRebuildSafeTextures()
    {
        if (!Mathf.Approximately(lastUsedCursorScale, cursorScale))
            return true;

        if (!Mathf.Approximately(lastUsedObeliskSurpriseScale, obeliskSurpriseScale))
            return true;

        if (lastNormalSource != normalCursor)
            return true;

        if (lastImpossibleSource != impossibleCursor)
            return true;

        if (lastActionSource != actionCursor)
            return true;

        if (lastAttackSource != attackCursor)
            return true;

        if (lastObeliskSurpriseSource != obeliskSurpriseCursor)
            return true;

        return false;
    }

    private Texture2D MakeCursorSafeTexture(Texture2D source, string label, float scale)
    {
        if (source == null)
            return null;

        scale = Mathf.Max(0.1f, scale);

        int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
        int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

        Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        copy.name = source.name + "_CursorSafe_" + label + "_x" + scale.ToString("0.##");
        copy.hideFlags = HideFlags.DontSave;
        copy.wrapMode = TextureWrapMode.Clamp;
        copy.filterMode = FilterMode.Point;

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture renderTexture = null;

        try
        {
            renderTexture = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default
            );

            renderTexture.filterMode = FilterMode.Point;

            Graphics.Blit(source, renderTexture);

            RenderTexture.active = renderTexture;

            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            copy.Apply(false, false);

            if (debugLogs)
                Debug.Log("[ObeliskCursorSystem] Texture cursor-safe créée : " + copy.name + " (" + width + "x" + height + ")");

            return copy;
        }
        catch (Exception exception)
        {
            Debug.LogError("[ObeliskCursorSystem] Impossible de convertir le curseur '" + source.name + "' en texture cursor-safe. " + exception.Message);
            DestroySafeTexture(copy);
            return null;
        }
        finally
        {
            RenderTexture.active = previousActive;

            if (renderTexture != null)
                RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    private void DestroySafeTexture(Texture2D texture)
    {
        if (texture == null)
            return;

        if (Application.isPlaying)
            Destroy(texture);
        else
            DestroyImmediate(texture);
    }
}
