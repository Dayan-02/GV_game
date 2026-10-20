using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

public static class OpeningVideoSetup
{
    public const string ScenePath = "Assets/Scenes/Opening.unity";
    const string Frames = "ArtSource/OpeningVideo/Frames";
    static Camera capture;
    static RenderTexture render;
    static Texture2D pixels;
    static int frame;
    static double nextFrame;

    public static void BuildWindows()
    {
        ConfigureSceneFlow();
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled)
            .Select(s => s.path == OutbreakEditor.Scene ? ExistingCityEnhancer.ScenePath : s.path).Distinct().ToArray();
        if (scenes.Length == 0 || scenes[0] != ScenePath) throw new InvalidOperationException("Opening must be the first build scene.");
        if (!File.Exists("Assets/StreamingAssets/Opening/OutbreakIntro.mp4")) throw new FileNotFoundException("Opening MP4 has not been encoded.");
        Directory.CreateDirectory("Builds/Windows");
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = scenes, target = BuildTarget.StandaloneWindows64,
            locationPathName = "Builds/Windows/ZombieOutbreak.exe", options = BuildOptions.None });
        File.WriteAllText("Logs/OpeningVideoBuild.txt", result.summary.result + "\nErrors: " + result.summary.totalErrors + "\nWarnings: " + result.summary.totalWarnings);
        if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Opening video build failed.");
    }

    [MenuItem("Tools/Dead Zone/Use current game scene flow")]
    public static void ConfigureSceneFlow()
    {
        string[] paths = { ScenePath, "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Loadout.unity", ExistingCityEnhancer.ScenePath };
        foreach (string path in paths)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                throw new FileNotFoundException("Required startup scene is missing: " + path);
        // Persist the enhanced map for Editor Play and ordinary Build Settings too.
        // A build-only substitution leaves LoadScene("ZombieMap") using the old map in Editor.
        EditorBuildSettings.scenes = paths.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
    }

    [MenuItem("Tools/Dead Zone/Create opening video scene")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory("Assets/StreamingAssets/Opening");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Opening Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.cullingMask = 0;
        var canvas = new GameObject("Opening Canvas", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var picture = Node("City video", canvas.transform).gameObject.AddComponent<RawImage>();
        picture.color = Color.black; picture.raycastTarget = false;
        var fit = picture.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 16f / 9;
        var title = Node("Title reveal", canvas.transform).gameObject.AddComponent<CanvasGroup>();
        title.alpha = 0;
        Text(title.transform, "Chapter", "SECTOR 07 / QUARANTINE ZONE", 22, 66, new Color(.55f,.72f,.64f));
        Text(title.transform, "Title", "ZOMBIE OUTBREAK", 76, -4, new Color(.91f,.92f,.86f));
        Text(title.transform, "Tagline", "THE CITY FELL. YOU HAVEN'T.", 22, -75, new Color(.74f,.77f,.73f));
        var hint = Text(canvas.transform, "Skip", "SPACE / ENTER / ESC / CLICK TO SKIP", 18, 0, new Color(.65f,.69f,.67f));
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(.5f,0);
        hint.rectTransform.anchoredPosition = new Vector2(0, 54);
        var black = Node("Exit fade", canvas.transform).gameObject.AddComponent<Image>();
        black.color = Color.black; black.raycastTarget = false;
        var fade = black.gameObject.AddComponent<CanvasGroup>(); fade.alpha = 0;
        var driver = new GameObject("Opening Video", typeof(VideoPlayer)).AddComponent<OpeningVideoController>();
        var s = new SerializedObject(driver);
        s.FindProperty("screen").objectReferenceValue = picture;
        s.FindProperty("title").objectReferenceValue = title;
        s.FindProperty("blackout").objectReferenceValue = fade;
        s.FindProperty("hint").objectReferenceValue = hint;
        s.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        ConfigureSceneFlow();
        AssetDatabase.SaveAssets();
    }
    static RectTransform Node(string name, Transform parent)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; return r;
    }
    static TMP_Text Text(Transform parent, string name, string value, float size, float y, Color color)
    {
        var r = Node(name, parent); r.anchorMin = r.anchorMax = new Vector2(.5f,.5f); r.sizeDelta = new Vector2(1600,110); r.anchoredPosition = new Vector2(0,y);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>(); t.font = TMP_Settings.defaultFontAsset; t.text = value; t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center; t.color = color; t.characterSpacing = 5; t.raycastTarget = false; return t;
    }

    // Offline asset production only. The gameplay scene is opened but never saved.
    public static void CaptureAndInstall()
    {
        try
        {
            Install();
            EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
            foreach (var canvas in Object.FindObjectsByType<Canvas>()) canvas.gameObject.SetActive(false);
            foreach (var player in Object.FindObjectsByType<ThirdPersonPlayerController>()) player.gameObject.SetActive(false);
            foreach (var camera in Object.FindObjectsByType<Camera>()) camera.enabled = false;
            capture = new GameObject("Opening capture camera").AddComponent<Camera>();
            capture.clearFlags = CameraClearFlags.Skybox; capture.fieldOfView = 60; capture.nearClipPlane = .1f; capture.farClipPlane = 250;
            capture.allowHDR = false; capture.allowMSAA = true; capture.cullingMask = ~((1 << 30) | (1 << 5));
            QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowDistance = 70;
            render = new RenderTexture(1280,720,24) { antiAliasing = 2 }; render.Create(); capture.targetTexture = render;
            pixels = new Texture2D(1280,720,TextureFormat.RGB24,false);
            Directory.CreateDirectory(Frames); frame = 0; nextFrame = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update += CaptureFrame;
        }
        catch (Exception e) { Debug.LogException(e); if(Application.isBatchMode) EditorApplication.Exit(1); }
    }
    static void CaptureFrame()
    {
        if (EditorApplication.timeSinceStartup < nextFrame) return;
        try
        {
            float t = frame / 24f;
            Vector3 position, look;
            if (t < 5)
            {
                position = Vector3.Lerp(new Vector3(.45f,2.7f,-23),new Vector3(.8f,2.5f,-19),t/5);
                look = new Vector3(0,3,12);
            }
            else
            {
                position = Vector3.Lerp(new Vector3(-2.3f,1.9f,-8),new Vector3(-3.1f,2.15f,-5),(t-5)/5);
                look = new Vector3(-9,4,14);
            }
            capture.transform.SetPositionAndRotation(position,Quaternion.LookRotation(look-position));
            var previous = RenderTexture.active;
            capture.Render(); RenderTexture.active = render;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply(); RenderTexture.active = previous;
            File.WriteAllBytes(Frames + "/" + frame.ToString("D4") + ".png", pixels.EncodeToPNG());
            frame++;
            if (frame % 24 == 0) Debug.Log("OPENING_CAPTURE " + frame + "/240");
            if (frame < 240) { nextFrame = EditorApplication.timeSinceStartup + .025; return; }
            EditorApplication.update -= CaptureFrame;
            capture.targetTexture = null; render.Release(); Object.DestroyImmediate(render); Object.DestroyImmediate(pixels);
            EditorSceneManager.OpenScene(ScenePath);
            File.WriteAllText("Logs/OpeningCaptureComplete.txt", "240 frames, 1280x720, 24 fps. City scene not saved.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception e) { EditorApplication.update -= CaptureFrame; Debug.LogException(e); if(Application.isBatchMode) EditorApplication.Exit(1); }
    }
}
