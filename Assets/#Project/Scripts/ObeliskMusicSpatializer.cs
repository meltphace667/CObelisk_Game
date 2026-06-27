using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(20000)]
public class ObeliskMusicSpatializer : MonoBehaviour
{
    [System.Serializable]
    public class RoomDistance
    {
        public string roomId;
        public int distance;
        public bool forceTrueSilence;

        public RoomDistance(string roomId, int distance, bool forceTrueSilence = false)
        {
            this.roomId = roomId;
            this.distance = distance;
            this.forceTrueSilence = forceTrueSilence;
        }
    }

    [Header("Références")]
    [SerializeField] private BackgroundManager backgroundManager;
    [SerializeField] private AudioSource musicSource;

    [Header("Source de l'obélisque")]
    [Tooltip("Ob_02 = image pile devant l'obélisque, donc source sonore.")]
    [SerializeField] private string obeliskRoomId = "Ob_02";

    [Header("Volume / distance")]
    [Range(0f, 1f)]
    [SerializeField] private float nearVolume = 0.80f;

    [Range(0f, 1f)]
    [SerializeField] private float farVolume = 0f;

    [Tooltip("Distance où le son tombe au silence.")]
    [SerializeField] private int silenceDistance = 8;

    [Tooltip("Plus c'est haut, plus le son chute vite. 1.8 est assez lisible.")]
    [SerializeField] private float falloffPower = 1.8f;

    [Tooltip("Vitesse de transition du volume.")]
    [SerializeField] private float volumeMoveSpeed = 3.5f;

    [Header("Pan")]
    [Tooltip("On laisse centré. Le pan directionnel était confus pour cette map.")]
    [SerializeField] private bool forceCenteredPan = true;

    [Header("Filtres")]
    [SerializeField] private bool useFilters = true;

    [SerializeField] private float nearLowPass = 22000f;
    [SerializeField] private float farLowPass = 600f;

    [SerializeField] private float nearHighPass = 10f;
    [SerializeField] private float farHighPass = 1700f;

    [Header("Echo très léger")]
    [SerializeField] private bool useEcho = true;

    [Range(0f, 1f)]
    [SerializeField] private float maxEchoWetMix = 0.08f;

    [SerializeField] private float echoDelay = 430f;
    [SerializeField] private float echoDecayRatio = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private bool debugEveryFrame = false;

    [Header("Distances Map V1")]
    [SerializeField]
    private List<RoomDistance> roomDistances = new List<RoomDistance>
    {
        // Ob_02 = plus proche de l'obélisque / source sonore.
        new RoomDistance("Ob_02", 0),
        new RoomDistance("Ob_01", 1),
        new RoomDistance("PRA_01", 2),

        // Chemin lac principal
        new RoomDistance("LAC_A1", 3),
        new RoomDistance("LAC_A2", 4),
        new RoomDistance("LAC_01", 5),

        // Chemin secondaire
        new RoomDistance("FOR_L1", 3),
        new RoomDistance("FOR_L2", 4),

        // Silence / réceptacle
        new RoomDistance("SIL_01", 6),
        new RoomDistance("SIL_02", 7),
        new RoomDistance("SIL_03", 8, true),

        // Château
        new RoomDistance("FOR_01", 3),
        new RoomDistance("FOR_02", 4),
        new RoomDistance("CHA_FAR", 5),
        new RoomDistance("CHA_NEAR", 6),
        new RoomDistance("CHA_INT_01", 7),
        new RoomDistance("CHA_INT_02", 8, true),
    };

    private readonly Dictionary<string, RoomDistance> distanceByRoom = new Dictionary<string, RoomDistance>();

    private AudioLowPassFilter lowPassFilter;
    private AudioHighPassFilter highPassFilter;
    private AudioEchoFilter echoFilter;

    private string lastRoomId = "";
    private float targetVolume;
    private float targetPan;

    private void Awake()
    {
        if (musicSource == null)
            musicSource = GetComponent<AudioSource>();

        if (musicSource == null)
            musicSource = FindAnyObjectByType<AudioSource>();

        if (backgroundManager == null)
            backgroundManager = BackgroundManager.Instance;

        if (backgroundManager == null)
            backgroundManager = FindAnyObjectByType<BackgroundManager>();

        if (musicSource == null)
        {
            Debug.LogError("[ObeliskMusicSpatializer] Aucun AudioSource trouvé.");
            enabled = false;
            return;
        }

        if (backgroundManager == null)
        {
            Debug.LogError("[ObeliskMusicSpatializer] Aucun BackgroundManager trouvé.");
            enabled = false;
            return;
        }

        // Sécurité : ton jeu a défini Ob_02 comme la vraie source.
        if (obeliskRoomId != "Ob_02")
        {
            Debug.LogWarning(
                "[ObeliskMusicSpatializer] Obelisk Room Id était '" +
                obeliskRoomId +
                "'. Correction automatique vers 'Ob_02'."
            );

            obeliskRoomId = "Ob_02";
        }

        musicSource.spatialBlend = 0f;

        if (forceCenteredPan)
            musicSource.panStereo = 0f;

        BuildDistanceDictionary();
        SetupAudioEffects();

        targetVolume = nearVolume;
        targetPan = 0f;

        if (debugLogs)
        {
            Debug.Log("[ObeliskMusicSpatializer] Awake OK - VERSION DISTANCE ONLY STABLE");
            Debug.Log("[ObeliskMusicSpatializer] Source obélisque = " + obeliskRoomId);
            Debug.Log("[ObeliskMusicSpatializer] Pan forcé au centre = " + forceCenteredPan);
            Debug.Log("[ObeliskMusicSpatializer] Silence distance = " + silenceDistance);
            PrintDistanceMap();
        }
    }

    private void Start()
    {
        ApplyForCurrentRoom(true);
    }

    private void LateUpdate()
    {
        ApplyForCurrentRoom(false);

        if (musicSource == null)
            return;

        musicSource.volume = Mathf.MoveTowards(
            musicSource.volume,
            targetVolume,
            volumeMoveSpeed * Time.deltaTime
        );

        if (forceCenteredPan)
        {
            musicSource.panStereo = Mathf.MoveTowards(
                musicSource.panStereo,
                0f,
                volumeMoveSpeed * Time.deltaTime
            );
        }
        else
        {
            musicSource.panStereo = Mathf.MoveTowards(
                musicSource.panStereo,
                targetPan,
                volumeMoveSpeed * Time.deltaTime
            );
        }

        if (debugEveryFrame)
        {
            Debug.Log(
                "[ObeliskMusicSpatializer] frame actualVol=" +
                musicSource.volume.ToString("0.000") +
                " targetVol=" +
                targetVolume.ToString("0.000") +
                " pan=" +
                musicSource.panStereo.ToString("0.000")
            );
        }
    }

    private void ApplyForCurrentRoom(bool instant)
    {
        if (backgroundManager == null)
            return;

        string currentRoomId = backgroundManager.GetCurrentRoomId();

        if (string.IsNullOrEmpty(currentRoomId))
            return;

        ApplyForRoom(currentRoomId, instant);
    }

    private void ApplyForRoom(string roomId, bool instant)
    {
        if (musicSource == null)
            return;

        if (!distanceByRoom.TryGetValue(roomId, out RoomDistance roomDistance))
        {
            if (roomId != lastRoomId)
            {
                Debug.LogWarning(
                    "[ObeliskMusicSpatializer] Room sans distance : '" +
                    roomId +
                    "'. Ajoute-la dans Room Distances."
                );
            }

            lastRoomId = roomId;
            return;
        }

        int distance = roomDistance.distance;
        float distance01 = Mathf.Clamp01((float)distance / Mathf.Max(1, silenceDistance));
        float remainingSignal = 1f - distance01;
        float signal = Mathf.Pow(remainingSignal, falloffPower);

        targetVolume = Mathf.Lerp(farVolume, nearVolume, signal);

        if (roomDistance.forceTrueSilence || distance >= silenceDistance)
            targetVolume = 0f;

        targetPan = 0f;

        ApplyFilters(signal, distance01, roomDistance.forceTrueSilence);
        ApplyEcho(distance01, roomDistance.forceTrueSilence);

        if (instant)
        {
            musicSource.volume = targetVolume;

            if (forceCenteredPan)
                musicSource.panStereo = 0f;
        }

        if (roomId != lastRoomId)
        {
            lastRoomId = roomId;

            if (debugLogs)
            {
                Debug.Log(
                    "[ObeliskMusicSpatializer] Room '" + roomId +
                    "' | source=" + obeliskRoomId +
                    " | distance=" + distance +
                    " | signal=" + signal.ToString("0.000") +
                    " | targetVol=" + targetVolume.ToString("0.000") +
                    " | actualVol=" + musicSource.volume.ToString("0.000") +
                    " | pan=" + musicSource.panStereo.ToString("0.000") +
                    " | trueSilence=" + roomDistance.forceTrueSilence
                );
            }
        }
    }

    private void ApplyFilters(float signal, float distance01, bool trueSilence)
    {
        if (!useFilters)
            return;

        if (lowPassFilter != null)
        {
            lowPassFilter.enabled = true;
            lowPassFilter.cutoffFrequency = Mathf.Lerp(farLowPass, nearLowPass, signal);
        }

        if (highPassFilter != null)
        {
            highPassFilter.enabled = true;
            highPassFilter.cutoffFrequency = Mathf.Lerp(farHighPass, nearHighPass, signal);
        }

        if (trueSilence)
        {
            if (lowPassFilter != null)
                lowPassFilter.cutoffFrequency = farLowPass;

            if (highPassFilter != null)
                highPassFilter.cutoffFrequency = farHighPass;
        }
    }

    private void ApplyEcho(float distance01, bool trueSilence)
    {
        if (!useEcho || echoFilter == null)
            return;

        echoFilter.enabled = true;
        echoFilter.delay = echoDelay;
        echoFilter.decayRatio = echoDecayRatio;

        float wet = Mathf.Lerp(0f, maxEchoWetMix, distance01);

        if (trueSilence)
            wet = 0f;

        echoFilter.wetMix = wet;
        echoFilter.dryMix = 1f;
    }

    private void SetupAudioEffects()
    {
        if (useFilters)
        {
            lowPassFilter = GetComponent<AudioLowPassFilter>();

            if (lowPassFilter == null)
                lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();

            highPassFilter = GetComponent<AudioHighPassFilter>();

            if (highPassFilter == null)
                highPassFilter = gameObject.AddComponent<AudioHighPassFilter>();

            lowPassFilter.enabled = true;
            highPassFilter.enabled = true;
        }

        if (useEcho)
        {
            echoFilter = GetComponent<AudioEchoFilter>();

            if (echoFilter == null)
                echoFilter = gameObject.AddComponent<AudioEchoFilter>();

            echoFilter.enabled = true;
        }
    }

    private void BuildDistanceDictionary()
    {
        distanceByRoom.Clear();

        foreach (RoomDistance roomDistance in roomDistances)
        {
            if (roomDistance == null)
                continue;

            if (string.IsNullOrWhiteSpace(roomDistance.roomId))
                continue;

            if (!distanceByRoom.ContainsKey(roomDistance.roomId))
                distanceByRoom.Add(roomDistance.roomId, roomDistance);
        }
    }

    private void PrintDistanceMap()
    {
        Debug.Log("[ObeliskMusicSpatializer] Distances V1 :");

        foreach (RoomDistance roomDistance in roomDistances)
        {
            if (roomDistance == null)
                continue;

            Debug.Log(
                "[ObeliskMusicSpatializer] " +
                roomDistance.roomId +
                " = " +
                roomDistance.distance +
                " silence=" +
                roomDistance.forceTrueSilence
            );
        }
    }

    [ContextMenu("Reset Distance Map V1")]
    private void ResetDistanceMapV1()
    {
        roomDistances = new List<RoomDistance>
        {
            new RoomDistance("Ob_02", 0),
            new RoomDistance("Ob_01", 1),
            new RoomDistance("PRA_01", 2),

            new RoomDistance("LAC_A1", 3),
            new RoomDistance("LAC_A2", 4),
            new RoomDistance("LAC_01", 5),

            new RoomDistance("FOR_L1", 3),
            new RoomDistance("FOR_L2", 4),

            new RoomDistance("SIL_01", 6),
            new RoomDistance("SIL_02", 7),
            new RoomDistance("SIL_03", 8, true),

            new RoomDistance("FOR_01", 3),
            new RoomDistance("FOR_02", 4),
            new RoomDistance("CHA_FAR", 5),
            new RoomDistance("CHA_NEAR", 6),
            new RoomDistance("CHA_INT_01", 7),
            new RoomDistance("CHA_INT_02", 8, true),
        };

        Debug.Log("[ObeliskMusicSpatializer] Distance map V1 réinitialisée.");
    }
}
