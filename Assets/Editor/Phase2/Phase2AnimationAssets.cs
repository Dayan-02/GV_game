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
