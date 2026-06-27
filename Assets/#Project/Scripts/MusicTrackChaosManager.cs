using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicTrackChaosManager : MonoBehaviour
{
    public enum TrackEventMode
    {
        Random,

        CinematicStopThenNewTrack,
        SuddenIntrusion,
        FalseStopThenReturn,
        NoiseBurstThenTrack,
        CorruptedCrossfade,
        DeadAirThenStinger,
        WrongMusicAfterSilence,
        LayeredBleed,
        HardCutToNoiseThenBack,
        SlowTakeover
    }

    [Header("Sources")]
    [Tooltip("Source principale. Souvent le même AudioSource que MusicManager.")]
    [SerializeField] private AudioSource mainSource;

    [Tooltip("Source secondaire créée automatiquement si vide.")]
    [SerializeField] private AudioSource secondarySource;

    [Tooltip("Source pour bruits ponctuels créée automatiquement si vide.")]
    [SerializeField] private AudioSource oneShotSource;

    [Header("Tracks alternatives")]
    [SerializeField] private AudioClip[] alternateTracks;

    [Header("Bruits / stingers / matières")]
    [SerializeField] private AudioClip[] noiseBursts;
    [SerializeField] private AudioClip[] stingers;
    [SerializeField] private AudioClip[] deadAirNoises;

    [Header("Random")]
    [SerializeField] private bool enableRandomTrackEvents = false;
    [SerializeField] private float randomStartGrace = 35f;
    [SerializeField] private float randomMinInterval = 45f;
    [SerializeField] private float randomMaxInterval = 140f;

    [Tooltip("Peut dépasser 1. 1 = toujours quand le timer arrive.")]
    [Min(0f)]
    [SerializeField] private float randomEventChance = 0.18f;

    [Header("Volumes")]
    [Range(0f, 1f)]
    [SerializeField] private float mainVolume = 0.75f;

    [Range(0f, 1f)]
    [SerializeField] private float alternateVolume = 0.75f;

    [Range(0f, 1f)]
    [SerializeField] private float noiseVolume = 0.85f;

    [Header("Timing")]
    [SerializeField] private float defaultFadeOut = 2.4f;
    [SerializeField] private float defaultFadeIn = 3.5f;
    [SerializeField] private float deadAirDuration = 1.2f;

    [Header("Glitch integration")]
    [SerializeField] private MusicGlitchDirector glitchDirector;
    [SerializeField] private bool triggerGlitchBeforeTrackEvent = true;
    [SerializeField] private bool clearGlitchesAfterTrackEvent = false;

    [Header("Sécurité")]
    [SerializeField] private bool blockOverlappingTrackEvents = true;
    [SerializeField] private bool debugLogs = true;

    private Coroutine currentRoutine;
    private Coroutine randomRoutine;

    private AudioClip rememberedMainClip;
    private float rememberedMainTime;
    private bool rememberedMainLoop;

    private bool isRunningEvent = false;

    public bool IsRunningEvent => isRunningEvent;

    private void Awake()
    {
        if (mainSource == null)
            mainSource = GetComponent<AudioSource>();

        if (secondarySource == null)
            secondarySource = CreateChildAudioSource("SecondaryMusicSource");

        if (oneShotSource == null)
            oneShotSource = CreateChildAudioSource("OneShotMusicSource");

        mainSource.spatialBlend = 0f;
        secondarySource.spatialBlend = 0f;
        oneShotSource.spatialBlend = 0f;

        secondarySource.playOnAwake = false;
        oneShotSource.playOnAwake = false;

        secondarySource.volume = 0f;
        oneShotSource.volume = noiseVolume;

        if (glitchDirector == null)
            glitchDirector = GetComponent<MusicGlitchDirector>();

        if (debugLogs)
            Debug.Log($"[MusicTrackChaosManager] Awake OK sur '{gameObject.name}'");
    }

    private void OnEnable()
    {
        if (enableRandomTrackEvents)
            randomRoutine = StartCoroutine(RandomEventLoop());
    }

    private void OnDisable()
    {
        if (randomRoutine != null)
            StopCoroutine(randomRoutine);

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        isRunningEvent = false;
    }

    private AudioSource CreateChildAudioSource(string objectName)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform);

        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 0f;

        return source;
    }

    private IEnumerator RandomEventLoop()
    {
        if (randomStartGrace > 0f)
            yield return new WaitForSecondsRealtime(randomStartGrace);

        while (true)
        {
            float wait = Random.Range(randomMinInterval, randomMaxInterval);
            yield return new WaitForSecondsRealtime(wait);

            if (!enableRandomTrackEvents)
                continue;

            if (blockOverlappingTrackEvents && isRunningEvent)
                continue;

            if (Random.value > Mathf.Clamp01(randomEventChance))
                continue;

            TriggerTrackEvent(TrackEventMode.Random);
        }
    }

    public void TriggerRandomTrackEvent()
    {
        TriggerTrackEvent(TrackEventMode.Random);
    }

    public void TriggerCinematicStopThenNewTrack()
    {
        TriggerTrackEvent(TrackEventMode.CinematicStopThenNewTrack);
    }

    public void TriggerSuddenIntrusion()
    {
        TriggerTrackEvent(TrackEventMode.SuddenIntrusion);
    }

    public void TriggerFalseStopThenReturn()
    {
        TriggerTrackEvent(TrackEventMode.FalseStopThenReturn);
    }

    public void TriggerNoiseBurstThenTrack()
    {
        TriggerTrackEvent(TrackEventMode.NoiseBurstThenTrack);
    }

    public void TriggerCorruptedCrossfade()
    {
        TriggerTrackEvent(TrackEventMode.CorruptedCrossfade);
    }

    public void TriggerDeadAirThenStinger()
    {
        TriggerTrackEvent(TrackEventMode.DeadAirThenStinger);
    }

    public void TriggerWrongMusicAfterSilence()
    {
        TriggerTrackEvent(TrackEventMode.WrongMusicAfterSilence);
    }

    public void TriggerLayeredBleed()
    {
        TriggerTrackEvent(TrackEventMode.LayeredBleed);
    }

    public void TriggerHardCutToNoiseThenBack()
    {
        TriggerTrackEvent(TrackEventMode.HardCutToNoiseThenBack);
    }

    public void TriggerSlowTakeover()
    {
        TriggerTrackEvent(TrackEventMode.SlowTakeover);
    }

    public void TriggerTrackEvent(TrackEventMode mode)
    {
        if (blockOverlappingTrackEvents && isRunningEvent)
        {
            if (debugLogs)
                Debug.Log("[MusicTrackChaosManager] Event ignoré : un event track est déjà actif.");

            return;
        }

        if (mode == TrackEventMode.Random)
            mode = PickRandomMode();

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(TrackEventRoutine(mode));
    }

    private TrackEventMode PickRandomMode()
    {
        float r = Random.value;

        if (r < 0.16f) return TrackEventMode.CinematicStopThenNewTrack;
        if (r < 0.30f) return TrackEventMode.FalseStopThenReturn;
        if (r < 0.43f) return TrackEventMode.NoiseBurstThenTrack;
        if (r < 0.56f) return TrackEventMode.CorruptedCrossfade;
        if (r < 0.68f) return TrackEventMode.DeadAirThenStinger;
        if (r < 0.80f) return TrackEventMode.WrongMusicAfterSilence;
        if (r < 0.90f) return TrackEventMode.LayeredBleed;

        return TrackEventMode.SlowTakeover;
    }

    private IEnumerator TrackEventRoutine(TrackEventMode mode)
    {
        isRunningEvent = true;

        RememberMainState();

        if (debugLogs)
            Debug.Log($"[MusicTrackChaosManager] Event lancé : {mode}");

        if (triggerGlitchBeforeTrackEvent && glitchDirector != null)
            glitchDirector.TriggerSituationShift();

        switch (mode)
        {
            case TrackEventMode.CinematicStopThenNewTrack:
                yield return CinematicStopThenNewTrackRoutine();
                break;

            case TrackEventMode.SuddenIntrusion:
                yield return SuddenIntrusionRoutine();
                break;

            case TrackEventMode.FalseStopThenReturn:
                yield return FalseStopThenReturnRoutine();
                break;

            case TrackEventMode.NoiseBurstThenTrack:
                yield return NoiseBurstThenTrackRoutine();
                break;

            case TrackEventMode.CorruptedCrossfade:
                yield return CorruptedCrossfadeRoutine();
                break;

            case TrackEventMode.DeadAirThenStinger:
                yield return DeadAirThenStingerRoutine();
                break;

            case TrackEventMode.WrongMusicAfterSilence:
                yield return WrongMusicAfterSilenceRoutine();
                break;

            case TrackEventMode.LayeredBleed:
                yield return LayeredBleedRoutine();
                break;

            case TrackEventMode.HardCutToNoiseThenBack:
                yield return HardCutToNoiseThenBackRoutine();
                break;

            case TrackEventMode.SlowTakeover:
                yield return SlowTakeoverRoutine();
                break;
        }

        if (clearGlitchesAfterTrackEvent && glitchDirector != null)
            glitchDirector.ClearAllGlitches();

        isRunningEvent = false;
        currentRoutine = null;

        if (debugLogs)
            Debug.Log("[MusicTrackChaosManager] Event terminé.");
    }

    private void RememberMainState()
    {
        rememberedMainClip = mainSource.clip;
        rememberedMainTime = mainSource.time;
        rememberedMainLoop = mainSource.loop;
    }

    private IEnumerator CinematicStopThenNewTrackRoutine()
    {
        AudioClip next = PickAlternateTrack();

        if (next == null)
            yield break;

        yield return FadeSource(mainSource, mainSource.volume, 0f, defaultFadeOut);

        mainSource.Stop();

        yield return new WaitForSecondsRealtime(deadAirDuration);

        PlayOneShot(PickClip(stingers), noiseVolume * 0.55f);

        yield return new WaitForSecondsRealtime(0.4f);

        mainSource.clip = next;
        mainSource.loop = true;
        mainSource.pitch = 1f;
        mainSource.volume = 0f;
        mainSource.Play();

        yield return FadeSource(mainSource, 0f, alternateVolume, defaultFadeIn);
    }

    private IEnumerator SuddenIntrusionRoutine()
    {
        AudioClip noise = PickClip(noiseBursts);
        AudioClip next = PickAlternateTrack();

        if (noise != null)
            PlayOneShot(noise, noiseVolume);

        if (next != null)
        {
            secondarySource.clip = next;
            secondarySource.loop = true;
            secondarySource.pitch = 1f;
            secondarySource.volume = 0f;
            secondarySource.Play();

            yield return FadeSource(secondarySource, 0f, alternateVolume, 0.25f);
            yield return FadeSource(mainSource, mainSource.volume, 0f, 0.35f);

            mainSource.Stop();
            SwapSecondaryToMain();
        }
    }

    private IEnumerator FalseStopThenReturnRoutine()
    {
        float originalVolume = mainSource.volume;

        yield return FadeSource(mainSource, originalVolume, 0f, 1.8f);

        mainSource.Pause();

        yield return new WaitForSecondsRealtime(Random.Range(1.2f, 3.5f));

        PlayOneShot(PickClip(deadAirNoises), noiseVolume * 0.35f);

        yield return new WaitForSecondsRealtime(Random.Range(0.6f, 1.4f));

        mainSource.UnPause();

        if (glitchDirector != null)
            glitchDirector.TriggerFalseRecovery();

        yield return FadeSource(mainSource, 0f, originalVolume, 4.5f);
    }

    private IEnumerator NoiseBurstThenTrackRoutine()
    {
        AudioClip next = PickAlternateTrack();

        yield return FadeSource(mainSource, mainSource.volume, 0f, 0.7f);

        mainSource.Stop();

        PlayOneShot(PickClip(noiseBursts), noiseVolume);

        yield return new WaitForSecondsRealtime(Random.Range(0.35f, 1.1f));

        if (next != null)
        {
            mainSource.clip = next;
            mainSource.loop = true;
            mainSource.pitch = 1f;
            mainSource.volume = 0f;
            mainSource.Play();

            yield return FadeSource(mainSource, 0f, alternateVolume, 3.5f);
        }
    }

    private IEnumerator CorruptedCrossfadeRoutine()
    {
        AudioClip next = PickAlternateTrack();

        if (next == null)
            yield break;

        secondarySource.clip = next;
        secondarySource.loop = true;
        secondarySource.volume = 0f;
        secondarySource.pitch = Random.Range(0.92f, 1.04f);
        secondarySource.Play();

        float duration = Random.Range(5f, 9f);
        float timer = 0f;
        float mainStart = mainSource.volume;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / duration);
            float smooth = SmootherStep(t);

            mainSource.volume = Mathf.Lerp(mainStart, 0f, smooth);
            secondarySource.volume = Mathf.Lerp(0f, alternateVolume, smooth);

            if (Random.value < 0.015f)
                PlayOneShot(PickClip(deadAirNoises), noiseVolume * 0.25f);

            yield return null;
        }

        mainSource.Stop();
        SwapSecondaryToMain();
    }

    private IEnumerator DeadAirThenStingerRoutine()
    {
        yield return FadeSource(mainSource, mainSource.volume, 0f, Random.Range(0.8f, 2f));

        mainSource.Pause();

        yield return new WaitForSecondsRealtime(Random.Range(2f, 5f));

        PlayOneShot(PickClip(stingers), noiseVolume);

        yield return new WaitForSecondsRealtime(Random.Range(0.3f, 1f));

        mainSource.UnPause();

        yield return FadeSource(mainSource, 0f, mainVolume, Random.Range(3f, 6f));
    }

    private IEnumerator WrongMusicAfterSilenceRoutine()
    {
        AudioClip next = PickAlternateTrack();

        yield return FadeSource(mainSource, mainSource.volume, 0f, 2.5f);

        mainSource.Stop();

        yield return new WaitForSecondsRealtime(Random.Range(1.5f, 4f));

        if (next != null)
        {
            mainSource.clip = next;
            mainSource.loop = true;
            mainSource.pitch = Random.Range(0.94f, 1.02f);
            mainSource.volume = 0f;
            mainSource.Play();

            float target = alternateVolume * Random.Range(0.35f, 0.75f);
            yield return FadeSource(mainSource, 0f, target, Random.Range(4f, 9f));
        }
    }

    private IEnumerator LayeredBleedRoutine()
    {
        AudioClip bleed = PickAlternateTrack();

        if (bleed == null)
            yield break;

        secondarySource.clip = bleed;
        secondarySource.loop = true;
        secondarySource.pitch = Random.Range(0.85f, 1.12f);
        secondarySource.volume = 0f;
        secondarySource.Play();

        float target = alternateVolume * Random.Range(0.12f, 0.35f);

        yield return FadeSource(secondarySource, 0f, target, Random.Range(4f, 8f));

        yield return new WaitForSecondsRealtime(Random.Range(8f, 20f));

        yield return FadeSource(secondarySource, secondarySource.volume, 0f, Random.Range(5f, 10f));

        secondarySource.Stop();
        secondarySource.pitch = 1f;
    }

    private IEnumerator HardCutToNoiseThenBackRoutine()
    {
        mainSource.Pause();
        mainSource.volume = 0f;

        PlayOneShot(PickClip(noiseBursts), noiseVolume);

        yield return new WaitForSecondsRealtime(Random.Range(0.25f, 0.9f));

        PlayOneShot(PickClip(stingers), noiseVolume * 0.65f);

        yield return new WaitForSecondsRealtime(Random.Range(0.6f, 1.6f));

        mainSource.UnPause();

        yield return FadeSource(mainSource, 0f, mainVolume, Random.Range(2f, 5f));
    }

    private IEnumerator SlowTakeoverRoutine()
    {
        AudioClip next = PickAlternateTrack();

        if (next == null)
            yield break;

        secondarySource.clip = next;
        secondarySource.loop = true;
        secondarySource.volume = 0f;
        secondarySource.pitch = Random.Range(0.96f, 1.03f);
        secondarySource.Play();

        yield return FadeSource(secondarySource, 0f, alternateVolume * 0.35f, 10f);

        yield return new WaitForSecondsRealtime(Random.Range(5f, 12f));

        yield return CrossfadeSources(mainSource, secondarySource, 8f);

        SwapSecondaryToMain();
    }

    private IEnumerator CrossfadeSources(AudioSource from, AudioSource to, float duration)
    {
        float fromStart = from.volume;
        float toStart = to.volume;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / duration);
            float smooth = SmootherStep(t);

            from.volume = Mathf.Lerp(fromStart, 0f, smooth);
            to.volume = Mathf.Lerp(toStart, alternateVolume, smooth);

            yield return null;
        }

        from.volume = 0f;
        to.volume = alternateVolume;
    }

    private IEnumerator FadeSource(AudioSource source, float from, float to, float duration)
    {
        if (source == null)
            yield break;

        if (duration <= 0f)
        {
            source.volume = to;
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / duration);
            float smooth = SmootherStep(t);

            source.volume = Mathf.Lerp(from, to, smooth);

            yield return null;
        }

        source.volume = to;
    }

    private void SwapSecondaryToMain()
    {
        AudioClip clip = secondarySource.clip;
        float time = secondarySource.time;
        bool loop = secondarySource.loop;
        float pitch = secondarySource.pitch;
        float volume = secondarySource.volume;

        secondarySource.Stop();
        secondarySource.clip = null;
        secondarySource.volume = 0f;
        secondarySource.pitch = 1f;

        mainSource.clip = clip;
        mainSource.loop = loop;
        mainSource.pitch = pitch;
        mainSource.volume = volume;

        if (mainSource.clip != null)
        {
            mainSource.time = Mathf.Clamp(time, 0f, mainSource.clip.length - 0.1f);
            mainSource.Play();
        }
    }

    private AudioClip PickAlternateTrack()
    {
        return PickClip(alternateTracks);
    }

    private AudioClip PickClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        List<AudioClip> valid = new List<AudioClip>();

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                valid.Add(clips[i]);
        }

        if (valid.Count == 0)
            return null;

        return valid[Random.Range(0, valid.Count)];
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null || oneShotSource == null)
            return;

        oneShotSource.pitch = Random.Range(0.92f, 1.08f);
        oneShotSource.volume = noiseVolume;
        oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private float SmootherStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}