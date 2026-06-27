using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Force Random à être celui de Unity, pas System.Random.
using Random = UnityEngine.Random;

// Important : évite le conflit avec ton ancien script Mouse.cs.
using InputMouse = UnityEngine.InputSystem.Mouse;
[RequireComponent(typeof(AudioSource))]
public class MusicGlitchDirector : MonoBehaviour
{
    public enum GlitchIntensity
    {
        Subtle,
        Medium,
        Hard,
        Extreme
    }

    public enum GlitchEvent
    {
        Random,

        // Ambiant / long / inquiétant
        AlmostNothing,
        LongPitchDrift,
        SlowPitchDrop,
        NeutralVeil,
        TapeSickness,
        RoomBreathing,
        MemoryLeak,
        DistantCompression,
        StereoUnease,
        LowFidelityDip,

        // Plus audibles
        LayeredUnease,
        SoftTimeSlip,
        GhostStutter,
        NeedleWobble,
        ProgramDesync,

        // Événementiel / volontaire
        HardSystemSlip,
        SituationShift,
        ScreamerPreShock,
        ScreamerImpact,
        TotalNeutralization,
        AudioPossession,
        BrokenLoop,
        SystemCollapse,
        FalseRecovery
    }

    public enum LongMutationType
    {
        Random,

        WronglyCalmWorld,
        RottenTapeWorld,
        DeepSystemIllness,
        RemoteBroadcast,
        FalseNormal,
        VacuumRoom,
        BeautifulFailure,
        ProgramSleep,
        DiseasedMemory
    }

    public enum GlitchBiome
    {
        None,
        Neutral,
        Tape,
        Deep,
        Digital,
        Vacuum,
        Broadcast,
        Possession,
        Collapse,
        FalseNormal
    }

    [System.Serializable]
    private class ModLayer
    {
        public string name;

        public float duration;
        public float elapsed;

        public float attack;
        public float release;

        public float volumeDbOffset;
        public float pitchSemitoneOffset;
        public float panOffset;

        public float lowPassCutoff = -1f;
        public float highPassCutoff = -1f;
        public float distortion = 0f;
        public float echoWet = 0f;

        public bool hard;
        public bool eventLayer;
        public bool longMutationLayer;

        public GlitchBiome biome;
        public float visualWeight;

        public float Weight
        {
            get
            {
                if (duration <= 0f)
                    return 0f;

                float attackWeight = 1f;

                if (attack > 0f)
                    attackWeight = SmootherStep(Mathf.Clamp01(elapsed / attack));

                float releaseWeight = 1f;

                if (release > 0f)
                {
                    float remaining = Mathf.Max(0f, duration - elapsed);
                    releaseWeight = SmootherStep(Mathf.Clamp01(remaining / release));
                }

                return Mathf.Clamp01(Mathf.Min(attackWeight, releaseWeight));
            }
        }

        private static float SmootherStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }
    }

    [Header("Référence")]
    [SerializeField] private AudioSource musicSource;

    [Header("Random glitches")]
    [SerializeField] private bool enableRandomGlitches = true;
    [SerializeField] private bool allowDuringScreenFade = false;

    [Tooltip("Temps avant les premiers glitchs random.")]
    [SerializeField] private float randomStartGrace = 18f;

    [Tooltip("Plus bas = glitchs plus fréquents.")]
    [SerializeField] private float randomMinInterval = 20f;

    [Tooltip("Plus bas = glitchs plus fréquents.")]
    [SerializeField] private float randomMaxInterval = 80f;

    [Tooltip("Peut dépasser 1. 1 = toujours quand le timer arrive. Au-dessus de 1, le système devient plus intense.")]
    [Min(0f)]
    [SerializeField] private float randomGlitchChance = 0.55f;

    [Tooltip("Multiplie la fréquence générale. 1 normal, 3 très instable, 10 chaos.")]
    [Min(0f)]
    [SerializeField] private float probabilityMultiplier = 1f;

    [Tooltip("Multiplie la force générale des glitchs. 1 normal, 2 fort, 5 cassé.")]
    [Min(0f)]
    [SerializeField] private float intensityMultiplier = 1f;

    [Tooltip("Multiplie spécialement les gros events triggerés.")]
    [Min(0f)]
    [SerializeField] private float eventIntensityMultiplier = 1f;

    [Header("Mutations longues aléatoires")]
    [SerializeField] private bool enableRandomLongMutations = true;

    [Tooltip("Chance qu'un glitch random devienne une mutation longue. Peut dépasser 1 pour forcer.")]
    [Min(0f)]
    [SerializeField] private float longMutationChance = 0.08f;

    [Tooltip("Chance bonus de mutation longue quand le joueur clique beaucoup.")]
    [Min(0f)]
    [SerializeField] private float longMutationInputHeatBonus = 0.18f;

    [Tooltip("Durée minimale d'une mutation longue random.")]
    [SerializeField] private float randomLongMutationMinDuration = 35f;

    [Tooltip("Durée maximale d'une mutation longue random.")]
    [SerializeField] private float randomLongMutationMaxDuration = 120f;

    [Tooltip("Multiplie la durée des mutations longues. 2 = deux fois plus long.")]
    [Min(0f)]
    [SerializeField] private float longMutationDurationMultiplier = 1f;

    [Tooltip("Si coché, une mutation longue nettoie les petits glitchs avant de commencer.")]
    [SerializeField] private bool longMutationClearsAmbientLayers = false;

    [Tooltip("Autorise plusieurs mutations longues en même temps. Déconseillé sauf chaos volontaire.")]
    [SerializeField] private bool allowLongMutationOverlap = false;

    [Header("Mutation possible au lancement du jeu")]
    [SerializeField] private bool enableStartupLongMutation = true;

    [Tooltip("Chance qu'une mutation longue soit active au tout début de la musique. Peut dépasser 1 pour forcer.")]
    [Min(0f)]
    [SerializeField] private float startupLongMutationChance = 0.12f;

    [Tooltip("Attend que la musique soit réellement en train de jouer avant de décider.")]
    [SerializeField] private bool startupWaitUntilMusicPlays = true;

    [Tooltip("Temps maximum d'attente de la musique au lancement.")]
    [SerializeField] private float startupMaxWaitForMusic = 12f;

    [Tooltip("Petit délai après le début de la musique avant mutation.")]
    [SerializeField] private float startupMutationDelay = 0.4f;

    [SerializeField] private float startupMutationMinDuration = 28f;
    [SerializeField] private float startupMutationMaxDuration = 80f;

    [Header("Inputs joueur")]
    [SerializeField] private bool monitorPlayerClicks = true;

    [Tooltip("Chaque clic augmente la chaleur du système. Peut dépasser 1 pour rendre le jeu très réactif.")]
    [Min(0f)]
    [SerializeField] private float inputHeatPerClick = 0.08f;

    [Tooltip("Vitesse de refroidissement.")]
    [Min(0f)]
    [SerializeField] private float inputHeatDecayPerSecond = 0.08f;

    [Tooltip("Réduction des intervalles quand le joueur clique beaucoup.")]
    [Min(0f)]
    [SerializeField] private float inputHeatIntervalReduction = 0.45f;

    [Tooltip("Chance hard au repos. Peut dépasser 1 si tu veux forcer.")]
    [Min(0f)]
    [SerializeField] private float hardChanceWhenCalm = 0.004f;

    [Tooltip("Chance hard quand le joueur clique beaucoup. Peut dépasser 1 si tu veux forcer.")]
    [Min(0f)]
    [SerializeField] private float hardChanceWhenInputHot = 0.09f;

    [Tooltip("Chance medium au repos.")]
    [Min(0f)]
    [SerializeField] private float mediumChanceWhenCalm = 0.20f;

    [Tooltip("Chance medium quand le joueur clique beaucoup.")]
    [Min(0f)]
    [SerializeField] private float mediumChanceWhenInputHot = 0.42f;

    [Header("Superposition")]
    [SerializeField] private bool allowAmbientLayerOverlap = true;

    [Tooltip("8 à 16 est déjà beaucoup. Tu peux monter plus haut si tu veux tester.")]
    [Min(1)]
    [SerializeField] private int maxSimultaneousLayers = 10;

    [SerializeField] private bool blockHardOverlap = true;
    [SerializeField] private bool eventGlitchesClearAmbientLayers = true;

    [Header("Filtres")]
    [SerializeField] private bool useLowPassFilter = true;
    [SerializeField] private bool useHighPassFilter = true;
    [SerializeField] private bool useDistortionFilter = true;
    [SerializeField] private bool useEchoFilter = true;
    [SerializeField] private bool autoAddMissingFilters = true;

    [Header("Valeurs neutres")]
    [SerializeField] private float neutralLowPassCutoff = 22000f;
    [SerializeField] private float neutralHighPassCutoff = 10f;

    [Min(1f)]
    [SerializeField] private float lowPassResonanceQ = 1.05f;

    [Min(1f)]
    [SerializeField] private float highPassResonanceQ = 1.05f;

    [Header("Écologie future pour glitchs visuels")]
    [Tooltip("Active des valeurs publiques que le futur système visuel pourra lire.")]
    [SerializeField] private bool exposeGlitchEcology = true;

    [Tooltip("Lisse la pression globale pour les futurs glitchs visuels.")]
    [SerializeField] private float ecologySmoothing = 4f;

    [Header("Sécurité")]
    [SerializeField] private bool requireMusicPlaying = true;
    [SerializeField] private float minPitch = 0.25f;
    [SerializeField] private float maxPitch = 2.0f;
    [SerializeField] private float clipEndSafetySeconds = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private AudioLowPassFilter lowPassFilter;
    private AudioHighPassFilter highPassFilter;
    private AudioDistortionFilter distortionFilter;
    private AudioEchoFilter echoFilter;

    private readonly List<ModLayer> activeLayers = new List<ModLayer>();

    private Coroutine randomRoutine;
    private Coroutine startupRoutine;
    private Coroutine currentEventRoutine;
    private Coroutine currentLongMutationRoutine;

    private float inputHeat = 0f;

    private float lastVolumeFactor = 1f;
    private float lastPitchFactor = 1f;
    private float lastPanOffset = 0f;
    private bool lastFrameHadModulation = false;

    private bool isInLongMutation = false;
    private string currentMutationName = "";

    private GlitchBiome currentBiome = GlitchBiome.None;
    private float glitchPressure = 0f;

    private GlitchBiome lastSentBiome = GlitchBiome.None;
    private float lastSentPressure = -1f;

    public float InputHeat => inputHeat;
    public bool HasActiveGlitch => activeLayers.Count > 0;
    public bool IsInLongMutation => isInLongMutation;
    public string CurrentMutationName => currentMutationName;
    public GlitchBiome CurrentBiome => currentBiome;
    public float GlitchPressure => glitchPressure;

    public event Action<GlitchBiome, float, bool> OnGlitchEcologyChanged;

    private void Awake()
    {
        if (musicSource == null)
            musicSource = GetComponent<AudioSource>();

        SetupAudioSource();
        SetupFilters();

        if (debugLogs)
            Debug.Log($"[MusicGlitchDirector] Awake OK sur '{gameObject.name}'");
    }

    private void OnEnable()
    {
        if (enableRandomGlitches)
            randomRoutine = StartCoroutine(RandomGlitchLoop());

        if (enableStartupLongMutation)
            startupRoutine = StartCoroutine(StartupLongMutationRoutine());
    }

    private void OnDisable()
    {
        if (randomRoutine != null)
            StopCoroutine(randomRoutine);

        if (startupRoutine != null)
            StopCoroutine(startupRoutine);

        if (currentEventRoutine != null)
            StopCoroutine(currentEventRoutine);

        if (currentLongMutationRoutine != null)
            StopCoroutine(currentLongMutationRoutine);

        ClearAllGlitches();
    }

    private void Update()
    {
        UpdateInputHeat();
        UpdateLayers();
        ApplyLayerModulations();
        UpdateEcology();
    }

    private void SetupAudioSource()
    {
        if (musicSource != null)
            musicSource.spatialBlend = 0f;
    }

    private void SetupFilters()
    {
        if (useLowPassFilter)
        {
            lowPassFilter = GetComponent<AudioLowPassFilter>();

            if (lowPassFilter == null && autoAddMissingFilters)
                lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();

            if (lowPassFilter != null)
            {
                lowPassFilter.enabled = true;
                lowPassFilter.cutoffFrequency = neutralLowPassCutoff;
                lowPassFilter.lowpassResonanceQ = lowPassResonanceQ;
            }
        }

        if (useHighPassFilter)
        {
            highPassFilter = GetComponent<AudioHighPassFilter>();

            if (highPassFilter == null && autoAddMissingFilters)
                highPassFilter = gameObject.AddComponent<AudioHighPassFilter>();

            if (highPassFilter != null)
            {
                highPassFilter.enabled = true;
                highPassFilter.cutoffFrequency = neutralHighPassCutoff;
                highPassFilter.highpassResonanceQ = highPassResonanceQ;
            }
        }

        if (useDistortionFilter)
        {
            distortionFilter = GetComponent<AudioDistortionFilter>();

            if (distortionFilter == null && autoAddMissingFilters)
                distortionFilter = gameObject.AddComponent<AudioDistortionFilter>();

            if (distortionFilter != null)
            {
                distortionFilter.enabled = true;
                distortionFilter.distortionLevel = 0f;
            }
        }

        if (useEchoFilter)
        {
            echoFilter = GetComponent<AudioEchoFilter>();

            if (echoFilter == null && autoAddMissingFilters)
                echoFilter = gameObject.AddComponent<AudioEchoFilter>();

            if (echoFilter != null)
            {
                echoFilter.enabled = true;
                echoFilter.delay = 230f;
                echoFilter.decayRatio = 0.28f;
                echoFilter.dryMix = 1f;
                echoFilter.wetMix = 0f;
            }
        }
    }

    private void UpdateInputHeat()
    {
        inputHeat = Mathf.MoveTowards(
            inputHeat,
            0f,
            inputHeatDecayPerSecond * Time.unscaledDeltaTime
        );

        if (!monitorPlayerClicks)
            return;

        if (InputMouse.current == null)
            return;

        if (InputMouse.current.leftButton.wasPressedThisFrame)
            RegisterPlayerInput();
    }

    public void RegisterPlayerInput()
    {
        inputHeat += inputHeatPerClick;
        inputHeat = Mathf.Clamp(inputHeat, 0f, 5f);

        if (debugLogs && inputHeat > 1f)
            Debug.Log($"[MusicGlitchDirector] Input heat très élevé : {inputHeat:0.00}");
    }

    public void RegisterPlayerInput(float multiplier)
    {
        inputHeat += inputHeatPerClick * Mathf.Max(0f, multiplier);
        inputHeat = Mathf.Clamp(inputHeat, 0f, 5f);
    }

    private IEnumerator StartupLongMutationRoutine()
    {
        if (startupWaitUntilMusicPlays)
        {
            float timer = 0f;

            while (musicSource != null && !musicSource.isPlaying && timer < startupMaxWaitForMusic)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (startupMutationDelay > 0f)
            yield return new WaitForSecondsRealtime(startupMutationDelay);

        if (!CanStartAnyGlitch())
            yield break;

        if (!PassChance(startupLongMutationChance))
            yield break;

        float duration = Random.Range(startupMutationMinDuration, startupMutationMaxDuration);
        duration *= Mathf.Max(0.01f, longMutationDurationMultiplier);

        if (debugLogs)
            Debug.Log($"[MusicGlitchDirector] Mutation longue de lancement décidée : {duration:0.0}s");

        TriggerLongMutation(LongMutationType.Random, duration);
    }

    private IEnumerator RandomGlitchLoop()
    {
        if (randomStartGrace > 0f)
            yield return new WaitForSecondsRealtime(randomStartGrace);

        while (true)
        {
            float interval = Random.Range(randomMinInterval, randomMaxInterval);

            float heatReduction = Mathf.Clamp01(inputHeatIntervalReduction * Mathf.Clamp01(inputHeat));
            float probabilityReduction = Mathf.Clamp01((probabilityMultiplier - 1f) * 0.12f);

            interval *= Mathf.Clamp(1f - heatReduction - probabilityReduction, 0.05f, 1f);

            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, interval));

            if (!enableRandomGlitches)
                continue;

            if (!CanStartAnyGlitch())
                continue;

            float chance = randomGlitchChance * probabilityMultiplier;
            chance += inputHeat * 0.20f;

            if (!PassChance(chance))
                continue;

            if (enableRandomLongMutations)
            {
                float longChance = longMutationChance * probabilityMultiplier;
                longChance += inputHeat * longMutationInputHeatBonus;

                if (PassChance(longChance))
                {
                    float duration = Random.Range(randomLongMutationMinDuration, randomLongMutationMaxDuration);
                    duration *= Mathf.Max(0.01f, longMutationDurationMultiplier);

                    TriggerLongMutation(LongMutationType.Random, duration);
                    continue;
                }
            }

            GlitchIntensity intensity = PickIntensityFromHeatAndProbability();
            TriggerGlitch(GlitchEvent.Random, intensity);
        }
    }

    private bool PassChance(float rawChance)
    {
        if (rawChance <= 0f)
            return false;

        if (rawChance >= 1f)
            return true;

        return Random.value <= rawChance;
    }

    private GlitchIntensity PickIntensityFromHeatAndProbability()
    {
        float heat01 = Mathf.Clamp01(inputHeat);

        float hardChance = Mathf.Lerp(hardChanceWhenCalm, hardChanceWhenInputHot, heat01);
        float mediumChance = Mathf.Lerp(mediumChanceWhenCalm, mediumChanceWhenInputHot, heat01);

        hardChance *= probabilityMultiplier;
        mediumChance *= probabilityMultiplier;

        if (probabilityMultiplier >= 3f || intensityMultiplier >= 3f)
            hardChance += 0.08f;

        if (probabilityMultiplier >= 6f || intensityMultiplier >= 6f)
            hardChance += 0.25f;

        float r = Random.value;

        if (r < Mathf.Clamp01(hardChance))
        {
            if (intensityMultiplier >= 4f || probabilityMultiplier >= 6f)
                return GlitchIntensity.Extreme;

            return GlitchIntensity.Hard;
        }

        if (r < Mathf.Clamp01(hardChance + mediumChance))
            return GlitchIntensity.Medium;

        return GlitchIntensity.Subtle;
    }

    private bool CanStartAnyGlitch()
    {
        if (musicSource == null)
            return false;

        if (requireMusicPlaying && !musicSource.isPlaying)
            return false;

        if (ScreenFader.Instance != null && !allowDuringScreenFade && !ScreenFader.Instance.CanPlayerInteract)
            return false;

        return true;
    }

    private bool IsAtLeast(GlitchIntensity value, GlitchIntensity minimum)
    {
        return (int)value >= (int)minimum;
    }

    private void UpdateLayers()
    {
        float dt = Time.unscaledDeltaTime;

        for (int i = activeLayers.Count - 1; i >= 0; i--)
        {
            activeLayers[i].elapsed += dt;

            if (activeLayers[i].elapsed >= activeLayers[i].duration)
                activeLayers.RemoveAt(i);
        }

        isInLongMutation = false;
        currentMutationName = "";

        for (int i = 0; i < activeLayers.Count; i++)
        {
            if (activeLayers[i].longMutationLayer)
            {
                isInLongMutation = true;
                currentMutationName = activeLayers[i].name;
                break;
            }
        }
    }

    private void ApplyLayerModulations()
    {
        bool hasLayers = activeLayers.Count > 0;

        if (!hasLayers && !lastFrameHadModulation)
            return;

        float safeLastVolumeFactor = Mathf.Max(0.0001f, lastVolumeFactor);
        float safeLastPitchFactor = Mathf.Max(0.0001f, lastPitchFactor);

        float baseVolume = Mathf.Clamp01(musicSource.volume / safeLastVolumeFactor);
        float basePitch = Mathf.Clamp(musicSource.pitch / safeLastPitchFactor, minPitch, maxPitch);
        float basePan = Mathf.Clamp(musicSource.panStereo - lastPanOffset, -1f, 1f);

        float volumeDb = 0f;
        float semitones = 0f;
        float pan = 0f;

        bool wantsLowPass = false;
        bool wantsHighPass = false;

        float wantedLowPass = neutralLowPassCutoff;
        float wantedHighPass = neutralHighPassCutoff;

        float distortion = 0f;
        float echo = 0f;

        for (int i = 0; i < activeLayers.Count; i++)
        {
            ModLayer layer = activeLayers[i];
            float w = layer.Weight;

            volumeDb += layer.volumeDbOffset * w;
            semitones += layer.pitchSemitoneOffset * w;
            pan += layer.panOffset * w;

            if (layer.lowPassCutoff > 0f)
            {
                wantsLowPass = true;
                wantedLowPass = Mathf.Min(wantedLowPass, Mathf.Lerp(neutralLowPassCutoff, layer.lowPassCutoff, w));
            }

            if (layer.highPassCutoff > 0f)
            {
                wantsHighPass = true;
                wantedHighPass = Mathf.Max(wantedHighPass, Mathf.Lerp(neutralHighPassCutoff, layer.highPassCutoff, w));
            }

            distortion = Mathf.Max(distortion, layer.distortion * w);
            echo = Mathf.Max(echo, layer.echoWet * w);
        }

        volumeDb = Mathf.Clamp(volumeDb, -60f, 18f);
        semitones = Mathf.Clamp(semitones, -24f, 12f);
        pan = Mathf.Clamp(pan, -0.9f, 0.9f);
        distortion = Mathf.Clamp01(distortion);
        echo = Mathf.Clamp01(echo);

        float newVolumeFactor = DbToLinear(volumeDb);
        float newPitchFactor = SemitoneToPitchFactor(semitones);

        musicSource.volume = Mathf.Clamp01(baseVolume * newVolumeFactor);
        musicSource.pitch = Mathf.Clamp(basePitch * newPitchFactor, minPitch, maxPitch);
        musicSource.panStereo = Mathf.Clamp(basePan + pan, -1f, 1f);

        if (lowPassFilter != null)
        {
            lowPassFilter.cutoffFrequency = wantsLowPass
                ? Mathf.Clamp(wantedLowPass, 180f, neutralLowPassCutoff)
                : neutralLowPassCutoff;

            lowPassFilter.lowpassResonanceQ = lowPassResonanceQ;
        }

        if (highPassFilter != null)
        {
            highPassFilter.cutoffFrequency = wantsHighPass
                ? Mathf.Clamp(wantedHighPass, neutralHighPassCutoff, 9000f)
                : neutralHighPassCutoff;

            highPassFilter.highpassResonanceQ = highPassResonanceQ;
        }

        if (distortionFilter != null)
            distortionFilter.distortionLevel = distortion;

        if (echoFilter != null)
        {
            echoFilter.wetMix = echo;
            echoFilter.dryMix = 1f;
        }

        lastVolumeFactor = newVolumeFactor;
        lastPitchFactor = newPitchFactor;
        lastPanOffset = pan;
        lastFrameHadModulation = hasLayers;
    }

    private void UpdateEcology()
    {
        if (!exposeGlitchEcology)
            return;

        float rawPressure = 0f;
        GlitchBiome dominantBiome = GlitchBiome.None;
        float dominantWeight = 0f;

        for (int i = 0; i < activeLayers.Count; i++)
        {
            ModLayer layer = activeLayers[i];
            float w = layer.Weight;

            float contribution = w * Mathf.Max(0.1f, layer.visualWeight);
            rawPressure += contribution;

            if (contribution > dominantWeight)
            {
                dominantWeight = contribution;
                dominantBiome = layer.biome;
            }
        }

        rawPressure += Mathf.Clamp01(inputHeat) * 0.25f;
        rawPressure = Mathf.Clamp01(rawPressure / 4f);

        glitchPressure = Mathf.Lerp(
            glitchPressure,
            rawPressure,
            1f - Mathf.Exp(-ecologySmoothing * Time.unscaledDeltaTime)
        );

        currentBiome = dominantBiome;

        if (currentBiome != lastSentBiome || Mathf.Abs(glitchPressure - lastSentPressure) > 0.03f)
        {
            lastSentBiome = currentBiome;
            lastSentPressure = glitchPressure;

            OnGlitchEcologyChanged?.Invoke(currentBiome, glitchPressure, isInLongMutation);
        }
    }

    private void AddLayer(
        string name,
        float duration,
        float attack,
        float release,
        float volumeDbOffset,
        float pitchSemitoneOffset,
        float panOffset = 0f,
        float lowPassCutoff = -1f,
        float highPassCutoff = -1f,
        float distortion = 0f,
        float echoWet = 0f,
        bool hard = false,
        bool eventLayer = false,
        bool longMutationLayer = false,
        GlitchBiome biome = GlitchBiome.Neutral,
        float visualWeight = 1f
    )
    {
        float power = eventLayer ? eventIntensityMultiplier : intensityMultiplier;

        if (longMutationLayer)
            power *= Mathf.Max(0.01f, longMutationDurationMultiplier * 0.35f + 0.65f);

        volumeDbOffset *= power;
        pitchSemitoneOffset *= power;
        panOffset *= Mathf.Sqrt(Mathf.Max(0.01f, power));
        distortion *= Mathf.Sqrt(Mathf.Max(0.01f, power));
        echoWet *= Mathf.Sqrt(Mathf.Max(0.01f, power));

        if (duration <= 0f)
            return;

        if (hard && blockHardOverlap && HasHardLayer())
        {
            if (debugLogs)
                Debug.Log("[MusicGlitchDirector] Hard glitch ignoré : hard déjà actif.");

            return;
        }

        if (!allowAmbientLayerOverlap && activeLayers.Count > 0 && !eventLayer && !longMutationLayer)
            return;

        while (activeLayers.Count >= maxSimultaneousLayers)
        {
            int index = FindOldestNonProtectedLayer();

            if (index < 0)
                break;

            activeLayers.RemoveAt(index);
        }

        ModLayer layer = new ModLayer
        {
            name = name,
            duration = duration,
            attack = Mathf.Max(0f, attack),
            release = Mathf.Max(0f, release),
            volumeDbOffset = volumeDbOffset,
            pitchSemitoneOffset = pitchSemitoneOffset,
            panOffset = panOffset,
            lowPassCutoff = lowPassCutoff,
            highPassCutoff = highPassCutoff,
            distortion = distortion,
            echoWet = echoWet,
            hard = hard,
            eventLayer = eventLayer,
            longMutationLayer = longMutationLayer,
            biome = biome,
            visualWeight = visualWeight
        };

        activeLayers.Add(layer);

        if (debugLogs)
            Debug.Log($"[MusicGlitchDirector] Layer ajouté : {name} / {duration:0.0}s / biome {biome}");
    }

    private bool HasHardLayer()
    {
        for (int i = 0; i < activeLayers.Count; i++)
        {
            if (activeLayers[i].hard)
                return true;
        }

        return false;
    }

    private bool HasLongMutationLayer()
    {
        for (int i = 0; i < activeLayers.Count; i++)
        {
            if (activeLayers[i].longMutationLayer)
                return true;
        }

        return false;
    }

    private int FindOldestNonProtectedLayer()
    {
        int index = -1;
        float oldest = -1f;

        for (int i = 0; i < activeLayers.Count; i++)
        {
            if (activeLayers[i].eventLayer || activeLayers[i].longMutationLayer)
                continue;

            if (activeLayers[i].elapsed > oldest)
            {
                oldest = activeLayers[i].elapsed;
                index = i;
            }
        }

        return index;
    }

    public void ClearAllGlitches()
    {
        activeLayers.Clear();

        if (musicSource != null && lastFrameHadModulation)
        {
            musicSource.volume = Mathf.Clamp01(musicSource.volume / Mathf.Max(0.0001f, lastVolumeFactor));
            musicSource.pitch = Mathf.Clamp(musicSource.pitch / Mathf.Max(0.0001f, lastPitchFactor), minPitch, maxPitch);
            musicSource.panStereo = Mathf.Clamp(musicSource.panStereo - lastPanOffset, -1f, 1f);
        }

        if (lowPassFilter != null)
            lowPassFilter.cutoffFrequency = neutralLowPassCutoff;

        if (highPassFilter != null)
            highPassFilter.cutoffFrequency = neutralHighPassCutoff;

        if (distortionFilter != null)
            distortionFilter.distortionLevel = 0f;

        if (echoFilter != null)
            echoFilter.wetMix = 0f;

        lastVolumeFactor = 1f;
        lastPitchFactor = 1f;
        lastPanOffset = 0f;
        lastFrameHadModulation = false;

        isInLongMutation = false;
        currentMutationName = "";
        currentBiome = GlitchBiome.None;
        glitchPressure = 0f;
    }

    private void ClearAmbientLayersForEvent()
    {
        if (!eventGlitchesClearAmbientLayers)
            return;

        for (int i = activeLayers.Count - 1; i >= 0; i--)
        {
            if (!activeLayers[i].eventLayer && !activeLayers[i].longMutationLayer)
                activeLayers.RemoveAt(i);
        }
    }

    private void ClearAmbientLayersForLongMutation()
    {
        if (!longMutationClearsAmbientLayers)
            return;

        for (int i = activeLayers.Count - 1; i >= 0; i--)
        {
            if (!activeLayers[i].eventLayer)
                activeLayers.RemoveAt(i);
        }
    }

    public void TriggerSubtleGlitch() => TriggerGlitch(GlitchEvent.Random, GlitchIntensity.Subtle);
    public void TriggerMediumGlitch() => TriggerGlitch(GlitchEvent.Random, GlitchIntensity.Medium);
    public void TriggerHardGlitch() => TriggerGlitch(GlitchEvent.Random, GlitchIntensity.Hard);
    public void TriggerExtremeGlitch() => TriggerGlitch(GlitchEvent.Random, GlitchIntensity.Extreme);

    public void TriggerNeutralVeil() => TriggerGlitch(GlitchEvent.NeutralVeil, GlitchIntensity.Medium);
    public void TriggerSlowPitchDrop() => TriggerGlitch(GlitchEvent.SlowPitchDrop, GlitchIntensity.Medium);
    public void TriggerMemoryLeak() => TriggerGlitch(GlitchEvent.MemoryLeak, GlitchIntensity.Medium);
    public void TriggerSituationShift() => TriggerGlitch(GlitchEvent.SituationShift, GlitchIntensity.Hard);
    public void TriggerScreamerPreShock() => TriggerGlitch(GlitchEvent.ScreamerPreShock, GlitchIntensity.Hard);
    public void TriggerScreamerImpact() => TriggerGlitch(GlitchEvent.ScreamerImpact, GlitchIntensity.Hard);
    public void TriggerSystemCollapse() => TriggerGlitch(GlitchEvent.SystemCollapse, GlitchIntensity.Extreme);
    public void TriggerFalseRecovery() => TriggerGlitch(GlitchEvent.FalseRecovery, GlitchIntensity.Hard);

    public void TriggerRandomLongMutation()
    {
        TriggerLongMutation(LongMutationType.Random, -1f);
    }

    public void TriggerWronglyCalmWorld()
    {
        TriggerLongMutation(LongMutationType.WronglyCalmWorld, -1f);
    }

    public void TriggerRottenTapeWorld()
    {
        TriggerLongMutation(LongMutationType.RottenTapeWorld, -1f);
    }

    public void TriggerDeepSystemIllness()
    {
        TriggerLongMutation(LongMutationType.DeepSystemIllness, -1f);
    }

    public void TriggerRemoteBroadcast()
    {
        TriggerLongMutation(LongMutationType.RemoteBroadcast, -1f);
    }

    public void TriggerFalseNormalLong()
    {
        TriggerLongMutation(LongMutationType.FalseNormal, -1f);
    }

    public void TriggerVacuumRoom()
    {
        TriggerLongMutation(LongMutationType.VacuumRoom, -1f);
    }

    public void TriggerBeautifulFailure()
    {
        TriggerLongMutation(LongMutationType.BeautifulFailure, -1f);
    }

    public void TriggerProgramSleep()
    {
        TriggerLongMutation(LongMutationType.ProgramSleep, -1f);
    }

    public void TriggerDiseasedMemory()
    {
        TriggerLongMutation(LongMutationType.DiseasedMemory, -1f);
    }

    public void TriggerLongMutation(LongMutationType mutationType, float forcedDuration)
    {
        if (!CanStartAnyGlitch())
        {
            if (debugLogs)
                Debug.Log("[MusicGlitchDirector] Mutation longue ignorée : conditions non remplies.");

            return;
        }

        if (!allowLongMutationOverlap && HasLongMutationLayer())
        {
            if (debugLogs)
                Debug.Log("[MusicGlitchDirector] Mutation longue ignorée : une mutation longue est déjà active.");

            return;
        }

        if (mutationType == LongMutationType.Random)
            mutationType = PickRandomLongMutationType();

        float duration = forcedDuration > 0f
            ? forcedDuration
            : Random.Range(randomLongMutationMinDuration, randomLongMutationMaxDuration) * Mathf.Max(0.01f, longMutationDurationMultiplier);

        if (currentLongMutationRoutine != null && !allowLongMutationOverlap)
            StopCoroutine(currentLongMutationRoutine);

        currentLongMutationRoutine = StartCoroutine(LongMutationRoutine(mutationType, duration));
    }

    private LongMutationType PickRandomLongMutationType()
    {
        float r = Random.value;

        if (r < 0.14f) return LongMutationType.WronglyCalmWorld;
        if (r < 0.28f) return LongMutationType.RottenTapeWorld;
        if (r < 0.42f) return LongMutationType.DeepSystemIllness;
        if (r < 0.55f) return LongMutationType.RemoteBroadcast;
        if (r < 0.68f) return LongMutationType.FalseNormal;
        if (r < 0.80f) return LongMutationType.VacuumRoom;
        if (r < 0.90f) return LongMutationType.BeautifulFailure;
        if (r < 0.96f) return LongMutationType.ProgramSleep;
        return LongMutationType.DiseasedMemory;
    }

    private IEnumerator LongMutationRoutine(LongMutationType mutationType, float duration)
    {
        ClearAmbientLayersForLongMutation();

        isInLongMutation = true;
        currentMutationName = mutationType.ToString();

        if (debugLogs)
            Debug.Log($"[MusicGlitchDirector] Mutation longue : {mutationType} / {duration:0.0}s");

        switch (mutationType)
        {
            case LongMutationType.WronglyCalmWorld:
                DoLongMutationWronglyCalmWorld(duration);
                break;

            case LongMutationType.RottenTapeWorld:
                DoLongMutationRottenTapeWorld(duration);
                break;

            case LongMutationType.DeepSystemIllness:
                DoLongMutationDeepSystemIllness(duration);
                break;

            case LongMutationType.RemoteBroadcast:
                DoLongMutationRemoteBroadcast(duration);
                break;

            case LongMutationType.FalseNormal:
                DoLongMutationFalseNormal(duration);
                break;

            case LongMutationType.VacuumRoom:
                DoLongMutationVacuumRoom(duration);
                break;

            case LongMutationType.BeautifulFailure:
                DoLongMutationBeautifulFailure(duration);
                break;

            case LongMutationType.ProgramSleep:
                DoLongMutationProgramSleep(duration);
                break;

            case LongMutationType.DiseasedMemory:
                DoLongMutationDiseasedMemory(duration);
                break;
        }

        yield return new WaitForSecondsRealtime(duration);

        currentLongMutationRoutine = null;
    }

    private void DoLongMutationWronglyCalmWorld(float duration)
    {
        AddLayer(
            "long_wrongly_calm_world_core",
            duration,
            duration * 0.18f,
            duration * 0.32f,
            -8f,
            -0.8f,
            Random.Range(-0.04f, 0.04f),
            Random.Range(5200f, 9000f),
            -1f,
            0.01f,
            0.04f,
            false,
            false,
            true,
            GlitchBiome.Neutral,
            2.2f
        );

        AddLayer(
            "long_wrongly_calm_world_thin_layer",
            duration * Random.Range(0.65f, 0.95f),
            duration * 0.25f,
            duration * 0.40f,
            -3f,
            Random.Range(0.15f, 0.55f),
            Random.Range(-0.06f, 0.06f),
            -1f,
            Random.Range(120f, 260f),
            0f,
            0.03f,
            false,
            false,
            true,
            GlitchBiome.FalseNormal,
            1.2f
        );
    }

    private void DoLongMutationRottenTapeWorld(float duration)
    {
        AddLayer(
            "long_rotten_tape_drag",
            duration,
            duration * 0.20f,
            duration * 0.38f,
            -5f,
            -2.2f,
            Random.Range(-0.09f, 0.09f),
            Random.Range(6000f, 13000f),
            -1f,
            0.05f,
            0.12f,
            false,
            false,
            true,
            GlitchBiome.Tape,
            2.6f
        );

        AddLayer(
            "long_rotten_tape_air",
            duration * Random.Range(0.75f, 1f),
            duration * 0.18f,
            duration * 0.45f,
            -8f,
            0.35f,
            Random.Range(-0.12f, 0.12f),
            -1f,
            Random.Range(160f, 420f),
            0.02f,
            0.08f,
            false,
            false,
            true,
            GlitchBiome.Tape,
            1.6f
        );
    }

    private void DoLongMutationDeepSystemIllness(float duration)
    {
        AddLayer(
            "long_deep_system_illness",
            duration,
            duration * 0.22f,
            duration * 0.42f,
            -11f,
            -5.2f,
            Random.Range(-0.12f, 0.12f),
            Random.Range(900f, 2800f),
            Random.Range(120f, 400f),
            0.22f,
            0.30f,
            true,
            false,
            true,
            GlitchBiome.Deep,
            3.4f
        );

        if (musicSource != null && musicSource.clip != null && Random.value < 0.55f)
            StartCoroutine(DelayedSoftSeek(Random.Range(5f, 12f), Random.Range(-4f, 5f)));
    }

    private void DoLongMutationRemoteBroadcast(float duration)
    {
        AddLayer(
            "long_remote_broadcast_band",
            duration,
            duration * 0.15f,
            duration * 0.40f,
            -13f,
            -0.55f,
            Random.Range(-0.03f, 0.03f),
            Random.Range(1800f, 4200f),
            Random.Range(450f, 1100f),
            0.03f,
            0.10f,
            false,
            false,
            true,
            GlitchBiome.Broadcast,
            2.7f
        );

        AddLayer(
            "long_remote_broadcast_unstable_pitch",
            duration * Random.Range(0.5f, 0.9f),
            duration * 0.25f,
            duration * 0.30f,
            -2f,
            Random.Range(-1.1f, 0.9f),
            Random.Range(-0.08f, 0.08f),
            -1f,
            -1f,
            0f,
            0.05f,
            false,
            false,
            true,
            GlitchBiome.Broadcast,
            1.3f
        );
    }

    private void DoLongMutationFalseNormal(float duration)
    {
        AddLayer(
            "long_false_normal",
            duration,
            duration * 0.25f,
            duration * 0.45f,
            -2.5f,
            Random.Range(0.45f, 1.2f),
            Random.Range(-0.04f, 0.04f),
            Random.Range(12000f, 18000f),
            -1f,
            0.01f,
            0.04f,
            false,
            false,
            true,
            GlitchBiome.FalseNormal,
            2.4f
        );

        AddLayer(
            "long_false_normal_wrong_shadow",
            duration * Random.Range(0.4f, 0.8f),
            duration * 0.30f,
            duration * 0.30f,
            -9f,
            Random.Range(-2.0f, -0.8f),
            Random.Range(-0.1f, 0.1f),
            Random.Range(4200f, 9000f),
            -1f,
            0.02f,
            0.09f,
            false,
            false,
            true,
            GlitchBiome.FalseNormal,
            1.5f
        );
    }

    private void DoLongMutationVacuumRoom(float duration)
    {
        AddLayer(
            "long_vacuum_room",
            duration,
            duration * 0.12f,
            duration * 0.48f,
            -22f,
            -1.4f,
            0f,
            Random.Range(1800f, 5200f),
            Random.Range(180f, 900f),
            0.01f,
            0.02f,
            true,
            false,
            true,
            GlitchBiome.Vacuum,
            3.1f
        );
    }

    private void DoLongMutationBeautifulFailure(float duration)
    {
        AddLayer(
            "long_beautiful_failure_low",
            duration,
            duration * 0.22f,
            duration * 0.38f,
            -5f,
            Random.Range(-1.6f, -0.4f),
            Random.Range(-0.08f, 0.08f),
            Random.Range(5000f, 11000f),
            -1f,
            0.025f,
            0.18f,
            false,
            false,
            true,
            GlitchBiome.Possession,
            2.2f
        );

        AddLayer(
            "long_beautiful_failure_high",
            duration * Random.Range(0.6f, 0.95f),
            duration * 0.35f,
            duration * 0.30f,
            -11f,
            Random.Range(1.0f, 2.4f),
            Random.Range(-0.16f, 0.16f),
            -1f,
            Random.Range(200f, 520f),
            0.02f,
            0.16f,
            false,
            false,
            true,
            GlitchBiome.Possession,
            1.8f
        );
    }

    private void DoLongMutationProgramSleep(float duration)
    {
        AddLayer(
            "long_program_sleep",
            duration,
            duration * 0.28f,
            duration * 0.42f,
            -18f,
            -3.2f,
            0f,
            Random.Range(2500f, 7000f),
            Random.Range(140f, 360f),
            0.03f,
            0.05f,
            true,
            false,
            true,
            GlitchBiome.Neutral,
            2.8f
        );
    }

    private void DoLongMutationDiseasedMemory(float duration)
    {
        AddLayer(
            "long_diseased_memory_main",
            duration,
            duration * 0.18f,
            duration * 0.50f,
            -9f,
            -3.8f,
            Random.Range(-0.1f, 0.1f),
            Random.Range(1300f, 4400f),
            Random.Range(120f, 600f),
            0.12f,
            0.26f,
            true,
            false,
            true,
            GlitchBiome.Digital,
            3.3f
        );

        if (musicSource != null && musicSource.clip != null && Random.value < 0.75f)
            StartCoroutine(RepeatedMemorySlipRoutine(duration));
    }

    private IEnumerator DelayedSoftSeek(float delay, float offset)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (musicSource != null && musicSource.clip != null)
            SafeSeek(musicSource.time + offset);
    }

    private IEnumerator RepeatedMemorySlipRoutine(float totalDuration)
    {
        float timer = 0f;

        while (timer < totalDuration * 0.75f)
        {
            float wait = Random.Range(6f, 16f);
            timer += wait;

            yield return new WaitForSecondsRealtime(wait);

            if (musicSource == null || musicSource.clip == null)
                yield break;

            SafeSeek(musicSource.time + Random.Range(-1.2f, 1.8f));
        }
    }

    public void TriggerGlitch(GlitchEvent glitchEvent, GlitchIntensity intensity)
    {
        if (!CanStartAnyGlitch())
        {
            if (debugLogs)
                Debug.Log("[MusicGlitchDirector] Glitch ignoré : conditions non remplies.");

            return;
        }

        if (glitchEvent == GlitchEvent.Random)
            glitchEvent = PickRandomGlitchEvent(intensity);

        if (debugLogs)
            Debug.Log($"[MusicGlitchDirector] Trigger : {glitchEvent} / {intensity}");

        switch (glitchEvent)
        {
            case GlitchEvent.AlmostNothing:
                DoAlmostNothing();
                break;

            case GlitchEvent.LongPitchDrift:
                DoLongPitchDrift(intensity);
                break;

            case GlitchEvent.SlowPitchDrop:
                DoSlowPitchDrop(intensity);
                break;

            case GlitchEvent.NeutralVeil:
                DoNeutralVeil(intensity);
                break;

            case GlitchEvent.TapeSickness:
                DoTapeSickness(intensity);
                break;

            case GlitchEvent.RoomBreathing:
                DoRoomBreathing(intensity);
                break;

            case GlitchEvent.MemoryLeak:
                DoMemoryLeak(intensity);
                break;

            case GlitchEvent.DistantCompression:
                DoDistantCompression(intensity);
                break;

            case GlitchEvent.StereoUnease:
                DoStereoUnease(intensity);
                break;

            case GlitchEvent.LowFidelityDip:
                DoLowFidelityDip(intensity);
                break;

            case GlitchEvent.LayeredUnease:
                DoLayeredUnease(intensity);
                break;

            case GlitchEvent.SoftTimeSlip:
                StartCoroutine(SoftTimeSlipRoutine(intensity));
                break;

            case GlitchEvent.GhostStutter:
                StartCoroutine(GhostStutterRoutine(intensity));
                break;

            case GlitchEvent.NeedleWobble:
                StartCoroutine(NeedleWobbleRoutine(intensity));
                break;

            case GlitchEvent.ProgramDesync:
                StartCoroutine(ProgramDesyncRoutine(intensity));
                break;

            case GlitchEvent.HardSystemSlip:
                StartEventRoutine(HardSystemSlipRoutine());
                break;

            case GlitchEvent.SituationShift:
                StartEventRoutine(SituationShiftRoutine());
                break;

            case GlitchEvent.ScreamerPreShock:
                StartEventRoutine(ScreamerPreShockRoutine());
                break;

            case GlitchEvent.ScreamerImpact:
                StartEventRoutine(ScreamerImpactRoutine());
                break;

            case GlitchEvent.TotalNeutralization:
                StartEventRoutine(TotalNeutralizationRoutine());
                break;

            case GlitchEvent.AudioPossession:
                StartEventRoutine(AudioPossessionRoutine());
                break;

            case GlitchEvent.BrokenLoop:
                StartEventRoutine(BrokenLoopRoutine());
                break;

            case GlitchEvent.SystemCollapse:
                StartEventRoutine(SystemCollapseRoutine());
                break;

            case GlitchEvent.FalseRecovery:
                StartEventRoutine(FalseRecoveryRoutine());
                break;
        }
    }

    private void StartEventRoutine(IEnumerator routine)
    {
        if (currentEventRoutine != null)
            StopCoroutine(currentEventRoutine);

        currentEventRoutine = StartCoroutine(EventWrapperRoutine(routine));
    }

    private IEnumerator EventWrapperRoutine(IEnumerator routine)
    {
        ClearAmbientLayersForEvent();
        yield return routine;
        currentEventRoutine = null;
    }

    private GlitchEvent PickRandomGlitchEvent(GlitchIntensity intensity)
    {
        float r = Random.value;

        if (intensity == GlitchIntensity.Subtle)
        {
            if (r < 0.16f) return GlitchEvent.AlmostNothing;
            if (r < 0.32f) return GlitchEvent.LongPitchDrift;
            if (r < 0.48f) return GlitchEvent.NeutralVeil;
            if (r < 0.64f) return GlitchEvent.TapeSickness;
            if (r < 0.80f) return GlitchEvent.RoomBreathing;
            if (r < 0.92f) return GlitchEvent.SlowPitchDrop;
            return GlitchEvent.StereoUnease;
        }

        if (intensity == GlitchIntensity.Medium)
        {
            if (r < 0.15f) return GlitchEvent.LayeredUnease;
            if (r < 0.30f) return GlitchEvent.MemoryLeak;
            if (r < 0.45f) return GlitchEvent.LowFidelityDip;
            if (r < 0.60f) return GlitchEvent.SoftTimeSlip;
            if (r < 0.75f) return GlitchEvent.NeedleWobble;
            if (r < 0.90f) return GlitchEvent.ProgramDesync;
            return GlitchEvent.GhostStutter;
        }

        if (intensity == GlitchIntensity.Hard)
        {
            if (r < 0.30f) return GlitchEvent.ProgramDesync;
            if (r < 0.52f) return GlitchEvent.HardSystemSlip;
            if (r < 0.72f) return GlitchEvent.TotalNeutralization;
            if (r < 0.88f) return GlitchEvent.BrokenLoop;
            return GlitchEvent.FalseRecovery;
        }

        if (r < 0.35f) return GlitchEvent.SystemCollapse;
        if (r < 0.58f) return GlitchEvent.ScreamerImpact;
        if (r < 0.78f) return GlitchEvent.AudioPossession;
        return GlitchEvent.SituationShift;
    }

    private void DoAlmostNothing()
    {
        AddLayer(
            "almost_nothing",
            Random.Range(14f, 34f),
            5f,
            9f,
            -0.8f,
            -0.15f,
            Random.Range(-0.02f, 0.02f),
            -1f,
            -1f,
            0f,
            0f,
            false,
            false,
            false,
            GlitchBiome.Neutral,
            0.35f
        );
    }

    private void DoLongPitchDrift(GlitchIntensity intensity)
    {
        float duration = intensity == GlitchIntensity.Subtle ? Random.Range(18f, 42f) : Random.Range(12f, 28f);
        float pitch = intensity == GlitchIntensity.Subtle ? Random.Range(-0.8f, 0.2f) : Random.Range(-2.0f, 0.35f);

        AddLayer(
            "long_pitch_drift",
            duration,
            duration * 0.35f,
            duration * 0.45f,
            -1.2f,
            pitch,
            Random.Range(-0.04f, 0.04f),
            -1f,
            -1f,
            0f,
            0.02f,
            false,
            false,
            false,
            GlitchBiome.Neutral,
            0.8f
        );
    }

    private void DoSlowPitchDrop(GlitchIntensity intensity)
    {
        float d = intensity == GlitchIntensity.Subtle ? Random.Range(10f, 20f) :
                  intensity == GlitchIntensity.Medium ? Random.Range(8f, 18f) :
                  Random.Range(6f, 14f);

        float p = intensity == GlitchIntensity.Subtle ? Random.Range(-1.6f, -0.5f) :
                  intensity == GlitchIntensity.Medium ? Random.Range(-3.4f, -1.2f) :
                  Random.Range(-6.4f, -2.4f);

        bool hard = IsAtLeast(intensity, GlitchIntensity.Hard);

        AddLayer(
            "slow_pitch_drop",
            d,
            d * 0.35f,
            d * 0.50f,
            -3f,
            p,
            0f,
            intensity == GlitchIntensity.Subtle ? -1f : 7000f,
            -1f,
            hard ? 0.08f : 0.03f,
            hard ? 0.12f : 0.05f,
            hard,
            hard,
            false,
            hard ? GlitchBiome.Deep : GlitchBiome.Neutral,
            hard ? 2.0f : 1.0f
        );
    }

    private void DoNeutralVeil(GlitchIntensity intensity)
    {
        float d = intensity == GlitchIntensity.Subtle ? Random.Range(14f, 32f) : Random.Range(10f, 22f);
        float lp = intensity == GlitchIntensity.Subtle ? Random.Range(7000f, 15000f) : Random.Range(2200f, 8500f);

        AddLayer(
            "neutral_veil",
            d,
            4f,
            8f,
            -6f,
            -0.35f,
            Random.Range(-0.03f, 0.03f),
            lp,
            -1f,
            0.01f,
            0.04f,
            false,
            false,
            false,
            GlitchBiome.Neutral,
            1.2f
        );
    }

    private void DoTapeSickness(GlitchIntensity intensity)
    {
        float d = intensity == GlitchIntensity.Subtle ? Random.Range(20f, 46f) : Random.Range(14f, 32f);

        AddLayer(
            "tape_sickness_low",
            d,
            d * 0.25f,
            d * 0.40f,
            -2.5f,
            -1.1f,
            Random.Range(-0.06f, 0.06f),
            Random.Range(8000f, 16000f),
            -1f,
            0.03f,
            0.06f,
            false,
            false,
            false,
            GlitchBiome.Tape,
            1.4f
        );

        if (allowAmbientLayerOverlap)
        {
            AddLayer(
                "tape_sickness_thin",
                d * 0.8f,
                d * 0.25f,
                d * 0.35f,
                -1.8f,
                0.35f,
                Random.Range(-0.1f, 0.1f),
                -1f,
                Random.Range(100f, 300f),
                0f,
                0.02f,
                false,
                false,
                false,
                GlitchBiome.Tape,
                0.8f
            );
        }
    }

    private void DoRoomBreathing(GlitchIntensity intensity)
    {
        AddLayer(
            "room_breathing",
            Random.Range(24f, 54f),
            8f,
            12f,
            -4f,
            -0.45f,
            Random.Range(-0.04f, 0.04f),
            Random.Range(4500f, 11000f),
            -1f,
            0f,
            0.08f,
            false,
            false,
            false,
            GlitchBiome.Neutral,
            1.1f
        );
    }

    private void DoMemoryLeak(GlitchIntensity intensity)
    {
        AddLayer(
            "memory_leak",
            Random.Range(22f, 52f),
            10f,
            9f,
            -5f,
            -2f,
            Random.Range(-0.08f, 0.08f),
            Random.Range(3500f, 9000f),
            -1f,
            0.06f,
            0.12f,
            false,
            false,
            false,
            GlitchBiome.Digital,
            1.6f
        );
    }

    private void DoDistantCompression(GlitchIntensity intensity)
    {
        AddLayer(
            "distant_compression",
            Random.Range(8f, 22f),
            2f,
            6f,
            -8f,
            -0.8f,
            0f,
            Random.Range(2400f, 6500f),
            Random.Range(120f, 450f),
            0.06f,
            0.14f,
            false,
            false,
            false,
            GlitchBiome.Broadcast,
            1.3f
        );
    }

    private void DoStereoUnease(GlitchIntensity intensity)
    {
        AddLayer(
            "stereo_unease",
            Random.Range(10f, 26f),
            4f,
            7f,
            -1.5f,
            -0.2f,
            Random.Range(-0.18f, 0.18f),
            -1f,
            -1f,
            0f,
            0.02f,
            false,
            false,
            false,
            GlitchBiome.Neutral,
            0.9f
        );
    }

    private void DoLowFidelityDip(GlitchIntensity intensity)
    {
        AddLayer(
            "low_fidelity_dip",
            Random.Range(7f, 18f),
            2f,
            5f,
            -7f,
            -1f,
            0f,
            Random.Range(1800f, 5000f),
            Random.Range(200f, 900f),
            0.10f,
            0.18f,
            false,
            false,
            false,
            GlitchBiome.Broadcast,
            1.4f
        );
    }

    private void DoLayeredUnease(GlitchIntensity intensity)
    {
        DoSlowPitchDrop(GlitchIntensity.Subtle);
        DoNeutralVeil(GlitchIntensity.Subtle);

        if (Random.value < 0.6f)
            DoTapeSickness(GlitchIntensity.Subtle);

        if (Random.value < 0.45f)
            DoStereoUnease(GlitchIntensity.Subtle);
    }

    private IEnumerator SoftTimeSlipRoutine(GlitchIntensity intensity)
    {
        if (musicSource.clip == null)
            yield break;

        bool hard = IsAtLeast(intensity, GlitchIntensity.Hard);

        AddLayer(
            "soft_time_slip_cover",
            hard ? 8f : 5f,
            1f,
            hard ? 5f : 3f,
            hard ? -10f : -5f,
            hard ? -4f : -1.4f,
            0f,
            hard ? 2500f : 9000f,
            -1f,
            hard ? 0.12f : 0.04f,
            hard ? 0.22f : 0.08f,
            hard,
            hard,
            false,
            hard ? GlitchBiome.Deep : GlitchBiome.Digital,
            hard ? 2.2f : 1.1f
        );

        yield return new WaitForSecondsRealtime(Random.Range(1.2f, 2.7f));

        SafeSeek(musicSource.time + (hard ? Random.Range(-3.2f, 4f) : Random.Range(-0.8f, 1f)));
    }

    private IEnumerator GhostStutterRoutine(GlitchIntensity intensity)
    {
        if (musicSource.clip == null)
            yield break;

        float point = musicSource.time;
        bool hard = IsAtLeast(intensity, GlitchIntensity.Hard);

        AddLayer(
            "ghost_stutter_cover",
            hard ? 5f : 3f,
            0.5f,
            2.5f,
            hard ? -10f : -5f,
            hard ? -2.6f : -1f,
            0f,
            hard ? 3000f : 9000f,
            -1f,
            hard ? 0.12f : 0.04f,
            hard ? 0.2f : 0.08f,
            hard,
            hard,
            false,
            GlitchBiome.Digital,
            hard ? 2.1f : 1.2f
        );

        int repeats = hard ? 6 : 3;

        for (int i = 0; i < repeats; i++)
        {
            yield return new WaitForSecondsRealtime(hard ? 0.16f : 0.12f);
            SafeSeek(point + Random.Range(-0.05f, 0.04f));
        }
    }

    private IEnumerator NeedleWobbleRoutine(GlitchIntensity intensity)
    {
        AddLayer(
            "needle_wobble",
            Random.Range(5f, 10f),
            0.8f,
            4f,
            -5f,
            -2f,
            Random.Range(-0.06f, 0.06f),
            Random.Range(4000f, 11000f),
            -1f,
            0.06f,
            0.12f,
            false,
            false,
            false,
            GlitchBiome.Tape,
            1.3f
        );

        yield return new WaitForSecondsRealtime(Random.Range(1f, 2f));

        if (musicSource.clip != null)
            SafeSeek(musicSource.time + Random.Range(-0.25f, 0.35f));
    }

    private IEnumerator ProgramDesyncRoutine(GlitchIntensity intensity)
    {
        bool hard = IsAtLeast(intensity, GlitchIntensity.Hard);

        AddLayer(
            "program_desync",
            hard ? 12f : 7f,
            hard ? 2.5f : 1.5f,
            hard ? 6f : 4f,
            hard ? -12f : -6f,
            hard ? -5f : -2f,
            Random.Range(-0.12f, 0.12f),
            hard ? 1800f : 6000f,
            hard ? 160f : -1f,
            hard ? 0.18f : 0.08f,
            hard ? 0.35f : 0.15f,
            hard,
            hard,
            false,
            GlitchBiome.Digital,
            hard ? 2.8f : 1.5f
        );

        yield return new WaitForSecondsRealtime(hard ? 3f : 2f);

        if (musicSource.clip != null)
            SafeSeek(musicSource.time + (hard ? Random.Range(-4f, 5f) : Random.Range(-1f, 1.3f)));
    }

    private IEnumerator HardSystemSlipRoutine()
    {
        AddLayer(
            "hard_system_slip",
            14f,
            2.5f,
            7f,
            -14f,
            -6f,
            Random.Range(-0.18f, 0.18f),
            1200f,
            220f,
            0.25f,
            0.35f,
            true,
            true,
            false,
            GlitchBiome.Deep,
            3.1f
        );

        yield return new WaitForSecondsRealtime(4f);

        if (musicSource.clip != null)
            SafeSeek(musicSource.time + Random.Range(-5f, 6f));
    }

    private IEnumerator SituationShiftRoutine()
    {
        AddLayer(
            "situation_shift_main",
            20f,
            3f,
            9f,
            -14f,
            -5.5f,
            Random.Range(-0.1f, 0.1f),
            1800f,
            140f,
            0.16f,
            0.32f,
            true,
            true,
            false,
            GlitchBiome.Possession,
            3.4f
        );

        yield return new WaitForSecondsRealtime(5f);

        AddLayer(
            "situation_shift_afterimage",
            24f,
            5f,
            12f,
            -7f,
            -2f,
            Random.Range(-0.05f, 0.05f),
            5000f,
            -1f,
            0.06f,
            0.16f,
            false,
            true,
            false,
            GlitchBiome.FalseNormal,
            1.8f
        );
    }

    private IEnumerator ScreamerPreShockRoutine()
    {
        AddLayer(
            "screamer_preshock",
            4.5f,
            0.8f,
            2.6f,
            -18f,
            -7f,
            0f,
            900f,
            240f,
            0.25f,
            0.26f,
            true,
            true,
            false,
            GlitchBiome.Collapse,
            3.6f
        );

        yield return new WaitForSecondsRealtime(2f);
    }

    private IEnumerator ScreamerImpactRoutine()
    {
        AddLayer(
            "screamer_impact_cut",
            1.1f,
            0.02f,
            0.8f,
            -34f,
            -11f,
            Random.Range(-0.25f, 0.25f),
            550f,
            650f,
            0.55f,
            0.45f,
            true,
            true,
            false,
            GlitchBiome.Collapse,
            4.2f
        );

        yield return new WaitForSecondsRealtime(0.12f);

        if (musicSource.clip != null)
            SafeSeek(musicSource.time + Random.Range(-1.4f, 1.4f));

        yield return new WaitForSecondsRealtime(0.55f);

        AddLayer(
            "screamer_impact_tail",
            8f,
            0.4f,
            6f,
            -10f,
            -4f,
            Random.Range(-0.12f, 0.12f),
            2500f,
            120f,
            0.20f,
            0.28f,
            true,
            true,
            false,
            GlitchBiome.Collapse,
            2.5f
        );
    }

    private IEnumerator TotalNeutralizationRoutine()
    {
        AddLayer(
            "total_neutralization",
            14f,
            2f,
            8f,
            -24f,
            -2.5f,
            0f,
            4300f,
            320f,
            0.02f,
            0.04f,
            true,
            true,
            false,
            GlitchBiome.Vacuum,
            3.3f
        );

        yield return new WaitForSecondsRealtime(6f);

        AddLayer(
            "wrong_recovery",
            16f,
            4f,
            10f,
            -8f,
            -0.9f,
            Random.Range(-0.04f, 0.04f),
            9000f,
            -1f,
            0f,
            0.08f,
            false,
            true,
            false,
            GlitchBiome.FalseNormal,
            1.8f
        );
    }

    private IEnumerator AudioPossessionRoutine()
    {
        AddLayer(
            "audio_possession",
            26f,
            5f,
            12f,
            -12f,
            -7f,
            Random.Range(-0.2f, 0.2f),
            1100f,
            150f,
            0.28f,
            0.40f,
            true,
            true,
            false,
            GlitchBiome.Possession,
            4.0f
        );

        yield return new WaitForSecondsRealtime(6f);

        if (musicSource.clip != null)
            SafeSeek(musicSource.time + Random.Range(-7f, 9f));

        yield return new WaitForSecondsRealtime(5f);

        AddLayer(
            "possession_memory",
            24f,
            5f,
            12f,
            -6f,
            -1.2f,
            Random.Range(-0.08f, 0.08f),
            7000f,
            -1f,
            0.05f,
            0.14f,
            false,
            true,
            false,
            GlitchBiome.Possession,
            2.0f
        );
    }

    private IEnumerator BrokenLoopRoutine()
    {
        if (musicSource.clip == null)
            yield break;

        float point = musicSource.time;

        AddLayer(
            "broken_loop",
            8f,
            0.6f,
            5f,
            -14f,
            -4.5f,
            0f,
            1600f,
            220f,
            0.22f,
            0.32f,
            true,
            true,
            false,
            GlitchBiome.Digital,
            3.0f
        );

        for (int i = 0; i < 6; i++)
        {
            yield return new WaitForSecondsRealtime(Random.Range(0.22f, 0.72f));
            SafeSeek(point + Random.Range(-0.09f, 0.07f));
        }
    }

    private IEnumerator SystemCollapseRoutine()
    {
        AddLayer(
            "system_collapse_begin",
            3f,
            0.05f,
            2.8f,
            -40f,
            -14f,
            Random.Range(-0.3f, 0.3f),
            350f,
            900f,
            0.75f,
            0.55f,
            true,
            true,
            false,
            GlitchBiome.Collapse,
            5.0f
        );

        yield return new WaitForSecondsRealtime(0.35f);

        if (musicSource.clip != null)
            SafeSeek(musicSource.time + Random.Range(-8f, 10f));

        yield return new WaitForSecondsRealtime(1.4f);

        AddLayer(
            "system_collapse_tail",
            18f,
            1.5f,
            12f,
            -18f,
            -7f,
            Random.Range(-0.18f, 0.18f),
            900f,
            180f,
            0.35f,
            0.42f,
            true,
            true,
            false,
            GlitchBiome.Collapse,
            4.0f
        );
    }

    private IEnumerator FalseRecoveryRoutine()
    {
        AddLayer(
            "false_recovery_silence",
            5f,
            0.8f,
            2.5f,
            -28f,
            -3f,
            0f,
            3000f,
            200f,
            0.04f,
            0.08f,
            true,
            true,
            false,
            GlitchBiome.FalseNormal,
            3.0f
        );

        yield return new WaitForSecondsRealtime(4f);

        AddLayer(
            "false_recovery_wrong_return",
            18f,
            1f,
            10f,
            -4f,
            1.2f,
            Random.Range(-0.06f, 0.06f),
            11000f,
            -1f,
            0.02f,
            0.06f,
            false,
            true,
            false,
            GlitchBiome.FalseNormal,
            2.0f
        );
    }

    private void SafeSeek(float targetTime)
    {
        if (musicSource == null || musicSource.clip == null)
            return;

        float maxTime = Mathf.Max(0f, musicSource.clip.length - clipEndSafetySeconds);
        float clamped = Mathf.Clamp(targetTime, 0f, maxTime);

        try
        {
            musicSource.time = clamped;

            if (debugLogs)
                Debug.Log($"[MusicGlitchDirector] Seek → {clamped:0.00}s");
        }
        catch
        {
            if (debugLogs)
                Debug.LogWarning("[MusicGlitchDirector] Seek impossible sur ce clip.");
        }
    }

    private float DbToLinear(float db)
    {
        return Mathf.Pow(10f, db / 20f);
    }

    private float SemitoneToPitchFactor(float semitones)
    {
        return Mathf.Pow(2f, semitones / 12f);
    }
}