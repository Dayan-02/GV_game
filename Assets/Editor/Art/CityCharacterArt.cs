using System.Linq;
using UnityEditor;
using UnityEngine;
using G = CityArtGeometry;

/// <summary>Clothing and silhouette improvements for the existing generated rigs; no original team-art claim.</summary>
public static class CityCharacterArt
{
    [MenuItem("Zombie Outbreak/Art/Refresh generated character clothing")]
    public static void Apply()
    {
        var player=Object.FindAnyObjectByType<ThirdPersonPlayerController>();
        if(player!=null) Dress(player.GetComponentInChildren<Animator>(),true,"Player");
        foreach(string name in new[]{"EnemyBase","Hunter","Runner","Ambusher","Elite"})
        {
            string path="Assets/Prefabs/Enemies/"+name+".prefab";
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset==null)continue;
            var root=PrefabUtility.LoadPrefabContents(path);
            try { Dress(root.GetComponentInChildren<Animator>(),false,name); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
    private static void Dress(Animator animator,bool player,string kind)
    {
        if(animator==null)return;
        var parts=animator.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name=="GreyboxMesh").ToArray();
        if(parts.Length==0)return; // An imported team character must never be overwritten.
        Color jacketColor=player?new Color(.25f,.29f,.25f):kind=="Runner"?new Color(.23f,.12f,.1f):kind=="Ambusher"?new Color(.13f,.18f,.2f):new Color(.25f,.24f,.2f);
        var jacket=CityArtAssets.Solid(kind+"_WornJacket",jacketColor,.16f);
        var trousers=CityArtAssets.Solid(kind+"_Trousers",new Color(.12f,.145f,.14f),.1f);
        var leather=CityArtAssets.Solid("WornLeather",new Color(.065f,.073f,.067f),.22f);
        var skin=CityArtAssets.Solid(player?"SurvivorSkin":"InfectedSkin",player?new Color(.42f,.3f,.23f):new Color(.36f,.38f,.285f),.18f);
        var wound=CityArtAssets.Solid("DriedBlood",new Color(.2f,.035f,.028f),.25f);
        var temp=GameObject.CreatePrimitive(PrimitiveType.Sphere); var sphere=temp.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(temp);
        temp=GameObject.CreatePrimitive(PrimitiveType.Capsule); var capsule=temp.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(temp);
        foreach(var part in parts)
        {
            string bone=part.transform.parent.name;
            part.sharedMesh=sphere;
            var r=part.GetComponent<Renderer>();
            r.sharedMaterial=bone.Contains("Head")?skin:bone.Contains("Hand")||bone.Contains("Foot")?leather:bone.Contains("Leg")||bone=="Hips"?trousers:jacket;
            if(bone=="Chest")part.transform.localScale=new Vector3(.45f,.38f,.27f);
            if(bone=="Hips")
            {
                part.transform.localPosition=new Vector3(0,.06f,0);
                part.transform.localScale=new Vector3(.38f,.34f,.27f);
            }
            part.transform.localRotation=Quaternion.identity;
            if(bone.Contains("UpperLeg") || bone.Contains("LowerLeg"))
            {
                part.sharedMesh=capsule;
                part.transform.localScale=bone.Contains("Upper")?new Vector3(.215f,.27f,.24f):new Vector3(.17f,.265f,.20f);
            }
            if(bone.Contains("UpperArm") || bone.Contains("LowerArm"))
            {
                part.sharedMesh=capsule;
                part.transform.localRotation=Quaternion.Euler(0,0,90);
                part.transform.localScale=bone.Contains("Upper")?new Vector3(.17f,.19f,.18f):new Vector3(.135f,.17f,.14f);
            }
            if(bone.Contains("Foot"))part.transform.localScale=new Vector3(.18f,.16f,.30f);
        }
        Transform Bone(string name)=>animator.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
        var chest=Bone("Chest");var head=Bone("Head");
        if(chest==null||head==null)return;
        if(chest.Find("ArtClothing")==null)
        {
            var detail=G.Group("ArtClothing",chest);
            G.Primitive(PrimitiveType.Sphere,"JacketWaist",detail,new Vector3(0,-.12f,0),new Vector3(.36f,.31f,.25f),jacket);
            if(player)
            {
                G.Primitive(PrimitiveType.Sphere,"CanvasRucksack",detail,new Vector3(0,0,-.2f),new Vector3(.32f,.42f,.2f),jacket);
                foreach(float x in new[]{-.14f,.14f})
                {
                    G.Box("PackStrap",detail,new Vector3(x,.03f,.145f),new Vector3(.055f,.33f,.025f),leather);
                    G.Box("ChestPouch",detail,new Vector3(x*.7f,-.01f,.17f),new Vector3(.14f,.14f,.08f),jacket);
                }
                G.Box("BackpackWebbing",detail,new Vector3(0,-.06f,-.3f),new Vector3(.25f,.035f,.025f),leather);
            }
            else
            {
                G.Primitive(PrimitiveType.Sphere,"TornShirt",detail,new Vector3(-.08f,-.04f,.132f),new Vector3(.17f,.2f,.012f),wound);
            }
        }
        if(head.Find("ArtHead")==null)
        {
            var detail=G.Group("ArtHead",head);
            G.Primitive(PrimitiveType.Sphere,"Nose",detail,new Vector3(0,.018f,.119f),new Vector3(.05f,.065f,.05f),skin);
            foreach(float side in new[]{-1f,1f})
            {
                G.Primitive(PrimitiveType.Sphere,"Ear",detail,new Vector3(side*.116f,0,0),new Vector3(.043f,.065f,.045f),skin);
                G.Primitive(PrimitiveType.Sphere,"EyeSocket",detail,new Vector3(side*.045f,.048f,.116f),new Vector3(.043f,.023f,.018f),leather);
            }
            if(player)
            {
                G.Primitive(PrimitiveType.Sphere,"BallisticHelmet",detail,new Vector3(0,.107f,-.01f),new Vector3(.273f,.2f,.29f),jacket);
                G.Box("HelmetRim",detail,new Vector3(0,.068f,.066f),new Vector3(.264f,.024f,.19f),leather);
            }
            else G.Primitive(PrimitiveType.Sphere,"Scalp",detail,new Vector3(0,.107f,-.034f),new Vector3(.24f,.12f,.21f),leather);
        }
    }
}
