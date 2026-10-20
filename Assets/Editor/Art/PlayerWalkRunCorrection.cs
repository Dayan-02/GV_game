using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>Repairs only the existing player's exploration gait assets and blend settings.</summary>
public static class PlayerWalkRunCorrection
{
    const string Prefab = "Assets/Prefabs/Player/PlayerPhase3.prefab";
    const string Root = "Assets/Animations/PlayerGrounded";
    const string ControllerPath = "Assets/Animations/StealthLocomotion/Survivor.controller";
    const string OverridesPath = "Assets/Animations/StealthLocomotion/ReferenceSurvivor.overrideController";

    [MenuItem("Tools/Dead Zone/Correct existing player walk and run")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play Mode before editing animation assets.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ExistingCityEnhancer.ScenePath)
            throw new InvalidOperationException("Open the enhanced ZombieMap scene first.");
        var player = Object.FindAnyObjectByType<ThirdPersonPlayerController>();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridesPath);
        if (player == null || controller == null || overrides == null ||
            player.GetComponentInChildren<Animator>().runtimeAnimatorController != overrides)
            throw new InvalidOperationException("The expected existing player/Animator is not active.");
        var state = controller.layers[0].stateMachine.states.First(s => s.state.name == "Locomotion").state;
        var tree = state.motion as BlendTree;
        if (tree == null || tree.children.Length != 4 || tree.blendParameter != "Speed")
            throw new InvalidOperationException("Inspect the changed locomotion tree before applying this repair.");

        // Use the shared source T-pose and native foot-goal tracks. Never rebake
        // the old per-clip Avatars or their travelling body curves.
        PlayerSourceMotionImport.Import();
        var idle = PlayerInPlaceMotion.Create("NaturalIdle", "Idle", true);
        var walk = PlayerInPlaceMotion.Create("NaturalWalk", "WalkForwards", true);
        var run = PlayerInPlaceMotion.Create("NaturalRun", "RunForwards", false);
        var sprint = PlayerInPlaceMotion.Create("NaturalSprint", "SprintForwards", false);
        var clips = new[] { walk, run, sprint };
        var samples = clips.Select(Sample).ToArray();
        float[] speeds = { player.WalkSpeed, player.RunSpeed, player.SprintSpeed };
        var children = tree.children;
        var report = new StringBuilder("Existing controller, model, motor speeds and camera retained.\n");
        for (int i = 0; i < 3; i++)
        {
            float rate = speeds[i] / samples[i].StanceSpeed;
            if (samples[i].MaximumHipX - samples[i].MinimumHipX > .10f)
                throw new InvalidOperationException(clips[i].name + " has excessive sideways body motion.");
            if (rate < .65f || rate > 1.35f)
                throw new InvalidOperationException(clips[i].name + " needs a different source clip; refusing extreme playback rate " + rate);
            children[i + 1].threshold = i + 1;
            children[i + 1].timeScale = rate;
            children[i + 1].cycleOffset = i == 0 ? 0f : AlignPhase(samples[0], samples[i]);
            report.AppendLine(clips[i].name + ": source stance=" + samples[i].StanceSpeed.ToString("F4") +
                " m/s; motor=" + speeds[i] + "; playback=" + rate.ToString("F4") +
                "; phase=" + children[i + 1].cycleOffset.ToString("F4") +
                "; lowest sole=" + samples[i].MinimumSole.ToString("F4") + ".." + samples[i].MaximumSole.ToString("F4") +
                "; hip lateral span=" + (samples[i].MaximumHipX - samples[i].MinimumHipX).ToString("F4") + " m");
        }
        children[0].threshold = 0;
        tree.useAutomaticThresholds = false;
        tree.children = children;
        overrides["Idle"] = idle;
        overrides["WalkForward"] = walk;
        overrides["Run"] = run;
        overrides["Sprint"] = sprint;
        // Preserve the original knee/ankle movement. The native goal tracks are
        // available to the existing small terrain helper; no second IK pass here.
        state.iKOnFeet = false;
        state.speed = 1f;
        state.speedParameterActive = false;
        Configure(player.gameObject);
        var prefab = PrefabUtility.LoadPrefabContents(Prefab);
        try { Configure(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, Prefab); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(overrides);
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/WalkRunCorrection.txt", report.ToString());
    }

    static void Configure(GameObject player)
    {
        var animator = player.GetComponentInChildren<Animator>();
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // This damping controls the directional aim/crouch trees. Exploration
        // Speed now follows the motor's already-smoothed measured displacement.
        var driver = new SerializedObject(player.GetComponent<PlayerAnimatorDriver>());
        driver.FindProperty("parameterDampTime").floatValue = .08f;
        driver.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(animator);
    }

    sealed class Gait
    {
        public float StanceSpeed, MinimumSole, MaximumSole;
        public float MinimumHipX = float.PositiveInfinity, MaximumHipX = float.NegativeInfinity;
        public float[] Phase = new float[120];
    }

    static Gait Sample(AnimationClip clip)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
        foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var a = go.GetComponentInChildren<Animator>();
        a.transform.localPosition = Vector3.zero;
        a.runtimeAnimatorController = null; a.applyRootMotion = false;
        var graph = PlayableGraph.Create("Player gait calibration");
        var mesh = new Mesh();
        try
        {
            var output = AnimationPlayableOutput.Create(graph, "Pose", a);
            var playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyFootIK(false);
            output.SetSourcePlayable(playable); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); graph.Play();
            var boots = a.GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.name.Contains("Boots"));
            var weights = boots.sharedMesh.boneWeights;
            var leftVertices = weights.Select(w => boots.bones[w.boneIndex0].name.EndsWith("_l")).ToArray();
            var sole = new float[2, 121]; var foot = new Vector3[2, 121];
            var result = new Gait { MinimumSole = float.PositiveInfinity, MaximumSole = float.NegativeInfinity };
            for (int i = 0; i <= 120; i++)
            {
                playable.SetTime(i * clip.length / 120d); graph.Evaluate(0); boots.BakeMesh(mesh);
                sole[0, i] = sole[1, i] = float.PositiveInfinity;
                var vertices = mesh.vertices;
                for (int v = 0; v < vertices.Length; v++)
                {
                    int side = leftVertices[v] ? 0 : 1;
                    sole[side, i] = Mathf.Min(sole[side, i], boots.transform.TransformPoint(vertices[v]).y);
                }
                float lowest = Mathf.Min(sole[0, i], sole[1, i]);
                result.MinimumSole = Mathf.Min(result.MinimumSole, lowest);
                result.MaximumSole = Mathf.Max(result.MaximumSole, lowest);
                float hipX = a.GetBoneTransform(HumanBodyBones.Hips).position.x;
                result.MinimumHipX = Mathf.Min(result.MinimumHipX, hipX);
                result.MaximumHipX = Mathf.Max(result.MaximumHipX, hipX);
                foot[0, i] = a.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                foot[1, i] = a.GetBoneTransform(HumanBodyBones.RightFoot).position;
                if (i < 120) result.Phase[i] = foot[0, i].z - foot[1, i].z;
            }
            var contactSpeeds = new List<float>();
            for (int side = 0; side < 2; side++)
                for (int i = 1; i < 120; i++)
                {
                    float backwardSpeed = -(foot[side, i].z - foot[side, i - 1].z) / (clip.length / 120);
                    // Measure only the supporting foot travelling backward through
                    // stance. Absolute velocity also counts a fast returning foot.
                    if (sole[side, i] < .035f && sole[side, i - 1] < .035f && backwardSpeed > .15f)
                        contactSpeeds.Add(backwardSpeed);
                }
            if (contactSpeeds.Count < 8) throw new InvalidOperationException("No usable stance phase in " + clip.name);
            contactSpeeds.Sort(); result.StanceSpeed = contactSpeeds[contactSpeeds.Count / 2];
            return result;
        }
        finally { graph.Destroy(); Object.DestroyImmediate(mesh); Object.DestroyImmediate(go); }
    }

    static float AlignPhase(Gait reference, Gait candidate)
    {
        float best = float.NegativeInfinity; int offset = 0;
        for (int phase = 0; phase < 120; phase++)
        {
            float score = 0;
            for (int i = 0; i < 120; i++) score += reference.Phase[i] * candidate.Phase[(i + phase) % 120];
            if (score > best) { best = score; offset = phase; }
        }
        return offset / 120f;
    }
}
