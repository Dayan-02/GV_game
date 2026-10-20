using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Upgrades ZombieMap in place; creates the Phase 1 greybox only when the scene does not exist.</summary>
public static class Phase2Setup
{
    private const string ScenePath = ExistingCityEnhancer.ScenePath;

    [MenuItem("Tools/Zombie Outbreak/Phase 2/Set Up ZombieMap")]
    public static void SetUpZombieMap()
    {
        CurrentGameScenePolicy.RejectLegacySceneGeneration();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before setting up Phase 2.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Build and validate reusable assets before changing the player's scene.
        AnimatorController controller = Phase2AnimationAssets.EnsureAssets();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Phase1SceneBuilder.BuildZombieMap();
                scene = SceneManager.GetActiveScene();
            }
            else scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        SceneManager.SetActiveScene(scene);
        ThirdPersonPlayerController[] players = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ThirdPersonPlayerController>(true)).ToArray();
        if (players.Length != 1)
            throw new InvalidOperationException("ZombieMap must have exactly one ThirdPersonPlayerController.");
        ThirdPersonPlayerController movement = players[0];
        GameObject player = movement.gameObject;
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health == null) health = Undo.AddComponent<PlayerHealth>(player);

        Animator animator = player.GetComponentsInChildren<Animator>(true)
            .FirstOrDefault(a => a.avatar != null && a.avatar.isValid && a.avatar.isHuman);
        if (animator == null)
        {
            Animator unrelatedAnimator = player.GetComponentInChildren<Animator>(true);
            if (unrelatedAnimator != null)
                throw new InvalidOperationException("Existing Animator has no valid Humanoid Avatar. Configure its Rig before rerunning setup.");
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(Phase2AnimationAssets.ModelPath), player.transform);
            model.name = "Humanoid_Phase2";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            animator = model.GetComponent<Animator>();
            Transform oldGreybox = player.transform.Find("Humanoid_Greybox");
            if (oldGreybox != null) oldGreybox.gameObject.SetActive(false);
        }
        animator.gameObject.SetActive(true);
        animator.enabled = true;
        AnimatorOverrideController overrides = animator.runtimeAnimatorController as AnimatorOverrideController;
        if (overrides == null || overrides.runtimeAnimatorController != controller)
            animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();

        PlayerAimController aim = player.GetComponent<PlayerAimController>();
        if (aim == null) aim = Undo.AddComponent<PlayerAimController>(player);
        PlayerAnimatorDriver driver = player.GetComponent<PlayerAnimatorDriver>();
        if (driver == null) driver = Undo.AddComponent<PlayerAnimatorDriver>(player);
        Transform target = Child(player.transform, "CameraTarget", new Vector3(0f, 1.48f, 0f));
        Transform aimTarget = Child(target, "AimTarget", new Vector3(0f, 0f, 20f));
        Transform ground = Child(player.transform, "GroundCheck", new Vector3(0f, 0.08f, 0f));

        Camera mainCamera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(camera => camera.CompareTag("MainCamera"));
        if (mainCamera == null)
            throw new InvalidOperationException("ZombieMap needs a Camera tagged MainCamera.");
        ThirdPersonCameraController cameraRig = mainCamera.GetComponent<ThirdPersonCameraController>();
        if (cameraRig == null) cameraRig = Undo.AddComponent<ThirdPersonCameraController>(mainCamera.gameObject);
        mainCamera.nearClipPlane = 0.1f;

        Transform modes = Child(mainCamera.transform, "Camera Modes", Vector3.zero);
        ThirdPersonCameraMode exploration = Mode(modes, "Exploration Camera", 4.5f, new Vector2(0.2f, 0.05f), 60f, 1f);
        ThirdPersonCameraMode aiming = Mode(modes, "Aim Camera", 2.25f, new Vector2(0.6f, 0.08f), 45f, 0.65f);
        Reference(movement, "cameraTransform", mainCamera.transform);
        Reference(movement, "groundCheck", ground);
        Reference(movement, "cameraRig", cameraRig);
        Reference(movement, "aim", aim);
        Reference(movement, "health", health);
        Reference(aim, "health", health);
        Reference(driver, "animator", animator);
        Reference(driver, "movement", movement);
        Reference(driver, "aim", aim);
        Reference(driver, "health", health);
        Reference(cameraRig, "cameraTarget", target);
        Reference(cameraRig, "aimTarget", aimTarget);
        Reference(cameraRig, "aim", aim);
        Reference(cameraRig, "explorationCamera", exploration);
        Reference(cameraRig, "aimCamera", aiming);

        // Keep the existing socket object; only reparent the original root-level placeholder.
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform socket = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "WeaponSocket");
        if (socket == null) socket = Child(hand, "WeaponSocket", new Vector3(0f, 0f, 0.08f));
        else if (socket.parent == player.transform && hand != null)
        {
            socket.SetParent(hand, false);
            socket.localPosition = new Vector3(0f, 0f, 0.08f);
            socket.localRotation = Quaternion.identity;
        }

        // Save a prefab without scene-only camera references.
        Transform previousCamera = new SerializedObject(movement).FindProperty("cameraTransform").objectReferenceValue as Transform;
        Reference(movement, "cameraTransform", null);
        Reference(movement, "cameraRig", null);
        try { PrefabUtility.SaveAsPrefabAsset(player, "Assets/Prefabs/Player/PlayerPhase2.prefab"); }
        finally
        {
            Reference(movement, "cameraTransform", previousCamera);
            Reference(movement, "cameraRig", cameraRig);
        }

        EditorSettings.serializationMode = SerializationMode.ForceText;
        UnityEditor.VersionControlSettings.mode = "Visible Meta Files";
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save ZombieMap.");
        AssetDatabase.SaveAssets();
        ValidateSetup();
        Selection.activeGameObject = player;
        Debug.Log("Phase 2 configured. Hold right mouse to aim. No firing gameplay is connected.");
    }

    private static ThirdPersonCameraMode Mode(Transform parent, string name, float distance, Vector2 offset, float fov, float sensitivity)
    {
        Transform transform = parent.Find(name);
        if (transform != null && transform.TryGetComponent(out ThirdPersonCameraMode existing)) return existing;
        if (transform == null) transform = Child(parent, name, Vector3.zero);
        ThirdPersonCameraMode mode = transform.gameObject.AddComponent<ThirdPersonCameraMode>();
        SerializedObject data = new SerializedObject(mode);
        data.FindProperty("distance").floatValue = distance;
        data.FindProperty("shoulderOffset").vector2Value = offset;
        data.FindProperty("fieldOfView").floatValue = fov;
        data.FindProperty("sensitivityMultiplier").floatValue = sensitivity;
        data.ApplyModifiedPropertiesWithoutUndo();
        return mode;
    }

    private static Transform Child(Transform parent, string name, Vector3 position)
    {
        if (parent == null) throw new InvalidOperationException("Missing required parent for " + name);
        Transform existing = parent.Find(name);
        if (existing != null) return existing;
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        child.localPosition = position;
        return child;
    }

    private static void Reference(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty field = data.FindProperty(name);
        if (field == null) throw new InvalidOperationException(target.name + " has no serialized property " + name);
        field.objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Zombie Outbreak/Phase 2/Validate Active Scene")]
    public static void ValidateSetup()
    {
        Scene scene = SceneManager.GetActiveScene();
        ThirdPersonPlayerController movement = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ThirdPersonPlayerController>()).Single();
        Animator animator = movement.GetComponentInChildren<Animator>();
        Require(animator != null && animator.avatar != null && animator.avatar.isHuman && animator.avatar.isValid,
            "Player needs a valid Humanoid Avatar.");
        Require(!animator.applyRootMotion, "Apply Root Motion must be off.");
        Require(animator.GetBoneTransform(HumanBodyBones.RightHand) != null, "Right hand mapping is missing.");
        RuntimeAnimatorController runtimeController = animator.runtimeAnimatorController;
        AnimatorOverrideController overrides = runtimeController as AnimatorOverrideController;
        AnimatorController controller = (overrides != null ? overrides.runtimeAnimatorController : runtimeController) as AnimatorController;
        Require(controller != null && controller.layers.Length == 2, "Expected Base Layer and Upper Body Actions.");
        string[] parameters =
        {
            "Speed", "Horizontal", "Vertical", "IsAiming", "IsGrounded", "IsSprinting",
            "VerticalVelocity", "IsDead", "Fire", "Reload", "Hit", "Death"
        };
        foreach (string parameter in parameters)
            Require(controller.parameters.Any(p => p.name == parameter), "Missing parameter: " + parameter);
        foreach (AnimationClip clip in runtimeController.animationClips.Distinct())
            Require(clip != null && !clip.empty && clip.humanMotion, "Empty or non-Humanoid clip: " + (clip != null ? clip.name : "null"));
        Require(runtimeController.animationClips.Distinct().Count() >= 18, "Expected all 18 locomotion and action clips.");

        ThirdPersonCameraController cameraRig = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ThirdPersonCameraController>()).Single();
        foreach (string reference in new[] { "cameraTarget", "aimTarget", "aim", "explorationCamera", "aimCamera" })
            Require(new SerializedObject(cameraRig).FindProperty(reference).objectReferenceValue != null,
                "Camera reference is missing: " + reference);
        foreach (string reference in new[] { "cameraTransform", "groundCheck", "cameraRig", "aim", "health" })
            Require(new SerializedObject(movement).FindProperty(reference).objectReferenceValue != null,
                "Movement reference is missing: " + reference);
        Camera[] activeCameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>())
            .Where(c => c.enabled).ToArray();
        Require(activeCameras.Length == 1, "Use one output Camera; camera modes are virtual poses.");
        Debug.Log("Phase 2 structure validation passed: Avatar, clips, parameters, camera and movement references.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Phase 2 validation: " + message);
    }
}
