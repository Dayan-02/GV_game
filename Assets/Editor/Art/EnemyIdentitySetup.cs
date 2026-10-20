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

/// <summary>Distinct infected silhouettes and one-second, contact-aligned Humanoid attacks.</summary>
public static class EnemyIdentitySetup
{
    public const string Root = "Assets/Art/EnemyIdentity";
    public static readonly string[] Kinds = { "Hunter", "Runner", "Ambusher", "Elite" };
    private const string Motion = "Assets/Animations/InfectedLocomotion/";

    [MenuItem("Zombie Outbreak/Enemies/Apply distinct identities and attacks")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        foreach (var folder in new[] { Root, Root + "/Meshes", Root + "/Materials", Root + "/Animations" })
            Phase2AnimationAssets.Folder(folder);
        var report = new System.Text.StringBuilder("Enemy identity and attack authoring. Not a combat play-through.\n");
        foreach (string kind in Kinds)
        {
            var normal = AuthorAttack(kind, false);
            var heavy = AuthorAttack(kind, true);
            CalibrateSoles(kind, normal); CalibrateSoles(kind, heavy);
            var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Motion + kind + ".overrideController");
            overrides["ZombieAttack"] = normal; overrides["EliteHeavy"] = heavy; EditorUtility.SetDirty(overrides);
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Motion+kind+".controller");
            foreach(var state in controller.layers[0].stateMachine.states.Where(s=>s.state.name=="Attack"||s.state.name=="Heavy"))
                foreach(var transition in state.state.transitions){transition.exitTime=.92f;transition.duration=.06f;transition.hasFixedDuration=true;EditorUtility.SetDirty(transition);}
            EditorUtility.SetDirty(controller);
            foreach (string path in kind == "Hunter" ? new[] { "Assets/Prefabs/Enemies/Hunter.prefab", "Assets/Prefabs/Enemies/EnemyBase.prefab" } : new[] { "Assets/Prefabs/Enemies/" + kind + ".prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Dress(root, kind);
                    var animation = new SerializedObject(root.GetComponent<EnemyAnimationController>());
                    animation.FindProperty("attackClip").objectReferenceValue = normal;
                    animation.FindProperty("heavyAttackClip").objectReferenceValue = heavy;
                    animation.ApplyModifiedPropertiesWithoutUndo();
                    var combat = new SerializedObject(root.GetComponent<EnemyCombat>());
                    combat.FindProperty("contactNormalizedTime").floatValue = .56f;
                    combat.FindProperty("hitArc").floatValue = kind == "Elite" ? 125 : 110;
                    combat.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            report.AppendLine(kind + ": unique body mesh, outfit attachments, normal/heavy clips; contact=0.56; clip lengths=" + normal.length + "/" + heavy.length);
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/EnemyIdentityAuthoring.txt", report.ToString());
        // Scene spawning references the updated prefabs; the city and FPS player are not regenerated.
        Debug.Log(report.ToString());
    }

    private static Material Surface(string name, string texture, Color colour, float shine = .12f)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
        material.color = colour;
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(CharacterSurfaceLibrary.Root + "/Textures/" + texture + "_Albedo.tga");
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CharacterSurfaceLibrary.Root + "/Textures/" + texture + "_Normal.tga"));
        material.EnableKeyword("_NORMALMAP"); material.SetFloat("_BumpScale", .35f);
        material.SetFloat("_Glossiness", shine); material.SetFloat("_Metallic", 0);
        material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
    }

    private static void Dress(GameObject root, string kind)
    {
        var a = root.GetComponentInChildren<Animator>();
        var old = a.GetComponentsInChildren<Transform>(true).Where(t => t.name == "EnemyIdentity").ToArray();
        foreach (var t in old) Object.DestroyImmediate(t.gameObject);
        var skin = a.GetComponentInChildren<SkinnedMeshRenderer>();
        // Every rebuild starts from the vendor mesh, never from a previously modified copy.
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(ImportedCharacterSetup.Zombie).GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
        var mesh = Object.Instantiate(source); mesh.name = kind + "_InfectedBody";
        var vertices = mesh.vertices;
        for (int i=0;i<vertices.Length;i++)
        {
            var v = a.transform.InverseTransformPoint(skin.transform.TransformPoint(vertices[i]));
            float chest = Mathf.SmoothStep(.8f, 1.3f, v.y) * (1 - Mathf.SmoothStep(1.55f, 1.8f, v.y));
            float width = kind == "Runner" ? .83f : kind == "Elite" ? 1.22f : kind == "Ambusher" ? .91f : 1.04f;
            v.x *= Mathf.Lerp(1, width, Mathf.Lerp(.35f, 1, chest));
            vertices[i] = skin.transform.InverseTransformPoint(a.transform.TransformPoint(v));
        }
        mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        skin.sharedMesh = SaveMesh(mesh, kind + "_Body"); skin.updateWhenOffscreen = true;
        string materialPath = Root + "/Materials/" + kind + "_Body.mat";
        var body = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (body == null) { body = Object.Instantiate(skin.sharedMaterial); AssetDatabase.CreateAsset(body, materialPath); }
        body.color = kind == "Runner" ? new Color(.78f,.62f,.50f) : kind == "Ambusher" ? new Color(.53f,.60f,.50f) : kind == "Elite" ? new Color(.57f,.59f,.56f) : new Color(.72f,.68f,.55f);
        body.SetFloat("_Glossiness", .13f); EditorUtility.SetDirty(body); skin.sharedMaterial = body;
        // Evaluate the existing idle once so accessories are fitted to real animated body positions.
        a.Rebind(); a.SetFloat("Speed",0); a.SetFloat("GaitScale",1); a.Play("Locomotion",0,.18f); a.Update(0);
        var cloth = Surface(kind + "_Cloth", "Canvas", kind == "Hunter" ? new Color(.48f,.29f,.07f) : kind == "Runner" ? new Color(.56f,.53f,.40f) : kind == "Ambusher" ? new Color(.16f,.20f,.14f) : new Color(.13f,.15f,.14f));
        var dark = Surface(kind + "_Straps", "Leather", new Color(.10f,.085f,.067f));
        var metal = Surface(kind + "_WornArmour", "Steel", kind == "Hunter" ? new Color(.56f,.40f,.12f) : new Color(.20f,.23f,.22f), .24f);
        var blood = Surface("DriedCloth", "Canvas", new Color(.20f,.055f,.035f));
        var tape = Surface("DirtyReflector", "Canvas", new Color(.67f,.67f,.48f), .19f);
        FittedClothing(a, skin, kind, cloth);
        if (kind == "Hunter")
        {
            Cap(a, "Cracked work helmet", metal, .165f, .11f);
            foreach (float x in new[]{-.14f,.14f}) Plate(a, HumanBodyBones.Chest, "Vest reflector", new Vector3(x,-.14f,.32f), new Vector3(.035f,.29f,.012f), tape);
            Plate(a, HumanBodyBones.Chest, "Ripped vest pocket", new Vector3(-.11f,-.27f,.29f), new Vector3(.13f,.11f,.035f), dark);
        }
        else if (kind == "Runner")
        {
            Plate(a,HumanBodyBones.Chest,"Broken shoulder strap",new Vector3(.15f,-.06f,.30f),new Vector3(.035f,.22f,.016f),blood);
        }
        else if (kind == "Ambusher")
        {
            Shell(a, HumanBodyBones.Chest, "Ragged rain cape", new Vector3(0,-.09f,.13f), new[]{.17f,.40f,.35f,.32f}, new[]{.19f,.28f,.29f,.29f}, new[]{.22f,.02f,-.26f,-.48f}, cloth, true);
            Hood(a, cloth);
        }
        else
        {
            Plate(a,HumanBodyBones.Chest,"Cracked chest plate",new Vector3(0,-.13f,.46f),new Vector3(.48f,.38f,.055f),metal);
            Plate(a,HumanBodyBones.Chest,"Chest fracture",new Vector3(.06f,-.13f,.491f),new Vector3(.018f,.31f,.008f),blood);
            foreach(float x in new[]{-.22f,0,.22f})Plate(a,HumanBodyBones.Hips,"Belt pouch",new Vector3(x,-.05f,.30f),new Vector3(.15f,.14f,.075f),cloth);
            Cap(a,"Damaged riot helmet",metal,.18f,.15f);
            Plate(a,HumanBodyBones.LeftUpperArm,"Asymmetric shoulder armour",new Vector3(-.05f,-.045f,.20f),new Vector3(.22f,.19f,.28f),metal);
            foreach(bool left in new[]{true,false})Plate(a,left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg,"Battered shin guard",new Vector3(0,-.10f,.24f),new Vector3(.16f,.30f,.045f),metal);
        }
        foreach(var r in a.GetComponentsInChildren<Renderer>(true))r.gameObject.layer=root.layer;
    }

    private static void FittedClothing(Animator a,SkinnedMeshRenderer body,string kind,Material material)
    {
        var source=body.sharedMesh;var mesh=Object.Instantiate(source);mesh.name=kind+"_FittedClothing";
        var verts=mesh.vertices;var normals=mesh.normals;
        for(int i=0;i<verts.Length;i++)verts[i]+=normals[i]*.009f;
        mesh.vertices=verts;
        var weights=mesh.boneWeights;var torso=new bool[verts.Length];
        for(int i=0;i<torso.Length;i++)
        {
            var w=weights[i];int index=w.boneIndex0;float max=w.weight0;
            if(w.weight1>max){max=w.weight1;index=w.boneIndex1;}if(w.weight2>max){max=w.weight2;index=w.boneIndex2;}if(w.weight3>max)index=w.boneIndex3;
            string bone=body.bones[index].name;
            torso[i]=bone.Contains("Spine")||bone.Contains("Clavicle")||(kind=="Hunter"&&bone.Contains("UpperArm"));
        }
        var triangles=source.triangles;var selected=new List<int>();
        for(int i=0;i<triangles.Length;i+=3)if((torso[triangles[i]]?1:0)+(torso[triangles[i+1]]?1:0)+(torso[triangles[i+2]]?1:0)>=2)selected.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});
        mesh.subMeshCount=1;mesh.SetTriangles(selected,0);mesh.RecalculateBounds();
        var group=new GameObject("EnemyIdentity").transform;group.SetParent(a.transform,false);
        var go=new GameObject(kind+" fitted clothing");go.transform.SetParent(body.transform,false);
        // Parent the visual under an identity group while retaining the skin's matrix.
        go.transform.SetParent(group,true);
        var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=SaveMesh(mesh,kind+"_FittedClothing");renderer.bones=body.bones;renderer.rootBone=body.rootBone;renderer.sharedMaterial=material;renderer.updateWhenOffscreen=true;renderer.localBounds=body.localBounds;
    }

    private static Bounds HeadBounds(Animator a)
    {
        var skin=a.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name=="default");var mesh=new Mesh();
        try{skin.BakeMesh(mesh);var vs=mesh.vertices;var ws=skin.sharedMesh.boneWeights;bool first=true;var bounds=new Bounds();
            for(int i=0;i<vs.Length;i++)if(skin.bones[ws[i].boneIndex0].name.Contains("Head")){var v=a.transform.InverseTransformPoint(skin.transform.TransformPoint(vs[i]));if(first){bounds=new Bounds(v,Vector3.zero);first=false;}else bounds.Encapsulate(v);}return bounds;
        }finally{Object.DestroyImmediate(mesh);}
    }

    private static Mesh SaveMesh(Mesh mesh,string name)
    {
        if(mesh.uv.Length==mesh.vertexCount)mesh.RecalculateTangents();
        string path=Root+"/Meshes/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
    }
    private static Transform Attachment(Animator a,HumanBodyBones bone,string name,Vector3 offset)
    {
        // On this Avatar, Chest maps to the lower ribcage, not the shoulder line.
        if (bone == HumanBodyBones.Chest) offset.y += .33f;
        var b=a.GetBoneTransform(bone);var group=b.Find("EnemyIdentity");
        if(group==null){group=new GameObject("EnemyIdentity").transform;group.SetParent(b,false);}
        var t=new GameObject(name).transform;t.SetParent(group,false);
        t.SetPositionAndRotation(b.position+a.transform.TransformVector(offset),a.transform.rotation);
        // World dimensions follow the existing variant scale, without inheriting bone-axis scale.
        t.localScale=Vector3.one;return t;
    }
    private static void RenderMesh(Transform t,Mesh mesh,Material mat)
    {t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;}
    private static void Plate(Animator a,HumanBodyBones bone,string name,Vector3 offset,Vector3 size,Material material)
    {
        // Bevelled eight-sided extruded plate; avoids sharp primitive-box silhouettes.
        var t=Attachment(a,bone,name,offset);var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
        var ring=new[]{new Vector2(-.40f,-.5f),new Vector2(.40f,-.5f),new Vector2(.5f,-.4f),new Vector2(.5f,.4f),new Vector2(.4f,.5f),new Vector2(-.4f,.5f),new Vector2(-.5f,.4f),new Vector2(-.5f,-.4f)};
        for(int z=0;z<2;z++)foreach(var p in ring){v.Add(new Vector3(p.x*size.x,p.y*size.y,(z-.5f)*size.z));uv.Add(p+Vector2.one*.5f);}
        for(int i=1;i<7;i++){tri.AddRange(new[]{0,i+1,i,8,8+i,9+i});}
        for(int i=0;i<8;i++){int j=(i+1)%8;tri.AddRange(new[]{i,j,8+j,i,8+j,8+i});}
        var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        RenderMesh(t,SaveMesh(mesh,a.transform.parent.name+"_"+name.Replace(" ","_")),material);
    }
    private static void Shell(Animator a,HumanBodyBones bone,string name,Vector3 offset,float[] xs,float[] zs,float[] ys,Material mat,bool ragged)
    {
        var t=Attachment(a,bone,name,offset);var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();const int n=32;
        for(int row=0;row<ys.Length;row++)for(int i=0;i<=n;i++)
        {float angle=i*2*Mathf.PI/n;float ripple=1+.028f*Mathf.Sin(angle*7+row*.7f);float y=ys[row]+(ragged&&row==ys.Length-1?.035f*Mathf.Sin(angle*9)+.018f*Mathf.Sin(angle*13):0);v.Add(new Vector3(Mathf.Sin(angle)*xs[row]*ripple,y,Mathf.Cos(angle)*zs[row]*ripple));uv.Add(new Vector2(i/(float)n*2,row/(float)(ys.Length-1)));}
        for(int r=0;r<ys.Length-1;r++)for(int i=0;i<n;i++){int j=r*(n+1)+i;tri.AddRange(new[]{j,j+n+1,j+1,j+1,j+n+1,j+n+2});}
        var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();Backfaces(mesh);mesh.RecalculateBounds();
        RenderMesh(t,SaveMesh(mesh,a.transform.parent.name+"_"+name.Replace(" ","_")),mat);
    }
    private static void Cap(Animator a,string name,Material mat,float radius,float height)
    {
        var head=HeadBounds(a);var bone=a.transform.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.Head).position);var center=new Vector3(head.center.x,head.max.y-height*.55f,head.center.z);var offset=center-bone;radius=Mathf.Max(head.extents.x,head.extents.z)*1.08f;
        var t=Attachment(a,HumanBodyBones.Head,name,offset);var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();const int n=32,rows=9;
        for(int r=0;r<=rows;r++)for(int i=0;i<=n;i++){float phi=r/(float)rows*Mathf.PI*.5f,theta=i/(float)n*Mathf.PI*2;v.Add(new Vector3(Mathf.Sin(theta)*Mathf.Sin(phi)*radius,Mathf.Cos(phi)*height,Mathf.Cos(theta)*Mathf.Sin(phi)*radius*1.07f));uv.Add(new Vector2(i/(float)n,r/(float)rows));}
        for(int r=0;r<rows;r++)for(int i=0;i<n;i++){int j=r*(n+1)+i;tri.AddRange(new[]{j,j+1,j+n+1,j+1,j+n+2,j+n+1});}
        var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();Backfaces(mesh);mesh.RecalculateBounds();RenderMesh(t,SaveMesh(mesh,name.Replace(" ","_")),mat);
        Shell(a,HumanBodyBones.Head,name+" rim",offset,new[]{radius*1.05f,radius*1.12f},new[]{radius*1.12f,radius*1.17f},new[]{.008f,-.014f},mat,false);
    }
    private static void Hood(Animator a,Material mat)
    {
        var head=HeadBounds(a);var bone=a.transform.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.Head).position);var offset=head.center-bone;offset.y+=.045f;
        var t=Attachment(a,HumanBodyBones.Head,"Torn hood",offset);var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();const int n=28;
        // Open forward arc frames the face rather than wrapping a solid sphere over it.
        for(int row=0;row<7;row++)for(int i=0;i<=n;i++){float angle=(.32f+i/(float)n*1.36f)*Mathf.PI;float y=.18f-row*.065f;float radius=row==0?.09f:row==1?.17f:row>4?.20f:.18f;v.Add(new Vector3(Mathf.Sin(angle)*radius,y,Mathf.Cos(angle)*radius));uv.Add(new Vector2(i/(float)n,row/6f));}
        for(int r=0;r<6;r++)for(int i=0;i<n;i++){int j=r*(n+1)+i;tri.AddRange(new[]{j,j+n+1,j+1,j+1,j+n+1,j+n+2});}
        var mesh=new Mesh{name="Torn hood"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();Backfaces(mesh);mesh.RecalculateBounds();RenderMesh(t,SaveMesh(mesh,"Ambusher_Hood"),mat);
    }
    private static void Backfaces(Mesh mesh)
    {
        // Separate back-face vertices preserve cloth normals; opposing triangles
        // sharing vertices would cancel them and produce striped black shading.
        var v=mesh.vertices;var n=mesh.normals;var uv=mesh.uv;var t=mesh.triangles;int count=v.Length;
        mesh.vertices=v.Concat(v).ToArray();mesh.normals=n.Concat(n.Select(x=>-x)).ToArray();mesh.uv=uv.Concat(uv).ToArray();
        var faces=new List<int>(t);for(int i=0;i<t.Length;i+=3)faces.AddRange(new[]{t[i+2]+count,t[i+1]+count,t[i]+count});mesh.triangles=faces.ToArray();
    }

    private static AnimationClip AuthorAttack(string kind,bool heavy)
    {
        var sample=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Animations/GroundedEnemies/CanonicalZombie.prefab"));
        var graph=PlayableGraph.Create("Distinct infected attack authoring");
        try
        {
            sample.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var a=sample.GetComponent<Animator>();a.runtimeAnimatorController=null;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Motion+kind+"_Idle.anim");
            var playable=AnimationClipPlayable.Create(graph,idle);playable.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(graph,"Pose",a).SetSourcePlayable(playable);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();playable.SetTime(idle.length*.18f);graph.Evaluate(0);
            var leftFoot=a.GetBoneTransform(HumanBodyBones.LeftFoot);var rightFoot=a.GetBoneTransform(HumanBodyBones.RightFoot);
            var lp=leftFoot.position;var rp=rightFoot.position;var lr=leftFoot.rotation;var rr=rightFoot.rotation;
            var curves=Enumerable.Range(0,HumanTrait.MuscleCount+7).Select(_=>new AnimationCurve()).ToArray();
            using(var handler=new HumanPoseHandler(a.avatar,a.transform))for(int f=0;f<=60;f++)
            {
                float t=f/60f;playable.SetTime(idle.length*.18f);graph.Evaluate(0);
                AttackPose(a,kind,heavy,t);
                Solve(a,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,lp,new Vector3(-.05f,0,1),lr);
                Solve(a,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,rp,new Vector3(.05f,0,1),rr);
                var pose=new HumanPose();handler.GetHumanPose(ref pose);
                for(int j=0;j<HumanTrait.MuscleCount;j++)curves[j].AddKey(t,pose.muscles[j]);
                for(int j=0;j<3;j++)curves[HumanTrait.MuscleCount+j].AddKey(t,pose.bodyPosition[j]);
                for(int j=0;j<4;j++)curves[HumanTrait.MuscleCount+3+j].AddKey(t,pose.bodyRotation[j]);
            }
            string name=kind+(heavy?"_HeavyStrike":"_Attack");string path=Root+"/Animations/"+name+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}clip.ClearCurves();clip.name=name;clip.frameRate=60;
            var properties=HumanTrait.MuscleName.Select(Property).Concat(new[]{"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"}).ToArray();
            for(int j=0;j<properties.Length;j++){for(int k=0;k<curves[j].length;k++)curves[j].SmoothTangents(k,0);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),properties[j]),curves[j]);}
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.startTime=0;settings.stopTime=1;settings.loopTime=false;settings.keepOriginalOrientation=true;settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=true;settings.heightFromFeet=false;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);return clip;
        }
        finally{graph.Destroy();Object.DestroyImmediate(sample);}
    }
    private static string Property(string name)
    {
        foreach(var side in new[]{"Left","Right"})foreach(var finger in new[]{"Thumb","Index","Middle","Ring","Little"}){string prefix=side+" "+finger+" ";if(name.StartsWith(prefix))return side+"Hand."+finger+"."+name.Substring(prefix.Length);}return name;
    }
    private static void AttackPose(Animator a,string kind,bool heavy,float t)
    {
        // Anticipation -> contact at 0.56 -> follow-through -> settle, with zero endpoint velocity.
        float wind=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.38f));
        float strike=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.38f,.56f,t));
        float recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.66f,1,t));
        float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.12f))*(1-recover);
        Vector3 leftStart=new Vector3(-.10f,-.26f,.18f),rightStart=new Vector3(.10f,-.32f,.10f);
        Vector3 leftWind=leftStart,rightWind=new Vector3(.28f,.10f,-.12f),leftHit=new Vector3(.12f,-.10f,.56f),rightHit=new Vector3(-.12f,-.10f,.60f);
        float twist=-22,lean=10;
        if(kind=="Runner"){leftWind=new Vector3(-.18f,-.05f,.14f);rightWind=new Vector3(.18f,-.03f,.12f);leftHit=new Vector3(-.06f,-.05f,.59f);rightHit=new Vector3(.06f,-.02f,.60f);twist=-8;lean=17;}
        if(kind=="Ambusher"){leftWind=new Vector3(-.32f,.12f,-.15f);rightWind=new Vector3(.18f,-.14f,.17f);leftHit=new Vector3(.17f,-.20f,.62f);rightHit=new Vector3(-.05f,-.07f,.52f);twist=28;lean=15;}
        if(kind=="Elite"){leftWind=new Vector3(-.12f,.30f,.06f);rightWind=new Vector3(.22f,.42f,.03f);leftHit=new Vector3(-.12f,-.26f,.58f);rightHit=new Vector3(-.08f,-.29f,.63f);twist=-14;lean=20;}
        if(heavy){leftWind=new Vector3(-.12f,.48f,.03f);rightWind=new Vector3(.12f,.48f,.03f);leftHit=new Vector3(-.12f,-.30f,.59f);rightHit=new Vector3(.12f,-.30f,.59f);lean+=8;}
        Rotate(a,HumanBodyBones.Spine,a.transform.right,(-5*wind+lean*strike)*(1-recover));
        Rotate(a,HumanBodyBones.Chest,a.transform.up,Mathf.Lerp(twist,-twist*.65f,strike)*wind*(1-recover));
        Rotate(a,HumanBodyBones.Head,a.transform.right,(-5*wind+7*strike)*(1-recover));
        Arm(a,true,Vector3.Lerp(Vector3.Lerp(leftStart,leftWind,wind),leftHit,strike),weight);
        Arm(a,false,Vector3.Lerp(Vector3.Lerp(rightStart,rightWind,wind),rightHit,strike),weight);
    }
    private static void Rotate(Animator a,HumanBodyBones bone,Vector3 axis,float angle){var t=a.GetBoneTransform(bone);if(t!=null)t.rotation=Quaternion.AngleAxis(angle,axis)*t.rotation;}
    private static void Arm(Animator a,bool left,Vector3 offset,float weight)
    {
        var upper=a.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
        var hand=a.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
        var target=Vector3.Lerp(hand.position,upper.position+a.transform.TransformVector(offset),weight);
        Solve(a,left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm,left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm,left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand,target,new Vector3(left?-.7f:.7f,-.35f,-.25f),hand.rotation);
    }
    private static void Solve(Animator a,HumanBodyBones b1,HumanBodyBones b2,HumanBodyBones b3,Vector3 target,Vector3 pole,Quaternion endRotation)
    {
        var upper=a.GetBoneTransform(b1);var lower=a.GetBoneTransform(b2);var end=a.GetBoneTransform(b3);var start=upper.position;
        float l1=Vector3.Distance(start,lower.position),l2=Vector3.Distance(lower.position,end.position);var delta=target-start;if(delta.sqrMagnitude<.0001f)return;
        float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(l1-l2)+.002f,l1+l2-.004f);var dir=delta.normalized;var bend=Vector3.ProjectOnPlane(a.transform.TransformDirection(pole),dir).normalized;
        float along=(l1*l1+d*d-l2*l2)/(2*d);var elbow=start+dir*along+bend*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
        upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(end.position-lower.position,start+dir*d-lower.position)*lower.rotation;end.rotation=endRotation;
    }
    private static void CalibrateSoles(string kind,AnimationClip clip)
    {
        var sample=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/"+kind+".prefab"));
        foreach(var b in sample.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
        var a=sample.GetComponentInChildren<Animator>();a.runtimeAnimatorController=null;sample.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var graph=PlayableGraph.Create("Attack sole height");var mesh=new Mesh();
        try
        {
            var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);var output=AnimationPlayableOutput.Create(graph,"Pose",a);output.SetSourcePlayable(playable);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();
            var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),"RootT.y");
            for(int pass=0;pass<2;pass++)
            {
                var old=AnimationUtility.GetEditorCurve(clip,binding);var curve=new AnimationCurve();
                for(int f=0;f<=60;f++){float t=f/60f;playable.SetTime(t);graph.Evaluate(0);float low=float.PositiveInfinity;foreach(var skin in a.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(mesh);foreach(var v in mesh.vertices)low=Mathf.Min(low,skin.transform.TransformPoint(v).y);}curve.AddKey(t,old.Evaluate(t)+(Mathf.Clamp(low,.004f,.025f)-low)/(a.humanScale*a.transform.lossyScale.y));}
                for(int k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);}AnimationUtility.SetEditorCurve(clip,binding,curve);
                graph.DestroyPlayable(playable);playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);output.SetSourcePlayable(playable);
            }
            EditorUtility.SetDirty(clip);
        }
        finally{graph.Destroy();Object.DestroyImmediate(mesh);Object.DestroyImmediate(sample);}
    }
}
