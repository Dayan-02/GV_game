using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Fits the four CC0 gun models onto the existing weapon gameplay prefabs.</summary>
public static class ImportedWeaponSetup
{
    private const string Root = "Assets/Art/ImportedWeapons/CC0";
    [MenuItem("Zombie Outbreak/Art/Apply imported weapon models")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        Phase2AnimationAssets.Folder(Root + "/Materials");
        foreach (string file in Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories))
        {
            string path = file.Replace('\\','/');
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool normal = path.Contains("Normal");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = path.Contains("Base_Color");
            importer.maxTextureSize = 2048; importer.mipmapEnabled = true;
            if (normal) importer.flipGreenChannel = path.Contains("DirectX");
            importer.SaveAndReimport();
        }
        Fit("Rifle","M4A1","M4A1",new Vector3(0,.08f,.08f),new Vector3(0,.12f,.62f),new Vector3(-.015f,.035f,.29f),"Ejector_2", "Magazine",false);
        Fit("SMG","GreaseGun","Grease_Gun",new Vector3(0,.09f,.16f),new Vector3(0,.10f,.50f),new Vector3(-.015f,.015f,.24f),"Bolt", "Magazine",false);
        Fit("Shotgun","Shotgun","Shotgun",new Vector3(0,.07f,.44f),new Vector3(0,.10f,.85f),new Vector3(-.015f,.035f,.415f),"Fore_Stock", null,true);
        Fit("Pistol","Pistol_MK","Makarov",new Vector3(0,.035f,.01f),new Vector3(0,.085f,.15f),new Vector3(-.025f,-.015f,.02f),"Slide", "Magazine",false);
        AssetDatabase.SaveAssets();
    }
    private static void Fit(string kind,string folder,string stem,Vector3 offset,Vector3 muzzle,Vector3 support,string boltName,string magazineName,bool pump)
    {
        string modelPath=Root+"/"+folder+"/"+stem+".fbx";
        var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
        importer.SaveAndReimport();
        var material=Material(folder,stem);
        string path="Assets/Prefabs/Weapons/"+kind+".prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var view=root.GetComponent<WeaponView>();
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))
                if(!r.transform.IsChildOf(view.Muzzle))r.enabled=false;
            foreach(string name in new[]{"DetailedWeaponArt","ImportedWeaponVisual"})
            {var old=root.transform.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);}
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),root.transform);
            PrefabUtility.UnpackPrefabInstance(visual,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            visual.name="ImportedWeaponVisual";visual.transform.localPosition=offset;
            visual.transform.localRotation=Quaternion.Euler(0,180,0);visual.transform.localScale=Vector3.one;
            foreach(var r in visual.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();
            foreach(var t in visual.GetComponentsInChildren<Transform>(true))t.gameObject.layer=root.layer;
            foreach(var c in visual.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
            var magazine=Find(visual,magazineName);
            if(kind=="Pistol" && magazine!=null)magazine.localPosition=new Vector3(0,-.055f,.012f);
            view.Muzzle.localPosition=muzzle;view.Muzzle.localRotation=Quaternion.identity;
            var hand=root.transform.Find("SupportHand");if(hand==null){hand=new GameObject("SupportHand").transform;hand.SetParent(root.transform,false);}
            hand.localPosition=support;hand.localRotation=Quaternion.identity;OutbreakEditor.Set(view,"supportHand",hand);
            if(pump && Find(visual,boltName)!=null)hand.SetParent(Find(visual,boltName),true);
            var mechanism=root.GetComponent<WeaponMechanism>()??root.AddComponent<WeaponMechanism>();
            var serialized=new SerializedObject(mechanism);
            serialized.FindProperty("bolt").objectReferenceValue=Find(visual,boltName);
            serialized.FindProperty("magazine").objectReferenceValue=magazine;
            serialized.FindProperty("pumpAction").boolValue=pump;
            serialized.FindProperty("travel").floatValue=pump?.065f:kind=="Pistol"?.025f:.018f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static Transform Find(GameObject root,string name) => name==null?null:root.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name==name);
    private static Material Material(string folder,string stem)
    {
        string prefix=Root+"/"+folder+"/"+stem;
        string path=Root+"/Materials/"+stem+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,path);}
        material.color=Color.white;material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"_Base_Color.png");
        string normal=File.Exists(prefix+"_Normal.png")?prefix+"_Normal.png":prefix+"_Normal_DirectX.png";
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));material.SetFloat("_BumpScale",.8f);material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_MetallicGlossMap",PackMaps(prefix,stem));material.EnableKeyword("_METALLICGLOSSMAP");
        material.SetFloat("_GlossMapScale",.85f);material.SetFloat("_SmoothnessTextureChannel",0);
        EditorUtility.SetDirty(material);return material;
    }
    private static Texture2D PackMaps(string prefix,string stem)
    {
        string path=Root+"/Materials/"+stem+"_MetalSmooth.asset";
        string metalPath=prefix+"_Metallic.png",roughPath=prefix+"_Roughness.png";
        var mi=(TextureImporter)AssetImporter.GetAtPath(metalPath);var ri=(TextureImporter)AssetImporter.GetAtPath(roughPath);
        bool mr=mi.isReadable,rr=ri.isReadable;
        try
        {
            mi.isReadable=true;ri.isReadable=true;mi.SaveAndReimport();ri.SaveAndReimport();
            var metal=AssetDatabase.LoadAssetAtPath<Texture2D>(metalPath);var rough=AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(texture==null){texture=new Texture2D(metal.width,metal.height,TextureFormat.RGBA32,true,true);AssetDatabase.CreateAsset(texture,path);}
            else texture.Reinitialize(metal.width,metal.height,TextureFormat.RGBA32,true);
            var pixels=metal.GetPixels();
            for(int y=0;y<metal.height;y++)for(int x=0;x<metal.width;x++)
            {int i=y*metal.width+x;float m=pixels[i].r;pixels[i]=new Color(m,m,m,1-rough.GetPixelBilinear((x+.5f)/metal.width,(y+.5f)/metal.height).r);}
            texture.SetPixels(pixels);texture.Apply(true,false);
            EditorUtility.CompressTexture(texture,TextureFormat.DXT5,TextureCompressionQuality.Normal);
            texture.name=stem+" Metal + Smoothness";EditorUtility.SetDirty(texture);return texture;
        }
        finally{mi.isReadable=mr;ri.isReadable=rr;mi.SaveAndReimport();ri.SaveAndReimport();}
    }
}
