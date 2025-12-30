using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject gamePanel;

    private void Start()
    {
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        if (gamePanel) gamePanel.SetActive(false);
    }

    public void ShowLobby()
    {
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        if (gamePanel) gamePanel.SetActive(false);
    }

    public void ShowGameUI()
    {
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        if (gamePanel) gamePanel.SetActive(true);
    }

}
