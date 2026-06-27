using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    [Header("Musique")]
    [SerializeField] private AudioClip music;
    [SerializeField] private bool playOnAwakeImmediately = true;
    [SerializeField] private bool loop = true;

    [Header("Fade In Audio")]
    [SerializeField] private float fadeInDuration = 8f;

    [Tooltip("Volume de départ. -35 = déjà légèrement audible. -60 = presque silence.")]
    [SerializeField] private float startVolumeDb = -35f;

    [Tooltip("Courbe du fade. 1 = normal. Plus haut = plus lent au début.")]
    [SerializeField] private float audioCurvePower = 1.15f;

    [Header("Volume final près de l'obélisque")]
    [Range(0f, 1f)]
    [SerializeField] private float targetVolume = 0.75f;

    [Header("Filtres acoustiques")]
    [SerializeField] private bool useAcousticFilters = true;
    [SerializeField] private bool autoAddMissingFilters = true;

    [SerializeField] private float neutralLowPassCutoff = 22000f;
    [SerializeField] private float neutralHighPassCutoff = 10f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private AudioSource audioSource;
    private AudioLowPassFilter lowPassFilter;
    private AudioHighPassFilter highPassFilter;
    private AudioEchoFilter echoFilter;

    private Coroutine fadeCoroutine;
    private Coroutine spatialCoroutine;

    private bool hasStartedMusic = false;
    private float fadeProgress = 0f;

    private float spatialVolumeMultiplier = 1f;
    private float spatialPan = 0f;
    private float spatialLowPassCutoff = 22000f;
    private float spatialHighPassCutoff = 10f;
    private float spatialEchoWet = 0f;

    public AudioSource AudioSource => audioSource;
    public float SpatialVolumeMultiplier => spatialVolumeMultiplier;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = loop;
        audioSource.spatialBlend = 0f;
        audioSource.mute = false;
        audioSource.pitch = 1f;
        audioSource.panStereo = 0f;

        if (music != null)
            audioSource.clip = music;

        SetupFilters();

        spatialLowPassCutoff = neutralLowPassCutoff;
        spatialHighPassCutoff = neutralHighPassCutoff;
        spatialEchoWet = 0f;

        ApplyComputedAudioState();

        if (audioSource.clip != null)
            audioSource.clip.LoadAudioData();

        if (debugLogs)
            Debug.Log($"[MusicManager] Awake OK sur '{gameObject.name}'");

        if (playOnAwakeImmediately)
            PlayWithFade();
    }

    private void SetupFilters()
    {
        if (!useAcousticFilters)
            return;

        lowPassFilter = GetComponent<AudioLowPassFilter>();

        if (lowPassFilter == null && autoAddMissingFilters)
            lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();

        if (lowPassFilter != null)
        {
            lowPassFilter.enabled = true;
            lowPassFilter.cutoffFrequency = neutralLowPassCutoff;
            lowPassFilter.lowpassResonanceQ = 1.05f;
        }

        highPassFilter = GetComponent<AudioHighPassFilter>();

        if (highPassFilter == null && autoAddMissingFilters)
            highPassFilter = gameObject.AddComponent<AudioHighPassFilter>();

        if (highPassFilter != null)
        {
            highPassFilter.enabled = true;
            highPassFilter.cutoffFrequency = neutralHighPassCutoff;
            highPassFilter.highpassResonanceQ = 1.05f;
        }

        echoFilter = GetComponent<AudioEchoFilter>();

        if (echoFilter == null && autoAddMissingFilters)
            echoFilter = gameObject.AddComponent<AudioEchoFilter>();

        if (echoFilter != null)
        {
            echoFilter.enabled = true;
            echoFilter.delay = 220f;
            echoFilter.decayRatio = 0.22f;
            echoFilter.dryMix = 1f;
            echoFilter.wetMix = 0f;
        }
    }

    public void PlayWithFade()
    {
        if (hasStartedMusic)
            return;

        if (audioSource.clip == null)
        {
            Debug.LogWarning("[MusicManager] Impossible de lancer : aucun clip audio.");
            return;
        }

        hasStartedMusic = true;

        audioSource.Stop();
        audioSource.loop = loop;
        fadeProgress = 0f;

        ApplyComputedAudioState();
        audioSource.Play();

        if (debugLogs)
            Debug.Log($"[MusicManager] Musique lancée dans Awake : {audioSource.clip.name}");

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeInAudioRoutine());
    }

    private IEnumerator FadeInAudioRoutine()
    {
        if (fadeInDuration <= 0f)
        {
            fadeProgress = 1f;
            ApplyComputedAudioState();
            fadeCoroutine = null;
            yield break;
        }

        float timer = 0f;

        while (timer < fadeInDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / fadeInDuration);
            float smoothT = SmootherStep(t);
            smoothT = Mathf.Pow(smoothT, audioCurvePower);

            fadeProgress = smoothT;
            ApplyComputedAudioState();

            yield return null;
        }

        fadeProgress = 1f;
        ApplyComputedAudioState();

        fadeCoroutine = null;

        if (debugLogs)
            Debug.Log("[MusicManager] Fade-in audio terminé.");
    }

    public void SetObeliskSpatialState(
        float volumeMultiplier,
        float pan,
        float lowPassCutoff,
        float highPassCutoff,
        float echoWet,
        float smoothDuration
    )
    {
        volumeMultiplier = Mathf.Clamp(volumeMultiplier, 0f, 1.5f);
        pan = Mathf.Clamp(pan, -1f, 1f);
        lowPassCutoff = Mathf.Clamp(lowPassCutoff, 180f, neutralLowPassCutoff);
        highPassCutoff = Mathf.Clamp(highPassCutoff, neutralHighPassCutoff, 9000f);
        echoWet = Mathf.Clamp01(echoWet);

        if (spatialCoroutine != null)
            StopCoroutine(spatialCoroutine);

        spatialCoroutine = StartCoroutine(SpatialAudioRoutine(
            volumeMultiplier,
            pan,
            lowPassCutoff,
            highPassCutoff,
            echoWet,
            smoothDuration
        ));
    }

    public void SetDistanceVolumeMultiplier(float multiplier, float smoothDuration)
    {
        SetObeliskSpatialState(
            multiplier,
            spatialPan,
            spatialLowPassCutoff,
            spatialHighPassCutoff,
            spatialEchoWet,
            smoothDuration
        );
    }

    private IEnumerator SpatialAudioRoutine(
        float targetVolumeMultiplier,
        float targetPan,
        float targetLowPass,
        float targetHighPass,
        float targetEchoWet,
        float duration
    )
    {
        float startVolumeMultiplier = spatialVolumeMultiplier;
        float startPan = spatialPan;
        float startLowPass = spatialLowPassCutoff;
        float startHighPass = spatialHighPassCutoff;
        float startEchoWet = spatialEchoWet;

        if (duration <= 0f)
        {
            spatialVolumeMultiplier = targetVolumeMultiplier;
            spatialPan = targetPan;
            spatialLowPassCutoff = targetLowPass;
            spatialHighPassCutoff = targetHighPass;
            spatialEchoWet = targetEchoWet;

            ApplyComputedAudioState();
            spatialCoroutine = null;
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / duration);
            float smoothT = SmootherStep(t);

            spatialVolumeMultiplier = Mathf.Lerp(startVolumeMultiplier, targetVolumeMultiplier, smoothT);
            spatialPan = Mathf.Lerp(startPan, targetPan, smoothT);
            spatialLowPassCutoff = Mathf.Lerp(startLowPass, targetLowPass, smoothT);
            spatialHighPassCutoff = Mathf.Lerp(startHighPass, targetHighPass, smoothT);
            spatialEchoWet = Mathf.Lerp(startEchoWet, targetEchoWet, smoothT);

            ApplyComputedAudioState();

            yield return null;
        }

        spatialVolumeMultiplier = targetVolumeMultiplier;
        spatialPan = targetPan;
        spatialLowPassCutoff = targetLowPass;
        spatialHighPassCutoff = targetHighPass;
        spatialEchoWet = targetEchoWet;

        ApplyComputedAudioState();

        spatialCoroutine = null;
    }

    private void ApplyComputedAudioState()
    {
        if (audioSource == null)
            return;

        float startLinear = DbToLinear(startVolumeDb);
        float finalTargetVolume = Mathf.Clamp01(targetVolume * spatialVolumeMultiplier);

        float volume = Mathf.Lerp(startLinear, finalTargetVolume, fadeProgress);

        audioSource.volume = Mathf.Clamp01(volume);
        audioSource.panStereo = spatialPan;

        if (lowPassFilter != null)
            lowPassFilter.cutoffFrequency = spatialLowPassCutoff;

        if (highPassFilter != null)
            highPassFilter.cutoffFrequency = spatialHighPassCutoff;

        if (echoFilter != null)
        {
            echoFilter.dryMix = 1f;
            echoFilter.wetMix = spatialEchoWet;
        }
    }

    public void StopMusic()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        if (spatialCoroutine != null)
            StopCoroutine(spatialCoroutine);

        audioSource.Stop();

        fadeProgress = 0f;
        ApplyComputedAudioState();

        hasStartedMusic = false;
        fadeCoroutine = null;
        spatialCoroutine = null;

        if (debugLogs)
            Debug.Log("[MusicManager] Musique stoppée.");
    }

    private float SmootherStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private float DbToLinear(float db)
    {
        return Mathf.Pow(10f, db / 20f);
    }
}