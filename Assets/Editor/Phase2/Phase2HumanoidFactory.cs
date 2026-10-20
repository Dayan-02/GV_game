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

    public static AnimationClip CreateClip(Rig rig, string clipName, float duration, bool loop, Action<Rig, string, float> poseBuilder = null)
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
                if (poseBuilder == null) Pose(rig, clipName, fraction);
                else poseBuilder(rig, clipName, fraction);
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
        settings.loopBlendOrientation = true;
        settings.loopBlendPositionY = true;
        settings.loopBlendPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        ResetPose(rig);
        return clip;
    }

    private static void Pose(Rig rig, string clip, float t)
    {
        ResetPose(rig);
        bool zombie = clip.StartsWith("Zombie", StringComparison.Ordinal) || clip == "EliteHeavy";
        bool heavy = clip == "EliteHeavy";
        if (clip.StartsWith("Zombie", StringComparison.Ordinal)) clip = clip.Substring(6);
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
        if (zombie)
        {
            float reach = clip == "Attack" || heavy ? (t < .6f ? Mathf.SmoothStep(0,1,t/.6f) : Mathf.SmoothStep(1,0,(t-.6f)/.4f)) : .15f;
            float raise = heavy ? -135f + 115f * reach : -30f - 65f * reach;
            Rotate(rig, HumanBodyBones.LeftUpperArm, raise, -8f, 60f);
            Rotate(rig, HumanBodyBones.RightUpperArm, raise, 8f, -60f);
            Rotate(rig, HumanBodyBones.LeftLowerArm, -18f, 0f, 0f);
            Rotate(rig, HumanBodyBones.RightLowerArm, -22f, 0f, 0f);
            Rotate(rig, HumanBodyBones.Chest, 10f + reach * 15f, 0f, 0f);
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
