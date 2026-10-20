using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Deterministic synthetic stance replay; does not replace a human control-feel review.</summary>
[InitializeOnLoad]
public static class StealthLocomotionReview
{
    private const string Key="Outbreak.StealthReview";
    private static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly string[] Names={"Stand","Crouch idle","Crouch forward","Crouch aim left","Crouch aim backward","Crouch aim right","Stand transition","Walk","Run","Crouch shooting","Crouch reloading"};
    private static readonly StringBuilder report=new StringBuilder();
    private static ThirdPersonPlayerController motor;
    private static PlayerStanceController stance;
    private static PlayerAimController aim;
    private static PlayerWeaponController weapons;
    private static Animator animator;
    private static SkinnedMeshRenderer boots;
    private static CharacterController capsule;
    private static Mesh mesh;
    private static double deadline;
    private static int frame,previous=-1;
    private static float started,bottom,low,high;
    private static bool initialized;
    static StealthLocomotionReview(){if(SessionState.GetBool(Key,false))Hook();}
    public static void RunBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        SessionState.SetBool(Key,true);Hook();EditorApplication.EnterPlaymode();
    }
    public static void PrepareAndRun(){StealthLocomotionSetup.PrepareFootContact();RunBatch();}
    private static void Hook(){initialized=false;previous=-1;frame=0;low=float.PositiveInfinity;high=float.NegativeInfinity;report.Clear();deadline=EditorApplication.timeSinceStartup+160;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    private static void Set(string name,object value)
    {
        var f=typeof(ThirdPersonPlayerController).GetField("<"+name+">k__BackingField",Fields)??typeof(ThirdPersonPlayerController).GetField(char.ToLowerInvariant(name[0])+name.Substring(1),Fields);
        if(f==null)throw new Exception("Missing replay field "+name);f.SetValue(motor,value);
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);report.AppendLine("PASS: "+message);}
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Stance review timeout");
            if(!EditorApplication.isPlaying)return;
            EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();
            if(!initialized)
            {
                var ui=Object.FindAnyObjectByType<GameUIController>();if(ui!=null){OutbreakEditor.Values(ui,s=>s.FindProperty("pauseOnFocusLoss").boolValue=false);ui.Resume();ui.enabled=false;}
                Time.timeScale=1;if(Time.time<.2f)return;Time.captureDeltaTime=1f/60;
                motor=Object.FindAnyObjectByType<ThirdPersonPlayerController>();motor.enabled=false;
                stance=motor.GetComponent<PlayerStanceController>();aim=motor.GetComponent<PlayerAimController>();aim.enabled=false;
                weapons=motor.GetComponent<PlayerWeaponController>();capsule=motor.GetComponent<CharacterController>();
                animator=motor.GetComponentInChildren<Animator>();boots=animator.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name.Contains("Boots"));mesh=new Mesh();
                bottom=capsule.center.y-capsule.height*.5f;
                var waves=Object.FindAnyObjectByType<WaveManager>();if(waves!=null)waves.enabled=false;
                foreach(var actor in Object.FindObjectsByType<EnemyActor>())foreach(var b in actor.GetComponents<MonoBehaviour>())if(b is HunterBrain||b is RunnerBrain||b is AmbusherBrain||b is EliteBrain)b.enabled=false;
                Camera.main.GetComponent<ThirdPersonCameraController>().enabled=false;
                Directory.CreateDirectory("Previews/StealthReference/Motion");started=Time.time;initialized=true;return;
            }
            float elapsed=Time.time-started;int stage=Mathf.FloorToInt(elapsed/1.1f);
            if(stage>=Names.Length){SafetyChecks();Check(low>=-.04f&&high<=.10f,"Boot clearance within -4cm / +10cm tolerance: "+low+" .. "+high);Finish(true);return;}
            bool crouch=(stage>=1&&stage<=5)||stage>=9,aiming=stage>=3&&stage<=5||stage>=9;
            float speed=stage==2?1.05f:stage>=3&&stage<=5?.85f:stage==7?1.6f:stage==8?3.6f:0;
            Vector3 direction=stage==3?Vector3.left:stage==4?Vector3.back:stage==5?Vector3.right:Vector3.forward;
            Set("PlanarSpeed",speed);Set("PlanarVelocity",direction*speed);Set("IsGrounded",true);Set("IsSprinting",false);Set("VerticalVelocity",-2f);
            typeof(PlayerAimController).GetField("<IsAiming>k__BackingField",Fields).SetValue(aim,aiming);
            if(stage!=previous)
            {
                Check(stance.TrySetCrouching(crouch),"Stance request "+Names[stage]);
                report.AppendLine("STAGE "+stage+": "+Names[stage]);previous=stage;
            }
            if(stage==9&&elapsed-stage*1.1f>.6f&&weapons.LastShotTime<0){int ammo=weapons.MagazineAmmo;Check(weapons.TryFire()&&weapons.MagazineAmmo==ammo-1,"Crouched shot consumes one round");}
            if(stage==10&&!weapons.IsReloading)Check(weapons.TryReload(),"Crouched reload starts");
            capsule.Move((direction*speed+Vector3.down*2)*Time.deltaTime);Physics.SyncTransforms();
            if(Mathf.Abs(capsule.center.y-capsule.height*.5f-bottom)>.0001f)throw new Exception("Capsule base moved during stance blend");
            boots.BakeMesh(mesh);float min=mesh.vertices.Min(v=>boots.transform.TransformPoint(v).y);
            if(Physics.Raycast(motor.transform.position+Vector3.up*.3f,Vector3.down,out var hit,1,1<<8,QueryTriggerInteraction.Ignore)&&elapsed>.3f){float clearance=min-hit.point.y;if(clearance<low){var feet=animator.GetComponent<HumanoidFootPlacement>();report.AppendLine("MIN frame="+frame+" stage="+Names[stage]+" clearance="+clearance+" IKleft="+feet.LastLeftContact.ToString("F4")+" IKright="+feet.LastRightContact.ToString("F4"));}low=Mathf.Min(low,clearance);high=Mathf.Max(high,clearance);}
            if(frame%10==0)
            {
                var camera=Camera.main;camera.transform.position=motor.transform.position+new Vector3(3.4f,1.3f,-2.8f);camera.transform.LookAt(motor.transform.position+Vector3.up*.78f);camera.fieldOfView=38;
                Capture(camera,"Previews/StealthReference/Motion/"+frame.ToString("D4")+".png");
                report.AppendLine("FRAME "+frame.ToString("D4")+" "+Names[stage]+" clips="+string.Join(",",animator.GetCurrentAnimatorClipInfo(0).Select(c=>c.clip.name+":"+c.weight)));
                var diagnostic=animator.GetComponent<HumanoidFootPlacement>();report.AppendLine("IK enabled="+diagnostic.enabled+" calls="+diagnostic.IkCallbacks+" probe="+diagnostic.IkProbe.ToString("F4"));
            }
            frame++;
        }
        catch(Exception e){report.AppendLine("FAIL: "+e);Finish(false);}
    }
    private static void SafetyChecks()
    {
        Check(Mathf.Abs(capsule.height-1.55f)<.01f,"Crouched capsule reaches 1.55m");
        var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.name="Temporary stance clearance check";ceiling.layer=8;
        ceiling.transform.position=motor.transform.position+Vector3.up*1.74f;ceiling.transform.localScale=new Vector3(2,.16f,2);Physics.SyncTransforms();
        Check(!stance.CanStand()&&!stance.TrySetCrouching(false),"Standing blocked under low ceiling");Object.DestroyImmediate(ceiling);Physics.SyncTransforms();
        Check(stance.CanStand()&&stance.TrySetCrouching(false),"Standing allowed after ceiling clears");
        Time.timeScale=0;Check(!stance.TrySetCrouching(true),"Stance changes rejected while paused");Time.timeScale=1;
        motor.GetComponent<PlayerHealth>().Die();Check(!stance.TrySetCrouching(true),"Stance changes rejected after death");
        report.AppendLine("PASS: Capsule bottom stayed fixed throughout stance transitions.");
    }
    private static void Finish(bool success)
    {
        report.AppendLine(success?"PASS: Synthetic stance replay":"FAILED synthetic stance replay");File.WriteAllText("Logs/StealthRuntimeReview.txt",report.ToString());
        Time.captureDeltaTime=0;Time.timeScale=1;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;if(mesh!=null)Object.DestroyImmediate(mesh);EditorApplication.ExitPlaymode();
        if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(success?0:1);
    }
    private static void Capture(Camera camera,string path)
    {
        var rt=new RenderTexture(800,600,24);var texture=new Texture2D(800,600,TextureFormat.RGB24,false);var active=RenderTexture.active;var old=camera.targetTexture;float aspect=camera.aspect;
        try{camera.targetTexture=rt;camera.aspect=4f/3;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,800,600),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{camera.targetTexture=old;camera.aspect=aspect;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
    }
}
