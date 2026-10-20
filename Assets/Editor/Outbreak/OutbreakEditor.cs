using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Shared native asset helpers. Builders never hand-edit Unity YAML.</summary>
public static class OutbreakEditor
{
    public const string Scene = ExistingCityEnhancer.ScenePath;
    public static void Open() => EditorSceneManager.OpenScene(Scene);
    public static void Save(string phase)
    {
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("PHASE_" + phase + "_PASS");
    }

    public static T One<T>()
        where T : UnityEngine.Object => UnityEngine.Object.FindAnyObjectByType<T>();
    public static T Add<T>(GameObject go)
        where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    public static Transform Group(string name, Transform parent = null)
    {
        var found = parent != null ? parent.Find(name) : GameObject.Find(name)?.transform;
        if (found != null)
            return found;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    public static Material Material(string name, Color color)
    {
        Phase2AnimationAssets.Folder("Assets/Materials/Outbreak");
        string path = "Assets/Materials/Outbreak/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
            return mat;
        mat = new Material(Shader.Find("Standard"))
        {
            name = name,
            color = color
        };
        mat.SetFloat("_Glossiness", 0.15f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    public static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.layer = 8;
        return go;
    }

    public static void Set(UnityEngine.Object owner, string name, UnityEngine.Object value) => Phase4UIFactory.Ref(owner, name, value);
    public static void Values(UnityEngine.Object owner, Action<SerializedObject> edit)
    {
        var so = new SerializedObject(owner);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(owner);
    }

    public static void Layer(int id, string name)
    {
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        tags.FindProperty("layers").GetArrayElementAtIndex(id).stringValue = name;
        tags.ApplyModifiedProperties();
    }

    public static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
