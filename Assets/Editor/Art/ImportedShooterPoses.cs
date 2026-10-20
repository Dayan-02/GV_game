using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Authors upper-body poses against the actual imported arm lengths, including finger grip.</summary>
public static class ImportedShooterPoses
{
    public static AnimationClip Create(string name,float duration,bool loop, string modelPath = null, string outputRoot = null)
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath ?? ImportedCharacterSetup.Soldier));
        try
        {
            var a=go.GetComponent<Animator>();
            var bones=go.GetComponentsInChildren<Transform>();
            var rotations=bones.Select(t=>t.localRotation).ToArray();
            var positions=bones.Select(t=>t.localPosition).ToArray();
            var curves=Enumerable.Range(0,HumanTrait.MuscleCount).Select(_=>new AnimationCurve()).ToArray();
            using(var handler=new HumanPoseHandler(a.avatar,a.transform))
            {
                int count=Mathf.CeilToInt(duration*30);
                for(int frame=0;frame<=count;frame++)
                {
                    for(int j=0;j<bones.Length;j++){bones[j].localRotation=rotations[j];bones[j].localPosition=positions[j];}
                    float t=frame/(float)count;
                    Pose(a,t,name);
                    var human=new HumanPose();handler.GetHumanPose(ref human);
                    for(int j=0;j<curves.Length;j++)
                    {
                        string muscle=HumanTrait.MuscleName[j];
                        float v=human.muscles[j];
                        if(muscle.Contains("Stretched") && (muscle.Contains("Thumb")||muscle.Contains("Index")||muscle.Contains("Middle")||muscle.Contains("Ring")||muscle.Contains("Little")))
                            v=muscle.Contains("Index")?-.25f:-.65f;
                        curves[j].AddKey(t*duration,v);
                    }
                }
            }
            string path=(outputRoot ?? ImportedCharacterSetup.MotionRoot)+"/"+name+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}
            clip.ClearCurves();clip.name=name;clip.frameRate=30;
            for(int j=0;j<curves.Length;j++)
            {
                string property=HumanTrait.MuscleName[j];
                // Finger binding names differ from the names exposed by HumanTrait.
                foreach(string side in new[]{"Left", "Right"})
                    foreach(string finger in new[]{"Thumb", "Index", "Middle", "Ring", "Little"})
                    {
                        string prefix=side+" "+finger+" ";
                        if(property.StartsWith(prefix)) property=side+"Hand."+finger+"."+property.Substring(prefix.Length);
                    }
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property),curves[j]);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.startTime=0;settings.stopTime=duration;settings.loopTime=loop;
            AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);return clip;
        }
        finally{Object.DestroyImmediate(go);}
    }
    public static void Pose(Animator a,float t,string action)
    {
        Transform r=a.transform;
        Vector3 right=new Vector3(.18f,1.33f,.18f),left=new Vector3(-.03f,1.29f,.50f);
        bool lowReady=action=="LowReady";
        if(lowReady){right=new Vector3(.2f,1.10f,.22f);left=new Vector3(-.03f,.99f,.47f);}
        float recoil=action=="ImportedFire"?Mathf.Sin(t*Mathf.PI)*.035f:0;
        right.z-=recoil;left.z-=recoil;
        if(action=="ImportedReload")
        {
            float lower=Mathf.Sin(t*Mathf.PI);
            right+=new Vector3(.03f,-.14f,-.06f)*lower;
            left=Vector3.Lerp(left,t<.45f?new Vector3(-.14f,.94f,.16f):new Vector3(.14f,1.15f,.43f),lower);
        }
        Solve(a,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,r.TransformPoint(right),r.TransformPoint(new Vector3(.5f,1.1f,.03f)));
        Solve(a,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,r.TransformPoint(left),r.TransformPoint(new Vector3(-.42f,1.10f,.25f)));
        foreach(var side in new[]{HumanBodyBones.RightHand,HumanBodyBones.LeftHand})
        {
            var hand=a.GetBoneTransform(side);
            var finger=a.GetBoneTransform(side==HumanBodyBones.RightHand?HumanBodyBones.RightMiddleProximal:HumanBodyBones.LeftMiddleProximal);
            Vector3 direction=lowReady?Quaternion.AngleAxis(22,r.right)*r.forward:r.forward;
            if(finger!=null)hand.rotation=Quaternion.FromToRotation(finger.position-hand.position,direction)*hand.rotation;
        }
    }
    private static void Solve(Animator a,HumanBodyBones upperId,HumanBodyBones lowerId,HumanBodyBones handId,Vector3 target,Vector3 pole)
    {
        var upper=a.GetBoneTransform(upperId);var lower=a.GetBoneTransform(lowerId);var hand=a.GetBoneTransform(handId);
        Vector3 start=upper.position;float l1=Vector3.Distance(start,lower.position),l2=Vector3.Distance(lower.position,hand.position);
        Vector3 dir=(target-start).normalized;float d=Mathf.Clamp(Vector3.Distance(start,target),.01f,l1+l2-.005f);
        float along=(l1*l1+d*d-l2*l2)/(2*d),h=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
        Vector3 bend=Vector3.ProjectOnPlane(pole-start,dir).normalized;
        Vector3 elbow=start+dir*along+bend*h;
        upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,start+dir*d-lower.position)*lower.rotation;
    }
}
