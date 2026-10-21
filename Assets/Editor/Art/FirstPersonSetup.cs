using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Converts the enhanced gameplay scene, using the installed Survivalist FPS meshes.</summary>
public static class FirstPersonSetup
{
    public const string ArmsPath = "Assets/Prefabs/Player/FirstPersonArms.prefab";
    public const string PlayerPath = "Assets/Prefabs/Player/PlayerFirstPerson.prefab";
    private const int ViewLayer = 30;

    [MenuItem("Dead city/First Person/Apply to Enhanced City")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring first person.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ExistingCityEnhancer.ScenePath)
            throw new InvalidOperationException("Open the enhanced ZombieMap before applying first person.");
        var player = Object.FindAnyObjectByType<ThirdPersonPlayerController>();
        var camera = Camera.main;
        if (player == null || camera == null) throw new InvalidOperationException("Player and Main Camera are required.");
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layer = tags.FindProperty("layers").GetArrayElementAtIndex(ViewLayer);
        if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != "FirstPerson")
            throw new InvalidOperationException("Layer 30 is already in use.");
        layer.stringValue = "FirstPerson"; tags.ApplyModifiedPropertiesWithoutUndo();

        var visual = player.transform.Find("SurvivalistVisual");
        if (visual == null) throw new InvalidOperationException("The Survivalist player visual is required.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ArmsPath) == null) CreateArms(visual.gameObject);
        var oldView = player.transform.Find("FirstPersonView");
        if (oldView != null) Object.DestroyImmediate(oldView.gameObject);
        var root = Child("FirstPersonView", player.transform);
        var socket = Child("WeaponSocket", root);
        var armsObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ArmsPath), root);
        armsObject.name = "FirstPersonArms";
        armsObject.transform.localPosition = new Vector3(0, -1.68f, .12f);
        armsObject.transform.localRotation = Quaternion.identity;
        var arms = armsObject.GetComponent<FirstPersonArms>();
        var viewCamera = Child("WeaponCamera", root).gameObject.AddComponent<Camera>();
        viewCamera.clearFlags = CameraClearFlags.Depth;
        viewCamera.cullingMask = 1 << ViewLayer;
        viewCamera.nearClipPlane = .01f; viewCamera.farClipPlane = 3f;
        viewCamera.fieldOfView = 75; viewCamera.depth = camera.depth + 1;
        viewCamera.allowHDR = false; viewCamera.useOcclusionCulling = false;
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ViewLayer;
        var rig = player.GetComponent<FirstPersonWeaponRig>() ?? player.gameObject.AddComponent<FirstPersonWeaponRig>();
        var so = new SerializedObject(rig);
        so.FindProperty("worldCamera").objectReferenceValue = camera;
        so.FindProperty("weaponCamera").objectReferenceValue = viewCamera;
        so.FindProperty("viewRoot").objectReferenceValue = root;
        so.FindProperty("weaponSocket").objectReferenceValue = socket;
        so.FindProperty("arms").objectReferenceValue = arms;
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        var list = so.FindProperty("bodyRenderers"); list.arraySize = renderers.Length;
        for (int i=0; i<renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        var weapons = new SerializedObject(player.GetComponent<PlayerWeaponController>());
        weapons.FindProperty("weaponSocket").objectReferenceValue = socket;
        weapons.ApplyModifiedPropertiesWithoutUndo();
        foreach (var grip in visual.GetComponentsInChildren<SupportHandGrip>(true)) grip.enabled = false;
        var stance = new SerializedObject(player.GetComponent<PlayerStanceController>());
        stance.FindProperty("crouchedHeight").floatValue = 1.2f; stance.ApplyModifiedPropertiesWithoutUndo();
        var cameraSettings = new SerializedObject(camera.GetComponent<ThirdPersonCameraController>());
        cameraSettings.FindProperty("firstPerson").boolValue = true;
        cameraSettings.FindProperty("firstPersonFieldOfView").floatValue = 75;
        cameraSettings.FindProperty("firstPersonAimFieldOfView").floatValue = 55;
        cameraSettings.FindProperty("startingPitch").floatValue = 0;
        cameraSettings.FindProperty("minimumPitch").floatValue = -85;
        cameraSettings.FindProperty("maximumPitch").floatValue = 85;
        cameraSettings.ApplyModifiedPropertiesWithoutUndo();
        camera.cullingMask &= ~(1 << ViewLayer); camera.nearClipPlane = .04f; camera.fieldOfView = 75;
        camera.transform.SetPositionAndRotation(player.transform.position + Vector3.up * 1.66f, player.transform.rotation);
        root.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
        PrefabUtility.SaveAsPrefabAsset(player.gameObject, PlayerPath);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("First-person conversion saved: enhanced ZombieMap and PlayerFirstPerson prefab.");
    }

    private static Transform Child(string name, Transform parent)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
    }

    private static void CreateArms(GameObject source)
    {
        var root = Object.Instantiate(source);
        try
        {
            root.name = "FirstPersonArms"; root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
            var animator = root.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) throw new InvalidOperationException("FPS arms require the Survivalist Humanoid Avatar.");
            var leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            // Keep the existing authored carry pose, including the finger curl. No Animator writes to this rig at runtime.
            Object.DestroyImmediate(animator);
            int found = 0;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                bool keep = renderer.name == "SK_Miliary_FPS_Arms_Gloves1" || renderer.name == "SK_Miliary_Military_FPS_Shirt3";
                renderer.gameObject.SetActive(keep); renderer.enabled = keep;
                if (!keep) continue;
                for (var parent = renderer.transform.parent; parent != null && parent != root.transform; parent = parent.parent)
                    parent.gameObject.SetActive(true);
                found++;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
            if (found != 2) throw new InvalidOperationException("Dedicated FPS glove and shirt meshes were not found.");
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ViewLayer;
            var arms = root.AddComponent<FirstPersonArms>();
            var so = new SerializedObject(arms);
            SetArm(so.FindProperty("left"), leftUpper, leftLower, leftHand);
            SetArm(so.FindProperty("right"), rightUpper, rightLower, rightHand);
            so.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory(Path.GetDirectoryName(ArmsPath));
            PrefabUtility.SaveAsPrefabAsset(root, ArmsPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void SetArm(SerializedProperty arm, Transform upper, Transform lower, Transform hand)
    {
        arm.FindPropertyRelative("upper").objectReferenceValue = upper;
        arm.FindPropertyRelative("lower").objectReferenceValue = lower;
        arm.FindPropertyRelative("hand").objectReferenceValue = hand;
        arm.FindPropertyRelative("wristRotation").quaternionValue = hand.rotation;
    }
}
