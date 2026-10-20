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
