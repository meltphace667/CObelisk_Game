using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BackgroundManager : MonoBehaviour
{
    public static BackgroundManager Instance { get; private set; }

    public enum Direction
    {
        Haut,
        Bas,
        Gauche,
        Droite
    }

    [System.Serializable]
    public class Room
    {
        [Header("Room")]
        public string id;
        public GameObject image;

        [Header("Connexions")]
        public string haut;
        public string bas;
        public string gauche;
        public string droite;
    }

    [Header("Toutes les rooms du jeu")]
    [SerializeField] private List<Room> rooms = new List<Room>();

    [Header("Room active au démarrage")]
    [SerializeField] private string startRoomId = "PRA_01";

    [Header("Transition - fluidité humaine")]
    [Tooltip("Change la room avant la fin technique du fade-to-black, dès que le noir est assez opaque pour que l'humain ne voie pas le changement.")]
    [SerializeField] private bool changeRoomWhenFadeLooksBlack = true;

    [Tooltip("Alpha du fade noir à partir duquel on peut changer la room sans que l'oeil voie le swap. 0.90 à 0.97 conseillé.")]
    [Range(0.75f, 1f)]
    [SerializeField] private float roomSwapOpaqueAlphaThreshold = 0.92f;

    [Tooltip("Si l'alpha du fade est introuvable, délai de secours avant de changer la room pendant le fade-to-black.")]
    [SerializeField] private float fallbackRoomSwapDelayAfterFadeToBlackStarts = 0.12f;

    [Tooltip("Débloque l'input quand le fade depuis noir est visuellement fini pour l'humain, pas quand la coroutine est mathématiquement finie.")]
    [SerializeField] private bool unlockInputWhenFadeIsHumanClear = true;

    [Tooltip("Alpha du fade noir à partir duquel l'humain ne perçoit presque plus le noir. 0.03 à 0.06 conseillé.")]
    [Range(0f, 0.15f)]
    [SerializeField] private float humanClearAlphaThreshold = 0.04f;

    [Tooltip("Si l'alpha du fade est introuvable, délai de secours avant de rendre l'input après le début du fade depuis noir.")]
    [SerializeField] private float fallbackUnlockDelayAfterFadeFromBlackStarts = 0.06f;

    [Tooltip("Sécurité anti-bug : l'input revient au plus tard après cette durée une fois la nouvelle room prête.")]
    [SerializeField] private float emergencyMaxInputLockAfterRoomReady = 0.35f;

    [Header("Spam / queue de déplacement")]
    [Tooltip("Quand le joueur clique pendant la toute fin visuelle d'une transition, on garde le dernier déplacement valide et on le lance proprement juste après.")]
    [SerializeField] private bool queueLastMoveDuringVisualTail = true;

    [Tooltip("Quand le joueur spam plusieurs directions pendant la fin d'un fade, seul le dernier clic valide est gardé.")]
    [SerializeField] private bool keepOnlyLastQueuedMove = true;

    [Tooltip("Évite les doubles déplacements instantanés avec des fades très courts.")]
    [SerializeField] private float minimumSecondsBetweenMoves = 0.06f;

    [Header("Fade overlay - optionnel")]
    [Tooltip("Si vide, auto-find depuis ScreenFader.")]
    [SerializeField] private Graphic fadeOverlayGraphic;

    [Tooltip("Optionnel. Si le fade utilise un CanvasGroup, mets-le ici ou laisse Auto Find.")]
    [SerializeField] private CanvasGroup fadeOverlayCanvasGroup;

    [SerializeField] private bool autoFindFadeOverlay = true;

    [Tooltip("Le fade noir doit être seulement visuel : il ne doit jamais bloquer les raycasts/clics.")]
    [SerializeField] private bool forceFadeOverlayNotRaycastable = true;

    [Header("Images de rooms")]
    [SerializeField] private bool forceRoomImagesNotRaycastable = true;

    [Header("Debug")]
    [SerializeField] private bool debugTransitionSync = false;

    private string currentRoomId;
    private bool isChangingRoom = false;
    private bool isVisualTransitionRunning = false;
    private bool roomSwapDoneForCurrentTransition = false;
    private float lastMoveStartTime = -999f;

    private bool hasQueuedTarget = false;
    private string queuedTargetId = "";
    private Direction queuedDirection;

    /// <summary>
    /// À utiliser par les zones/cursors pour bloquer le gameplay.
    /// Repasse à false dès que l'image est humainement lisible.
    /// </summary>
    public bool IsChangingRoom => isChangingRoom;

    /// <summary>
    /// True tant qu'une animation de transition existe encore.
    /// TryMove peut mettre un déplacement en queue pendant cette phase.
    /// </summary>
    public bool IsVisualTransitionRunning => isVisualTransitionRunning;

    public bool HasQueuedMove => hasQueuedTarget;

    public event Action<string> OnRoomChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        AutoFindFadeOverlayReferences();
        ApplyFadeOverlayClickThrough();
    }

    private void Start()
    {
        PrepareRooms();
        AutoFindFadeOverlayReferences();
        ApplyFadeOverlayClickThrough();
        ChangeToImmediate(startRoomId);
    }

    private void PrepareRooms()
    {
        foreach (Room room in rooms)
        {
            if (room == null || room.image == null)
                continue;

            room.image.SetActive(false);

            if (forceRoomImagesNotRaycastable)
                DisableRaycastsOnGameObject(room.image);
        }
    }

    private void DisableRaycastsOnGameObject(GameObject root)
    {
        if (root == null)
            return;

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = false;
        }

        CanvasGroup[] groups = root.GetComponentsInChildren<CanvasGroup>(true);

        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null)
                continue;

            groups[i].blocksRaycasts = false;
            groups[i].interactable = false;
        }
    }

    private void ApplyFadeOverlayClickThrough()
    {
        if (!forceFadeOverlayNotRaycastable)
            return;

        AutoFindFadeOverlayReferences();

        if (fadeOverlayGraphic != null)
            fadeOverlayGraphic.raycastTarget = false;

        if (fadeOverlayCanvasGroup != null)
        {
            fadeOverlayCanvasGroup.blocksRaycasts = false;
            fadeOverlayCanvasGroup.interactable = false;
        }
    }

    private void AutoFindFadeOverlayReferences()
    {
        if (!autoFindFadeOverlay)
            return;

        if (ScreenFader.Instance == null)
            return;

        if (fadeOverlayGraphic == null)
        {
            Graphic[] graphics = ScreenFader.Instance.GetComponentsInChildren<Graphic>(true);
            fadeOverlayGraphic = PickBestFadeGraphic(graphics);
        }

        if (fadeOverlayCanvasGroup == null)
        {
            CanvasGroup[] groups = ScreenFader.Instance.GetComponentsInChildren<CanvasGroup>(true);

            if (groups != null && groups.Length > 0)
                fadeOverlayCanvasGroup = groups[0];
        }
    }

    private Graphic PickBestFadeGraphic(Graphic[] graphics)
    {
        if (graphics == null || graphics.Length == 0)
            return null;

        Graphic fallback = null;

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null)
                continue;

            if (fallback == null)
                fallback = graphic;

            string lowerName = graphic.gameObject.name.ToLowerInvariant();

            if (lowerName.Contains("fade") ||
                lowerName.Contains("black") ||
                lowerName.Contains("overlay") ||
                lowerName.Contains("fader"))
            {
                return graphic;
            }
        }

        return fallback;
    }

    public void TryMove(Direction direction)
    {
        if (isChangingRoom)
            return;

        if (isVisualTransitionRunning)
        {
            TryQueueMove(direction);
            return;
        }

        if (Time.unscaledTime - lastMoveStartTime < minimumSecondsBetweenMoves)
            return;

        TryStartMoveNow(direction);
    }

    public void ChangeTo(string targetId)
    {
        if (string.IsNullOrEmpty(targetId))
            return;

        if (isChangingRoom)
            return;

        if (isVisualTransitionRunning)
        {
            QueueTarget(targetId, Direction.Bas);
            return;
        }

        if (Time.unscaledTime - lastMoveStartTime < minimumSecondsBetweenMoves)
            return;

        StartCoroutine(ChangeToWithFade(targetId));
    }

    private void TryStartMoveNow(Direction direction)
    {
        string targetId;

        if (!TryGetTargetForDirection(direction, out targetId))
            return;

        StartCoroutine(ChangeToWithFade(targetId));
    }

    private bool TryGetTargetForDirection(Direction direction, out string targetId)
    {
        targetId = "";

        Room currentRoom = GetRoom(currentRoomId);

        if (currentRoom == null)
        {
            Debug.LogError($"[BackgroundManager] Room courante introuvable : '{currentRoomId}'");
            return false;
        }

        targetId = GetTargetId(currentRoom, direction);

        if (string.IsNullOrEmpty(targetId))
        {
            Debug.Log($"[BackgroundManager] Pas de sortie vers {direction} depuis '{currentRoomId}'");
            return false;
        }

        if (ObeliskBlackSquareDirector.Instance != null &&
            !ObeliskBlackSquareDirector.Instance.CanMove(currentRoomId, direction, targetId))
        {
            targetId = "";
            return false;
        }

        return true;
    }

    private void TryQueueMove(Direction direction)
    {
        if (!queueLastMoveDuringVisualTail)
            return;

        string targetId;

        if (!TryGetTargetForDirection(direction, out targetId))
            return;

        QueueTarget(targetId, direction);
    }

    private void QueueTarget(string targetId, Direction direction)
    {
        if (string.IsNullOrEmpty(targetId))
            return;

        if (!keepOnlyLastQueuedMove && hasQueuedTarget)
            return;

        queuedTargetId = targetId;
        queuedDirection = direction;
        hasQueuedTarget = true;

        if (debugTransitionSync)
            Debug.Log($"[BackgroundManager] Move queued : {direction} → {targetId}");
    }

    private IEnumerator ChangeToWithFade(string targetId)
    {
        isChangingRoom = true;
        isVisualTransitionRunning = true;
        roomSwapDoneForCurrentTransition = false;
        hasQueuedTarget = false;
        queuedTargetId = "";
        lastMoveStartTime = Time.unscaledTime;

        ApplyFadeOverlayClickThrough();

        if (ScreenFader.Instance != null)
        {
            if (changeRoomWhenFadeLooksBlack)
            {
                Coroutine fadeToBlackCoroutine = StartCoroutine(ScreenFader.Instance.FadeToBlackRoutine());

                yield return WaitUntilFadeLooksOpaqueEnoughForRoomSwap();

                if (!roomSwapDoneForCurrentTransition)
                {
                    ChangeToImmediate(targetId);
                    roomSwapDoneForCurrentTransition = true;
                }

                yield return fadeToBlackCoroutine;
            }
            else
            {
                yield return ScreenFader.Instance.FadeToBlackRoutine();

                ChangeToImmediate(targetId);
                roomSwapDoneForCurrentTransition = true;
            }
        }
        else
        {
            ChangeToImmediate(targetId);
            roomSwapDoneForCurrentTransition = true;
        }

        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.HoldBlackRoutine();

        ApplyFadeOverlayClickThrough();

        if (ScreenFader.Instance != null)
        {
            if (unlockInputWhenFadeIsHumanClear)
            {
                Coroutine fadeFromBlackCoroutine = StartCoroutine(ScreenFader.Instance.FadeFromBlackRoutine());

                yield return WaitUntilFadeFeelsClearToHuman();

                isChangingRoom = false;

                if (debugTransitionSync)
                    Debug.Log("[BackgroundManager] Input débloqué : fade humainement clair.");

                yield return fadeFromBlackCoroutine;
            }
            else
            {
                yield return ScreenFader.Instance.FadeFromBlackRoutine();
                isChangingRoom = false;
            }
        }
        else
        {
            isChangingRoom = false;
        }

        isVisualTransitionRunning = false;
        ApplyFadeOverlayClickThrough();

        if (hasQueuedTarget)
        {
            string nextTarget = queuedTargetId;
            hasQueuedTarget = false;
            queuedTargetId = "";

            if (debugTransitionSync)
                Debug.Log($"[BackgroundManager] Move queued lancé → {nextTarget}");

            if (!string.IsNullOrEmpty(nextTarget) && RoomExists(nextTarget))
                StartCoroutine(ChangeToWithFade(nextTarget));
        }
    }

    private IEnumerator WaitUntilFadeLooksOpaqueEnoughForRoomSwap()
    {
        float elapsed = 0f;
        bool alphaWasReadableOnce = false;

        while (true)
        {
            ApplyFadeOverlayClickThrough();

            float alpha;
            bool alphaReadable = TryGetFadeAlpha(out alpha);

            if (alphaReadable)
            {
                alphaWasReadableOnce = true;

                if (alpha >= roomSwapOpaqueAlphaThreshold)
                    yield break;
            }
            else
            {
                if (!alphaWasReadableOnce && elapsed >= fallbackRoomSwapDelayAfterFadeToBlackStarts)
                    yield break;
            }

            // Sécurité : même si l'alpha est mal lu, on ne reste jamais bloqué.
            if (elapsed >= Mathf.Max(fallbackRoomSwapDelayAfterFadeToBlackStarts, 0.25f))
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitUntilFadeFeelsClearToHuman()
    {
        float elapsed = 0f;
        bool alphaWasReadableOnce = false;

        while (elapsed < emergencyMaxInputLockAfterRoomReady)
        {
            ApplyFadeOverlayClickThrough();

            float alpha;
            bool alphaReadable = TryGetFadeAlpha(out alpha);

            if (alphaReadable)
            {
                alphaWasReadableOnce = true;

                if (alpha <= humanClearAlphaThreshold)
                    yield break;
            }
            else
            {
                if (!alphaWasReadableOnce && elapsed >= fallbackUnlockDelayAfterFadeFromBlackStarts)
                    yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (debugTransitionSync)
            Debug.LogWarning("[BackgroundManager] Déblocage input par sécurité : impossible de confirmer alpha fade.");
    }

    private bool TryGetFadeAlpha(out float alpha)
    {
        alpha = 0f;

        AutoFindFadeOverlayReferences();

        if (fadeOverlayCanvasGroup != null)
        {
            alpha = fadeOverlayCanvasGroup.alpha;
            return true;
        }

        if (fadeOverlayGraphic != null)
        {
            alpha = fadeOverlayGraphic.color.a;
            return true;
        }

        return false;
    }

    private void ChangeToImmediate(string targetId)
    {
        Room target = GetRoom(targetId);

        if (target == null || target.image == null)
        {
            Debug.LogError($"[BackgroundManager] Room introuvable : '{targetId}'");
            return;
        }

        foreach (Room room in rooms)
        {
            if (room == null || room.image == null)
                continue;

            room.image.SetActive(false);

            if (forceRoomImagesNotRaycastable)
                DisableRaycastsOnGameObject(room.image);
        }

        target.image.SetActive(true);

        if (forceRoomImagesNotRaycastable)
            DisableRaycastsOnGameObject(target.image);

        currentRoomId = targetId;

        Debug.Log($"[BackgroundManager] → {targetId}");

        OnRoomChanged?.Invoke(currentRoomId);
    }

    public string GetCurrentRoomId()
    {
        return currentRoomId;
    }

    public bool RoomExists(string roomId)
    {
        return GetRoom(roomId) != null;
    }

    public List<string> GetAllRoomIds()
    {
        List<string> ids = new List<string>();

        foreach (Room room in rooms)
        {
            if (room != null && !string.IsNullOrEmpty(room.id))
                ids.Add(room.id);
        }

        return ids;
    }

    public List<string> GetConnectedRoomIdsUndirected(string roomId)
    {
        List<string> result = new List<string>();

        Room room = GetRoom(roomId);

        if (room != null)
        {
            AddConnectionIfValid(result, room.haut);
            AddConnectionIfValid(result, room.bas);
            AddConnectionIfValid(result, room.gauche);
            AddConnectionIfValid(result, room.droite);
        }

        foreach (Room otherRoom in rooms)
        {
            if (otherRoom == null)
                continue;

            if (string.IsNullOrEmpty(otherRoom.id))
                continue;

            if (otherRoom.id == roomId)
                continue;

            if (otherRoom.haut == roomId ||
                otherRoom.bas == roomId ||
                otherRoom.gauche == roomId ||
                otherRoom.droite == roomId)
            {
                AddConnectionIfValid(result, otherRoom.id);
            }
        }

        return result;
    }

    public bool TryGetDirectionFromRoomToNeighbor(string fromRoomId, string neighborRoomId, out Direction direction)
    {
        direction = Direction.Bas;

        Room fromRoom = GetRoom(fromRoomId);

        if (fromRoom != null)
        {
            if (fromRoom.haut == neighborRoomId)
            {
                direction = Direction.Haut;
                return true;
            }

            if (fromRoom.bas == neighborRoomId)
            {
                direction = Direction.Bas;
                return true;
            }

            if (fromRoom.gauche == neighborRoomId)
            {
                direction = Direction.Gauche;
                return true;
            }

            if (fromRoom.droite == neighborRoomId)
            {
                direction = Direction.Droite;
                return true;
            }
        }

        Room neighborRoom = GetRoom(neighborRoomId);

        if (neighborRoom != null)
        {
            if (neighborRoom.haut == fromRoomId)
            {
                direction = Direction.Bas;
                return true;
            }

            if (neighborRoom.bas == fromRoomId)
            {
                direction = Direction.Haut;
                return true;
            }

            if (neighborRoom.gauche == fromRoomId)
            {
                direction = Direction.Droite;
                return true;
            }

            if (neighborRoom.droite == fromRoomId)
            {
                direction = Direction.Gauche;
                return true;
            }
        }

        return false;
    }

    private void AddConnectionIfValid(List<string> list, string targetId)
    {
        if (string.IsNullOrEmpty(targetId))
            return;

        if (!RoomExists(targetId))
        {
            Debug.LogWarning($"[BackgroundManager] Connexion vers une room inexistante : '{targetId}'");
            return;
        }

        if (!list.Contains(targetId))
            list.Add(targetId);
    }

    private string GetTargetId(Room room, Direction direction)
    {
        switch (direction)
        {
            case Direction.Haut:
                return room.haut;

            case Direction.Bas:
                return room.bas;

            case Direction.Gauche:
                return room.gauche;

            case Direction.Droite:
                return room.droite;

            default:
                return "";
        }
    }

    private Room GetRoom(string id)
    {
        return rooms.Find(room => room != null && room.id == id);
    }
}
