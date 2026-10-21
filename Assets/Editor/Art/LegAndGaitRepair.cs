using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Repairs foot IK and replaces conflicting enemy gait blends with speed-matched states.</summary>
public static class LegAndGaitRepair
{
    private const string Root = "Assets/Animations/GroundedEnemies";
    [MenuItem("Dead city/Art/Repair player feet and zombie locomotion")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        EditorSceneManager.SaveOpenScenes();
        Phase2AnimationAssets.Folder(Root);
        GroundedZombieRig.Build();
        var walk = Copy("GroundedWalk", AssetDatabase.LoadAssetAtPath<AnimationClip>(ImportedCharacterSetup.MotionRoot + "/Walk.anim"));
        var idle = Copy("GroundedIdle", AssetDatabase.LoadAssetAtPath<AnimationClip>(ImportedCharacterSetup.MotionRoot + "/Idle.anim"));
        var chase = Copy("GroundedChase", AssetDatabase.LoadAssetAtPath<AnimationClip>(ImportedCharacterSetup.MotionRoot + "/Run.anim"));
        foreach (var clip in new[] { idle, walk, chase })
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true; settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }
        string path = Root + "/GroundedEnemies.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) == null)
            AssetDatabase.CopyAsset(ImportedCharacterSetup.MotionRoot + "/Infected.controller", path);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (!controller.parameters.Any(p => p.name == "GaitScale"))
            controller.AddParameter(new AnimatorControllerParameter { name = "GaitScale", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        var machine = controller.layers[0].stateMachine;
        var slow = machine.states.First(s => s.state.name == "Locomotion").state;
        var tree = AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>().FirstOrDefault(t => t.name == "Grounded slow gait");
        if (tree == null) { tree = new BlendTree { name = "Grounded slow gait" }; AssetDatabase.AddObjectToAsset(tree, controller); }
        tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
        tree.children = new[] { new ChildMotion { motion = idle, threshold = 0, timeScale = 1 }, new ChildMotion { motion = walk, threshold = .08f, timeScale = 1 } };
        slow.motion = tree; slow.speed = 1; slow.speedParameter = "GaitScale"; slow.speedParameterActive = true;
        var run = machine.states.FirstOrDefault(s => s.state.name == "Chase").state;
        if (run == null) run = machine.AddState("Chase");
        run.motion = chase; run.speed = 1; run.speedParameter = "GaitScale"; run.speedParameterActive = true;
        foreach (var transition in slow.transitions.Where(t => t.destinationState == run).ToArray()) slow.RemoveTransition(transition);
        foreach (var transition in run.transitions.Where(t => t.destinationState == slow).ToArray()) run.RemoveTransition(transition);
        Link(slow, run, AnimatorConditionMode.Greater, 2.7f);
        Link(run, slow, AnimatorConditionMode.Less, 2.4f);
        EditorUtility.SetDirty(tree); EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        var attack = GroundedZombieActions.Bake("GroundedAttack", ImportedCharacterMotion.ImportClip(ImportedCharacterSetup.Root + "/PixelhouseZombie/fury.FBX"));
        var death = GroundedZombieActions.Bake("GroundedDeath", ImportedCharacterMotion.ImportClip(ImportedCharacterSetup.Root + "/PixelhouseZombie/dead.FBX"));

        // Measure on the actual target skeleton, not the source character's height.
        var sample = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/CanonicalZombie.prefab"));
        float chaseSpeed, walkSpeed;
        try
        {
            var a = sample.GetComponentInChildren<Animator>(); a.runtimeAnimatorController = controller;
            chaseSpeed = Measure(a, "Chase");
            walkSpeed = Measure(a, "Locomotion");
        }
        finally { Object.DestroyImmediate(sample); }
        File.WriteAllText("Logs/GaitCalibration.txt", "Walk stance speed: " + walkSpeed + " m/s\nChase stance speed: " + chaseSpeed + " m/s\n");

        foreach (string kind in new[] { "EnemyBase", "Hunter", "Runner", "Ambusher", "Elite" })
        {
            string sourceName = kind == "EnemyBase" ? "Hunter" : kind;
            var previous = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ImportedCharacterSetup.MotionRoot + "/" + sourceName + ".overrideController");
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); previous.GetOverrides(pairs);
            string overridePath = Root + "/" + sourceName + ".overrideController";
            var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if (overrides == null) { overrides = new AnimatorOverrideController(controller); AssetDatabase.CreateAsset(overrides, overridePath); }
            overrides.runtimeAnimatorController = controller;
            foreach (var clip in controller.animationClips.Distinct())
            {
                var pair = pairs.FirstOrDefault(p => p.Key.name == clip.name);
                if (pair.Value != null) overrides[clip.name] = pair.Value;
            }
            overrides["ZombieAttack"] = attack; overrides["EliteHeavy"] = attack; overrides["ZombieDeath"] = death;
            EditorUtility.SetDirty(overrides);
            Edit("Assets/Prefabs/Enemies/" + kind + ".prefab", root =>
            {
                var old = root.GetComponentInChildren<Animator>(true);
                var materials = new[] { AssetDatabase.LoadAssetAtPath<Material>(ImportedCharacterSetup.Root + "/Materials/Infected_" + kind + ".mat") };
                var scale = kind == "Elite" ? new Vector3(1.17f,1.12f,1.17f) : kind == "Runner" ? new Vector3(.94f,1,.94f) : Vector3.one;
                // Base-prefab replacements can introduce a new inherited Visual beside
                // a variant's old override. Remove every prior rig before installing one.
                foreach (var prior in root.GetComponentsInChildren<Animator>(true))
                    if (prior != null) Object.DestroyImmediate(prior.gameObject);
                var visual = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/CanonicalZombie.prefab"), root.transform);
                visual.name = "Visual"; visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity; visual.transform.localScale = scale;
                foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;
                foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.sharedMaterials = materials;
                var a = visual.GetComponent<Animator>(); a.runtimeAnimatorController = overrides;
                OutbreakEditor.Set(root.GetComponent<EnemyAnimationController>(), "animator", a);
                // Prototype clothing was parented to the gameplay root and slid along the road.
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t != null && (t.name == "SurfaceBelt" || t.name == "SurfaceEquipment" || t.name == "KneeProtection" || t.name == "BootSole"))
                        Object.DestroyImmediate(t.gameObject);
                a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                OutbreakEditor.Values(root.GetComponent<EnemyAnimationController>(), s =>
                {
                    s.FindProperty("walkStrideSpeed").floatValue = walkSpeed;
                    s.FindProperty("chaseStrideSpeed").floatValue = chaseSpeed;
                    s.FindProperty("visualStrideScale").floatValue = Mathf.Abs(a.transform.localScale.x);
                });
            });
        }
        Edit("Assets/Prefabs/Player/PlayerPhase3.prefab", ConfigureFeet);
        OutbreakEditor.Open();
        ConfigureFeet(Object.FindAnyObjectByType<ThirdPersonPlayerController>().gameObject);
        OutbreakEditor.Save("LEG_AND_GAIT_REPAIR");
    }

    private static void ConfigureFeet(GameObject root)
    {
        var feet = root.GetComponentInChildren<HumanoidFootPlacement>(true);
        if (feet == null) return;
        OutbreakEditor.Values(feet, s => { s.FindProperty("weight").floatValue = .65f; s.FindProperty("maximumCorrection").floatValue = .08f; s.FindProperty("groundLayers").intValue = 1 << 8; });
    }

    private static float Measure(Animator a, string state)
    {
        a.Rebind(); a.SetFloat("GaitScale", 1); a.SetFloat("Speed", state == "Chase" ? 3 : .8f); a.Play(state, 0, 0); a.Update(.001f);
        float duration = a.GetCurrentAnimatorStateInfo(0).length;
        var points = new Vector3[2, 121];
        for (int f = 0; f <= 120; f++)
        {
            a.Play(state, 0, f / 120f); a.Update(0);
            points[0, f] = a.GetBoneTransform(HumanBodyBones.LeftFoot).position;
            points[1, f] = a.GetBoneTransform(HumanBodyBones.RightFoot).position;
        }
        var speeds = new List<float>();
        for (int foot = 0; foot < 2; foot++)
        {
            float low = Enumerable.Range(0, 121).Min(f => points[foot, f].y);
            for (int f = 1; f < 120; f++) if (points[foot, f].y < low + .05f)
            {
                float v = Vector3.Dot(points[foot, f] - points[foot, f - 1], a.transform.forward) / (duration / 120f);
                if (v < -.05f) speeds.Add(-v);
            }
        }
        if (speeds.Count < 4) throw new InvalidOperationException("Unable to measure chase foot travel.");
        speeds.Sort(); return speeds[speeds.Count / 2];
    }
    private static AnimationClip Copy(string name, AnimationClip source)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/" + name + ".anim");
        if (clip == null) { clip = Object.Instantiate(source); AssetDatabase.CreateAsset(clip, Root + "/" + name + ".anim"); }
        else EditorUtility.CopySerialized(source, clip);
        clip.name = name; return clip;
    }
    private static void Link(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float speed)
    {
        var t = from.AddTransition(to); t.hasExitTime = false; t.hasFixedDuration = true; t.duration = .12f;
        t.AddCondition(mode, speed, "Speed"); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
    }
    private static void Edit(string path, Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
