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

    public void SubmitCard()
    {
        string text = cardInput.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        string phase = phaseDropdown.options[phaseDropdown.value].text;
        GameManager.Instance.photonView.RPC("SubmitCard", RpcTarget.MasterClient, text, phase);

        cardInput.text = "";
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
}
