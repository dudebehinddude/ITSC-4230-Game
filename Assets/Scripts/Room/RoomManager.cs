using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [SerializeField] private Player player;
    [SerializeField] private PlayerDeathHandler playerDeathHandler;

    private Room[] rooms = System.Array.Empty<Room>();
    private readonly List<Room> roomsPlayerIsInside = new List<Room>();
    private Room currentRoom;
    private bool hasAssignedRoom;
    private bool isRespawning;
    private Vector2 entryPosition;
    private RoomSpawn activeSpawn;

    public Room CurrentRoom => currentRoom;
    public RoomSpawn ActiveSpawn => activeSpawn;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple RoomManager instances found. Using the first one.", this);
            return;
        }

        Instance = this;

        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
        }

        if (playerDeathHandler == null && player != null)
        {
            playerDeathHandler = player.GetComponent<PlayerDeathHandler>();
        }

        rooms = FindObjectsByType<Room>();
    }

    private void Start()
    {
        if (player == null)
        {
            return;
        }

        Vector2 playerPosition = player.transform.position;
        RefreshRoomsPlayerIsInside(playerPosition);

        Room startingRoom = FindFallbackRoom(playerPosition);
        if (startingRoom != null)
        {
            ActivateRoom(startingRoom, playerPosition, forceInstant: true);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void FixedUpdate()
    {
        if (player == null)
        {
            return;
        }

        if (isRespawning || currentRoom != null || !hasAssignedRoom)
        {
            return;
        }

        Vector2 playerPosition = player.transform.position;
        RefreshRoomsPlayerIsInside(playerPosition);

        Room resolved = FindFallbackRoom(playerPosition);
        if (resolved != null)
        {
            ActivateRoom(resolved, playerPosition);
            return;
        }

        playerDeathHandler?.Kill();
    }

    private Room FindFallbackRoom(Vector2 position)
    {
        for (int i = 0; i < roomsPlayerIsInside.Count; i++)
        {
            Room room = roomsPlayerIsInside[i];
            if (room != null)
            {
                return room;
            }
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            Room room = rooms[i];
            if (room != null && room.ContainsPoint(position))
            {
                return room;
            }
        }

        return null;
    }

    public void OnPlayerEnteredRoom(Room room, Vector2 entryPos)
    {
        if (room == null || isRespawning)
        {
            return;
        }

        if (!MarkPlayerInsideRoom(room))
        {
            return;
        }

        ActivateRoom(room, entryPos);
    }

    private void ActivateRoom(Room room, Vector2 entryPos, bool forceInstant = false, bool resetActiveSpawn = true)
    {
        bool instant = forceInstant || !hasAssignedRoom;
        hasAssignedRoom = true;
        currentRoom = room;
        entryPosition = entryPos;

        if (resetActiveSpawn)
        {
            activeSpawn = room.GetNearestSpawn(entryPos);
            if (activeSpawn == null)
            {
                activeSpawn = room.DefaultSpawn;
            }
        }

        if (CameraController.Instance != null)
        {
            float? orthoOverride = null;
            if (room.TryGetOrthoSizeOverride(out float size))
            {
                orthoOverride = size;
            }

            CameraController.Instance.SetRoomBounds(room.CameraBounds, instant, orthoOverride);
        }

        if (room.HasStageOnEnter)
        {
            GameStageManager.RequestStage(room.StageOnEnter);
        }
    }

    public void OnPlayerExitedRoom(Room room, Vector2 exitPos)
    {
        if (room == null || isRespawning)
        {
            return;
        }

        if (!MarkPlayerOutsideRoom(room))
        {
            return;
        }

        if (room == currentRoom)
        {
            ResolveAfterLeavingCurrentRoom(exitPos);
        }
    }

    private void ResolveAfterLeavingCurrentRoom(Vector2 playerPosition)
    {
        Room resolved = FindFallbackRoom(playerPosition);
        if (resolved != null)
        {
            ActivateRoom(resolved, playerPosition);
            return;
        }

        currentRoom = null;
    }

    private bool MarkPlayerInsideRoom(Room room)
    {
        if (roomsPlayerIsInside.Contains(room))
        {
            return false;
        }

        roomsPlayerIsInside.Add(room);
        return true;
    }

    private bool MarkPlayerOutsideRoom(Room room)
    {
        return roomsPlayerIsInside.Remove(room);
    }

    private void PrioritizeRoomPlayerIsInside(Room room)
    {
        roomsPlayerIsInside.Remove(room);
        roomsPlayerIsInside.Insert(0, room);
    }

    private void RefreshRoomsPlayerIsInside(Vector2 playerPosition)
    {
        roomsPlayerIsInside.Clear();

        for (int i = 0; i < rooms.Length; i++)
        {
            Room room = rooms[i];
            if (room != null && room.ContainsPoint(playerPosition))
            {
                roomsPlayerIsInside.Add(room);
            }
        }
    }

    public void SetActiveSpawn(RoomSpawn spawn)
    {
        if (spawn == null || isRespawning)
        {
            return;
        }

        activeSpawn = spawn;
    }

    public void BeginRespawn()
    {
        isRespawning = true;
    }

    public void EndRespawn()
    {
        isRespawning = false;
    }

    public void RespawnPlayer()
    {
        if (player == null)
        {
            return;
        }

        Room respawnRoom = activeSpawn != null ? activeSpawn.Room : currentRoom;
        Vector2 spawnPosition = ResolveSpawnPosition();

        player.Teleport(spawnPosition);
        RefreshRoomsPlayerIsInside(spawnPosition);

        if (respawnRoom == null || !respawnRoom.ContainsPoint(spawnPosition))
        {
            respawnRoom = FindFallbackRoom(spawnPosition);
        }

        if (respawnRoom != null)
        {
            PrioritizeRoomPlayerIsInside(respawnRoom);
            ActivateRoom(respawnRoom, spawnPosition, forceInstant: true, resetActiveSpawn: false);
            return;
        }

        currentRoom = null;
    }

    private Vector2 ResolveSpawnPosition()
    {
        if (activeSpawn != null)
        {
            return activeSpawn.Position;
        }

        if (currentRoom != null)
        {
            RoomSpawn fallback = currentRoom.DefaultSpawn;
            if (fallback != null)
            {
                return fallback.Position;
            }
        }

        return player.transform.position;
    }
}
