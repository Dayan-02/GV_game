using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameScreenState
{
    Playing,
    Paused,
    GameOver,
    Victory
}

/// <summary>Owns UI screen state, pause time/audio and cursor. Contains no wave or victory-condition logic.</summary>
[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public sealed class GameUIController : MonoBehaviour
{
    [SerializeField]
    private PlayerHealth health;
    [SerializeField]
    private PlayerAimController aim;
    [SerializeField]
    private ThirdPersonCameraController cameraRig;
    [SerializeField]
    private bool pauseOnFocusLoss = true;
    private bool ownsFreeze;
    private float previousTimeScale;
    private bool previousAudioPause;
    private bool restarting;
    public GameScreenState State { get; private set; } = GameScreenState.Playing;
    public bool IsPlaying => State == GameScreenState.Playing;

    public event Action<GameScreenState> StateChanged;
    private void Awake()
    {
        if (health != null && aim != null && cameraRig != null)
            return;
        Debug.LogError("GameUIController requires PlayerHealth, PlayerAimController and the camera rig.", this);
        enabled = false;
    }

    private void OnEnable()
    {
        if (health == null || aim == null || cameraRig == null)
            return;
        health.Died += ShowGameOver;
        cameraRig.SetCursorManagedByUI(true);
        ApplyState();
    }

    private void Start()
    {
        if (health != null && health.IsDead)
            ShowGameOver();
    }

    private void OnDisable()
    {
        if (health != null)
            health.Died -= ShowGameOver;
        ReleaseFreeze();
        if (aim != null)
            aim.SetInputSuppressed(false);
        if (cameraRig != null)
        {
            cameraRig.SetCursorManagedByUI(false);
            cameraRig.SetGameplayInputEnabled(health != null && !health.IsDead);
        }
    }

    private void Update()
    {
        // Input and EventSystem Update still run while timeScale is zero.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (State == GameScreenState.Playing)
                Pause();
            else if (State == GameScreenState.Paused)
                Resume();
        }
    }

    public void Pause()
    {
        if (State == GameScreenState.Playing)
            ChangeState(GameScreenState.Paused);
    }

    public void Resume()
    {
        if (State != GameScreenState.Paused)
            return;
        if (health.IsDead)
            ShowGameOver();
        else
            ChangeState(GameScreenState.Playing);
    }

    public void ShowGameOver()
    {
        // A completed victory screen remains terminal until restart.
        if (State != GameScreenState.Victory)
            ChangeState(GameScreenState.GameOver);
    }

    public void ShowVictory()
    {
        if (health != null && !health.IsDead && State != GameScreenState.GameOver)
            ChangeState(GameScreenState.Victory);
    }

    private void ChangeState(GameScreenState next)
    {
        if (!isActiveAndEnabled || State == next || restarting)
            return;
        State = next;
        ApplyState();
        StateChanged?.Invoke(State);
    }

    private void ApplyState()
    {
        if (IsPlaying)
            ReleaseFreeze();
        else if (!ownsFreeze)
        {
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            ownsFreeze = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        aim.SetInputSuppressed(!IsPlaying);
        cameraRig.SetGameplayInputEnabled(IsPlaying);
    }

    private void ReleaseFreeze()
    {
        if (!ownsFreeze)
            return;
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
        ownsFreeze = false;
    }

    public void RestartLevel()
    {
        if (!isActiveAndEnabled || restarting)
            return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex < 0)
        {
            Debug.LogError("Add ZombieMap to Build Profiles > Scene List before using Restart.", this);
            return;
        }

        restarting = true;
        ReleaseFreeze();
        SceneManager.LoadScene(scene.buildIndex);
    }

    public void ReturnToMenu()
    {
        if (restarting)
            return;
        restarting = true;
        ReleaseFreeze();
        Time.timeScale = 1;
        AudioListener.pause = false;
        SceneManager.LoadScene("MainMenu");
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && pauseOnFocusLoss && isActiveAndEnabled)
            Pause();
    }

    [ContextMenu("Phase 4/Preview Victory (Play Mode)")]
    private void PreviewVictory()
    {
        if (Application.isPlaying)
            ShowVictory();
    }

    [ContextMenu("Phase 4/Preview Game Over (Play Mode)")]
    private void PreviewGameOver()
    {
        if (Application.isPlaying && health != null)
            health.Die();
    }
}
