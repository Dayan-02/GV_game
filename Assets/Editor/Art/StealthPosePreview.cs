using System;using System.IO;using System.Linq;using System.Text;using UnityEngine;using UnityEditor;using Object=UnityEngine.Object;
public static class StealthPosePreview {
 public static void ApplyAndPreview(){StealthReferenceDressing.Apply();Main();}
 public static void RunBatch(){UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);Main();}
 public static object Main(){
 Directory.CreateDirectory("Previews/StealthReference");var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerPhase3.prefab"));var stage=new GameObject("Pose stage");var meshes=new System.Collections.Generic.List<Mesh>();
 var lights=Object.FindObjectsByType<Light>();var flags=lights.Select(l=>l.enabled).ToArray();bool fog=RenderSettings.fog;var mode=RenderSettings.ambientMode;var ambient=RenderSettings.ambientLight;
 var report=new StringBuilder();
 try{
 foreach(var l in lights)l.enabled=false;RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.5f,.53f);
 go.transform.SetPositionAndRotation(new Vector3(0,.08f,0),Quaternion.identity);foreach(var b in go.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;foreach(var t in go.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
 var lamp=new GameObject("Key").AddComponent<Light>();lamp.transform.SetParent(stage.transform);lamp.type=LightType.Directional;lamp.intensity=1.2f;lamp.shadows=LightShadows.Soft;lamp.transform.rotation=Quaternion.Euler(35,135,0);lamp.cullingMask=1<<31;
 var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(stage.transform);floor.transform.position=new Vector3(0,-.025f,0);floor.transform.localScale=new Vector3(5,.05f,5);floor.layer=31;var mat=new Material(Shader.Find("Standard")){color=new Color(.18f,.20f,.2f)};floor.GetComponent<Renderer>().sharedMaterial=mat;
 var cam=new GameObject("Pose camera").AddComponent<Camera>();cam.transform.SetParent(stage.transform);cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.14f,.15f);cam.transform.position=new Vector3(3.6f,1.5f,-2.4f);cam.transform.LookAt(new Vector3(0,.85f,0));cam.fieldOfView=35;
 var a=go.GetComponentInChildren<Animator>();a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
 var skins=go.GetComponentsInChildren<SkinnedMeshRenderer>();var baked=new Mesh[skins.Length];
 for(int i=0;i<skins.Length;i++){baked[i]=new Mesh();meshes.Add(baked[i]);var obj=new GameObject("Baked skin",typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(skins[i].transform,false);obj.layer=31;obj.GetComponent<MeshFilter>().sharedMesh=baked[i];obj.GetComponent<Renderer>().sharedMaterials=skins[i].sharedMaterials;skins[i].enabled=false;}
 string[] labels={"Standing low ready","Crouch idle","Crouch forward","Crouch left","Crouch right","Crouch backwards","Crouch aiming","Standing aiming"};
 for(int pose=0;pose<labels.Length;pose++){
 a.Rebind();a.SetBool("IsGrounded",true);a.SetBool("IsCrouching",pose>0&&pose<7);a.SetBool("IsAiming",pose>=6);a.SetFloat("Horizontal",pose==3?-1:pose==4?1:0);a.SetFloat("Vertical",pose==2||pose==6?1:pose==5?-1:0);
 a.SetLayerWeight(1,1);a.Play(pose==0?"Locomotion":pose==7?"Aim Locomotion":"Crouch Locomotion",0,.2f);a.Play(pose>=6?"Carry":"Low Ready",1,.2f);a.Update(0);
 float low=float.PositiveInfinity,high=float.NegativeInfinity;for(int i=0;i<skins.Length;i++){skins[i].BakeMesh(baked[i]);foreach(var v in baked[i].vertices){float y=skins[i].transform.TransformPoint(v).y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);}}
 report.AppendLine(labels[pose]+": min="+low+" max="+high+" hips="+a.GetBoneTransform(HumanBodyBones.Hips).position.y);
 Save(cam,"Previews/StealthReference/Pose_"+pose+".ppm");
 }
 Object.DestroyImmediate(mat);File.WriteAllText("Logs/StealthPoseMeasurements.txt",report.ToString());return report.ToString();
 }finally{Object.DestroyImmediate(go);Object.DestroyImmediate(stage);foreach(var mesh in meshes)Object.DestroyImmediate(mesh);for(int i=0;i<lights.Length;i++)lights[i].enabled=flags[i];RenderSettings.fog=fog;RenderSettings.ambientMode=mode;RenderSettings.ambientLight=ambient;}
 }
 static void Save(Camera c,string path){var rt=new RenderTexture(900,700,24);var tex=new Texture2D(900,700,TextureFormat.RGB24,false);var old=RenderTexture.active;try{c.targetTexture=rt;c.aspect=900f/700;c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,900,700),0,0);tex.Apply();var p=tex.GetPixels32();using(var f=new BinaryWriter(File.Create(path))){f.Write(Encoding.ASCII.GetBytes("P6\n900 700\n255\n"));for(int y=699;y>=0;y--)for(int x=0;x<900;x++){var v=p[y*900+x];f.Write(v.r);f.Write(v.g);f.Write(v.b);}}}finally{c.targetTexture=null;RenderTexture.active=old;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);}}
}
