using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Update the game's visible name without rebuilding its scenes or layout.</summary>
public static class GameBrandingSetup
{
    public const string ProductName = "Dead city";
    const string Backup = "Backups/DeadCityRename-20261020";

    [MenuItem("Tools/Dead city/Apply game name")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before renaming the game.");
        BackupFile("ProjectSettings/ProjectSettings.asset");
        foreach (var entry in EditorBuildSettings.scenes)
            if (entry.enabled) BackupFile(entry.path);
        const string prefabPath = "Assets/Prefabs/UI/GameUI.prefab";
        BackupFile(prefabPath);
        // Preserve any current scene edits before visiting the other game screens.
        if (!EditorSceneManager.SaveOpenScenes())
            throw new InvalidOperationException("Could not save the currently open scenes.");
        PlayerSettings.productName = ProductName;
        int labels = 0;
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled) continue;
            var scene = SceneManager.GetSceneByPath(entry.path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);
            try
            {
                int changed = 0;
                foreach (var root in scene.GetRootGameObjects()) changed += RenameLabels(root);
                if (changed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                        throw new InvalidOperationException("Could not save " + entry.path);
                }
                labels += changed;
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            labels += RenameLabels(prefab);
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        EditorUserBuildSettings.SetBuildLocation(BuildTarget.StandaloneWindows64, "Builds/Windows/DeadCity.exe");
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/DeadCityRename.txt",
            "Product name: " + PlayerSettings.productName + "\nUpdated labels: " + labels +
            "\nWindows executable: Builds/Windows/DeadCity.exe\n");
        Debug.Log("DEAD_CITY_NAME_APPLIED: " + labels + " UI labels updated.");
    }

    static string Rename(string value) => value.Replace("ZOMBIE OUTBREAK", "DEAD CITY")
        .Replace("Zombie Outbreak", ProductName).Replace("zombie outbreak", ProductName);

    static int RenameLabels(GameObject root)
    {
        int count = 0;
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            string value = Rename(label.text ?? "");
            if (value == label.text) continue;
            label.text = value;
            EditorUtility.SetDirty(label);
            if (PrefabUtility.IsPartOfPrefabInstance(label))
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            count++;
        }
        foreach (var label in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            string value = Rename(label.text ?? "");
            if (value == label.text) continue;
            label.text = value;
            EditorUtility.SetDirty(label);
            if (PrefabUtility.IsPartOfPrefabInstance(label))
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            count++;
        }
        return count;
    }

    static void BackupFile(string path)
    {
        string destination = Backup + "/" + path;
        if (File.Exists(destination)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(path, destination);
    }
}
