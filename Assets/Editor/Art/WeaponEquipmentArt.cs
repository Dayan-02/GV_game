using UnityEditor;
using UnityEngine;
using G = CityArtGeometry;
using Object = UnityEngine.Object;

/// <summary>Four distinctive visual assemblies using shared surfaces; muzzle and gameplay references remain intact.</summary>
public static class WeaponEquipmentArt
{
    public static void Dress(GameObject root,string kind)
    {
        if(root.transform.Find("ImportedWeaponVisual")!=null)return;
        var view=root.GetComponent<WeaponView>(); if(view==null || view.Muzzle==null)return;
        foreach(string oldName in new[]{"Receiver","Barrel","Grip","Stock","Magazine"})
        { var old=root.transform.Find(oldName);if(old!=null && old.GetComponent<Renderer>()!=null)old.GetComponent<Renderer>().enabled=false; }
        var prior=root.transform.Find("DetailedWeaponArt");if(prior!=null)Object.DestroyImmediate(prior.gameObject);
        var art=G.Group("DetailedWeaponArt",root.transform);
        var steel=CharacterSurfaceLibrary.Material("ParkerizedSteel","Steel",new Color(.29f,.32f,.34f),.8f,.64f);
        var edge=CharacterSurfaceLibrary.Material("ExposedSteelEdges","Steel",new Color(.48f,.50f,.51f),.8f,.68f);
        var polymer=CharacterSurfaceLibrary.Material("WeaponPolymer","Polymer",new Color(.17f,.19f,.185f),0,.28f);
        var tan=CharacterSurfaceLibrary.Material("RifleFurniture","Polymer",new Color(.43f,.39f,.27f),0,.3f);
        var wood=CharacterSurfaceLibrary.Material("ShotgunWalnut","Wood",new Color(.40f,.22f,.105f),0,.48f);
        var dark=CharacterSurfaceLibrary.Material("WeaponRecesses","Polymer",new Color(.06f,.065f,.066f),0,.16f);
        bool pistol=kind=="Pistol",shotgun=kind=="Shotgun",smg=kind=="SMG";
        float length=view.Muzzle.localPosition.z-.02f;
        var furniture=shotgun?wood:smg?polymer:tan;
        G.Box("LowerReceiver",art,new Vector3(0,.025f,.075f),new Vector3(.068f,.072f,pistol?.15f:.23f),steel);
        G.Box("SlideOrUpper",art,new Vector3(0,.076f,pistol?.095f:.115f),new Vector3(pistol?.071f:.080f,.065f,pistol?.25f:.29f),steel);
        G.Box("PistolGrip",art,new Vector3(0,-.055f,-.018f),new Vector3(.063f,.15f,.075f),polymer,new Vector3(-14,0,0));
        G.Box("GripHeel",art,new Vector3(0,-.135f,-.035f),new Vector3(.066f,.020f,.083f),edge);
        G.Box("TriggerGuardBottom",art,new Vector3(0,-.06f,.064f),new Vector3(.018f,.016f,.09f),steel);
        G.Box("TriggerGuardFront",art,new Vector3(0,-.035f,.104f),new Vector3(.018f,.066f,.018f),steel);
        G.Box("Trigger",art,new Vector3(0,-.025f,.06f),new Vector3(.012f,.04f,.014f),dark,new Vector3(12,0,0));
        Tube(art,"Barrel",new Vector3(0,.061f,(.20f+length)*.5f),.025f,length-.20f,steel);
        Tube(art,"MuzzleCrown",new Vector3(0,.04f,length),.035f,.03f,edge);
        Tube(art,"Bore",new Vector3(0,.04f,length+.016f),.021f,.002f,dark);
        // Align the decorative bore exactly to the authoritative muzzle axis.
        art.Find("Barrel").localPosition=new Vector3(0,.04f,(.20f+length)*.5f);
        G.Box("EjectionPort",art,new Vector3(.041f,.08f,.115f),new Vector3(.005f,.032f,.065f),dark);
        G.Box("Bolt",art,new Vector3(.044f,.081f,.102f),new Vector3(.009f,.026f,.025f),edge);
        foreach(float z in new[]{.02f,.16f})
            G.Primitive(PrimitiveType.Cylinder,"ReceiverPin",art,new Vector3(.044f,.033f,z),new Vector3(.011f,.003f,.011f),edge,new Vector3(0,0,90));
        if(pistol)
        {
            for(int i=0;i<6;i++)foreach(float x in new[]{-.037f,.037f})G.Box("SlideSerration",art,new Vector3(x,.08f,-.01f+i*.009f),new Vector3(.004f,.052f,.003f),dark);
            G.Box("RearSight",art,new Vector3(0,.119f,-.018f),new Vector3(.058f,.018f,.02f),polymer);
            G.Box("FrontSight",art,new Vector3(0,.119f,.204f),new Vector3(.016f,.02f,.022f),edge);
        }
        else
        {
            G.Box("Buttstock",art,new Vector3(0,.025f,-.185f),new Vector3(.066f,.115f,.25f),furniture);
            G.Box("RecoilPad",art,new Vector3(0,.025f,-.322f),new Vector3(.078f,.13f,.026f),polymer);
            G.Box("CheekRest",art,new Vector3(0,.09f,-.19f),new Vector3(.075f,.025f,.16f),furniture);
            if(shotgun)
            {
                Tube(art,"MagazineTube",new Vector3(0,.006f,.40f),.025f,.44f,steel);
                G.Box("WalnutPump",art,new Vector3(0,.0f,.39f),new Vector3(.086f,.09f,.19f),wood);
                for(int i=0;i<8;i++)G.Box("PumpGroove",art,new Vector3(0,-.048f,.31f+i*.023f),new Vector3(.078f,.008f,.008f),dark);
            }
            else
            {
                float fore=smg?.28f:.38f, foreLength=smg?.16f:.29f;
                G.Box("Handguard",art,new Vector3(0,.04f,fore),new Vector3(.09f,.10f,foreLength),furniture);
                G.Box("DetachableMagazine",art,new Vector3(0,-.11f,.14f),new Vector3(.056f,smg?.22f:.19f,.082f),steel,new Vector3(smg?0:-10,0,0));
                for(int i=0;i<5;i++)foreach(float x in new[]{-.047f,.047f})
                    G.Box("CoolingVent",art,new Vector3(x,.053f,fore-foreLength*.37f+i*foreLength*.18f),new Vector3(.005f,.025f,.018f),dark);
                for(int i=0;i<7;i++)G.Box("TopRail",art,new Vector3(0,.101f,.02f+i*.036f),new Vector3(.06f,.016f,.018f),steel);
                G.Box("SightBase",art,new Vector3(0,.123f,.08f),new Vector3(.066f,.025f,.075f),polymer);
                Tube(art,"OpticHousing",new Vector3(0,.163f,.08f),.060f,.07f,polymer);
                Tube(art,"OpticLens",new Vector3(0,.163f,.043f),.043f,.003f,dark);
            }
            G.Box("FrontPost",art,new Vector3(0,.084f,length-.085f),new Vector3(.023f,.057f,.020f),steel);
        }
        var support=root.transform.Find("SupportHand");if(support==null)support=G.Group("SupportHand",root.transform);
        support.localPosition=pistol?new Vector3(-.035f,-.045f,.015f):new Vector3(-.015f,-.020f,shotgun?.36f:smg?.25f:.32f);
        support.localRotation=Quaternion.identity;
        var so=new SerializedObject(view);so.FindProperty("supportHand").objectReferenceValue=support;so.ApplyModifiedPropertiesWithoutUndo();
        var bevel=BeveledSection();
        foreach(var filter in art.GetComponentsInChildren<MeshFilter>())
            if(filter.name=="LowerReceiver" || filter.name=="SlideOrUpper" || filter.name=="Buttstock" || filter.name=="Handguard" || filter.name=="WalnutPump" || filter.name=="PistolGrip")filter.sharedMesh=bevel;
        foreach(var t in art.GetComponentsInChildren<Transform>())t.gameObject.layer=root.layer;
    }
    private static Mesh BeveledSection()
    {
        string path=CharacterSurfaceLibrary.Root+"/Meshes/BeveledWeaponSection.asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
        var shape=new[]{new Vector2(-.36f,-.5f),new Vector2(.36f,-.5f),new Vector2(.5f,-.36f),new Vector2(.5f,.36f),new Vector2(.36f,.5f),new Vector2(-.36f,.5f),new Vector2(-.5f,.36f),new Vector2(-.5f,-.36f)};
        var vertices=new System.Collections.Generic.List<Vector3>();var uv=new System.Collections.Generic.List<Vector2>();var triangles=new System.Collections.Generic.List<int>();
        for(int side=0;side<2;side++)for(int i=0;i<8;i++){vertices.Add(new Vector3(shape[i].x,shape[i].y,side-.5f));uv.Add(new Vector2(i/8f,side));}
        for(int i=0;i<8;i++){int j=(i+1)%8;triangles.AddRange(new[]{i,j,j+8,i,j+8,i+8});}
        for(int i=1;i<7;i++)triangles.AddRange(new[]{0,i+1,i,8,8+i,9+i});
        mesh=new Mesh{name="BeveledWeaponSection"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    private static void Tube(Transform parent,string name,Vector3 pos,float diameter,float length,Material material)
    {G.Primitive(PrimitiveType.Cylinder,name,parent,pos,new Vector3(diameter,Mathf.Max(.001f,length)*.5f,diameter),material,new Vector3(90,0,0));}
}
