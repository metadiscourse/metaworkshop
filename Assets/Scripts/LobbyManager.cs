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

    private void Start()
    {
        PhotonNetwork.ConnectUsingSettings();
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
        // Instantiate minimal avatar (position random around center)
        Vector3 pos = Random.insideUnitSphere * 5 + new Vector3(0, 1, 0);
        GameObject avatar = PhotonNetwork.Instantiate("AvatarPrefab", pos, Quaternion.identity);
        // Set name tag
        avatar.GetComponentInChildren<TextMeshPro>().text = PhotonNetwork.NickName;
        if (PhotonNetwork.IsMasterClient)
        {
            // Show reveal button for master
            FindObjectOfType<UIManager>().ShowRevealButton(true);
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError("Join failed: " + message);
    }
}