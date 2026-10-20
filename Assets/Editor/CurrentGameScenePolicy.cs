using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

/// <summary>One supported game flow; rejects accidentally restored prototype maps.</summary>
public sealed class CurrentGameScenePolicy : IPreprocessBuildWithReport, IProcessSceneWithReport
{
    public int callbackOrder => -1000;
    public static void RejectLegacySceneGeneration()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ExistingCityEnhancer.ScenePath) != null)
            throw new InvalidOperationException("Prototype scene generation is retired. Open Assets/Scenes/Opening.unity or the current EnhancedCity/ZombieMap.unity.");
    }
    public void OnPreprocessBuild(BuildReport report)
    {
        string[] expected = { OpeningVideoSetup.ScenePath, "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Loadout.unity", ExistingCityEnhancer.ScenePath };
        if (!EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).SequenceEqual(expected))
            throw new BuildFailedException("Incorrect game scene list. Use Tools > Dead Zone > Use current game scene flow.");
    }
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null) return;
        if (scene.name == "ZombieMap" && scene.path != ExistingCityEnhancer.ScenePath || scene.path.Contains("/ArtBackups/"))
            throw new BuildFailedException("Obsolete scene rejected: " + scene.path);
    }
}
