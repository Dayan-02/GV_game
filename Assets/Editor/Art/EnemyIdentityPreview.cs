using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Small native studio capture of the four saved prefabs and their actual attack clips.</summary>
public static class EnemyIdentityPreview
{
    private const string Output="Previews/EnemyIdentity";
    public static void Capture()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory(Output);
        var stage=new GameObject("Enemy identity preview");stage.hideFlags=HideFlags.HideAndDontSave;stage.transform.position=new Vector3(10000,0,10000);
        var lights=Object.FindObjectsByType<Light>();var enabled=lights.Select(l=>l.enabled).ToArray();
        bool fog=RenderSettings.fog;var ambientMode=RenderSettings.ambientMode;var ambient=RenderSettings.ambientLight;
        var previous=RenderTexture.active;var graphs=new List<PlayableGraph>();var temps=new List<Object>();
        RenderTexture rt=null;Texture2D image=null;
        try
        {
            foreach(var l in lights)l.enabled=false;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.48f,.48f);
            foreach(var rot in new[]{new Vector3(35,145,0),new Vector3(25,-55,0)}){var l=new GameObject("Studio light").AddComponent<Light>();l.transform.SetParent(stage.transform,false);l.type=LightType.Directional;l.intensity=rot.y>0?1.1f:.5f;l.transform.rotation=Quaternion.Euler(rot);l.cullingMask=1<<31;}
            var camera=new GameObject("Studio camera").AddComponent<Camera>();camera.transform.SetParent(stage.transform,false);camera.transform.localPosition=new Vector3(0,1.65f,8.8f);camera.transform.LookAt(stage.transform.position+Vector3.up*1.15f);camera.enabled=false;camera.orthographic=true;camera.orthographicSize=2.1f;camera.aspect=2;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.065f,.07f);camera.cullingMask=1<<31;camera.allowHDR=false;camera.nearClipPlane=.1f;camera.farClipPlane=25;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(stage.transform,false);floor.transform.localPosition=new Vector3(0,-.045f,0);floor.transform.localScale=new Vector3(11,.08f,5);floor.layer=31;Object.DestroyImmediate(floor.GetComponent<Collider>());var floorMat=new Material(Shader.Find("Standard")){color=new Color(.19f,.20f,.19f)};temps.Add(floorMat);floorMat.SetFloat("_Glossiness",0);floor.GetComponent<Renderer>().sharedMaterial=floorMat;
            var animators=new List<Animator>();var playables=new List<AnimationClipPlayable>();var skins=new List<SkinnedMeshRenderer[]>();var baked=new List<Mesh[]>();
            for(int i=0;i<4;i++)
            {
                string kind=EnemyIdentitySetup.Kinds[i];var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/"+kind+".prefab"),stage.transform);
                foreach(var b in go.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
                foreach(var agent in go.GetComponentsInChildren<NavMeshAgent>(true))agent.enabled=false;
                foreach(var col in go.GetComponentsInChildren<Collider>(true))col.enabled=false;
                foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                go.transform.localPosition=new Vector3((1.5f-i)*2.15f,0,0);go.transform.localRotation=Quaternion.Euler(0,-12,0);
                var a=go.GetComponentInChildren<Animator>();a.runtimeAnimatorController=null;a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;animators.Add(a);
                var graph=PlayableGraph.Create("Identity studio "+kind);graphs.Add(graph);var p=AnimationClipPlayable.Create(graph,AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/InfectedLocomotion/"+kind+"_Idle.anim"));p.SetApplyFootIK(false);AnimationPlayableOutput.Create(graph,"Pose",a).SetSourcePlayable(p);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();playables.Add(p);
                var ss=a.GetComponentsInChildren<SkinnedMeshRenderer>();skins.Add(ss);var mm=new Mesh[ss.Length];baked.Add(mm);
                for(int s=0;s<ss.Length;s++){mm[s]=new Mesh();temps.Add(mm[s]);var g=new GameObject("Preview baked skin",typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(ss[s].transform,false);g.layer=31;g.GetComponent<MeshFilter>().sharedMesh=mm[s];g.GetComponent<MeshRenderer>().sharedMaterials=ss[s].sharedMaterials;ss[s].enabled=false;}
                var label=new GameObject(kind+" label").AddComponent<TextMesh>();label.transform.SetParent(stage.transform,false);label.transform.localPosition=new Vector3((1.5f-i)*2.15f,2.36f,0);label.transform.rotation=camera.transform.rotation;label.text=kind.ToUpperInvariant();label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=64;label.characterSize=.026f;label.color=new Color(.85f,.87f,.81f);label.gameObject.layer=31;
            }
            rt=new RenderTexture(1280,640,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;image=new Texture2D(1280,640,TextureFormat.RGB24,false);
            Action<string> capture=path=>{camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,640),0,0);image.Apply();File.WriteAllBytes(Output+"/"+path,image.EncodeToPNG());};
            Action bake=()=>{for(int i=0;i<4;i++)for(int s=0;s<skins[i].Length;s++){skins[i][s].BakeMesh(baked[i][s]);baked[i][s].RecalculateBounds();}};
            for(int i=0;i<4;i++){playables[i].SetTime(.3);graphs[i].Evaluate(0);}bake();capture("Lineup.png");
            var fit=new System.Text.StringBuilder();for(int i=0;i<4;i++){var a=animators[i];fit.AppendLine(EnemyIdentitySetup.Kinds[i]+" head="+a.transform.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.Head).position)+" headRot="+a.GetBoneTransform(HumanBodyBones.Head).eulerAngles);foreach(var m in a.GetComponentsInChildren<MeshFilter>().Where(m=>!m.name.StartsWith("Preview")))fit.AppendLine(m.name+" pos="+a.transform.InverseTransformPoint(m.transform.position)+" rot="+m.transform.eulerAngles);};File.WriteAllText("Logs/EnemyPreviewFit.txt",fit.ToString());
            for(int i=0;i<4;i++){animators[i].transform.parent.localRotation=Quaternion.Euler(0,-38,0);graphs[i].DestroyPlayable(playables[i]);var p=AnimationClipPlayable.Create(graphs[i],AssetDatabase.LoadAssetAtPath<AnimationClip>(EnemyIdentitySetup.Root+"/Animations/"+EnemyIdentitySetup.Kinds[i]+"_Attack.anim"));p.SetApplyFootIK(false);((AnimationPlayableOutput)graphs[i].GetOutput(0)).SetSourcePlayable(p);playables[i]=p;}
            for(int frame=0;frame<24;frame++){float t=frame/23f;for(int i=0;i<4;i++){playables[i].SetTime(t);graphs[i].Evaluate(0);}bake();capture("Attack_"+frame.ToString("00")+".png");}
            File.WriteAllText(Output+"/README.txt","Native Unity authoring preview, not a gameplay capture. Left to right: Hunter, Runner, Ambusher, Elite. Each attack is a one-second source clip; runtime duration follows the existing per-enemy windup. Contact at 0.56.\n");
        }
        finally
        {
            foreach(var g in graphs)if(g.IsValid())g.Destroy();RenderTexture.active=previous;
            if(rt!=null){rt.Release();Object.DestroyImmediate(rt);}if(image!=null)Object.DestroyImmediate(image);
            Object.DestroyImmediate(stage);foreach(var o in temps)if(o!=null)Object.DestroyImmediate(o);
            for(int i=0;i<lights.Length;i++)if(lights[i]!=null)lights[i].enabled=enabled[i];RenderSettings.fog=fog;RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambient;
        }
    }
}
