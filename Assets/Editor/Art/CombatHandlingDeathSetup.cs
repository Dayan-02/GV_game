using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Bind the animation-only death exports without replacing models or movement clips.</summary>
public static class CombatHandlingDeathSetup
{
    const string Root = "Assets/Art/CustomEnemies";
    static readonly string[] Kinds = { "Hunter", "Runner", "Ambusher", "Elite" };
    static readonly string[] Modes = { "DeathForward", "DeathBack", "DeathSide" };

    [MenuItem("Tools/Dead city/Apply handling and death animation update")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before applying the animation assets.");
        string folder = Root + "/DeathAnimations";
        Directory.CreateDirectory(folder);
        foreach (string kind in Kinds)
        {
            string source = "ArtSource/CombatPolish/DeathRevision/Exports/" + kind + "_Deaths.fbx";
            if (!File.Exists(source)) throw new FileNotFoundException("Death export missing", source);
            File.Copy(source, folder + "/" + kind + "_Deaths.fbx", true);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new StringBuilder();
        foreach (string kind in Kinds)
        {
            string path = folder + "/" + kind + "_Deaths.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName = "";
            importer.importAnimation = true;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects = false;
            importer.preserveHierarchy = true;
            importer.importCameras = false; importer.importLights = false;
            importer.SaveAndReimport();
            var takes = importer.defaultClipAnimations;
            foreach (var take in takes)
            {
                string mode = Modes.FirstOrDefault(m => take.name.EndsWith(kind + "_" + m, StringComparison.Ordinal));
                if (mode == null) throw new InvalidOperationException("Unexpected death take: " + take.name);
                take.name = kind + "_" + mode;
                take.loopTime = false; take.loopPose = false;
                take.wrapMode = WrapMode.ClampForever;
                take.keepOriginalOrientation = true;
                take.keepOriginalPositionY = true; take.keepOriginalPositionXZ = true;
                take.lockRootRotation = true; take.lockRootHeightY = true; take.lockRootPositionXZ = true;
            }
            importer.clipAnimations = takes; importer.SaveAndReimport();
            var importedClips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + kind + "_Animated.fbx");
            var clips = importedClips.Select(c => AnimationUtility.GetCurveBindings(c)
                .All(b => string.IsNullOrEmpty(b.path) || model.transform.Find(b.path) != null)
                    ? c : BindToModel(c, kind, model, folder)).ToArray();
            // Fail before binding a take to a differently named hierarchy.
            foreach (var clip in clips)
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (!string.IsNullOrEmpty(binding.path) && model.transform.Find(binding.path) == null)
                        throw new InvalidOperationException(kind + ": animation target missing: " + binding.path);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/Controllers/" + kind + ".controller");
            var sm = controller.layers[0].stateMachine;
            AddParameter(controller, "DeathVariant", AnimatorControllerParameterType.Int, 0);
            AddParameter(controller, "DeathSpeed", AnimatorControllerParameterType.Float, 1);
            foreach (var transition in sm.anyStateTransitions.ToArray())
                if (transition.destinationState != null && transition.destinationState.name.StartsWith("Death", StringComparison.Ordinal))
                    sm.RemoveAnyStateTransition(transition);
            for (int i = 0; i < Modes.Length; i++)
            {
                string name = i == 0 ? "Death" : Modes[i];
                var state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? sm.AddState(name);
                state.motion = clips.Single(c => c.name == kind + "_" + Modes[i]);
                state.speed = 1; state.speedParameter = "DeathSpeed"; state.speedParameterActive = true;
                state.writeDefaultValues = true;
                foreach (var exit in state.transitions.ToArray()) state.RemoveTransition(exit);
                var enter = sm.AddAnyStateTransition(state);
                enter.hasExitTime = false; enter.hasFixedDuration = true; enter.duration = .13f;
                enter.canTransitionToSelf = false; enter.interruptionSource = TransitionInterruptionSource.None;
                enter.AddCondition(AnimatorConditionMode.If, 0, "Dead");
                enter.AddCondition(AnimatorConditionMode.Equals, i, "DeathVariant");
                report.AppendLine(kind + " / " + name + ": " + ((AnimationClip)state.motion).length.ToString("F2") + " s; held final pose.");
            }
            // Death must not be interrupted by a late exit from a hit/attack state.
            foreach (var state in sm.states.Select(s => s.state))
                if (!state.name.StartsWith("Death", StringComparison.Ordinal))
                    foreach (var transition in state.transitions)
                        if (!transition.conditions.Any(c => c.parameter == "Dead"))
                            transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            // Remove only this builder's obsolete copies when the FBX hierarchy is preserved.
            foreach (var clip in clips)
                if (AssetDatabase.GetAssetPath(clip) == path)
                {
                    string obsolete = folder + "/" + clip.name + ".anim";
                    if (AssetDatabase.LoadAssetAtPath<AnimationClip>(obsolete) != null)
                        AssetDatabase.DeleteAsset(obsolete);
                }
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CombatHandlingDeathImport.txt", report.ToString());
        Debug.Log("COMBAT_HANDLING_DEATH_IMPORTED\n" + report);
    }

    static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type, float value)
    {
        if (controller.parameters.Any(p => p.name == name)) return;
        controller.AddParameter(new AnimatorControllerParameter { name = name, type = type, defaultFloat = value });
    }

    static AnimationClip BindToModel(AnimationClip source, string kind, GameObject model, string folder)
    {
        // A skeleton-only FBX collapses the armature object into its import root.
        // The mesh FBX retains that object below its root. Restore this one prefix.
        string prefix = kind + "_Rig";
        if (model.transform.Find(prefix) == null)
            throw new InvalidOperationException("Missing model armature: " + prefix);
        string path = folder + "/" + source.name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();
        clip.name = source.name;
        clip.frameRate = source.frameRate;
        clip.wrapMode = WrapMode.ClampForever;
        var bindings = AnimationUtility.GetCurveBindings(source);
        var curves = bindings.Select(b => AnimationUtility.GetEditorCurve(source, b)).ToArray();
        for (int i = 0; i < bindings.Length; i++)
            bindings[i].path = string.IsNullOrEmpty(bindings[i].path) ? prefix : prefix + "/" + bindings[i].path;
        AnimationUtility.SetEditorCurves(clip, bindings, curves);
        var settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.loopTime = false;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(clip);
        return clip;
    }
}
