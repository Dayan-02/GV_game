using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>One launch-only cinematic; the menu remains the destination for Back/Restart.</summary>
[RequireComponent(typeof(VideoPlayer))]
public sealed class OpeningVideoController : MonoBehaviour
{
    [SerializeField] private RawImage screen;
    [SerializeField] private CanvasGroup title;
    [SerializeField] private CanvasGroup blackout;
    [SerializeField] private TMP_Text hint;
    [SerializeField] private string nextScene = "MainMenu";
    [SerializeField] private string videoFile = "Opening/OutbreakIntro.mp4";
    [SerializeField, Min(1)] private float preparationTimeout = 8;
    private VideoPlayer video;
    private RenderTexture target;
    private bool leaving;
    private bool playbackStarted;
    private float startedAt;

    private IEnumerator Start()
    {
        Time.timeScale = 1;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        video = GetComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.isLooping = false;
        video.waitForFirstFrame = true;
        video.skipOnDrop = true;
        video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.audioOutputMode = VideoAudioOutputMode.Direct;
        video.controlledAudioTrackCount = 1;
        video.SetDirectAudioVolume(0, .55f);
        target = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
        target.Create();
        video.targetTexture = target;
        screen.texture = target;
        screen.color = Color.black;
        title.alpha = 0;
        blackout.alpha = 0;
        video.loopPointReached += Finished;
        video.errorReceived += Failed;
        string path = Path.Combine(Application.streamingAssetsPath, videoFile);
        if (!File.Exists(path)) { Leave(); yield break; }
        video.url = new System.Uri(path).AbsoluteUri;
        video.source = VideoSource.Url;
        video.Prepare();
        float deadline = Time.realtimeSinceStartup + preparationTimeout;
        while (!video.isPrepared && !leaving && Time.realtimeSinceStartup < deadline) yield return null;
        if (leaving) yield break;
        if (!video.isPrepared) { Leave(); yield break; }
        screen.color = Color.white;
        startedAt = Time.unscaledTime;
        playbackStarted = true;
        video.Play();
    }

    private void Update()
    {
        if (leaving) return;
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0)) { Leave(); return; }
        // This also covers a decoder that stops without reporting an error.
        if (playbackStarted && Time.unscaledTime - startedAt > 15) { Leave(); return; }
        if (video == null || !video.isPlaying) return;
        float t = (float)video.time;
        title.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 5.6f) / 1.2f)) *
                      (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 9.1f) / .7f)));
    }

    private void Finished(VideoPlayer player) => Leave();
    private void Failed(VideoPlayer player, string message)
    {
        Debug.LogWarning("Opening video unavailable; continuing to menu. " + message, this);
        Leave();
    }
    public void Leave()
    {
        if (leaving) return;
        leaving = true;
        StartCoroutine(Transition());
    }
    private IEnumerator Transition()
    {
        hint.text = "ENTERING THE DISTRICT";
        for (float t = 0; t < .35f; t += Time.unscaledDeltaTime)
        {
            blackout.alpha = Mathf.Clamp01(t / .35f);
            if (video != null && video.isPrepared) video.SetDirectAudioVolume(0, .55f * (1 - blackout.alpha));
            yield return null;
        }
        blackout.alpha = 1;
        if (video != null) video.Stop();
        yield return SceneManager.LoadSceneAsync(nextScene);
    }
    private void OnDestroy()
    {
        if (video != null) { video.loopPointReached -= Finished; video.errorReceived -= Failed; video.targetTexture = null; }
        if (screen != null) screen.texture = null;
        if (target != null) { target.Release(); Destroy(target); }
    }
}
