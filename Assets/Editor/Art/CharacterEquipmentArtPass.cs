using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using G = CityArtGeometry;
using Object = UnityEngine.Object;

/// <summary>Native character/weapon art authoring. Leaves gameplay components and source rig transforms intact.</summary>
public static class CharacterEquipmentArtPass
{
    [MenuItem("Dead city/Art/Apply character textures weapons and motion")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before applying art.");
        // The earlier menu remains safe after upgrading from procedural mannequins.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ImportedCharacterSetup.Soldier) != null)
        {
            ImportedCharacterSetup.Apply();
            ImportedWeaponSetup.Apply();
            return;
        }
        EditorSceneManager.SaveOpenScenes();
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != OutbreakEditor.Scene) OutbreakEditor.Open();
        CharacterSurfaceLibrary.Prepare();
        var player = Object.FindAnyObjectByType<ThirdPersonPlayerController>();
        Dress(player.GetComponentInChildren<Animator>(), "Player");
        foreach (string name in new[] { "EnemyBase", "Hunter", "Runner", "Ambusher", "Elite" })
            EditPrefab("Assets/Prefabs/Enemies/" + name + ".prefab", root => Dress(root.GetComponentInChildren<Animator>(), name));
        foreach (string path in new[] { Phase2AnimationAssets.ModelPath, "Assets/Prefabs/Player/PlayerPhase3.prefab" })
            EditPrefab(path, root => Dress(root.GetComponentInChildren<Animator>(), "Player"));
        foreach (string name in new[] { "Rifle", "SMG", "Shotgun", "Pistol" })
            EditPrefab("Assets/Prefabs/Weapons/" + name + ".prefab", root => WeaponEquipmentArt.Dress(root, name));
        CharacterMotionAuthoring.Apply();
        OutbreakEditor.Save("CHARACTER_EQUIPMENT_ART");
        Debug.Log("CHARACTER_EQUIPMENT_ART_APPLIED: textured characters, four weapons and revised Humanoid motion saved.");
    }

    private static void EditPrefab(string path, Action<GameObject> edit)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void Dress(Animator animator, string kind)
    {
        if (animator == null) return;
        var parts = animator.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name == "GreyboxMesh").ToArray();
        if (parts.Length == 0) return; // Never replace imported team-authored models.
        bool player = kind == "Player";
        Color coatColor = player ? new Color(.48f,.51f,.38f) : kind == "Runner" ? new Color(.48f,.22f,.14f) : kind == "Ambusher" ? new Color(.25f,.35f,.38f) : kind == "Elite" ? new Color(.35f,.36f,.28f) : new Color(.45f,.42f,.31f);
        var coat = CharacterSurfaceLibrary.Material(kind + "_Canvas", "Canvas", coatColor);
        var pants = CharacterSurfaceLibrary.Material(kind + "_Denim", "Denim", new Color(.22f,.28f,.30f),0,.15f);
        var leather = CharacterSurfaceLibrary.Material("WornBootLeather", "Leather", new Color(.20f,.17f,.135f),0,.35f);
        var skin = CharacterSurfaceLibrary.Material(player ? "SurvivorSkin" : "InfectedSkin", "Skin", player ? new Color(.64f,.45f,.33f) : new Color(.48f,.49f,.36f),0,.28f);
        var webbing = CharacterSurfaceLibrary.Material("TacticalWebbing", "Canvas",new Color(.28f,.29f,.21f));
        var steel = CharacterSurfaceLibrary.Material("Buckles", "Steel",new Color(.38f,.40f,.41f),.8f,.6f);
        var blood = CharacterSurfaceLibrary.Material("DriedClothingStains", "Leather",new Color(.27f,.045f,.026f));
        var limb = Tube("TailoredLimb",new[]{-1f,-.9f,-.65f,0,.65f,.9f,1f},new[]{.34f,.41f,.47f,.5f,.46f,.40f,.32f});
        var torso = Tube("TailoredTorso",new[]{-1f,-.83f,-.3f,.3f,.72f,.9f,1f},new[]{.32f,.40f,.43f,.49f,.5f,.4f,.22f});
        foreach (var part in parts)
        {
            string bone = part.transform.parent.name;
            part.GetComponent<Renderer>().sharedMaterial = bone.Contains("Head") ? skin : bone.Contains("Foot") || bone.Contains("Hand") ? leather : bone.Contains("Leg") || bone == "Hips" ? pants : coat;
            if (bone.Contains("Arm"))
            {
                part.sharedMesh = limb; part.transform.localRotation = Quaternion.Euler(0,0,90);
                part.transform.localScale = bone.Contains("Upper") ? new Vector3(.19f,.17f,.20f) : new Vector3(.15f,.155f,.16f);
            }
            if (bone.Contains("Leg"))
            {
                part.sharedMesh = limb; part.transform.localRotation = Quaternion.identity;
                part.transform.localScale = bone.Contains("Upper") ? new Vector3(.24f,.24f,.27f) : new Vector3(.19f,.245f,.215f);
            }
            if (bone == "Chest")
            {
                part.sharedMesh=torso; part.transform.localRotation=Quaternion.identity;
                part.transform.localScale=new Vector3(.50f,.24f,.30f); part.transform.localPosition=new Vector3(0,-.025f,0);
            }
            if (bone == "Hips") { part.sharedMesh=torso;part.transform.localRotation=Quaternion.identity;part.transform.localPosition=new Vector3(0,.035f,0);part.transform.localScale=new Vector3(.39f,.17f,.27f); }
        }
        foreach (var renderer in animator.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.name == "GreyboxMesh") continue;
            string n=renderer.name;
            if(n.Contains("Nose") || n.Contains("Ear"))renderer.sharedMaterial=skin;
            else if(n.Contains("Eye") || n.Contains("Scalp") || n.Contains("Rim"))renderer.sharedMaterial=leather;
            else if(n.Contains("Torn"))renderer.sharedMaterial=blood;
            else if(n.Contains("Strap") || n.Contains("Webbing"))renderer.sharedMaterial=webbing;
            else if(n.Contains("Jacket") || n.Contains("Helmet") || n.Contains("Rucksack") || n.Contains("Pouch"))renderer.sharedMaterial=coat;
        }
        Transform Bone(string n)=>animator.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==n);
        var chest=Bone("Chest"); var hips=Bone("Hips");
        if(chest==null || hips==null)return;
        var old=chest.Find("SurfaceEquipment"); if(old!=null)Object.DestroyImmediate(old.gameObject);
        var detail=G.Group("SurfaceEquipment",chest);
        if(player || kind=="Elite")
        {
            G.Box("CanvasPlateCarrier",detail,new Vector3(0,-.015f,.155f),new Vector3(.35f,.29f,.065f),webbing);
            foreach(float x in new[]{-.11f,0f,.11f})
            {
                G.Box("MagazinePouch",detail,new Vector3(x,-.055f,.205f),new Vector3(.09f,.145f,.05f),coat);
                G.Box("PouchClosure",detail,new Vector3(x,.015f,.235f),new Vector3(.08f,.027f,.014f),leather);
            }
            foreach(float x in new[]{-.155f,.155f})G.Box("ShoulderWebbing",detail,new Vector3(x,.15f,.08f),new Vector3(.065f,.08f,.19f),webbing);
        }
        G.Box("JacketZip",detail,new Vector3(0,.01f,.167f),new Vector3(.013f,.32f,.008f),steel);
        if(!player)
        {
            for(int i=0;i<3;i++)G.Box("TornFabricStain",detail,new Vector3(-.1f+i*.065f,-.02f,.176f),new Vector3(.018f,.13f-i*.02f,.004f),blood,new Vector3(0,0,-15));
        }
        var belt=hips.Find("SurfaceBelt");if(belt!=null)Object.DestroyImmediate(belt.gameObject);
        belt=G.Group("SurfaceBelt",hips);
        G.Box("BeltFront",belt,new Vector3(0,.10f,.145f),new Vector3(.34f,.055f,.028f),leather);
        G.Box("BeltBuckle",belt,new Vector3(0,.10f,.163f),new Vector3(.054f,.044f,.018f),steel);
        foreach(string side in new[]{"Left","Right"})
        {
            var knee=Bone(side+"LowerLeg");var foot=Bone(side+"Foot");
            if(knee==null || foot==null)continue;
            var prior=knee.Find("KneeProtection");if(prior!=null)Object.DestroyImmediate(prior.gameObject);
            var pad=G.Primitive(PrimitiveType.Sphere,"KneeProtection",knee,new Vector3(0,-.035f,.105f),new Vector3(.15f,.19f,.06f),leather);
            prior=foot.Find("BootSole");if(prior!=null)Object.DestroyImmediate(prior.gameObject);
            G.Box("BootSole",foot,new Vector3(0,-.067f,.073f),new Vector3(.175f,.034f,.29f),leather);
        }
        if(player)
        {
            var grip=animator.GetComponent<SupportHandGrip>();
            if(grip==null)grip=animator.gameObject.AddComponent<SupportHandGrip>();
        }
    }

    private static Mesh Tube(string name,float[] levels,float[] radii)
    {
        string path=CharacterSurfaceLibrary.Root+"/Meshes/"+name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(mesh!=null)return mesh;
        const int sides=20; var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
        for(int k=0;k<levels.Length;k++)for(int i=0;i<=sides;i++)
        {
            float a=i*Mathf.PI*2/sides;
            v.Add(new Vector3(Mathf.Sin(a)*radii[k],levels[k],Mathf.Cos(a)*radii[k]));
            uv.Add(new Vector2(i/(float)sides,(levels[k]+1)*.5f));
            if(k==levels.Length-1 || i==sides)continue;
            int n=k*(sides+1)+i;tri.AddRange(new[]{n,n+1,n+sides+1,n+1,n+sides+2,n+sides+1});
        }
        for(int i=1;i<sides-1;i++){tri.AddRange(new[]{0,i+1,i});int n=(levels.Length-1)*(sides+1);tri.AddRange(new[]{n,n+i,n+i+1});}
        mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
}
