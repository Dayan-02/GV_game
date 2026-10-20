using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Samples improved poses onto the existing Humanoid assets, retaining GUIDs and combat timing.</summary>
public static class CharacterMotionAuthoring
{
    public static void Apply()
    {
        var rig=Phase2HumanoidFactory.Create(Phase2AnimationAssets.AvatarPath);
        try
        {
            foreach(string name in new[]{"Idle","WalkForward","WalkBackward","StrafeLeft","StrafeRight","Run","Sprint","Jump","Falling","AimIdle","AimForward","AimBackward","AimLeft","AimRight","Fire","Reload","Hit","Death"})
            {
                string path=Phase2AnimationAssets.Root+"/Clips/"+name+".anim";
                float duration=name=="Idle"||name=="AimIdle"?3.2f:name=="Sprint"?.55f:name=="Run"?.70f:name=="Fire"?.18f:name=="Hit"?.35f:name=="Jump"?.4f:name=="Reload"?1.2f:name=="Death"?1.1f:1f;
                bool loop=name!="Jump" && name!="Fire" && name!="Reload" && name!="Hit" && name!="Death";
                Store(Phase2HumanoidFactory.CreateClip(rig,name,duration,loop,(r,n,t)=>Pose(r,n,t,"Player")),path);
            }
            foreach(string name in new[]{"ZombieIdle","ZombieWalkForward","ZombieRun","ZombieAttack","EliteHeavy","ZombieHit","ZombieDeath"})
            {
                bool loop=name=="ZombieIdle" || name=="ZombieWalkForward" || name=="ZombieRun";
                float duration=name=="ZombieRun"?.6f:name=="ZombieHit"?.3f:1;
                Store(Phase2HumanoidFactory.CreateClip(rig,name,duration,loop,(r,n,t)=>Pose(r,n,t,"Hunter")),"Assets/Animations/Enemies/Clips/"+name+".anim");
            }
            Phase2AnimationAssets.Folder("Assets/Animations/CharacterMotion");
            var shared=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Phase7Setup.ControllerPath);
            foreach(string kind in new[]{"Hunter","Runner","Ambusher","Elite"})
            {
                string controllerPath="Assets/Animations/CharacterMotion/"+kind+".overrideController";
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
                if(controller==null){controller=new AnimatorOverrideController(shared);AssetDatabase.CreateAsset(controller,controllerPath);}
                controller.runtimeAnimatorController=shared;
                foreach(string name in new[]{"ZombieIdle","ZombieWalkForward","ZombieRun"})
                {
                    string path="Assets/Animations/CharacterMotion/"+kind+"_"+name+".anim";
                    float duration=name=="ZombieIdle"?2.4f:name=="ZombieRun"?.6f:1;
                    Store(Phase2HumanoidFactory.CreateClip(rig,name,duration,true,(r,n,t)=>Pose(r,n,t,kind)),path);
                    controller[name]=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                }
                EditorUtility.SetDirty(controller);
                string prefab="Assets/Prefabs/Enemies/"+kind+".prefab";
                var root=PrefabUtility.LoadPrefabContents(prefab);
                try{var a=root.GetComponentInChildren<Animator>();a.runtimeAnimatorController=controller;PrefabUtility.SaveAsPrefabAsset(root,prefab);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
        }
        finally{Object.DestroyImmediate(rig.Root);}
        AssetDatabase.SaveAssets();
    }
    private static void Store(AnimationClip clip,string path)
    {
        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(existing==null)AssetDatabase.CreateAsset(clip,path);
        else{EditorUtility.CopySerialized(clip,existing);existing.name=System.IO.Path.GetFileNameWithoutExtension(path);EditorUtility.SetDirty(existing);Object.DestroyImmediate(clip);}
    }
    private static void Pose(Phase2HumanoidFactory.Rig rig,string name,float t,string kind)
    {
        Phase2HumanoidFactory.ResetPose(rig);
        void R(HumanBodyBones bone,float x,float y=0,float z=0)=>rig.Bones[bone].localRotation=Quaternion.Euler(x,y,z);
        bool enemy=kind!="Player", heavy=name=="EliteHeavy";
        string clip=name.Replace("Zombie","");
        bool aim=clip.StartsWith("Aim")||clip=="Fire"||clip=="Reload";
        bool move=clip.Contains("Walk")||clip.Contains("Strafe")||clip=="Run"||clip=="Sprint"||(aim && clip!="AimIdle"&&clip!="Fire"&&clip!="Reload");
        bool run=clip=="Run"||clip=="Sprint";
        float phase=t*Mathf.PI*2,swing=Mathf.Sin(phase),breath=Mathf.Sin(phase);
        float stride=clip=="Sprint"?39:run?31:aim?17:24;
        if(enemy && kind=="Ambusher")stride*=.8f;
        if(enemy && kind=="Elite")stride*=.85f;
        float lean=enemy?(kind=="Runner"?18:kind=="Ambusher"?22:kind=="Elite"?7:13):run?9:2;
        R(HumanBodyBones.Spine,lean);
        R(HumanBodyBones.Chest,1.1f*breath,0,enemy?2:0);
        R(HumanBodyBones.Head,-lean*.6f,enemy?2.5f*breath:0);
        R(HumanBodyBones.LeftUpperArm,-45,-12,68);
        R(HumanBodyBones.RightUpperArm,-48,8,-70);
        R(HumanBodyBones.LeftLowerArm,-38);
        R(HumanBodyBones.RightLowerArm,-42);
        rig.Bones[HumanBodyBones.Hips].localPosition+=Vector3.up*(.0025f*breath);
        if(move)
        {
            bool backward=clip.Contains("Backward");
            float direction=backward?-1:1;
            for(int side=0;side<2;side++)
            {
                bool left=side==0;float cycle=phase+(left?0:Mathf.PI),s=Mathf.Sin(cycle),lift=Mathf.Max(0,Mathf.Cos(cycle));
                float hip=-s*stride*direction,knee=6+lift*(run?72:38);
                if(enemy && kind=="Hunter" && !left){hip*=.72f;knee*=.65f;}
                var upper=left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg;
                var lower=left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg;
                var foot=left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot;
                float lateral=clip.Contains("Left")?-1:clip.Contains("Right")?1:0;
                R(upper,lateral!=0?hip*.3f:hip,0,lateral!=0?-s*18*lateral:(left?-2:2));
                R(lower,knee);
                R(foot,-hip*.32f-knee*.45f+lift*8);
            }
            float sway=Mathf.Sin(phase)* (run?.017f:.013f);
            rig.Bones[HumanBodyBones.Hips].localPosition+=new Vector3(sway,.015f*(1-Mathf.Cos(phase*2)),0);
            R(HumanBodyBones.Hips,0,swing*(run?5:3),-swing*2);
            R(HumanBodyBones.Chest,1,-swing*(run?6:3),swing*1.4f);
            if(clip=="Sprint")
            {
                R(HumanBodyBones.LeftUpperArm,swing*26,0,72);R(HumanBodyBones.RightUpperArm,-28-swing*9,0,-72);
                R(HumanBodyBones.LeftLowerArm,-58);R(HumanBodyBones.RightLowerArm,-50);
            }
        }
        if(aim)
        {
            R(HumanBodyBones.LeftUpperArm,-66,-10,67);R(HumanBodyBones.RightUpperArm,-70,8,-70);
            R(HumanBodyBones.LeftLowerArm,-25);R(HumanBodyBones.RightLowerArm,-38);
            R(HumanBodyBones.Chest,3,-5,0);
        }
        if(enemy)
        {
            float asym=kind=="Runner"?18:kind=="Ambusher"?9:5;
            R(HumanBodyBones.LeftUpperArm,-35-swing*asym,-8,64);
            R(HumanBodyBones.RightUpperArm,-46+swing*asym,8,-68);
            R(HumanBodyBones.LeftLowerArm,-22);R(HumanBodyBones.RightLowerArm,-28);
            if(kind=="Ambusher")rig.Bones[HumanBodyBones.Hips].localPosition+=Vector3.down*.055f;
            if(clip=="Attack"||heavy)
            {
                // Match the shared impact gate at 60% of each normalized attack.
                float strike=t<.6f?Mathf.SmoothStep(0,1,t/.6f):Mathf.SmoothStep(1,0,(t-.6f)/.4f);
                R(HumanBodyBones.LeftUpperArm,heavy?-135+110*strike:-28-70*strike,-8,60);
                R(HumanBodyBones.RightUpperArm,heavy?-135+110*strike:-35-65*strike,8,-60);
                R(HumanBodyBones.Chest,10+strike*16,heavy?0:-12+24*strike);
            }
        }
        if(clip=="Jump"||clip=="Falling")
        {
            float lift=clip=="Jump"?Mathf.Sin(t*Mathf.PI):.4f;
            R(HumanBodyBones.LeftUpperLeg,-25*lift,0,-4);R(HumanBodyBones.RightUpperLeg,-13*lift,0,4);
            R(HumanBodyBones.LeftLowerLeg,48*lift);R(HumanBodyBones.RightLowerLeg,33*lift);
            R(HumanBodyBones.LeftFoot,-12*lift);R(HumanBodyBones.RightFoot,-9*lift);
        }
        if(clip=="Fire")R(HumanBodyBones.Chest,3-7*Mathf.Sin(t*Mathf.PI),-5);
        if(clip=="Reload")
        {
            float reach=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.28f))*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.72f)/.28f)));
            R(HumanBodyBones.LeftUpperArm,-45+20*reach,-15-20*reach,70);
            R(HumanBodyBones.LeftLowerArm,-30-60*reach);
            R(HumanBodyBones.RightUpperArm,-62,14,-65);
            R(HumanBodyBones.Chest,3,-5-5*reach);
        }
        if(clip=="Hit")R(HumanBodyBones.Chest,-18*Mathf.Sin(t*Mathf.PI),0,6*Mathf.Sin(t*Mathf.PI));
        if(clip=="Death")
        {
            float collapse=Mathf.SmoothStep(0,1,t);
            rig.Bones[HumanBodyBones.Hips].localPosition+=Vector3.down*(.72f*collapse);
            R(HumanBodyBones.Hips,-10*collapse,0,85*collapse);
            R(HumanBodyBones.LeftLowerLeg,40*collapse);R(HumanBodyBones.RightLowerLeg,55*collapse);
        }
    }
}
