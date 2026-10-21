using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Player-only import and assembly for the selected Slayver Survivalist (P2).</summary>
public static class SurvivalistSetup
{
    public const string Root = "Assets/Art/ImportedCharacters/Survivalist";
    public const string MotionRoot = "Assets/Animations/Survivalist";
    public const string Model = Root + "/Vendor/Prefab/Survivalist (1).prefab";

    [MenuItem("Dead city/Art/Apply selected P2 Survivalist player")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before applying P2.");
        EditorSceneManager.SaveOpenScenes();
        Phase2AnimationAssets.Folder(MotionRoot);
        CorrectPelvis((ModelImporter)AssetImporter.GetAtPath(Root + "/Vendor/Basemesh/SK_Military_Survivalist.fbx"));
        PrepareMotion();
        BuildController();
        OutbreakEditor.Open();
        Replace(Object.FindAnyObjectByType<ThirdPersonPlayerController>().gameObject);
        var prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player/PlayerPhase3.prefab");
        try { Replace(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Prefabs/Player/PlayerPhase3.prefab"); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        OutbreakEditor.Save("P2_SURVIVALIST");
    }

    private static void PrepareMotion()
    {
        foreach (string file in Directory.GetFiles(Root + "/Motion"))
        {
            if (!file.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
            string path = file.Replace('\\', '/');
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            CorrectPelvis(importer);
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = Path.GetFileNameWithoutExtension(path);
                clip.loopTime = clip.name != "Death" && clip.name != "Jump";
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = false;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = false;
                clip.lockRootHeightY = clip.name != "Death";
                clip.keepOriginalPositionY = false;
                clip.heightFromFeet = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static void CorrectPelvis(ModelImporter importer)
    {
        var description = importer.humanDescription;
        if (!description.skeleton.Any(b => b.name == "pelvis")) return;
        var bones = description.human;
        int index = Array.FindIndex(bones, b => b.humanName == "Hips");
        if (index < 0 || bones[index].boneName == "pelvis") return;
        bones[index].boneName = "pelvis";
        description.human = bones;
        importer.humanDescription = description;
        importer.SaveAndReimport();
    }

    private static AnimationClip Clip(string file) => ImportedCharacterMotion.ImportClip(Root + "/Motion/" + file);

    private static void BuildController()
    {
        string controllerPath = MotionRoot + "/Survivalist.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) == null)
            AssetDatabase.CopyAsset(ImportedCharacterSetup.MotionRoot + "/Survivor.controller", controllerPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        var layers = controller.layers;
        layers[0].iKPass = true;
        // The authored arm pose has no body-translation curves. It belongs ONLY on
        // the masked carry layer; using it as full-body aim idle sinks the hips.
        var carryClip = ImportedShooterPoses.Create("Carry", 2.4f, true, Model, MotionRoot);
        layers.First(l => l.name == "Weapon Carry").stateMachine.defaultState.motion = carryClip;
        controller.layers = layers;
        var normal = layers[0].stateMachine.states.First(s => s.state.name == "Locomotion").state;
        var tree = (BlendTree)normal.motion;
        var children = tree.children;
        // One speed blend: nesting an idle/walk tree here blends idle twice and makes feet slide.
        children[1].motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(Phase2AnimationAssets.Root + "/Clips/WalkForward.anim");
        // Calibrated against median planted-foot travel on this Avatar (metres/second).
        children[1].timeScale = 1.6f / 1.414194f;
        children[2].timeScale = 3.6f / 3.244947f;
        children[3].timeScale = 6f / 5.991491f;
        tree.children = children;
        var aimingTree = (BlendTree)layers[0].stateMachine.states.First(s => s.state.name == "Aim Locomotion").state.motion;
        var directions = aimingTree.children;
        float[] stanceSpeeds = { 1f, 1.346661f, 1.494087f, .8224851f, .8242143f };
        for (int i = 1; i < directions.Length; i++) directions[i].timeScale = 1.25f / stanceSpeeds[i];
        aimingTree.children = directions;
        EditorUtility.SetDirty(aimingTree);
        foreach (var layer in layers)
            foreach (var state in layer.stateMachine.states)
                foreach (var transition in state.state.transitions)
                {
                    transition.hasFixedDuration = true;
                    transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
                }

        string path = MotionRoot + "/Survivalist.overrideController";
        var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (overrides == null) { overrides = new AnimatorOverrideController(controller); AssetDatabase.CreateAsset(overrides, path); }
        overrides.runtimeAnimatorController = controller;
        var old = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ImportedCharacterSetup.MotionRoot + "/Survivor.overrideController");
        var previous = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
        old.GetOverrides(previous);
        foreach (var baseClip in controller.animationClips.Distinct())
        {
            var pair = previous.FirstOrDefault(p => p.Key.name == baseClip.name);
            if (pair.Value != null) overrides[baseClip.name] = pair.Value;
        }
        overrides["Idle"] = Clip("Idle.fbx");
        overrides["WalkForward"] = Clip("Walk.FBX");
        overrides["Run"] = Clip("Run.FBX");
        overrides["Jump"] = Clip("Jump.FBX");
        overrides["Death"] = Clip("Death.fbx");
        overrides["AimIdle"] = Clip("Idle.fbx");
        overrides["Fire"] = ImportedShooterPoses.Create("ImportedFire", .18f, false, Model, MotionRoot);
        overrides["Reload"] = ImportedShooterPoses.Create("ImportedReload", 1.2f, false, Model, MotionRoot);
        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(overrides);
        AssetDatabase.SaveAssets();
    }

    private static void Replace(GameObject player)
    {
        var old = player.GetComponentInChildren<Animator>(true);
        var nextObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model), player.transform);
        PrefabUtility.UnpackPrefabInstance(nextObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        nextObject.name = "SurvivalistVisual";
        nextObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        nextObject.transform.localScale = Vector3.one;
        var next = nextObject.GetComponentInChildren<Animator>(true);
        if (next == null || next.avatar == null || !next.avatar.isValid || !next.avatar.isHuman)
            throw new InvalidOperationException("P2 requires a valid Humanoid Avatar.");
        next.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MotionRoot + "/Survivalist.overrideController");
        next.applyRootMotion = false;
        next.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (Transform child in nextObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = player.layer;
        foreach (var r in nextObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.name.Contains("FPS")) r.gameObject.SetActive(false);
            if (r.name == "SK_Military_Vest1_4" || r.name == "SK_Military_Cap1" || r.name == "SK_Military_Backpack1_vest") r.gameObject.SetActive(true);
            r.updateWhenOffscreen = true;
            foreach (var m in r.sharedMaterials)
                if (m == null || m.shader == null) throw new InvalidOperationException("Missing P2 material on " + r.name);
        }
        next.Rebind();
        next.SetBool("IsGrounded", true);
        next.Play("Locomotion", 0, 0f);
        next.Update(.001f);
        var weapons = player.GetComponent<PlayerWeaponController>();
        if (weapons != null)
        {
            var socket = new SerializedObject(weapons).FindProperty("weaponSocket").objectReferenceValue as Transform;
            if (socket == null) socket = new GameObject("WeaponSocket").transform;
            var hand = next.GetBoneTransform(HumanBodyBones.RightHand);
            socket.SetParent(hand, false);
            socket.position = hand.position + player.transform.forward * .04f;
            socket.rotation = player.transform.rotation;
            socket.localScale = Vector3.one;
            OutbreakEditor.Set(weapons, "weaponSocket", socket);
        }
        var driver = player.GetComponent<PlayerAnimatorDriver>();
        OutbreakEditor.Set(driver, "animator", next);
        // The motor already eases speed; a second long parameter lag would delay the feet.
        OutbreakEditor.Values(driver, s => s.FindProperty("parameterDampTime").floatValue = .035f);
        OutbreakEditor.Values(player.GetComponent<ThirdPersonPlayerController>(), s =>
        {
            s.FindProperty("acceleration").floatValue = 14f;
            s.FindProperty("braking").floatValue = 20f;
            s.FindProperty("turnSmoothTime").floatValue = .09f;
            s.FindProperty("walkSpeed").floatValue = 1.6f;
            s.FindProperty("runSpeed").floatValue = 3.6f;
            s.FindProperty("sprintSpeed").floatValue = 6f;
        });
        OutbreakEditor.Values(player.GetComponent<PlayerAimController>(), s => s.FindProperty("aimMoveSpeed").floatValue = 1.25f);
        next.gameObject.AddComponent<SupportHandGrip>();
        next.gameObject.AddComponent<ShooterBodyPose>();
        next.gameObject.AddComponent<HumanoidFootPlacement>();
        if (old != null) Object.DestroyImmediate(old.gameObject);
    }

    public static void Inspect()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new StringBuilder();
        foreach (string file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
        {
            string path = file.Replace('\\', '/');
            if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.importCameras = importer.importLights = false;
            importer.SaveAndReimport();
            CorrectPelvis(importer);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var a = source.GetComponent<Animator>();
            report.AppendLine(path + " human=" + (a != null && a.avatar != null && a.avatar.isHuman && a.avatar.isValid));
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                report.AppendLine("  " + clip.name + " length=" + clip.length + " velocity=" + clip.averageSpeed + " human=" + clip.isHumanMotion);
            foreach (var mesh in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                report.AppendLine("  mesh " + mesh.name + " bounds=" + mesh.bounds + " mats=" + string.Join(",", mesh.sharedMaterials.Select(m => m != null ? m.name : "null")));
        }
        File.WriteAllText("Logs/SurvivalistImport.txt", report.ToString());
        Debug.Log(report.ToString());
    }
}
