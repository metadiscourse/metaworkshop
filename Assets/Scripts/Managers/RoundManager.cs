using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using Unity.Cinemachine;

public enum GamePhase
{
    Lobby,
    CardWriting,
    Reveal
}
public class RoundManager : MonoBehaviourPunCallbacks
{
    public static RoundManager Instance;

    [Header("Scene refs")]
    [SerializeField] private Transform boardsRoot;
    [SerializeField] private CinemachineCamera lobbyCam;
    [SerializeField] private CinemachineCamera boardCam;

    [Header("Settings (defaults)")]
    [SerializeField] private int playersPerBoard = 4;
    [SerializeField] private int cardsPerPlayer = 5;
    [SerializeField] private float writePhaseSeconds = 120f;

    private GamePhase phase = GamePhase.Lobby;

    // readiness: how many cards each player submitted in this round
    private readonly Dictionary<string, int> submittedCount = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartCardPhase()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        AssignBoards();
        submittedCount.Clear();

        // broadcast: enter writing phase and teleport
        photonView.RPC(nameof(RPC_BeginCardPhase), RpcTarget.All, (double)PhotonNetwork.Time, writePhaseSeconds, cardsPerPlayer);
    }

    private void AssignBoards()
    {
        // Assign boardId per player using ActorNumber sort
        Player[] players = PhotonNetwork.PlayerList;
        System.Array.Sort(players, (a, b) => a.ActorNumber.CompareTo(b.ActorNumber));

        for (int i = 0; i < players.Length; i++)
        {
            int boardId = i / Mathf.Max(1, playersPerBoard);
            int seatId = i % Mathf.Max(1, playersPerBoard);

            var props = new ExitGames.Client.Photon.Hashtable
            {
                { "boardId", boardId },
                { "seatId", seatId }
            };
            players[i].SetCustomProperties(props);
        }
    }

    [PunRPC]
    private void RPC_BeginCardPhase(double serverStartTime, float durationSeconds, int cardsRequired)
    {
        phase = GamePhase.CardWriting;

        // Change camera mode to board
        SetCameraMode(boardMode: true);

        // Disable movement while writing
        SetLocalMovementEnabled(false);

        // Teleport to board seat
        TeleportLocalToAssignedSeat();

        // Show board-writing UI
        var ui = FindFirstObjectByType<UIManager>();
        if (ui) ui.ShowCardPhaseUI(true, cardsRequired, (float)serverStartTime, durationSeconds);
    }

    private void TeleportLocalToAssignedSeat()
    {
        // Find local avatar in scene
        var localAvatar = FindLocalAvatar();
        if (!localAvatar) return;

        int boardId = GetLocalIntProp("boardId", 0);
        int seatId = GetLocalIntProp("seatId", 0);

        Transform board = boardsRoot ? boardsRoot.Find($"Board_{boardId}") : null;
        Transform spawn = board ? board.Find($"Spawn_{seatId}") : null;

        if (!spawn)
        {
            Debug.LogWarning($"No spawn found for Board_{boardId}/Spawn_{seatId}. Using current position");
            return;
        }

        // Teleport rigidbody safely
        var rb = localAvatar.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.position = spawn.position;
            rb.rotation = spawn.rotation;
            rb.linearVelocity = Vector3.zero;
        }
        else
        {
            localAvatar.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        }
    }

    private GameObject FindLocalAvatar()
    {
        // Find all PhotonViews and return the one that is mine and named AvatarPrefab instance
        foreach (var pv in FindObjectsByType<PhotonView>(FindObjectsSortMode.None))
        {
            if (pv.IsMine && pv.gameObject.name.Contains("AvatarPrefab"))
            {
                return pv.gameObject;
            }
        }
        return null;
    }

    private int GetLocalIntProp(string key, int fallback)
    {
        object val;
        if (PhotonNetwork.LocalPlayer.CustomProperties != null &&
            PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(key, out val) &&
            val is int i)
            return i;


        return fallback;
    }

    private void SetCameraMode(bool boardMode)
    {
        if (!lobbyCam || !boardCam) return;
        lobbyCam.Priority = boardMode ? 0 : 20;
        boardCam.Priority = boardMode ? 20 : 0;
    }

    private void SetLocalMovementEnabled(bool enabled)
    {
        var avatar = FindLocalAvatar();
        if (!avatar) return;
        var ctrl = avatar.GetComponent<LobbyPlayerController>();
        if (ctrl) ctrl.SetMovementEnabled(enabled);

    }

    // Called by master when any player submits a card
    [PunRPC]
    public void RPC_NotifySubmitted(string playerId)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (!submittedCount.ContainsKey(playerId)) submittedCount[playerId] = 0;
        submittedCount[playerId]++;

        // Check all ready
        foreach (var p in PhotonNetwork.PlayerList)
        {
            string id = p.UserId;
            if (!submittedCount.ContainsKey(id) || submittedCount[id] < cardsPerPlayer)
                return;
        }

        // Start reveal 
        photonView.RPC(nameof(RPC_BeginReveal), RpcTarget.All);
    }

    [PunRPC]
    private void RPC_BeginReveal()
    {
        phase = GamePhase.Reveal;

        var ui = FindFirstObjectByType<UIManager>();
        if (ui) ui.ShowCardPhaseUI(false, 0, 0, 0);

        // Master triggers reveal
        if (PhotonNetwork.IsMasterClient && GameManager.Instance != null)
            GameManager.Instance.TriggerReveal();
    }
}
