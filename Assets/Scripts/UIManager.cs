using UnityEngine;
using TMPro;
using Photon.Pun;

// Handles UI: card submit, reveal button, combo display.
public class UIManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField cardInput;
    [SerializeField] private TMP_Dropdown phaseDropdown; // Options: pre, post
    [SerializeField] private GameObject revealButton;
    [SerializeField] private TextMeshProUGUI comboText; // For displaying combos

    public void SubmitCard()
    {
        string text = cardInput.text;
        string phase = phaseDropdown.options[phaseDropdown.value].text;
        GameManager.Instance.photonView.RPC("SubmitCard", PhotonTargets.MasterClient, text, phase);
        cardInput.text = "";
    }

    public void StartReveal()
    {
        GameManager.Instance.TriggerReveal();
    }

    public void ShowRevealButton(bool show)
    {
        revealButton.SetActive(show);
    }

    public void ShowCombo(string clusterId, int count)
    {
        comboText.text = $"Combo on {clusterId}: {count} bonks!";
        // Fade out after 3s, etc.
    }
}