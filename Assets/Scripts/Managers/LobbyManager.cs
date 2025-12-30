using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

// Handles lobby: create/join session, set name, instantiate avatar.
public class LobbyManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private GameObject lobbyPanel;
    private UIManager uiManager;

    private void Start()
    {
        PhotonNetwork.ConnectUsingSettings();
        uiManager = FindFirstObjectByType<UIManager>();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master");
    }

    public void SetName()
    {
        PhotonNetwork.NickName = nameInput.text;
    }

    public void CreateSession()
    {
        SetName();
        string code = codeInput.text;
        RoomOptions options = new RoomOptions { MaxPlayers = 20 };
        PhotonNetwork.CreateRoom(code, options);
    }

    public void JoinSession()
    {
        SetName();
        string code = codeInput.text;
        PhotonNetwork.JoinRoom(code);
    }

    public override void OnJoinedRoom()
    {
        lobbyPanel.SetActive(false);

        Debug.Log("AvatarPrefab Resources.Load = " + (Resources.Load<GameObject>("AvatarPrefab") != null));

        // Instantiate minimal avatar 
        Vector2 circle = Random.insideUnitCircle.normalized * 3f;
        Vector3 pos = new Vector3(circle.x, 1f, circle.y);


        if (Resources.Load<GameObject>("AvatarPrefab") == null)
        {
            Debug.LogError("Missing Resources/AvatarPrefab prefab. Expected at Assets/Resources/AvatarPrefab.prefab");
            return;
        }
        GameObject avatar = PhotonNetwork.Instantiate("AvatarPrefab", pos, Quaternion.identity);

        // Set name tag
        avatar.GetComponentInChildren<TextMeshPro>().text = PhotonNetwork.NickName;
        if (PhotonNetwork.IsMasterClient)
        {
            // Show reveal button for master
            uiManager.ShowRevealButton(true);
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError("Join failed: " + message);
    }
}