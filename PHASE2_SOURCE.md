> Archived Phase 2 snapshot. Use [PHASE3_SOURCE.md](<D:/GV project/PHASE3_SOURCE.md>) for current, Unity-compiled source. Do not overwrite the current project with this older snapshot.

# Phase 2 complete C# source

Delivery snapshot. Setup, Inspector values, blend trees, hierarchy, tests and common bugs are in PHASE2_GUIDE.md. The actual .cs files are the editable source of truth. This listing includes unchanged Phase 1 dependencies used by setup.

## D:\GV project\Assets\Editor\Phase1SceneBuilder.cs

~~~csharp
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Builds the Phase 1 greybox scene and player prefab from the Unity Editor menu.</summary>
public static class Phase1SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/ZombieMap.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
    private const int IgnoreRaycastLayer = 2;

    [MenuItem("Tools/Phase 1/Build ZombieMap Greybox")]
    public static void BuildZombieMap()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSettings.serializationMode = SerializationMode.ForceText;
        EditorSettings.externalVersionControl = "Visible Meta Files";
        EnsureFolders();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "ZombieMap";

        Material groundMaterial = CreateOrLoadMaterial(
            "Assets/Materials/Greybox/Ground_Grey.mat",
            new Color(0.34f, 0.36f, 0.38f));
        Material coverMaterial = CreateOrLoadMaterial(
            "Assets/Materials/Greybox/Cover_Grey.mat",
            new Color(0.46f, 0.48f, 0.5f));
        Material playerMaterial = CreateOrLoadMaterial(
            "Assets/Materials/Greybox/Player_Grey.mat",
            new Color(0.25f, 0.42f, 0.58f));

        Transform environment = new GameObject("Environment").transform;
        CreateEnvironment(environment, groundMaterial, coverMaterial);

        GameObject player = CreatePlayer(playerMaterial);
        ThirdPersonPlayerController playerController = player.GetComponent<ThirdPersonPlayerController>();
        Transform cameraTarget = player.transform.Find("CameraTarget");
        Transform aimTarget = player.transform.Find("CameraTarget/AimTarget");

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera playerCamera = cameraObject.AddComponent<Camera>();
        playerCamera.fieldOfView = 60f;
        playerCamera.nearClipPlane = 0.1f;
        playerCamera.farClipPlane = 500f;
        cameraObject.AddComponent<AudioListener>();
        ThirdPersonCameraController cameraController = cameraObject.AddComponent<ThirdPersonCameraController>();
        SetObjectReference(cameraController, "cameraTarget", cameraTarget);
        SetObjectReference(cameraController, "aimTarget", aimTarget);

        cameraObject.transform.position = player.transform.position + new Vector3(0f, 2.2f, -4.5f);
        cameraObject.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

        GameObject lightObject = new GameObject("Directional Light");
        Light directionalLight = lightObject.AddComponent<Light>();
        directionalLight.type = LightType.Directional;
        directionalLight.intensity = 1.1f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.52f, 0.52f, 0.52f);

        PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
        SetObjectReference(playerController, "cameraTransform", cameraObject.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool sceneAlreadyInBuild = false;
        for (int i = 0; i < buildScenes.Count; i++)
        {
            if (buildScenes[i].path == ScenePath)
            {
                buildScenes[i] = new EditorBuildSettingsScene(ScenePath, true);
                sceneAlreadyInBuild = true;
                break;
            }
        }

        if (!sceneAlreadyInBuild)
        {
            buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        }

        EditorBuildSettings.scenes = buildScenes.ToArray();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Phase 1 scene created: " + ScenePath + ". Player prefab: " + PlayerPrefabPath);
    }

    private static GameObject CreatePlayer(Material playerMaterial)
    {
        GameObject player = new GameObject("Player");
        player.layer = IgnoreRaycastLayer;
        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = 1.8f;
        characterController.radius = 0.32f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.stepOffset = 0.3f;
        characterController.slopeLimit = 45f;
        characterController.skinWidth = 0.08f;

        ThirdPersonPlayerController movement = player.AddComponent<ThirdPersonPlayerController>();
        player.AddComponent<PlayerHealth>();

        Transform visuals = new GameObject("Humanoid_Greybox").transform;
        visuals.SetParent(player.transform, false);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "Torso", new Vector3(0f, 1.08f, 0f), new Vector3(0.42f, 0.52f, 0.27f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.66f, 0f), new Vector3(0.28f, 0.28f, 0.28f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "LeftArm", new Vector3(-0.34f, 1.08f, 0f), new Vector3(0.14f, 0.4f, 0.14f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "RightArm", new Vector3(0.34f, 1.08f, 0f), new Vector3(0.14f, 0.4f, 0.14f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "LeftLeg", new Vector3(-0.13f, 0.47f, 0f), new Vector3(0.16f, 0.44f, 0.16f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "RightLeg", new Vector3(0.13f, 0.47f, 0f), new Vector3(0.16f, 0.44f, 0.16f), playerMaterial);

        Transform cameraTarget = CreateChild(player.transform, "CameraTarget", new Vector3(0f, 1.48f, 0f));
        CreateChild(cameraTarget, "AimTarget", new Vector3(0f, 0f, 20f));
        CreateChild(player.transform, "WeaponSocket", new Vector3(0.34f, 1.12f, 0.22f));
        Transform groundCheck = CreateChild(player.transform, "GroundCheck", new Vector3(0f, 0.08f, 0f));
        SetObjectReference(movement, "groundCheck", groundCheck);

        SetLayerRecursively(player.transform, IgnoreRaycastLayer);
        return player;
    }

    private static void CreateEnvironment(Transform parent, Material groundMaterial, Material coverMaterial)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Ground_Greybox";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = Vector3.zero;
        floor.transform.localScale = new Vector3(5f, 1f, 5f);
        ApplyMaterial(floor, groundMaterial);

        CreateBlock(parent, "Cover_West", new Vector3(-3.5f, 0.55f, 2.5f), new Vector3(1.4f, 1.1f, 0.7f), coverMaterial);
        CreateBlock(parent, "Cover_East", new Vector3(3.5f, 0.55f, 3f), new Vector3(1.4f, 1.1f, 0.7f), coverMaterial);
        CreateBlock(parent, "Cover_North", new Vector3(0f, 0.6f, 6f), new Vector3(3f, 1.2f, 0.7f), coverMaterial);
        CreateBlock(parent, "LowBarrier_West", new Vector3(-7f, 0.4f, -3f), new Vector3(3f, 0.8f, 0.6f), coverMaterial);
        CreateBlock(parent, "LowBarrier_East", new Vector3(7f, 0.4f, -3f), new Vector3(3f, 0.8f, 0.6f), coverMaterial);
        CreateBlock(parent, "Boundary_North", new Vector3(0f, 1.5f, 20f), new Vector3(40f, 3f, 0.5f), coverMaterial);
        CreateBlock(parent, "Boundary_South", new Vector3(0f, 1.5f, -20f), new Vector3(40f, 3f, 0.5f), coverMaterial);
        CreateBlock(parent, "Boundary_East", new Vector3(20f, 1.5f, 0f), new Vector3(0.5f, 3f, 40f), coverMaterial);
        CreateBlock(parent, "Boundary_West", new Vector3(-20f, 1.5f, 0f), new Vector3(0.5f, 3f, 40f), coverMaterial);
    }

    private static void CreateBlock(Transform parent, string objectName, Vector3 position, Vector3 size, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = position;
        block.transform.localScale = size;
        ApplyMaterial(block, material);
    }

    private static GameObject CreateBodyPart(
        Transform parent,
        PrimitiveType primitive,
        string objectName,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = objectName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            Object.DestroyImmediate(partCollider);
        }
        ApplyMaterial(part, material);
        SetLayerRecursively(part.transform, IgnoreRaycastLayer);
        return part;
    }

    private static Transform CreateChild(Transform parent, string childName, Vector3 localPosition)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        return child.transform;
    }

    private static Material CreateOrLoadMaterial(string assetPath, Color color)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (existing != null)
        {
            existing.color = color;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (shader == null)
        {
            Debug.LogError("No Standard or URP/Lit shader was found. The greybox will use Unity's default material.");
            return null;
        }

        Material material = new Material(shader);
        material.color = color;
        AssetDatabase.CreateAsset(material, assetPath);
        return material;
    }

    private static void ApplyMaterial(GameObject target, Material material)
    {
        if (material == null)
        {
            return;
        }

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Prefabs/Player");
        EnsureFolder("Assets/Materials/Greybox");
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string currentPath = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }
            currentPath = nextPath;
        }
    }
}
#endif
~~~

## D:\GV project\Assets\Editor\Phase2\Phase2AnimationAssets.cs

~~~csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Creates missing native Humanoid clips and the shared Animator Controller; preserves existing assets.</summary>
public static class Phase2AnimationAssets
{
    public const string Root = "Assets/Animations/Phase2";
    public const string ControllerPath = Root + "/PlayerHumanoid.controller";
    public const string AvatarPath = Root + "/GreyboxHumanoidAvatar.asset";
    public const string ModelPath = "Assets/Prefabs/Player/Phase2Humanoid.prefab";

    public static AnimatorController EnsureAssets()
    {
        Folder(Root + "/Clips");
        Folder("Assets/Materials");
        Folder("Assets/Prefabs/Player");
        Phase2HumanoidFactory.Rig rig = Phase2HumanoidFactory.Create(AvatarPath);
        try
        {
            var clips = new Dictionary<string, AnimationClip>();
            string[] names =
            {
                "Idle", "WalkForward", "WalkBackward", "StrafeLeft", "StrafeRight", "Run", "Sprint",
                "Jump", "Falling", "AimIdle", "AimForward", "AimBackward", "AimLeft", "AimRight",
                "Fire", "Reload", "Hit", "Death"
            };
            foreach (string name in names)
            {
                string path = Root + "/Clips/" + name + ".anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    float duration = name == "Sprint" ? 0.55f : name == "Run" ? 0.7f :
                        name == "Fire" ? 0.18f : name == "Hit" ? 0.35f :
                        name == "Jump" ? 0.4f : name == "Reload" ? 1.2f : name == "Death" ? 1.1f : 1f;
                    bool loop = name != "Jump" && name != "Fire" && name != "Reload" && name != "Hit" && name != "Death";
                    clip = Phase2HumanoidFactory.CreateClip(rig, name, duration, loop);
                    AssetDatabase.CreateAsset(clip, path);
                }
                clips.Add(name, clip);
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = BuildController(clips);
            Phase2HumanoidFactory.ResetPose(rig);
            rig.Animator.runtimeAnimatorController = controller;
            rig.Animator.enabled = true;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
                PrefabUtility.SaveAsPrefabAsset(rig.Root, ModelPath);
            AssetDatabase.SaveAssets();
            return controller;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rig.Root);
        }
    }

    private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
    {
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (string name in new[] { "Speed", "Horizontal", "Vertical", "VerticalVelocity" })
            controller.AddParameter(name, AnimatorControllerParameterType.Float);
        foreach (string name in new[] { "IsAiming", "IsGrounded", "IsSprinting", "IsDead" })
            controller.AddParameter(name, AnimatorControllerParameterType.Bool);
        foreach (string name in new[] { "Fire", "Reload", "Hit", "Death" })
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        BlendTree walk = Directional(controller, "Walk Direction", clips["Idle"], clips["WalkForward"],
            clips["WalkBackward"], clips["StrafeLeft"], clips["StrafeRight"]);
        BlendTree locomotion = Tree(controller, "Exploration Speed", BlendTreeType.Simple1D, "Speed");
        locomotion.useAutomaticThresholds = false;
        locomotion.AddChild(clips["Idle"], 0f);
        locomotion.AddChild(walk, 1f);
        locomotion.AddChild(clips["Run"], 2f);
        locomotion.AddChild(clips["Sprint"], 3f);
        BlendTree aiming = Directional(controller, "Aim Direction", clips["AimIdle"], clips["AimForward"],
            clips["AimBackward"], clips["AimLeft"], clips["AimRight"]);

        AnimatorState normal = State(machine, "Locomotion", locomotion, new Vector3(250f, 70f));
        AnimatorState aim = State(machine, "Aim Locomotion", aiming, new Vector3(500f, 70f));
        AnimatorState jump = State(machine, "Jump", clips["Jump"], new Vector3(250f, 220f));
        AnimatorState fall = State(machine, "Falling", clips["Falling"], new Vector3(500f, 220f));
        AnimatorState death = State(machine, "Death", clips["Death"], new Vector3(750f, 70f));
        machine.defaultState = normal;

        // Air transitions take precedence over aim changes.
        foreach (AnimatorState ground in new[] { normal, aim })
        {
            AnimatorStateTransition rising = Transition(ground, jump, 0.06f);
            Bool(rising, "IsGrounded", false);
            rising.AddCondition(AnimatorConditionMode.Greater, 0f, "VerticalVelocity");
            AnimatorStateTransition dropping = Transition(ground, fall, 0.06f);
            Bool(dropping, "IsGrounded", false);
            dropping.AddCondition(AnimatorConditionMode.Less, 0.01f, "VerticalVelocity");
        }

        AnimatorStateTransition enterAim = Transition(normal, aim, 0.15f);
        Bool(enterAim, "IsAiming", true);
        Bool(enterAim, "IsGrounded", true);
        AnimatorStateTransition leaveAim = Transition(aim, normal, 0.15f);
        Bool(leaveAim, "IsAiming", false);
        Bool(leaveAim, "IsGrounded", true);
        Transition(jump, fall, 0.1f).AddCondition(AnimatorConditionMode.Less, 0.01f, "VerticalVelocity");
        foreach (AnimatorState air in new[] { jump, fall })
        {
            AnimatorStateTransition land = Transition(air, normal, 0.1f);
            Bool(land, "IsGrounded", true);
            Bool(land, "IsAiming", false);
            AnimatorStateTransition landAim = Transition(air, aim, 0.1f);
            Bool(landAim, "IsGrounded", true);
            Bool(landAim, "IsAiming", true);
        }
        AnimatorStateTransition die = machine.AddAnyStateTransition(death);
        SetupTransition(die, 0.12f);
        Bool(die, "IsDead", true);
        die.AddCondition(AnimatorConditionMode.If, 0f, "Death");

        AvatarMask mask = new AvatarMask { name = "UpperBody" };
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (AvatarMaskBodyPart part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers,
            AvatarMaskBodyPart.RightFingers })
            mask.SetHumanoidBodyPartActive(part, true);
        AssetDatabase.CreateAsset(mask, Root + "/UpperBody.mask");

        controller.AddLayer("Upper Body Actions");
        AnimatorControllerLayer[] layers = controller.layers;
        layers[1].avatarMask = mask;
        layers[1].defaultWeight = 0f; // Driver fades this layer only while an action is active.
        layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
        controller.layers = layers;
        AnimatorStateMachine actions = controller.layers[1].stateMachine;
        AnimatorState empty = State(actions, "Empty", null, new Vector3(230f, 40f));
        actions.defaultState = empty;
        int row = 0;
        foreach (string action in new[] { "Fire", "Reload", "Hit" })
        {
            AnimatorState state = State(actions, action, clips[action], new Vector3(450f, 40f + 80f * row++));
            AnimatorStateTransition start = actions.AddAnyStateTransition(state);
            SetupTransition(start, 0.04f);
            Bool(start, "IsDead", false);
            start.AddCondition(AnimatorConditionMode.If, 0f, action);
            AnimatorStateTransition finish = state.AddTransition(empty);
            SetupTransition(finish, 0.1f);
            finish.hasExitTime = true;
            finish.exitTime = 0.9f;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static BlendTree Directional(AnimatorController controller, string name, Motion idle,
        Motion forward, Motion backward, Motion left, Motion right)
    {
        BlendTree tree = Tree(controller, name, BlendTreeType.SimpleDirectional2D, "Horizontal");
        tree.blendParameterY = "Vertical";
        tree.AddChild(idle, Vector2.zero);
        tree.AddChild(forward, Vector2.up);
        tree.AddChild(backward, Vector2.down);
        tree.AddChild(left, Vector2.left);
        tree.AddChild(right, Vector2.right);
        return tree;
    }

    private static BlendTree Tree(AnimatorController controller, string name, BlendTreeType type, string parameter)
    {
        BlendTree tree = new BlendTree { name = name, blendType = type, blendParameter = parameter };
        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    private static AnimatorState State(AnimatorStateMachine machine, string name, Motion motion, Vector3 position)
    {
        AnimatorState state = machine.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = true;
        return state;
    }

    private static AnimatorStateTransition Transition(AnimatorState source, AnimatorState target, float duration)
    {
        AnimatorStateTransition transition = source.AddTransition(target);
        SetupTransition(transition, duration);
        Bool(transition, "IsDead", false);
        return transition;
    }

    private static void SetupTransition(AnimatorStateTransition transition, float duration)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.interruptionSource = TransitionInterruptionSource.None;
    }

    private static void Bool(AnimatorStateTransition transition, string name, bool value)
        => transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, name);

    public static void Folder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            if (!AssetDatabase.IsValidFolder(current + "/" + parts[i]))
                AssetDatabase.CreateFolder(current, parts[i]);
            current += "/" + parts[i];
        }
    }
}
~~~

## D:\GV project\Assets\Editor\Phase2\Phase2HumanoidFactory.cs

~~~csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Creates an original, jointed greybox humanoid and samples its poses into Humanoid clips.</summary>
public static class Phase2HumanoidFactory
{
    public sealed class Rig
    {
        public GameObject Root;
        public Animator Animator;
        public readonly Dictionary<HumanBodyBones, Transform> Bones = new Dictionary<HumanBodyBones, Transform>();
        public readonly Dictionary<HumanBodyBones, Vector3> BindPositions = new Dictionary<HumanBodyBones, Vector3>();
    }

    public static Rig Create(string avatarPath)
    {
        Rig rig = new Rig { Root = new GameObject("Humanoid_Phase2") };
        Add(rig, HumanBodyBones.Hips, rig.Root.transform, new Vector3(0f, 0.97f, 0f));
        Add(rig, HumanBodyBones.Spine, B(rig, HumanBodyBones.Hips), new Vector3(0f, 0.2f, 0f));
        Add(rig, HumanBodyBones.Chest, B(rig, HumanBodyBones.Spine), new Vector3(0f, 0.18f, 0f));
        Add(rig, HumanBodyBones.Neck, B(rig, HumanBodyBones.Chest), new Vector3(0f, 0.18f, 0f));
        Add(rig, HumanBodyBones.Head, B(rig, HumanBodyBones.Neck), new Vector3(0f, 0.15f, 0f));
        AddSide(rig, true);
        AddSide(rig, false);

        string[] names = HumanTrait.BoneName;
        HumanBone[] human = rig.Bones.Select(pair => new HumanBone
        {
            boneName = pair.Value.name,
            humanName = names.First(n => n.Replace(" ", "") == pair.Key.ToString()),
            limit = new HumanLimit { useDefaultValues = true }
        }).ToArray();
        SkeletonBone[] skeleton = rig.Root.GetComponentsInChildren<Transform>().Select(t => new SkeletonBone
        {
            name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale
        }).ToArray();

        Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
        if (avatar == null)
        {
            avatar = AvatarBuilder.BuildHumanAvatar(rig.Root, new HumanDescription
            {
                human = human, skeleton = skeleton,
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false
            });
            if (!avatar.isValid || !avatar.isHuman)
            {
                UnityEngine.Object.DestroyImmediate(rig.Root);
                UnityEngine.Object.DestroyImmediate(avatar);
                throw new InvalidOperationException("Generated Avatar is not a valid Humanoid. No scene was changed.");
            }
            avatar.name = "Phase2GreyboxHumanoidAvatar";
            AssetDatabase.CreateAsset(avatar, avatarPath);
        }

        rig.Animator = rig.Root.AddComponent<Animator>();
        rig.Animator.avatar = avatar;
        rig.Animator.applyRootMotion = false;
        rig.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        rig.Animator.enabled = false; // Sampling is driven by the factory, not by an Animator tick.
        BuildVisuals(rig);
        return rig;
    }

    private static void AddSide(Rig rig, bool left)
    {
        float side = left ? -1f : 1f;
        HumanBodyBones upperLeg = left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
        HumanBodyBones lowerLeg = left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
        HumanBodyBones foot = left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
        HumanBodyBones toes = left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes;
        HumanBodyBones shoulder = left ? HumanBodyBones.LeftShoulder : HumanBodyBones.RightShoulder;
        HumanBodyBones upperArm = left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
        HumanBodyBones lowerArm = left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
        HumanBodyBones hand = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;

        Add(rig, upperLeg, B(rig, HumanBodyBones.Hips), new Vector3(side * 0.13f, -0.02f, 0f));
        Add(rig, lowerLeg, B(rig, upperLeg), new Vector3(0f, -0.43f, 0.01f));
        Add(rig, foot, B(rig, lowerLeg), new Vector3(0f, -0.44f, -0.01f));
        Add(rig, toes, B(rig, foot), new Vector3(0f, -0.03f, 0.16f));
        Add(rig, shoulder, B(rig, HumanBodyBones.Chest), new Vector3(side * 0.14f, 0.1f, 0f));
        Add(rig, upperArm, B(rig, shoulder), new Vector3(side * 0.1f, 0f, 0f));
        Add(rig, lowerArm, B(rig, upperArm), new Vector3(side * 0.29f, 0f, 0f));
        Add(rig, hand, B(rig, lowerArm), new Vector3(side * 0.26f, 0f, 0f));
    }

    private static void Add(Rig rig, HumanBodyBones bone, Transform parent, Vector3 position)
    {
        Transform joint = new GameObject(bone.ToString()).transform;
        joint.SetParent(parent, false);
        joint.localPosition = position;
        rig.Bones.Add(bone, joint);
        rig.BindPositions.Add(bone, position);
    }

    private static Transform B(Rig rig, HumanBodyBones bone) => rig.Bones[bone];

    public static void ResetPose(Rig rig)
    {
        foreach (var pair in rig.Bones)
        {
            pair.Value.localPosition = rig.BindPositions[pair.Key];
            pair.Value.localRotation = Quaternion.identity;
        }
    }

    private static void Rotate(Rig rig, HumanBodyBones bone, float x, float y, float z)
        => B(rig, bone).localRotation = Quaternion.Euler(x, y, z);

    public static AnimationClip CreateClip(Rig rig, string clipName, float duration, bool loop)
    {
        AnimationClip clip = new AnimationClip { name = clipName, frameRate = 30f };
        int frames = Mathf.Max(2, Mathf.CeilToInt(duration * clip.frameRate));
        int channels = HumanTrait.MuscleCount + 7;
        List<Keyframe>[] samples = new List<Keyframe>[channels];
        for (int i = 0; i < channels; i++) samples[i] = new List<Keyframe>();

        using (HumanPoseHandler handler = new HumanPoseHandler(rig.Animator.avatar, rig.Root.transform))
        {
            HumanPose pose = new HumanPose();
            for (int frame = 0; frame <= frames; frame++)
            {
                float fraction = (float)frame / frames;
                Pose(rig, clipName, fraction);
                handler.GetHumanPose(ref pose);
                float time = fraction * duration;
                for (int muscle = 0; muscle < HumanTrait.MuscleCount; muscle++)
                    samples[muscle].Add(new Keyframe(time, pose.muscles[muscle]));
                float[] body =
                {
                    pose.bodyPosition.x, pose.bodyPosition.y, pose.bodyPosition.z,
                    pose.bodyRotation.x, pose.bodyRotation.y, pose.bodyRotation.z, pose.bodyRotation.w
                };
                for (int i = 0; i < body.Length; i++)
                    samples[HumanTrait.MuscleCount + i].Add(new Keyframe(time, body[i]));
            }
        }

        string[] bodyProperties = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
        for (int i = 0; i < channels; i++)
        {
            // No finger/eye/jaw bones exist on this greybox. Do not write unsupported bindings.
            if (i < HumanTrait.MuscleCount)
            {
                int bone = HumanTrait.BoneFromMuscle(i);
                if (bone < 0 || !rig.Bones.Keys.Any(b => b.ToString() == HumanTrait.BoneName[bone].Replace(" ", ""))) continue;
            }
            string property = i < HumanTrait.MuscleCount ? HumanTrait.MuscleName[i] : bodyProperties[i - HumanTrait.MuscleCount];
            AnimationCurve curve = new AnimationCurve(samples[i].ToArray());
            for (int k = 0; k < curve.length; k++) curve.SmoothTangents(k, 0f);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curve);
        }
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        settings.lockRootRotation = true;
        settings.lockRootHeightY = true;
        settings.lockRootPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        ResetPose(rig);
        return clip;
    }

    private static void Pose(Rig rig, string clip, float t)
    {
        ResetPose(rig);
        float phase = t * Mathf.PI * 2f;
        float swing = Mathf.Sin(phase);
        bool aiming = clip.StartsWith("Aim", StringComparison.Ordinal) ||
            clip == "Fire" || clip == "Reload";
        bool locomotion = clip.Contains("Walk") || clip.Contains("Strafe") ||
            clip == "Run" || clip == "Sprint" || (aiming && clip != "AimIdle" && clip != "Fire" && clip != "Reload");
        float stride = clip == "Sprint" ? 43f : clip == "Run" ? 34f : 23f;
        if (clip.Contains("Backward")) swing = -swing;

        Rotate(rig, HumanBodyBones.LeftUpperArm, 0f, 0f, 75f);
        Rotate(rig, HumanBodyBones.RightUpperArm, 0f, 0f, -75f);
        Rotate(rig, HumanBodyBones.LeftLowerArm, -12f, 0f, 0f);
        Rotate(rig, HumanBodyBones.RightLowerArm, -12f, 0f, 0f);
        Rotate(rig, HumanBodyBones.Chest, -1f + 1.2f * Mathf.Sin(phase), 0f, 0f);

        if (locomotion)
        {
            Rotate(rig, HumanBodyBones.LeftUpperLeg, -swing * stride, 0f, 0f);
            Rotate(rig, HumanBodyBones.RightUpperLeg, swing * stride, 0f, 0f);
            Rotate(rig, HumanBodyBones.LeftLowerLeg, Mathf.Max(0f, -swing) * 48f, 0f, 0f);
            Rotate(rig, HumanBodyBones.RightLowerLeg, Mathf.Max(0f, swing) * 48f, 0f, 0f);
            Rotate(rig, HumanBodyBones.LeftUpperArm, swing * stride * 0.7f, 0f, 75f);
            Rotate(rig, HumanBodyBones.RightUpperArm, -swing * stride * 0.7f, 0f, -75f);
            B(rig, HumanBodyBones.Hips).localPosition += Vector3.up * (0.012f * (1f - Mathf.Cos(phase * 2f)));
            if (clip.Contains("Left") || clip.Contains("Right"))
            {
                float side = clip.Contains("Left") ? -1f : 1f;
                Rotate(rig, HumanBodyBones.LeftUpperLeg, -swing * 7f, 0f, -side * swing * 20f);
                Rotate(rig, HumanBodyBones.RightUpperLeg, swing * 7f, 0f, side * swing * 20f);
            }
            if (clip == "Run" || clip == "Sprint")
                Rotate(rig, HumanBodyBones.Spine, clip == "Sprint" ? 12f : 6f, 0f, 0f);
        }

        if (aiming)
        {
            Rotate(rig, HumanBodyBones.LeftUpperArm, -66f, -10f, 67f);
            Rotate(rig, HumanBodyBones.RightUpperArm, -70f, 8f, -70f);
            Rotate(rig, HumanBodyBones.LeftLowerArm, -25f, 0f, 0f);
            Rotate(rig, HumanBodyBones.RightLowerArm, -38f, 0f, 0f);
            Rotate(rig, HumanBodyBones.Chest, 3f, -5f, 0f);
        }

        if (clip == "Jump" || clip == "Falling")
        {
            float lift = clip == "Jump" ? Mathf.Sin(t * Mathf.PI) : 0.35f;
            Rotate(rig, HumanBodyBones.LeftUpperLeg, -25f * lift, 0f, -4f);
            Rotate(rig, HumanBodyBones.RightUpperLeg, -12f * lift, 0f, 4f);
            Rotate(rig, HumanBodyBones.LeftLowerLeg, 45f * lift, 0f, 0f);
            Rotate(rig, HumanBodyBones.RightLowerLeg, 30f * lift, 0f, 0f);
            Rotate(rig, HumanBodyBones.LeftUpperArm, -15f, 0f, 45f);
            Rotate(rig, HumanBodyBones.RightUpperArm, -15f, 0f, -45f);
        }
        if (clip == "Fire")
            Rotate(rig, HumanBodyBones.Chest, -8f * Mathf.Sin(t * Mathf.PI), -5f, 0f);
        if (clip == "Reload")
        {
            float reach = Mathf.Sin(t * Mathf.PI);
            Rotate(rig, HumanBodyBones.LeftUpperArm, -40f, -20f * reach, 70f);
            Rotate(rig, HumanBodyBones.LeftLowerArm, -30f - 60f * reach, 0f, 0f);
        }
        if (clip == "Hit")
            Rotate(rig, HumanBodyBones.Chest, -18f * Mathf.Sin(t * Mathf.PI), 0f, 6f * Mathf.Sin(t * Mathf.PI));
        if (clip == "Death")
        {
            float collapse = Mathf.SmoothStep(0f, 1f, t);
            B(rig, HumanBodyBones.Hips).localPosition += Vector3.down * (0.72f * collapse);
            Rotate(rig, HumanBodyBones.Hips, -10f * collapse, 0f, 85f * collapse);
            Rotate(rig, HumanBodyBones.LeftLowerLeg, 40f * collapse, 0f, 0f);
            Rotate(rig, HumanBodyBones.RightLowerLeg, 55f * collapse, 0f, 0f);
        }
    }

    private static void BuildVisuals(Rig rig)
    {
        string path = "Assets/Materials/Phase2Greybox.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null ?
                Shader.Find("Standard") : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No compatible greybox shader found.");
            material = new Material(shader) { color = new Color(0.32f, 0.42f, 0.48f) };
            AssetDatabase.CreateAsset(material, path);
        }
        Part(B(rig, HumanBodyBones.Hips), new Vector3(0f, 0.14f, 0f), new Vector3(0.34f, 0.27f, 0.21f), material);
        Part(B(rig, HumanBodyBones.Chest), new Vector3(0f, 0.03f, 0f), new Vector3(0.41f, 0.25f, 0.23f), material);
        Part(B(rig, HumanBodyBones.Head), new Vector3(0f, 0.02f, 0f), new Vector3(0.24f, 0.27f, 0.24f), material);
        foreach (bool left in new[] { true, false })
        {
            float side = left ? -1f : 1f;
            Part(B(rig, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm), new Vector3(side * 0.145f, 0f, 0f), new Vector3(0.29f, 0.105f, 0.105f), material);
            Part(B(rig, left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm), new Vector3(side * 0.13f, 0f, 0f), new Vector3(0.26f, 0.09f, 0.09f), material);
            Part(B(rig, left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand), new Vector3(side * 0.035f, 0f, 0f), new Vector3(0.09f, 0.065f, 0.095f), material);
            Part(B(rig, left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg), new Vector3(0f, -0.215f, 0f), new Vector3(0.15f, 0.43f, 0.15f), material);
            Part(B(rig, left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg), new Vector3(0f, -0.22f, 0f), new Vector3(0.12f, 0.44f, 0.12f), material);
            Part(B(rig, left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot), new Vector3(0f, -0.02f, 0.065f), new Vector3(0.13f, 0.10f, 0.25f), material);
        }
    }

    private static void Part(Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mesh.name = "GreyboxMesh";
        mesh.transform.SetParent(parent, false);
        mesh.transform.localPosition = position;
        mesh.transform.localScale = scale;
        UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());
        mesh.GetComponent<Renderer>().sharedMaterial = material;
    }
}
~~~

## D:\GV project\Assets\Editor\Phase2\Phase2Setup.cs

~~~csharp
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
    private const string ScenePath = "Assets/Scenes/ZombieMap.unity";

    [MenuItem("Tools/Zombie Outbreak/Phase 2/Set Up ZombieMap")]
    public static void SetUpZombieMap()
    {
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
~~~

## D:\GV project\Assets\Scripts\Core\IInteractable.cs

~~~csharp
using UnityEngine;

/// <summary>Contract for future world objects the player can interact with.</summary>
public interface IInteractable
{
    string InteractionPrompt { get; }
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
}
~~~

## D:\GV project\Assets\Scripts\Player\PlayerAimController.cs

~~~csharp
using System;
using UnityEngine;

/// <summary>Owns aim intent. Camera and animation read this state; future UI can subscribe.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class PlayerAimController : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private KeyCode aimKey = KeyCode.Mouse1;
    [SerializeField] private bool requireLockedCursor = true;
    [SerializeField, Min(0f)] private float aimMoveSpeed = 2.2f;

    public bool IsAiming { get; private set; }
    public float AimMoveSpeed => aimMoveSpeed;
    public event Action<bool> AimChanged;

    private void Awake()
    {
        if (health == null) health = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        bool canAim = (health == null || !health.IsDead) && Application.isFocused &&
            (!requireLockedCursor || Cursor.lockState == CursorLockMode.Locked);
        SetAiming(canAim && Input.GetKey(aimKey));
    }

    public void SetAiming(bool value)
    {
        value = value && isActiveAndEnabled && (health == null || !health.IsDead);
        if (IsAiming == value) return;
        IsAiming = value;
        AimChanged?.Invoke(value);
    }

    private void OnDisable() => SetAiming(false);
}
~~~

## D:\GV project\Assets\Scripts\Player\PlayerAnimatorDriver.cs

~~~csharp
using UnityEngine;

/// <summary>Translates controller state into Animator parameters and exposes animation-only action hooks.</summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(ThirdPersonPlayerController))]
public sealed class PlayerAnimatorDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private ThirdPersonPlayerController movement;
    [SerializeField] private PlayerAimController aim;
    [SerializeField] private PlayerHealth health;
    [SerializeField, Min(0f)] private float parameterDampTime = 0.1f;

    private int actionsLayer = -1;
    private bool deathRequested;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Horizontal = Animator.StringToHash("Horizontal");
    private static readonly int Vertical = Animator.StringToHash("Vertical");
    private static readonly int IsAiming = Animator.StringToHash("IsAiming");
    private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int IsSprinting = Animator.StringToHash("IsSprinting");
    private static readonly int VerticalVelocity = Animator.StringToHash("VerticalVelocity");
    private static readonly int IsDead = Animator.StringToHash("IsDead");
    private static readonly int Fire = Animator.StringToHash("Fire");
    private static readonly int Reload = Animator.StringToHash("Reload");
    private static readonly int Hit = Animator.StringToHash("Hit");
    private static readonly int Death = Animator.StringToHash("Death");

    private void Awake()
    {
        if (movement == null) movement = GetComponent<ThirdPersonPlayerController>();
        if (aim == null) aim = GetComponent<PlayerAimController>();
        if (health == null) health = GetComponent<PlayerHealth>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("PlayerAnimatorDriver needs an Animator with the Phase 2 controller.", this);
            enabled = false;
            return;
        }
        animator.applyRootMotion = false;
        actionsLayer = animator.GetLayerIndex("Upper Body Actions");
    }

    private void OnEnable()
    {
        if (health != null) health.Died += PlayDeath;
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= PlayDeath;
    }

    private void Update()
    {
        if (health != null && health.IsDead && !deathRequested) PlayDeath();
        bool dead = deathRequested || (health != null && health.IsDead);
        bool aiming = aim != null && aim.IsAiming && !dead;
        Vector3 localVelocity = transform.InverseTransformDirection(movement.PlanarVelocity);
        Vector2 local = Vector2.ClampMagnitude(new Vector2(localVelocity.x, localVelocity.z) / movement.WalkSpeed, 1f);
        float dt = Time.deltaTime;
        // Speed uses semantic knots: idle 0, walk 1, run 2, sprint 3.
        float normalizedSpeed = NormalizeSpeed(movement.PlanarSpeed);
        animator.SetFloat(Speed, dead ? 0f : normalizedSpeed, parameterDampTime, dt);
        animator.SetFloat(Horizontal, dead ? 0f : local.x, parameterDampTime, dt);
        animator.SetFloat(Vertical, dead ? 0f : local.y, parameterDampTime, dt);
        animator.SetBool(IsAiming, aiming);
        animator.SetBool(IsGrounded, movement.IsGrounded);
        animator.SetBool(IsSprinting, movement.IsSprinting && !dead);
        animator.SetFloat(VerticalVelocity, movement.VerticalVelocity);
        animator.SetBool(IsDead, dead);
        if (actionsLayer >= 0)
        {
            bool active = !animator.GetCurrentAnimatorStateInfo(actionsLayer).IsName("Empty") ||
                (animator.IsInTransition(actionsLayer) && !animator.GetNextAnimatorStateInfo(actionsLayer).IsName("Empty"));
            float weight = Mathf.MoveTowards(animator.GetLayerWeight(actionsLayer), !dead && active ? 1f : 0f, dt / 0.08f);
            animator.SetLayerWeight(actionsLayer, dead ? 0f : weight);
        }
    }

    private float NormalizeSpeed(float speed)
    {
        if (speed <= movement.WalkSpeed) return speed / movement.WalkSpeed;
        if (speed <= movement.RunSpeed)
            return 1f + Mathf.InverseLerp(movement.WalkSpeed, movement.RunSpeed, speed);
        return 2f + Mathf.InverseLerp(movement.RunSpeed, movement.SprintSpeed, speed);
    }

    // These methods animate only. They do not deal damage, spawn objects, consume ammo, or read firing keys.
    public void PlayFire() { if (CanPlayAction()) animator.SetTrigger(Fire); }
    public void PlayReload() { if (CanPlayAction()) animator.SetTrigger(Reload); }
    public void PlayHit() { if (CanPlayAction()) animator.SetTrigger(Hit); }

    public void PlayDeath()
    {
        if (animator == null) return;
        deathRequested = true;
        animator.ResetTrigger(Fire);
        animator.ResetTrigger(Reload);
        animator.ResetTrigger(Hit);
        animator.SetBool(IsDead, true);
        animator.SetTrigger(Death);
    }

    private bool CanPlayAction() => animator != null && isActiveAndEnabled &&
        !deathRequested && (health == null || !health.IsDead);

    [ContextMenu("Animation Preview/Fire (Play Mode)")]
    private void PreviewFire() { if (Application.isPlaying) PlayFire(); }
    [ContextMenu("Animation Preview/Reload (Play Mode)")]
    private void PreviewReload() { if (Application.isPlaying) PlayReload(); }
    [ContextMenu("Animation Preview/Hit (Play Mode)")]
    private void PreviewHit() { if (Application.isPlaying) PlayHit(); }
    [ContextMenu("Animation Preview/Death via PlayerHealth (Play Mode)")]
    private void PreviewDeath() { if (Application.isPlaying && health != null) health.Die(); }
}
~~~

## D:\GV project\Assets\Scripts\Player\PlayerHealth.cs

~~~csharp
using System;
using UnityEngine;

public sealed class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    public float MaxHealth { get { return maxHealth; } }
    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    /// <summary>Raised with the new current health and the configured maximum health.</summary>
    public event Action<float, float> HealthChanged;
    public event Action Died;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        RaiseHealthChanged();

        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        RaiseHealthChanged();
    }

    public void Die()
    {
        if (IsDead)
        {
            return;
        }

        bool healthChanged = CurrentHealth > 0f;
        IsDead = true;
        CurrentHealth = 0f;
        if (healthChanged)
        {
            RaiseHealthChanged();
        }
        Died?.Invoke();
    }

    private void RaiseHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
    }
}
~~~

## D:\GV project\Assets\Scripts\Player\ThirdPersonCameraController.cs

~~~csharp
using UnityEngine;

/// <summary>One output camera blending two virtual poses, with a shared orbit and collision solve.</summary>
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class ThirdPersonCameraController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform aimTarget;
    [SerializeField] private PlayerAimController aim;

    [Header("Virtual camera modes")]
    [SerializeField] private ThirdPersonCameraMode explorationCamera;
    [SerializeField] private ThirdPersonCameraMode aimCamera;
    [SerializeField, Min(0.01f)] private float modeBlendTime = 0.2f;

    [Header("Orbit and fallback exploration pose")]
    [SerializeField, Min(0.1f)] private float followDistance = 4.5f;
    [SerializeField] private float lookAtHeightOffset = 0.05f;
    [SerializeField] private float startingPitch = 12f;
    [SerializeField] private float minimumPitch = -25f;
    [SerializeField] private float maximumPitch = 65f;
    [SerializeField, Min(0f)] private float sensitivity = 2.5f;
    [SerializeField, Min(0f)] private float positionSmoothTime = 0.08f;
    [SerializeField, Min(1f)] private float aimTargetDistance = 100f;

    [Header("Collision")]
    [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
    [SerializeField, Min(0f)] private float collisionPadding = 0.05f;
    [SerializeField] private LayerMask collisionLayers = Physics.DefaultRaycastLayers;

    [Header("Mouse")]
    [SerializeField] private bool lockCursorAtStart = true;
    [SerializeField] private bool invertMouseY;

    private readonly RaycastHit[] collisionHits = new RaycastHit[32];
    private Camera outputCamera;
    private Transform playerRoot;
    private float yaw;
    private float pitch;
    private float aimBlend;
    private float blendVelocity;
    private Vector3 pivot;
    private Vector3 pivotVelocity;
    private bool initialized;

    public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    public bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;
    public float AimBlend => aimBlend;
    public float FollowDistance
    {
        get => explorationCamera != null ? explorationCamera.Distance : followDistance;
        set
        {
            followDistance = Mathf.Max(0.3f, value);
            if (explorationCamera != null) explorationCamera.Distance = followDistance;
        }
    }
    public float Sensitivity { get => sensitivity; set => sensitivity = Mathf.Max(0f, value); }

    private void Awake()
    {
        outputCamera = GetComponent<Camera>();
        if (cameraTarget == null)
        {
            ThirdPersonPlayerController player = FindFirstObjectByType<ThirdPersonPlayerController>();
            if (player != null)
                cameraTarget = player.transform.Find("CameraTarget") ?? player.transform;
        }
        if (cameraTarget != null)
        {
            ThirdPersonPlayerController player = cameraTarget.GetComponentInParent<ThirdPersonPlayerController>();
            playerRoot = player != null ? player.transform : null;
            if (aim == null && playerRoot != null) aim = playerRoot.GetComponent<PlayerAimController>();
            yaw = cameraTarget.eulerAngles.y;
        }
        pitch = Mathf.Clamp(startingPitch, minimumPitch, maximumPitch);
    }

    private void OnEnable() => SetCursorLocked(lockCursorAtStart);

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(false);
        else if (!CursorLocked && Application.isFocused && Input.GetMouseButtonDown(0)) SetCursorLocked(true);
        if (!CursorLocked || !Application.isFocused || Time.timeScale <= 0f) return;
        float multiplier = Mathf.Lerp(
            explorationCamera != null ? explorationCamera.SensitivityMultiplier : 1f,
            aimCamera != null ? aimCamera.SensitivityMultiplier : 0.65f, aimBlend);
        yaw = Mathf.Repeat(yaw + Input.GetAxisRaw("Mouse X") * sensitivity * multiplier, 360f);
        pitch = Mathf.Clamp(pitch + Input.GetAxisRaw("Mouse Y") * sensitivity * multiplier *
            (invertMouseY ? 1f : -1f), minimumPitch, maximumPitch);
    }

    private void LateUpdate()
    {
        if (cameraTarget == null || Time.deltaTime <= 0f) return;
        aimBlend = Mathf.SmoothDamp(aimBlend, aim != null && aim.IsAiming ? 1f : 0f,
            ref blendVelocity, modeBlendTime);
        Vector3 targetPivot = cameraTarget.position;
        if (!initialized)
        {
            pivot = targetPivot;
            initialized = true;
        }
        else pivot = Vector3.SmoothDamp(pivot, targetPivot, ref pivotVelocity, positionSmoothTime);

        float distance = Mathf.Lerp(explorationCamera != null ? explorationCamera.Distance : followDistance,
            aimCamera != null ? aimCamera.Distance : 2.25f, aimBlend);
        Vector2 offset = Vector2.Lerp(
            explorationCamera != null ? explorationCamera.ShoulderOffset : new Vector2(0.2f, lookAtHeightOffset),
            aimCamera != null ? aimCamera.ShoulderOffset : new Vector2(0.6f, 0.08f), aimBlend);
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desired = pivot + orbit * new Vector3(offset.x, offset.y, -distance);

        // Re-solve collision from the current player pivot after smoothing, including the shoulder offset.
        // This avoids interpolating the camera through a wall during a mode change.
        outputCamera.fieldOfView = Mathf.Lerp(
            explorationCamera != null ? explorationCamera.FieldOfView : 60f,
            aimCamera != null ? aimCamera.FieldOfView : 45f, aimBlend);
        float radius = Mathf.Max(collisionRadius, NearPlaneRadius());
        Vector3 safe = ConstrainCamera(targetPivot, desired, radius);
        transform.SetPositionAndRotation(safe, orbit);

        if (aimTarget != null)
        {
            Ray ray = outputCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float distanceToAim = NearestHit(ray.origin, 0f, ray.direction, aimTargetDistance);
            aimTarget.position = ray.GetPoint(distanceToAim);
        }
    }

    private Vector3 ConstrainCamera(Vector3 origin, Vector3 desired, float radius)
    {
        Vector3 delta = desired - origin;
        float distance = delta.magnitude;
        if (distance < 0.001f) return origin;
        float allowed = NearestHit(origin, radius, delta / distance, distance);
        // A ray still detects the wall if a large probe starts overlapping it.
        float rayDistance = NearestHit(origin, 0f, delta / distance, distance);
        if (rayDistance < distance) allowed = Mathf.Min(allowed, Mathf.Max(0f, rayDistance - radius));
        return origin + delta / distance * Mathf.Max(0f, allowed < distance ? allowed - collisionPadding : distance);
    }

    private float NearestHit(Vector3 origin, float radius, Vector3 direction, float distance)
    {
        int count = radius > 0f ?
            Physics.SphereCastNonAlloc(origin, radius, direction, collisionHits, distance, collisionLayers, QueryTriggerInteraction.Ignore) :
            Physics.RaycastNonAlloc(origin, direction, collisionHits, distance, collisionLayers, QueryTriggerInteraction.Ignore);
        float nearest = distance;
        for (int i = 0; i < count; i++)
        {
            Transform hit = collisionHits[i].transform;
            if (hit == null || (playerRoot != null && hit.IsChildOf(playerRoot))) continue;
            nearest = Mathf.Min(nearest, collisionHits[i].distance);
        }
        return nearest;
    }

    private float NearPlaneRadius()
    {
        float halfHeight = Mathf.Tan(outputCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * outputCamera.nearClipPlane;
        return Mathf.Sqrt(halfHeight * halfHeight * (1f + outputCamera.aspect * outputCamera.aspect));
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnApplicationFocus(bool focused) { if (!focused) SetCursorLocked(false); }
    private void OnDisable() => SetCursorLocked(false);
}
~~~

## D:\GV project\Assets\Scripts\Player\ThirdPersonCameraMode.cs

~~~csharp
using UnityEngine;

/// <summary>A virtual camera pose; only Main Camera renders or owns an AudioListener.</summary>
[DisallowMultipleComponent]
public sealed class ThirdPersonCameraMode : MonoBehaviour
{
    [SerializeField, Min(0.3f)] private float distance = 4.5f;
    [SerializeField] private Vector2 shoulderOffset = new Vector2(0.2f, 0.05f);
    [SerializeField, Range(20f, 100f)] private float fieldOfView = 60f;
    [SerializeField, Min(0f)] private float sensitivityMultiplier = 1f;

    public float Distance { get => distance; set => distance = Mathf.Max(0.3f, value); }
    public Vector2 ShoulderOffset => shoulderOffset;
    public float FieldOfView => fieldOfView;
    public float SensitivityMultiplier => sensitivityMultiplier;
}
~~~

## D:\GV project\Assets\Scripts\Player\ThirdPersonPlayerController.cs

~~~csharp
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public sealed class ThirdPersonPlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private ThirdPersonCameraController cameraRig;
    [SerializeField] private PlayerAimController aim;
    [SerializeField] private PlayerHealth health;

    [Header("Speeds in metres per second")]
    [SerializeField, Min(0.01f)] private float walkSpeed = 2.2f;
    [SerializeField, Min(0.01f)] private float runSpeed = 4.2f;
    [SerializeField, Min(0.01f)] private float sprintSpeed = 6.5f;
    [SerializeField, Min(0f)] private float rotationDegreesPerSecond = 720f;

    [Header("Jump and gravity")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.25f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftControl;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    [Header("Ground probe")]
    [SerializeField, Min(0.01f)] private float groundProbeRadius = 0.2f;
    [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;

    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private CharacterController characterController;
    private float verticalVelocity;

    public Vector2 MoveInput { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsSprinting { get; private set; }
    public float VerticalVelocity => verticalVelocity;
    public float PlanarSpeed { get; private set; }
    public Vector3 PlanarVelocity { get; private set; }
    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float SprintSpeed => sprintSpeed;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (cameraRig == null && cameraTransform != null)
            cameraRig = cameraTransform.GetComponent<ThirdPersonCameraController>();
        if (aim == null) aim = GetComponent<PlayerAimController>();
        if (health == null) health = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || !characterController.enabled) return;

        bool alive = health == null || !health.IsDead;
        bool inputAllowed = alive && Application.isFocused &&
            Cursor.lockState == CursorLockMode.Locked;
        bool aiming = aim != null && aim.IsAiming && alive;

        float x = inputAllowed ? (Input.GetKey(KeyCode.D) ? 1f : 0f) -
            (Input.GetKey(KeyCode.A) ? 1f : 0f) : 0f;
        float z = inputAllowed ? (Input.GetKey(KeyCode.W) ? 1f : 0f) -
            (Input.GetKey(KeyCode.S) ? 1f : 0f) : 0f;
        MoveInput = Vector2.ClampMagnitude(new Vector2(x, z), 1f);

        // Never let a ground probe re-ground an ascending jump.
        IsGrounded = verticalVelocity <= 0f && (characterController.isGrounded || ProbeGround());
        if (IsGrounded) verticalVelocity = -2f;

        Vector3 forward = cameraRig != null ? cameraRig.PlanarForward :
            Vector3.ProjectOnPlane(cameraTransform != null ? cameraTransform.forward : transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 direction = Vector3.ClampMagnitude(forward * MoveInput.y + right * MoveInput.x, 1f);

        if (alive && (aiming || direction.sqrMagnitude > 0.0001f))
        {
            // In aim mode S walks backward and A/D strafe without turning the whole body.
            Vector3 facing = aiming ? forward : direction;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(facing, Vector3.up), rotationDegreesPerSecond * dt);
        }

        IsSprinting = inputAllowed && !aiming && MoveInput.sqrMagnitude > 0.001f &&
            Input.GetKey(sprintKey);
        float speed = aiming ? aim.AimMoveSpeed : IsSprinting ? sprintSpeed :
            inputAllowed && Input.GetKey(runKey) ? runSpeed : walkSpeed;

        if (inputAllowed && IsGrounded && Input.GetKeyDown(jumpKey))
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            IsGrounded = false;
        }

        verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, -50f);
        Vector3 before = transform.position;
        CollisionFlags flags = characterController.Move(
            (direction * speed + Vector3.up * verticalVelocity) * dt);
        PlanarVelocity = Vector3.ProjectOnPlane((transform.position - before) / dt, Vector3.up);
        PlanarSpeed = PlanarVelocity.magnitude;
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
        IsGrounded = verticalVelocity <= 0f &&
            (((flags & CollisionFlags.Below) != 0) || ProbeGround());
        if (IsGrounded) verticalVelocity = -2f;
        IsSprinting &= IsGrounded && PlanarSpeed > walkSpeed;
    }

    private bool ProbeGround()
    {
        Vector3 point = groundCheck != null ? groundCheck.position : transform.position + Vector3.up * 0.08f;
        // Start above the feet and inspect the contact normal: side walls are not ground.
        Vector3 origin = point + Vector3.up * (groundProbeRadius + 0.04f);
        int count = Physics.SphereCastNonAlloc(origin, groundProbeRadius, Vector3.down,
            groundHits, 0.16f, groundLayers, QueryTriggerInteraction.Ignore);
        float minimumNormalY = Mathf.Cos(characterController.slopeLimit * Mathf.Deg2Rad);
        for (int i = 0; i < count; i++)
        {
            Collider hit = groundHits[i].collider;
            if (hit == null || hit == characterController || hit.transform.IsChildOf(transform)) continue;
            if (groundHits[i].normal.y >= minimumNormalY) return true;
        }
        return false;
    }

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0.01f, walkSpeed);
        runSpeed = Mathf.Max(walkSpeed, runSpeed);
        sprintSpeed = Mathf.Max(runSpeed, sprintSpeed);
        gravity = Mathf.Min(-0.1f, gravity);
        groundProbeRadius = Mathf.Max(0.01f, groundProbeRadius);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = IsGrounded ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundProbeRadius);
    }
}
~~~
