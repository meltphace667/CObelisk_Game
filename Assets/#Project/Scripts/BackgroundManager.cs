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

    private string currentRoomId;
    private bool isChangingRoom = false;

    public bool IsChangingRoom => isChangingRoom;

    public event Action<string> OnRoomChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        PrepareRooms();
        ChangeToImmediate(startRoomId);
    }

    private void PrepareRooms()
    {
        foreach (Room room in rooms)
        {
            if (room.image == null)
                continue;

            room.image.SetActive(false);

            Image img = room.image.GetComponent<Image>();

            if (img != null)
                img.raycastTarget = false;
        }
    }

    public void TryMove(Direction direction)
    {
        if (isChangingRoom)
            return;

        Room currentRoom = GetRoom(currentRoomId);

        if (currentRoom == null)
        {
            Debug.LogError($"[BackgroundManager] Room courante introuvable : '{currentRoomId}'");
            return;
        }

        string targetId = GetTargetId(currentRoom, direction);

        if (string.IsNullOrEmpty(targetId))
        {
            Debug.Log($"[BackgroundManager] Pas de sortie vers {direction} depuis '{currentRoomId}'");
            return;
        }

        if (ObeliskBlackSquareDirector.Instance != null &&
            !ObeliskBlackSquareDirector.Instance.CanMove(currentRoomId, direction, targetId))
        {
            return;
        }

        StartCoroutine(ChangeToWithFade(targetId));
    }

    public void ChangeTo(string targetId)
    {
        if (isChangingRoom)
            return;

        StartCoroutine(ChangeToWithFade(targetId));
    }

    private IEnumerator ChangeToWithFade(string targetId)
    {
        isChangingRoom = true;

        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeToBlackRoutine();

        ChangeToImmediate(targetId);

        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.HoldBlackRoutine();

        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeFromBlackRoutine();

        isChangingRoom = false;
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
            if (room.image != null)
                room.image.SetActive(false);
        }

        target.image.SetActive(true);
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
            if (!string.IsNullOrEmpty(room.id))
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
        return rooms.Find(room => room.id == id);
    }
}
