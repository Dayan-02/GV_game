using UnityEngine;

/// <summary>Shows one modal panel at a time and wires explicit button callbacks.</summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class GamePanelsPresenter : MonoBehaviour
{
    [SerializeField]
    private GameUIController screens;
    [SerializeField]
    private UnityEngine.EventSystems.EventSystem eventSystem;
    [SerializeField]
    private GameObject pausePanel;
    [SerializeField]
    private GameObject gameOverPanel;
    [SerializeField]
    private GameObject victoryPanel;
    [SerializeField]
    private UnityEngine.UI.Button resumeButton;
    [SerializeField]
    private UnityEngine.UI.Button pauseRestartButton;
    [SerializeField]
    private UnityEngine.UI.Button gameOverRestartButton;
    [SerializeField]
    private UnityEngine.UI.Button victoryRestartButton;
    private bool subscribed;
    [SerializeField]
    private UnityEngine.UI.Button[] menuButtons = System.Array.Empty<UnityEngine.UI.Button>();
    private void OnEnable()
    {
        if (screens == null || eventSystem == null || pausePanel == null || gameOverPanel == null || victoryPanel == null || resumeButton == null || pauseRestartButton == null || gameOverRestartButton == null || victoryRestartButton == null)
        {
            Debug.LogError("GamePanelsPresenter needs its controller, EventSystem, panels and buttons.", this);
            enabled = false;
            return;
        }

        screens.StateChanged += Show;
        resumeButton.onClick.AddListener(screens.Resume);
        pauseRestartButton.onClick.AddListener(screens.RestartLevel);
        gameOverRestartButton.onClick.AddListener(screens.RestartLevel);
        victoryRestartButton.onClick.AddListener(screens.RestartLevel);
        foreach (var button in menuButtons)
            if (button != null)
                button.onClick.AddListener(screens.ReturnToMenu);
        subscribed = true;
        Show(screens.State);
    }

    private void OnDisable()
    {
        if (!subscribed)
            return;
        screens.StateChanged -= Show;
        resumeButton.onClick.RemoveListener(screens.Resume);
        pauseRestartButton.onClick.RemoveListener(screens.RestartLevel);
        gameOverRestartButton.onClick.RemoveListener(screens.RestartLevel);
        victoryRestartButton.onClick.RemoveListener(screens.RestartLevel);
        foreach (var button in menuButtons)
            if (button != null)
                button.onClick.RemoveListener(screens.ReturnToMenu);
        subscribed = false;
    }

    private void Show(GameScreenState state)
    {
        pausePanel.SetActive(state == GameScreenState.Paused);
        gameOverPanel.SetActive(state == GameScreenState.GameOver);
        victoryPanel.SetActive(state == GameScreenState.Victory);
        UnityEngine.UI.Button first = state == GameScreenState.Paused ? resumeButton : state == GameScreenState.GameOver ? gameOverRestartButton : state == GameScreenState.Victory ? victoryRestartButton : null;
        eventSystem.SetSelectedGameObject(null);
        if (first != null)
            eventSystem.SetSelectedGameObject(first.gameObject);
    }
}
