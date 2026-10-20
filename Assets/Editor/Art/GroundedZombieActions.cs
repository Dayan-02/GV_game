using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Re-encodes the vendor's action poses against the corrected forward-facing Avatar.</summary>
public static class GroundedZombieActions
{
    public static AnimationClip Bake(string name, AnimationClip source, string outputRoot = "Assets/Animations/GroundedEnemies")
    {
        const string root = "Assets/Animations/GroundedEnemies";
        var original = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ImportedCharacterSetup.Zombie));
        var corrected = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(root + "/CanonicalZombie.prefab"));
        var overrides = new AnimatorOverrideController(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(root + "/GroundedEnemies.controller"));
        try
        {
            var from = original.GetComponent<Animator>();
            var to = corrected.GetComponent<Animator>(); to.enabled = false;
            original.transform.position = Vector3.zero; original.transform.rotation = Quaternion.identity;
            corrected.transform.position = Vector3.zero; corrected.transform.rotation = Quaternion.identity;
            from.cullingMode = AnimatorCullingMode.AlwaysAnimate; from.applyRootMotion = false;
            overrides["GroundedChase"] = source;
            from.runtimeAnimatorController = overrides; from.Rebind();
            from.SetFloat("Speed", 4); from.SetFloat("GaitScale", 1);
            var sourceBones = original.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
            var targetBones = corrected.GetComponentsInChildren<Transform>().Where(t => sourceBones.ContainsKey(t.name)).ToArray();
            var curves = Enumerable.Range(0, HumanTrait.MuscleCount + 7).Select(_ => new AnimationCurve()).ToArray();
            int steps = Mathf.CeilToInt(source.length * 60);
            Vector3 origin = Vector3.zero;
            using (var handler = new HumanPoseHandler(to.avatar, to.transform))
            {
                for (int i = 0; i <= steps; i++)
                {
                    float time = i * source.length / steps;
                    from.Play("Chase", 0, Mathf.Min(.99999f, i / (float)steps)); from.Update(.00001f);
                    foreach (var bone in targetBones)
                    {
                        bone.localPosition = sourceBones[bone.name].localPosition;
                        bone.localRotation = sourceBones[bone.name].localRotation;
                    }
                    var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                    if (i == 0) origin = new Vector3(pose.bodyPosition.x, 0, pose.bodyPosition.z);
                    pose.bodyPosition -= origin;
                    for (int j = 0; j < HumanTrait.MuscleCount; j++) curves[j].AddKey(time, pose.muscles[j]);
                    int offset = HumanTrait.MuscleCount;
                    for (int j = 0; j < 3; j++) curves[offset+j].AddKey(time, pose.bodyPosition[j]);
                    for (int j = 0; j < 4; j++) curves[offset+3+j].AddKey(time, pose.bodyRotation[j]);
                }
            }
            string path = outputRoot + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves(); clip.name = name; clip.frameRate = 60;
            var properties = HumanTrait.MuscleName.Concat(new[] { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" }).ToArray();
            for (int j = 0; j < properties.Length; j++)
            {
                string property = properties[j];
                foreach (string side in new[] { "Left", "Right" })
                    foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                    {
                        string prefix = side + " " + finger + " ";
                        if (property.StartsWith(prefix)) property = side + "Hand." + finger + "." + property.Substring(prefix.Length);
                    }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curves[j]);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0; settings.stopTime = source.length; settings.loopTime = false;
            settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true; settings.heightFromFeet = false;
            settings.loopBlendOrientation = true; settings.loopBlendPositionY = true; settings.loopBlendPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
            return clip;
        }
        finally { Object.DestroyImmediate(original); Object.DestroyImmediate(corrected); Object.DestroyImmediate(overrides); }
    }
}
