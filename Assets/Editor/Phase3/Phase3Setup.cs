using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Phase3Setup
{
    private const string ScenePath = ExistingCityEnhancer.ScenePath;

    [MenuItem("Tools/Dead city/Phase 3/Set Up ZombieMap")]
    public static void SetUpZombieMap()
    {
        CurrentGameScenePolicy.RejectLegacySceneGeneration();
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Phase2Setup.SetUpZombieMap();
            else EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            scene = SceneManager.GetSceneByPath(ScenePath);
        }
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("ZombieMap setup was cancelled or failed.");
        SceneManager.SetActiveScene(scene);
        ThirdPersonPlayerController movement = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ThirdPersonPlayerController>(true)).Single();
        GameObject player = movement.gameObject;
        if (player.GetComponent<PlayerAimController>() == null || player.GetComponent<PlayerAnimatorDriver>() == null)
        {
            Phase2Setup.SetUpZombieMap();
            movement = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ThirdPersonPlayerController>()).Single();
            player = movement.gameObject;
        }
        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            throw new InvalidOperationException("Phase 2 requires a valid Humanoid Animator before Phase 3 setup.");
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform socket = hand != null ? hand.Find("WeaponSocket") : null;
        if (socket == null) throw new InvalidOperationException("Run Phase 2 setup to create RightHand/WeaponSocket.");
        Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>())
            .Single(c => c.CompareTag("MainCamera"));
        WeaponData[] presets = Phase3WeaponAssets.EnsureAssets();
        PlayerWeaponController weapons = player.GetComponent<PlayerWeaponController>();
        if (weapons == null) weapons = Undo.AddComponent<PlayerWeaponController>(player);
        SerializedObject so = new SerializedObject(weapons);
        so.FindProperty("viewCamera").objectReferenceValue = camera;
        so.FindProperty("cameraRig").objectReferenceValue = camera.GetComponent<ThirdPersonCameraController>();
        so.FindProperty("aim").objectReferenceValue = player.GetComponent<PlayerAimController>();
        so.FindProperty("health").objectReferenceValue = player.GetComponent<PlayerHealth>();
        so.FindProperty("animationDriver").objectReferenceValue = player.GetComponent<PlayerAnimatorDriver>();
        so.FindProperty("weaponSocket").objectReferenceValue = socket;
        so.FindProperty("shotOrigin").objectReferenceValue = player.transform.Find("CameraTarget");
        so.FindProperty("aimTarget").objectReferenceValue = player.transform.Find("CameraTarget/AimTarget");
        SerializedProperty list = so.FindProperty("loadout");
        if (list.arraySize == 0)
        {
            list.arraySize = presets.Length;
            for (int i = 0; i < presets.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = presets[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AddTargets(scene);
        SavePlayerPrefab(player, movement, weapons);
        EditorSettings.serializationMode = SerializationMode.ForceText;
        UnityEditor.VersionControlSettings.mode = "Visible Meta Files";
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save ZombieMap.");
        AssetDatabase.SaveAssets();
        ValidateActiveScene();
        Selection.activeGameObject = player;
        Debug.Log("Phase 3 configured: RMB aim, LMB fire, R reload, 1-4 or wheel switch. Target health prints in Console.");
    }

    private static void SavePlayerPrefab(GameObject player, ThirdPersonPlayerController movement, PlayerWeaponController weapons)
    {
        // Scene camera references must not be serialized into a reusable prefab.
        SerializedObject move = new SerializedObject(movement);
        SerializedObject shoot = new SerializedObject(weapons);
        string[] moveFields = { "cameraTransform", "cameraRig" };
        string[] shootFields = { "viewCamera", "cameraRig" };
        UnityEngine.Object[] refs = moveFields.Select(f => move.FindProperty(f).objectReferenceValue)
            .Concat(shootFields.Select(f => shoot.FindProperty(f).objectReferenceValue)).ToArray();
        foreach (string field in moveFields) move.FindProperty(field).objectReferenceValue = null;
        foreach (string field in shootFields) shoot.FindProperty(field).objectReferenceValue = null;
        move.ApplyModifiedPropertiesWithoutUndo();
        shoot.ApplyModifiedPropertiesWithoutUndo();
        try
        {
            if (PrefabUtility.SaveAsPrefabAsset(player, "Assets/Prefabs/Player/PlayerPhase3.prefab") == null)
                throw new InvalidOperationException("Could not save PlayerPhase3.prefab.");
        }
        finally
        {
            for (int i = 0; i < moveFields.Length; i++) move.FindProperty(moveFields[i]).objectReferenceValue = refs[i];
            for (int i = 0; i < shootFields.Length; i++) shoot.FindProperty(shootFields[i]).objectReferenceValue = refs[i + 2];
            move.ApplyModifiedPropertiesWithoutUndo();
            shoot.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void AddTargets(Scene scene)
    {
        if (scene.GetRootGameObjects().Any(go => go.name == "Phase3 Practice Range")) return;
        GameObject group = new GameObject("Phase3 Practice Range");
        SceneManager.MoveGameObjectToScene(group, scene);
        for (int i = 0; i < 4; i++)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Target " + (i + 1) + " (200 HP)";
            target.transform.SetParent(group.transform, false);
            target.transform.position = new Vector3(-6f + i * 4f, 1.35f, 12f);
            target.transform.localScale = new Vector3(1.4f, 2.2f, 0.25f);
            target.AddComponent<ShootingTarget>();
        }
    }

    [MenuItem("Tools/Dead city/Phase 3/Validate Active Scene")]
    public static void ValidateActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        PlayerWeaponController weapons = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlayerWeaponController>()).Single();
        SerializedObject so = new SerializedObject(weapons);
        foreach (string field in new[] { "viewCamera", "cameraRig", "aim", "health", "animationDriver", "weaponSocket", "shotOrigin", "aimTarget" })
            Require(so.FindProperty(field).objectReferenceValue != null, "Missing player reference " + field);
        Transform socket = so.FindProperty("weaponSocket").objectReferenceValue as Transform;
        Animator animator = weapons.GetComponentInChildren<Animator>();
        Require(socket.parent == animator.GetBoneTransform(HumanBodyBones.RightHand), "WeaponSocket must be under RightHand.");
        SerializedProperty list = so.FindProperty("loadout");
        Require(list.arraySize == 4, "Expected four weapon slots.");
        WeaponCategory[] categories = new WeaponCategory[4];
        for (int i = 0; i < list.arraySize; i++)
        {
            WeaponData data = list.GetArrayElementAtIndex(i).objectReferenceValue as WeaponData;
            Require(data != null && data.WeaponPrefab != null && data.WeaponPrefab.Muzzle != null, "Invalid weapon slot " + i);
            Require(data.WeaponPrefab.GetComponentsInChildren<Collider>(true).Length == 0, "Weapon view prefabs must have no colliders.");
            categories[i] = data.Category;
        }
        Require(categories.Distinct().Count() == 4 && !categories.Contains(WeaponCategory.None), "Need Rifle, SMG, Shotgun and Pistol categories.");
        Require(so.FindProperty("hitLayers").intValue != 0, "Hit Layers must include environment and targets.");
        Debug.Log("Phase 3 structure validation passed: four weapons, muzzle references, socket and player wiring.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Phase 3 validation: " + message);
    }
}
