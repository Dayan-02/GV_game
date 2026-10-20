using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Retargets imported Humanoid motion while retaining the gameplay Animator parameters.</summary>
public static class ImportedCharacterMotion
{
    private const string Root = ImportedCharacterSetup.MotionRoot;
    public static void Build()
    {
        var idle = Motion("Idle","m@Idle");
        var walk = Motion("Walk","m@WalkForwards");
        var run = Motion("Run","m@RunForwards");
        var sprint = Motion("Sprint","m@SprintForwards");
        var strafe = Motion("StrafeRight","m@StrafeRight");
        var left = Clone("StrafeLeft",strafe,true);
        var back = Clone("WalkBackwards",walk,false,true);
        var forward = Motion("AimForward","m@StrafeWalkForwards");
        var aimIdle = Motion("AimIdle","m@StrafeIdle");
        string controllerPath=Root+"/Survivor.controller";
        if(AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)==null)
            AssetDatabase.CopyAsset(Phase2AnimationAssets.ControllerPath,controllerPath);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(!controller.layers.Any(l=>l.name=="Weapon Carry"))
        {
            controller.AddLayer("Weapon Carry");
            var layers=controller.layers;
            var carry=layers[layers.Length-1];
            var mask=new AvatarMask {name="WeaponCarryArms"};
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
            foreach(var p in new[]{AvatarMaskBodyPart.LeftArm,AvatarMaskBodyPart.RightArm,AvatarMaskBodyPart.LeftFingers,AvatarMaskBodyPart.RightFingers}) mask.SetHumanoidBodyPartActive(p,true);
            AssetDatabase.AddObjectToAsset(mask,controller);
            carry.avatarMask=mask;carry.defaultWeight=1;
            var state=carry.stateMachine.AddState("Carry");
            state.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(Phase2AnimationAssets.Root+"/Clips/AimIdle.anim");
            carry.stateMachine.defaultState=state;
            // Actions remain above the carry pose so reload and hit reactions stay visible.
            controller.layers=new[]{layers[0],carry,layers[1]};
        }
        var player=Override("Survivor",controller);
        player["AimIdle"]=ImportedShooterPoses.Create("Carry",2.4f,true);
        player["Fire"]=ImportedShooterPoses.Create("ImportedFire",.18f,false);
        player["Reload"]=ImportedShooterPoses.Create("ImportedReload",1.2f,false);
        player["Idle"]=idle;player["WalkForward"]=walk;player["WalkBackward"]=back;
        player["StrafeRight"]=strafe;player["StrafeLeft"]=left;player["Run"]=run;player["Sprint"]=sprint;
        player["Jump"]=Motion("Jump","m@StandingJump_Frame02",false);
        player["Falling"]=Motion("Falling","m@Falling");
        player["AimForward"]=forward;player["AimBackward"]=back;player["AimLeft"]=left;player["AimRight"]=strafe;
        EditorUtility.SetDirty(player);

        var zombieWalk=ImportClip(ImportedCharacterSetup.Zombie);
        var attack=ImportClip(ImportedCharacterSetup.Root+"/PixelhouseZombie/fury.FBX");
        var death=ImportClip(ImportedCharacterSetup.Root+"/PixelhouseZombie/dead.FBX");
        var standing=Clone("InfectedIdle",zombieWalk);
        // A stable first-frame stance with a small breathing motion, avoiding marching while stopped.
        foreach(var binding in AnimationUtility.GetCurveBindings(standing))
        {
            var curve=AnimationUtility.GetEditorCurve(standing,binding);
            float v=curve.Evaluate(0);
            AnimationUtility.SetEditorCurve(standing,binding,AnimationCurve.Constant(0,2.4f,v));
        }
        AnimationUtility.SetEditorCurve(standing,EditorCurveBinding.FloatCurve("",typeof(Animator),"Chest Front-Back"),new AnimationCurve(new Keyframe(0,.10f),new Keyframe(1.2f,.12f),new Keyframe(2.4f,.10f)));
        var idleSettings=AnimationUtility.GetAnimationClipSettings(standing);idleSettings.startTime=0;idleSettings.stopTime=2.4f;idleSettings.loopTime=true;AnimationUtility.SetAnimationClipSettings(standing,idleSettings);
        var zombieRun=Clone("InfectedRun",run);
        foreach(var binding in AnimationUtility.GetCurveBindings(zombieWalk))
        {
            string p=binding.propertyName;
            if(p.Contains("Arm")||p.Contains("Forearm")||p.Contains("Hand")||p.Contains("Shoulder"))
            {
                var curve=AnimationUtility.GetEditorCurve(zombieWalk,binding);
                AnimationUtility.SetEditorCurve(zombieRun,binding,AnimationCurve.Constant(0,run.length,curve.Evaluate(.25f)));
            }
        }
        string enemyPath=Root+"/Infected.controller";
        if(AssetDatabase.LoadAssetAtPath<AnimatorController>(enemyPath)==null)AssetDatabase.CopyAsset(Phase7Setup.ControllerPath,enemyPath);
        var enemy=AssetDatabase.LoadAssetAtPath<AnimatorController>(enemyPath);
        foreach(var s in enemy.layers[0].stateMachine.states)
            if(s.state.name=="Attack"||s.state.name=="Heavy") {s.state.speed=attack.length;s.state.speedParameter="AttackSpeed";s.state.speedParameterActive=true;}
        foreach(string kind in new[]{"Hunter","Runner","Ambusher","Elite"})
        {
            var c=Override(kind,enemy);
            c["ZombieIdle"]=standing;c["ZombieWalkForward"]=zombieWalk;c["ZombieRun"]=zombieRun;
            c["ZombieAttack"]=attack;c["EliteHeavy"]=attack;c["ZombieDeath"]=death;
            EditorUtility.SetDirty(c);
        }
        AssetDatabase.SaveAssets();
    }
    public static AnimationClip ImportClip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
    private static AnimationClip Motion(string name,string source,bool loop=true)
    {
        var clip=Clone(name,ImportClip(ImportedCharacterSetup.Root+"/UnityMotion/"+source+".fbx"));
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);return clip;
    }
    private static AnimationClip Clone(string name,AnimationClip source,bool mirror=false,bool reverse=false)
    {
        string path=Root+"/"+name+".anim";
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(clip==null){clip=Object.Instantiate(source);AssetDatabase.CreateAsset(clip,path);}
        else EditorUtility.CopySerialized(source,clip);
        clip.name=name;
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.mirror=mirror;AnimationUtility.SetAnimationClipSettings(clip,settings);
        if(reverse)foreach(var binding in AnimationUtility.GetCurveBindings(clip))
        {
            var original=AnimationUtility.GetEditorCurve(clip,binding);
            var keys=original.keys.Reverse().Select(k=>new Keyframe(source.length-k.time,k.value,-k.outTangent,-k.inTangent)).ToArray();
            AnimationUtility.SetEditorCurve(clip,binding,new AnimationCurve(keys));
        }
        EditorUtility.SetDirty(clip);return clip;
    }
    private static AnimatorOverrideController Override(string name,RuntimeAnimatorController source)
    {
        string path=Root+"/"+name+".overrideController";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if(controller==null){controller=new AnimatorOverrideController(source);AssetDatabase.CreateAsset(controller,path);}
        controller.runtimeAnimatorController=source;return controller;
    }
}
