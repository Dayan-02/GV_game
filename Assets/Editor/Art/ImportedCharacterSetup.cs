using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Imports licensed skinned models through Unity and retains the existing gameplay roots.</summary>
public static class ImportedCharacterSetup
{
    public const string Root = "Assets/Art/ImportedCharacters";
    public const string MotionRoot = "Assets/Animations/ImportedMotion";
    public const string Soldier = Root + "/Rocketbox/Military_Male_01.fbx";
    public const string Zombie = Root + "/PixelhouseZombie/walk.FBX";

    [MenuItem("Zombie Outbreak/Art/Apply imported people and zombies")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before changing models.");
        EditorSceneManager.SaveOpenScenes();
        PrepareImports();
        ImportedCharacterMotion.Build();
        OutbreakEditor.Open();
        ReplacePlayer(Object.FindAnyObjectByType<ThirdPersonPlayerController>().gameObject);
        EditPrefab("Assets/Prefabs/Player/PlayerPhase3.prefab", ReplacePlayer);
        foreach (string kind in new[] { "EnemyBase", "Hunter", "Runner", "Ambusher", "Elite" })
            EditPrefab("Assets/Prefabs/Enemies/" + kind + ".prefab", go => ReplaceEnemy(go, kind));
        OutbreakEditor.Save("IMPORTED_CHARACTER_ART");
    }

    public static void PrepareImports()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Phase2AnimationAssets.Folder(MotionRoot);
        Phase2AnimationAssets.Folder(Root + "/Materials");
        foreach (string path in Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Select(p => p.Replace('\\','/')))
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".fbx" && ext != ".tga" && ext != ".jpg") continue;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter texture)
            {
                bool normal = Path.GetFileName(path).ToLowerInvariant().Contains("normal");
                texture.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                texture.sRGBTexture = !normal && !path.Contains("specular");
                texture.maxTextureSize = 2048; texture.mipmapEnabled = true;
                texture.SaveAndReimport();
            }
            else if (AssetImporter.GetAtPath(path) is ModelImporter model)
            {
                model.animationType = ModelImporterAnimationType.Human;
                model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                model.importCameras = false; model.importLights = false; model.importVisibility = false;
                model.optimizeGameObjects = false; model.importAnimation = !path.Contains("Rocketbox");
                model.globalScale = path.Contains("PixelhouseZombie") ? 3.2f : 1f;
                model.SaveAndReimport();
                if (path.Contains("PixelhouseZombie"))
                {
                    // This legacy FBX stores its auto-generated Avatar skeleton in source units.
                    // Use Unity's converted local transforms so skin bind poses and animation agree.
                    var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var description = model.humanDescription;
                    description.skeleton = imported.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
                    { name=t.name, position=t.localPosition, rotation=t.localRotation, scale=t.localScale }).ToArray();
                    model.humanDescription=description;
                    model.SaveAndReimport();
                }
                if (model.importAnimation)
                {
                    var clips = model.defaultClipAnimations;
                    foreach (var clip in clips)
                    {
                        clip.name = Path.GetFileNameWithoutExtension(path);
                        clip.loopTime = !path.Contains("fury") && !path.Contains("dead") && !path.Contains("Jump");
                        clip.loopPose = clip.loopTime;
                        clip.lockRootRotation = true; clip.keepOriginalOrientation = false;
                        clip.lockRootPositionXZ = true; clip.keepOriginalPositionXZ = false;
                        clip.lockRootHeightY = true; clip.keepOriginalPositionY = false; clip.heightFromFeet = true;
                    }
                    model.clipAnimations = clips; model.SaveAndReimport();
                }
            }
        }
    }

    private static Animator InstantiateVisual(string path, Transform parent, string name)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null) throw new InvalidOperationException("Missing downloaded model: " + path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = name; instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity; instance.transform.localScale = Vector3.one;
        var animator = instance.GetComponent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid)
            throw new InvalidOperationException("Model must import with a valid Humanoid Avatar: " + path);
        animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach(var t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = parent.gameObject.layer;
        return animator;
    }

    private static void ReplacePlayer(GameObject player)
    {
        var old = player.GetComponentInChildren<Animator>(true);
        var next = InstantiateVisual(Soldier, player.transform, "SurvivorVisual");
        next.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MotionRoot + "/Survivor.overrideController");
        ApplySoldierMaterials(next);
        var weapons = player.GetComponent<PlayerWeaponController>();
        if (weapons != null)
        {
            var serialized = new SerializedObject(weapons);
            var socket = serialized.FindProperty("weaponSocket").objectReferenceValue as Transform;
            if(socket == null) socket = new GameObject("WeaponSocket").transform;
            socket.SetParent(next.GetBoneTransform(HumanBodyBones.RightHand), false);
            // Calibrate against the evaluated retargeted pose, not the source skeleton's
            // authored wrist axes. Humanoid conversion changes those axes on this Avatar.
            next.Rebind();
            next.SetBool("IsGrounded",true);
            next.SetFloat("Speed",0);
            next.Play("Locomotion",0,0);
            next.Update(.001f);
            socket.position = next.GetBoneTransform(HumanBodyBones.RightHand).position + player.transform.forward * .05f;
            socket.rotation = player.transform.rotation;
            socket.localScale = Vector3.one;
            OutbreakEditor.Set(weapons, "weaponSocket", socket);
        }
        var driver = player.GetComponent<PlayerAnimatorDriver>();
        if(driver != null) OutbreakEditor.Set(driver, "animator", next);
        next.gameObject.AddComponent<SupportHandGrip>();
        next.gameObject.AddComponent<ShooterBodyPose>();
        if(old != null) Object.DestroyImmediate(old.gameObject);
    }

    private static void ApplySoldierMaterials(Animator animator)
    {
        foreach(var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => {
                string stem = m.name;
                string color = Root + "/Rocketbox/" + stem + "_color" + ((stem.EndsWith("body") || stem.EndsWith("helmet") || stem.EndsWith("equipment")) ? "_acu" : "") + ".tga";
                return MakeMaterial(stem, color, Root + "/Rocketbox/" + stem + "_normal.tga", Color.white, .22f);
            }).ToArray();
            renderer.updateWhenOffscreen = true;
        }
    }

    private static void ReplaceEnemy(GameObject root, string kind)
    {
        // Variants may have inherited or previously overridden Visual children.
        var old = root.transform.Find("Visual");
        var next = old != null && old.GetComponentInChildren<SkinnedMeshRenderer>(true) != null ? old.GetComponent<Animator>() : null;
        if(next == null)
        {
            if(old != null) Object.DestroyImmediate(old.gameObject);
            next = InstantiateVisual(Zombie, root.transform, "Visual");
        }
        next.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MotionRoot + "/" + (kind == "EnemyBase" ? "Hunter" : kind) + ".overrideController");
        // The source Avatar's anatomical forward is +X, while all gameplay roots use +Z.
        next.transform.localRotation = Quaternion.Euler(0,-90,0);
        next.transform.localPosition = Vector3.zero;
        next.transform.localScale = kind == "Elite" ? new Vector3(1.17f,1.12f,1.17f) : kind == "Runner" ? new Vector3(.94f,1,.94f) : Vector3.one;
        Color tint = kind == "Runner" ? new Color(1,.85f,.79f) : kind == "Ambusher" ? new Color(.79f,.84f,.88f) : kind == "Elite" ? new Color(.82f,.87f,.78f) : Color.white;
        var material = MakeMaterial("Infected_" + kind, Root + "/PixelhouseZombie/difusse.jpg", Root + "/PixelhouseZombie/normal.JPG", tint, .15f);
        foreach(var renderer in next.GetComponentsInChildren<SkinnedMeshRenderer>())
        { renderer.sharedMaterial = material; renderer.updateWhenOffscreen = true; }
        OutbreakEditor.Set(root.GetComponent<EnemyAnimationController>(), "animator", next);
    }

    private static Material MakeMaterial(string name, string albedo, string normal, Color tint, float smoothness)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material,path); }
        material.color = tint; material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);
        if(material.mainTexture == null) throw new InvalidOperationException("Missing character texture: " + albedo);
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
        material.EnableKeyword("_NORMALMAP"); material.SetFloat("_BumpScale",.7f);
        material.SetFloat("_Glossiness",smoothness); material.SetFloat("_Metallic",0);
        EditorUtility.SetDirty(material); return material;
    }

    private static void EditPrefab(string path, Action<GameObject> action)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try { action(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
