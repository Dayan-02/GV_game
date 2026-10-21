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
using Object = UnityEngine.Object;

/// <summary>Builds consistent target-rig locomotion with measured boot contact and phase-aligned gait blending.</summary>
public static class PlayerLocomotionRepair
{
    public const string Root = "Assets/Animations/PlayerGrounded";
    [MenuItem("Tools/Dead city/Repair player locomotion")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        EditorSceneManager.SaveOpenScenes();Phase2AnimationAssets.Folder(Root);
        string controllerPath=Root+"/PlayerGrounded.controller";
        if(AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)==null)
            AssetDatabase.CopyAsset(SurvivalistSetup.MotionRoot+"/Survivalist.controller",controllerPath);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        foreach(string parameter in new[]{"LeftFootContact","RightFootContact"})
            if(!controller.parameters.Any(p=>p.name==parameter))controller.AddParameter(new AnimatorControllerParameter{name=parameter,type=AnimatorControllerParameterType.Float,defaultFloat=1});
        var previous=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(SurvivalistSetup.MotionRoot+"/Survivalist.overrideController");
        var walk=AssetDatabase.LoadAssetAtPath<AnimationClip>(ImportedCharacterSetup.MotionRoot+"/Walk.anim");
        var run=AssetDatabase.LoadAssetAtPath<AnimationClip>(ImportedCharacterSetup.MotionRoot+"/Run.anim");
        var idle=Bake("GroundedIdle",previous["Idle"],null,0,0);
        var walking=Bake("GroundedWalk",walk,null,0,0);
        // A restrained jog shares the walk's footfall phase, rather than blending unrelated poses.
        float phase=FindPhase(run,walk);
        var running=Bake("GroundedRun",run,walk,.25f,phase);
        var backward=Bake("GroundedBackward",previous["AimBackward"],null,0,0);
        var left=Bake("GroundedLeft",previous["AimLeft"],null,0,0);
        var right=Bake("GroundedRight",previous["AimRight"],null,0,0);
        var aimForward=Bake("GroundedAimForward",previous["AimForward"],null,0,0);
        float walkPhase=FindPhase(walking,running);
        var tree=(BlendTree)controller.layers[0].stateMachine.states.First(s=>s.state.name=="Locomotion").state.motion;
        var children=tree.children;
        float walkSpeed=MeasureStride(walking),runSpeed=MeasureStride(running);
        children[0].cycleOffset=0;children[1].cycleOffset=0;
        children[1].timeScale=1.6f/walkSpeed;children[2].timeScale=3.6f/runSpeed;children[3].timeScale=6f/runSpeed;
        children[2].cycleOffset=walkPhase;children[3].cycleOffset=walkPhase;
        tree.children=children;
        var aimTree=(BlendTree)controller.layers[0].stateMachine.states.First(s=>s.state.name=="Aim Locomotion").state.motion;
        var aimChildren=aimTree.children;
        var aimClips=new[]{idle,aimForward,backward,left,right};
        for(int i=1;i<aimChildren.Length;i++)aimChildren[i].timeScale=1.25f/MeasureStride(aimClips[i],i>=3?Vector3.right:Vector3.forward);
        aimTree.children=aimChildren;
        string overridePath=Root+"/PlayerGrounded.overrideController";
        var overrides=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
        if(overrides==null){overrides=new AnimatorOverrideController(controller);AssetDatabase.CreateAsset(overrides,overridePath);}
        overrides.runtimeAnimatorController=controller;
        var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();previous.GetOverrides(pairs);
        foreach(var pair in pairs)if(pair.Value!=null)overrides[pair.Key.name]=pair.Value;
        overrides["Idle"]=idle;overrides["AimIdle"]=idle;overrides["WalkForward"]=walking;
        overrides["Run"]=running;overrides["Sprint"]=running;
        overrides["AimForward"]=aimForward;overrides["AimBackward"]=backward;overrides["WalkBackward"]=backward;
        overrides["AimLeft"]=left;overrides["StrafeLeft"]=left;overrides["AimRight"]=right;overrides["StrafeRight"]=right;
        EditorUtility.SetDirty(tree);EditorUtility.SetDirty(aimTree);EditorUtility.SetDirty(controller);EditorUtility.SetDirty(overrides);
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player/PlayerPhase3.prefab");
        try{Configure(prefab,overrides);PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/Player/PlayerPhase3.prefab");}
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        Configure(Object.FindAnyObjectByType<ThirdPersonPlayerController>().gameObject,overrides);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/PlayerGroundedCalibration.txt","Walk stance speed="+walkSpeed+"\nJog stance speed="+runSpeed+"\nWalk contribution to jog phase="+phase+"\nJog offset in exploration tree="+walkPhase+"\n");
    }

    private static void Configure(GameObject player,RuntimeAnimatorController controller)
    {
        var a=player.GetComponentInChildren<Animator>();a.runtimeAnimatorController=controller;a.applyRootMotion=false;
        // CharacterController contact rests one skin width above the street.
        var p=a.transform.localPosition;p.y=-player.GetComponent<CharacterController>().skinWidth;a.transform.localPosition=p;
        var feet=a.GetComponent<HumanoidFootPlacement>();
        OutbreakEditor.Values(feet,s=>{s.FindProperty("weight").floatValue=.45f;s.FindProperty("maximumCorrection").floatValue=.06f;s.FindProperty("useAnimationContacts").boolValue=true;});
        OutbreakEditor.Values(player.GetComponent<PlayerAnimatorDriver>(),s=>s.FindProperty("parameterDampTime").floatValue=.025f);
    }

    private static GameObject Rig(out Animator animator)
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerPhase3.prefab"));
        foreach(var behaviour in go.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
        go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        animator=go.GetComponentInChildren<Animator>();animator.transform.localPosition=Vector3.zero;
        animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        return go;
    }
    public static AnimationClip Bake(string name,AnimationClip source,AnimationClip secondary,float secondaryWeight,float phase,string outputRoot=Root,float maximumFlightHeight=-1f)
    {
        var go=Rig(out var animator);var graph=PlayableGraph.Create("Grounded motion authoring");var mesh=new Mesh();
        try
        {
            var output=AnimationPlayableOutput.Create(graph,"Pose",animator);
            var main=AnimationClipPlayable.Create(graph,source);main.SetApplyFootIK(false);
            var mixer=AnimationMixerPlayable.Create(graph,2);graph.Connect(main,0,mixer,0);mixer.SetInputWeight(0,1-secondaryWeight);
            AnimationClipPlayable extra=default;
            if(secondary!=null){extra=AnimationClipPlayable.Create(graph,secondary);extra.SetApplyFootIK(false);graph.Connect(extra,0,mixer,1);mixer.SetInputWeight(1,secondaryWeight);}
            output.SetSourcePlayable(mixer);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();
            var boot=animator.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name.Contains("Boots"));
            var curves=Enumerable.Range(0,HumanTrait.MuscleCount+7).Select(_=>new AnimationCurve()).ToArray();
            var contacts=new[]{new AnimationCurve(),new AnimationCurve()};
            int count=Mathf.CeilToInt(source.length*120);float duration=source.length;
            var weights=boot.sharedMesh.boneWeights;
            var leftVertex=weights.Select(w=>boot.bones[w.boneIndex0].name.EndsWith("_l")).ToArray();
            using(var handler=new HumanPoseHandler(animator.avatar,animator.transform))
            {
                for(int f=0;f<=count;f++)
                {
                    double cycle=f/(double)count;main.SetTime(cycle*source.length);
                    if(secondary!=null)extra.SetTime(((cycle+phase)%1)*secondary.length);
                    graph.Evaluate(0);boot.BakeMesh(mesh);var vertices=mesh.vertices;float[] soles={float.PositiveInfinity,float.PositiveInfinity};
                    for(int v=0;v<vertices.Length;v++){int side=leftVertex[v]?0:1;soles[side]=Mathf.Min(soles[side],boot.transform.TransformPoint(vertices[v]).y);}
                    float low=Mathf.Min(soles[0],soles[1]);
                    // A run/sprint must retain its airborne part. The older repair
                    // treated every single-source clip as a grounded walking pose.
                    float flight=maximumFlightHeight>=0f?maximumFlightHeight:secondary!=null?.035f:0f;
                    float target=Mathf.Clamp(low,.004f,flight+.004f);
                    float correction=target-low;
                    var pose=new HumanPose();handler.GetHumanPose(ref pose);pose.bodyPosition.y+=correction/animator.humanScale;
                    float time=f*duration/count;
                    for(int j=0;j<HumanTrait.MuscleCount;j++)curves[j].AddKey(time,pose.muscles[j]);
                    int k=HumanTrait.MuscleCount;for(int j=0;j<3;j++)curves[k+j].AddKey(time,pose.bodyPosition[j]);for(int j=0;j<4;j++)curves[k+3+j].AddKey(time,pose.bodyRotation[j]);
                    for(int side=0;side<2;side++)contacts[side].AddKey(time,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.015f,.065f,soles[side]+correction)));
                }
            }
            string path=outputRoot+"/"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}clip.ClearCurves();clip.name=name;clip.frameRate=120;
            var names=HumanTrait.MuscleName.Concat(new[]{"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"}).ToArray();
            for(int j=0;j<names.Length;j++)
            {
                string property=names[j];foreach(string side in new[]{"Left","Right"})foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Little"})
                {string prefix=side+" "+finger+" ";if(property.StartsWith(prefix))property=side+"Hand."+finger+"."+property.Substring(prefix.Length);}
                // Enforce exact loop closure, then linear interpolation avoids overshoot into the floor.
                var keys=curves[j].keys;keys[keys.Length-1].value=keys[0].value;curves[j].keys=keys;
                for(int key=0;key<keys.Length;key++){AnimationUtility.SetKeyLeftTangentMode(curves[j],key,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curves[j],key,AnimationUtility.TangentMode.Linear);}
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property),curves[j]);
            }
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),"LeftFootContact"),contacts[0]);
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),"RightFootContact"),contacts[1]);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.startTime=0;settings.stopTime=duration;settings.loopTime=true;settings.loopBlend=true;
            settings.keepOriginalPositionY=true;settings.heightFromFeet=false;settings.keepOriginalPositionXZ=true;settings.keepOriginalOrientation=true;
            settings.loopBlendPositionY=true;settings.loopBlendPositionXZ=true;settings.loopBlendOrientation=true;
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            RefineContact(clip,flightLimit: maximumFlightHeight>=0f?maximumFlightHeight+.004f:secondary!=null?.039f:.004f);
            EditorUtility.SetDirty(clip);return clip;
        }
        finally{graph.Destroy();Object.DestroyImmediate(mesh);Object.DestroyImmediate(go);}
    }
    private static void RefineContact(AnimationClip clip,float flightLimit)
    {
        var go=Rig(out var a);var graph=PlayableGraph.Create("Retargeted contact refinement");var mesh=new Mesh();
        try
        {
            var output=AnimationPlayableOutput.Create(graph,"Pose",a);var p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);output.SetSourcePlayable(p);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();
            var boot=a.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name.Contains("Boots"));
            var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),"RootT.y");
            for(int pass=0;pass<2;pass++)
            {
                var previous=AnimationUtility.GetEditorCurve(clip,binding);var curve=new AnimationCurve();int steps=Mathf.CeilToInt(clip.length*120);
                for(int i=0;i<=steps;i++)
                {
                    float time=i*clip.length/steps;p.SetTime(time);graph.Evaluate(0);boot.BakeMesh(mesh);
                    float low=mesh.vertices.Min(v=>boot.transform.TransformPoint(v).y);
                    curve.AddKey(time,previous.Evaluate(time)+(Mathf.Clamp(low,.004f,flightLimit)-low)/a.humanScale);
                }
                var keys=curve.keys;keys[keys.Length-1].value=keys[0].value;curve.keys=keys;
                for(int i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
                AnimationUtility.SetEditorCurve(clip,binding,curve);
                // Recreate the playable so its internal curve cache sees the edited clip.
                graph.DestroyPlayable(p);p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);output.SetSourcePlayable(p);
            }
        }
        finally{graph.Destroy();Object.DestroyImmediate(mesh);Object.DestroyImmediate(go);}
    }
    private static float[] Track(AnimationClip clip)
    {
        var go=Rig(out var a);var graph=PlayableGraph.Create("Footfall phase");
        try{var output=AnimationPlayableOutput.Create(graph,"Pose",a);var p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);output.SetSourcePlayable(p);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();var track=new float[64];for(int i=0;i<64;i++){p.SetTime(i*clip.length/64d);graph.Evaluate(0);track[i]=a.GetBoneTransform(HumanBodyBones.LeftFoot).position.z-a.GetBoneTransform(HumanBodyBones.RightFoot).position.z;}return track;}
        finally{graph.Destroy();Object.DestroyImmediate(go);}
    }
    private static float FindPhase(AnimationClip reference,AnimationClip shifted)
    {
        var a=Track(reference);var b=Track(shifted);float best=float.NegativeInfinity;int offset=0;
        for(int phase=0;phase<64;phase++){float score=0;for(int i=0;i<64;i++)score+=a[i]*b[(i+phase)%64];if(score>best){best=score;offset=phase;}}
        return offset/64f;
    }
    public static float MeasureStride(AnimationClip clip,Vector3 axis=default)
    {
        if(axis==Vector3.zero)axis=Vector3.forward;
        var go=Rig(out var a);var graph=PlayableGraph.Create("Stride calibration");
        try{
            var output=AnimationPlayableOutput.Create(graph,"Pose",a);var p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);output.SetSourcePlayable(p);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();
            var foot=new Vector3[2,121];for(int f=0;f<=120;f++){p.SetTime(f*clip.length/120d);graph.Evaluate(0);foot[0,f]=a.GetBoneTransform(HumanBodyBones.LeftFoot).position;foot[1,f]=a.GetBoneTransform(HumanBodyBones.RightFoot).position;}
            var speeds=new List<float>();for(int leg=0;leg<2;leg++){float low=Enumerable.Range(0,121).Min(f=>foot[leg,f].y);for(int f=1;f<120;f++)if(foot[leg,f].y<low+.045f){float v=Vector3.Dot(foot[leg,f]-foot[leg,f-1],axis)/(clip.length/120);if(Mathf.Abs(v)>.15f)speeds.Add(Mathf.Abs(v));}}
            if(speeds.Count<4)throw new Exception("Unable to calibrate "+clip.name);speeds.Sort();return speeds[speeds.Count/2];
        }finally{graph.Destroy();Object.DestroyImmediate(go);}
    }
}
