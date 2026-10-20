using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Runtime animation/physics replay. Synthetic movement inputs, not a human keyboard test.</summary>
[InitializeOnLoad]
public static class PlayerLocomotionReview
{
    private const string Key="Player.GaitReview";
    private static double deadline;
    private static float started;
    private static int frame,stage;
    private static ThirdPersonPlayerController motor;
    private static Animator animator;
    private static SkinnedMeshRenderer boots;
    private static Mesh mesh;
    private static float low=float.PositiveInfinity,high=float.NegativeInfinity;
    private static readonly StringBuilder report=new StringBuilder();
    static PlayerLocomotionReview(){if(SessionState.GetBool(Key,false))Hook();}
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode first.");
        SessionState.SetBool(Key,true);Hook();EditorApplication.isPaused=false;EditorApplication.EnterPlaymode();
    }
    public static void RunBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        Begin();
    }
    private static void Hook(){stage=0;frame=0;deadline=EditorApplication.timeSinceStartup+75;EditorApplication.update-=Tick;EditorApplication.update+=Tick;report.Clear();}
    private static void Field(string name,object value)
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var field=typeof(ThirdPersonPlayerController).GetField("<"+name+">k__BackingField",flags)??typeof(ThirdPersonPlayerController).GetField(char.ToLowerInvariant(name[0])+name.Substring(1),flags);
        if(field==null)throw new Exception("Replay field not found: "+name);
        field.SetValue(motor,value);
    }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Gait review timed out");
            if(!EditorApplication.isPlaying)return;
            EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();
            var ui=Object.FindAnyObjectByType<GameUIController>();if(ui!=null){OutbreakEditor.Values(ui,s=>s.FindProperty("pauseOnFocusLoss").boolValue=false);ui.Resume();}Time.timeScale=1;
            if(stage==0)
            {
                if(Time.time<.15f)return;
                motor=Object.FindAnyObjectByType<ThirdPersonPlayerController>();motor.enabled=false;
                var waves=Object.FindAnyObjectByType<WaveManager>();waves.enabled=false;
                foreach(var actor in Object.FindObjectsByType<EnemyActor>())foreach(var b in actor.GetComponents<MonoBehaviour>())if(b is HunterBrain||b is RunnerBrain||b is AmbusherBrain||b is EliteBrain)b.enabled=false;
                animator=motor.GetComponentInChildren<Animator>();boots=animator.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name.Contains("Boots"));mesh=new Mesh();
                motor.GetComponent<PlayerAimController>().enabled=false;
                Camera.main.GetComponent<ThirdPersonCameraController>().enabled=false;
                started=Time.time;stage=1;low=float.PositiveInfinity;high=float.NegativeInfinity;
                return;
            }
            float elapsed=Time.time-started;int gait=Mathf.FloorToInt(elapsed/1.2f);
            if(gait>6){report.AppendLine("Overall lowest boot clearance: "+low+" .. "+high+" metres");if(low<-.04f||high>.10f)throw new Exception("Runtime contact outside review tolerance");Finish(true);return;}
            float speed=gait==0?1.6f:gait==1?3.6f:gait==2?6f:gait<6?1.25f:Mathf.Lerp(3.6f,0,Mathf.Clamp01((elapsed-7.2f)/.8f));
            bool aiming=gait>=3&&gait<=5;
            Vector3 direction=gait==4?Vector3.left:gait==5?Vector3.back:Vector3.forward;
            Field("PlanarSpeed",speed);Field("PlanarVelocity",direction*speed);Field("IsGrounded",true);Field("IsSprinting",gait==2);Field("VerticalVelocity",-2f);
            typeof(PlayerAimController).GetField("<IsAiming>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(motor.GetComponent<PlayerAimController>(),aiming);
            motor.GetComponent<CharacterController>().Move((direction*speed+Vector3.down*2)*Mathf.Min(Time.deltaTime,.04f));Physics.SyncTransforms();
            // Frame poses are sampled after normal Animator/IK evaluation from the prior player loop.
            boots.BakeMesh(mesh);float min=mesh.vertices.Min(v=>boots.transform.TransformPoint(v).y);
            if(Physics.Raycast(motor.transform.position+Vector3.up*.3f,Vector3.down,out var hit,1,1<<8,QueryTriggerInteraction.Ignore))
            {float clearance=min-hit.point.y;if(elapsed>.25f){low=Mathf.Min(low,clearance);high=Mathf.Max(high,clearance);}}
            if(frame%8==0)
            {
                report.AppendLine("time="+elapsed+" gait="+gait+" speed="+speed+" clips="+string.Join(",",animator.GetCurrentAnimatorClipInfo(0).Select(c=>c.clip.name+":"+c.weight)));
                var camera=Camera.main;bool rear=gait>=3;camera.transform.position=motor.transform.position+(rear?new Vector3(2,1.45f,-4.5f):new Vector3(4.6f,1.35f,-.4f));camera.transform.LookAt(motor.transform.position+Vector3.up*.9f);camera.fieldOfView=38;
                Capture(camera,frame.ToString("D4"));
            }
            frame++;
        }
        catch(Exception e){report.AppendLine("FAIL: "+e);Finish(false);}
    }
    private static void Finish(bool passed)
    {
        report.AppendLine(passed?"PASS: Runtime gait/contact review":"Review failed");File.WriteAllText("Logs/PlayerRuntimeGait.txt",report.ToString());
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;if(mesh!=null)Object.DestroyImmediate(mesh);EditorApplication.ExitPlaymode();
        if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(passed?0:1);
    }
    private static void Capture(Camera camera,string name)
    {
        var rt=new RenderTexture(900,650,24);var texture=new Texture2D(900,650,TextureFormat.RGB24,false);var active=RenderTexture.active;
        try{camera.targetTexture=rt;camera.aspect=900f/650;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,900,650),0,0);texture.Apply();var pixels=texture.GetPixels32();
        using(var file=new BinaryWriter(File.Create("Logs/Captures/PlayerRuntime_"+name+".ppm"))){file.Write(System.Text.Encoding.ASCII.GetBytes("P6\n900 650\n255\n"));for(int y=649;y>=0;y--)for(int x=0;x<900;x++){var c=pixels[y*900+x];file.Write(c.r);file.Write(c.g);file.Write(c.b);}}}
        finally{camera.targetTexture=null;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
    }
}
