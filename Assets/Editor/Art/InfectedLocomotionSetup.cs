using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>Authors infected locomotion on the corrected zombie Avatar. Navigation owns translation.</summary>
public static class InfectedLocomotionSetup
{
    public const string Root = "Assets/Animations/InfectedLocomotion";
    private const string Grounded = "Assets/Animations/GroundedEnemies";
    private static readonly string[] Kinds = { "Hunter", "Runner", "Ambusher", "Elite" };

    [MenuItem("Tools/Dead Zone/Author infected locomotion")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode first.");
        Phase2AnimationAssets.Folder(Root);
        var nativeWalk = GroundedZombieActions.Bake("InfectedShambleSource",
            ImportedCharacterMotion.ImportClip(ImportedCharacterSetup.Zombie), Root);
        Loop(nativeWalk);
        var report = new System.Text.StringBuilder("Infected animation authoring measurements (not a gameplay test)\n");
        var controllers = new Dictionary<string, RuntimeAnimatorController>();
        foreach (string kind in Kinds)
        {
            var idle = Author(kind, "Idle", AssetDatabase.LoadAssetAtPath<AnimationClip>(Grounded + "/GroundedIdle.anim"));
            var walk = Author(kind, "Shamble", nativeWalk);
            var chase = Author(kind, "Chase", AssetDatabase.LoadAssetAtPath<AnimationClip>(Grounded + "/GroundedChase.anim"));
            RefineContact(idle, .008f, kind); RefineContact(walk, .025f, kind); RefineContact(chase, .06f, kind);
            var previous = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Grounded + "/" + kind + ".overrideController");
            string controllerPath = Root + "/" + kind + ".controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) == null)
                AssetDatabase.CopyAsset(Grounded + "/GroundedEnemies.controller", controllerPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            var states = controller.layers[0].stateMachine.states;
            var locomotion = states.First(s => s.state.name == "Locomotion").state;
            var run = states.First(s => s.state.name == "Chase").state;
            // Elite advance and Ambusher stalking remain a shamble; their rushes use Chase.
            float enter = kind == "Elite" ? 3.2f : kind == "Ambusher" ? 3.35f : 2.7f;
            foreach (var transition in locomotion.transitions.Where(t => t.destinationState == run))
                SetThreshold(transition, enter);
            foreach (var transition in run.transitions.Where(t => t.destinationState == locomotion))
                SetThreshold(transition, enter - .25f);
            EditorUtility.SetDirty(controller);
            string overridePath = Root + "/" + kind + ".overrideController";
            var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if (overrides == null) { overrides = new AnimatorOverrideController(controller); AssetDatabase.CreateAsset(overrides, overridePath); }
            overrides.runtimeAnimatorController = controller;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); previous.GetOverrides(pairs);
            overrides.ApplyOverrides(pairs); // Existing attack, heavy, hit and death remain intact.
            overrides["GroundedIdle"] = idle;
            overrides["GroundedWalk"] = walk;
            overrides["GroundedChase"] = chase;
            EditorUtility.SetDirty(overrides);
            controllers[kind] = overrides;
            float walkSpeed = MeasureStride(walk), chaseSpeed = MeasureStride(chase);
            report.AppendLine(kind + ": shamble stride=" + walkSpeed.ToString("F3") + " m/s; chase=" + chaseSpeed.ToString("F3") + " m/s; chase threshold=" + enter);
            Assign("Assets/Prefabs/Enemies/" + kind + ".prefab", overrides, walkSpeed, chaseSpeed);
            if (kind == "Hunter") Assign("Assets/Prefabs/Enemies/EnemyBase.prefab", overrides, walkSpeed, chaseSpeed);
        }
        // Existing scene actors are prefab instances; explicit references are updated too.
        foreach (var actor in Object.FindObjectsByType<EnemyActor>())
        {
            string kind = Kinds.FirstOrDefault(k => actor.name.StartsWith(k, StringComparison.Ordinal));
            if (kind == null) continue;
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator != null) animator.runtimeAnimatorController = controllers[kind];
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/InfectedMotionAuthoring.txt", report.ToString());
    }

    private static void SetThreshold(AnimatorStateTransition transition, float value)
    {
        var conditions = transition.conditions;
        for (int i = 0; i < conditions.Length; i++) if (conditions[i].parameter == "Speed") conditions[i].threshold = value;
        transition.conditions = conditions; transition.duration = .16f; EditorUtility.SetDirty(transition);
    }

    private static void Assign(string path, RuntimeAnimatorController controller, float walk, float chase)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var animator = root.GetComponentInChildren<Animator>(); animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // The authored infected torso replaces the shared survivor-style breathing overlay.
            var polish = animator.GetComponent<CharacterMotionPolish>(); if (polish != null) polish.enabled = false;
            OutbreakEditor.Values(root.GetComponent<EnemyAnimationController>(), s => {
                s.FindProperty("walkStrideSpeed").floatValue = walk;
                s.FindProperty("chaseStrideSpeed").floatValue = chase;
                s.FindProperty("randomizeLocomotionPhase").boolValue = true;
            });
            // A short dragging stride cannot cover a sprint distance without sliding
            // or comically fast playback. Keep rushes fast and make travel a shamble.
            var hunter = root.GetComponent<HunterBrain>();
            if (hunter != null) OutbreakEditor.Values(hunter, s => s.FindProperty("speed").floatValue = 1.25f);
            var runner = root.GetComponent<RunnerBrain>();
            if (runner != null) OutbreakEditor.Values(runner, s => s.FindProperty("cautionSpeed").floatValue = 1.5f);
            var ambusher = root.GetComponent<AmbusherBrain>();
            if (ambusher != null) OutbreakEditor.Values(ambusher, s => s.FindProperty("travelSpeed").floatValue = 1.5f);
            var elite = root.GetComponent<EliteBrain>();
            if (elite != null) OutbreakEditor.Values(elite, s => s.FindProperty("advanceSpeed").floatValue = 1.45f);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static AnimationClip Author(string kind, string mode, AnimationClip source)
    {
        string path = Root + "/" + kind + "_" + mode + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = Object.Instantiate(source); AssetDatabase.CreateAsset(clip, path); }
        else EditorUtility.CopySerialized(source, clip);
        clip.name = kind + "_" + mode;
        var sample = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Grounded + "/CanonicalZombie.prefab"));
        var graph = PlayableGraph.Create("Infected pose authoring");
        try
        {
            var animator = sample.GetComponent<Animator>(); animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            var playable = AnimationClipPlayable.Create(graph, source); playable.SetApplyFootIK(false); output.SetSourcePlayable(playable);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); graph.Play();
            // HumanPose expresses muscles relative to its body frame. Changing the
            // torso changes that frame: record the complete pose so the evaluated
            // legs remain where the source animation placed them.
            var properties = HumanTrait.MuscleName.Select(Property).Concat(new[] { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" }).ToArray();
            var curves = properties.Select(_ => new AnimationCurve()).ToArray();
            using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
            {
                int frames = Mathf.CeilToInt(source.length * 60);
                for (int f = 0; f <= frames; f++)
                {
                    float phase = f / (float)frames;
                    playable.SetTime((phase >= 1 ? 0 : phase) * source.length); graph.Evaluate(0);
                    Pose(animator, kind, mode, phase);
                    var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                    for (int j = 0; j < HumanTrait.MuscleCount; j++)
                    {
                        float v = pose.muscles[j];
                        if (HumanTrait.MuscleName[j].Contains("Stretched")) v = -.35f - .10f * Mathf.Sin(phase * Mathf.PI * 2);
                        curves[j].AddKey(phase * source.length, v);
                    }
                    for (int j = 0; j < 3; j++) curves[HumanTrait.MuscleCount + j].AddKey(phase * source.length, pose.bodyPosition[j]);
                    for (int j = 0; j < 4; j++) curves[HumanTrait.MuscleCount + 3 + j].AddKey(phase * source.length, pose.bodyRotation[j]);
                }
            }
            clip.ClearCurves();
            for (int j = 0; j < properties.Length; j++)
            {
                for (int k = 0; k < curves[j].length; k++) curves[j].SmoothTangents(k, 0);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), properties[j]), curves[j]);
            }
            Loop(clip); EditorUtility.SetDirty(clip); return clip;
        }
        finally { graph.Destroy(); Object.DestroyImmediate(sample); }
    }

    private static void RefineContact(AnimationClip clip, float flightLimit, string kind)
    {
        // The Elite has a taller, nonuniform visual scale: calibrate each actual
        // prefab mesh rather than assuming that canonical-height contact transfers.
        var sample = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + kind + ".prefab"));
        sample.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (var behaviour in sample.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
        var graph = PlayableGraph.Create("Infected sole calibration"); var mesh = new Mesh();
        try
        {
            var a = sample.GetComponentInChildren<Animator>(); a.runtimeAnimatorController = null;
            var output = AnimationPlayableOutput.Create(graph, "Pose", a);
            var playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyFootIK(false); output.SetSourcePlayable(playable);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); graph.Play();
            var skins = a.GetComponentsInChildren<SkinnedMeshRenderer>();
            var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y");
            for (int pass = 0; pass < 2; pass++)
            {
                var previous = AnimationUtility.GetEditorCurve(clip, binding); var curve = new AnimationCurve();
                int steps = Mathf.CeilToInt(clip.length * 60);
                for (int i = 0; i <= steps; i++)
                {
                    float time = i * clip.length / steps; playable.SetTime(time); graph.Evaluate(0);
                    float low = float.PositiveInfinity;
                    foreach (var skin in skins)
                    {
                        skin.BakeMesh(mesh);
                        foreach (var v in mesh.vertices) low = Mathf.Min(low, skin.transform.TransformPoint(v).y);
                    }
                    curve.AddKey(time, previous.Evaluate(time) + (Mathf.Clamp(low, .004f, flightLimit) - low) / (a.humanScale * a.transform.lossyScale.y));
                }
                var keys = curve.keys; keys[keys.Length - 1].value = keys[0].value; curve.keys = keys;
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(clip, binding, curve);
                graph.DestroyPlayable(playable); playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false); output.SetSourcePlayable(playable);
            }
            EditorUtility.SetDirty(clip);
        }
        finally { graph.Destroy(); Object.DestroyImmediate(mesh); Object.DestroyImmediate(sample); }
    }

    private static string Property(string name)
    {
        foreach (string side in new[] { "Left", "Right" }) foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
        { string prefix = side + " " + finger + " "; if (name.StartsWith(prefix)) return side + "Hand." + finger + "." + name.Substring(prefix.Length); }
        return name;
    }
    private static void Loop(AnimationClip clip)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; settings.loopBlend = true;
        // These are explicit HumanPose body curves, not imported FBX root tracks.
        // Feet-based root extraction would subtract body height again in a controller.
        settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true; settings.heightFromFeet = false;
        settings.loopBlendOrientation = true; settings.loopBlendPositionY = true; settings.loopBlendPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
    }

    private static void Pose(Animator a, string kind, string mode, float t)
    {
        var root = a.transform; float p = t * Mathf.PI * 2;
        bool runner = kind == "Runner", ambusher = kind == "Ambusher", elite = kind == "Elite", idle = mode == "Idle";
        float uneven = Mathf.Sin(p) * .7f + Mathf.Sin(p * 2 + .7f) * .3f;
        float lean = runner ? 19 : ambusher ? 24 : elite ? 11 : 16;
        if (idle) lean *= .8f;
        Rotate(a, HumanBodyBones.Spine, root.right, lean * .6f + uneven * 1.4f);
        Rotate(a, HumanBodyBones.Chest, root.right, lean * .4f);
        Rotate(a, HumanBodyBones.Chest, root.forward, (elite ? -3 : 4) + uneven * (runner ? 1.8f : 3));
        Rotate(a, HumanBodyBones.Head, root.right, -lean * .42f);
        Rotate(a, HumanBodyBones.Head, root.forward, (ambusher ? -7 : 5) + Mathf.Sin(p + .8f) * 2);
        Rotate(a, HumanBodyBones.Head, root.up, Mathf.Sin(p + .3f) * (idle ? 6 : 3));
        // Asymmetric reaching and dragging replaces the healthy reciprocal arm pump.
        float motion = idle ? .25f : 1;
        Arm(a, true, new Vector3(-.10f, runner ? -.23f : ambusher ? -.32f : -.36f,
            runner ? .34f : ambusher ? .30f : elite ? .13f : .25f) + new Vector3(0, uneven * .035f * motion, Mathf.Sin(p + .6f) * .045f * motion));
        Arm(a, false, new Vector3(.10f, runner ? -.31f : ambusher ? -.42f : -.53f,
            runner ? .22f : ambusher ? .20f : elite ? .06f : .09f) + new Vector3(0, Mathf.Sin(p + 1.3f) * .025f * motion, uneven * .035f * motion));
        Rotate(a, HumanBodyBones.LeftHand, root.right, 17 + uneven * 4);
        Rotate(a, HumanBodyBones.RightHand, root.right, 25 - uneven * 3);
    }
    private static void Rotate(Animator a, HumanBodyBones bone, Vector3 axis, float degrees)
    { var t = a.GetBoneTransform(bone); if (t != null) t.rotation = Quaternion.AngleAxis(degrees, axis) * t.rotation; }
    private static void Arm(Animator a, bool left, Vector3 offset)
    {
        var upper = a.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        var lower = a.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        var hand = a.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        Vector3 start = upper.position, target = start + a.transform.TransformDirection(offset);
        float l1 = Vector3.Distance(start, lower.position), l2 = Vector3.Distance(lower.position, hand.position);
        float distance = Mathf.Clamp(Vector3.Distance(start, target), .08f, (l1 + l2) * .96f);
        Vector3 direction = (target - start).normalized;
        float along = (l1 * l1 + distance * distance - l2 * l2) / (2 * distance);
        float height = Mathf.Sqrt(Mathf.Max(0, l1 * l1 - along * along));
        Vector3 pole = a.transform.TransformDirection(new Vector3(left ? -.5f : .5f, -.2f, -.15f));
        Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
        Vector3 elbow = start + direction * along + bend * height;
        upper.rotation = Quaternion.FromToRotation(lower.position - start, elbow - start) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, start + direction * distance - lower.position) * lower.rotation;
    }

    private static float MeasureStride(AnimationClip clip)
    {
        var sample = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Grounded + "/CanonicalZombie.prefab"));
        var controller = new AnimatorOverrideController(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Grounded + "/GroundedEnemies.controller"));
        try
        {
            controller["GroundedChase"] = clip; var a = sample.GetComponent<Animator>(); a.runtimeAnimatorController = controller;
            a.Rebind(); a.SetFloat("Speed", 4); a.SetFloat("GaitScale", 1);
            var points = new Vector3[2, 120]; var speeds = new List<float>();
            for (int i = 0; i < 120; i++)
            {
                a.Play("Chase", 0, i / 120f); a.Update(0);
                points[0, i] = a.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                points[1, i] = a.GetBoneTransform(HumanBodyBones.RightFoot).position;
            }
            for (int foot = 0; foot < 2; foot++)
            {
                float low = Enumerable.Range(0, 120).Min(i => points[foot, i].y);
                for (int i = 1; i < 119; i++) if (points[foot, i].y < low + .05f)
                {
                    float v = -(points[foot, i].z - points[foot, i - 1].z) / (clip.length / 120);
                    if (v > .05f) speeds.Add(v);
                }
            }
            if (speeds.Count < 4) throw new InvalidOperationException("Cannot calibrate foot travel: " + clip.name);
            speeds.Sort(); return speeds[speeds.Count / 2];
        }
        finally { Object.DestroyImmediate(sample); Object.DestroyImmediate(controller); }
    }
}
