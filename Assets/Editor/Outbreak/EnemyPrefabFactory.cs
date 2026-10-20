using UnityEditor;
using UnityEngine;

public static class EnemyPrefabFactory
{
    public static void Create<T>(string name, float health, Color color, float scale = 1)
        where T : Component
    {
        string path = "Assets/Prefabs/Enemies/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Phase7Setup.BasePrefab));
        go.name = name;
        go.AddComponent<T>();
        OutbreakEditor.Values(go.GetComponent<EnemyHealth>(), s => s.FindProperty("maxHealth").floatValue = health);
        var visual = go.transform.Find("Visual");
        visual.localScale = Vector3.one * scale;
        var mat = OutbreakEditor.Material(name + "Skin", color);
        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterial = mat;
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }
}
