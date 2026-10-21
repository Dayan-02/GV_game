# Player shaking and knee correction — 15 October 2026

## Result and scope

Corrected the existing player exploration idle, walk, run and sprint. The P2 Survivalist model, movement controller, inputs, camera, weapon setup, existing Animator and city layout remain in place. The earlier correction did not resolve the shaking; this revision replaces its affected clips.

## Confirmed causes

1. **Inconsistent animation Avatars.** Each source animation FBX had been assigned Create From This Model from its posed skeleton. The run's right-foot twist curve ranged approximately -4.20 to +0.19 in normalized muscle units. With Unity's original shared source T-pose, it ranges -0.374 to -0.021. The source rig is now shared by private player-only motion copies.
2. **Forward travel remained inside the body curves.** Marking XZ as baked into pose did not remove the recorded forward trajectory. It produced large lateral body displacement as the body turned during the stride. The old bake preserved that displacement. The new conversion subtracts the linear XZ trajectory, including its tangent slope, before retargeting. It preserves the small authored body sway.
3. **The previous bake discarded native foot-goal tracks.** The new clips clone native Humanoid data, preserving knee/ankle muscle and foot-goal tracks. Boot contact is fitted through body height, without synthesizing knee rotations or forcing airborne running feet down.

The user's live before-recording contains 946 frames over 10.0065 seconds. The root stayed at Y=0.0800 m and grounded throughout, while the hips ranged X=-0.1614 to +0.4553 m (0.6167 m sideways). Frame intervals were 6.5–21.6 ms. This identifies animation displacement as the visible shaking; it is not a claim that every possible terrain/camera issue has been ruled out.

## Exact current settings

Controller: `D:/GV project/Assets/Animations/StealthLocomotion/Survivor.controller`.

Overrides: `D:/GV project/Assets/Animations/StealthLocomotion/ReferenceSurvivor.overrideController`.

State: Base Layer → Locomotion, 1D `Speed` blend tree.

| Child | Clip | Threshold | Playback | Cycle offset | Motor speed |
|---|---|---:|---:|---:|---:|
| Idle | NaturalIdle | 0 | 1 | 0 | 0 |
| Walk | NaturalWalk | 1 | 1.0423 | 0 | 1.6 m/s |
| Run | NaturalRun | 2 | 1.1547 | 0.025 | 3.6 m/s |
| Sprint | NaturalSprint | 3 | 1.3353 | 0 | 6 m/s |

- Animator: Apply Root Motion off, Update Mode Normal, Culling Always Animate, global speed 1. CharacterController owns world movement.
- Locomotion state speed 1, speed-parameter control off, built-in Foot IK off. Native source knee/ankle poses drive the stride. Existing terrain helper and base-layer IK Pass retained.
- `Speed` uses actual measured movement; the motor already smooths acceleration/braking. Direction parameters retain 0.08 s damping.
- Source imports: Humanoid, Copy From Other Avatar → original defaultmale_rigAvatar; compression off; Loop Time/Loop Pose on; rotation/Y/XZ baked; rotation based on Original, source Y based on Feet.
- Output clips: native Humanoid data with detrended RootT.x/z, calibrated RootT.y and LeftFootContact/RightFootContact curves. Original orientation and positions retained, Height From Feet off after calibration; loop blending on.
- Aim, crouch, jump, weapon actions and enemy animation assets are unchanged by this revision.

## Measurements on the actual player rig

| Motion | Lateral hip span | Lowest boot sole across the cycle | Source knee flexion, left / right |
|---|---:|---:|---|
| Walk | 0.0445 m | 0.0040–0.0040 m | 7.9–69.4° / 9.3–69.4° |
| Run | 0.0122 m | 0.0036–0.1071 m | 27.4–130.0° / 31.8–124.2° |
| Sprint | 0.0479 m | 0.0030–0.1508 m | 18.7–134.7° / 17.7–130.3° |

Zero degrees means a straight knee. Higher sole values during running/sprinting are the airborne phase. These are authored-pose measurements, not measurements of every terrain condition. A generation guard rejects lateral hip spans over 0.10 m and extreme cadence multipliers.

## Files and setup

- `D:/GV project/Assets/Editor/Art/PlayerSourceMotionImport.cs`: imports private player motion copies against the original source Avatar; no GameObject component required.
- `D:/GV project/Assets/Editor/Art/PlayerInPlaceMotion.cs`: preserves native animation tracks, removes horizontal travel and fits boot sole height; Editor-only.
- `D:/GV project/Assets/Editor/Art/PlayerWalkRunCorrection.cs`: reuses the active controller, calibrates stride/phase and saves the player prefab and enhanced scene. Menu: Tools → Dead city → Correct existing player walk and run.
- `D:/GV project/Assets/Scripts/Player/PlayerAnimatorDriver.cs`: existing player-root component; referenced Animator, motor, aim and health remain assigned. Its direct measured-Speed behavior from the preceding revision remains.
- `D:/GV project/Assets/Animations/PlayerSourceRig/`: original reference rig, private animation FBXs, source hashes and Unity Companion license notice. No package purchase or package upgrade.
- `D:/GV project/Assets/Animations/PlayerGrounded/NaturalIdle.anim`, `NaturalWalk.anim`, `NaturalRun.anim`, `NaturalSprint.anim`: regenerated native assets, existing gait GUIDs retained.

## Evidence, build and review limits

- Before gameplay telemetry: `Logs/PlayerJitterBefore.csv`.
- Source rig comparison: `Logs/SharedRigInspection.txt`, `Logs/InPlaceRigInspection.txt`, `Logs/DetrendedRootInspection.txt`.
- Final gait calibration: `Logs/WalkRunCorrection.txt`.
- Three-angle preview: `Previews/PlayerJitter-2026-10-15/CorrectedPlayerMovement.mp4`, 240 frames / 8 seconds. Shows the current Animator over a ground grid from side/front/rear. Physics and runtime scripts are disabled in this authoring preview; it is not a substitute for gameplay observation.
- Windows x64 build: succeeded, 0 errors, 1 warning, 480,276,492 bytes. `Logs/PlayerJitterBuildSummary.txt`. Updated executable: `Builds/Windows/DeadCity.exe`; updated distribution: `Builds/ZombieOutbreak-Windows.zip`.
- Main Editor hit a Direct3D 12 graphics crash during import; the isolated preview also initially crashed while the city editor was open. Reopened using Direct3D 11; closing the main Editor during the isolated render completed all 240 frames successfully. No player-facing renderer settings were changed.
- The user completed the live walk/run review and reported: done and it looks smooth. The after-recorder ended with zero frames, so no measured after-gameplay telemetry is claimed. The numerical after-values above come from native authoring samples. No automated gameplay suite was added or run.
- Original map SHA-256 remains `BC1D62E75E604F47164C248A4C1F09E50D8952B122BD3C029824FFEDADB31431`.

## Manual review

1. Resume the corrected Unity scene or start the updated Windows build.
2. Walk with W, run with W + Left Shift, sprint with Left Control. Watch the hips stay centered and knees bend on each returning foot.
3. Transition walk → run → walk → stop; check loop boundaries and deceleration. Check a side view as well as the normal camera.
4. Walk into a wall, then try a curb and a turn. The latter cases require terrain/gameplay review beyond the flat-floor authoring preview.

## Troubleshooting

- **Old shaking still appears:** ensure the Animator uses ReferenceSurvivor.overrideController and its Idle/WalkForward/Run/Sprint overrides point to NaturalIdle/NaturalWalk/NaturalRun/NaturalSprint. Restart an already-running standalone executable to load the rebuilt data.
- **Turned-out or twisting foot after reimport:** ensure the private source FBXs use Copy From Other Avatar with defaultmale_rigAvatar. Do not auto-create a different Avatar from each posed animation file.
- **Large sideways sway:** do not feed travelling RootT curves back into the old HumanPose bake. Use PlayerInPlaceMotion's trajectory removal before sampling the character.
- **Skating after changing motor speed:** recalibrate with the repair menu. Do not change global Animator.speed, which also changes combat timings.
- **Feet collapse with IK:** do not enable built-in Foot IK indiscriminately on other legacy muscle-only clips; some lack native foot goals.

## Backup and Git

Backup before this revision: `Backups/BeforePlayerJitterRepair-2026-10-15/PlayerAssets.zip`.

Suggested commits:

- `fix(animation): share source avatar and remove locomotion root travel`
- `fix(player): preserve knee motion and calibrate walk run sprint contact`

Commit animation/rig assets, controller/override, prefab/scene, Editor scripts and their .meta files together. Keep Library, Logs and temporary preview frames out of Git.

Rig reference: [Unity humanoid import documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/ConfiguringtheAvatar.html). Asset provenance and original source metadata are in PlayerSourceRig/README.md.

## Complete current scripts

### PlayerSourceMotionImport.cs

Full file path: `D:/GV project/Assets/Editor/Art/PlayerSourceMotionImport.cs`

```csharp
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Imports player motion using the original animation skeleton's shared T-pose.</summary>
public static class PlayerSourceMotionImport
{
    public const string Root = "Assets/Animations/PlayerSourceRig";
    public static void Import()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        string rigPath = Root + "/defaultmale_rig.fbx";
        AssetDatabase.ImportAsset(rigPath, ImportAssetOptions.ForceSynchronousImport);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(rigPath).OfType<Avatar>().Single();
        if (!avatar.isHuman || !avatar.isValid) throw new InvalidOperationException("Invalid source rig.");
        foreach (string name in new[] { "Idle", "WalkForwards", "RunForwards", "SprintForwards" })
        {
            string path = Root + "/m@" + name + ".fbx";
            if (!File.Exists(path))
                File.Copy("Assets/Art/ImportedCharacters/UnityMotion/m@" + name + ".fbx", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = "SharedRig" + name;
                clip.loopTime = true;
                clip.loopPose = true;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = false;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = false;
                clip.heightFromFeet = true;
                clip.cycleOffset = 0;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
    }
}

```

### PlayerInPlaceMotion.cs

Full file path: `D:/GV project/Assets/Editor/Art/PlayerInPlaceMotion.cs`

```csharp
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>Preserves native Humanoid motion/IK tracks while removing travel for the CharacterController.</summary>
public static class PlayerInPlaceMotion
{
    public static AnimationClip Create(string name, string sourceName, bool grounded)
    {
        string sourcePath = PlayerSourceMotionImport.Root + "/m@" + sourceName + ".fbx";
        var source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview"));
        string path = "Assets/Animations/PlayerGrounded/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = Object.Instantiate(source); AssetDatabase.CreateAsset(clip, path); }
        else EditorUtility.CopySerialized(source, clip);
        clip.name = name;
        // Bake Into Pose alone leaves the travelling body curve in the clip. As
        // the body yaws, that metre-scale travel can become sideways motion.
        // Remove the straight trajectory before evaluating/retargeting the pose.
        foreach (string axis in new[] { "x", "z" })
        {
            var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT." + axis);
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            float first = curve.Evaluate(0), slope = (curve.Evaluate(clip.length) - first) / clip.length;
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value -= first + slope * keys[i].time;
                keys[i].inTangent -= slope;
                keys[i].outTangent -= slope;
            }
            curve.keys = keys;
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true; settings.loopBlend = true;
        settings.loopBlendOrientation = true;
        settings.loopBlendPositionY = true; settings.loopBlendPositionXZ = true;
        settings.keepOriginalPositionXZ = true; settings.keepOriginalPositionY = true;
        settings.keepOriginalOrientation = true; settings.heightFromFeet = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        FitBootContact(clip, grounded);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void FitBootContact(AnimationClip clip, bool grounded)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerPhase3.prefab"));
        foreach (var b in go.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var animator = go.GetComponentInChildren<Animator>();
        animator.transform.localPosition = Vector3.zero;
        animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var graph = PlayableGraph.Create("Fit player boot contact");
        var mesh = new Mesh();
        try
        {
            var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); graph.Play();
            var boots = animator.GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.name.Contains("Boots"));
            bool[] left = boots.sharedMesh.boneWeights.Select(w => boots.bones[w.boneIndex0].name.EndsWith("_l")).ToArray();
            int steps = Mathf.CeilToInt(clip.length * 120);
            var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y");
            // Correct boot thickness through body height only. The original knee,
            // ankle, toe and IK-goal tracks remain intact. Preserve airborne run poses.
            for (int pass = 0; pass < 3; pass++)
            {
                var p = AnimationClipPlayable.Create(graph, clip); p.SetApplyFootIK(false);
                output.SetSourcePlayable(p);
                var previous = AnimationUtility.GetEditorCurve(clip, binding);
                var height = new AnimationCurve();
                var contact = new[] { new AnimationCurve(), new AnimationCurve() };
                for (int f = 0; f < steps; f++)
                {
                    float time = f * clip.length / steps;
                    p.SetTime(time); graph.Evaluate(0); boots.BakeMesh(mesh);
                    float[] sole = { float.PositiveInfinity, float.PositiveInfinity };
                    var vertices = mesh.vertices;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        int side = left[v] ? 0 : 1;
                        sole[side] = Mathf.Min(sole[side], boots.transform.TransformPoint(vertices[v]).y);
                    }
                    float lowest = Mathf.Min(sole[0], sole[1]);
                    float correction = (grounded ? .004f : Mathf.Max(.004f, lowest)) - lowest;
                    height.AddKey(time, previous.Evaluate(time) + correction / animator.humanScale);
                    for (int side = 0; side < 2; side++)
                        contact[side].AddKey(time, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.015f, .065f, sole[side] + correction)));
                }
                CloseCurve(height, clip.length);
                if (pass < 2) AnimationUtility.SetEditorCurve(clip, binding, height);
                else
                {
                    for (int side = 0; side < 2; side++)
                    {
                        CloseCurve(contact[side], clip.length);
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), side == 0 ? "LeftFootContact" : "RightFootContact"), contact[side]);
                    }
                }
                graph.DestroyPlayable(p);
            }
        }
        finally { graph.Destroy(); Object.DestroyImmediate(mesh); Object.DestroyImmediate(go); }
    }

    static void CloseCurve(AnimationCurve curve, float duration)
    {
        curve.AddKey(duration, curve.keys[0].value);
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
    }
}

```

### PlayerWalkRunCorrection.cs

Full file path: `D:/GV project/Assets/Editor/Art/PlayerWalkRunCorrection.cs`

```csharp
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

    [MenuItem("Tools/Dead city/Correct existing player walk and run")]
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

```

### PlayerAnimatorDriver.cs

Full file path: `D:/GV project/Assets/Scripts/Player/PlayerAnimatorDriver.cs`

```csharp
using UnityEngine;

/// <summary>Translates controller state into Animator parameters and exposes animation-only action hooks.</summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(ThirdPersonPlayerController))]
public sealed class PlayerAnimatorDriver : MonoBehaviour
{
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private ThirdPersonPlayerController movement;
    [SerializeField]
    private PlayerAimController aim;
    [SerializeField]
    private PlayerHealth health;
    [SerializeField, Min(0f)]
    private float parameterDampTime = 0.1f;
    private int actionsLayer = -1;
    private bool deathRequested;
    private PlayerStanceController stance;
    private bool supportsCrouch;
    private float previousHealth;
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
        stance=GetComponent<PlayerStanceController>();
        if (movement == null)
            movement = GetComponent<ThirdPersonPlayerController>();
        if (aim == null)
            aim = GetComponent<PlayerAimController>();
        if (health == null)
            health = GetComponent<PlayerHealth>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("PlayerAnimatorDriver needs an Animator with the Phase 2 controller.", this);
            enabled = false;
            return;
        }

        animator.applyRootMotion = false;
        foreach(var parameter in animator.parameters)if(parameter.name=="IsCrouching")supportsCrouch=true;
        actionsLayer = animator.GetLayerIndex("Upper Body Actions");
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Died += PlayDeath;
            health.HealthChanged += OnHealthChanged;
            previousHealth = health.CurrentHealth;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= PlayDeath;
            health.HealthChanged -= OnHealthChanged;
        }
    }

    private void Update()
    {
        if (health != null && health.IsDead && !deathRequested)
            PlayDeath();
        bool dead = deathRequested || (health != null && health.IsDead);
        bool aiming = aim != null && aim.IsAiming && !dead;
        Vector3 localVelocity = transform.InverseTransformDirection(movement.PlanarVelocity);
        float referenceSpeed = aiming ? aim.AimMoveSpeed : movement.WalkSpeed;
        bool crouching=stance!=null&&stance.IsCrouching&&!dead;
        if(crouching)referenceSpeed=stance.CrouchedSpeed;
        Vector2 local = Vector2.ClampMagnitude(new Vector2(localVelocity.x, localVelocity.z) / Mathf.Max(.01f, referenceSpeed), 1f);
        float dt = Time.deltaTime;
        // Speed uses semantic knots: idle 0, walk 1, run 2, sprint 3.
        float normalizedSpeed = NormalizeSpeed(movement.PlanarSpeed);
        // The motor already accelerates/brakes smoothly. Smoothing Speed a second
        // time leaves the feet walking after the capsule slows or hits a wall.
        // Keep gait weights tied to measured displacement; damp direction only.
        animator.SetFloat(Speed, dead ? 0f : normalizedSpeed);
        animator.SetFloat(Horizontal, dead ? 0f : local.x, parameterDampTime, dt);
        animator.SetFloat(Vertical, dead ? 0f : local.y, parameterDampTime, dt);
        animator.SetBool(IsAiming, aiming);
        animator.SetBool(IsGrounded, movement.IsGrounded);
        animator.SetBool(IsSprinting, movement.IsSprinting && !dead);
        animator.SetFloat(VerticalVelocity, movement.VerticalVelocity);
        animator.SetBool(IsDead, dead);
        if(supportsCrouch)animator.SetBool("IsCrouching",crouching);
        if (actionsLayer >= 0)
        {
            bool active = !animator.GetCurrentAnimatorStateInfo(actionsLayer).IsName("Empty") || (animator.IsInTransition(actionsLayer) && !animator.GetNextAnimatorStateInfo(actionsLayer).IsName("Empty"));
            float weight = Mathf.MoveTowards(animator.GetLayerWeight(actionsLayer), !dead && active ? 1f : 0f, dt / 0.08f);
            animator.SetLayerWeight(actionsLayer, dead ? 0f : weight);
        }
    }

    private float NormalizeSpeed(float speed)
    {
        if (speed <= movement.WalkSpeed)
            return speed / movement.WalkSpeed;
        if (speed <= movement.RunSpeed)
            return 1f + Mathf.InverseLerp(movement.WalkSpeed, movement.RunSpeed, speed);
        return 2f + Mathf.InverseLerp(movement.RunSpeed, movement.SprintSpeed, speed);
    }

    // These methods animate only. They do not deal damage, spawn objects, consume ammo, or read firing keys.
    public void PlayFire()
    {
        if (CanPlayAction())
            animator.SetTrigger(Fire);
    }

    public void PlayReload(float duration = 1.2f)
    {
        if (CanPlayAction())
        {
            animator.SetFloat("ReloadSpeed", 1.2f / Mathf.Max(.05f, duration));
            animator.SetTrigger(Reload);
        }
    }

    public void PlayHit()
    {
        if (CanPlayAction())
            animator.SetTrigger(Hit);
    }

    private void OnHealthChanged(float current, float maximum)
    {
        if (animator == null)
            return;
        bool reloading = actionsLayer >= 0 && (animator.GetCurrentAnimatorStateInfo(actionsLayer).IsName("Reload") || animator.GetNextAnimatorStateInfo(actionsLayer).IsName("Reload"));
        if (current > 0 && current < previousHealth && !reloading)
            PlayHit();
        previousHealth = current;
    }

    public void CancelWeaponAction()
    {
        if (animator == null)
            return;
        animator.ResetTrigger(Fire);
        animator.ResetTrigger(Reload);
        if (actionsLayer >= 0)
        {
            animator.Play("Empty", actionsLayer, 0f);
            animator.SetLayerWeight(actionsLayer, 0f);
        }
    }

    public void PlayDeath()
    {
        if (animator == null)
            return;
        deathRequested = true;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime; // Finish death behind the frozen Game Over panel.
        animator.ResetTrigger(Fire);
        animator.ResetTrigger(Reload);
        animator.ResetTrigger(Hit);
        animator.SetBool(IsDead, true);
        animator.SetTrigger(Death);
    }

    private bool CanPlayAction() => animator != null && isActiveAndEnabled && !deathRequested && (health == null || !health.IsDead);
    [ContextMenu("Animation Preview/Fire (Play Mode)")]
    private void PreviewFire()
    {
        if (Application.isPlaying)
            PlayFire();
    }

    [ContextMenu("Animation Preview/Reload (Play Mode)")]
    private void PreviewReload()
    {
        if (Application.isPlaying)
            PlayReload();
    }

    [ContextMenu("Animation Preview/Hit (Play Mode)")]
    private void PreviewHit()
    {
        if (Application.isPlaying)
            PlayHit();
    }

    [ContextMenu("Animation Preview/Death via PlayerHealth (Play Mode)")]
    private void PreviewDeath()
    {
        if (Application.isPlaying && health != null)
            health.Die();
    }
}

```

