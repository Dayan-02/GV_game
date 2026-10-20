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
