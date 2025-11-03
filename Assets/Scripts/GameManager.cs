using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Networking;
using System;
using ExitGames.Client.Photon;
using TMPro;

// Handles core logic: submit card, reveal, bonk RPCs, combo detection.
public class GameManager : MonoBehaviourPun
{
    public static GameManager Instance;
    private List<BonkData> allBonks = new List<BonkData>();
    private const float ComboWindowMs = 3000f;
    private const string ServerUrl = "http://localhost:3000";
    private const byte RevealWaveEvent = 1;
    private const byte ComboEvent = 2;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    // Called from UI: Submit card to master via RPC, master posts to server for dedup/storage.
    [PunRPC]
    public void SubmitCard(string text, string phase)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        StartCoroutine(PostCardToServer(text, PhotonNetwork.LocalPlayer.UserId, phase));
    }

    private IEnumerator PostCardToServer(string text, string playerId, string phase)
    {
        string url = $"{ServerUrl}/sessions/{PhotonNetwork.CurrentRoom.Name}/cards";
        string json = JsonUtility.ToJson(new CardSubmitData { text = text, player_id = playerId, phase = phase });
        using (UnityWebRequest www = UnityWebRequest.Post(url, json, "application/json"))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                if (www.responseCode == 201)
                {
                    Debug.Log("Card added");
                }
                else if (www.responseCode == 409)
                {
                    Debug.Log("Duplicate card ignored");
                }
            }
            else
            {
                Debug.LogError("Submit failed: " + www.error);
            }
        }
    }

    // Master triggers reveal: fetch cards from server, divide into waves, send events with delay.
    public void TriggerReveal()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        StartCoroutine(FetchCardsAndReveal());
    }

    private IEnumerator FetchCardsAndReveal()
    {
        string url = $"{ServerUrl}/sessions/{PhotonNetwork.CurrentRoom.Name}/cards";
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                string json = www.downloadHandler.text;
                RevealCardData[] cards = JsonUtility.FromJson<RevealCardWrapper>("{\"cards\":" + json + "}").cards;
                // Shuffle and divide into waves (e.g., 5 per wave)
                System.Random rng = new System.Random();
                List<RevealCardData> cardList = new List<RevealCardData>(cards);
                cardList.Sort((a, b) => rng.Next(-1, 1));
                int waveSize = 5;
                for (int i = 0; i < cardList.Count; i += waveSize)
                {
                    RevealCardData[] wave = cardList.GetRange(i, Mathf.Min(waveSize, cardList.Count - i)).ToArray();
                    object content = wave;
                    RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                    PhotonNetwork.RaiseEvent(RevealWaveEvent, content, options, SendOptions.SendReliable);
                    yield return new WaitForSeconds(2f); // Delay between waves
                }
            }
            else
            {
                Debug.LogError("Fetch cards failed: " + www.error);
            }
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == RevealWaveEvent)
        {
            RevealCardData[] wave = (RevealCardData[])photonEvent.CustomData;
            foreach (var card in wave)
            {
                // Instantiate card locally and animate
                GameObject cardObj = Instantiate(Resources.Load<GameObject>("CardPrefab"));
                cardObj.GetComponentInChildren<TextMeshPro>().text = card.text;
                cardObj.GetComponent<CardScript>().clusterId = card.cleaned;
                // Start floating animation
                cardObj.GetComponent<CardScript>().FloatToCenter();
            }
        }
        else if (photonEvent.Code == ComboEvent)
        {
            object[] data = (object[])photonEvent.CustomData;
            string clusterId = (string)data[0];
            int count = (int)data[1];
            long ts = (long)data[2];
            // Display combo (e.g., UI text)
            FindObjectOfType<UIManager>().ShowCombo(clusterId, count);
        }
    }

    // Called from CardScript on click: RPC to master for bonk.
    [PunRPC]
    public void BonkCard(string clusterId)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        BonkData bonk = new BonkData { cluster_id = clusterId, player_id = PhotonNetwork.LocalPlayer.UserId, timestamp = ts };
        allBonks.Add(bonk);
        StartCoroutine(PostBonkToServer(bonk));

        // Check for combo
        List<BonkData> recent = allBonks.FindAll(b => b.cluster_id == clusterId && b.timestamp > ts - ComboWindowMs);
        if (recent.Count >= 2)
        {
            int count = recent.Count;
            object[] content = new object[] { clusterId, count, ts };
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(ComboEvent, content, options, SendOptions.SendReliable);
            StartCoroutine(PostComboToServer(clusterId, count, ts));
        }
    }

    private IEnumerator PostBonkToServer(BonkData bonk)
    {
        string url = $"{ServerUrl}/sessions/{PhotonNetwork.CurrentRoom.Name}/bonks";
        string json = JsonUtility.ToJson(bonk);
        using (UnityWebRequest www = UnityWebRequest.Post(url, json, "application/json"))
        {
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Bonk post failed: " + www.error);
            }
        }
    }

    private IEnumerator PostComboToServer(string clusterId, int count, long ts)
    {
        string url = $"{ServerUrl}/sessions/{PhotonNetwork.CurrentRoom.Name}/combos";
        string json = JsonUtility.ToJson(new ComboData { cluster_id = clusterId, combo_count = count, timestamp = ts });
        using (UnityWebRequest www = UnityWebRequest.Post(url, json, "application/json"))
        {
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Combo post failed: " + www.error);
            }
        }
    }
}

// Data classes
[System.Serializable]
public class CardSubmitData
{
    public string text;
    public string player_id;
    public string phase;
}

[System.Serializable]
public class RevealCardData
{
    public string text;
    public string cleaned;
}

[System.Serializable]
class RevealCardWrapper
{
    public RevealCardData[] cards;
}

[System.Serializable]
public class BonkData
{
    public string cluster_id;
    public string player_id;
    public long timestamp;
}

[System.Serializable]
public class ComboData
{
    public string cluster_id;
    public int combo_count;
    public long timestamp;
}