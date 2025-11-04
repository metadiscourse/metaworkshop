using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

/// <summary>
/// Core multiplayer logic: card submission, reveal orchestration, and combo tracking.
/// </summary>
public class GameManager : MonoBehaviourPun, IOnEventCallback
{
    public static GameManager Instance;

    private readonly List<BonkData> allBonks = new List<BonkData>();
    private const float ComboWindowMs = 3000f;
    private const string ServerUrl = "http://localhost:3000";
    private const byte RevealWaveEvent = 1;
    private const byte ComboEvent = 2;

    private UIManager uiManager;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        uiManager = FindFirstObjectByType<UIManager>();
    }

    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    // ---------------------------
    // RPC: Card Submission
    // ---------------------------
    [PunRPC]
    public void SubmitCard(string text, string phase)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        StartCoroutine(PostJson($"{ServerUrl}/sessions/{PhotonNetwork.CurrentRoom.Name}/cards",
            JsonUtility.ToJson(new CardSubmitData
            {
                text = text,
                player_id = PhotonNetwork.LocalPlayer.UserId,
                phase = phase
            })));
    }

    // ---------------------------
    // Reveal logic
    // ---------------------------
    public void TriggerReveal()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        StartCoroutine(FetchCardsAndReveal());
    }

    private IEnumerator FetchCardsAndReveal()
    {
        if (PhotonNetwork.CurrentRoom == null) yield break;
        string url = $"{ServerUrl}/sessions/{PhotonNetwork.CurrentRoom.Name}/cards";
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Fetch cards failed: " + www.error);
                yield break;
            }

            string json = www.downloadHandler.text;
            RevealCardData[] cards = JsonUtility.FromJson<RevealCardWrapper>("{\"cards\":" + json + "}").cards;
            if (cards.Length == 0) yield break;

            System.Random rng = new System.Random();
            List<RevealCardData> shuffled = new List<RevealCardData>(cards);
            shuffled.Sort((a, b) => rng.Next(-1, 2));

            int waveSize = 5;
            for (int i = 0; i < shuffled.Count; i += waveSize)
            {
                var wave = shuffled.GetRange(i, Mathf.Min(waveSize, shuffled.Count - i)).ToArray();
                RaiseEventOptions opts = new() { Receivers = ReceiverGroup.All };
                PhotonNetwork.RaiseEvent(RevealWaveEvent, wave, opts, SendOptions.SendReliable);
                yield return new WaitForSeconds(2f);
            }
        }
    }

    // ---------------------------
    // RaiseEvent callback handler
    // ---------------------------
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == RevealWaveEvent)
        {
            var wave = (RevealCardData[])photonEvent.CustomData;
            foreach (var card in wave)
            {
                GameObject cardObj = Instantiate(Resources.Load<GameObject>("CardPrefab"));
                cardObj.GetComponentInChildren<TextMeshPro>().text = card.text;
                var script = cardObj.GetComponent<CardScript>();
                script.clusterId = card.cleaned;
                script.FloatToCenter();
            }
        }
        else if (photonEvent.Code == ComboEvent)
        {
            object[] data = (object[])photonEvent.CustomData;
            string clusterId = (string)data[0];
            int count = (int)data[1];
            uiManager?.ShowCombo(clusterId, count);
        }
    }

    // ---------------------------
    // RPC: Bonk Logic
    // ---------------------------
    [PunRPC]
    public void BonkCard(string clusterId)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        BonkData bonk = new() { cluster_id = clusterId, player_id = PhotonNetwork.LocalPlayer.UserId, timestamp = ts };
        allBonks.Add(bonk);
        StartCoroutine(PostJson($"{ServerUrl}/sessions/{PhotonNetwork.CurrentRoom.Name}/bonks", JsonUtility.ToJson(bonk)));

        // Combo check
        var recent = allBonks.FindAll(b => b.cluster_id == clusterId && b.timestamp > ts - ComboWindowMs);
        if (recent.Count >= 2)
        {
            object[] content = new object[] { clusterId, recent.Count, ts };
            RaiseEventOptions opts = new() { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(ComboEvent, content, opts, SendOptions.SendReliable);

            var combo = new ComboData { cluster_id = clusterId, combo_count = recent.Count, timestamp = ts };
            StartCoroutine(PostJson($"{ServerUrl}/sessions/{PhotonNetwork.CurrentRoom.Name}/combos", JsonUtility.ToJson(combo)));
        }
    }

    // ---------------------------
    // Helper: Post JSON
    // ---------------------------
    private IEnumerator PostJson(string url, string json)
    {
        var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogError($"POST failed: {req.error}");
    }
}

// ---------------------------
// Data Classes
// ---------------------------
[System.Serializable] public class CardSubmitData { public string text; public string player_id; public string phase; }
[System.Serializable] public class RevealCardData { public string text; public string cleaned; }
[System.Serializable] public class RevealCardWrapper { public RevealCardData[] cards; }
[System.Serializable] public class BonkData { public string cluster_id; public string player_id; public long timestamp; }
[System.Serializable] public class ComboData { public string cluster_id; public int combo_count; public long timestamp; }
