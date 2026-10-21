using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Phase18Setup
{
    public static void FinalizeAndBuild()
    {
        Phase17Setup.Build();
        Build();
        BuildWindows();
    }

    [MenuItem("Dead city/Phase 18 - Assessment display and validation")]
    public static void Build()
    {
        OutbreakEditor.Open();
        TeamArtProjectSetup.EnsureSetup();
        Phase4UIFactory.PrepareResources();
        var hud = GameObject.Find("GameUI/HUDCanvas").transform;
        var debug = OutbreakEditor.Add<AIDebugPresenter>(hud.gameObject);
        var panel = hud.Find("AIDebugPanel");
        if (panel == null)
        {
            panel = Phase4UIFactory.Card(hud, "AIDebugPanel", new Vector2(640, 660), new Vector2(0, 1), new Vector2(48, -48));
            var output = Phase4UIFactory.Label(panel, "Output", "", 22, Color.white, new Vector2(22, 22), new Vector2(596, 616));
            output.textWrappingMode = TextWrappingModes.Normal;
            OutbreakEditor.Set(debug, "output", output);
            panel.gameObject.SetActive(false);
        }

        panel.Find("Output").GetComponent<TMP_Text>().alignment = TextAlignmentOptions.TopLeft;
        OutbreakEditor.Set(debug, "panel", panel.gameObject);
        OutbreakEditor.Set(debug, "world", OutbreakEditor.One<EnemyWorld>());
        OutbreakEditor.Set(debug, "viewCamera", Camera.main);
        Validate();
        BenchmarkPaths();
        OutbreakEditor.Save("18");
    }

    public static void Validate()
    {
        Phase5Setup.Validate();
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                OutbreakEditor.Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "Missing script on " + t.name);
        var types = new[]
        {
            typeof(HunterBrain),
            typeof(RunnerBrain),
            typeof(AmbusherBrain),
            typeof(EliteBrain)
        };
        var names = new[]
        {
            "Hunter",
            "Runner",
            "Ambusher",
            "Elite"
        };
        for (int i = 0; i < 4; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + names[i] + ".prefab");
            OutbreakEditor.Require(prefab != null && prefab.GetComponent(types[i]) != null && prefab.GetComponent<EnemyHealth>() != null && prefab.GetComponent<EnemyMovement>() != null && prefab.GetComponent<EnemyCombat>() != null, "Shared composition + distinct decision script: " + names[i]);
            OutbreakEditor.Require(prefab.GetComponent<Rigidbody>() == null, "Do not combine dynamic Rigidbody with agent");
        }

        var enemy = AssetDatabase.LoadAssetAtPath<AnimatorController>(Phase7Setup.ControllerPath);
        foreach (var state in enemy.layers[0].stateMachine.states)
            OutbreakEditor.Require(state.state.motion != null, "Missing enemy motion");
        var player = AssetDatabase.LoadAssetAtPath<AnimatorController>(Phase2AnimationAssets.ControllerPath);
        OutbreakEditor.Require(player.parameters.Any(p => p.name == "ReloadSpeed"), "Reload speed parameter persisted");
        var sceneFlow = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)).ToArray();
        OutbreakEditor.Require(sceneFlow.SequenceEqual(new[] { "Opening", "MainMenu", "Loadout", "ZombieMap" }) ||
            sceneFlow.SequenceEqual(new[] { "MainMenu", "Loadout", "ZombieMap" }), "Correct scene flow in build");
        foreach (var material in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" }).Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))))
            OutbreakEditor.Require(material.shader != null && material.shader.isSupported, "Unsupported material shader: " + material.name);
    }

    private static void BenchmarkPaths()
    {
        var graph = OutbreakEditor.One<HunterNavigationGraph>();
        graph.Build();
        var entrances = Object.FindObjectsByType<MapMarker>().Where(m => m.kind == MapMarkerKind.ZombieEntrance).ToArray();
        var path = new List<Vector3>();
        var timer = new Stopwatch();
        var samples = new List<double>();
        int failures = 0;
        for (int repeat = 0; repeat < 10; repeat++)
            foreach (var entrance in entrances)
            {
                timer.Restart();
                bool found = graph.FindPath(entrance.transform.position, OutbreakEditor.One<PlayerHealth>().transform.position, path);
                timer.Stop();
                samples.Add(timer.Elapsed.TotalMilliseconds);
                if (!found)
                    failures++;
            }

        samples.Sort();
        Directory.CreateDirectory("Logs");
        string report = "A* microbenchmark / Unity Editor / " + Application.unityVersion + "\nSearches: " + samples.Count + "\nFailures: " + failures + "\nMean ms: " + samples.Average().ToString("F3") + "\np95 ms: " + samples[(int)(samples.Count * .95) - 1].ToString("F3") + "\nMax ms: " + samples.Last().ToString("F3") + "\nThis measures path searches only, not whole-game FPS. Hardware, Editor overhead and cache state affect results.\n";
        File.WriteAllText("Logs/NavigationBenchmark.txt", report);
        UnityEngine.Debug.Log(report);
        OutbreakEditor.Require(failures == 0, "All benchmark paths must be complete");
    }

    [MenuItem("Dead city/Build Windows Player")]
    public static void BuildWindows()
    {
        // Preserve the latest authored city when rebuilding from the normal menu.
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ExistingCityEnhancer.ScenePath) != null)
        {
            ReferenceCityBuild.Build();
            return;
        }
        OutbreakEditor.Open();
        Validate();
        Directory.CreateDirectory("Builds/Windows");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(), locationPathName = "Builds/Windows/DeadCity.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/WindowsBuildSummary.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings + "\nSize: " + report.summary.totalSize + "\nTime: " + report.summary.totalTime);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Windows build failed: " + report.summary.result);
        UnityEngine.Debug.Log("WINDOWS_BUILD_PASS: Builds/Windows/DeadCity.exe");
    }
}
