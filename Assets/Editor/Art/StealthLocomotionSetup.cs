using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

/// <summary>Retargets the licensed crouch motion to the actual player, preserving the corrected standing gaits.</summary>
public static class StealthLocomotionSetup
{
    public const string Root="Assets/Animations/StealthLocomotion";
    [MenuItem("Tools/Dead city/Apply reference locomotion")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Leave Play Mode first.");
        Phase2AnimationAssets.Folder(Root);
        string path=Root+"/Survivor.controller";
        if(AssetDatabase.LoadAssetAtPath<AnimatorController>(path)!=null)throw new Exception("Reference locomotion already installed; edit its existing assets.");
        var source=ImportedCharacterMotion.ImportClip(SurvivalistSetup.Root+"/Motion/Crouch_Walk.FBX");
        var forward=PlayerLocomotionRepair.Bake("CrouchForward",source,null,0,0,Root);
        var back=Object.Instantiate(forward);back.name="CrouchBackward";Reverse(back);AssetDatabase.CreateAsset(back,Root+"/CrouchBackward.anim");
        var idle=Object.Instantiate(forward);idle.name="CrouchIdle";
        float idleTime=BestIdleTime(forward);
        foreach(var binding in AnimationUtility.GetCurveBindings(idle))
            AnimationUtility.SetEditorCurve(idle,binding,AnimationCurve.Constant(0,2,AnimationUtility.GetEditorCurve(idle,binding).Evaluate(idleTime)));
        var idleSettings=AnimationUtility.GetAnimationClipSettings(idle);idleSettings.stopTime=2;AnimationUtility.SetAnimationClipSettings(idle,idleSettings);
        AssetDatabase.CreateAsset(idle,Root+"/CrouchIdle.anim");
        var sideLeft=CrouchedSide("CrouchLeftSource",AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerLocomotionRepair.Root+"/GroundedLeft.anim"));
        var sideRight=CrouchedSide("CrouchRightSource",AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerLocomotionRepair.Root+"/GroundedRight.anim"));
        var left=PlayerLocomotionRepair.Bake("CrouchLeft",sideLeft,null,0,0,Root);
        var right=PlayerLocomotionRepair.Bake("CrouchRight",sideRight,null,0,0,Root);
        Object.DestroyImmediate(sideLeft);Object.DestroyImmediate(sideRight);
        var clips=new[]{idle,forward,back,left,right};
        float[] stride={1,PlayerLocomotionRepair.MeasureStride(forward),PlayerLocomotionRepair.MeasureStride(back),PlayerLocomotionRepair.MeasureStride(left,Vector3.right),PlayerLocomotionRepair.MeasureStride(right,Vector3.right)};
        AssetDatabase.CopyAsset(PlayerLocomotionRepair.Root+"/PlayerGrounded.controller",path);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);controller.AddParameter("IsCrouching",AnimatorControllerParameterType.Bool);
        var machine=controller.layers[0].stateMachine;
        var normal=machine.states.First(s=>s.state.name=="Locomotion").state;
        var aim=machine.states.First(s=>s.state.name=="Aim Locomotion").state;
        var falling=machine.states.First(s=>s.state.name=="Falling").state;
        var crouch=machine.AddState("Crouch Locomotion");
        var tree=new BlendTree{name="Crouch direction",blendType=BlendTreeType.SimpleDirectional2D,blendParameter="Horizontal",blendParameterY="Vertical"};
        AssetDatabase.AddObjectToAsset(tree,controller);
        var positions=new[]{Vector2.zero,Vector2.up,Vector2.down,Vector2.left,Vector2.right};
        tree.children=Enumerable.Range(0,5).Select(i=>new ChildMotion{motion=clips[i],position=positions[i],timeScale=i==0?1:1.05f/stride[i]}).ToArray();crouch.motion=tree;
        foreach(var state in new[]{normal,aim})
        {
            foreach(var transition in state.transitions.Where(t=>t.destinationState==normal||t.destinationState==aim))transition.AddCondition(AnimatorConditionMode.IfNot,0,"IsCrouching");
            var enter=Link(state,crouch,.20f);Bool(enter,"IsCrouching",true);Bool(enter,"IsGrounded",true);
        }
        var stand=Link(crouch,normal,.20f);Bool(stand,"IsCrouching",false);Bool(stand,"IsAiming",false);Bool(stand,"IsGrounded",true);
        var standAim=Link(crouch,aim,.20f);Bool(standAim,"IsCrouching",false);Bool(standAim,"IsAiming",true);Bool(standAim,"IsGrounded",true);
        var fall=Link(crouch,falling,.08f);Bool(fall,"IsGrounded",false);
        var landed=Link(falling,crouch,.14f);Bool(landed,"IsGrounded",true);Bool(landed,"IsCrouching",true);
        foreach(var transition in falling.transitions.Where(t=>t.destinationState!=crouch))if(transition.conditions.Any(c=>c.parameter=="IsGrounded"))transition.AddCondition(AnimatorConditionMode.IfNot,0,"IsCrouching");
        var carryMachine=controller.layers.First(l=>l.name=="Weapon Carry").stateMachine;
        var raised=carryMachine.defaultState;
        var low=carryMachine.AddState("Low Ready");
        low.motion=ImportedShooterPoses.Create("LowReady",2,true,SurvivalistSetup.Model,Root);carryMachine.defaultState=low;
        Bool(Link(low,raised,.20f),"IsAiming",true);Bool(Link(raised,low,.24f),"IsAiming",false);
        var previous=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(PlayerLocomotionRepair.Root+"/PlayerGrounded.overrideController");
        var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();previous.GetOverrides(pairs);
        var overrides=new AnimatorOverrideController(controller){name="ReferenceSurvivor"};
        foreach(var pair in pairs)if(pair.Value!=null&&controller.animationClips.Any(c=>c.name==pair.Key.name))overrides[pair.Key.name]=pair.Value;
        AssetDatabase.CreateAsset(overrides,Root+"/ReferenceSurvivor.overrideController");
        EditorUtility.SetDirty(controller);EditorUtility.SetDirty(tree);AssetDatabase.SaveAssets();
        var player=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player/PlayerPhase3.prefab");
        try{Configure(player,overrides);PrefabUtility.SaveAsPrefabAsset(player,"Assets/Prefabs/Player/PlayerPhase3.prefab");}finally{PrefabUtility.UnloadPrefabContents(player);}
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);Configure(Object.FindAnyObjectByType<ThirdPersonPlayerController>().gameObject,overrides);
        var camera=Camera.main.GetComponent<ThirdPersonCameraController>();OutbreakEditor.Values(camera,s=>{s.FindProperty("modeBlendTime").floatValue=.24f;s.FindProperty("positionSmoothTime").floatValue=.10f;});
        foreach(var mode in Object.FindObjectsByType<ThirdPersonCameraMode>())if(mode.name.Contains("Exploration"))OutbreakEditor.Values(mode,s=>{s.FindProperty("distance").floatValue=3.1f;s.FindProperty("shoulderOffset").vector2Value=new Vector2(.48f,.06f);s.FindProperty("fieldOfView").floatValue=58;});
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/StealthLocomotionCalibration.txt",string.Join("\n",Enumerable.Range(0,5).Select(i=>clips[i].name+": stance speed="+stride[i]))+"\nIdle sample time="+idleTime);
        RefineCrouch();
        PrepareFootContact();
    }
    private static void Configure(GameObject player,RuntimeAnimatorController controller)
    {
        if(player.GetComponent<PlayerStanceController>()==null)player.AddComponent<PlayerStanceController>();
        player.GetComponentInChildren<Animator>().runtimeAnimatorController=controller;
        OutbreakEditor.Values(player.GetComponent<ThirdPersonPlayerController>(),s=>{s.FindProperty("acceleration").floatValue=9;s.FindProperty("braking").floatValue=13;s.FindProperty("turnSmoothTime").floatValue=.12f;});
    }
    private static AnimatorStateTransition Link(AnimatorState from,AnimatorState to,float duration)
    {
        var transition=from.AddTransition(to);transition.hasExitTime=false;transition.hasFixedDuration=true;transition.duration=duration;
        transition.interruptionSource=TransitionInterruptionSource.SourceThenDestination;Bool(transition,"IsDead",false);return transition;
    }
    private static void Bool(AnimatorStateTransition transition,string name,bool value)=>transition.AddCondition(value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,name);
    private static void Reverse(AnimationClip clip)
    {
        float length=clip.length;
        foreach(var binding in AnimationUtility.GetCurveBindings(clip))
        {var curve=AnimationUtility.GetEditorCurve(clip,binding);curve.keys=curve.keys.Reverse().Select(k=>new Keyframe(length-k.time,k.value)).ToArray();Linear(curve);AnimationUtility.SetEditorCurve(clip,binding,curve);}
    }
    private static GameObject Rig(out Animator a,out PlayableGraph graph,out AnimationPlayableOutput output,AnimationClip source,out AnimationClipPlayable playable)
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerPhase3.prefab"));foreach(var b in go.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
        go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);a=go.GetComponentInChildren<Animator>();a.transform.localPosition=Vector3.zero;a.runtimeAnimatorController=null;a.applyRootMotion=false;
        graph=PlayableGraph.Create("Crouch retargeting");output=AnimationPlayableOutput.Create(graph,"Pose",a);playable=AnimationClipPlayable.Create(graph,source);playable.SetApplyFootIK(false);output.SetSourcePlayable(playable);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();return go;
    }
    private static float BestIdleTime(AnimationClip source)
    {
        var go=Rig(out var a,out var graph,out _,source,out var playable);
        try{float best=float.PositiveInfinity,time=0;for(int i=0;i<64;i++){float t=i*source.length/64;playable.SetTime(t);graph.Evaluate(0);float score=Mathf.Abs(a.GetBoneTransform(HumanBodyBones.LeftFoot).position.y-a.GetBoneTransform(HumanBodyBones.RightFoot).position.y);if(score<best){best=score;time=t;}}return time;}
        finally{graph.Destroy();Object.DestroyImmediate(go);}
    }
    private static AnimationClip CrouchedSide(string name,AnimationClip source,float lower=.38f,float lean=22)
    {
        var go=Rig(out var a,out var graph,out _,source,out var playable);
        try
        {
            var curves=Enumerable.Range(0,HumanTrait.MuscleCount+7).Select(_=>new AnimationCurve()).ToArray();
            using(var handler=new HumanPoseHandler(a.avatar,a.transform))
            for(int frame=0;frame<=120;frame++)
            {
                float time=frame*source.length/120;playable.SetTime(time);graph.Evaluate(0);
                var left=a.GetBoneTransform(HumanBodyBones.LeftFoot);var right=a.GetBoneTransform(HumanBodyBones.RightFoot);
                var lp=left.position;var rp=right.position;var lr=left.rotation;var rr=right.rotation;
                a.GetBoneTransform(HumanBodyBones.Hips).position-=Vector3.up*lower;
                var chest=a.GetBoneTransform(HumanBodyBones.Chest);chest.rotation=Quaternion.AngleAxis(lean,Vector3.right)*chest.rotation;
                SolveLeg(a,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,lp);left.rotation=lr;
                SolveLeg(a,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,rp);right.rotation=rr;
                var pose=new HumanPose();handler.GetHumanPose(ref pose);
                for(int i=0;i<HumanTrait.MuscleCount;i++)curves[i].AddKey(time,pose.muscles[i]);int k=HumanTrait.MuscleCount;
                for(int i=0;i<3;i++)curves[k+i].AddKey(time,pose.bodyPosition[i]);for(int i=0;i<4;i++)curves[k+3+i].AddKey(time,pose.bodyRotation[i]);
            }
            var clip=new AnimationClip{name=name,frameRate=120};
            var names=HumanTrait.MuscleName.Concat(new[]{"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"}).ToArray();
            for(int i=0;i<curves.Length;i++){string property=names[i];foreach(string side in new[]{"Left","Right"})foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Little"}){string prefix=side+" "+finger+" ";if(property.StartsWith(prefix))property=side+"Hand."+finger+"."+property.Substring(prefix.Length);}Linear(curves[i]);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property),curves[i]);}
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.startTime=0;settings.stopTime=source.length;settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);return clip;
        }
        finally{graph.Destroy();Object.DestroyImmediate(go);}
    }
    private static void SolveLeg(Animator a,HumanBodyBones upperId,HumanBodyBones lowerId,HumanBodyBones footId,Vector3 target)
    {
        var upper=a.GetBoneTransform(upperId);var lower=a.GetBoneTransform(lowerId);var foot=a.GetBoneTransform(footId);var start=upper.position;
        float l1=Vector3.Distance(start,lower.position),l2=Vector3.Distance(lower.position,foot.position);var dir=(target-start).normalized;
        float d=Mathf.Clamp(Vector3.Distance(start,target),Mathf.Abs(l1-l2)+.001f,l1+l2-.003f),along=(l1*l1+d*d-l2*l2)/(2*d);
        var bend=Vector3.ProjectOnPlane(Vector3.forward,dir).normalized;var knee=start+dir*along+bend*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
        upper.rotation=Quaternion.FromToRotation(lower.position-start,knee-start)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(foot.position-lower.position,start+dir*d-lower.position)*lower.rotation;
    }
    private static void Linear(AnimationCurve curve){for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}}

    public static void RefineCrouch()
    {
        var native=ImportedCharacterMotion.ImportClip(SurvivalistSetup.Root+"/Motion/Crouch_Walk.FBX");
        var calibrated=PlayerLocomotionRepair.Bake("CrouchSource",native,null,0,0,Root);
        var temp=CrouchedSide("Lowered native crouch",calibrated,.12f,6);
        var forward=PlayerLocomotionRepair.Bake("CrouchForward",temp,null,0,0,Root);Object.DestroyImmediate(temp);
        var reverse=Object.Instantiate(forward);Reverse(reverse);
        var backward=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/CrouchBackward.anim");EditorUtility.CopySerialized(reverse,backward);backward.name="CrouchBackward";Object.DestroyImmediate(reverse);
        // A two-foot standing source yields a planted idle, unlike freezing a walking stride.
        temp=CrouchedSide("Planted crouch idle",AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerLocomotionRepair.Root+"/GroundedIdle.anim"));
        var idle=PlayerLocomotionRepair.Bake("CrouchIdle",temp,null,0,0,Root);Object.DestroyImmediate(temp);
        temp=CrouchedSide("Crouch left",AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerLocomotionRepair.Root+"/GroundedLeft.anim"));
        var left=PlayerLocomotionRepair.Bake("CrouchLeft",temp,null,0,0,Root);Object.DestroyImmediate(temp);
        temp=CrouchedSide("Crouch right",AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerLocomotionRepair.Root+"/GroundedRight.anim"));
        var right=PlayerLocomotionRepair.Bake("CrouchRight",temp,null,0,0,Root);Object.DestroyImmediate(temp);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Root+"/Survivor.controller");
        var tree=(BlendTree)controller.layers[0].stateMachine.states.First(s=>s.state.name=="Crouch Locomotion").state.motion;
        var clips=new[]{idle,forward,backward,left,right};var children=tree.children;var report=new System.Text.StringBuilder();
        for(int i=1;i<5;i++){float stride=PlayerLocomotionRepair.MeasureStride(clips[i],i>=3?Vector3.right:Vector3.forward);children[i].timeScale=1.05f/stride;report.AppendLine(clips[i].name+" stance speed="+stride);}
        tree.children=children;EditorUtility.SetDirty(tree);EditorUtility.SetDirty(backward);
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player/PlayerPhase3.prefab");
        try{OutbreakEditor.Values(prefab.GetComponent<PlayerStanceController>(),s=>s.FindProperty("crouchedHeight").floatValue=1.55f);PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/Player/PlayerPhase3.prefab");}finally{PrefabUtility.UnloadPrefabContents(prefab);}
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        OutbreakEditor.Values(Object.FindAnyObjectByType<PlayerStanceController>(),s=>s.FindProperty("crouchedHeight").floatValue=1.55f);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/StealthLocomotionCalibration.txt",report.ToString()+"Planted crouch idle; capsule height=1.55m; feet retained during pelvis lowering.\n");
    }
    public static void PrepareFootContact()
    {
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player/PlayerPhase3.prefab");
        try{prefab.GetComponentInChildren<HumanoidFootPlacement>().BakeSoles();PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/Player/PlayerPhase3.prefab");}finally{PrefabUtility.UnloadPrefabContents(prefab);}
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        var feet=Object.FindAnyObjectByType<ThirdPersonPlayerController>().GetComponentInChildren<HumanoidFootPlacement>();feet.BakeSoles();EditorUtility.SetDirty(feet);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
    }
}
