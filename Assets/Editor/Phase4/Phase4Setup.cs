using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Adds Phase 4 UI to the existing scene. Reruns only rebind sources, preserving layout edits.</summary>
public static class Phase4Setup
{
    public const string ScenePath = ExistingCityEnhancer.ScenePath;
    public const string PrefabPath = "Assets/Prefabs/UI/GameUI.prefab";
    private static bool waitingForText;
    private static bool ownsBatchProcess;
    private static double textDeadline;

    // Run with the Editor binary directly, without -quit, so TMP resource import can finish.
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("RunBatch is only for a temporary batch Editor.");
        ownsBatchProcess = true;
        try { SetUp(); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/Dead city/Phase 4/Set Up HUD and Panels")]
    public static void SetUp()
    {
        CurrentGameScenePolicy.RejectLegacySceneGeneration();
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before UI setup.");
        if (!Phase4UIFactory.TextResourcesReady)
        {
            if (waitingForText) return;
            waitingForText = true;
            textDeadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += WaitForText;
            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        PlayerWeaponController player = All<PlayerWeaponController>(scene).Single();
        ThirdPersonCameraController cameraRig = All<ThirdPersonCameraController>(scene).Single();
        GameUIController[] found = All<GameUIController>(scene);
        if (found.Length > 1) throw new InvalidOperationException("ZombieMap has multiple GameUIControllers.");
        GameObject root;
        if (found.Length == 1) root = found[0].gameObject;
        else
        {
            // Assets are created through the Editor; never patch Canvas/prefab YAML manually.
            root = Phase4UIFactory.Create();
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create Phase 4 UI");
        }
        UnityEngine.EventSystems.EventSystem[] systems = All<UnityEngine.EventSystems.EventSystem>(scene);
        if (systems.Length > 1) throw new InvalidOperationException("Keep exactly one EventSystem in ZombieMap.");
        UnityEngine.EventSystems.EventSystem system;
        if (systems.Length == 0)
        {
            GameObject go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(go, scene);
            system = go.GetComponent<UnityEngine.EventSystems.EventSystem>();
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }
        else system = systems[0];
        if (system.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null)
            throw new InvalidOperationException("This project uses legacy input. Configure one StandaloneInputModule on the EventSystem.");

        GameUIController screens = root.GetComponent<GameUIController>();
        PlayerHudPresenter hud = root.GetComponentInChildren<PlayerHudPresenter>(true);
        GamePanelsPresenter panels = root.GetComponentInChildren<GamePanelsPresenter>(true);
        if (hud == null || panels == null) throw new InvalidOperationException("Existing GameUI is incomplete. Repair its presenter references before rerunning.");
        Phase4UIFactory.Ref(screens, "health", player.GetComponent<PlayerHealth>());
        Phase4UIFactory.Ref(screens, "aim", player.GetComponent<PlayerAimController>());
        Phase4UIFactory.Ref(screens, "cameraRig", cameraRig);
        Phase4UIFactory.Ref(hud, "health", player.GetComponent<PlayerHealth>());
        Phase4UIFactory.Ref(hud, "weapons", player);
        Phase4UIFactory.Ref(hud, "aim", player.GetComponent<PlayerAimController>());
        Phase4UIFactory.Ref(hud, "screens", screens);
        Phase4UIFactory.Ref(panels, "screens", screens);
        Phase4UIFactory.Ref(panels, "eventSystem", system);
        SavePrefab(root, screens, hud, panels);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save ZombieMap.");
        AssetDatabase.SaveAssets();
        Validate();
        Selection.activeGameObject = root;
        Debug.Log("Phase 4 HUD and panels saved. Escape pauses; death shows Game Over; Victory is an explicit preview hook.");
        if (ownsBatchProcess) EditorApplication.Exit(0);
    }

    private static void WaitForText()
    {
        bool timedOut = EditorApplication.timeSinceStartup > textDeadline;
        if (!timedOut && (!Phase4UIFactory.TextResourcesReady || EditorApplication.isUpdating)) return;
        EditorApplication.update -= WaitForText;
        waitingForText = false;
        try
        {
            if (timedOut) throw new InvalidOperationException("TMP Essentials import timed out.");
            SetUp();
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (ownsBatchProcess) EditorApplication.Exit(1);
        }
    }

    private static void SavePrefab(GameObject root, GameUIController screens, PlayerHudPresenter hud, GamePanelsPresenter panels)
    {
        Phase2AnimationAssets.Folder("Assets/Prefabs/UI");
        UnityEngine.Object[] owners = { screens, screens, screens, hud, hud, hud, panels };
        string[] fields = { "health", "aim", "cameraRig", "health", "weapons", "aim", "eventSystem" };
        UnityEngine.Object[] values = new UnityEngine.Object[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            values[i] = new SerializedObject(owners[i]).FindProperty(fields[i]).objectReferenceValue;
            Phase4UIFactory.Ref(owners[i], fields[i], null);
        }
        try
        {
            if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null) throw new InvalidOperationException("Could not save GameUI.prefab.");
        }
        finally
        {
            for (int i = 0; i < fields.Length; i++) Phase4UIFactory.Ref(owners[i], fields[i], values[i]);
        }
    }

    [MenuItem("Tools/Dead city/Phase 4/Validate HUD and Panels")]
    public static void Validate()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameUIController screens = All<GameUIController>(scene).Single();
        PlayerHudPresenter hud = screens.GetComponentInChildren<PlayerHudPresenter>(true);
        GamePanelsPresenter panels = screens.GetComponentInChildren<GamePanelsPresenter>(true);
        Require(hud != null && panels != null, "Both presenters must exist.");
        References(screens, "health", "aim", "cameraRig");
        References(hud, "health", "weapons", "aim", "screens", "crosshair", "healthFill", "healthText", "weaponText", "magazineText", "reserveText", "reloadingIndicator", "hudGroup");
        References(panels, "screens", "eventSystem", "pausePanel", "gameOverPanel", "victoryPanel", "resumeButton", "pauseRestartButton", "gameOverRestartButton", "victoryRestartButton");
        Require(All<UnityEngine.EventSystems.EventSystem>(scene).Length == 1, "Exactly one EventSystem is required.");
        foreach (Canvas canvas in screens.GetComponentsInChildren<Canvas>(true))
        {
            UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay && scaler != null, "Overlay Canvas and CanvasScaler required.");
            Require(scaler.uiScaleMode == UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                scaler.referenceResolution == new Vector2(1920, 1080), "Use the 1920x1080 scalable layout.");
            Require(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null, "Missing GraphicRaycaster.");
        }
        foreach (TMPro.TMP_Text label in screens.GetComponentsInChildren<TMPro.TMP_Text>(true))
            Require(label.font != null && !label.raycastTarget, "Labels need a font and must not intercept pointer clicks.");
        Require(!hud.GetComponent<CanvasGroup>().blocksRaycasts, "HUD must not block gameplay input.");
        Debug.Log("PHASE4_STRUCTURE_PASS: HUD bindings, panels, text fonts, EventSystem and Canvas scaling.");
    }

    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray();

    private static void References(UnityEngine.Object owner, params string[] names)
    {
        SerializedObject so = new SerializedObject(owner);
        foreach (string name in names) Require(so.FindProperty(name)?.objectReferenceValue != null, owner.name + ": missing " + name);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Phase 4 validation: " + message);
    }
}
