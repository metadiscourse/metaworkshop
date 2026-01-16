using UnityEngine;
using TMPro;
using Photon.Pun;

/// <summary>
/// Handles UI interactions: card submission, reveal trigger, and combo feedback.
/// </summary>
public class UIManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField cardInput;
    [SerializeField] private TMP_Dropdown phaseDropdown; // Options: pre, post
    [SerializeField] private GameObject revealButton;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private TextMeshProUGUI warningText;

    [SerializeField] private GameObject cardPhasePanel;
    [SerializeField] private TextMeshProUGUI cardsRemainingText;
    [SerializeField] private TextMeshProUGUI timerText;

    private int cardsRequired;
    private int cardsSubmitted;
    private float writeEndLocalTime;

    public void ShowCardPhaseUI(bool show, int required, float serverStartTime, float durationSeconds)
    {
        if (cardPhasePanel) cardPhasePanel.SetActive(show);

        if (show)
        {
            cardsRequired = required;
            cardsSubmitted = 0;

            // Time
            writeEndLocalTime = Time.time + durationSeconds;

            UpdateCardsRemaining();
            CancelInvoke(nameof(UpdateTimer));
            InvokeRepeating(nameof(UpdateTimer), 0f, 0.2f);
        }
        else
        {
            CancelInvoke(nameof(UpdateTimer));
            if (timerText) timerText.text = "";
        }
    }

    private void UpdateCardsRemaining()
    {
        if (cardsRemainingText)
            cardsRemainingText.text = $"Cards: {cardsSubmitted}/{cardsRequired}";
    }

    private void UpdateTimer()
    {
        if (!timerText) return;
        float remaining = Mathf.Max(0f, writeEndLocalTime - Time.time);
        timerText.text = $"Time: {remaining:0}s";
    }

    public void SubmitCard()
    {
        string text = cardInput.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        string phase = phaseDropdown.options[phaseDropdown.value].text;
        GameManager.Instance.photonView.RPC("SubmitCard", RpcTarget.MasterClient, text, phase);

        cardInput.text = "";

        cardsSubmitted++;
        UpdateCardsRemaining();

        if (RoundManager.Instance != null)
        {
            // Notify master the player submitted a card
            RoundManager.Instance.photonView.RPC("RPC_NotifySubmitted", RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.UserId);
        }
    }

    public void StartReveal()
    {
        GameManager.Instance.TriggerReveal();
    }

    public void ShowRevealButton(bool show)
    {
        revealButton?.SetActive(show);
    }

    public void ShowCombo(string clusterId, int count)
    {
        comboText.text = $"Combo on {clusterId}: {count} bonks!";
        CancelInvoke(nameof(ClearComboText));
        Invoke(nameof(ClearComboText), 3f);
    }

    private void ClearComboText()
    {
        comboText.text = "";
    }

    public void ShowError(string message)
    {
        if (!warningText)
        {
            Debug.LogError(message);
            return;
        }

        warningText.text = message;
        CancelInvoke(nameof(ClearError));
        Invoke(nameof(ClearError), 4f);
    }

    private void ClearError()
    {
        if (warningText)
        {
            warningText.text = "";
        }
    }
}
