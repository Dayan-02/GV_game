using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

public static class Phase7Setup
{
    public const string BasePrefab = "Assets/Prefabs/Enemies/EnemyBase.prefab";
    public const string ControllerPath = "Assets/Animations/Enemies/Enemy.controller";
    [MenuItem("Dead city/Phase 7 - Shared enemies")]
    public static void Build()
    {
        OutbreakEditor.Open();
        Phase2AnimationAssets.Folder("Assets/Prefabs/Enemies");
        Phase2AnimationAssets.Folder("Assets/Animations/Enemies");
        var world = OutbreakEditor.Add<EnemyWorld>(OutbreakEditor.Group("EnemySystems").gameObject);
        world.player = OutbreakEditor.One<PlayerHealth>();
        world.weapons = OutbreakEditor.One<PlayerWeaponController>();
        world.movement = OutbreakEditor.One<ThirdPersonPlayerController>();
        world.tacticalPoints = Object.FindObjectsByType<MapMarker>().Where(m => m.kind == MapMarkerKind.Ambush || m.kind == MapMarkerKind.EliteArena).ToArray();
        var controller = MakeController();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab) == null)
        {
            var go = new GameObject("EnemyBase");
            go.layer = 9;
            var rig = Phase2HumanoidFactory.Create("Assets/Animations/Enemies/EnemyAvatar.asset");
            rig.Root.name = "Visual";
            rig.Root.transform.SetParent(go.transform, false);
            rig.Animator.runtimeAnimatorController = controller;
            rig.Animator.enabled = true;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = Vector3.up * .95f;
            capsule.height = 1.9f;
            capsule.radius = .35f;
            var agent = go.AddComponent<NavMeshAgent>();
            agent.height = 1.9f;
            agent.radius = .4f;
            agent.speed = 2.2f;
            agent.acceleration = 12;
            agent.angularSpeed = 540;
            agent.stoppingDistance = 1.2f;
            agent.autoBraking = true;
            go.AddComponent<EnemyHealth>();
            go.AddComponent<EnemyMovement>();
            go.AddComponent<EnemyCombat>();
            go.AddComponent<EnemyPerception>();
            go.AddComponent<EnemyActor>();
            var animation = go.AddComponent<EnemyAnimationController>();
            OutbreakEditor.Set(animation, "animator", rig.Animator);
            PrefabUtility.SaveAsPrefabAsset(go, BasePrefab);
            Object.DestroyImmediate(go);
        }

        OutbreakEditor.Save("7");
    }

    private static AnimatorController MakeController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
            return controller;
        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (string trigger in new[]
        {
            "Attack",
            "Heavy",
            "Hit"
        }

        )
            controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        var sm = controller.layers[0].stateMachine;
        var locomotion = sm.AddState("Locomotion");
        sm.defaultState = locomotion;
        var tree = new BlendTree
        {
            name = "EnemyLocomotion",
            blendParameter = "Speed",
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(Clip("Idle"), 0);
        tree.AddChild(Clip("WalkForward"), 1.5f);
        tree.AddChild(Clip("Run"), 3.5f);
        locomotion.motion = tree;
        foreach (string action in new[]
        {
            "Attack",
            "Heavy",
            "Hit",
            "Death"
        }

        )
        {
            var state = sm.AddState(action);
            state.motion = Clip(action == "Attack" || action == "Heavy" ? "Fire" : action);
            if (action == "Attack" || action == "Heavy")
            {
                state.speedParameter = "AttackSpeed";
                state.speedParameterActive = true;
            }

            var enter = sm.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = .06f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(action == "Death" ? AnimatorConditionMode.If : AnimatorConditionMode.If, 0, action == "Death" ? "Dead" : action);
            if (action != "Death")
            {
                enter.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                var exit = state.AddTransition(locomotion);
                exit.hasExitTime = true;
                exit.exitTime = 1;
                exit.duration = .1f;
            }
        }

        return controller;
    }

    private static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Phase2/Clips/" + name + ".anim");
}
