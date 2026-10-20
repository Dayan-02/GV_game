# Complete current C# source

Snapshot generated 17 October 2026 after the enemy AI behaviour update. Authoritative source remains in Assets. Includes all 131 C# files.


## AIBehaviorValidation

Full file path: D:/GV project/Assets/Editor/AIBehaviorValidation.cs

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>Isolated Play Mode regression scenarios; never saves or edits the gameplay scene.</summary>
[InitializeOnLoad]
public static class AIBehaviorValidation
{
    private const string Running = "Outbreak.AIValidation";
    private static IEnumerator sequence;
    private static readonly List<string> results = new List<string>();
    private static EnemyWorld world;
    private static PlayerHealth player;
    private static readonly List<EnemyActor> actors = new List<EnemyActor>();
    private static GameObject wall;
    private static NavMeshData data;
    private static NavMeshDataInstance navigation;
    private static double started;
    private static readonly Vector3 Origin = new Vector3(1000, 0, 1000);
    private static string failure;

    static AIBehaviorValidation() { EditorApplication.update += Tick; }

    [MenuItem("Zombie Outbreak/AI/Run isolated behavior validation")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before validation.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SessionState.SetString(Running + ".Scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    public static void RunCitySmoke()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the current scene before the city check.");
        EditorSceneManager.OpenScene("Assets/Scenes/EnhancedCity/ZombieMap.unity");
        SessionState.SetBool(Running + ".City", true);
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (SessionState.GetBool(Running + ".Restore", false) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetBool(Running + ".Restore", false);
            RestoreScene();
        }
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (sequence == null)
            {
                results.Clear();
                actors.Clear();
                failure = null;
                started = EditorApplication.timeSinceStartup;
                Application.logMessageReceived += CaptureError;
                sequence = SessionState.GetBool(Running + ".City", false) ? CityScenario() : Scenarios();
            }
            if (failure != null) throw new Exception(failure);
            if (EditorApplication.timeSinceStartup - started > 150) throw new TimeoutException("AI validation exceeded 150 seconds.");
            if (!sequence.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void CaptureError(string message, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            failure = message + "\n" + trace;
    }

    private static void Finish(string error)
    {
        Application.logMessageReceived -= CaptureError;
        SessionState.SetBool(Running, false);
        Time.timeScale = 1;
        navigation.Remove();
        if (data != null) Object.Destroy(data);
        Directory.CreateDirectory("Logs");
        bool city = SessionState.GetBool(Running + ".City", false);
        SessionState.SetBool(Running + ".City", false);
        File.WriteAllText(city ? "Logs/AICityValidation.txt" : "Logs/AIBehaviorValidation.txt",
            (error == null ? "PASS" : "FAIL") + "\n" + string.Join("\n", results) +
            "\nElapsed editor seconds: " + (EditorApplication.timeSinceStartup - started).ToString("0.00") +
            (error == null ? "" : "\n" + error));
        sequence = null;
        if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        else
        {
            SessionState.SetBool(Running + ".Restore", !city);
            EditorApplication.ExitPlaymode();
            Debug.Log(error == null ? "AI behavior validation passed. See Logs/AIBehaviorValidation.txt." : error);
        }
    }

    private static void RestoreScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += RestoreScene; return; }
        string path = SessionState.GetString(Running + ".Scene", "");
        if (!string.IsNullOrEmpty(path)) EditorSceneManager.OpenScene(path);
    }

    private static IEnumerator CityScenario()
    {
        yield return null;
        world = Object.FindAnyObjectByType<EnemyWorld>();
        Check(world != null && world.player != null && world.weapons != null, "City has wired AI/player/weapon references");
        player = world.player;
        Check(world.tacticalPoints != null && world.tacticalPoints.Length > 0, "City supplies tactical markers");
        Check(world.weapons.CurrentWeapon != null, "FPS weapon initializes in the enhanced city");
        string[] kinds = { "Hunter", "Runner", "Ambusher", "Elite" };
        int spawned = 0;
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI * 2 / 16;
            Vector3 desired = player.transform.position + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * (9 + i % 3 * 2);
            if (!NavMesh.SamplePosition(desired, out var hit, 4, NavMesh.AllAreas)) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + kinds[i % 4] + ".prefab");
            var root = Object.Instantiate(prefab, hit.position, Quaternion.LookRotation(
                Vector3.ProjectOnPlane(player.transform.position - hit.position, Vector3.up)));
            root.GetComponent<EnemyActor>().Initialize(world);
            spawned++;
        }
        Check(spawned >= 8, "Mixed group can spawn on the actual city navigation mesh");
        int peak = 0, peakAttackers = 0, movingSamples = 0;
        float until = Time.time + 15;
        while (Time.time < until)
        {
            player.Heal(100);
            peak = Mathf.Max(peak, world.Living.Count);
            peakAttackers = Mathf.Max(peakAttackers, world.ActiveAttackers);
            if (world.ActiveAttackers > world.MaximumAttackers) throw new Exception("City attack limit exceeded.");
            foreach (var a in world.Living) if (a != null && a.Movement.Velocity.sqrMagnitude > .1f) movingSamples++;
            yield return null;
        }
        Check(movingSamples > 0, "Enemy brains navigate in the actual city");
        Check(peakAttackers > 0 && peakAttackers <= world.MaximumAttackers, "City attacks respect the two-attacker cap");
        results.Add("City smoke: peak living=" + peak + ", peak committed attackers=" + peakAttackers);
        results.Add("Not a five-wave balance run or a target-device frame-rate benchmark.");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAILED: " + name);
        results.Add("PASS: " + name);
    }

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    private static EnemyActor Spawn(string kind, Vector3 offset)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + kind + ".prefab");
        var root = Object.Instantiate(prefab, Origin + offset, Quaternion.identity);
        foreach (var b in root.GetComponents<MonoBehaviour>())
            if (b is HunterBrain || b is RunnerBrain || b is AmbusherBrain || b is EliteBrain) b.enabled = false;
        var actor = root.GetComponent<EnemyActor>();
        actor.Initialize(world);
        actor.Perception.enabled = false;
        actors.Add(actor);
        return actor;
    }

    private static void Place(EnemyActor actor, Vector3 offset, Vector3 target)
    {
        actor.Combat.Cancel();
        actor.Movement.Stop();
        actor.GetComponent<NavMeshAgent>().Warp(Origin + offset);
        actor.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(target - actor.transform.position, Vector3.up));
        Set(actor.Combat, "readyAt", 0f);
        actor.Movement.ClearFailure();
        Physics.SyncTransforms();
        actor.Perception.Sample();
    }

    private static IEnumerator Scenarios()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Validation ground";
        ground.layer = 8;
        ground.transform.position = Origin + Vector3.down * .5f;
        ground.transform.localScale = new Vector3(80, 1, 80);
        var sources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(Origin + Vector3.down * .5f, Quaternion.identity, Vector3.one),
                size = new Vector3(80, 1, 80), area = 0 }
        };
        data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
            new Bounds(Origin, new Vector3(82, 10, 82)), Vector3.zero, Quaternion.identity);
        navigation = NavMesh.AddNavMeshData(data);
        var playerRoot = new GameObject("Validation player");
        playerRoot.transform.position = Origin + Vector3.forward * 10;
        var capsule = playerRoot.AddComponent<CharacterController>();
        capsule.height = 1.8f;
        capsule.center = Vector3.up * .9f;
        player = playerRoot.AddComponent<PlayerHealth>();
        var worldRoot = new GameObject("Validation world");
        worldRoot.SetActive(false);
        world = worldRoot.AddComponent<EnemyWorld>();
        world.player = player;
        world.tacticalPoints = Array.Empty<MapMarker>();
        worldRoot.SetActive(true);
        var graph = worldRoot.AddComponent<HunterNavigationGraph>();
        Set(graph, "center", Origin);
        graph.Build();
        var hunter = Spawn("Hunter", Vector3.zero);
        var runner = Spawn("Runner", new Vector3(-15, 0, 0));
        var ambusher = Spawn("Ambusher", new Vector3(15, 0, 0));
        var elite = Spawn("Elite", new Vector3(0, 0, -15));
        yield return null;
        Physics.SyncTransforms();
        hunter.Perception.Sample();
        Check(hunter.Perception.Visible, "Player in field of view is detected");
        hunter.transform.rotation = Quaternion.Euler(0, 180, 0);
        hunter.Perception.Sample();
        Check(!hunter.Perception.Visible && hunter.Perception.HasMemory, "Behind-view target becomes last-known memory");
        Vector3 remembered = hunter.Perception.LastKnownPosition;
        player.transform.position += Vector3.right * 6;
        Physics.SyncTransforms();
        hunter.Perception.Sample();
        Check(hunter.Perception.TargetPosition == remembered, "Unseen movement cannot update target position");
        Check(hunter.Perception.Weapon == WeaponCategory.None && !hunter.Perception.PlayerReloading &&
            hunter.Perception.PlayerWeakness == 0, "Unobserved tactical facts are withheld");

        Vector3 shot = Origin + new Vector3(2, 0, 1);
        world.ReportNoise(shot, 30, EnemyNoiseKind.Gunshot);
        world.ReportNoise(Origin + Vector3.right * 35, 2, EnemyNoiseKind.Sprint);
        hunter.Perception.Sample();
        Check(hunter.Perception.Heard && hunter.Perception.LastKnownPosition == shot,
            "Audible gunshot survives a later inaudible footstep");
        Set(hunter.Perception, "lastKnowledgeAt", Time.time - 20);
        hunter.Perception.Sample();
        Check(!hunter.Perception.HasMemory, "Memory expires without repeated sightings or new sounds");
        hunter.Perception.Sample();
        Check(!hunter.Perception.HasMemory, "Previously consumed sound does not refresh memory");
        player.transform.position = Origin + Vector3.forward * 1.5f;
        Place(hunter, Vector3.zero, player.transform.position);
        Place(runner, new Vector3(1, 0, 1.5f), player.transform.position);
        Place(ambusher, new Vector3(-1, 0, 1.5f), player.transform.position);
        Check(hunter.Combat.TryAttack() && runner.Combat.TryAttack(), "Two nearby enemies can acquire attack slots");
        Check(!ambusher.Combat.TryAttack() && world.ActiveAttackers == 2, "Third simultaneous attacker is denied");
        Time.timeScale = 0;
        Call(hunter.Combat, "Update");
        Check(hunter.Combat.IsAttacking && world.HasAttackSlot(hunter), "Pause preserves committed attack and slot");
        Time.timeScale = 1;
        hunter.Combat.Cancel();
        Check(ambusher.Combat.TryAttack(), "Cancelled attack releases slot for another enemy");
        foreach (var a in actors) a.Combat.Cancel();

        player.transform.position = Origin + Vector3.forward * 1.5f;
        Place(hunter, Vector3.zero, player.transform.position);
        wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.layer = 8;
        wall.transform.position = Origin + new Vector3(0, 1.4f, .75f);
        wall.transform.localScale = new Vector3(5, 3, .2f);
        Physics.SyncTransforms();
        hunter.Perception.Sample();
        Check(!hunter.Combat.TryAttack(true), "Heavy attacks cannot begin through a wall");
        wall.SetActive(false);
        Physics.SyncTransforms();
        hunter.Perception.Sample();
        Check(hunter.Combat.TryAttack(), "Clear strike begins after cover is removed");
        float hp = player.CurrentHealth;
        hunter.Combat.AnimationImpact();
        Check(player.CurrentHealth == hp, "Early animation events cannot apply damage");
        Set(hunter.Combat, "impactAt", Time.time - 1);
        wall.SetActive(true);
        Physics.SyncTransforms();
        hunter.Combat.AnimationImpact();
        Check(player.CurrentHealth == hp, "Cover introduced during windup blocks impact");
        wall.SetActive(false);
        Place(hunter, Vector3.zero, player.transform.position);
        Check(hunter.Combat.TryAttack(), "Strike can restart after explicit cancellation and reset");
        Set(hunter.Combat, "impactAt", Time.time - 1);
        hunter.Combat.AnimationImpact();
        float afterHit = player.CurrentHealth;
        hunter.Combat.AnimationImpact();
        Check(afterHit < hp && player.CurrentHealth == afterHit, "One committed strike deals damage only once");
        hunter.Combat.Cancel();
        Check(!hunter.Combat.IsReady, "Recovery cooldown prevents immediate second strike");
        player.Heal(100);

        float[] scores = new float[5];
        RunnerBrain.Score(2.5f, 1, 0, false, false, true, true, scores, 3, true);
        Check(scores[1] > 0, "Runner scoring uses configurable combat range");
        RunnerBrain.Score(1, 1, 0, false, false, true, true, scores, 3, false);
        Check(scores[1] == 0, "Runner scoring rejects unavailable attacks");
        var path = new List<Vector3>();
        Check(graph.FindPath(Origin, Origin + new Vector3(15, 0, 12), path) && path.Count > 2,
            "Hunter retains explicit A* route generation on a real NavMesh");
        Check(!hunter.Movement.MoveTo(Origin + Vector3.right * 500, 1) && hunter.Movement.Failed,
            "Off-mesh destination reports failure");
        Check(hunter.Movement.MoveTo(Origin + Vector3.right * 5, 1), "Different reachable destination recovers from failure");
        hunter.Movement.Stop();

        var markerRoot = new GameObject("Reserved cover");
        markerRoot.transform.position = Origin + new Vector3(8, 0, 4);
        var marker = markerRoot.AddComponent<MapMarker>();
        marker.kind = MapMarkerKind.Cover;
        world.tacticalPoints = new[] { marker };
        Check(world.Reserve(marker, ambusher) && !world.Reserve(marker, elite), "Cover reservations are exclusive across types");
        Set(ambusher.GetComponent<AmbusherBrain>(), "selected", marker);
        Call(ambusher.GetComponent<AmbusherBrain>(), "SetState", AmbusherState.Warn, .6f);
        Check(world.Available(marker, elite) && ambusher.GetComponent<AmbusherBrain>().Selected == null,
            "Ambusher warning/strike transition releases hiding position");
        Check(world.Reserve(marker, elite), "Elite can reserve released cover");
        world.Remove(elite);
        Check(world.Available(marker, ambusher), "Removed enemy releases tactical reservation");
        world.Register(elite);

        // Actual brain updates and NavMesh movement: no synthetic root motion.
        player.transform.position = Origin;
        Place(runner, new Vector3(0, 0, -7), player.transform.position);
        runner.Perception.enabled = true;
        var runnerBrain = runner.GetComponent<RunnerBrain>();
        Set(runnerBrain, "aggressionWeight", 2f);
        runnerBrain.enabled = true;
        bool sawRush = false, sawRecovery = false;
        float until = Time.time + 7;
        while (Time.time < until)
        {
            sawRush |= runner.Movement.Velocity.magnitude > 2;
            sawRecovery |= runner.Decision.Contains("recover");
            player.Heal(100);
            yield return null;
        }
        Check(sawRush && sawRecovery, "Runner moves in a real rush and enters bounded recovery");
        runnerBrain.enabled = false;
        runner.Movement.Stop();

        // Freeze translation to emulate a blocked agent and exercise retry budget.
        Place(hunter, new Vector3(-12, 0, -12), Origin);
        var hunterAgent = hunter.GetComponent<NavMeshAgent>();
        hunter.Movement.MoveTo(Origin + new Vector3(-12, 0, 8), 1.25f);
        hunterAgent.speed = 0; // Emulate a persistent obstruction without moving the navigation simulation separately.
        until = Time.time + 10;
        while (Time.time < until && !hunter.Movement.Failed) yield return null;
        Check(hunter.Movement.Failed && hunter.Movement.RetryCount > 0, "Stalled movement stops after bounded sidestep retries");
        hunterAgent.speed = 1.25f;

        // Wave-sized mixed group: check slot bounds, live decisions and runtime exceptions.
        string[] kinds = { "Hunter", "Runner", "Ambusher", "Elite" };
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI * 2 / 16;
            var a = Spawn(kinds[i % 4], new Vector3(Mathf.Sin(angle) * 11, 0, Mathf.Cos(angle) * 11));
            Place(a, a.transform.position - Origin, player.transform.position);
            a.Perception.enabled = true;
            foreach (var b in a.GetComponents<MonoBehaviour>())
                if (b is HunterBrain || b is RunnerBrain || b is AmbusherBrain || b is EliteBrain) b.enabled = true;
        }
        int maximum = 0;
        until = Time.time + 12;
        while (Time.time < until)
        {
            maximum = Mathf.Max(maximum, world.ActiveAttackers);
            if (world.ActiveAttackers > world.MaximumAttackers) throw new Exception("Group exceeded attack-slot cap.");
            player.Heal(100);
            yield return null;
        }
        Check(maximum > 0 && maximum <= world.MaximumAttackers,
            "20-enemy mixed encounter maintains shared attack limit without runtime exceptions");
        results.Add("Scope: isolated navigation/combat scenarios; rendered animation quality and human difficulty require play review.");
    }
}
```


## AbandonedCityArtPass

Full file path: D:/GV project/Assets/Editor/Art/AbandonedCityArtPass.cs

```csharp
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using G = CityArtGeometry;

/// <summary>A repeatable, additive art pass over the existing collision and navigation layout.</summary>
public static class AbandonedCityArtPass
{
    private static Material brick, concrete, asphalt, iron, wood, paving, trim, glass, glow, black;

    [MenuItem("Zombie Outbreak/Art/Apply abandoned city art pass")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before editing art.");
        EditorSceneManager.SaveOpenScenes();
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != OutbreakEditor.Scene) OutbreakEditor.Open();
        Phase2AnimationAssets.Folder("Assets/Scenes/ArtBackups");
        const string backup = "Assets/Scenes/ArtBackups/ZombieMap_BeforeCityArt.unity";
        if (!System.IO.File.Exists(backup)) EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), backup, true);
        CityArtAssets.Prepare();
        Materials();
        var district = GameObject.Find("OutbreakDistrict").transform;
        RefinishExisting(district);
        if (district.Find("HorrorCity") == null)
        {
            var stale = district.Find("HorrorCity_Draft");
            if (stale != null) Object.DestroyImmediate(stale.gameObject);
            var art = G.Group("HorrorCity_Draft",district);
            Streets(art);
            Buildings(art);
            Skyline(art);
            Props(art,district);
            art.name = "HorrorCity";
        }
        Lighting(district);
        PolishSurfaces(district);
        CityCharacterArt.Apply();
        Phase5Setup.Validate();
        Phase18Setup.Validate();
        OutbreakEditor.Save("ABANDONED_CITY_ART");
        var view = SceneView.lastActiveSceneView;
        if (view != null) view.LookAt(new Vector3(0,2,-6),Quaternion.Euler(12,-12,0),22);
        Debug.Log("CITY_ART_PASS: photographic materials, city architecture, street dressing and lighting applied. Gameplay navigation checks passed.");
    }

    private static void Materials()
    {
        brick=CityArtAssets.Surface("brick_wall_001",.4f);
        concrete=CityArtAssets.Surface("cracked_concrete_wall",.42f);
        asphalt=CityArtAssets.Surface("asphalt_02",.33f,0,.4f);
        iron=CityArtAssets.Surface("rusty_corrugated_iron",.5f,.35f);
        wood=CityArtAssets.Surface("wood_planks_grey",.6f);
        paving=CityArtAssets.Surface("concrete_pavement",.5f);
        trim=CityArtAssets.Solid("OxidizedFrames",new Color(.14f,.17f,.16f),.32f,.45f);
        glass=CityArtAssets.Solid("BlackenedWindowGlass",new Color(.075f,.12f,.13f),.8f,.25f);
        black=CityArtAssets.Solid("RubberAndTar",new Color(.035f,.04f,.042f),.15f);
        glow=CityArtAssets.Solid("EmergencyAmber",new Color(.6f,.37f,.11f),.35f,0,new Color(1.3f,.59f,.16f));
    }

    private static void RefinishExisting(Transform district)
    {
        foreach(var renderer in district.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.sharedMaterial == null) continue;
            string name=renderer.sharedMaterial.name;
            switch(name)
            {
                case "Concrete": case "Rubble": renderer.sharedMaterial=concrete; break;
                case "Asphalt": renderer.sharedMaterial=asphalt; break;
                case "Brick": renderer.sharedMaterial=brick; break;
                case "Wood": renderer.sharedMaterial=wood; break;
                case "Metal": case "DoorMetal": case "AbandonedVehicle": renderer.sharedMaterial=iron; break;
            }
        }
        var originalArt=district.Find("ArtPass");
        if(originalArt != null)
            foreach(Transform child in originalArt)
                if(child.name.StartsWith("AbandonedCar") || child.name=="OfficeWindow" || child.name=="HOLD THE LINE")
                    foreach(var renderer in child.GetComponentsInChildren<Renderer>()) renderer.enabled=false;
        var atmosphere=district.Find("AtmosphereDetails");
        if(atmosphere != null)
            foreach(Transform child in atmosphere)
                if(child.name=="GroundGrime") child.gameObject.SetActive(false);
    }

    private static void Streets(Transform parent)
    {
        var street=G.Group("RoadAndDrainage",parent);
        foreach(float side in new[]{-1f,1f})
        {
            for(int z=-35;z<=35;z+=2)
            {
                if(z>-14 && z<-2)continue;
                G.Box("Kerbstone",street,new Vector3(side*5.12f,.085f,z),new Vector3(.28f,.14f,1.93f),concrete);
                G.Box("PavementSlab",street,new Vector3(side*6.25f,.057f,z),new Vector3(1.95f,.025f,1.96f),paving);
            }
        }
        var faded=CityArtAssets.Solid("FadedRoadPaint",new Color(.46f,.43f,.31f),.12f);
        foreach(float z in new[]{-14.5f,-1.5f})
            for(int i=-4;i<=4;i++) G.Box("WornCrosswalk",street,new Vector3(i,.055f,z),new Vector3(.53f,.015f,2.3f),faded);
        for(int z=-29;z<=28;z+=19)
        {
            G.Primitive(PrimitiveType.Cylinder,"Manhole",street,new Vector3(1.9f,.068f,z),new Vector3(.8f,.014f,.8f),trim);
            for(int i=-3;i<=3;i++) G.Box("ManholeRidge",street,new Vector3(1.9f+i*.085f,.086f,z),new Vector3(.028f,.012f,.57f),black);
            foreach(float side in new[]{-1f,1f})
            {
                G.Box("StormDrain",street,new Vector3(side*4.75f,.06f,z+3),new Vector3(.45f,.022f,.8f),black);
                for(int i=0;i<7;i++) G.Box("DrainBar",street,new Vector3(side*4.75f,.079f,z+2.69f+i*.1f),new Vector3(.43f,.025f,.025f),trim);
            }
        }
        var wet=CityArtAssets.Solid("StandingRainwater",new Color(.047f,.07f,.075f),.94f,.28f);
        var random=new System.Random(104);
        for(int i=0;i<19;i++) G.Puddle(street,new Vector3(-4+(float)random.NextDouble()*8,.059f,-34+(float)random.NextDouble()*68),.65f+(float)random.NextDouble()*1.7f,wet,random);
        // Small papers, stones and cable fragments stay below the controller step height.
        var paper=CityArtAssets.Solid("WaterloggedPaper",new Color(.42f,.4f,.31f),.1f);
        for(int i=0;i<70;i++)
        {
            float side=i%2==0?-1:1;
            var pos=new Vector3(side*(5.5f+(float)random.NextDouble()*2.3f),.09f,-34+(float)random.NextDouble()*68);
            G.Box("StreetLitter",street,pos,new Vector3(.08f+(float)random.NextDouble()*.22f,.018f,.1f+(float)random.NextDouble()*.19f),i%4==0?paper:concrete,new Vector3(0,random.Next(360),random.Next(-8,8)));
        }
    }

    private static void Buildings(Transform parent)
    {
        var block=G.Group("Architecture",parent);
        Apartment(block,"QuarantineClinic",new Vector3(21,0,15),18,18,3,"ST. BRIGID  /  MEDICAL CENTRE");
        Apartment(block,"MunicipalUtilities",new Vector3(-22,0,-24),15,14,1,"CITY UTILITIES  /  NO ENTRY");
        var warehouse=G.Group("RiversideFreight",block,new Vector3(-20,0,14));
        G.Box("RoofLeft",warehouse,new Vector3(-5,5.7f,0),new Vector3(10.5f,.18f,21),iron,new Vector3(0,0,9));
        G.Box("RoofRight",warehouse,new Vector3(5,5.7f,0),new Vector3(10.5f,.18f,21),iron,new Vector3(0,0,-9));
        for(int i=-9;i<=9;i+=3)
        {
            foreach(float side in new[]{-1f,1f})
            {
                G.Box("WallColumn",warehouse,new Vector3(side*10.34f,2.6f,i),new Vector3(.18f,5.2f,.22f),trim);
                G.Box("RoofBeam",warehouse,new Vector3(i,4.85f,0),new Vector3(.15f,.25f,20),trim);
            }
        }
        foreach(float side in new[]{-1f,1f})
        {
            var face=G.Group("LoadingFacade",warehouse,new Vector3(0,0,side*10.35f),side<0?0:180);
            G.Box("PortalHeader",face,new Vector3(0,3.85f,-.05f),new Vector3(4.4f,.3f,.3f),trim);
            foreach(float x in new[]{-2.15f,2.15f})G.Box("PortalUpright",face,new Vector3(x,1.85f,0),new Vector3(.18f,3.7f,.22f),trim);
            for(int x=-8;x<=8;x+=4) if(x!=0) Window(face,new Vector3(x,3.2f,-.04f),false);
            Sign(face,"RIVERSIDE FREIGHT",new Vector3(0,4.72f,-.2f),6.4f,.65f,Color.white);
        }
        for(int z=-7;z<=7;z+=7)
        {
            G.Beam("RoofTruss",warehouse,new Vector3(-9.6f,4.7f,z),new Vector3(0,6.4f,z),.13f,trim);
            G.Beam("RoofTruss",warehouse,new Vector3(9.6f,4.7f,z),new Vector3(0,6.4f,z),.13f,trim);
            G.Beam("TieBeam",warehouse,new Vector3(-9.6f,4.7f,z),new Vector3(9.6f,4.7f,z),.1f,trim);
        }
        // Signs sit against physical walls, clear of the four existing entrances.
        Sign(warehouse,"07",new Vector3(-5.5f,2,-10.4f),1.4f,1.7f,new Color(.67f,.62f,.43f));
        foreach(float side in new[]{-1f,1f})
            G.Primitive(PrimitiveType.Cylinder,"Downpipe",warehouse,new Vector3(side*10.5f,2.4f,9),new Vector3(.14f,2.4f,.14f),trim);
    }

    private static void Apartment(Transform parent,string name,Vector3 position,float width,float depth,int floors,string sign)
    {
        var root=G.Group(name,parent,position);
        float top=4+floors*3.1f;
        G.Box("UpperBuilding",root,new Vector3(0,(4+top)/2,0),new Vector3(width,top-4,depth),floors>1?brick:concrete);
        G.Box("Parapet",root,new Vector3(0,top+.17f,0),new Vector3(width+.6f,.34f,depth+.6f),concrete);
        G.Box("InteriorCeiling",root,new Vector3(0,3.96f,0),new Vector3(width,.12f,depth),concrete);
        for(int side=0;side<4;side++)
        {
            bool front=side%2==0;
            float span=front?width:depth;
            Vector3 offset=side==0?new Vector3(0,0,-depth/2):side==1?new Vector3(-width/2,0,0):side==2?new Vector3(0,0,depth/2):new Vector3(width/2,0,0);
            var face=G.Group("Facade",root,offset,side*90);
            for(int floor=0;floor<floors;floor++)
            {
                float y=5.55f+floor*3.1f;
                G.Box("FloorCornice",face,new Vector3(0,y-1.35f,-.2f),new Vector3(span+.3f,.18f,.42f),concrete);
                int count=Mathf.FloorToInt(span/3);
                for(int j=0;j<count;j++)
                {
                    float x=(j-(count-1)*.5f)*2.8f;
                    Window(face,new Vector3(x,y,-.34f),(j+floor+side)%6==0);
                    if(floor==0 && j%3==1) G.Box("AirConditioner",face,new Vector3(x,y-1.1f,-.57f),new Vector3(.82f,.42f,.52f),iron);
                }
            }
            foreach(float x in new[]{-span*.36f,span*.36f})
            {
                Window(face,new Vector3(x,2.1f,-.34f),false);
                for(int plank=0;plank<3;plank++)G.Box("BoardedWindow",face,new Vector3(x,1.55f+plank*.44f,-.59f),new Vector3(1.62f,.24f,.085f),wood,new Vector3(0,0,plank%2==0?7:-9));
            }
        }
        Sign(root,sign,new Vector3(0,3.38f,-depth/2-.4f),width*.83f,.56f,new Color(.76f,.76f,.63f));
        G.Box("RoofPlant",root,new Vector3(-width*.2f,top+.7f,0),new Vector3(3.1f,1.3f,2.1f),iron);
        for(int i=0;i<8;i++) G.Box("PlantLouvre",root,new Vector3(-width*.2f-1.3f+i*.36f,top+.7f,-1.07f),new Vector3(.09f,1.1f,.06f),trim);
    }

    private static void Window(Transform parent,Vector3 position,bool lit)
    {
        G.Box("WindowRecess",parent,position,new Vector3(1.48f,1.88f,.09f),black);
        G.Box("WindowGlass",parent,position+new Vector3(0,0,-.06f),new Vector3(1.3f,1.68f,.035f),lit?glow:glass);
        foreach(float x in new[]{-.72f,0,.72f})G.Box("WindowFrame",parent,position+new Vector3(x,0,-.12f),new Vector3(.055f,1.85f,.08f),trim);
        foreach(float y in new[]{-.92f,0,.92f})G.Box("WindowFrame",parent,position+new Vector3(0,y,-.12f),new Vector3(1.46f,.06f,.08f),trim);
        G.Box("WindowSill",parent,position+new Vector3(0,-1.01f,-.15f),new Vector3(1.7f,.16f,.38f),concrete);
    }

    private static void Skyline(Transform parent)
    {
        var skyline=G.Group("DistantCity",parent);
        var random=new System.Random(73);
        for(int side=0;side<4;side++)
        {
            var row=G.Group("CityBlock",skyline,Vector3.zero,side*90);
            for(int i=-2;i<=2;i++)
            {
                float height=13+random.Next(16),x=i*19+random.Next(-2,3);
                var building=G.Group("BackgroundTenement",row,new Vector3(x,0,49));
                G.Box("Masonry",building,new Vector3(0,height/2,0),new Vector3(16,height,12),i%2==0?brick:concrete);
                G.Box("RoofLine",building,new Vector3(0,height,0),new Vector3(16.5f,.45f,12.4f),trim);
                for(float y=6;y<height-1;y+=3.1f)
                    for(int j=-2;j<=2;j++)G.Box("DistantWindow",building,new Vector3(j*2.8f,y,-6.03f),new Vector3(1.05f,1.5f,.06f),random.Next(12)==0?glow:glass);
            }
        }
    }

    private static void Props(Transform parent,Transform district)
    {
        var props=G.Group("StreetFurniture",parent);
        G.Car(props,new Vector3(13,0,-6),65,iron,trim,glass,black);
        G.Car(props,new Vector3(-9,0,27),-15,iron,trim,glass,black);
        foreach(var pos in new[]{new Vector3(-6.4f,0,-27),new Vector3(6.5f,0,14),new Vector3(6.3f,0,-19)})
            CityArtAssets.Model("fire_hydrant",props,pos,.88f,35);
        foreach(var pos in new[]{new Vector3(7.3f,0,-31),new Vector3(-7.1f,0,3),new Vector3(10.6f,0,8),new Vector3(-29,0,-17)})
        {
            CityArtAssets.Model("metal_trash_can",props,pos,1.05f,60);
            CityArtAssets.Model("trashbag",props,pos+new Vector3(.7f,0,.25f),.65f,130);
            CityArtAssets.Model("trashbag",props,pos+new Vector3(.3f,0,-.5f),.48f,35);
        }
        var physics=district.Find("PhysicsObjects");
        foreach(Transform item in physics)
        {
            var own=item.GetComponent<Renderer>();
            if(item.name.StartsWith("Barrel"))
            {
                if(own!=null)own.enabled=false;
                CityArtAssets.Model("Barrel_01",item,item.position-Vector3.up*.6f,1.2f,15);
            }
            else
            {
                var detail=G.Group("CrateStraps",item);
                detail.localScale=new Vector3(1/item.lossyScale.x,1/item.lossyScale.y,1/item.lossyScale.z);
                foreach(float z in new[]{-.4f,.4f})G.Box("SteelBand",detail,new Vector3(0,0,z),new Vector3(1.13f,1.24f,.06f),trim);
            }
        }
        foreach(Transform barricade in district.Find("Barricades"))
        {
            // Supplied team models take priority over the generated fallback.
            if(barricade.Find("TeamBarricadeVisual")!=null)continue;
            var own=barricade.GetComponent<Renderer>(); if(own!=null)own.enabled=false;
            var detail=G.Group("TimberBarricadeDetail",barricade);
            detail.localScale=new Vector3(1/barricade.lossyScale.x,1/barricade.lossyScale.y,1/barricade.lossyScale.z);
            foreach(float x in new[]{-.87f,.87f})G.Box("SupportPost",detail,new Vector3(x,0,0),new Vector3(.13f,1.3f,.18f),wood,new Vector3(0,0,x*4));
            for(int i=0;i<4;i++)G.Box("SplinteredPlank",detail,new Vector3(0,-.4f+i*.27f,-.16f),new Vector3(2.4f,.2f,.1f),wood,new Vector3(0,0,i%2==0?2:-3));
            G.Beam("CrossBrace",detail,new Vector3(-1,-.53f,.15f),new Vector3(1,.53f,.15f),.14f,wood);
        }
        for(int side=-1;side<=1;side+=2)
        {
            var pole=G.Group("UtilityPole",props,new Vector3(side*7.3f,0,-24));
            G.Primitive(PrimitiveType.Cylinder,"Pole",pole,new Vector3(0,4.2f,0),new Vector3(.21f,4.2f,.21f),wood);
            G.Box("CrossArm",pole,new Vector3(0,7.3f,0),new Vector3(2,.14f,.16f),trim);
        }
        for(int wire=0;wire<3;wire++)
        {
            Vector3 previous=new Vector3(-7.3f,7.4f,-24+wire*.25f);
            for(int i=1;i<=16;i++)
            {
                float t=i/16f;
                var next=new Vector3(Mathf.Lerp(-7.3f,7.3f,t),7.4f-Mathf.Sin(t*Mathf.PI)*1.15f,-24+wire*.25f);
                G.Beam("SaggingCable",props,previous,next,.023f,black); previous=next;
            }
        }
    }

    private static void Sign(Transform parent,string text,Vector3 pos,float width,float height,Color color)
    {
        G.Box("WeatheredSignBacking",parent,pos,new Vector3(width+.2f,height+.14f,.08f),trim);
        var go=new GameObject("Sign - "+text,typeof(RectTransform),typeof(TextMeshPro));
        go.transform.SetParent(parent,false); go.transform.localPosition=pos+new Vector3(0,0,-.052f);
        var label=go.GetComponent<TextMeshPro>(); label.text=text;
        label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Phase4UIFactory.FontPath);
        label.fontSize=2.2f; label.enableAutoSizing=true; label.fontSizeMin=.7f; label.fontSizeMax=2.2f;
        label.color=color; label.alignment=TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta=new Vector2(width,height);
        label.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
    }

    private static void Lighting(Transform district)
    {
        RenderSettings.fog=true; RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=new Color(.075f,.1f,.115f); RenderSettings.fogDensity=.018f;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.33f,.39f,.43f);
        RenderSettings.ambientEquatorColor=new Color(.17f,.205f,.23f);
        RenderSettings.ambientGroundColor=new Color(.085f,.095f,.1f);
        foreach(var light in Object.FindObjectsByType<Light>())
        {
            if(light.type==LightType.Directional)
            {
                light.transform.rotation=Quaternion.Euler(32,-35,0);
                light.color=new Color(.67f,.79f,.87f); light.intensity=.82f;
                light.shadows=LightShadows.Soft; light.shadowBias=.035f; light.shadowNormalBias=.25f;
            }
            else if(light.name=="StreetPool")
            {
                light.type=LightType.Spot; light.spotAngle=112; light.innerSpotAngle=65;
                light.transform.rotation=Quaternion.Euler(90,0,0); light.range=17;
                light.intensity=2.2f; light.color=new Color(1,.69f,.36f);
            }
        }
        var camera=Camera.main;
        camera.backgroundColor=RenderSettings.fogColor; camera.farClipPlane=180; camera.allowHDR=true;
        QualitySettings.shadowDistance=48; QualitySettings.shadowResolution=ShadowResolution.High;
        QualitySettings.pixelLightCount=8; QualitySettings.anisotropicFiltering=AnisotropicFiltering.Enable;
        QualitySettings.antiAliasing=4;
        var effects=OutbreakEditor.Add<CityCameraAtmosphere>(camera.gameObject);
        effects.Configure(Shader.Find("Hidden/Outbreak/Atmosphere"));
        EditorUtility.SetDirty(effects);
        if(district.Find("CityReflectionProbe")==null)
        {
            var probe=G.Group("CityReflectionProbe",district,new Vector3(0,4,-4)).gameObject.AddComponent<ReflectionProbe>();
            probe.mode=ReflectionProbeMode.Realtime; probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution=128; probe.size=new Vector3(85,35,85); probe.boxProjection=true;
            probe.cullingMask=~(1<<9); probe.clearFlags=ReflectionProbeClearFlags.SolidColor; probe.backgroundColor=RenderSettings.fogColor;
        }
    }

    private static void PolishSurfaces(Transform district)
    {
        // Match the underlying asphalt at the puddle boundary; a black polygon reads as a hole.
        string path=CityArtAssets.Root+"/Materials/StandingRainwater.mat";
        var wet=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(wet!=null)
        {
            wet.shader=asphalt.shader;
            wet.CopyPropertiesFromMaterial(asphalt);
            wet.SetFloat("_Wetness",.8f);
            wet.SetFloat("_SmoothnessFloor",.86f);
            wet.SetFloat("_NormalStrength",.12f);
            EditorUtility.SetDirty(wet);
        }
        var art=district.Find("HorrorCity");
        if(art!=null)
        {
            foreach(var renderer in art.GetComponentsInChildren<MeshRenderer>())
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic);
            foreach(var filter in art.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;
                if(filter.name!="DentedBody" || mesh.vertexCount!=48 || mesh.triangles.Length!=240)continue;
                var triangles=mesh.triangles.ToList();
                for(int j=1;j<7;j++)triangles.AddRange(new[]{0,j+1,j,40,40+j,41+j});
                mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateTangents();
                EditorUtility.SetDirty(mesh);
            }
        }
        // Refresh only generated scanned prop instances, never their internal source transforms.
        var props=art!=null?art.Find("StreetFurniture"):null;
        if(props!=null)
        {
            foreach(var item in props.Cast<Transform>().ToArray())
                if(item.name=="fire_hydrant" || item.name=="trashbag" || item.name=="metal_trash_can")
                    Object.DestroyImmediate(item.gameObject);
            foreach(var pos in new[]{new Vector3(-6.4f,0,-27),new Vector3(6.5f,0,14),new Vector3(6.3f,0,-19)})
                CityArtAssets.Model("fire_hydrant",props,pos,.88f,35);
            foreach(var pos in new[]{new Vector3(7.3f,0,-31),new Vector3(-7.1f,0,3),new Vector3(10.6f,0,8),new Vector3(-29,0,-17)})
            {
                CityArtAssets.Model("metal_trash_can",props,pos,1.05f,60);
                CityArtAssets.Model("trashbag",props,pos+new Vector3(.7f,0,.25f),.65f,130);
                CityArtAssets.Model("trashbag",props,pos+new Vector3(.3f,0,-.5f),.48f,35);
            }
        }
        foreach(Transform item in district.Find("PhysicsObjects"))
        {
            if(!item.name.StartsWith("Barrel"))continue;
            var old=item.Find("Barrel_01");
            if(old!=null)Object.DestroyImmediate(old.gameObject);
            CityArtAssets.Model("Barrel_01",item,item.position-Vector3.up*.6f,1.2f,15);
        }
    }
}
```


## CharacterEquipmentArtPass

Full file path: D:/GV project/Assets/Editor/Art/CharacterEquipmentArtPass.cs

```csharp
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
    [MenuItem("Zombie Outbreak/Art/Apply character textures weapons and motion")]
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
```


## CharacterMotionAuthoring

Full file path: D:/GV project/Assets/Editor/Art/CharacterMotionAuthoring.cs

```csharp
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
```


## CharacterSurfaceLibrary

Full file path: D:/GV project/Assets/Editor/Art/CharacterSurfaceLibrary.cs

```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Deterministic, editable surface maps authored for the generated character/weapon art.</summary>
public static class CharacterSurfaceLibrary
{
    public const string Root = "Assets/Art/CharactersAndWeapons";
    private const int Size = 512;

    public static void Prepare()
    {
        Phase2AnimationAssets.Folder(Root + "/Textures");
        Phase2AnimationAssets.Folder(Root + "/Materials");
        Phase2AnimationAssets.Folder(Root + "/Meshes");
        foreach (string surface in new[] { "Canvas", "Denim", "Leather", "Skin", "Steel", "Polymer", "Wood" })
            CreateMaps(surface);
        File.WriteAllText(Root + "/Textures/SurfaceRevision2.txt", "Deterministic native surface maps, revision 2. See CharacterSurfaceLibrary.cs for source.\n");
    }

    public static Material Material(string name, string surface, Color tint, float metallic = 0, float smoothness = .2f)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
        material.color = tint;
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + surface + "_Albedo.tga");
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + surface + "_Normal.tga"));
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + surface + "_MetalSmooth.tga"));
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICGLOSSMAP");
        // Red stores full metallic mask; the scale is baked separately per material using a tint-independent shader value.
        material.SetFloat("_GlossMapScale", smoothness);
        material.SetFloat("_BumpScale", surface == "Skin" ? .18f : surface == "Steel" ? .22f : .48f);
        material.SetFloat("_Metallic", metallic);
        // Standard ignores _Metallic with a texture, so nonmetals use a black-red smoothness map.
        if (metallic < .5f)
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + surface + "_NonmetalSmooth.tga"));
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateMaps(string kind)
    {
        string prefix = Root + "/Textures/" + kind;
        if (File.Exists(prefix + "_NonmetalSmooth.tga") && File.Exists(Root + "/Textures/SurfaceRevision2.txt")) return;
        var albedo = new Color32[Size * Size]; var heights = new float[Size * Size];
        var normal = new Color32[Size * Size]; var metal = new Color32[Size * Size]; var nonmetal = new Color32[Size * Size];
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
        {
            float u = x / (float)Size, v = y / (float)Size;
            float noise = Hash(x, y), cloud = Periodic(u, v, 5), grain = Periodic(u, v, 31);
            float shade = .76f + .14f * cloud + .06f * noise, height = grain * .1f;
            if (kind == "Canvas" || kind == "Denim")
            {
                float weave = .5f + .5f * Mathf.Sin(x * Mathf.PI / 2) * Mathf.Cos(y * Mathf.PI / 2);
                float twill = kind == "Denim" ? .5f + .5f * Mathf.Sin((x + y) * Mathf.PI / 4) : weave;
                float wrinkle = Mathf.Pow(.5f + .5f * Mathf.Sin(v * Mathf.PI * 12 + Mathf.Sin(u * Mathf.PI * 6)), 14);
                shade = .69f + .08f * twill + .11f * cloud - .035f * wrinkle + .025f * noise;
                height = weave * .14f + twill * .07f + wrinkle * .025f;
            }
            else if (kind == "Leather")
            {
                float creases = Mathf.Pow(grain, 5);
                shade = .55f + .27f * cloud + .12f * noise - .22f * creases;
                height = grain * .35f + noise * .08f;
            }
            else if (kind == "Skin")
            {
                shade = .78f + .15f * cloud + .025f * noise;
                height = noise * .07f + grain * .06f;
            }
            else if (kind == "Steel")
            {
                float scratch = Hash(x / 2, y / 41) > .989f ? .28f : 0;
                shade = .58f + .16f * cloud + .06f * noise + scratch;
                height = noise * .035f - scratch * .12f;
            }
            else if (kind == "Polymer")
            {
                shade = .68f + .12f * cloud + .10f * noise;
                height = noise * .2f;
            }
            else if (kind == "Wood")
            {
                float rings = .5f + .5f * Mathf.Sin(u * Mathf.PI * 48 + 3 * Mathf.Sin(v * Mathf.PI * 2) + grain);
                shade = .5f + .22f * rings + .16f * cloud;
                height = rings * .14f;
            }
            int i = y * Size + x; heights[i] = height;
            float stain = Mathf.SmoothStep(.34f, .65f, cloud);
            albedo[i] = new Color(shade, shade * (kind == "Skin" ? .96f : .99f), shade * (kind == "Skin" ? .92f : .96f), 1);
            byte gloss = (byte)(Mathf.Lerp(.48f, 1, stain) * 255);
            metal[i] = new Color32(210, 0, 0, gloss); nonmetal[i] = new Color32(0, 0, 0, gloss);
        }
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
        {
            float dx = heights[y * Size + (x + 1) % Size] - heights[y * Size + (x + Size - 1) % Size];
            float dy = heights[((y + 1) % Size) * Size + x] - heights[((y + Size - 1) % Size) * Size + x];
            Vector3 n = new Vector3(-dx * 2, -dy * 2, 1).normalized;
            normal[y * Size + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f);
        }
        Write(prefix + "_Albedo.tga", albedo, false, false);
        Write(prefix + "_Normal.tga", normal, true, true);
        Write(prefix + "_MetalSmooth.tga", metal, false, true);
        Write(prefix + "_NonmetalSmooth.tga", nonmetal, false, true);
    }

    private static float Hash(int x, int y)
    { unchecked { uint n = (uint)(x * 374761393 + y * 668265263 + 104729); n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 65535) / 65535f; } }
    private static float Periodic(float u, float v, float frequency)
    {
        int cells=Mathf.RoundToInt(frequency);float x=u*cells,y=v*cells;
        int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);
        float sx=Mathf.SmoothStep(0,1,x-ix),sy=Mathf.SmoothStep(0,1,y-iy);
        return Mathf.Lerp(Mathf.Lerp(Hash(ix%cells,iy%cells),Hash((ix+1)%cells,iy%cells),sx),
            Mathf.Lerp(Hash(ix%cells,(iy+1)%cells),Hash((ix+1)%cells,(iy+1)%cells),sx),sy);
    }
    private static void Write(string path, Color32[] pixels, bool normal, bool linear)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(new byte[] { 0, 0, 2 }); w.Write(new byte[9]); w.Write((short)Size); w.Write((short)Size); w.Write((byte)32); w.Write((byte)8);
            foreach (var p in pixels) { w.Write(p.b); w.Write(p.g); w.Write(p.r); w.Write(p.a); }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !linear; importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true; importer.anisoLevel = 4; importer.maxTextureSize = Size;
        importer.textureCompression = TextureImporterCompression.Compressed; importer.SaveAndReimport();
    }
}
```


## CityArtAssets

Full file path: D:/GV project/Assets/Editor/Art/CityArtAssets.cs

```csharp
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Imports the documented CC0 surface maps and keeps material assets reusable.</summary>
public static class CityArtAssets
{
    public const string Root = "Assets/Art/AbandonedCity";
    public static void Prepare()
    {
        Phase2AnimationAssets.Folder(Root + "/Materials");
        Phase2AnimationAssets.Folder(Root + "/Meshes");
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/PolyHaven" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            bool normal = path.Contains("nor_gl"), rough = path.Contains("Rough");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !rough;
            importer.maxTextureSize = normal || rough ? 1024 : 2048;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
    }

    public static Material Surface(string id, float tile = .5f, float metallic = 0, float wetness = .1f, bool projected = true)
    {
        string path = Root + "/Materials/" + id + (projected ? "_World" : "_UV") + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Outbreak/Weathered Surface");
        if (shader == null) throw new InvalidOperationException("Weathered Surface shader has not imported.");
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        material.SetTexture("_MainTex", Texture(id, "Diffuse"));
        material.SetTexture("_BumpMap", Texture(id, "nor_gl"));
        material.SetTexture("_Roughness", Texture(id, "Rough"));
        material.SetFloat("_Scale", tile);
        material.SetFloat("_Triplanar", projected ? 1 : 0);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Wetness", wetness);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D Texture(string id, string channel)
    {
        foreach (string extension in new[] { ".jpg", ".png" })
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/PolyHaven/" + id + "/" + channel + extension);
            if (texture != null) return texture;
        }
        throw new InvalidOperationException("Missing downloaded surface map: " + id + "/" + channel);
    }

    public static Material Solid(string name, Color color, float smoothness = .25f, float metal = 0, Color emission = default)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
        material.color = color;
        material.SetFloat("_Glossiness", smoothness);
        material.SetFloat("_Metallic", metal);
        if (emission.maxColorComponent > 0) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", emission); }
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    public static GameObject Model(string id, Transform parent, Vector3 position, float height, float yaw = 0)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/PolyHaven/" + id + "/" + id + ".fbx");
        if (asset == null) throw new InvalidOperationException("Missing model: " + id);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = id;
        // These source FBXs contain alternative versions side by side. Use one complete version.
        foreach(var child in go.GetComponentsInChildren<Renderer>())
            if(child.name.Contains("_aged") || child.name.Contains("_rust"))child.gameObject.SetActive(false);
        go.transform.position = position;
        // FBX roots often carry the Blender Z-up to Unity Y-up conversion.
        go.transform.rotation = Quaternion.Euler(0, yaw, 0) * asset.transform.localRotation;
        var renderers = go.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        go.transform.localScale *= height / Mathf.Max(.001f, bounds.size.y);
        bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        go.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
        var material = Surface(id, 1, id == "trashbag" ? 0 : .45f, .05f, false);
        foreach (var r in renderers) r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
        return go;
    }

}
```


## CityArtGeometry

Full file path: D:/GV project/Assets/Editor/Art/CityArtGeometry.cs

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Small native mesh helpers for decorative architecture. No gameplay components.</summary>
public static class CityArtGeometry
{
    public static Transform Group(string name, Transform parent, Vector3 position = default, float yaw = 0)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(0,yaw,0);
        return go.transform;
    }
    public static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, Vector3 rotation = default)
        => Primitive(PrimitiveType.Cube,name,parent,position,size,material,rotation);
    public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 size, Material material, Vector3 rotation = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(rotation);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }
    public static void Beam(string name, Transform parent, Vector3 from, Vector3 to, float width, Material material)
    {
        var go = Box(name,parent,(from+to)*.5f,new Vector3(width,width,Vector3.Distance(from,to)),material);
        go.transform.localRotation = Quaternion.LookRotation((to-from).normalized);
    }
    public static GameObject MeshObject(string name, Transform parent, List<Vector3> vertices, List<int> triangles, Material material)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var uv = new Vector2[vertices.Count];
        for(int i=0;i<uv.Length;i++) uv[i] = new Vector2(vertices[i].x,vertices[i].z);
        mesh.uv = uv;
        mesh.RecalculateTangents();
        string path = AssetDatabase.GenerateUniqueAssetPath(CityArtAssets.Root + "/Meshes/" + name + ".asset");
        AssetDatabase.CreateAsset(mesh,path);
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }
    public static void Puddle(Transform parent, Vector3 position, float radius, Material material, System.Random random)
    {
        var vertices = new List<Vector3> { Vector3.zero };
        var triangles = new List<int>();
        const int sides=18;
        for(int i=0;i<sides;i++)
        {
            float a=i*Mathf.PI*2/sides, r=radius*(.65f+(float)random.NextDouble()*.35f);
            vertices.Add(new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r*.55f));
        }
        for(int i=0;i<sides;i++) { triangles.Add(0); triangles.Add((i+1)%sides+1); triangles.Add(i+1); }
        MeshObject("RainPuddle",parent,vertices,triangles,material).transform.localPosition = position;
    }
    public static void Car(Transform parent, Vector3 position, float yaw, Material paint, Material metal, Material glass, Material rubber)
    {
        var car = Group("WreckedSedan",parent,position,yaw);
        var v=new List<Vector3>(); var t=new List<int>();
        float[] z={-2.15f,-1.8f,-1.1f,.9f,1.85f,2.12f};
        float[] widths={.74f,.95f,.96f,.96f,.88f,.73f};
        float[] tops={.64f,.88f,.97f,.94f,.83f,.67f};
        for(int k=0;k<z.Length;k++)
        {
            float w=widths[k],h=tops[k];
            v.AddRange(new[]{ new Vector3(-w+.1f,.33f,z[k]),new Vector3(w-.1f,.33f,z[k]),new Vector3(w,.48f,z[k]),new Vector3(w,h-.1f,z[k]),new Vector3(w-.13f,h,z[k]),new Vector3(-w+.13f,h,z[k]),new Vector3(-w,h-.1f,z[k]),new Vector3(-w,.48f,z[k]) });
        }
        for(int k=0;k<z.Length-1;k++) for(int j=0;j<8;j++)
        { int a=k*8+j,b=k*8+(j+1)%8,c=b+8,d=a+8; t.AddRange(new[]{a,b,c,a,c,d}); }
        for(int j=1;j<7;j++)
        {
            t.AddRange(new[]{0,j+1,j});
            int rear=(z.Length-1)*8;
            t.AddRange(new[]{rear,rear+j,rear+j+1});
        }
        MeshObject("DentedBody",car,v,t,paint);
        v=new List<Vector3> {new Vector3(-.84f,.89f,-1.2f),new Vector3(.84f,.89f,-1.2f),new Vector3(-.65f,1.52f,-.65f),new Vector3(.65f,1.52f,-.65f),new Vector3(-.65f,1.48f,.48f),new Vector3(.65f,1.48f,.48f),new Vector3(-.83f,.9f,1.13f),new Vector3(.83f,.9f,1.13f)};
        t=new List<int>{0,2,3,0,3,1,2,4,5,2,5,3,4,6,7,4,7,5,0,6,4,0,4,2,1,3,5,1,5,7};
        MeshObject("SmashedDarkGlass",car,v,t,glass);
        Box("Roof",car,new Vector3(0,1.51f,-.08f),new Vector3(1.36f,.08f,1.2f),paint);
        foreach(float side in new[]{-1f,1f})
        {
            Beam("Pillar",car,new Vector3(side*.86f,.9f,-1.17f),new Vector3(side*.66f,1.5f,-.65f),.07f,paint);
            Beam("Pillar",car,new Vector3(side*.84f,.9f,1.1f),new Vector3(side*.65f,1.47f,.47f),.075f,paint);
            Beam("DoorPillar",car,new Vector3(side*.88f,.9f,0),new Vector3(side*.67f,1.5f,0),.06f,paint);
            Box("Mirror",car,new Vector3(side*1.04f,.98f,.65f),new Vector3(.22f,.13f,.18f),metal);
            for(int i=0;i<2;i++)
            {
                float wheelZ=i==0?-1.38f:1.35f;
                Primitive(PrimitiveType.Cylinder,"Tyre",car,new Vector3(side*.93f,.39f,wheelZ),new Vector3(.76f,.14f,.76f),rubber,new Vector3(0,0,90));
                Primitive(PrimitiveType.Cylinder,"WheelRim",car,new Vector3(side*1.085f,.39f,wheelZ),new Vector3(.43f,.012f,.43f),metal,new Vector3(0,0,90));
                Box("DoorHandle",car,new Vector3(side*.966f,.79f,i==0?-.8f:.28f),new Vector3(.035f,.04f,.15f),metal);
            }
        }
        foreach(float end in new[]{-1f,1f})
        {
            Box("Bumper",car,new Vector3(0,.45f,end*2.11f),new Vector3(1.65f,.12f,.16f),metal);
            foreach(float side in new[]{-1f,1f}) Box("BrokenLamp",car,new Vector3(side*.54f,.66f,end*2.1f),new Vector3(.36f,.16f,.055f),glass);
        }
    }
}
```


## CityCharacterArt

Full file path: D:/GV project/Assets/Editor/Art/CityCharacterArt.cs

```csharp
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
```


## EnemyIdentityPreview

Full file path: D:/GV project/Assets/Editor/Art/EnemyIdentityPreview.cs

```csharp
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
```


## EnemyIdentitySetup

Full file path: D:/GV project/Assets/Editor/Art/EnemyIdentitySetup.cs

```csharp
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
```


## ExistingCityEnhancer

Full file path: D:/GV project/Assets/Editor/Art/ExistingCityEnhancer.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;
using G = CityArtGeometry;

/// <summary>Additive reference-image environment pass. The original scene and shared art stay untouched.</summary>
public static class ExistingCityEnhancer
{
    public const string ScenePath = "Assets/Scenes/EnhancedCity/ZombieMap.unity";
    public const string Root = "Assets/Art/ReferenceCity";
    private static Material brick, concrete, iron, wood, dark, glass, paint, wear, pale;
    private static readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();

    [MenuItem("Tools/Dead Zone/Enhance Existing City")]
    public static void Enhance()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before editing the environment.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != OutbreakEditor.Scene && scene.path != ScenePath)
            throw new InvalidOperationException("Open the existing ZombieMap scene first.");
        EditorSceneManager.SaveOpenScenes();
        Phase2AnimationAssets.Folder("Assets/Scenes/EnhancedCity");
        Phase2AnimationAssets.Folder(Root + "/Materials");
        Phase2AnimationAssets.Folder(Root + "/Meshes");
        Phase2AnimationAssets.Folder(Root + "/Prefabs");
        if (scene.path != ScenePath)
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("An enhanced copy already exists. Open that copy to adjust it.");
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        var district = GameObject.Find("OutbreakDistrict").transform;
        if (district.Find("ReferenceCityEnhancement") != null)
        {
            Debug.Log("Enhancement already exists; retained all authored objects. Adjust the existing groups directly.");
            return;
        }
        copies.Clear();
        foreach (var renderer in district.GetComponentsInChildren<MeshRenderer>(true))
            renderer.sharedMaterials = renderer.sharedMaterials.Select(Clone).ToArray();
        brick = Surface("brick_wall_001",new Color(.76f,.72f,.65f),.47f);
        concrete = Surface("cracked_concrete_wall",new Color(.82f,.8f,.74f),.35f);
        iron = Surface("rusty_corrugated_iron",new Color(.55f,.51f,.44f),.5f);
        wood = Surface("wood_planks_grey",new Color(.65f,.6f,.49f),.7f);
        dark = Solid("Aged iron",new Color(.10f,.115f,.105f),.18f,.3f);
        glass = Solid("Dusty glass",new Color(.11f,.13f,.125f),.38f,.12f);
        paint = Solid("Dusty vehicle paint",new Color(.21f,.235f,.22f),.25f,.3f);
        pale = Solid("Chipped limestone",new Color(.48f,.475f,.425f),.12f);
        wear = WearMaterial();
        foreach (var r in district.GetComponentsInChildren<MeshRenderer>(true))
            if (r.sharedMaterial != null && r.sharedMaterial.name.Contains("asphalt_02"))
            { r.sharedMaterial.SetColor("_Color",new Color(.68f,.69f,.64f));r.sharedMaterial.SetFloat("_Wetness",.05f); }
        var additions = G.Group("ReferenceCityEnhancement",district);
        Architecture(additions);
        Streets(additions,district);
        Polish(additions,district);
        Atmosphere(district);
        Optimize(district, additions);
        var surface=district.GetComponent<NavMeshSurface>();
        surface.BuildNavMesh();
        string navPath=Root+"/EnhancedCityNavMesh.asset";
        if (!AssetDatabase.Contains(surface.navMeshData)) AssetDatabase.CreateAsset(surface.navMeshData,AssetDatabase.GenerateUniqueAssetPath(navPath));
        ValidateNavigation();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("REFERENCE_CITY: enhanced existing scene copy saved; original scene and gameplay scripts retained.");
    }

    private static Material Clone(Material source)
    {
        if (source == null || !(source.shader.name == "Standard" || source.shader.name.Contains("Weathered Surface"))) return source;
        if (copies.TryGetValue(source,out var found)) return found;
        string path=Root+"/Materials/"+source.name+".mat";
        var copy=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(copy==null){copy=new Material(source);AssetDatabase.CreateAsset(copy,path);}
        copy.enableInstancing=true;copies[source]=copy;return copy;
    }
    private static Material Surface(string name,Color tint,float scale)
    {
        var m=Clone(AssetDatabase.LoadAssetAtPath<Material>(CityArtAssets.Root+"/Materials/"+name+"_World.mat"));
        m.SetColor("_Color",tint);m.SetFloat("_Scale",scale);m.SetFloat("_Wetness",.03f);EditorUtility.SetDirty(m);return m;
    }
    private static Material Solid(string name,Color color,float smooth,float metal=0)
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.SetFloat("_Glossiness",smooth);m.SetFloat("_Metallic",metal);m.enableInstancing=true;return m;
    }
    private static Material WearMaterial()
    {
        var texture=new Texture2D(256,256,TextureFormat.RGBA32,true){name="Irregular dust and seepage",wrapMode=TextureWrapMode.Clamp};
        var colors=new Color[256*256];
        for(int y=0;y<256;y++)for(int x=0;x<256;x++)
        {
            float u=x/255f,v=y/255f;
            float edge=Mathf.Clamp01(1f-(new Vector2(u-.5f,v-.5f).magnitude*2));
            float noise=Mathf.PerlinNoise(u*11,v*8)*Mathf.PerlinNoise(u*31+12,v*29);
            colors[y*256+x]=new Color(1,1,1,edge*noise*1.8f);
        }
        texture.SetPixels(colors);texture.Apply(true);AssetDatabase.CreateAsset(texture,Root+"/DustSeepage.asset");
        var m=new Material(Shader.Find("Outbreak/City Wear Decal")){name="Seepage and dust"};
        m.SetTexture("_MainTex",texture);m.SetColor("_Color",new Color(.17f,.15f,.11f,.64f));
        AssetDatabase.CreateAsset(m,Root+"/Materials/Seepage.mat");return m;
    }

    private static void Architecture(Transform root)
    {
        var architecture=G.Group("Facade additions",root);
        // Upper levels sit above the existing warehouse; its four entrances remain intact.
        var warehouse=G.Group("Warehouse upper tenement",architecture,new Vector3(-20,0,14));
        G.Box("Weathered upper masonry",warehouse,new Vector3(0,12.2f,0),new Vector3(20,11,20),concrete);
        Roof(warehouse,20,20,17.8f);
        Face(warehouse,new Vector3(10.06f,0,0),-90,20,4,6.7f);
        Face(warehouse,new Vector3(0,0,-10.06f),0,20,4,6.7f);
        Face(warehouse,new Vector3(-10.06f,0,0),90,20,4,6.7f);
        // Supplement the existing clinic rather than replacing its shell or interior.
        var clinic=G.Group("Clinic facade detail",architecture,new Vector3(21,0,15));
        Face(clinic,new Vector3(-9.42f,0,0),90,18,3,4.2f,true);
        Face(clinic,new Vector3(0,0,-9.42f),0,18,3,4.2f,true);
        // Foreground upper structures form the street silhouette; ground level stays open.
        foreach(float side in new[]{-1f,1f})
        {
            var upper=G.Group(side<0?"West street apartments":"East street apartments",architecture,new Vector3(side*17.5f,0,-25));
            float height=side<0?20.2f:16.9f;
            G.Box("Upper apartments",upper,new Vector3(0,(5+height)*.5f,0),new Vector3(12,height-5,18),side<0?brick:concrete);
            Roof(upper,12,18,height);
            Face(upper,new Vector3(-side*6.04f,0,0),side<0?-90:90,18,side<0?5:4,5);
            Face(upper,new Vector3(0,0,9.04f),180,12,side<0?5:4,5);
            // Ground-floor arcade columns leave existing alleys and tactical points reachable.
            foreach(float z in new[]{-8f,-3f,3f,8f})
            {
                var column=G.Box("Ground arcade pier",upper,new Vector3(-side*5.6f,2.5f,z),new Vector3(.55f,5,.65f),concrete);
                column.layer=8;column.AddComponent<BoxCollider>();
            }
            var shopfront=G.Group("Ground floor arcade",upper,new Vector3(-side*6.04f,0,0),side<0?-90:90);
            Storefront(shopfront,-5,2);Storefront(shopfront,5,2);
        }
    }

    // Facade faces local -Z. A single deterministic module contains a whole frontage.
    private static void Face(Transform parent,Vector3 position,float yaw,float width,int floors,float bottom,bool detailOnly=false)
    {
        var face=G.Group("Weathered facade",parent,position,yaw);
        int count=Mathf.FloorToInt(width/2.7f);
        for(int floor=0;floor<floors;floor++)
        {
            float y=bottom+1.4f+floor*2.95f;
            G.Box("Floor ledge",face,new Vector3(0,y-1.4f,-.15f),new Vector3(width+.2f,.13f,.4f),pale);
            for(int j=0;j<count;j++)
            {
                float x=(j-(count-1)*.5f)*2.7f;
                Window(face,new Vector3(x,y,-.10f),(j+floor)%7==2);
                if((j+floor)%5==0) Decal(face,new Vector3(x,y-1,-.43f),new Vector2(1.5f,2.8f));
            }
        }
        float top=bottom+floors*2.95f;
        G.Box("Ornamental roof cornice",face,new Vector3(0,top+.2f,-.24f),new Vector3(width+.4f,.35f,.55f),pale);
        foreach(float x in new[]{-width*.46f,width*.46f})
        {
            G.Box("Masonry pilaster",face,new Vector3(x,(bottom+top)*.5f,-.18f),new Vector3(.25f,top-bottom,.3f),concrete);
            G.Primitive(PrimitiveType.Cylinder,"Rusted downpipe",face,new Vector3(x+.18f,(bottom+top)*.5f,-.45f),new Vector3(.075f,(top-bottom)*.5f,.075f),dark);
        }
        Escape(face,width*.28f,bottom,floors);
        if (!detailOnly && bottom>5) Storefront(face,-width*.3f,bottom-3.4f);
    }
    private static void Window(Transform parent,Vector3 p,bool boarded)
    {
        G.Box("Deep dark window",parent,p,new Vector3(1.18f,1.73f,.10f),dark);
        G.Box("Dusty window panes",parent,p+new Vector3(0,0,-.06f),new Vector3(1.02f,1.56f,.025f),glass);
        foreach(float x in new[]{-.60f,0,.60f})G.Box("Window mullion",parent,p+new Vector3(x,0,-.12f),new Vector3(.055f,1.78f,.07f),pale);
        foreach(float y in new[]{-.88f,.05f,.88f})G.Box("Window crossbar",parent,p+new Vector3(0,y,-.12f),new Vector3(1.25f,.055f,.08f),pale);
        G.Box("Weathered sill",parent,p+new Vector3(0,-.98f,-.15f),new Vector3(1.48f,.14f,.35f),concrete);
        if(boarded)for(int b=0;b<3;b++)G.Box("Uneven window board",parent,p+new Vector3(0,-.55f+b*.48f,-.18f),new Vector3(1.3f,.19f,.07f),wood,new Vector3(0,0,b%2==0?9:-12));
    }
    private static void Escape(Transform parent,float x,float start,int floors)
    {
        for(int level=0;level<floors;level++)
        {
            float y=start+level*2.95f;
            var landing=G.Group("Fire escape landing",parent,new Vector3(x,y,-.95f));
            G.Box("Steel platform",landing,Vector3.zero,new Vector3(2.6f,.1f,1.4f),dark);
            for(int bar=-5;bar<=5;bar++)G.Box("Balustrade",landing,new Vector3(bar*.24f,.55f,-.68f),new Vector3(.03f,1.1f,.035f),dark);
            G.Box("Top rail",landing,new Vector3(0,1.1f,-.68f),new Vector3(2.6f,.045f,.05f),dark);
            foreach(float side in new[]{-1f,1f})G.Beam("Wall bracket",landing,new Vector3(side*1.2f,-.6f,.65f),new Vector3(side*1.2f,0,-.6f),.06f,dark);
            if(level==0)continue;
            float sign=level%2==0?-1:1;
            for(int step=0;step<14;step++)G.Box("Stair tread",landing,new Vector3(sign*(-1.15f+step*.17f),-2.95f+step*.211f,-.12f),new Vector3(.22f,.05f,.8f),dark);
            foreach(float z in new[]{-.55f,.30f})G.Beam("Stair handrail",landing,new Vector3(-sign*1.2f,-2.05f,z),new Vector3(sign*1.2f,.9f,z),.045f,dark);
        }
    }
    private static void Roof(Transform parent,float w,float d,float y)
    {
        G.Box("Flat roof cap",parent,new Vector3(0,y,0),new Vector3(w+.3f,.28f,d+.3f),pale);
        foreach(float x in new[]{-w*.5f,w*.5f})G.Box("Side parapet",parent,new Vector3(x,y+.5f,0),new Vector3(.3f,.9f,d),concrete);
        G.Box("Roof access",parent,new Vector3(-2,y+1.2f,1),new Vector3(3,2.4f,3),brick);
        G.Box("Roof air handler",parent,new Vector3(3,y+.7f,-2),new Vector3(2.3f,1.4f,2),iron);
        for(int i=0;i<3;i++)G.Primitive(PrimitiveType.Cylinder,"Roof vent",parent,new Vector3(-3+i*2,y+.6f,-4),new Vector3(.3f,.6f,.3f),dark);
        G.Beam("Antenna",parent,new Vector3(4,y,3),new Vector3(4,y+4,3),.045f,dark);
        G.Beam("Antenna array",parent,new Vector3(3,y+3,3),new Vector3(5,y+3,3),.035f,dark);
    }
    private static void Storefront(Transform parent,float x,float y)
    {
        G.Box("Rusted storefront shutter",parent,new Vector3(x,y,-.08f),new Vector3(3.4f,2.7f,.08f),iron);
        for(int i=0;i<22;i++)G.Box("Shutter rib",parent,new Vector3(x,y-1.2f+i*.11f,-.14f),new Vector3(3.4f,.028f,.045f),dark);
        G.Box("Old shop sign",parent,new Vector3(x,y+1.7f,-.22f),new Vector3(4,.62f,.15f),wood);
        G.Box("Sloped shop awning",parent,new Vector3(x,y+1.28f,-.65f),new Vector3(3.7f,.12f,1.3f),iron,new Vector3(12,0,0));
    }
    private static void Decal(Transform parent,Vector3 position,Vector2 size)
    {
        var quad=G.Primitive(PrimitiveType.Quad,"Water seepage",parent,position,new Vector3(size.x,size.y,1),wear);
        quad.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
    }

    private static void Streets(Transform root,Transform district)
    {
        var dressing=G.Group("Street decay",root);
        var random=new System.Random(3419);
        for(int i=0;i<115;i++)
        {
            float side=i%2==0?-1:1;float z=-35+(float)random.NextDouble()*69;
            float x=side*(4.35f+(float)random.NextDouble()*2.7f);
            G.Box("Small broken masonry",dressing,new Vector3(x,.09f,z),new Vector3(.05f+(float)random.NextDouble()*.28f,.035f+(float)random.NextDouble()*.07f,.08f+(float)random.NextDouble()*.3f),i%3==0?brick:concrete,new Vector3(random.Next(-8,9),random.Next(360),random.Next(-8,9)));
        }
        for(int i=0;i<20;i++)
        {
            float z=-34+(float)random.NextDouble()*67,x=-4+(float)random.NextDouble()*8;
            var q=G.Primitive(PrimitiveType.Quad,"Dust accumulation",dressing,new Vector3(x,.053f,z),new Vector3(2+(float)random.NextDouble()*2,1+(float)random.NextDouble()*2,1),wear,new Vector3(90,0,random.Next(360)));
            q.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            for(int k=0;k<4;k++) G.Beam("Asphalt fracture",dressing,new Vector3(x+k*.23f,.054f,z+Mathf.Sin(k)*.2f),new Vector3(x+(k+1)*.23f,.054f,z+Mathf.Sin(k+1)*.2f),.016f,dark);
        }
        var template=district.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="WreckedSedan");
        if(template!=null)
        {
            foreach(var location in new[]{new Vector4(-3.7f,0,-27,22),new Vector4(3.8f,0,9,-12),new Vector4(-3.6f,0,25,9)})
            {
                var car=Object.Instantiate(template.gameObject,dressing);car.name="Abandoned vehicle";
                car.transform.localPosition=new Vector3(location.x,0,location.z);car.transform.localRotation=Quaternion.Euler(0,location.w,0);
                foreach(var r in car.GetComponentsInChildren<MeshRenderer>())if(r.name=="DentedBody"||r.name=="Roof")r.sharedMaterial=paint;
                VehicleDetails(car.transform);
                car.layer=8;var collider=car.AddComponent<BoxCollider>();collider.center=new Vector3(0,.7f,0);collider.size=new Vector3(1.9f,1.4f,4.3f);
            }
        }
        foreach(var p in new[]{new Vector3(-7.1f,0,-13),new Vector3(8,0,26)})Dumpster(dressing,p);
        foreach(var p in new[]{new Vector3(6.8f,0,-29),new Vector3(-7.5f,0,13),new Vector3(8.5f,0,28)})Tree(dressing,p);
        var fence=G.Group("Damaged railing",dressing,new Vector3(-7,0,28));
        for(int i=0;i<12;i++)G.Box("Fence picket",fence,new Vector3(i*.35f,.75f,0),new Vector3(.035f,1.5f,.035f),dark,new Vector3(0,0,i==3?13:0));
        G.Box("Fence rail",fence,new Vector3(1.9f,1.1f,0),new Vector3(4.1f,.05f,.05f),dark);
    }
    private static void VehicleDetails(Transform car)
    {
        foreach(float side in new[]{-1f,1f})
        {
            G.Box("Dusty door sill",car,new Vector3(side*.97f,.49f,0),new Vector3(.035f,.04f,3),dark);
            G.Box("Door seam",car,new Vector3(side*.975f,.72f,-.12f),new Vector3(.02f,.45f,.018f),dark);
            G.Box("Wing mirror",car,new Vector3(side*1.02f,1.06f,-.72f),new Vector3(.2f,.12f,.22f),paint);
            for(int j=0;j<7;j++)G.Box("Front grille",car,new Vector3(j*.13f-.39f,.58f,-2.14f),new Vector3(.045f,.2f,.035f),dark);
        }
        Decal(car,new Vector3(0,.64f,-2.18f),new Vector2(1.6f,.5f));
        G.Box("Rear plate",car,new Vector3(0,.56f,2.16f),new Vector3(.52f,.18f,.025f),pale);
    }
    private static void Dumpster(Transform parent,Vector3 p)
    {
        var d=G.Group("Rusted dumpster",parent,p,25);
        G.Box("Dumpster body",d,new Vector3(0,.55f,0),new Vector3(1.8f,1,.95f),iron);
        foreach(float x in new[]{-.7f,0,.7f})G.Box("Dumpster pressed rib",d,new Vector3(x,.55f,-.49f),new Vector3(.05f,.8f,.05f),dark);
        G.Box("Open lid",d,new Vector3(0,1.2f,.32f),new Vector3(1.9f,.06f,1),dark,new Vector3(-28,0,0));
        d.gameObject.layer=8;var c=d.gameObject.AddComponent<BoxCollider>();c.center=new Vector3(0,.6f,0);c.size=new Vector3(1.8f,1.2f,1);
    }
    private static void Tree(Transform parent,Vector3 p)
    {
        var tree=G.Group("Dead street tree",parent,p);
        G.Beam("Bare trunk",tree,Vector3.zero,new Vector3(.1f,3.5f,.03f),.16f,wood);
        var r=new System.Random((int)(p.z*10+1000));
        for(int i=0;i<9;i++)
        {
            float y=1.7f+i*.19f,sign=i%2==0?-1:1;
            var end=new Vector3(sign*(.6f+(float)r.NextDouble()*.6f),y+.8f,(float)r.NextDouble()-.5f);
            G.Beam("Dead branch",tree,new Vector3(.06f,y,0),end,.055f,wood);
            G.Beam("Dead twig",tree,end,end+new Vector3(sign*.3f,.5f,-.2f),.022f,wood);
        }
    }

    private static void Atmosphere(Transform district)
    {
        string skyPath=Root+"/Materials/Overcast sky.mat";
        var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if(sky==null){sky=new Material(Shader.Find("Outbreak/Overcast City Sky")){name="Overcast gray city sky"};AssetDatabase.CreateAsset(sky,skyPath);}
        sky.SetColor("_SkyColor",new Color(.85f,.84f,.8f));sky.SetColor("_Horizon",new Color(.65f,.67f,.63f));sky.SetColor("_CloudDark",new Color(.53f,.55f,.52f));
        EditorUtility.SetDirty(sky);RenderSettings.skybox=sky;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=new Color(.52f,.545f,.515f);RenderSettings.fogDensity=.014f;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.53f,.55f,.52f);
        RenderSettings.ambientEquatorColor=new Color(.38f,.395f,.37f);
        RenderSettings.ambientGroundColor=new Color(.22f,.235f,.22f);
        foreach(var l in Object.FindObjectsByType<Light>())
        {
            if(l.type==LightType.Directional){l.transform.rotation=Quaternion.Euler(47,-35,0);l.color=new Color(1,.95f,.84f);l.intensity=.82f;l.shadows=LightShadows.Soft;l.shadowStrength=.58f;}
            else if(l.name=="StreetPool")l.intensity=.12f;
        }
        var camera=Camera.main;camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=200;
        var atmosphere=camera.GetComponent<CityCameraAtmosphere>();
        if(atmosphere!=null)OutbreakEditor.Values(atmosphere,s=>{
            s.FindProperty("exposure").floatValue=1.05f;s.FindProperty("saturation").floatValue=.73f;
            s.FindProperty("contrast").floatValue=1.04f;s.FindProperty("vignette").floatValue=.12f;s.FindProperty("bloom").floatValue=.07f;
        });
        ConfigureStaticReflections(district);
    }

    public static void ConfigureStaticReflections(Transform district)
    {
        string path=Root+"/OvercastReflection.asset";
        var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
        if(cube==null)
        {
            const int size=64;
            cube=new Cubemap(size,TextureFormat.RGBA32,true){name="Overcast static reflection"};
            for(int face=0;face<6;face++)
            {
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float u=x/(float)(size-1)*2-1,v=y/(float)(size-1)*2-1;
                    Vector3 d=face==0?new Vector3(1,-v,-u):face==1?new Vector3(-1,-v,u):face==2?new Vector3(u,1,v):face==3?new Vector3(u,-1,-v):face==4?new Vector3(u,-v,1):new Vector3(-u,-v,-1);
                    d.Normalize();float noise=Mathf.PerlinNoise(d.x*4+12,d.z*4+8)*.11f;
                    pixels[y*size+x]=Color.Lerp(new Color(.19f,.20f,.18f),new Color(.64f-noise,.66f-noise,.62f-noise),Mathf.InverseLerp(-.3f,.5f,d.y));
                }
                cube.SetPixels(pixels,(CubemapFace)face);
            }
            cube.Apply(true);AssetDatabase.CreateAsset(cube,path);
        }
        foreach(var probe in district.GetComponentsInChildren<ReflectionProbe>())
        {probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=cube;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;EditorUtility.SetDirty(probe);}
    }

    private static void Polish(Transform additions,Transform district)
    {
        foreach(var r in district.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(r.sharedMaterial!=null && (r.sharedMaterial.name=="RoadPaint"||r.sharedMaterial.name=="FadedRoadPaint"))
            {r.sharedMaterial.color=new Color(.32f,.30f,.22f);EditorUtility.SetDirty(r.sharedMaterial);}
            if(r.name=="DentedBody"||r.name=="Roof"&&r.transform.parent.name=="WreckedSedan")r.sharedMaterial=paint;
        }
        var warehouse=district.Find("Buildings/Warehouse");
        if(warehouse!=null)foreach(var r in warehouse.GetComponentsInChildren<MeshRenderer>())r.sharedMaterial=concrete;
        var roof=district.Find("HorrorCity/Architecture/RiversideFreight");
        if(roof!=null)foreach(var r in roof.GetComponentsInChildren<MeshRenderer>())
            if(r.name=="RoofLeft"||r.name=="RoofRight")r.enabled=false;
        var infill=G.Group("Warehouse masonry transition",additions.Find("Facade additions"),new Vector3(-20,0,14));
        G.Box("Upper floor transition",infill,new Vector3(0,5.9f,0),new Vector3(20,1.8f,20),concrete);
        var skyline=G.Group("Skyline masonry details",additions.Find("Facade additions"));
        var distant=district.Find("HorrorCity/DistantCity");
        if(distant!=null)foreach(var t in distant.GetComponentsInChildren<Transform>().Where(t=>t.name=="BackgroundTenement"))
        {
            var masonry=t.GetComponentsInChildren<MeshRenderer>().First(r=>r.name=="Masonry");float height=masonry.bounds.size.y;
            var detail=G.Group("Highrise pilasters",skyline);detail.position=t.position;detail.rotation=t.rotation;
            foreach(float x in new[]{-7.7f,-4.1f,4.1f,7.7f})G.Box("Vertical masonry bay",detail,new Vector3(x,height*.5f,-6.11f),new Vector3(.18f,height,.16f),pale);
            for(float y=5;y<height-1;y+=3.1f)G.Box("Highrise sill course",detail,new Vector3(0,y,-6.11f),new Vector3(16,.10f,.14f),pale);
        }
        // Low-cost LODs for the distant silhouettes: cull tiny screen-space details.
        foreach(Transform child in skyline)
        {
            var lod=child.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.018f,child.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
        }
        foreach(var t in additions.GetComponentsInChildren<Transform>())
            if(t.name=="Upper apartments"||t.name=="Weathered upper masonry"||t.name=="Upper floor transition"||t.name=="Rusted storefront shutter")
            {t.gameObject.layer=8;if(t.GetComponent<Collider>()==null)t.gameObject.AddComponent<BoxCollider>();}
    }

    private static void Optimize(Transform district,Transform additions)
    {
        // Combine static decorative renderers per building/material, preserving source objects.
        var old=district.Find("HorrorCity");
        if(old!=null)foreach(Transform group in old)
        {
            if(group.name=="Architecture" || group.name=="DistantCity")foreach(Transform child in group)Combine(child);
            else if(group.name=="RoadAndDrainage")Combine(group);
        }
        foreach(Transform group in additions)foreach(Transform child in group.Cast<Transform>().ToArray())
            if(group.name=="Facade additions" || child.name=="Abandoned vehicle" || child.name=="Dead street tree" || child.name=="Rusted dumpster")
            {
                if(child.name=="Skyline masonry details")foreach(Transform tower in child){Combine(tower);var lod=tower.GetComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.018f,tower.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray())});lod.RecalculateBounds();}
                else Combine(child);
            }
        foreach(Transform group in additions)foreach(Transform child in group)
            if(child.name=="Abandoned vehicle"||child.name=="Rusted dumpster"||child.name=="Dead street tree"||child.name=="Warehouse upper tenement")
                PrefabUtility.SaveAsPrefabAsset(child.gameObject,Root+"/Prefabs/"+child.name+".prefab");
    }
    private static void Combine(Transform chunk)
    {
        var renderers=chunk.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.GetComponent<MeshFilter>()!=null&&r.sharedMaterials.Length==1&&r.sharedMaterial!=wear&&r.GetComponent<MeshFilter>().sharedMesh.isReadable).ToArray();
        foreach(var batch in renderers.GroupBy(r=>r.sharedMaterial))
        {
            var mesh=new Mesh{name=chunk.name+" "+batch.Key.name,indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(batch.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=chunk.worldToLocalMatrix*r.localToWorldMatrix}).ToArray());
            string path=AssetDatabase.GenerateUniqueAssetPath(Root+"/Meshes/"+mesh.name.Replace('/','_')+".asset");AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject("Batched "+batch.Key.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(chunk,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=batch.Key;
            foreach(var renderer in batch)renderer.enabled=false;
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic);
        }
    }
    public static void ValidateNavigation()
    {
        var markers=Object.FindObjectsByType<MapMarker>();
        var spawn=markers.First(m=>m.kind==MapMarkerKind.PlayerSpawn);
        if(!NavMesh.SamplePosition(spawn.transform.position,out var start,2,NavMesh.AllAreas))throw new Exception("Player spawn is not on NavMesh.");
        foreach(var marker in markers)
        {
            if(!NavMesh.SamplePosition(marker.transform.position,out var hit,2,NavMesh.AllAreas))throw new Exception(marker.name+" is outside navigation.");
            var path=new NavMeshPath();if(!NavMesh.CalculatePath(hit.position,start.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception(marker.name+" lost its route to the player.");
        }
    }
}
```


## ExistingCitySmokeCheck

Full file path: D:/GV project/Assets/Editor/Art/ExistingCitySmokeCheck.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>Bounded Play Mode integration check on the enhanced copy. Does not save runtime changes.</summary>
[InitializeOnLoad]
public static class ExistingCitySmokeCheck
{
    private const string Key = "ReferenceCity.Smoke";
    private static int stage;
    private static double deadline;
    private static float started;
    private static readonly List<EnemyActor> actors = new List<EnemyActor>();
    private static readonly List<Vector3> positions = new List<Vector3>();
    private static readonly List<string> results = new List<string>();
    static ExistingCitySmokeCheck()
    {
        if(SessionState.GetBool(Key,false))Hook();
    }
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=ExistingCityEnhancer.ScenePath)throw new InvalidOperationException("Open the enhanced city copy.");
        SessionState.SetBool(Key,true);Hook();EditorApplication.isPaused=false;EditorApplication.EnterPlaymode();
    }
    public static void RunBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        ExistingCityEnhancer.ConfigureStaticReflections(GameObject.Find("OutbreakDistrict").transform);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        Begin();
    }
    private static void Hook(){stage=0;actors.Clear();positions.Clear();results.Clear();deadline=EditorApplication.timeSinceStartup+45;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    private static void Require(bool ok,string text){if(!ok)throw new Exception(text);results.Add("PASS: "+text);}
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Integration check timed out.");
            if(!EditorApplication.isPlaying)return;
            EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();
            var ui=Object.FindAnyObjectByType<GameUIController>();
            if(ui!=null){OutbreakEditor.Values(ui,s=>s.FindProperty("pauseOnFocusLoss").boolValue=false);ui.Resume();}
            Time.timeScale=1;
            if(Time.time<.1f)return;
            if(stage==0)
            {
                var world=Object.FindAnyObjectByType<EnemyWorld>();var motor=world.movement;
                Require(motor!=null&&motor.enabled&&motor.IsGrounded,"Existing movement initializes and ground probe detects the street");
                var waves=Object.FindAnyObjectByType<WaveManager>();if(waves!=null)waves.enabled=false;
                foreach(var existing in world.Living.ToArray()){existing.Movement.Stop();StopBrains(existing);}
                var cc=motor.GetComponent<CharacterController>();Vector3 before=motor.transform.position;
                cc.Move(Vector3.forward);Physics.SyncTransforms();
                Require((motor.transform.position-before).z>.85f,"Player collision controller can move along the main street");
                Require(!Physics.CheckBox(new Vector3(0,1,-22),new Vector3(.45f,.8f,.45f),Quaternion.identity,1<<8,QueryTriggerInteraction.Ignore),"Central route stays clear of the added vehicle obstacles");
                var weapons=world.weapons;var aim=motor.GetComponent<PlayerAimController>();
                Require(weapons!=null&&weapons.CurrentWeapon!=null&&weapons.ActiveView!=null,"Existing weapon and muzzle references initialize");
                var camera=Camera.main;var rig=camera.GetComponent<ThirdPersonCameraController>();
                Require(rig!=null&&rig.enabled&&rig.PlanarForward.sqrMagnitude>.9f,"Existing camera orbit controller initializes");
                rig.enabled=false;camera.transform.position=motor.transform.position+new Vector3(.3f,1.6f,-3);
                var target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.name="Temporary smoke-test target";target.layer=8;
                target.transform.position=motor.transform.position+new Vector3(0,1.4f,10);target.transform.localScale=Vector3.one*1.8f;
                var receiver=target.AddComponent<ShootingTarget>();camera.transform.LookAt(target.transform.position);Physics.SyncTransforms();
                aim.SetAiming(true);int ammo=weapons.MagazineAmmo;float hp=receiver.CurrentHealth;
                Require(weapons.TryFire()&&weapons.MagazineAmmo==ammo-1,"Aimed shooting consumes one round");
                Require(receiver.CurrentHealth<hp&&!weapons.LastShotBlocked,"Camera-to-muzzle shooting hits and damages its target");
                Require(weapons.TryReload()&&weapons.IsReloading,"Reload begins using the existing weapon system");
                weapons.CancelReload();Require(weapons.SelectWeapon(1),"Existing weapon switching works");
                Object.Destroy(target);rig.enabled=true;aim.SetAiming(false);
                int i=0;foreach(string kind in new[]{"Hunter","Runner","Ambusher","Elite"})
                {
                    Require(NavMesh.SamplePosition(new Vector3(-.6f+i*.4f,0,-30),out var point,2,NavMesh.AllAreas),kind+" test start lies on navigation");
                    var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/"+kind+".prefab"),point.position,Quaternion.identity).GetComponent<EnemyActor>();
                    actor.Initialize(world);StopBrains(actor);
                    Require(actor.Movement.MoveTo(motor.transform.position,kind=="Runner"?4.8f:2.25f),kind+" accepts a route through the enhanced street");
                    actors.Add(actor);positions.Add(actor.transform.position);i++;
                }
                started=Time.time;stage=1;
            }
            else if(stage==1&&Time.time-started>2.5f)
            {
                for(int i=0;i<actors.Count;i++)
                {
                    var agent=actors[i].GetComponent<NavMeshAgent>();
                    Require(agent.isOnNavMesh&&agent.pathStatus==NavMeshPathStatus.PathComplete&&(actors[i].transform.position-positions[i]).magnitude>1,"Enemy "+i+" moves on a complete path around street obstacles");
                }
                results.Add("Scope: API/physics/agent integration checks. Keyboard, mouse feel, full waves and target-machine performance need manual review.");
                Finish();
            }
        }
        catch(Exception e){results.Add("FAIL: "+e);Finish();}
    }
    private static void StopBrains(EnemyActor actor)
    {
        foreach(var b in actor.GetComponents<MonoBehaviour>())if(b is HunterBrain||b is RunnerBrain||b is AmbusherBrain||b is EliteBrain)b.enabled=false;
    }
    private static void Finish()
    {
        File.WriteAllLines("Logs/ReferenceCitySmoke.txt",results);SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();
        if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(results.Any(r=>r.StartsWith("FAIL:"))?1:0);
    }
}
```


## FirstPersonSetup

Full file path: D:/GV project/Assets/Editor/Art/FirstPersonSetup.cs

```csharp
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Converts the enhanced gameplay scene, using the installed Survivalist FPS meshes.</summary>
public static class FirstPersonSetup
{
    public const string ArmsPath = "Assets/Prefabs/Player/FirstPersonArms.prefab";
    public const string PlayerPath = "Assets/Prefabs/Player/PlayerFirstPerson.prefab";
    private const int ViewLayer = 30;

    [MenuItem("Zombie Outbreak/First Person/Apply to Enhanced City")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring first person.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ExistingCityEnhancer.ScenePath)
            throw new InvalidOperationException("Open the enhanced ZombieMap before applying first person.");
        var player = Object.FindAnyObjectByType<ThirdPersonPlayerController>();
        var camera = Camera.main;
        if (player == null || camera == null) throw new InvalidOperationException("Player and Main Camera are required.");
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layer = tags.FindProperty("layers").GetArrayElementAtIndex(ViewLayer);
        if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != "FirstPerson")
            throw new InvalidOperationException("Layer 30 is already in use.");
        layer.stringValue = "FirstPerson"; tags.ApplyModifiedPropertiesWithoutUndo();

        var visual = player.transform.Find("SurvivalistVisual");
        if (visual == null) throw new InvalidOperationException("The Survivalist player visual is required.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ArmsPath) == null) CreateArms(visual.gameObject);
        var oldView = player.transform.Find("FirstPersonView");
        if (oldView != null) Object.DestroyImmediate(oldView.gameObject);
        var root = Child("FirstPersonView", player.transform);
        var socket = Child("WeaponSocket", root);
        var armsObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ArmsPath), root);
        armsObject.name = "FirstPersonArms";
        armsObject.transform.localPosition = new Vector3(0, -1.68f, .12f);
        armsObject.transform.localRotation = Quaternion.identity;
        var arms = armsObject.GetComponent<FirstPersonArms>();
        var viewCamera = Child("WeaponCamera", root).gameObject.AddComponent<Camera>();
        viewCamera.clearFlags = CameraClearFlags.Depth;
        viewCamera.cullingMask = 1 << ViewLayer;
        viewCamera.nearClipPlane = .01f; viewCamera.farClipPlane = 3f;
        viewCamera.fieldOfView = 75; viewCamera.depth = camera.depth + 1;
        viewCamera.allowHDR = false; viewCamera.useOcclusionCulling = false;
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ViewLayer;
        var rig = player.GetComponent<FirstPersonWeaponRig>() ?? player.gameObject.AddComponent<FirstPersonWeaponRig>();
        var so = new SerializedObject(rig);
        so.FindProperty("worldCamera").objectReferenceValue = camera;
        so.FindProperty("weaponCamera").objectReferenceValue = viewCamera;
        so.FindProperty("viewRoot").objectReferenceValue = root;
        so.FindProperty("weaponSocket").objectReferenceValue = socket;
        so.FindProperty("arms").objectReferenceValue = arms;
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        var list = so.FindProperty("bodyRenderers"); list.arraySize = renderers.Length;
        for (int i=0; i<renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        var weapons = new SerializedObject(player.GetComponent<PlayerWeaponController>());
        weapons.FindProperty("weaponSocket").objectReferenceValue = socket;
        weapons.ApplyModifiedPropertiesWithoutUndo();
        foreach (var grip in visual.GetComponentsInChildren<SupportHandGrip>(true)) grip.enabled = false;
        var stance = new SerializedObject(player.GetComponent<PlayerStanceController>());
        stance.FindProperty("crouchedHeight").floatValue = 1.2f; stance.ApplyModifiedPropertiesWithoutUndo();
        var cameraSettings = new SerializedObject(camera.GetComponent<ThirdPersonCameraController>());
        cameraSettings.FindProperty("firstPerson").boolValue = true;
        cameraSettings.FindProperty("firstPersonFieldOfView").floatValue = 75;
        cameraSettings.FindProperty("firstPersonAimFieldOfView").floatValue = 55;
        cameraSettings.FindProperty("startingPitch").floatValue = 0;
        cameraSettings.FindProperty("minimumPitch").floatValue = -85;
        cameraSettings.FindProperty("maximumPitch").floatValue = 85;
        cameraSettings.ApplyModifiedPropertiesWithoutUndo();
        camera.cullingMask &= ~(1 << ViewLayer); camera.nearClipPlane = .04f; camera.fieldOfView = 75;
        camera.transform.SetPositionAndRotation(player.transform.position + Vector3.up * 1.66f, player.transform.rotation);
        root.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
        PrefabUtility.SaveAsPrefabAsset(player.gameObject, PlayerPath);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("First-person conversion saved: enhanced ZombieMap and PlayerFirstPerson prefab.");
    }

    private static Transform Child(string name, Transform parent)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
    }

    private static void CreateArms(GameObject source)
    {
        var root = Object.Instantiate(source);
        try
        {
            root.name = "FirstPersonArms"; root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
            var animator = root.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) throw new InvalidOperationException("FPS arms require the Survivalist Humanoid Avatar.");
            var leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            // Keep the existing authored carry pose, including the finger curl. No Animator writes to this rig at runtime.
            Object.DestroyImmediate(animator);
            int found = 0;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                bool keep = renderer.name == "SK_Miliary_FPS_Arms_Gloves1" || renderer.name == "SK_Miliary_Military_FPS_Shirt3";
                renderer.gameObject.SetActive(keep); renderer.enabled = keep;
                if (!keep) continue;
                for (var parent = renderer.transform.parent; parent != null && parent != root.transform; parent = parent.parent)
                    parent.gameObject.SetActive(true);
                found++;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
            if (found != 2) throw new InvalidOperationException("Dedicated FPS glove and shirt meshes were not found.");
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ViewLayer;
            var arms = root.AddComponent<FirstPersonArms>();
            var so = new SerializedObject(arms);
            SetArm(so.FindProperty("left"), leftUpper, leftLower, leftHand);
            SetArm(so.FindProperty("right"), rightUpper, rightLower, rightHand);
            so.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory(Path.GetDirectoryName(ArmsPath));
            PrefabUtility.SaveAsPrefabAsset(root, ArmsPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void SetArm(SerializedProperty arm, Transform upper, Transform lower, Transform hand)
    {
        arm.FindPropertyRelative("upper").objectReferenceValue = upper;
        arm.FindPropertyRelative("lower").objectReferenceValue = lower;
        arm.FindPropertyRelative("hand").objectReferenceValue = hand;
        arm.FindPropertyRelative("wristRotation").quaternionValue = hand.rotation;
    }
}
```


## GroundedZombieActions

Full file path: D:/GV project/Assets/Editor/Art/GroundedZombieActions.cs

```csharp
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Re-encodes the vendor's action poses against the corrected forward-facing Avatar.</summary>
public static class GroundedZombieActions
{
    public static AnimationClip Bake(string name, AnimationClip source, string outputRoot = "Assets/Animations/GroundedEnemies")
    {
        const string root = "Assets/Animations/GroundedEnemies";
        var original = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ImportedCharacterSetup.Zombie));
        var corrected = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(root + "/CanonicalZombie.prefab"));
        var overrides = new AnimatorOverrideController(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(root + "/GroundedEnemies.controller"));
        try
        {
            var from = original.GetComponent<Animator>();
            var to = corrected.GetComponent<Animator>(); to.enabled = false;
            original.transform.position = Vector3.zero; original.transform.rotation = Quaternion.identity;
            corrected.transform.position = Vector3.zero; corrected.transform.rotation = Quaternion.identity;
            from.cullingMode = AnimatorCullingMode.AlwaysAnimate; from.applyRootMotion = false;
            overrides["GroundedChase"] = source;
            from.runtimeAnimatorController = overrides; from.Rebind();
            from.SetFloat("Speed", 4); from.SetFloat("GaitScale", 1);
            var sourceBones = original.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
            var targetBones = corrected.GetComponentsInChildren<Transform>().Where(t => sourceBones.ContainsKey(t.name)).ToArray();
            var curves = Enumerable.Range(0, HumanTrait.MuscleCount + 7).Select(_ => new AnimationCurve()).ToArray();
            int steps = Mathf.CeilToInt(source.length * 60);
            Vector3 origin = Vector3.zero;
            using (var handler = new HumanPoseHandler(to.avatar, to.transform))
            {
                for (int i = 0; i <= steps; i++)
                {
                    float time = i * source.length / steps;
                    from.Play("Chase", 0, Mathf.Min(.99999f, i / (float)steps)); from.Update(.00001f);
                    foreach (var bone in targetBones)
                    {
                        bone.localPosition = sourceBones[bone.name].localPosition;
                        bone.localRotation = sourceBones[bone.name].localRotation;
                    }
                    var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                    if (i == 0) origin = new Vector3(pose.bodyPosition.x, 0, pose.bodyPosition.z);
                    pose.bodyPosition -= origin;
                    for (int j = 0; j < HumanTrait.MuscleCount; j++) curves[j].AddKey(time, pose.muscles[j]);
                    int offset = HumanTrait.MuscleCount;
                    for (int j = 0; j < 3; j++) curves[offset+j].AddKey(time, pose.bodyPosition[j]);
                    for (int j = 0; j < 4; j++) curves[offset+3+j].AddKey(time, pose.bodyRotation[j]);
                }
            }
            string path = outputRoot + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves(); clip.name = name; clip.frameRate = 60;
            var properties = HumanTrait.MuscleName.Concat(new[] { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" }).ToArray();
            for (int j = 0; j < properties.Length; j++)
            {
                string property = properties[j];
                foreach (string side in new[] { "Left", "Right" })
                    foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                    {
                        string prefix = side + " " + finger + " ";
                        if (property.StartsWith(prefix)) property = side + "Hand." + finger + "." + property.Substring(prefix.Length);
                    }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curves[j]);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0; settings.stopTime = source.length; settings.loopTime = false;
            settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true; settings.heightFromFeet = false;
            settings.loopBlendOrientation = true; settings.loopBlendPositionY = true; settings.loopBlendPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
            return clip;
        }
        finally { Object.DestroyImmediate(original); Object.DestroyImmediate(corrected); Object.DestroyImmediate(overrides); }
    }
}
```


## GroundedZombieRig

Full file path: D:/GV project/Assets/Editor/Art/GroundedZombieRig.cs

```csharp
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class GroundedZombieRig
{
 public static void Build(){
 var source=AssetDatabase.LoadAssetAtPath<GameObject>(ImportedCharacterSetup.Zombie);
 var wrapper=new GameObject("CanonicalZombie");var go=Object.Instantiate(source,wrapper.transform);go.name="SourceRig";
 try{
 go.transform.localRotation=Quaternion.Euler(0,-90,0);
 var old=go.GetComponent<Animator>();
 foreach(bool left in new[]{true,false}){
 var thigh=old.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
 var calf=old.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
 var foot=old.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
 var toe=old.GetBoneTransform(left?HumanBodyBones.LeftToes:HumanBodyBones.RightToes);
 Aim(thigh,calf,-Vector3.up+Vector3.forward*.015f);Aim(calf,foot,-Vector3.up-Vector3.forward*.015f);if(toe!=null)Aim(foot,toe,Vector3.forward);
 Aim(old.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm),old.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm),left?Vector3.left:Vector3.right);
 Aim(old.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm),old.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand),left?Vector3.left:Vector3.right);
 }
 var d=((ModelImporter)AssetImporter.GetAtPath(ImportedCharacterSetup.Zombie)).humanDescription;
 d.skeleton=wrapper.GetComponentsInChildren<Transform>().Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
 Object.DestroyImmediate(old);var a=wrapper.AddComponent<Animator>();var avatar=AvatarBuilder.BuildHumanAvatar(wrapper,d);avatar.name="CanonicalZombieAvatar";
 if(!avatar.isHuman||!avatar.isValid)throw new System.Exception("Invalid canonical avatar");
 string path="Assets/Animations/GroundedEnemies/CanonicalZombieAvatar.asset";var existing=AssetDatabase.LoadAssetAtPath<Avatar>(path);
 if(existing==null){AssetDatabase.CreateAsset(avatar,path);existing=avatar;}else{EditorUtility.CopySerialized(avatar,existing);Object.DestroyImmediate(avatar);}
 a.avatar=existing;a.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/GroundedEnemies/Hunter.overrideController");a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
 var material=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Hunter.prefab").GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
 foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.sharedMaterial=material;skin.updateWhenOffscreen=true;}
 PrefabUtility.SaveAsPrefabAsset(wrapper,"Assets/Animations/GroundedEnemies/CanonicalZombie.prefab");AssetDatabase.SaveAssets();
 }finally{Object.DestroyImmediate(wrapper);}
 }
 private static void Aim(Transform bone,Transform child,Vector3 direction){bone.rotation=Quaternion.FromToRotation(child.position-bone.position,direction)*bone.rotation;}
}
```


## ImportedCharacterMotion

Full file path: D:/GV project/Assets/Editor/Art/ImportedCharacterMotion.cs

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Retargets imported Humanoid motion while retaining the gameplay Animator parameters.</summary>
public static class ImportedCharacterMotion
{
    private const string Root = ImportedCharacterSetup.MotionRoot;
    public static void Build()
    {
        var idle = Motion("Idle","m@Idle");
        var walk = Motion("Walk","m@WalkForwards");
        var run = Motion("Run","m@RunForwards");
        var sprint = Motion("Sprint","m@SprintForwards");
        var strafe = Motion("StrafeRight","m@StrafeRight");
        var left = Clone("StrafeLeft",strafe,true);
        var back = Clone("WalkBackwards",walk,false,true);
        var forward = Motion("AimForward","m@StrafeWalkForwards");
        var aimIdle = Motion("AimIdle","m@StrafeIdle");
        string controllerPath=Root+"/Survivor.controller";
        if(AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)==null)
            AssetDatabase.CopyAsset(Phase2AnimationAssets.ControllerPath,controllerPath);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(!controller.layers.Any(l=>l.name=="Weapon Carry"))
        {
            controller.AddLayer("Weapon Carry");
            var layers=controller.layers;
            var carry=layers[layers.Length-1];
            var mask=new AvatarMask {name="WeaponCarryArms"};
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
            foreach(var p in new[]{AvatarMaskBodyPart.LeftArm,AvatarMaskBodyPart.RightArm,AvatarMaskBodyPart.LeftFingers,AvatarMaskBodyPart.RightFingers}) mask.SetHumanoidBodyPartActive(p,true);
            AssetDatabase.AddObjectToAsset(mask,controller);
            carry.avatarMask=mask;carry.defaultWeight=1;
            var state=carry.stateMachine.AddState("Carry");
            state.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(Phase2AnimationAssets.Root+"/Clips/AimIdle.anim");
            carry.stateMachine.defaultState=state;
            // Actions remain above the carry pose so reload and hit reactions stay visible.
            controller.layers=new[]{layers[0],carry,layers[1]};
        }
        var player=Override("Survivor",controller);
        player["AimIdle"]=ImportedShooterPoses.Create("Carry",2.4f,true);
        player["Fire"]=ImportedShooterPoses.Create("ImportedFire",.18f,false);
        player["Reload"]=ImportedShooterPoses.Create("ImportedReload",1.2f,false);
        player["Idle"]=idle;player["WalkForward"]=walk;player["WalkBackward"]=back;
        player["StrafeRight"]=strafe;player["StrafeLeft"]=left;player["Run"]=run;player["Sprint"]=sprint;
        player["Jump"]=Motion("Jump","m@StandingJump_Frame02",false);
        player["Falling"]=Motion("Falling","m@Falling");
        player["AimForward"]=forward;player["AimBackward"]=back;player["AimLeft"]=left;player["AimRight"]=strafe;
        EditorUtility.SetDirty(player);

        var zombieWalk=ImportClip(ImportedCharacterSetup.Zombie);
        var attack=ImportClip(ImportedCharacterSetup.Root+"/PixelhouseZombie/fury.FBX");
        var death=ImportClip(ImportedCharacterSetup.Root+"/PixelhouseZombie/dead.FBX");
        var standing=Clone("InfectedIdle",zombieWalk);
        // A stable first-frame stance with a small breathing motion, avoiding marching while stopped.
        foreach(var binding in AnimationUtility.GetCurveBindings(standing))
        {
            var curve=AnimationUtility.GetEditorCurve(standing,binding);
            float v=curve.Evaluate(0);
            AnimationUtility.SetEditorCurve(standing,binding,AnimationCurve.Constant(0,2.4f,v));
        }
        AnimationUtility.SetEditorCurve(standing,EditorCurveBinding.FloatCurve("",typeof(Animator),"Chest Front-Back"),new AnimationCurve(new Keyframe(0,.10f),new Keyframe(1.2f,.12f),new Keyframe(2.4f,.10f)));
        var idleSettings=AnimationUtility.GetAnimationClipSettings(standing);idleSettings.startTime=0;idleSettings.stopTime=2.4f;idleSettings.loopTime=true;AnimationUtility.SetAnimationClipSettings(standing,idleSettings);
        var zombieRun=Clone("InfectedRun",run);
        foreach(var binding in AnimationUtility.GetCurveBindings(zombieWalk))
        {
            string p=binding.propertyName;
            if(p.Contains("Arm")||p.Contains("Forearm")||p.Contains("Hand")||p.Contains("Shoulder"))
            {
                var curve=AnimationUtility.GetEditorCurve(zombieWalk,binding);
                AnimationUtility.SetEditorCurve(zombieRun,binding,AnimationCurve.Constant(0,run.length,curve.Evaluate(.25f)));
            }
        }
        string enemyPath=Root+"/Infected.controller";
        if(AssetDatabase.LoadAssetAtPath<AnimatorController>(enemyPath)==null)AssetDatabase.CopyAsset(Phase7Setup.ControllerPath,enemyPath);
        var enemy=AssetDatabase.LoadAssetAtPath<AnimatorController>(enemyPath);
        foreach(var s in enemy.layers[0].stateMachine.states)
            if(s.state.name=="Attack"||s.state.name=="Heavy") {s.state.speed=attack.length;s.state.speedParameter="AttackSpeed";s.state.speedParameterActive=true;}
        foreach(string kind in new[]{"Hunter","Runner","Ambusher","Elite"})
        {
            var c=Override(kind,enemy);
            c["ZombieIdle"]=standing;c["ZombieWalkForward"]=zombieWalk;c["ZombieRun"]=zombieRun;
            c["ZombieAttack"]=attack;c["EliteHeavy"]=attack;c["ZombieDeath"]=death;
            EditorUtility.SetDirty(c);
        }
        AssetDatabase.SaveAssets();
    }
    public static AnimationClip ImportClip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
    private static AnimationClip Motion(string name,string source,bool loop=true)
    {
        var clip=Clone(name,ImportClip(ImportedCharacterSetup.Root+"/UnityMotion/"+source+".fbx"));
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);return clip;
    }
    private static AnimationClip Clone(string name,AnimationClip source,bool mirror=false,bool reverse=false)
    {
        string path=Root+"/"+name+".anim";
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(clip==null){clip=Object.Instantiate(source);AssetDatabase.CreateAsset(clip,path);}
        else EditorUtility.CopySerialized(source,clip);
        clip.name=name;
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.mirror=mirror;AnimationUtility.SetAnimationClipSettings(clip,settings);
        if(reverse)foreach(var binding in AnimationUtility.GetCurveBindings(clip))
        {
            var original=AnimationUtility.GetEditorCurve(clip,binding);
            var keys=original.keys.Reverse().Select(k=>new Keyframe(source.length-k.time,k.value,-k.outTangent,-k.inTangent)).ToArray();
            AnimationUtility.SetEditorCurve(clip,binding,new AnimationCurve(keys));
        }
        EditorUtility.SetDirty(clip);return clip;
    }
    private static AnimatorOverrideController Override(string name,RuntimeAnimatorController source)
    {
        string path=Root+"/"+name+".overrideController";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if(controller==null){controller=new AnimatorOverrideController(source);AssetDatabase.CreateAsset(controller,path);}
        controller.runtimeAnimatorController=source;return controller;
    }
}
```


## ImportedCharacterSetup

Full file path: D:/GV project/Assets/Editor/Art/ImportedCharacterSetup.cs

```csharp
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Imports licensed skinned models through Unity and retains the existing gameplay roots.</summary>
public static class ImportedCharacterSetup
{
    public const string Root = "Assets/Art/ImportedCharacters";
    public const string MotionRoot = "Assets/Animations/ImportedMotion";
    public const string Soldier = Root + "/Rocketbox/Military_Male_01.fbx";
    public const string Zombie = Root + "/PixelhouseZombie/walk.FBX";

    [MenuItem("Zombie Outbreak/Art/Apply imported people and zombies")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before changing models.");
        EditorSceneManager.SaveOpenScenes();
        PrepareImports();
        ImportedCharacterMotion.Build();
        OutbreakEditor.Open();
        ReplacePlayer(Object.FindAnyObjectByType<ThirdPersonPlayerController>().gameObject);
        EditPrefab("Assets/Prefabs/Player/PlayerPhase3.prefab", ReplacePlayer);
        foreach (string kind in new[] { "EnemyBase", "Hunter", "Runner", "Ambusher", "Elite" })
            EditPrefab("Assets/Prefabs/Enemies/" + kind + ".prefab", go => ReplaceEnemy(go, kind));
        OutbreakEditor.Save("IMPORTED_CHARACTER_ART");
    }

    public static void PrepareImports()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Phase2AnimationAssets.Folder(MotionRoot);
        Phase2AnimationAssets.Folder(Root + "/Materials");
        foreach (string path in Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Select(p => p.Replace('\\','/')))
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".fbx" && ext != ".tga" && ext != ".jpg") continue;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter texture)
            {
                bool normal = Path.GetFileName(path).ToLowerInvariant().Contains("normal");
                texture.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                texture.sRGBTexture = !normal && !path.Contains("specular");
                texture.maxTextureSize = 2048; texture.mipmapEnabled = true;
                texture.SaveAndReimport();
            }
            else if (AssetImporter.GetAtPath(path) is ModelImporter model)
            {
                model.animationType = ModelImporterAnimationType.Human;
                model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                model.importCameras = false; model.importLights = false; model.importVisibility = false;
                model.optimizeGameObjects = false; model.importAnimation = !path.Contains("Rocketbox");
                model.globalScale = path.Contains("PixelhouseZombie") ? 3.2f : 1f;
                model.SaveAndReimport();
                if (path.Contains("PixelhouseZombie"))
                {
                    // This legacy FBX stores its auto-generated Avatar skeleton in source units.
                    // Use Unity's converted local transforms so skin bind poses and animation agree.
                    var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var description = model.humanDescription;
                    description.skeleton = imported.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
                    { name=t.name, position=t.localPosition, rotation=t.localRotation, scale=t.localScale }).ToArray();
                    model.humanDescription=description;
                    model.SaveAndReimport();
                }
                if (model.importAnimation)
                {
                    var clips = model.defaultClipAnimations;
                    foreach (var clip in clips)
                    {
                        clip.name = Path.GetFileNameWithoutExtension(path);
                        clip.loopTime = !path.Contains("fury") && !path.Contains("dead") && !path.Contains("Jump");
                        clip.loopPose = clip.loopTime;
                        clip.lockRootRotation = true; clip.keepOriginalOrientation = false;
                        clip.lockRootPositionXZ = true; clip.keepOriginalPositionXZ = false;
                        clip.lockRootHeightY = true; clip.keepOriginalPositionY = false; clip.heightFromFeet = true;
                    }
                    model.clipAnimations = clips; model.SaveAndReimport();
                }
            }
        }
    }

    private static Animator InstantiateVisual(string path, Transform parent, string name)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null) throw new InvalidOperationException("Missing downloaded model: " + path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = name; instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity; instance.transform.localScale = Vector3.one;
        var animator = instance.GetComponent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid)
            throw new InvalidOperationException("Model must import with a valid Humanoid Avatar: " + path);
        animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach(var t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = parent.gameObject.layer;
        return animator;
    }

    private static void ReplacePlayer(GameObject player)
    {
        var old = player.GetComponentInChildren<Animator>(true);
        var next = InstantiateVisual(Soldier, player.transform, "SurvivorVisual");
        next.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MotionRoot + "/Survivor.overrideController");
        ApplySoldierMaterials(next);
        var weapons = player.GetComponent<PlayerWeaponController>();
        if (weapons != null)
        {
            var serialized = new SerializedObject(weapons);
            var socket = serialized.FindProperty("weaponSocket").objectReferenceValue as Transform;
            if(socket == null) socket = new GameObject("WeaponSocket").transform;
            socket.SetParent(next.GetBoneTransform(HumanBodyBones.RightHand), false);
            // Calibrate against the evaluated retargeted pose, not the source skeleton's
            // authored wrist axes. Humanoid conversion changes those axes on this Avatar.
            next.Rebind();
            next.SetBool("IsGrounded",true);
            next.SetFloat("Speed",0);
            next.Play("Locomotion",0,0);
            next.Update(.001f);
            socket.position = next.GetBoneTransform(HumanBodyBones.RightHand).position + player.transform.forward * .05f;
            socket.rotation = player.transform.rotation;
            socket.localScale = Vector3.one;
            OutbreakEditor.Set(weapons, "weaponSocket", socket);
        }
        var driver = player.GetComponent<PlayerAnimatorDriver>();
        if(driver != null) OutbreakEditor.Set(driver, "animator", next);
        next.gameObject.AddComponent<SupportHandGrip>();
        next.gameObject.AddComponent<ShooterBodyPose>();
        if(old != null) Object.DestroyImmediate(old.gameObject);
    }

    private static void ApplySoldierMaterials(Animator animator)
    {
        foreach(var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => {
                string stem = m.name;
                string color = Root + "/Rocketbox/" + stem + "_color" + ((stem.EndsWith("body") || stem.EndsWith("helmet") || stem.EndsWith("equipment")) ? "_acu" : "") + ".tga";
                return MakeMaterial(stem, color, Root + "/Rocketbox/" + stem + "_normal.tga", Color.white, .22f);
            }).ToArray();
            renderer.updateWhenOffscreen = true;
        }
    }

    private static void ReplaceEnemy(GameObject root, string kind)
    {
        // Variants may have inherited or previously overridden Visual children.
        var old = root.transform.Find("Visual");
        var next = old != null && old.GetComponentInChildren<SkinnedMeshRenderer>(true) != null ? old.GetComponent<Animator>() : null;
        if(next == null)
        {
            if(old != null) Object.DestroyImmediate(old.gameObject);
            next = InstantiateVisual(Zombie, root.transform, "Visual");
        }
        next.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MotionRoot + "/" + (kind == "EnemyBase" ? "Hunter" : kind) + ".overrideController");
        // The source Avatar's anatomical forward is +X, while all gameplay roots use +Z.
        next.transform.localRotation = Quaternion.Euler(0,-90,0);
        next.transform.localPosition = Vector3.zero;
        next.transform.localScale = kind == "Elite" ? new Vector3(1.17f,1.12f,1.17f) : kind == "Runner" ? new Vector3(.94f,1,.94f) : Vector3.one;
        Color tint = kind == "Runner" ? new Color(1,.85f,.79f) : kind == "Ambusher" ? new Color(.79f,.84f,.88f) : kind == "Elite" ? new Color(.82f,.87f,.78f) : Color.white;
        var material = MakeMaterial("Infected_" + kind, Root + "/PixelhouseZombie/difusse.jpg", Root + "/PixelhouseZombie/normal.JPG", tint, .15f);
        foreach(var renderer in next.GetComponentsInChildren<SkinnedMeshRenderer>())
        { renderer.sharedMaterial = material; renderer.updateWhenOffscreen = true; }
        OutbreakEditor.Set(root.GetComponent<EnemyAnimationController>(), "animator", next);
    }

    private static Material MakeMaterial(string name, string albedo, string normal, Color tint, float smoothness)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material,path); }
        material.color = tint; material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);
        if(material.mainTexture == null) throw new InvalidOperationException("Missing character texture: " + albedo);
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
        material.EnableKeyword("_NORMALMAP"); material.SetFloat("_BumpScale",.7f);
        material.SetFloat("_Glossiness",smoothness); material.SetFloat("_Metallic",0);
        EditorUtility.SetDirty(material); return material;
    }

    private static void EditPrefab(string path, Action<GameObject> action)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try { action(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
```


## ImportedShooterPoses

Full file path: D:/GV project/Assets/Editor/Art/ImportedShooterPoses.cs

```csharp
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Authors upper-body poses against the actual imported arm lengths, including finger grip.</summary>
public static class ImportedShooterPoses
{
    public static AnimationClip Create(string name,float duration,bool loop, string modelPath = null, string outputRoot = null)
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath ?? ImportedCharacterSetup.Soldier));
        try
        {
            var a=go.GetComponent<Animator>();
            var bones=go.GetComponentsInChildren<Transform>();
            var rotations=bones.Select(t=>t.localRotation).ToArray();
            var positions=bones.Select(t=>t.localPosition).ToArray();
            var curves=Enumerable.Range(0,HumanTrait.MuscleCount).Select(_=>new AnimationCurve()).ToArray();
            using(var handler=new HumanPoseHandler(a.avatar,a.transform))
            {
                int count=Mathf.CeilToInt(duration*30);
                for(int frame=0;frame<=count;frame++)
                {
                    for(int j=0;j<bones.Length;j++){bones[j].localRotation=rotations[j];bones[j].localPosition=positions[j];}
                    float t=frame/(float)count;
                    Pose(a,t,name);
                    var human=new HumanPose();handler.GetHumanPose(ref human);
                    for(int j=0;j<curves.Length;j++)
                    {
                        string muscle=HumanTrait.MuscleName[j];
                        float v=human.muscles[j];
                        if(muscle.Contains("Stretched") && (muscle.Contains("Thumb")||muscle.Contains("Index")||muscle.Contains("Middle")||muscle.Contains("Ring")||muscle.Contains("Little")))
                            v=muscle.Contains("Index")?-.25f:-.65f;
                        curves[j].AddKey(t*duration,v);
                    }
                }
            }
            string path=(outputRoot ?? ImportedCharacterSetup.MotionRoot)+"/"+name+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}
            clip.ClearCurves();clip.name=name;clip.frameRate=30;
            for(int j=0;j<curves.Length;j++)
            {
                string property=HumanTrait.MuscleName[j];
                // Finger binding names differ from the names exposed by HumanTrait.
                foreach(string side in new[]{"Left", "Right"})
                    foreach(string finger in new[]{"Thumb", "Index", "Middle", "Ring", "Little"})
                    {
                        string prefix=side+" "+finger+" ";
                        if(property.StartsWith(prefix)) property=side+"Hand."+finger+"."+property.Substring(prefix.Length);
                    }
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property),curves[j]);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.startTime=0;settings.stopTime=duration;settings.loopTime=loop;
            AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);return clip;
        }
        finally{Object.DestroyImmediate(go);}
    }
    public static void Pose(Animator a,float t,string action)
    {
        Transform r=a.transform;
        Vector3 right=new Vector3(.18f,1.33f,.18f),left=new Vector3(-.03f,1.29f,.50f);
        bool lowReady=action=="LowReady";
        if(lowReady){right=new Vector3(.2f,1.10f,.22f);left=new Vector3(-.03f,.99f,.47f);}
        float recoil=action=="ImportedFire"?Mathf.Sin(t*Mathf.PI)*.035f:0;
        right.z-=recoil;left.z-=recoil;
        if(action=="ImportedReload")
        {
            float lower=Mathf.Sin(t*Mathf.PI);
            right+=new Vector3(.03f,-.14f,-.06f)*lower;
            left=Vector3.Lerp(left,t<.45f?new Vector3(-.14f,.94f,.16f):new Vector3(.14f,1.15f,.43f),lower);
        }
        Solve(a,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,r.TransformPoint(right),r.TransformPoint(new Vector3(.5f,1.1f,.03f)));
        Solve(a,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,r.TransformPoint(left),r.TransformPoint(new Vector3(-.42f,1.10f,.25f)));
        foreach(var side in new[]{HumanBodyBones.RightHand,HumanBodyBones.LeftHand})
        {
            var hand=a.GetBoneTransform(side);
            var finger=a.GetBoneTransform(side==HumanBodyBones.RightHand?HumanBodyBones.RightMiddleProximal:HumanBodyBones.LeftMiddleProximal);
            Vector3 direction=lowReady?Quaternion.AngleAxis(22,r.right)*r.forward:r.forward;
            if(finger!=null)hand.rotation=Quaternion.FromToRotation(finger.position-hand.position,direction)*hand.rotation;
        }
    }
    private static void Solve(Animator a,HumanBodyBones upperId,HumanBodyBones lowerId,HumanBodyBones handId,Vector3 target,Vector3 pole)
    {
        var upper=a.GetBoneTransform(upperId);var lower=a.GetBoneTransform(lowerId);var hand=a.GetBoneTransform(handId);
        Vector3 start=upper.position;float l1=Vector3.Distance(start,lower.position),l2=Vector3.Distance(lower.position,hand.position);
        Vector3 dir=(target-start).normalized;float d=Mathf.Clamp(Vector3.Distance(start,target),.01f,l1+l2-.005f);
        float along=(l1*l1+d*d-l2*l2)/(2*d),h=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
        Vector3 bend=Vector3.ProjectOnPlane(pole-start,dir).normalized;
        Vector3 elbow=start+dir*along+bend*h;
        upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,start+dir*d-lower.position)*lower.rotation;
    }
}
```


## ImportedWeaponSetup

Full file path: D:/GV project/Assets/Editor/Art/ImportedWeaponSetup.cs

```csharp
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
```


## InfectedLocomotionSetup

Full file path: D:/GV project/Assets/Editor/Art/InfectedLocomotionSetup.cs

```csharp
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

/// <summary>Authors infected locomotion on the corrected zombie Avatar. Navigation owns translation.</summary>
public static class InfectedLocomotionSetup
{
    public const string Root = "Assets/Animations/InfectedLocomotion";
    private const string Grounded = "Assets/Animations/GroundedEnemies";
    private static readonly string[] Kinds = { "Hunter", "Runner", "Ambusher", "Elite" };

    [MenuItem("Tools/Dead Zone/Author infected locomotion")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode first.");
        Phase2AnimationAssets.Folder(Root);
        var nativeWalk = GroundedZombieActions.Bake("InfectedShambleSource",
            ImportedCharacterMotion.ImportClip(ImportedCharacterSetup.Zombie), Root);
        Loop(nativeWalk);
        var report = new System.Text.StringBuilder("Infected animation authoring measurements (not a gameplay test)\n");
        var controllers = new Dictionary<string, RuntimeAnimatorController>();
        foreach (string kind in Kinds)
        {
            var idle = Author(kind, "Idle", AssetDatabase.LoadAssetAtPath<AnimationClip>(Grounded + "/GroundedIdle.anim"));
            var walk = Author(kind, "Shamble", nativeWalk);
            var chase = Author(kind, "Chase", AssetDatabase.LoadAssetAtPath<AnimationClip>(Grounded + "/GroundedChase.anim"));
            RefineContact(idle, .008f, kind); RefineContact(walk, .025f, kind); RefineContact(chase, .06f, kind);
            var previous = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Grounded + "/" + kind + ".overrideController");
            string controllerPath = Root + "/" + kind + ".controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) == null)
                AssetDatabase.CopyAsset(Grounded + "/GroundedEnemies.controller", controllerPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            var states = controller.layers[0].stateMachine.states;
            var locomotion = states.First(s => s.state.name == "Locomotion").state;
            var run = states.First(s => s.state.name == "Chase").state;
            // Elite advance and Ambusher stalking remain a shamble; their rushes use Chase.
            float enter = kind == "Elite" ? 3.2f : kind == "Ambusher" ? 3.35f : 2.7f;
            foreach (var transition in locomotion.transitions.Where(t => t.destinationState == run))
                SetThreshold(transition, enter);
            foreach (var transition in run.transitions.Where(t => t.destinationState == locomotion))
                SetThreshold(transition, enter - .25f);
            EditorUtility.SetDirty(controller);
            string overridePath = Root + "/" + kind + ".overrideController";
            var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if (overrides == null) { overrides = new AnimatorOverrideController(controller); AssetDatabase.CreateAsset(overrides, overridePath); }
            overrides.runtimeAnimatorController = controller;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); previous.GetOverrides(pairs);
            overrides.ApplyOverrides(pairs); // Existing attack, heavy, hit and death remain intact.
            overrides["GroundedIdle"] = idle;
            overrides["GroundedWalk"] = walk;
            overrides["GroundedChase"] = chase;
            EditorUtility.SetDirty(overrides);
            controllers[kind] = overrides;
            float walkSpeed = MeasureStride(walk), chaseSpeed = MeasureStride(chase);
            report.AppendLine(kind + ": shamble stride=" + walkSpeed.ToString("F3") + " m/s; chase=" + chaseSpeed.ToString("F3") + " m/s; chase threshold=" + enter);
            Assign("Assets/Prefabs/Enemies/" + kind + ".prefab", overrides, walkSpeed, chaseSpeed);
            if (kind == "Hunter") Assign("Assets/Prefabs/Enemies/EnemyBase.prefab", overrides, walkSpeed, chaseSpeed);
        }
        // Existing scene actors are prefab instances; explicit references are updated too.
        foreach (var actor in Object.FindObjectsByType<EnemyActor>())
        {
            string kind = Kinds.FirstOrDefault(k => actor.name.StartsWith(k, StringComparison.Ordinal));
            if (kind == null) continue;
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator != null) animator.runtimeAnimatorController = controllers[kind];
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/InfectedMotionAuthoring.txt", report.ToString());
    }

    private static void SetThreshold(AnimatorStateTransition transition, float value)
    {
        var conditions = transition.conditions;
        for (int i = 0; i < conditions.Length; i++) if (conditions[i].parameter == "Speed") conditions[i].threshold = value;
        transition.conditions = conditions; transition.duration = .16f; EditorUtility.SetDirty(transition);
    }

    private static void Assign(string path, RuntimeAnimatorController controller, float walk, float chase)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var animator = root.GetComponentInChildren<Animator>(); animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // The authored infected torso replaces the shared survivor-style breathing overlay.
            var polish = animator.GetComponent<CharacterMotionPolish>(); if (polish != null) polish.enabled = false;
            OutbreakEditor.Values(root.GetComponent<EnemyAnimationController>(), s => {
                s.FindProperty("walkStrideSpeed").floatValue = walk;
                s.FindProperty("chaseStrideSpeed").floatValue = chase;
                s.FindProperty("randomizeLocomotionPhase").boolValue = true;
            });
            // A short dragging stride cannot cover a sprint distance without sliding
            // or comically fast playback. Keep rushes fast and make travel a shamble.
            var hunter = root.GetComponent<HunterBrain>();
            if (hunter != null) OutbreakEditor.Values(hunter, s => s.FindProperty("speed").floatValue = 1.25f);
            var runner = root.GetComponent<RunnerBrain>();
            if (runner != null) OutbreakEditor.Values(runner, s => s.FindProperty("cautionSpeed").floatValue = 1.5f);
            var ambusher = root.GetComponent<AmbusherBrain>();
            if (ambusher != null) OutbreakEditor.Values(ambusher, s => s.FindProperty("travelSpeed").floatValue = 1.5f);
            var elite = root.GetComponent<EliteBrain>();
            if (elite != null) OutbreakEditor.Values(elite, s => s.FindProperty("advanceSpeed").floatValue = 1.45f);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static AnimationClip Author(string kind, string mode, AnimationClip source)
    {
        string path = Root + "/" + kind + "_" + mode + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = Object.Instantiate(source); AssetDatabase.CreateAsset(clip, path); }
        else EditorUtility.CopySerialized(source, clip);
        clip.name = kind + "_" + mode;
        var sample = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Grounded + "/CanonicalZombie.prefab"));
        var graph = PlayableGraph.Create("Infected pose authoring");
        try
        {
            var animator = sample.GetComponent<Animator>(); animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            var playable = AnimationClipPlayable.Create(graph, source); playable.SetApplyFootIK(false); output.SetSourcePlayable(playable);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); graph.Play();
            // HumanPose expresses muscles relative to its body frame. Changing the
            // torso changes that frame: record the complete pose so the evaluated
            // legs remain where the source animation placed them.
            var properties = HumanTrait.MuscleName.Select(Property).Concat(new[] { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" }).ToArray();
            var curves = properties.Select(_ => new AnimationCurve()).ToArray();
            using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
            {
                int frames = Mathf.CeilToInt(source.length * 60);
                for (int f = 0; f <= frames; f++)
                {
                    float phase = f / (float)frames;
                    playable.SetTime((phase >= 1 ? 0 : phase) * source.length); graph.Evaluate(0);
                    Pose(animator, kind, mode, phase);
                    var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                    for (int j = 0; j < HumanTrait.MuscleCount; j++)
                    {
                        float v = pose.muscles[j];
                        if (HumanTrait.MuscleName[j].Contains("Stretched")) v = -.35f - .10f * Mathf.Sin(phase * Mathf.PI * 2);
                        curves[j].AddKey(phase * source.length, v);
                    }
                    for (int j = 0; j < 3; j++) curves[HumanTrait.MuscleCount + j].AddKey(phase * source.length, pose.bodyPosition[j]);
                    for (int j = 0; j < 4; j++) curves[HumanTrait.MuscleCount + 3 + j].AddKey(phase * source.length, pose.bodyRotation[j]);
                }
            }
            clip.ClearCurves();
            for (int j = 0; j < properties.Length; j++)
            {
                for (int k = 0; k < curves[j].length; k++) curves[j].SmoothTangents(k, 0);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), properties[j]), curves[j]);
            }
            Loop(clip); EditorUtility.SetDirty(clip); return clip;
        }
        finally { graph.Destroy(); Object.DestroyImmediate(sample); }
    }

    private static void RefineContact(AnimationClip clip, float flightLimit, string kind)
    {
        // The Elite has a taller, nonuniform visual scale: calibrate each actual
        // prefab mesh rather than assuming that canonical-height contact transfers.
        var sample = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + kind + ".prefab"));
        sample.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (var behaviour in sample.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
        var graph = PlayableGraph.Create("Infected sole calibration"); var mesh = new Mesh();
        try
        {
            var a = sample.GetComponentInChildren<Animator>(); a.runtimeAnimatorController = null;
            var output = AnimationPlayableOutput.Create(graph, "Pose", a);
            var playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyFootIK(false); output.SetSourcePlayable(playable);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); graph.Play();
            var skins = a.GetComponentsInChildren<SkinnedMeshRenderer>();
            var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y");
            for (int pass = 0; pass < 2; pass++)
            {
                var previous = AnimationUtility.GetEditorCurve(clip, binding); var curve = new AnimationCurve();
                int steps = Mathf.CeilToInt(clip.length * 60);
                for (int i = 0; i <= steps; i++)
                {
                    float time = i * clip.length / steps; playable.SetTime(time); graph.Evaluate(0);
                    float low = float.PositiveInfinity;
                    foreach (var skin in skins)
                    {
                        skin.BakeMesh(mesh);
                        foreach (var v in mesh.vertices) low = Mathf.Min(low, skin.transform.TransformPoint(v).y);
                    }
                    curve.AddKey(time, previous.Evaluate(time) + (Mathf.Clamp(low, .004f, flightLimit) - low) / (a.humanScale * a.transform.lossyScale.y));
                }
                var keys = curve.keys; keys[keys.Length - 1].value = keys[0].value; curve.keys = keys;
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(clip, binding, curve);
                graph.DestroyPlayable(playable); playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false); output.SetSourcePlayable(playable);
            }
            EditorUtility.SetDirty(clip);
        }
        finally { graph.Destroy(); Object.DestroyImmediate(mesh); Object.DestroyImmediate(sample); }
    }

    private static string Property(string name)
    {
        foreach (string side in new[] { "Left", "Right" }) foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
        { string prefix = side + " " + finger + " "; if (name.StartsWith(prefix)) return side + "Hand." + finger + "." + name.Substring(prefix.Length); }
        return name;
    }
    private static void Loop(AnimationClip clip)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; settings.loopBlend = true;
        // These are explicit HumanPose body curves, not imported FBX root tracks.
        // Feet-based root extraction would subtract body height again in a controller.
        settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true; settings.heightFromFeet = false;
        settings.loopBlendOrientation = true; settings.loopBlendPositionY = true; settings.loopBlendPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
    }

    private static void Pose(Animator a, string kind, string mode, float t)
    {
        var root = a.transform; float p = t * Mathf.PI * 2;
        bool runner = kind == "Runner", ambusher = kind == "Ambusher", elite = kind == "Elite", idle = mode == "Idle";
        float uneven = Mathf.Sin(p) * .7f + Mathf.Sin(p * 2 + .7f) * .3f;
        float lean = runner ? 19 : ambusher ? 24 : elite ? 11 : 16;
        if (idle) lean *= .8f;
        Rotate(a, HumanBodyBones.Spine, root.right, lean * .6f + uneven * 1.4f);
        Rotate(a, HumanBodyBones.Chest, root.right, lean * .4f);
        Rotate(a, HumanBodyBones.Chest, root.forward, (elite ? -3 : 4) + uneven * (runner ? 1.8f : 3));
        Rotate(a, HumanBodyBones.Head, root.right, -lean * .42f);
        Rotate(a, HumanBodyBones.Head, root.forward, (ambusher ? -7 : 5) + Mathf.Sin(p + .8f) * 2);
        Rotate(a, HumanBodyBones.Head, root.up, Mathf.Sin(p + .3f) * (idle ? 6 : 3));
        // Asymmetric reaching and dragging replaces the healthy reciprocal arm pump.
        float motion = idle ? .25f : 1;
        Arm(a, true, new Vector3(-.10f, runner ? -.23f : ambusher ? -.32f : -.36f,
            runner ? .34f : ambusher ? .30f : elite ? .13f : .25f) + new Vector3(0, uneven * .035f * motion, Mathf.Sin(p + .6f) * .045f * motion));
        Arm(a, false, new Vector3(.10f, runner ? -.31f : ambusher ? -.42f : -.53f,
            runner ? .22f : ambusher ? .20f : elite ? .06f : .09f) + new Vector3(0, Mathf.Sin(p + 1.3f) * .025f * motion, uneven * .035f * motion));
        Rotate(a, HumanBodyBones.LeftHand, root.right, 17 + uneven * 4);
        Rotate(a, HumanBodyBones.RightHand, root.right, 25 - uneven * 3);
    }
    private static void Rotate(Animator a, HumanBodyBones bone, Vector3 axis, float degrees)
    { var t = a.GetBoneTransform(bone); if (t != null) t.rotation = Quaternion.AngleAxis(degrees, axis) * t.rotation; }
    private static void Arm(Animator a, bool left, Vector3 offset)
    {
        var upper = a.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        var lower = a.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        var hand = a.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        Vector3 start = upper.position, target = start + a.transform.TransformDirection(offset);
        float l1 = Vector3.Distance(start, lower.position), l2 = Vector3.Distance(lower.position, hand.position);
        float distance = Mathf.Clamp(Vector3.Distance(start, target), .08f, (l1 + l2) * .96f);
        Vector3 direction = (target - start).normalized;
        float along = (l1 * l1 + distance * distance - l2 * l2) / (2 * distance);
        float height = Mathf.Sqrt(Mathf.Max(0, l1 * l1 - along * along));
        Vector3 pole = a.transform.TransformDirection(new Vector3(left ? -.5f : .5f, -.2f, -.15f));
        Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
        Vector3 elbow = start + direction * along + bend * height;
        upper.rotation = Quaternion.FromToRotation(lower.position - start, elbow - start) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, start + direction * distance - lower.position) * lower.rotation;
    }

    private static float MeasureStride(AnimationClip clip)
    {
        var sample = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Grounded + "/CanonicalZombie.prefab"));
        var controller = new AnimatorOverrideController(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Grounded + "/GroundedEnemies.controller"));
        try
        {
            controller["GroundedChase"] = clip; var a = sample.GetComponent<Animator>(); a.runtimeAnimatorController = controller;
            a.Rebind(); a.SetFloat("Speed", 4); a.SetFloat("GaitScale", 1);
            var points = new Vector3[2, 120]; var speeds = new List<float>();
            for (int i = 0; i < 120; i++)
            {
                a.Play("Chase", 0, i / 120f); a.Update(0);
                points[0, i] = a.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                points[1, i] = a.GetBoneTransform(HumanBodyBones.RightFoot).position;
            }
            for (int foot = 0; foot < 2; foot++)
            {
                float low = Enumerable.Range(0, 120).Min(i => points[foot, i].y);
                for (int i = 1; i < 119; i++) if (points[foot, i].y < low + .05f)
                {
                    float v = -(points[foot, i].z - points[foot, i - 1].z) / (clip.length / 120);
                    if (v > .05f) speeds.Add(v);
                }
            }
            if (speeds.Count < 4) throw new InvalidOperationException("Cannot calibrate foot travel: " + clip.name);
            speeds.Sort(); return speeds[speeds.Count / 2];
        }
        finally { Object.DestroyImmediate(sample); Object.DestroyImmediate(controller); }
    }
}
```


## InfectedMotionPreview

Full file path: D:/GV project/Assets/Editor/Art/InfectedMotionPreview.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Renders authored enemy motion in Edit Mode without running the game.</summary>
public static class InfectedMotionPreview
{
    private const int Width = 1400;
    private const int Height = 750;
    private const int FrameCount = 24;
    private const int PreviewLayer = 31;
    private const string OutputDirectory = "Previews/InfectedMotion";

    private sealed class Subject
    {
        public string Name;
        public Animator Animator;
        public SkinnedMeshRenderer[] Skins;
        public Mesh[] Baked;
    }

    [MenuItem("Zombie Outbreak/Art/Capture infected motion preview")]
    public static void Capture()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play Mode before capturing the authored motion preview.");

        Directory.CreateDirectory(OutputDirectory);
        var existingLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var lightFlags = existingLights.Select(light => light.enabled).ToArray();
        bool oldFog = RenderSettings.fog;
        AmbientMode oldAmbientMode = RenderSettings.ambientMode;
        Color oldAmbient = RenderSettings.ambientLight;
        float oldReflectionIntensity = RenderSettings.reflectionIntensity;
        RenderTexture oldActive = RenderTexture.active;
        var objects = new List<GameObject>();
        var meshes = new List<Mesh>();
        var vertices = new List<Vector3>();
        var bounds = new StringBuilder("Authored pose geometry reference; not a gameplay test.\n" +
            "Lowest baked skin vertex relative to the studio floor (metres). No contact assertions.\n" +
            "Mode,Frame,Enemy,LowestSkinYAboveFloor\n");
        Material floorMaterial = null;
        RenderTexture target = null;
        Texture2D pixels = null;
        Camera camera = null;
        try
        {
            foreach (var light in existingLights)
                light.enabled = false;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.40f, .42f, .44f);
            RenderSettings.reflectionIntensity = 0;

            // The dedicated layer keeps the gameplay map out of this studio render.
            var stage = new GameObject("Temporary infected motion studio");
            objects.Add(stage);
            stage.hideFlags = HideFlags.HideAndDontSave;
            stage.transform.position = new Vector3(10000, 0, 10000);
            AddLight(stage.transform, "Studio key", new Vector3(38, 150, 0), 1.1f, true);
            AddLight(stage.transform, "Studio fill", new Vector3(28, -65, 0), .42f, false);

            floorMaterial = new Material(Shader.Find("Standard"));
            floorMaterial.hideFlags = HideFlags.HideAndDontSave;
            floorMaterial.color = new Color(.16f, .18f, .19f);
            floorMaterial.SetFloat("_Glossiness", .08f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Studio floor";
            floor.transform.SetParent(stage.transform, false);
            floor.transform.localPosition = new Vector3(0, -.04f, 0);
            floor.transform.localScale = new Vector3(12, .08f, 8);
            floor.layer = PreviewLayer;
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            camera = new GameObject("Studio camera").AddComponent<Camera>();
            camera.transform.SetParent(stage.transform, false);
            camera.transform.localPosition = new Vector3(0, 2.05f, 9.5f);
            camera.transform.LookAt(stage.transform.position + new Vector3(0, 1.0f, 0));
            camera.enabled = false;
            camera.cullingMask = 1 << PreviewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.105f, .12f, .13f);
            camera.orthographic = true;
            camera.orthographicSize = 1.9f;
            camera.aspect = Width / (float)Height;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 30;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            string[] kinds = { "Hunter", "Runner", "Ambusher", "Elite" };
            var subjects = new Subject[kinds.Length];
            for (int index = 0; index < kinds.Length; index++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + kinds[index] + ".prefab");
                if (prefab == null)
                    throw new InvalidOperationException("Missing enemy prefab: " + kinds[index]);
                var instance = Object.Instantiate(prefab, stage.transform);
                instance.name = kinds[index] + " preview";
                foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                    behaviour.enabled = false;
                foreach (var agent in instance.GetComponentsInChildren<NavMeshAgent>(true))
                    agent.enabled = false;
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                foreach (var node in instance.GetComponentsInChildren<Transform>(true))
                    node.gameObject.layer = PreviewLayer;

                // Camera faces -Z: decreasing world X reads left-to-right in the image.
                instance.transform.localPosition = new Vector3((1.5f - index) * 1.65f, 0, 0);
                instance.transform.localRotation = Quaternion.Euler(0, -28, 0);
                var animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.runtimeAnimatorController == null)
                    throw new InvalidOperationException("Enemy needs a configured Animator: " + kinds[index]);
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.SetBool("Dead", false);
                animator.SetFloat("GaitScale", 1);
                var subject = new Subject { Name = kinds[index], Animator = animator, Skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true) };
                subject.Baked = new Mesh[subject.Skins.Length];
                for (int skinIndex = 0; skinIndex < subject.Skins.Length; skinIndex++)
                {
                    var skin = subject.Skins[skinIndex];
                    var mesh = new Mesh { name = kinds[index] + " baked preview", hideFlags = HideFlags.HideAndDontSave };
                    meshes.Add(mesh);
                    subject.Baked[skinIndex] = mesh;
                    var rendered = new GameObject("Baked preview skin", typeof(MeshFilter), typeof(MeshRenderer));
                    rendered.transform.SetParent(skin.transform, false);
                    rendered.layer = PreviewLayer;
                    rendered.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = rendered.GetComponent<MeshRenderer>();
                    renderer.sharedMaterials = skin.sharedMaterials;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    skin.enabled = false;
                }
                subjects[index] = subject;
            }

            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create();
            pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            foreach (string mode in new[] { "Walk", "Chase" })
            {
                int state = Animator.StringToHash("Base Layer." + (mode == "Walk" ? "Locomotion" : "Chase"));
                foreach (var subject in subjects)
                {
                    if (!subject.Animator.HasState(0, state))
                        throw new InvalidOperationException("Missing preview state: " + mode);
                    subject.Animator.SetFloat("Speed", mode == "Walk" ? 1 : 5);
                    subject.Animator.SetFloat("GaitScale", 1);
                }
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    foreach (var subject in subjects)
                    {
                        subject.Animator.Play(state, 0, frame / (float)FrameCount);
                        subject.Animator.Update(0);
                        float lowest = float.PositiveInfinity;
                        for (int skinIndex = 0; skinIndex < subject.Skins.Length; skinIndex++)
                        {
                            var skin = subject.Skins[skinIndex];
                            var mesh = subject.Baked[skinIndex];
                            skin.BakeMesh(mesh);
                            mesh.RecalculateBounds();
                            mesh.GetVertices(vertices);
                            foreach (var vertex in vertices)
                                lowest = Mathf.Min(lowest, skin.transform.TransformPoint(vertex).y - stage.transform.position.y);
                        }
                        bounds.Append(mode).Append(',').Append(frame.ToString("00")).Append(',').Append(subject.Name).Append(',')
                            .AppendLine(float.IsPositiveInfinity(lowest) ? "no skin vertices" : lowest.ToString("F6", CultureInfo.InvariantCulture));
                    }
                    // Warm newly uploaded baked meshes and shadows before recording each cycle.
                    if (frame == 0)
                        camera.Render();
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(OutputDirectory + "/" + mode + "_" + frame.ToString("00") + ".png", pixels.EncodeToPNG());
                }
            }
            File.WriteAllText(OutputDirectory + "/README.txt",
                "Edit Mode animation preview; no gameplay simulation.\n" +
                "Left to right: Hunter, Runner, Ambusher, Elite.\n" +
                "Walk_00..23 and Chase_00..23 each sample one normalized animation cycle.\n" +
                "These sheets show posture and motion; they do not demonstrate runtime travel speed or NavMesh foot contact.\n");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/InfectedPreviewBounds.txt", bounds.ToString());
        }
        finally
        {
            if (camera != null)
                camera.targetTexture = null;
            RenderTexture.active = oldActive;
            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }
            if (pixels != null)
                Object.DestroyImmediate(pixels);
            foreach (var instance in objects)
                if (instance != null)
                    Object.DestroyImmediate(instance);
            foreach (var mesh in meshes)
                if (mesh != null)
                    Object.DestroyImmediate(mesh);
            if (floorMaterial != null)
                Object.DestroyImmediate(floorMaterial);
            for (int index = 0; index < existingLights.Length; index++)
                if (existingLights[index] != null)
                    existingLights[index].enabled = lightFlags[index];
            RenderSettings.fog = oldFog;
            RenderSettings.ambientMode = oldAmbientMode;
            RenderSettings.ambientLight = oldAmbient;
            RenderSettings.reflectionIntensity = oldReflectionIntensity;
        }
    }

    private static void AddLight(Transform parent, string name, Vector3 rotation, float intensity, bool shadows)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.rotation = Quaternion.Euler(rotation);
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = Color.white;
        light.cullingMask = 1 << PreviewLayer;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        light.shadowStrength = .65f;
        light.shadowBias = .025f;
        light.shadowNormalBias = .08f;
    }
}
```


## LegAndGaitRepair

Full file path: D:/GV project/Assets/Editor/Art/LegAndGaitRepair.cs

```csharp
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Repairs foot IK and replaces conflicting enemy gait blends with speed-matched states.</summary>
public static class LegAndGaitRepair
{
    private const string Root = "Assets/Animations/GroundedEnemies";
    [MenuItem("Zombie Outbreak/Art/Repair player feet and zombie locomotion")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        EditorSceneManager.SaveOpenScenes();
        Phase2AnimationAssets.Folder(Root);
        GroundedZombieRig.Build();
        var walk = Copy("GroundedWalk", AssetDatabase.LoadAssetAtPath<AnimationClip>(ImportedCharacterSetup.MotionRoot + "/Walk.anim"));
        var idle = Copy("GroundedIdle", AssetDatabase.LoadAssetAtPath<AnimationClip>(ImportedCharacterSetup.MotionRoot + "/Idle.anim"));
        var chase = Copy("GroundedChase", AssetDatabase.LoadAssetAtPath<AnimationClip>(ImportedCharacterSetup.MotionRoot + "/Run.anim"));
        foreach (var clip in new[] { idle, walk, chase })
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true; settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }
        string path = Root + "/GroundedEnemies.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) == null)
            AssetDatabase.CopyAsset(ImportedCharacterSetup.MotionRoot + "/Infected.controller", path);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (!controller.parameters.Any(p => p.name == "GaitScale"))
            controller.AddParameter(new AnimatorControllerParameter { name = "GaitScale", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        var machine = controller.layers[0].stateMachine;
        var slow = machine.states.First(s => s.state.name == "Locomotion").state;
        var tree = AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>().FirstOrDefault(t => t.name == "Grounded slow gait");
        if (tree == null) { tree = new BlendTree { name = "Grounded slow gait" }; AssetDatabase.AddObjectToAsset(tree, controller); }
        tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
        tree.children = new[] { new ChildMotion { motion = idle, threshold = 0, timeScale = 1 }, new ChildMotion { motion = walk, threshold = .08f, timeScale = 1 } };
        slow.motion = tree; slow.speed = 1; slow.speedParameter = "GaitScale"; slow.speedParameterActive = true;
        var run = machine.states.FirstOrDefault(s => s.state.name == "Chase").state;
        if (run == null) run = machine.AddState("Chase");
        run.motion = chase; run.speed = 1; run.speedParameter = "GaitScale"; run.speedParameterActive = true;
        foreach (var transition in slow.transitions.Where(t => t.destinationState == run).ToArray()) slow.RemoveTransition(transition);
        foreach (var transition in run.transitions.Where(t => t.destinationState == slow).ToArray()) run.RemoveTransition(transition);
        Link(slow, run, AnimatorConditionMode.Greater, 2.7f);
        Link(run, slow, AnimatorConditionMode.Less, 2.4f);
        EditorUtility.SetDirty(tree); EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        var attack = GroundedZombieActions.Bake("GroundedAttack", ImportedCharacterMotion.ImportClip(ImportedCharacterSetup.Root + "/PixelhouseZombie/fury.FBX"));
        var death = GroundedZombieActions.Bake("GroundedDeath", ImportedCharacterMotion.ImportClip(ImportedCharacterSetup.Root + "/PixelhouseZombie/dead.FBX"));

        // Measure on the actual target skeleton, not the source character's height.
        var sample = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/CanonicalZombie.prefab"));
        float chaseSpeed, walkSpeed;
        try
        {
            var a = sample.GetComponentInChildren<Animator>(); a.runtimeAnimatorController = controller;
            chaseSpeed = Measure(a, "Chase");
            walkSpeed = Measure(a, "Locomotion");
        }
        finally { Object.DestroyImmediate(sample); }
        File.WriteAllText("Logs/GaitCalibration.txt", "Walk stance speed: " + walkSpeed + " m/s\nChase stance speed: " + chaseSpeed + " m/s\n");

        foreach (string kind in new[] { "EnemyBase", "Hunter", "Runner", "Ambusher", "Elite" })
        {
            string sourceName = kind == "EnemyBase" ? "Hunter" : kind;
            var previous = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ImportedCharacterSetup.MotionRoot + "/" + sourceName + ".overrideController");
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); previous.GetOverrides(pairs);
            string overridePath = Root + "/" + sourceName + ".overrideController";
            var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if (overrides == null) { overrides = new AnimatorOverrideController(controller); AssetDatabase.CreateAsset(overrides, overridePath); }
            overrides.runtimeAnimatorController = controller;
            foreach (var clip in controller.animationClips.Distinct())
            {
                var pair = pairs.FirstOrDefault(p => p.Key.name == clip.name);
                if (pair.Value != null) overrides[clip.name] = pair.Value;
            }
            overrides["ZombieAttack"] = attack; overrides["EliteHeavy"] = attack; overrides["ZombieDeath"] = death;
            EditorUtility.SetDirty(overrides);
            Edit("Assets/Prefabs/Enemies/" + kind + ".prefab", root =>
            {
                var old = root.GetComponentInChildren<Animator>(true);
                var materials = new[] { AssetDatabase.LoadAssetAtPath<Material>(ImportedCharacterSetup.Root + "/Materials/Infected_" + kind + ".mat") };
                var scale = kind == "Elite" ? new Vector3(1.17f,1.12f,1.17f) : kind == "Runner" ? new Vector3(.94f,1,.94f) : Vector3.one;
                // Base-prefab replacements can introduce a new inherited Visual beside
                // a variant's old override. Remove every prior rig before installing one.
                foreach (var prior in root.GetComponentsInChildren<Animator>(true))
                    if (prior != null) Object.DestroyImmediate(prior.gameObject);
                var visual = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/CanonicalZombie.prefab"), root.transform);
                visual.name = "Visual"; visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity; visual.transform.localScale = scale;
                foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;
                foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.sharedMaterials = materials;
                var a = visual.GetComponent<Animator>(); a.runtimeAnimatorController = overrides;
                OutbreakEditor.Set(root.GetComponent<EnemyAnimationController>(), "animator", a);
                // Prototype clothing was parented to the gameplay root and slid along the road.
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t != null && (t.name == "SurfaceBelt" || t.name == "SurfaceEquipment" || t.name == "KneeProtection" || t.name == "BootSole"))
                        Object.DestroyImmediate(t.gameObject);
                a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                OutbreakEditor.Values(root.GetComponent<EnemyAnimationController>(), s =>
                {
                    s.FindProperty("walkStrideSpeed").floatValue = walkSpeed;
                    s.FindProperty("chaseStrideSpeed").floatValue = chaseSpeed;
                    s.FindProperty("visualStrideScale").floatValue = Mathf.Abs(a.transform.localScale.x);
                });
            });
        }
        Edit("Assets/Prefabs/Player/PlayerPhase3.prefab", ConfigureFeet);
        OutbreakEditor.Open();
        ConfigureFeet(Object.FindAnyObjectByType<ThirdPersonPlayerController>().gameObject);
        OutbreakEditor.Save("LEG_AND_GAIT_REPAIR");
    }

    private static void ConfigureFeet(GameObject root)
    {
        var feet = root.GetComponentInChildren<HumanoidFootPlacement>(true);
        if (feet == null) return;
        OutbreakEditor.Values(feet, s => { s.FindProperty("weight").floatValue = .65f; s.FindProperty("maximumCorrection").floatValue = .08f; s.FindProperty("groundLayers").intValue = 1 << 8; });
    }

    private static float Measure(Animator a, string state)
    {
        a.Rebind(); a.SetFloat("GaitScale", 1); a.SetFloat("Speed", state == "Chase" ? 3 : .8f); a.Play(state, 0, 0); a.Update(.001f);
        float duration = a.GetCurrentAnimatorStateInfo(0).length;
        var points = new Vector3[2, 121];
        for (int f = 0; f <= 120; f++)
        {
            a.Play(state, 0, f / 120f); a.Update(0);
            points[0, f] = a.GetBoneTransform(HumanBodyBones.LeftFoot).position;
            points[1, f] = a.GetBoneTransform(HumanBodyBones.RightFoot).position;
        }
        var speeds = new List<float>();
        for (int foot = 0; foot < 2; foot++)
        {
            float low = Enumerable.Range(0, 121).Min(f => points[foot, f].y);
            for (int f = 1; f < 120; f++) if (points[foot, f].y < low + .05f)
            {
                float v = Vector3.Dot(points[foot, f] - points[foot, f - 1], a.transform.forward) / (duration / 120f);
                if (v < -.05f) speeds.Add(-v);
            }
        }
        if (speeds.Count < 4) throw new InvalidOperationException("Unable to measure chase foot travel.");
        speeds.Sort(); return speeds[speeds.Count / 2];
    }
    private static AnimationClip Copy(string name, AnimationClip source)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/" + name + ".anim");
        if (clip == null) { clip = Object.Instantiate(source); AssetDatabase.CreateAsset(clip, Root + "/" + name + ".anim"); }
        else EditorUtility.CopySerialized(source, clip);
        clip.name = name; return clip;
    }
    private static void Link(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float speed)
    {
        var t = from.AddTransition(to); t.hasExitTime = false; t.hasFixedDuration = true; t.duration = .12f;
        t.AddCondition(mode, speed, "Speed"); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
    }
    private static void Edit(string path, Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
```


## PlayerInPlaceMotion

Full file path: D:/GV project/Assets/Editor/Art/PlayerInPlaceMotion.cs

```csharp
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>Preserves native Humanoid motion/IK tracks while removing travel for the CharacterController.</summary>
public static class PlayerInPlaceMotion
{
    public static AnimationClip Create(string name, string sourceName, bool grounded)
    {
        string sourcePath = PlayerSourceMotionImport.Root + "/m@" + sourceName + ".fbx";
        var source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview"));
        string path = "Assets/Animations/PlayerGrounded/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = Object.Instantiate(source); AssetDatabase.CreateAsset(clip, path); }
        else EditorUtility.CopySerialized(source, clip);
        clip.name = name;
        // Bake Into Pose alone leaves the travelling body curve in the clip. As
        // the body yaws, that metre-scale travel can become sideways motion.
        // Remove the straight trajectory before evaluating/retargeting the pose.
        foreach (string axis in new[] { "x", "z" })
        {
            var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT." + axis);
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            float first = curve.Evaluate(0), slope = (curve.Evaluate(clip.length) - first) / clip.length;
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value -= first + slope * keys[i].time;
                keys[i].inTangent -= slope;
                keys[i].outTangent -= slope;
            }
            curve.keys = keys;
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true; settings.loopBlend = true;
        settings.loopBlendOrientation = true;
        settings.loopBlendPositionY = true; settings.loopBlendPositionXZ = true;
        settings.keepOriginalPositionXZ = true; settings.keepOriginalPositionY = true;
        settings.keepOriginalOrientation = true; settings.heightFromFeet = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        FitBootContact(clip, grounded);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void FitBootContact(AnimationClip clip, bool grounded)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerPhase3.prefab"));
        foreach (var b in go.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var animator = go.GetComponentInChildren<Animator>();
        animator.transform.localPosition = Vector3.zero;
        animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var graph = PlayableGraph.Create("Fit player boot contact");
        var mesh = new Mesh();
        try
        {
            var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); graph.Play();
            var boots = animator.GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.name.Contains("Boots"));
            bool[] left = boots.sharedMesh.boneWeights.Select(w => boots.bones[w.boneIndex0].name.EndsWith("_l")).ToArray();
            int steps = Mathf.CeilToInt(clip.length * 120);
            var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y");
            // Correct boot thickness through body height only. The original knee,
            // ankle, toe and IK-goal tracks remain intact. Preserve airborne run poses.
            for (int pass = 0; pass < 3; pass++)
            {
                var p = AnimationClipPlayable.Create(graph, clip); p.SetApplyFootIK(false);
                output.SetSourcePlayable(p);
                var previous = AnimationUtility.GetEditorCurve(clip, binding);
                var height = new AnimationCurve();
                var contact = new[] { new AnimationCurve(), new AnimationCurve() };
                for (int f = 0; f < steps; f++)
                {
                    float time = f * clip.length / steps;
                    p.SetTime(time); graph.Evaluate(0); boots.BakeMesh(mesh);
                    float[] sole = { float.PositiveInfinity, float.PositiveInfinity };
                    var vertices = mesh.vertices;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        int side = left[v] ? 0 : 1;
                        sole[side] = Mathf.Min(sole[side], boots.transform.TransformPoint(vertices[v]).y);
                    }
                    float lowest = Mathf.Min(sole[0], sole[1]);
                    float correction = (grounded ? .004f : Mathf.Max(.004f, lowest)) - lowest;
                    height.AddKey(time, previous.Evaluate(time) + correction / animator.humanScale);
                    for (int side = 0; side < 2; side++)
                        contact[side].AddKey(time, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.015f, .065f, sole[side] + correction)));
                }
                CloseCurve(height, clip.length);
                if (pass < 2) AnimationUtility.SetEditorCurve(clip, binding, height);
                else
                {
                    for (int side = 0; side < 2; side++)
                    {
                        CloseCurve(contact[side], clip.length);
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), side == 0 ? "LeftFootContact" : "RightFootContact"), contact[side]);
                    }
                }
                graph.DestroyPlayable(p);
            }
        }
        finally { graph.Destroy(); Object.DestroyImmediate(mesh); Object.DestroyImmediate(go); }
    }

    static void CloseCurve(AnimationCurve curve, float duration)
    {
        curve.AddKey(duration, curve.keys[0].value);
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
    }
}
```


## PlayerLocomotionRepair

Full file path: D:/GV project/Assets/Editor/Art/PlayerLocomotionRepair.cs

```csharp
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

/// <summary>Builds consistent target-rig locomotion with measured boot contact and phase-aligned gait blending.</summary>
public static class PlayerLocomotionRepair
{
    public const string Root = "Assets/Animations/PlayerGrounded";
    [MenuItem("Tools/Dead Zone/Repair player locomotion")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        EditorSceneManager.SaveOpenScenes();Phase2AnimationAssets.Folder(Root);
        string controllerPath=Root+"/PlayerGrounded.controller";
        if(AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)==null)
            AssetDatabase.CopyAsset(SurvivalistSetup.MotionRoot+"/Survivalist.controller",controllerPath);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        foreach(string parameter in new[]{"LeftFootContact","RightFootContact"})
            if(!controller.parameters.Any(p=>p.name==parameter))controller.AddParameter(new AnimatorControllerParameter{name=parameter,type=AnimatorControllerParameterType.Float,defaultFloat=1});
        var previous=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(SurvivalistSetup.MotionRoot+"/Survivalist.overrideController");
        var walk=AssetDatabase.LoadAssetAtPath<AnimationClip>(ImportedCharacterSetup.MotionRoot+"/Walk.anim");
        var run=AssetDatabase.LoadAssetAtPath<AnimationClip>(ImportedCharacterSetup.MotionRoot+"/Run.anim");
        var idle=Bake("GroundedIdle",previous["Idle"],null,0,0);
        var walking=Bake("GroundedWalk",walk,null,0,0);
        // A restrained jog shares the walk's footfall phase, rather than blending unrelated poses.
        float phase=FindPhase(run,walk);
        var running=Bake("GroundedRun",run,walk,.25f,phase);
        var backward=Bake("GroundedBackward",previous["AimBackward"],null,0,0);
        var left=Bake("GroundedLeft",previous["AimLeft"],null,0,0);
        var right=Bake("GroundedRight",previous["AimRight"],null,0,0);
        var aimForward=Bake("GroundedAimForward",previous["AimForward"],null,0,0);
        float walkPhase=FindPhase(walking,running);
        var tree=(BlendTree)controller.layers[0].stateMachine.states.First(s=>s.state.name=="Locomotion").state.motion;
        var children=tree.children;
        float walkSpeed=MeasureStride(walking),runSpeed=MeasureStride(running);
        children[0].cycleOffset=0;children[1].cycleOffset=0;
        children[1].timeScale=1.6f/walkSpeed;children[2].timeScale=3.6f/runSpeed;children[3].timeScale=6f/runSpeed;
        children[2].cycleOffset=walkPhase;children[3].cycleOffset=walkPhase;
        tree.children=children;
        var aimTree=(BlendTree)controller.layers[0].stateMachine.states.First(s=>s.state.name=="Aim Locomotion").state.motion;
        var aimChildren=aimTree.children;
        var aimClips=new[]{idle,aimForward,backward,left,right};
        for(int i=1;i<aimChildren.Length;i++)aimChildren[i].timeScale=1.25f/MeasureStride(aimClips[i],i>=3?Vector3.right:Vector3.forward);
        aimTree.children=aimChildren;
        string overridePath=Root+"/PlayerGrounded.overrideController";
        var overrides=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
        if(overrides==null){overrides=new AnimatorOverrideController(controller);AssetDatabase.CreateAsset(overrides,overridePath);}
        overrides.runtimeAnimatorController=controller;
        var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();previous.GetOverrides(pairs);
        foreach(var pair in pairs)if(pair.Value!=null)overrides[pair.Key.name]=pair.Value;
        overrides["Idle"]=idle;overrides["AimIdle"]=idle;overrides["WalkForward"]=walking;
        overrides["Run"]=running;overrides["Sprint"]=running;
        overrides["AimForward"]=aimForward;overrides["AimBackward"]=backward;overrides["WalkBackward"]=backward;
        overrides["AimLeft"]=left;overrides["StrafeLeft"]=left;overrides["AimRight"]=right;overrides["StrafeRight"]=right;
        EditorUtility.SetDirty(tree);EditorUtility.SetDirty(aimTree);EditorUtility.SetDirty(controller);EditorUtility.SetDirty(overrides);
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player/PlayerPhase3.prefab");
        try{Configure(prefab,overrides);PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/Player/PlayerPhase3.prefab");}
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        Configure(Object.FindAnyObjectByType<ThirdPersonPlayerController>().gameObject,overrides);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/PlayerGroundedCalibration.txt","Walk stance speed="+walkSpeed+"\nJog stance speed="+runSpeed+"\nWalk contribution to jog phase="+phase+"\nJog offset in exploration tree="+walkPhase+"\n");
    }

    private static void Configure(GameObject player,RuntimeAnimatorController controller)
    {
        var a=player.GetComponentInChildren<Animator>();a.runtimeAnimatorController=controller;a.applyRootMotion=false;
        // CharacterController contact rests one skin width above the street.
        var p=a.transform.localPosition;p.y=-player.GetComponent<CharacterController>().skinWidth;a.transform.localPosition=p;
        var feet=a.GetComponent<HumanoidFootPlacement>();
        OutbreakEditor.Values(feet,s=>{s.FindProperty("weight").floatValue=.45f;s.FindProperty("maximumCorrection").floatValue=.06f;s.FindProperty("useAnimationContacts").boolValue=true;});
        OutbreakEditor.Values(player.GetComponent<PlayerAnimatorDriver>(),s=>s.FindProperty("parameterDampTime").floatValue=.025f);
    }

    private static GameObject Rig(out Animator animator)
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerPhase3.prefab"));
        foreach(var behaviour in go.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
        go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        animator=go.GetComponentInChildren<Animator>();animator.transform.localPosition=Vector3.zero;
        animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        return go;
    }
    public static AnimationClip Bake(string name,AnimationClip source,AnimationClip secondary,float secondaryWeight,float phase,string outputRoot=Root,float maximumFlightHeight=-1f)
    {
        var go=Rig(out var animator);var graph=PlayableGraph.Create("Grounded motion authoring");var mesh=new Mesh();
        try
        {
            var output=AnimationPlayableOutput.Create(graph,"Pose",animator);
            var main=AnimationClipPlayable.Create(graph,source);main.SetApplyFootIK(false);
            var mixer=AnimationMixerPlayable.Create(graph,2);graph.Connect(main,0,mixer,0);mixer.SetInputWeight(0,1-secondaryWeight);
            AnimationClipPlayable extra=default;
            if(secondary!=null){extra=AnimationClipPlayable.Create(graph,secondary);extra.SetApplyFootIK(false);graph.Connect(extra,0,mixer,1);mixer.SetInputWeight(1,secondaryWeight);}
            output.SetSourcePlayable(mixer);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();
            var boot=animator.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name.Contains("Boots"));
            var curves=Enumerable.Range(0,HumanTrait.MuscleCount+7).Select(_=>new AnimationCurve()).ToArray();
            var contacts=new[]{new AnimationCurve(),new AnimationCurve()};
            int count=Mathf.CeilToInt(source.length*120);float duration=source.length;
            var weights=boot.sharedMesh.boneWeights;
            var leftVertex=weights.Select(w=>boot.bones[w.boneIndex0].name.EndsWith("_l")).ToArray();
            using(var handler=new HumanPoseHandler(animator.avatar,animator.transform))
            {
                for(int f=0;f<=count;f++)
                {
                    double cycle=f/(double)count;main.SetTime(cycle*source.length);
                    if(secondary!=null)extra.SetTime(((cycle+phase)%1)*secondary.length);
                    graph.Evaluate(0);boot.BakeMesh(mesh);var vertices=mesh.vertices;float[] soles={float.PositiveInfinity,float.PositiveInfinity};
                    for(int v=0;v<vertices.Length;v++){int side=leftVertex[v]?0:1;soles[side]=Mathf.Min(soles[side],boot.transform.TransformPoint(vertices[v]).y);}
                    float low=Mathf.Min(soles[0],soles[1]);
                    // A run/sprint must retain its airborne part. The older repair
                    // treated every single-source clip as a grounded walking pose.
                    float flight=maximumFlightHeight>=0f?maximumFlightHeight:secondary!=null?.035f:0f;
                    float target=Mathf.Clamp(low,.004f,flight+.004f);
                    float correction=target-low;
                    var pose=new HumanPose();handler.GetHumanPose(ref pose);pose.bodyPosition.y+=correction/animator.humanScale;
                    float time=f*duration/count;
                    for(int j=0;j<HumanTrait.MuscleCount;j++)curves[j].AddKey(time,pose.muscles[j]);
                    int k=HumanTrait.MuscleCount;for(int j=0;j<3;j++)curves[k+j].AddKey(time,pose.bodyPosition[j]);for(int j=0;j<4;j++)curves[k+3+j].AddKey(time,pose.bodyRotation[j]);
                    for(int side=0;side<2;side++)contacts[side].AddKey(time,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.015f,.065f,soles[side]+correction)));
                }
            }
            string path=outputRoot+"/"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}clip.ClearCurves();clip.name=name;clip.frameRate=120;
            var names=HumanTrait.MuscleName.Concat(new[]{"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"}).ToArray();
            for(int j=0;j<names.Length;j++)
            {
                string property=names[j];foreach(string side in new[]{"Left","Right"})foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Little"})
                {string prefix=side+" "+finger+" ";if(property.StartsWith(prefix))property=side+"Hand."+finger+"."+property.Substring(prefix.Length);}
                // Enforce exact loop closure, then linear interpolation avoids overshoot into the floor.
                var keys=curves[j].keys;keys[keys.Length-1].value=keys[0].value;curves[j].keys=keys;
                for(int key=0;key<keys.Length;key++){AnimationUtility.SetKeyLeftTangentMode(curves[j],key,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curves[j],key,AnimationUtility.TangentMode.Linear);}
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property),curves[j]);
            }
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),"LeftFootContact"),contacts[0]);
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),"RightFootContact"),contacts[1]);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.startTime=0;settings.stopTime=duration;settings.loopTime=true;settings.loopBlend=true;
            settings.keepOriginalPositionY=true;settings.heightFromFeet=false;settings.keepOriginalPositionXZ=true;settings.keepOriginalOrientation=true;
            settings.loopBlendPositionY=true;settings.loopBlendPositionXZ=true;settings.loopBlendOrientation=true;
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            RefineContact(clip,flightLimit: maximumFlightHeight>=0f?maximumFlightHeight+.004f:secondary!=null?.039f:.004f);
            EditorUtility.SetDirty(clip);return clip;
        }
        finally{graph.Destroy();Object.DestroyImmediate(mesh);Object.DestroyImmediate(go);}
    }
    private static void RefineContact(AnimationClip clip,float flightLimit)
    {
        var go=Rig(out var a);var graph=PlayableGraph.Create("Retargeted contact refinement");var mesh=new Mesh();
        try
        {
            var output=AnimationPlayableOutput.Create(graph,"Pose",a);var p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);output.SetSourcePlayable(p);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();
            var boot=a.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name.Contains("Boots"));
            var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),"RootT.y");
            for(int pass=0;pass<2;pass++)
            {
                var previous=AnimationUtility.GetEditorCurve(clip,binding);var curve=new AnimationCurve();int steps=Mathf.CeilToInt(clip.length*120);
                for(int i=0;i<=steps;i++)
                {
                    float time=i*clip.length/steps;p.SetTime(time);graph.Evaluate(0);boot.BakeMesh(mesh);
                    float low=mesh.vertices.Min(v=>boot.transform.TransformPoint(v).y);
                    curve.AddKey(time,previous.Evaluate(time)+(Mathf.Clamp(low,.004f,flightLimit)-low)/a.humanScale);
                }
                var keys=curve.keys;keys[keys.Length-1].value=keys[0].value;curve.keys=keys;
                for(int i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
                AnimationUtility.SetEditorCurve(clip,binding,curve);
                // Recreate the playable so its internal curve cache sees the edited clip.
                graph.DestroyPlayable(p);p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);output.SetSourcePlayable(p);
            }
        }
        finally{graph.Destroy();Object.DestroyImmediate(mesh);Object.DestroyImmediate(go);}
    }
    private static float[] Track(AnimationClip clip)
    {
        var go=Rig(out var a);var graph=PlayableGraph.Create("Footfall phase");
        try{var output=AnimationPlayableOutput.Create(graph,"Pose",a);var p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);output.SetSourcePlayable(p);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();var track=new float[64];for(int i=0;i<64;i++){p.SetTime(i*clip.length/64d);graph.Evaluate(0);track[i]=a.GetBoneTransform(HumanBodyBones.LeftFoot).position.z-a.GetBoneTransform(HumanBodyBones.RightFoot).position.z;}return track;}
        finally{graph.Destroy();Object.DestroyImmediate(go);}
    }
    private static float FindPhase(AnimationClip reference,AnimationClip shifted)
    {
        var a=Track(reference);var b=Track(shifted);float best=float.NegativeInfinity;int offset=0;
        for(int phase=0;phase<64;phase++){float score=0;for(int i=0;i<64;i++)score+=a[i]*b[(i+phase)%64];if(score>best){best=score;offset=phase;}}
        return offset/64f;
    }
    public static float MeasureStride(AnimationClip clip,Vector3 axis=default)
    {
        if(axis==Vector3.zero)axis=Vector3.forward;
        var go=Rig(out var a);var graph=PlayableGraph.Create("Stride calibration");
        try{
            var output=AnimationPlayableOutput.Create(graph,"Pose",a);var p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);output.SetSourcePlayable(p);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();
            var foot=new Vector3[2,121];for(int f=0;f<=120;f++){p.SetTime(f*clip.length/120d);graph.Evaluate(0);foot[0,f]=a.GetBoneTransform(HumanBodyBones.LeftFoot).position;foot[1,f]=a.GetBoneTransform(HumanBodyBones.RightFoot).position;}
            var speeds=new List<float>();for(int leg=0;leg<2;leg++){float low=Enumerable.Range(0,121).Min(f=>foot[leg,f].y);for(int f=1;f<120;f++)if(foot[leg,f].y<low+.045f){float v=Vector3.Dot(foot[leg,f]-foot[leg,f-1],axis)/(clip.length/120);if(Mathf.Abs(v)>.15f)speeds.Add(Mathf.Abs(v));}}
            if(speeds.Count<4)throw new Exception("Unable to calibrate "+clip.name);speeds.Sort();return speeds[speeds.Count/2];
        }finally{graph.Destroy();Object.DestroyImmediate(go);}
    }
}
```


## PlayerLocomotionReview

Full file path: D:/GV project/Assets/Editor/Art/PlayerLocomotionReview.cs

```csharp
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
```


## PlayerSourceMotionImport

Full file path: D:/GV project/Assets/Editor/Art/PlayerSourceMotionImport.cs

```csharp
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Imports player motion using the original animation skeleton's shared T-pose.</summary>
public static class PlayerSourceMotionImport
{
    public const string Root = "Assets/Animations/PlayerSourceRig";
    public static void Import()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        string rigPath = Root + "/defaultmale_rig.fbx";
        AssetDatabase.ImportAsset(rigPath, ImportAssetOptions.ForceSynchronousImport);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(rigPath).OfType<Avatar>().Single();
        if (!avatar.isHuman || !avatar.isValid) throw new InvalidOperationException("Invalid source rig.");
        foreach (string name in new[] { "Idle", "WalkForwards", "RunForwards", "SprintForwards" })
        {
            string path = Root + "/m@" + name + ".fbx";
            if (!File.Exists(path))
                File.Copy("Assets/Art/ImportedCharacters/UnityMotion/m@" + name + ".fbx", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = "SharedRig" + name;
                clip.loopTime = true;
                clip.loopPose = true;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = false;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = false;
                clip.heightFromFeet = true;
                clip.cycleOffset = 0;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
    }
}
```


## PlayerWalkRunCorrection

Full file path: D:/GV project/Assets/Editor/Art/PlayerWalkRunCorrection.cs

```csharp
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>Repairs only the existing player's exploration gait assets and blend settings.</summary>
public static class PlayerWalkRunCorrection
{
    const string Prefab = "Assets/Prefabs/Player/PlayerPhase3.prefab";
    const string Root = "Assets/Animations/PlayerGrounded";
    const string ControllerPath = "Assets/Animations/StealthLocomotion/Survivor.controller";
    const string OverridesPath = "Assets/Animations/StealthLocomotion/ReferenceSurvivor.overrideController";

    [MenuItem("Tools/Dead Zone/Correct existing player walk and run")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play Mode before editing animation assets.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ExistingCityEnhancer.ScenePath)
            throw new InvalidOperationException("Open the enhanced ZombieMap scene first.");
        var player = Object.FindAnyObjectByType<ThirdPersonPlayerController>();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridesPath);
        if (player == null || controller == null || overrides == null ||
            player.GetComponentInChildren<Animator>().runtimeAnimatorController != overrides)
            throw new InvalidOperationException("The expected existing player/Animator is not active.");
        var state = controller.layers[0].stateMachine.states.First(s => s.state.name == "Locomotion").state;
        var tree = state.motion as BlendTree;
        if (tree == null || tree.children.Length != 4 || tree.blendParameter != "Speed")
            throw new InvalidOperationException("Inspect the changed locomotion tree before applying this repair.");

        // Use the shared source T-pose and native foot-goal tracks. Never rebake
        // the old per-clip Avatars or their travelling body curves.
        PlayerSourceMotionImport.Import();
        var idle = PlayerInPlaceMotion.Create("NaturalIdle", "Idle", true);
        var walk = PlayerInPlaceMotion.Create("NaturalWalk", "WalkForwards", true);
        var run = PlayerInPlaceMotion.Create("NaturalRun", "RunForwards", false);
        var sprint = PlayerInPlaceMotion.Create("NaturalSprint", "SprintForwards", false);
        var clips = new[] { walk, run, sprint };
        var samples = clips.Select(Sample).ToArray();
        float[] speeds = { player.WalkSpeed, player.RunSpeed, player.SprintSpeed };
        var children = tree.children;
        var report = new StringBuilder("Existing controller, model, motor speeds and camera retained.\n");
        for (int i = 0; i < 3; i++)
        {
            float rate = speeds[i] / samples[i].StanceSpeed;
            if (samples[i].MaximumHipX - samples[i].MinimumHipX > .10f)
                throw new InvalidOperationException(clips[i].name + " has excessive sideways body motion.");
            if (rate < .65f || rate > 1.35f)
                throw new InvalidOperationException(clips[i].name + " needs a different source clip; refusing extreme playback rate " + rate);
            children[i + 1].threshold = i + 1;
            children[i + 1].timeScale = rate;
            children[i + 1].cycleOffset = i == 0 ? 0f : AlignPhase(samples[0], samples[i]);
            report.AppendLine(clips[i].name + ": source stance=" + samples[i].StanceSpeed.ToString("F4") +
                " m/s; motor=" + speeds[i] + "; playback=" + rate.ToString("F4") +
                "; phase=" + children[i + 1].cycleOffset.ToString("F4") +
                "; lowest sole=" + samples[i].MinimumSole.ToString("F4") + ".." + samples[i].MaximumSole.ToString("F4") +
                "; hip lateral span=" + (samples[i].MaximumHipX - samples[i].MinimumHipX).ToString("F4") + " m");
        }
        children[0].threshold = 0;
        tree.useAutomaticThresholds = false;
        tree.children = children;
        overrides["Idle"] = idle;
        overrides["WalkForward"] = walk;
        overrides["Run"] = run;
        overrides["Sprint"] = sprint;
        // Preserve the original knee/ankle movement. The native goal tracks are
        // available to the existing small terrain helper; no second IK pass here.
        state.iKOnFeet = false;
        state.speed = 1f;
        state.speedParameterActive = false;
        Configure(player.gameObject);
        var prefab = PrefabUtility.LoadPrefabContents(Prefab);
        try { Configure(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, Prefab); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(overrides);
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/WalkRunCorrection.txt", report.ToString());
    }

    static void Configure(GameObject player)
    {
        var animator = player.GetComponentInChildren<Animator>();
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // This damping controls the directional aim/crouch trees. Exploration
        // Speed now follows the motor's already-smoothed measured displacement.
        var driver = new SerializedObject(player.GetComponent<PlayerAnimatorDriver>());
        driver.FindProperty("parameterDampTime").floatValue = .08f;
        driver.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(animator);
    }

    sealed class Gait
    {
        public float StanceSpeed, MinimumSole, MaximumSole;
        public float MinimumHipX = float.PositiveInfinity, MaximumHipX = float.NegativeInfinity;
        public float[] Phase = new float[120];
    }

    static Gait Sample(AnimationClip clip)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
        foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var a = go.GetComponentInChildren<Animator>();
        a.transform.localPosition = Vector3.zero;
        a.runtimeAnimatorController = null; a.applyRootMotion = false;
        var graph = PlayableGraph.Create("Player gait calibration");
        var mesh = new Mesh();
        try
        {
            var output = AnimationPlayableOutput.Create(graph, "Pose", a);
            var playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyFootIK(false);
            output.SetSourcePlayable(playable); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); graph.Play();
            var boots = a.GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.name.Contains("Boots"));
            var weights = boots.sharedMesh.boneWeights;
            var leftVertices = weights.Select(w => boots.bones[w.boneIndex0].name.EndsWith("_l")).ToArray();
            var sole = new float[2, 121]; var foot = new Vector3[2, 121];
            var result = new Gait { MinimumSole = float.PositiveInfinity, MaximumSole = float.NegativeInfinity };
            for (int i = 0; i <= 120; i++)
            {
                playable.SetTime(i * clip.length / 120d); graph.Evaluate(0); boots.BakeMesh(mesh);
                sole[0, i] = sole[1, i] = float.PositiveInfinity;
                var vertices = mesh.vertices;
                for (int v = 0; v < vertices.Length; v++)
                {
                    int side = leftVertices[v] ? 0 : 1;
                    sole[side, i] = Mathf.Min(sole[side, i], boots.transform.TransformPoint(vertices[v]).y);
                }
                float lowest = Mathf.Min(sole[0, i], sole[1, i]);
                result.MinimumSole = Mathf.Min(result.MinimumSole, lowest);
                result.MaximumSole = Mathf.Max(result.MaximumSole, lowest);
                float hipX = a.GetBoneTransform(HumanBodyBones.Hips).position.x;
                result.MinimumHipX = Mathf.Min(result.MinimumHipX, hipX);
                result.MaximumHipX = Mathf.Max(result.MaximumHipX, hipX);
                foot[0, i] = a.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                foot[1, i] = a.GetBoneTransform(HumanBodyBones.RightFoot).position;
                if (i < 120) result.Phase[i] = foot[0, i].z - foot[1, i].z;
            }
            var contactSpeeds = new List<float>();
            for (int side = 0; side < 2; side++)
                for (int i = 1; i < 120; i++)
                {
                    float backwardSpeed = -(foot[side, i].z - foot[side, i - 1].z) / (clip.length / 120);
                    // Measure only the supporting foot travelling backward through
                    // stance. Absolute velocity also counts a fast returning foot.
                    if (sole[side, i] < .035f && sole[side, i - 1] < .035f && backwardSpeed > .15f)
                        contactSpeeds.Add(backwardSpeed);
                }
            if (contactSpeeds.Count < 8) throw new InvalidOperationException("No usable stance phase in " + clip.name);
            contactSpeeds.Sort(); result.StanceSpeed = contactSpeeds[contactSpeeds.Count / 2];
            return result;
        }
        finally { graph.Destroy(); Object.DestroyImmediate(mesh); Object.DestroyImmediate(go); }
    }

    static float AlignPhase(Gait reference, Gait candidate)
    {
        float best = float.NegativeInfinity; int offset = 0;
        for (int phase = 0; phase < 120; phase++)
        {
            float score = 0;
            for (int i = 0; i < 120; i++) score += reference.Phase[i] * candidate.Phase[(i + phase) % 120];
            if (score > best) { best = score; offset = phase; }
        }
        return offset / 120f;
    }
}
```


## RealisticCityUpgrade

Full file path: D:/GV project/Assets/Editor/Art/RealisticCityUpgrade.cs

```csharp
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;

/// <summary>Rendering and scanned prop upgrade. Existing transforms, physics and navigation remain authoritative.</summary>
public static class RealisticCityUpgrade
{
    public const string Root="Assets/Art/Realism";
    private const string Vendor=Root+"/PolyHaven";
    private static readonly System.Random random=new System.Random(74173);
    private static float Range(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
    [MenuItem("Tools/Dead Zone/Apply realistic city graphics")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Leave Play Mode first.");
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        var district=GameObject.Find("OutbreakDistrict").transform;
        if(district.Find("Scanned realism details")!=null)throw new Exception("Already installed; refine existing materials instead.");
        var layout=district.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale));
        var physics=district.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
        var nav=district.GetComponent<NavMeshSurface>().navMeshData;
        PrepareImports();
        var root=new GameObject("Scanned realism details").transform;root.SetParent(district,false);
        Materials(district);Lighting(district);
        var fern=Foliage("fern_02");var shrub=Foliage("shrub_02");var car=CoveredCarMaterial();
        ReplaceVehicles(district,car);
        // Existing scanned barrels already have the correct collider/root. Hide only obsolete primitive skins.
        foreach(var t in district.GetComponentsInChildren<Transform>(true))if(t.name=="BarrelVisual")foreach(var r in t.GetComponentsInChildren<Renderer>())r.enabled=false;
        PlantEdges(root,fern,shrub);
        foreach(var entry in layout)if(entry.Key==null||Matrix4x4.TRS(entry.Key.localPosition,entry.Key.localRotation,entry.Key.localScale)!=entry.Value)throw new Exception("Existing transform changed: "+entry.Key);
        foreach(var entry in physics)if(entry.Key==null||EditorJsonUtility.ToJson(entry.Key)!=entry.Value)throw new Exception("Existing collider changed.");
        if(district.GetComponentsInChildren<Collider>(true).Length!=physics.Count||district.GetComponent<NavMeshSurface>().navMeshData!=nav)throw new Exception("Physics/navigation changed.");
        var camera=Camera.main;camera.transform.SetPositionAndRotation(new Vector3(.45f,2.3f,-23),Quaternion.LookRotation(new Vector3(0,2,12)-new Vector3(.45f,2.3f,-23)));camera.fieldOfView=58;
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/RealismLayout.txt","Preserved "+layout.Count+" existing transforms and "+physics.Count+" collider definitions; unchanged NavMesh data.\nNew scanned foliage renderers="+root.GetComponentsInChildren<Renderer>().Length+"\nNo gameplay scripts, character rig, sockets or movement settings changed.\n");
        Capture();
    }
    private static void PrepareImports()
    {
        Phase2AnimationAssets.Folder(Root+"/Materials");Phase2AnimationAssets.Folder(Root+"/Reflections");
        AssetDatabase.StartAssetEditing();
        try{
            foreach(string guid in AssetDatabase.FindAssets("t:Texture",new[]{Vendor}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
                bool normal=path.Contains("nor_gl"),colour=path.Contains("_diff_"),hdr=path.EndsWith(".hdr");
                importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=colour;importer.mipmapEnabled=true;importer.anisoLevel=8;
                importer.maxTextureSize=path.Contains("_2k")?2048:1024;
                importer.textureCompression=hdr?TextureImporterCompression.Uncompressed:TextureImporterCompression.CompressedHQ;
                importer.alphaSource=TextureImporterAlphaSource.None;importer.wrapMode=TextureWrapMode.Repeat;importer.SaveAndReimport();
            }
        }finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.Refresh();
    }
    private static Texture2D Map(string id,string channel,string res="1k",string extension="jpg")=>AssetDatabase.LoadAssetAtPath<Texture2D>(Vendor+"/"+id+"/textures/"+id+"_"+channel+"_"+res+"."+extension);
    private static Material NewMaterial(string name,string shader)
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find(shader)){name=name,enableInstancing=true};AssetDatabase.CreateAsset(m,path);}return m;
    }
    private static Material Foliage(string id)
    {
        var m=NewMaterial(id,"Outbreak/Scanned Foliage");m.SetTexture("_MainTex",Map(id,"diff","2k"));m.SetTexture("_AlphaTex",Map(id,"alpha","2k","png"));m.SetTexture("_BumpMap",Map(id,"nor_gl"));m.SetTexture("_RoughnessMap",Map(id,"rough"));m.SetTexture("_OcclusionMap",Map(id,"ao"));
        m.SetColor("_Color",new Color(.89f,.91f,.85f));m.SetFloat("_Cutoff",.43f);m.SetFloat("_WindStrength",id=="fern_02"?.008f:.013f);m.SetFloat("_Translucency",.10f);EditorUtility.SetDirty(m);return m;
    }
    private static Material CoveredCarMaterial()
    {
        var m=NewMaterial("Scanned covered car","Outbreak/Survivor Surface");m.SetTexture("_MainTex",Map("covered_car","diff","2k"));m.SetTexture("_BumpMap",Map("covered_car","nor_gl"));m.SetTexture("_Roughness",Map("covered_car","rough"));m.SetTexture("_OcclusionMap",Map("covered_car","ao"));m.SetColor("_Color",Color.white);m.SetFloat("_Dirt",.12f);m.SetFloat("_BumpScale",1);m.SetFloat("_RoughnessGamma",1);m.SetFloat("_RoughnessAmount",1);EditorUtility.SetDirty(m);return m;
    }
    private static void Materials(Transform district)
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{ReclaimedCityPresentation.Root+"/Materials"}))
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));string n=m.name.ToLowerInvariant();
            if(n.Contains("asphalt")||n.Contains("rainwater")){m.SetColor("_Color",new Color(.72f,.73f,.72f));m.SetFloat("_Moss",.28f);m.SetFloat("_Wetness",n.Contains("rainwater")?.55f:.22f);m.SetFloat("_NormalStrength",n.Contains("rainwater")?.25f:1f);}
            if(n.Contains("cracked_concrete")||n.Contains("brick_wall")){m.SetColor("_Color",new Color(.88f,.87f,.83f));m.SetFloat("_Moss",.3f);m.SetFloat("_NormalStrength",1.1f);m.SetFloat("_Wetness",.08f);}
            if(n.Contains("roadpaint")){m.shader=Shader.Find("Outbreak/Worn Road Paint");m.SetColor("_Color",n.Contains("faded")?new Color(.58f,.56f,.48f):new Color(.65f,.56f,.31f));m.SetFloat("_Cutoff",.40f);m.SetFloat("_SurfaceY",.033f);m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(CityArtAssets.Root+"/PolyHaven/asphalt_02/nor_gl.jpg"));}
            if(n.Contains("glass")){m.shader=Shader.Find("Standard");m.color=new Color(.13f,.17f,.19f);m.SetFloat("_Glossiness",.65f);m.SetFloat("_Metallic",.35f);}
            if(n.StartsWith("survivor_")){m.SetFloat("_Dirt",n.Contains("head")||n.Contains("hands")?.09f:.18f);m.SetFloat("_BumpScale",.85f);m.SetFloat("_RoughnessGamma",1/2.2f);}
            if(n.Contains("infected")){m.SetFloat("_Dirt",.27f);m.SetFloat("_BumpScale",.8f);}
            EditorUtility.SetDirty(m);
        }
        foreach(var r in district.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="Outbreak/Worn Road Paint")r.shadowCastingMode=ShadowCastingMode.Off;
    }
    private static void Lighting(Transform district)
    {
        PlayerSettings.colorSpace=ColorSpace.Linear;QualitySettings.antiAliasing=4;QualitySettings.anisotropicFiltering=AnisotropicFiltering.ForceEnable;
        QualitySettings.shadowResolution=ShadowResolution.VeryHigh;QualitySettings.shadowCascades=4;QualitySettings.shadowDistance=85;
        var sky=NewMaterial("Photographic overcast sky","Skybox/Panoramic");sky.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Vendor+"/kloofendal_overcast_puresky/kloofendal_overcast_puresky_2k.hdr"));sky.SetFloat("_Exposure",.75f);sky.SetFloat("_Rotation",120);EditorUtility.SetDirty(sky);
        RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Skybox;RenderSettings.ambientIntensity=.85f;RenderSettings.reflectionIntensity=.75f;RenderSettings.fog=false;
        foreach(var l in Object.FindObjectsByType<Light>())if(l.type==LightType.Directional){l.transform.rotation=Quaternion.Euler(44,-38,0);l.color=new Color(.98f,.98f,1);l.intensity=.8f;l.shadows=LightShadows.Soft;l.shadowStrength=.58f;l.shadowBias=.025f;l.shadowNormalBias=.14f;}
        var camera=Camera.main;camera.allowHDR=true;camera.allowMSAA=true;
        var old=camera.GetComponent<CityCameraAtmosphere>();if(old!=null)old.enabled=false;
        var effect=camera.GetComponent<CinematicCityCamera>()??camera.gameObject.AddComponent<CinematicCityCamera>();effect.Configure(Shader.Find("Hidden/Outbreak/Cinematic City"));
        OutbreakEditor.Values(effect,s=>{s.FindProperty("exposureEV").floatValue=.05f;s.FindProperty("hazeDensity").floatValue=.007f;s.FindProperty("maximumHaze").floatValue=.38f;});
        var ao=camera.GetComponent<CityContactShadows>();if(ao!=null)OutbreakEditor.Values(ao,s=>{s.FindProperty("strength").floatValue=.65f;s.FindProperty("radius").floatValue=.45f;});
        DynamicGI.UpdateEnvironment();
    }
    private static void ReplaceVehicles(Transform district,Material material)
    {
        var targets=district.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Abandoned vehicle"||t.name=="WreckedSedan").ToArray();
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Vendor+"/covered_car/covered_car_1k.fbx");
        foreach(var target in targets)
        {
            foreach(var lod in target.GetComponentsInChildren<LODGroup>())lod.enabled=false;
            foreach(var r in target.GetComponentsInChildren<Renderer>())r.enabled=false;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,target);go.name="Scanned vehicle";
            go.transform.localPosition=Vector3.zero;go.transform.localRotation=asset.transform.localRotation;
            var rs=go.GetComponentsInChildren<Renderer>();var initial=LocalBounds(target,rs);
            if(initial.size.x>initial.size.z)go.transform.localRotation=Quaternion.Euler(0,90,0)*go.transform.localRotation;
            // Fit the retained collider footprint using parent-space bounds, independent of the parking yaw.
            var local=LocalBounds(target,rs);float factor=Mathf.Min(1.9f/local.size.x,4.3f/local.size.z);go.transform.localScale*=factor;
            local=LocalBounds(target,rs);go.transform.localPosition+=new Vector3(-local.center.x,.03f-local.min.y,-local.center.z);
            foreach(var r in rs){r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;r.lightProbeUsage=LightProbeUsage.BlendProbes;}
            var group=go.AddComponent<LODGroup>();group.SetLODs(new[]{new LOD(.025f,rs)});group.RecalculateBounds();
        }
    }
    private static Bounds BoundsOf(Renderer[] rs){var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    private static Bounds LocalBounds(Transform parent,Renderer[] rs)
    {
        var b=new Bounds();bool first=true;
        foreach(var r in rs){var f=r.GetComponent<MeshFilter>();if(f==null)continue;var m=f.sharedMesh.bounds;foreach(float x in new[]{-1f,1f})foreach(float y in new[]{-1f,1f})foreach(float z in new[]{-1f,1f}){var p=parent.InverseTransformPoint(r.transform.TransformPoint(m.center+Vector3.Scale(m.extents,new Vector3(x,y,z))));if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}}
        return b;
    }
    private static void PlantEdges(Transform root,Material fern,Material shrub)
    {
        Physics.SyncTransforms();
        var fernAsset=AssetDatabase.LoadAssetAtPath<GameObject>(Vendor+"/fern_02/fern_02_1k.fbx");var shrubAsset=AssetDatabase.LoadAssetAtPath<GameObject>(Vendor+"/shrub_02/shrub_02_1k.fbx");
        var ferns=fernAsset.GetComponentsInChildren<MeshFilter>().OrderBy(f=>f.sharedMesh.vertexCount).ToArray();var shrubs=shrubAsset.GetComponentsInChildren<MeshFilter>().OrderBy(f=>f.sharedMesh.vertexCount).ToArray();
        for(int i=0;i<126;i++)
        {
            float side=i%2==0?-1:1;Vector3 p=new Vector3(side*Range(5.55f,7.6f),3,Range(-34,34));
            if(!Physics.Raycast(p,Vector3.down,out var hit,4,1<<8,QueryTriggerInteraction.Ignore)||hit.point.y>.6f)continue;
            var source=ferns[i%ferns.Length];Plant(root,source,fern,hit.point,Range(.3f,.58f),Range(0,360),.014f);
        }
        for(int i=0;i<22;i++)
        {
            float side=i%2==0?-1:1;Vector3 p=new Vector3(side*Range(7.1f,10.4f),4,Range(-34,33));
            if(!Physics.Raycast(p,Vector3.down,out var hit,5,1<<8,QueryTriggerInteraction.Ignore)||hit.point.y>.6f)continue;
            Plant(root,shrubs[i%2],shrub,hit.point,Range(.65f,1.15f),Range(0,360),.025f);
        }
    }
    private static void Plant(Transform root,MeshFilter source,Material material,Vector3 position,float height,float yaw,float cull)
    {
        var go=new GameObject(source.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;
        // Preserve the source FBX's axis conversion, while discarding its showroom spacing.
        go.transform.rotation=Quaternion.Euler(0,yaw,0)*source.transform.rotation;go.transform.localScale=source.transform.lossyScale;
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;var b=renderer.bounds;go.transform.localScale*=height/Mathf.Max(.001f,b.size.y);b=renderer.bounds;go.transform.position=position-new Vector3(b.center.x,b.min.y,b.center.z);
        renderer.shadowCastingMode=ShadowCastingMode.TwoSided;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
        var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(cull,new Renderer[]{renderer})});lod.RecalculateBounds();
    }
    public static void BakeReflections()
    {
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        var district=GameObject.Find("OutbreakDistrict").transform;int index=0;
        foreach(var probe in district.GetComponentsInChildren<ReflectionProbe>())
        {
            probe.mode=ReflectionProbeMode.Baked;probe.resolution=256;probe.hdr=true;probe.boxProjection=true;probe.intensity=.8f;probe.cullingMask=~(1<<9);probe.clearFlags=ReflectionProbeClearFlags.Skybox;
            string path=Root+"/Reflections/City_"+(index++)+".exr";if(!Lightmapping.BakeReflectionProbe(probe,path))throw new Exception("Reflection capture failed.");
            AssetDatabase.ImportAsset(path);probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(path);EditorUtility.SetDirty(probe);
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();Capture();
    }
    [MenuItem("Tools/Dead Zone/Refine realistic city lighting")]
    public static void Refine()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play Mode first.");
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        var district=GameObject.Find("OutbreakDistrict").transform;
        // Scanned plants replace the triangular stand-in grass. Wetness stays on the
        // asphalt itself rather than on the old disconnected opaque puddle polygons.
        foreach(var r in district.GetComponentsInChildren<Renderer>(true))
            if(r.name.StartsWith("Curb growth ")||r.name=="Batched StandingRainwater")r.enabled=false;
        RenderSettings.ambientIntensity=.45f;RenderSettings.reflectionIntensity=.65f;
        RenderSettings.skybox.SetFloat("_Exposure",.42f);EditorUtility.SetDirty(RenderSettings.skybox);
        foreach(var l in Object.FindObjectsByType<Light>())if(l.type==LightType.Directional)
        {l.intensity=1.1f;l.shadowStrength=.82f;l.color=new Color(1f,.96f,.90f);}
        OutbreakEditor.Values(Camera.main.GetComponent<CinematicCityCamera>(),s=>{
            s.FindProperty("exposureEV").floatValue=-.15f;
            s.FindProperty("hazeDensity").floatValue=.0028f;
            s.FindProperty("maximumHaze").floatValue=.18f;
            s.FindProperty("contrast").floatValue=1.05f;
            s.FindProperty("saturation").floatValue=.92f;
        });
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{ReclaimedCityPresentation.Root+"/Materials"}))
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if(m.shader.name=="Outbreak/Worn Road Paint"){m.SetFloat("_Cutoff",.29f);EditorUtility.SetDirty(m);}
            if(m.name.ToLowerInvariant().Contains("glass"))
            {
                m.shader=Shader.Find("Outbreak/Dirty City Glass");
                m.SetColor("_Color",new Color(.045f,.055f,.055f));
                m.SetFloat("_DustAmount",.38f);m.SetFloat("_Smoothness",.82f);
                m.SetFloat("_DustSmoothness",.24f);m.SetFloat("_StreakStrength",.45f);
                EditorUtility.SetDirty(m);
            }
        }
        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
    }
    public static void Capture()
    {
        Directory.CreateDirectory("Previews/ReclaimedCity");
        ReclaimedCityPresentation.Capture("RealismAfter",new Vector3(.45f,2.7f,-23),new Vector3(0,3,12),60);
        ReclaimedCityPresentation.Capture("RealismStreet",new Vector3(-2.3f,1.9f,-8),new Vector3(-9,4,14),62);
    }
}
```


## ReclaimedCityPresentation

Full file path: D:/GV project/Assets/Editor/Art/ReclaimedCityPresentation.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;

/// <summary>A presentation pass on the existing level. Geometry, collision and navigation are locked.</summary>
public static class ReclaimedCityPresentation
{
    public const string Root="Assets/Art/ReclaimedCity";
    private static readonly Dictionary<Material,Material> materials=new Dictionary<Material,Material>();
    private static readonly System.Random random=new System.Random(17042);
    private static readonly StringBuilder report=new StringBuilder();
    private static float Rand(float lo,float hi)=>Mathf.Lerp(lo,hi,(float)random.NextDouble());

    [MenuItem("Tools/Dead Zone/Apply reclaimed city presentation")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play Mode first.");
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        var district=GameObject.Find("OutbreakDistrict").transform;
        if(district.Find("Reclaimed vegetation")!=null)throw new InvalidOperationException("Presentation is already installed. Edit its existing materials and vegetation groups.");
        foreach(string folder in new[]{Root,Root+"/Materials",Root+"/Meshes","Previews/ReclaimedCity"})Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();materials.Clear();report.Clear();
        var transforms=district.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale));
        var collisions=district.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
        var surface=district.GetComponent<NavMeshSurface>();var nav=surface.navMeshData;
        Capture("Before",new Vector3(.45f,2.7f,-23),new Vector3(0,3,12),60);
        foreach(var r in district.GetComponentsInChildren<MeshRenderer>(true))r.sharedMaterials=r.sharedMaterials.Select(EnvironmentMaterial).ToArray();
        Dress(district);
        Lighting();
        string[] prefabs={"Assets/Prefabs/Player/PlayerPhase3.prefab","Assets/Prefabs/Enemies/EnemyBase.prefab","Assets/Prefabs/Enemies/Hunter.prefab","Assets/Prefabs/Enemies/Runner.prefab","Assets/Prefabs/Enemies/Ambusher.prefab","Assets/Prefabs/Enemies/Elite.prefab"};
        foreach(string path in prefabs)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try{Character(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        Character(Object.FindAnyObjectByType<ThirdPersonPlayerController>().gameObject);
        foreach(var actor in Object.FindObjectsByType<EnemyActor>())Character(actor.gameObject);
        foreach(var item in transforms)
            if(item.Key==null||Matrix4x4.TRS(item.Key.localPosition,item.Key.localRotation,item.Key.localScale)!=item.Value)
                throw new InvalidOperationException("Existing layout transform changed: "+(item.Key==null?"missing":item.Key.name));
        foreach(var item in collisions)
            if(item.Key==null||EditorJsonUtility.ToJson(item.Key)!=item.Value)throw new InvalidOperationException("Existing collision changed.");
        if(surface.navMeshData!=nav)throw new InvalidOperationException("Navigation data changed.");
        if(district.GetComponentsInChildren<Collider>(true).Length!=collisions.Count)throw new InvalidOperationException("Added decoration has collision.");
        report.AppendLine("PASS: "+transforms.Count+" existing environment transforms unchanged; "+collisions.Count+" collider definitions unchanged; navigation data unchanged.");
        ExistingCityEnhancer.ValidateNavigation();Phase18Setup.Validate();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        Capture("After",new Vector3(.45f,2.7f,-23),new Vector3(0,3,12),60);
        Capture("Street",new Vector3(-2.3f,1.9f,-8),new Vector3(-9,4,14),62);
        CharacterPreview();
        var renderers=district.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
        report.AppendLine("Enabled environment renderers: "+renderers.Length);
        report.AppendLine("Vegetation triangles: "+district.Find("Reclaimed vegetation").GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3));
        report.AppendLine("Retained current grounded controllers, all meshes/Avatars, weapon sockets, enemy stats and AI.");
        File.WriteAllText("Logs/ReclaimedCityPresentation.txt",report.ToString());
        Debug.Log("RECLAIMED_CITY_PASS\n"+report);
    }

    private static Material EnvironmentMaterial(Material source)
    {
        if(source==null)return null;
        if(materials.TryGetValue(source,out var found))return found;
        string shader=source.shader.name;
        if(shader!="Standard"&&shader!="Outbreak/Weathered Surface")return source;
        var copy=new Material(source){name=source.name+" Reclaimed",enableInstancing=true};
        if(shader=="Outbreak/Weathered Surface")
        {
            copy.shader=Shader.Find("Outbreak/Reclaimed Surface");
            string lower=source.name.ToLowerInvariant();
            bool metal=lower.Contains("iron")||lower.Contains("rust")||lower.Contains("bin")||lower.Contains("barrel");
            copy.SetFloat("_Moss",metal?.14f:lower.Contains("asphalt")?.5f:.65f);
            copy.SetFloat("_Wetness",lower.Contains("asphalt")?.34f:.12f);
            copy.SetFloat("_NormalStrength",.85f);
            copy.SetColor("_MossColor",new Color(.16f,.205f,.07f));
            copy.SetColor("_Color",Color.Lerp(source.GetColor("_Color"),Color.white,.16f));
        }
        else if(source.name.Contains("vehicle paint")||source.name.Contains("CarPaint"))
        {
            var rust=AssetDatabase.LoadAssetAtPath<Material>(CityArtAssets.Root+"/Materials/rusty_corrugated_iron_World.mat");
            if(rust!=null){copy.CopyPropertiesFromMaterial(rust);copy.shader=Shader.Find("Outbreak/Reclaimed Surface");copy.SetFloat("_Scale",2);copy.SetFloat("_Moss",.15f);copy.SetColor("_Color",new Color(.5f,.55f,.43f));}
        }
        else if(copy.HasProperty("_Glossiness"))copy.SetFloat("_Glossiness",Mathf.Min(.4f,copy.GetFloat("_Glossiness")));
        AssetDatabase.CreateAsset(copy,AssetDatabase.GenerateUniqueAssetPath(Root+"/Materials/"+Safe(source.name)+".mat"));
        materials[source]=copy;return copy;
    }
    private static string Safe(string name)=>string.Concat(name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));

    private static void Character(GameObject root)
    {
        bool player=root.GetComponent<ThirdPersonPlayerController>()!=null;
        string category=player?"Survivor":root.name.Replace("(Clone)","");
        foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skin.sharedMaterials=skin.sharedMaterials.Select(m=>CharacterMaterial(m,category,player)).ToArray();
        var animator=root.GetComponentInChildren<Animator>(true);
        if(animator==null||!animator.isHuman)return;
        if(animator.GetComponent<CharacterMotionPolish>()==null)animator.gameObject.AddComponent<CharacterMotionPolish>();
        OutbreakEditor.Values(animator.GetComponent<CharacterMotionPolish>(),s=>{
            s.FindProperty("amount").floatValue=player?.8f:category.Contains("Runner")?.65f:.45f;
            s.FindProperty("breathing").floatValue=player?.35f:.6f;
        });
        report.AppendLine(category+": preserved humanoid Avatar and "+animator.runtimeAnimatorController.name+", added torso inertia.");
    }

    private static Material CharacterMaterial(Material source,string category,bool player)
    {
        if(source==null||source.name.Contains("Eyes"))return source;
        if(source.shader.name=="Outbreak/Survivor Surface")return source;
        string name=category+"_"+Safe(source.name),path=Root+"/Materials/"+name+".mat";
        var result=AssetDatabase.LoadAssetAtPath<Material>(path);if(result!=null)return result;
        result=new Material(Shader.Find("Outbreak/Survivor Surface")){name=name};
        result.SetTexture("_MainTex",source.mainTexture);
        result.SetTexture("_BumpMap",source.GetTexture("_BumpMap"));
        result.SetTexture("_OcclusionMap",source.GetTexture("_OcclusionMap"));
        result.SetFloat("_BumpScale",player?.85f:.65f);result.SetFloat("_Dirt",player?.38f:.5f);
        result.SetFloat("_Smoothness",.22f);result.SetFloat("_RoughnessAmount",0);
        Color tint=Color.white;
        if(player)
        {
            string part=source.name.Split('_')[0].ToLowerInvariant();if(part=="cap1")part="caps1";
            string texture=AssetDatabase.FindAssets("t:Texture2D",new[]{SurvivalistSetup.Root+"/Vendor/Texture"}).Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p=>Path.GetFileNameWithoutExtension(p).ToLowerInvariant()=="t_military_"+part+"_rough");
            if(texture!=null){result.SetTexture("_Roughness",AssetDatabase.LoadAssetAtPath<Texture2D>(texture));result.SetFloat("_RoughnessAmount",1);result.SetFloat("_RoughnessGamma",((TextureImporter)AssetImporter.GetAtPath(texture)).sRGBTexture?1/2.2f:1);}
            if(part.Contains("pants"))
            {
                result.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(SurvivalistSetup.Root+"/Vendor/Texture/Bottom/T_Military_Pants2_color_skin3.png"));
                tint=new Color(.76f,.79f,.7f);
            }
            if(part.Contains("shirt"))tint=new Color(.81f,.86f,.74f);
            if(part.Contains("head")||part.Contains("hands")){result.SetFloat("_Dirt",.14f);result.SetFloat("_BumpScale",.6f);}
        }
        else
        {
            bool runner=category.Contains("Runner"),elite=category.Contains("Elite"),ambusher=category.Contains("Ambusher");
            tint=runner?new Color(.93f,.83f,.77f):elite?new Color(.76f,.8f,.67f):ambusher?new Color(.76f,.8f,.79f):new Color(.89f,.88f,.8f);
            result.SetFloat("_Infection",runner?.22f:elite?.75f:ambusher?.6f:.4f);
            result.SetColor("_InfectionColor",elite?new Color(.36f,.28f,.13f):ambusher?new Color(.20f,.24f,.19f):new Color(.28f,.14f,.11f));
            result.SetFloat("_Smoothness",.16f);
        }
        result.SetColor("_Color",tint);AssetDatabase.CreateAsset(result,path);return result;
    }

    private static void Lighting()
    {
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=new Color(.49f,.55f,.55f);RenderSettings.fogDensity=.0085f;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.47f,.53f,.59f);
        RenderSettings.ambientEquatorColor=new Color(.29f,.32f,.30f);
        RenderSettings.ambientGroundColor=new Color(.16f,.18f,.13f);
        var sky=new Material(RenderSettings.skybox){name="Broken cloud daylight"};
        sky.SetColor("_SkyColor",new Color(.77f,.82f,.86f));sky.SetColor("_Horizon",new Color(.64f,.69f,.7f));sky.SetColor("_CloudDark",new Color(.42f,.48f,.53f));
        AssetDatabase.CreateAsset(sky,Root+"/Materials/Daylight sky.mat");RenderSettings.skybox=sky;
        foreach(var l in Object.FindObjectsByType<Light>())if(l.type==LightType.Directional)
        {l.transform.rotation=Quaternion.Euler(43,-38,0);l.color=new Color(1,.91f,.77f);l.intensity=1.15f;l.shadows=LightShadows.Soft;l.shadowStrength=.8f;l.shadowBias=.035f;l.shadowNormalBias=.22f;}
        var camera=Camera.main;
        var ao=camera.GetComponent<CityContactShadows>()??camera.gameObject.AddComponent<CityContactShadows>();ao.Configure(Shader.Find("Hidden/Outbreak/Contact Shadows"));
        var grade=camera.GetComponent<CityCameraAtmosphere>();
        if(grade!=null)OutbreakEditor.Values(grade,s=>{s.FindProperty("exposure").floatValue=1.03f;s.FindProperty("saturation").floatValue=.91f;s.FindProperty("contrast").floatValue=1.06f;s.FindProperty("vignette").floatValue=.08f;s.FindProperty("bloom").floatValue=.045f;});
        // Closer shoulder framing retains all targets and the existing collision solver.
        foreach(var mode in Object.FindObjectsByType<ThirdPersonCameraMode>())if(mode.name.Contains("Exploration"))
            OutbreakEditor.Values(mode,s=>{s.FindProperty("distance").floatValue=3.5f;s.FindProperty("shoulderOffset").vector2Value=new Vector2(.46f,.05f);s.FindProperty("fieldOfView").floatValue=58;});
    }

    internal sealed class Leaves
    {
        public readonly List<Vector3> v=new List<Vector3>();public readonly List<int> t=new List<int>();
        public readonly List<Color> colors=new List<Color>();public readonly List<Vector2> uv=new List<Vector2>();
        public void Leaf(Vector3 origin,Vector3 direction,Vector3 side,float length,float width,Color color)
        {
            direction.Normalize();side.Normalize();Vector3 normal=Vector3.Cross(side,direction).normalized;
            int start=v.Count;
            v.Add(origin);v.Add(origin+direction*length*.42f-side*width);v.Add(origin+direction*length*.48f+normal*width*.22f);
            v.Add(origin+direction*length*.42f+side*width);v.Add(origin+direction*length);
            uv.AddRange(new[]{new Vector2(.5f,0),new Vector2(0,.45f),new Vector2(.5f,.5f),new Vector2(1,.45f),new Vector2(.5f,1)});
            for(int i=0;i<5;i++){var c=color*(i==0?.58f:i==2?1.13f:1);c.a=i==0?0:i==4?1:.5f;colors.Add(c);}
            foreach(int index in new[]{0,1,2,0,2,3,1,4,2,2,4,3})t.Add(start+index);
        }
        public void Save(Transform parent,string name,Material material)
        {
            if(v.Count==0)return;
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();
            mesh.bounds=new Bounds(mesh.bounds.center,mesh.bounds.size+Vector3.one*.1f);
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Root+"/Meshes/"+Safe(name)+".asset"));
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=material;
            var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.009f,new[]{go.GetComponent<Renderer>()})});lod.RecalculateBounds();
        }
    }
    private static Color Green()=>Color.Lerp(new Color(.12f,.19f,.045f),new Color(.34f,.38f,.105f),Rand(0,1));
    private static void Dress(Transform district)
    {
        var root=new GameObject("Reclaimed vegetation").transform;root.SetParent(district,false);
        var leaves=new Material(Shader.Find("Outbreak/Living Foliage")){name="Weathered green foliage",enableInstancing=true};AssetDatabase.CreateAsset(leaves,Root+"/Materials/Foliage.mat");
        Physics.SyncTransforms();
        // Edge growth is visual-only and does not alter path width, collision or tactical markers.
        for(int zone=0;zone<10;zone++)
        {
            var grass=new Leaves();float startZ=-35+zone*7;
            for(int patch=0;patch<20;patch++)
            {
                float x=(patch%2==0?-1:1)*Rand(4.8f,7.6f),z=startZ+Rand(0,7);
                if(!Physics.Raycast(new Vector3(x,3,z),Vector3.down,out var hit,3.4f,1<<8,QueryTriggerInteraction.Ignore)||hit.point.y>.5f)continue;
                for(int blade=0;blade<24;blade++)
                {
                    Vector3 p=hit.point+new Vector3(Rand(-.38f,.38f),.005f,Rand(-.38f,.38f));
                    float angle=Rand(0,Mathf.PI*2);Vector3 side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    grass.Leaf(p,Vector3.up+Vector3.Cross(side,Vector3.up)*Rand(.05f,.65f),side,Rand(.13f,.52f),Rand(.008f,.025f),Green());
                }
                for(int stem=0;stem<5;stem++)
                {
                    Vector3 p=hit.point+new Vector3(Rand(-.25f,.25f),Rand(.05f,.17f),Rand(-.25f,.25f));
                    for(int leaf=0;leaf<5;leaf++){float a=leaf*1.256f;var d=new Vector3(Mathf.Cos(a),.45f,Mathf.Sin(a));grass.Leaf(p,d,Vector3.Cross(d,Vector3.up),Rand(.12f,.24f),.045f,Green());}
                }
            }
            grass.Save(root,"Curb growth "+zone,leaves);
        }
        int faceId=0;
        foreach(var face in district.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Weathered facade").ToArray())
        {
            var ivy=new Leaves();
            float width=face.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Floor ledge").Select(t=>t.localScale.x).DefaultIfEmpty(12).Max();
            for(int vine=0;vine<9;vine++)
            {
                float centre=(vine%2==0?-1:1)*width*Rand(.25f,.46f);float height=Rand(5,12),baseY=Rand(.4f,2.8f);
                for(float y=baseY;y<height;y+=.17f)
                {
                    float x=centre+Mathf.Sin(y*.9f+vine)*.3f;
                    for(int side=-1;side<=1;side+=2)
                    {
                        Vector3 local=new Vector3(x+side*Rand(.03f,.25f),y,-Rand(.20f,.38f));
                        Vector3 dir=face.TransformDirection(new Vector3(side*Rand(.3f,.9f),Rand(.2f,.75f),-.15f));
                        Vector3 lateral=face.TransformDirection(new Vector3(-dir.y,side*.7f,0));
                        ivy.Leaf(face.TransformPoint(local),dir,lateral,Rand(.15f,.3f),Rand(.065f,.13f),Green());
                    }
                }
            }
            ivy.Save(root,"Facade ivy "+faceId++,leaves);
        }
        // Sparse crowns on the existing dead trees preserve their footprint and silhouette anchors.
        int treeId=0;
        foreach(var tree in district.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Dead street tree").ToArray())
        {
            var crown=new Leaves();
            for(int i=0;i<420;i++)
            {
                float angle=Rand(0,Mathf.PI*2),distance=Rand(.1f,1.35f);
                var p=tree.position+new Vector3(Mathf.Cos(angle)*distance,Rand(2.8f,4.4f),Mathf.Sin(angle)*distance*.7f);
                var d=new Vector3(Rand(-1,1),Rand(-.15f,.7f),Rand(-1,1));
                crown.Leaf(p,d,Vector3.Cross(d,Vector3.up),Rand(.12f,.24f),Rand(.04f,.08f),Green());
            }
            crown.Save(root,"Sparse tree regrowth "+treeId++,leaves);
        }
    }

    public static void Capture(string name,Vector3 position,Vector3 lookAt,float fov)
    {
        var camera=Camera.main;var p=camera.transform.position;var q=camera.transform.rotation;float angle=camera.fieldOfView,aspect=camera.aspect;
        var player=Object.FindAnyObjectByType<ThirdPersonPlayerController>();var a=player.GetComponentInChildren<Animator>();
        var bones=a.GetComponentsInChildren<Transform>();var poses=bones.Select(t=>(t.localPosition,t.localRotation)).ToArray();
        var rt=new RenderTexture(1600,1000,24){antiAliasing=4};var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);var active=RenderTexture.active;
        try
        {
            a.Rebind();a.SetBool("IsGrounded",true);a.SetLayerWeight(a.GetLayerIndex("Weapon Carry"),1);a.Update(.01f);
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(lookAt-position));camera.fieldOfView=fov;camera.aspect=1.6f;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();
            SavePPM(tex,"Previews/ReclaimedCity/"+name+".ppm");
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
            camera.transform.SetPositionAndRotation(p,q);camera.fieldOfView=angle;camera.aspect=aspect;
            for(int i=0;i<bones.Length;i++){bones[i].localPosition=poses[i].Item1;bones[i].localRotation=poses[i].Item2;}
        }
    }
    private static void SavePPM(Texture2D tex,string path)
    {
        var pixels=tex.GetPixels32();using(var file=new BinaryWriter(File.Create(path)))
        {file.Write(Encoding.ASCII.GetBytes("P6\n"+tex.width+" "+tex.height+"\n255\n"));for(int y=tex.height-1;y>=0;y--)for(int x=0;x<tex.width;x++){var c=pixels[y*tex.width+x];file.Write(c.r);file.Write(c.g);file.Write(c.b);}}
    }
    private static void CharacterPreview()
    {
        var objects=new List<GameObject>();var camera=Camera.main;int mask=camera.cullingMask;bool fog=RenderSettings.fog;
        var meshes=new List<Mesh>();var clear=camera.clearFlags;var background=camera.backgroundColor;
        var floorMaterial=new Material(Shader.Find("Standard")){color=new Color(.18f,.20f,.19f)};
        string[] names={"Player/PlayerPhase3","Enemies/Hunter","Enemies/Runner","Enemies/Ambusher","Enemies/Elite"};
        try
        {
            RenderSettings.fog=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.19f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(floor);floor.layer=31;
            floor.transform.position=new Vector3(0,-.06f,-20);floor.transform.localScale=new Vector3(20,.1f,16);floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
            for(int i=0;i<names.Length;i++)
            {
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+names[i]+".prefab"));objects.Add(go);
                foreach(var b in go.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
                foreach(var t in go.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
                go.transform.SetPositionAndRotation(new Vector3((i-2)*1.45f,.08f,-20),Quaternion.Euler(0,165,0));
                var a=go.GetComponentInChildren<Animator>();a.Rebind();a.Update(.01f);
                if(i==0){a.SetBool("IsGrounded",true);a.SetLayerWeight(a.GetLayerIndex("Weapon Carry"),1);a.Update(.01f);AttachPreviewWeapon(go);}
                float low=float.PositiveInfinity;
                foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {var mesh=new Mesh();meshes.Add(mesh);skin.BakeMesh(mesh);foreach(var v in mesh.vertices)low=Mathf.Min(low,skin.transform.TransformPoint(v).y);var baked=new GameObject("Baked pose",typeof(MeshFilter),typeof(MeshRenderer));baked.transform.SetParent(skin.transform,false);baked.layer=31;baked.GetComponent<MeshFilter>().sharedMesh=mesh;baked.GetComponent<Renderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;}
                go.transform.position+=Vector3.up*(-low);
            }
            Capture("Characters",new Vector3(.2f,1.8f,-26.6f),new Vector3(0,1,-20),60);
        }
        finally{foreach(var go in objects)Object.DestroyImmediate(go);foreach(var mesh in meshes)Object.DestroyImmediate(mesh);Object.DestroyImmediate(floorMaterial);camera.cullingMask=mask;camera.clearFlags=clear;camera.backgroundColor=background;RenderSettings.fog=fog;}
    }

    private static GameObject AttachPreviewWeapon(GameObject player)
    {
        var a=player.GetComponentInChildren<Animator>();
        var socket=player.GetComponentsInChildren<Transform>(true).First(t=>t.name=="WeaponSocket");
        var gun=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/Rifle.prefab"),socket);
        gun.transform.localPosition=Vector3.zero;gun.transform.localRotation=Quaternion.identity;
        foreach(var t in gun.GetComponentsInChildren<Transform>(true))t.gameObject.layer=player.layer;
        var view=gun.GetComponent<WeaponView>();view.AimAt(gun.transform.position+player.transform.forward*10,true);
        var upper=a.GetBoneTransform(HumanBodyBones.LeftUpperArm);var lower=a.GetBoneTransform(HumanBodyBones.LeftLowerArm);var hand=a.GetBoneTransform(HumanBodyBones.LeftHand);
        var target=view.SupportHand.position;var start=upper.position;float l1=Vector3.Distance(start,lower.position),l2=Vector3.Distance(lower.position,hand.position);
        var direction=(target-start).normalized;float d=Mathf.Clamp(Vector3.Distance(start,target),.01f,l1+l2-.003f),along=(l1*l1+d*d-l2*l2)/(2*d);
        var bend=Vector3.ProjectOnPlane(-player.transform.right*.7f-player.transform.forward*.25f-Vector3.up*.5f,direction).normalized;
        var elbow=start+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));var wrist=hand.rotation;
        upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,start+direction*d-lower.position)*lower.rotation;hand.rotation=wrist;
        return gun;
    }

    public static void FinishPresentation()
    {
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}))
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if(material.name.StartsWith("Survivor_Pants"))
            {material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(SurvivalistSetup.Root+"/Vendor/Texture/Bottom/T_Military_Pants2_color_skin3.png"));material.SetColor("_Color",new Color(.76f,.79f,.7f));EditorUtility.SetDirty(material);}
            if(ShaderUtil.ShaderHasError(material.shader))throw new Exception("Shader errors: "+material.shader.name);
        }
        RenderSettings.ambientEquatorColor=new Color(.35f,.39f,.39f);
        foreach(var l in Object.FindObjectsByType<Light>())if(l.type==LightType.Directional)l.shadowStrength=.72f;
        OutbreakEditor.Values(Camera.main.GetComponent<CityContactShadows>(),s=>s.FindProperty("strength").floatValue=.85f);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        Capture("After",new Vector3(.45f,2.7f,-23),new Vector3(0,3,12),60);
        Capture("Street",new Vector3(-2.3f,1.9f,-8),new Vector3(-9,4,14),62);
        CharacterPreview();
        File.AppendAllText("Logs/ReclaimedCityPresentation.txt","PASS: presentation material shaders compile; final fill light and contact shadow strength calibrated.\n");
    }
}
```


## ReferenceCityBuild

Full file path: D:/GV project/Assets/Editor/Art/ReferenceCityBuild.cs

```csharp
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

/// <summary>Builds the enhanced scene while retaining the original ZombieMap file.</summary>
public static class ReferenceCityBuild
{
    public static void Build()
    {
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        ExistingCityEnhancer.ValidateNavigation();
        Phase18Setup.Validate();
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Builds/Windows");
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path == OutbreakEditor.Scene ? ExistingCityEnhancer.ScenePath : s.path).Distinct().ToArray();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/Windows/ZombieOutbreak.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        string result = report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings + "\nSize: " + report.summary.totalSize + "\nTime: " + report.summary.totalTime;
        File.WriteAllText("Logs/WindowsBuildSummary.txt", result);
        if(report.summary.result != BuildResult.Succeeded)throw new Exception(result);
    }
}
```


## StealthLocomotionReview

Full file path: D:/GV project/Assets/Editor/Art/StealthLocomotionReview.cs

```csharp
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
```


## StealthLocomotionSetup

Full file path: D:/GV project/Assets/Editor/Art/StealthLocomotionSetup.cs

```csharp
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
using Object=UnityEngine.Object;

/// <summary>Retargets the licensed crouch motion to the actual player, preserving the corrected standing gaits.</summary>
public static class StealthLocomotionSetup
{
    public const string Root="Assets/Animations/StealthLocomotion";
    [MenuItem("Tools/Dead Zone/Apply reference locomotion")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Leave Play Mode first.");
        Phase2AnimationAssets.Folder(Root);
        string path=Root+"/Survivor.controller";
        if(AssetDatabase.LoadAssetAtPath<AnimatorController>(path)!=null)throw new Exception("Reference locomotion already installed; edit its existing assets.");
        var source=ImportedCharacterMotion.ImportClip(SurvivalistSetup.Root+"/Motion/Crouch_Walk.FBX");
        var forward=PlayerLocomotionRepair.Bake("CrouchForward",source,null,0,0,Root);
        var back=Object.Instantiate(forward);back.name="CrouchBackward";Reverse(back);AssetDatabase.CreateAsset(back,Root+"/CrouchBackward.anim");
        var idle=Object.Instantiate(forward);idle.name="CrouchIdle";
        float idleTime=BestIdleTime(forward);
        foreach(var binding in AnimationUtility.GetCurveBindings(idle))
            AnimationUtility.SetEditorCurve(idle,binding,AnimationCurve.Constant(0,2,AnimationUtility.GetEditorCurve(idle,binding).Evaluate(idleTime)));
        var idleSettings=AnimationUtility.GetAnimationClipSettings(idle);idleSettings.stopTime=2;AnimationUtility.SetAnimationClipSettings(idle,idleSettings);
        AssetDatabase.CreateAsset(idle,Root+"/CrouchIdle.anim");
        var sideLeft=CrouchedSide("CrouchLeftSource",AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerLocomotionRepair.Root+"/GroundedLeft.anim"));
        var sideRight=CrouchedSide("CrouchRightSource",AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerLocomotionRepair.Root+"/GroundedRight.anim"));
        var left=PlayerLocomotionRepair.Bake("CrouchLeft",sideLeft,null,0,0,Root);
        var right=PlayerLocomotionRepair.Bake("CrouchRight",sideRight,null,0,0,Root);
        Object.DestroyImmediate(sideLeft);Object.DestroyImmediate(sideRight);
        var clips=new[]{idle,forward,back,left,right};
        float[] stride={1,PlayerLocomotionRepair.MeasureStride(forward),PlayerLocomotionRepair.MeasureStride(back),PlayerLocomotionRepair.MeasureStride(left,Vector3.right),PlayerLocomotionRepair.MeasureStride(right,Vector3.right)};
        AssetDatabase.CopyAsset(PlayerLocomotionRepair.Root+"/PlayerGrounded.controller",path);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);controller.AddParameter("IsCrouching",AnimatorControllerParameterType.Bool);
        var machine=controller.layers[0].stateMachine;
        var normal=machine.states.First(s=>s.state.name=="Locomotion").state;
        var aim=machine.states.First(s=>s.state.name=="Aim Locomotion").state;
        var falling=machine.states.First(s=>s.state.name=="Falling").state;
        var crouch=machine.AddState("Crouch Locomotion");
        var tree=new BlendTree{name="Crouch direction",blendType=BlendTreeType.SimpleDirectional2D,blendParameter="Horizontal",blendParameterY="Vertical"};
        AssetDatabase.AddObjectToAsset(tree,controller);
        var positions=new[]{Vector2.zero,Vector2.up,Vector2.down,Vector2.left,Vector2.right};
        tree.children=Enumerable.Range(0,5).Select(i=>new ChildMotion{motion=clips[i],position=positions[i],timeScale=i==0?1:1.05f/stride[i]}).ToArray();crouch.motion=tree;
        foreach(var state in new[]{normal,aim})
        {
            foreach(var transition in state.transitions.Where(t=>t.destinationState==normal||t.destinationState==aim))transition.AddCondition(AnimatorConditionMode.IfNot,0,"IsCrouching");
            var enter=Link(state,crouch,.20f);Bool(enter,"IsCrouching",true);Bool(enter,"IsGrounded",true);
        }
        var stand=Link(crouch,normal,.20f);Bool(stand,"IsCrouching",false);Bool(stand,"IsAiming",false);Bool(stand,"IsGrounded",true);
        var standAim=Link(crouch,aim,.20f);Bool(standAim,"IsCrouching",false);Bool(standAim,"IsAiming",true);Bool(standAim,"IsGrounded",true);
        var fall=Link(crouch,falling,.08f);Bool(fall,"IsGrounded",false);
        var landed=Link(falling,crouch,.14f);Bool(landed,"IsGrounded",true);Bool(landed,"IsCrouching",true);
        foreach(var transition in falling.transitions.Where(t=>t.destinationState!=crouch))if(transition.conditions.Any(c=>c.parameter=="IsGrounded"))transition.AddCondition(AnimatorConditionMode.IfNot,0,"IsCrouching");
        var carryMachine=controller.layers.First(l=>l.name=="Weapon Carry").stateMachine;
        var raised=carryMachine.defaultState;
        var low=carryMachine.AddState("Low Ready");
        low.motion=ImportedShooterPoses.Create("LowReady",2,true,SurvivalistSetup.Model,Root);carryMachine.defaultState=low;
        Bool(Link(low,raised,.20f),"IsAiming",true);Bool(Link(raised,low,.24f),"IsAiming",false);
        var previous=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(PlayerLocomotionRepair.Root+"/PlayerGrounded.overrideController");
        var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();previous.GetOverrides(pairs);
        var overrides=new AnimatorOverrideController(controller){name="ReferenceSurvivor"};
        foreach(var pair in pairs)if(pair.Value!=null&&controller.animationClips.Any(c=>c.name==pair.Key.name))overrides[pair.Key.name]=pair.Value;
        AssetDatabase.CreateAsset(overrides,Root+"/ReferenceSurvivor.overrideController");
        EditorUtility.SetDirty(controller);EditorUtility.SetDirty(tree);AssetDatabase.SaveAssets();
        var player=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player/PlayerPhase3.prefab");
        try{Configure(player,overrides);PrefabUtility.SaveAsPrefabAsset(player,"Assets/Prefabs/Player/PlayerPhase3.prefab");}finally{PrefabUtility.UnloadPrefabContents(player);}
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);Configure(Object.FindAnyObjectByType<ThirdPersonPlayerController>().gameObject,overrides);
        var camera=Camera.main.GetComponent<ThirdPersonCameraController>();OutbreakEditor.Values(camera,s=>{s.FindProperty("modeBlendTime").floatValue=.24f;s.FindProperty("positionSmoothTime").floatValue=.10f;});
        foreach(var mode in Object.FindObjectsByType<ThirdPersonCameraMode>())if(mode.name.Contains("Exploration"))OutbreakEditor.Values(mode,s=>{s.FindProperty("distance").floatValue=3.1f;s.FindProperty("shoulderOffset").vector2Value=new Vector2(.48f,.06f);s.FindProperty("fieldOfView").floatValue=58;});
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/StealthLocomotionCalibration.txt",string.Join("\n",Enumerable.Range(0,5).Select(i=>clips[i].name+": stance speed="+stride[i]))+"\nIdle sample time="+idleTime);
        RefineCrouch();
        PrepareFootContact();
    }
    private static void Configure(GameObject player,RuntimeAnimatorController controller)
    {
        if(player.GetComponent<PlayerStanceController>()==null)player.AddComponent<PlayerStanceController>();
        player.GetComponentInChildren<Animator>().runtimeAnimatorController=controller;
        OutbreakEditor.Values(player.GetComponent<ThirdPersonPlayerController>(),s=>{s.FindProperty("acceleration").floatValue=9;s.FindProperty("braking").floatValue=13;s.FindProperty("turnSmoothTime").floatValue=.12f;});
    }
    private static AnimatorStateTransition Link(AnimatorState from,AnimatorState to,float duration)
    {
        var transition=from.AddTransition(to);transition.hasExitTime=false;transition.hasFixedDuration=true;transition.duration=duration;
        transition.interruptionSource=TransitionInterruptionSource.SourceThenDestination;Bool(transition,"IsDead",false);return transition;
    }
    private static void Bool(AnimatorStateTransition transition,string name,bool value)=>transition.AddCondition(value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,name);
    private static void Reverse(AnimationClip clip)
    {
        float length=clip.length;
        foreach(var binding in AnimationUtility.GetCurveBindings(clip))
        {var curve=AnimationUtility.GetEditorCurve(clip,binding);curve.keys=curve.keys.Reverse().Select(k=>new Keyframe(length-k.time,k.value)).ToArray();Linear(curve);AnimationUtility.SetEditorCurve(clip,binding,curve);}
    }
    private static GameObject Rig(out Animator a,out PlayableGraph graph,out AnimationPlayableOutput output,AnimationClip source,out AnimationClipPlayable playable)
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerPhase3.prefab"));foreach(var b in go.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
        go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);a=go.GetComponentInChildren<Animator>();a.transform.localPosition=Vector3.zero;a.runtimeAnimatorController=null;a.applyRootMotion=false;
        graph=PlayableGraph.Create("Crouch retargeting");output=AnimationPlayableOutput.Create(graph,"Pose",a);playable=AnimationClipPlayable.Create(graph,source);playable.SetApplyFootIK(false);output.SetSourcePlayable(playable);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();return go;
    }
    private static float BestIdleTime(AnimationClip source)
    {
        var go=Rig(out var a,out var graph,out _,source,out var playable);
        try{float best=float.PositiveInfinity,time=0;for(int i=0;i<64;i++){float t=i*source.length/64;playable.SetTime(t);graph.Evaluate(0);float score=Mathf.Abs(a.GetBoneTransform(HumanBodyBones.LeftFoot).position.y-a.GetBoneTransform(HumanBodyBones.RightFoot).position.y);if(score<best){best=score;time=t;}}return time;}
        finally{graph.Destroy();Object.DestroyImmediate(go);}
    }
    private static AnimationClip CrouchedSide(string name,AnimationClip source,float lower=.38f,float lean=22)
    {
        var go=Rig(out var a,out var graph,out _,source,out var playable);
        try
        {
            var curves=Enumerable.Range(0,HumanTrait.MuscleCount+7).Select(_=>new AnimationCurve()).ToArray();
            using(var handler=new HumanPoseHandler(a.avatar,a.transform))
            for(int frame=0;frame<=120;frame++)
            {
                float time=frame*source.length/120;playable.SetTime(time);graph.Evaluate(0);
                var left=a.GetBoneTransform(HumanBodyBones.LeftFoot);var right=a.GetBoneTransform(HumanBodyBones.RightFoot);
                var lp=left.position;var rp=right.position;var lr=left.rotation;var rr=right.rotation;
                a.GetBoneTransform(HumanBodyBones.Hips).position-=Vector3.up*lower;
                var chest=a.GetBoneTransform(HumanBodyBones.Chest);chest.rotation=Quaternion.AngleAxis(lean,Vector3.right)*chest.rotation;
                SolveLeg(a,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,lp);left.rotation=lr;
                SolveLeg(a,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,rp);right.rotation=rr;
                var pose=new HumanPose();handler.GetHumanPose(ref pose);
                for(int i=0;i<HumanTrait.MuscleCount;i++)curves[i].AddKey(time,pose.muscles[i]);int k=HumanTrait.MuscleCount;
                for(int i=0;i<3;i++)curves[k+i].AddKey(time,pose.bodyPosition[i]);for(int i=0;i<4;i++)curves[k+3+i].AddKey(time,pose.bodyRotation[i]);
            }
            var clip=new AnimationClip{name=name,frameRate=120};
            var names=HumanTrait.MuscleName.Concat(new[]{"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"}).ToArray();
            for(int i=0;i<curves.Length;i++){string property=names[i];foreach(string side in new[]{"Left","Right"})foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Little"}){string prefix=side+" "+finger+" ";if(property.StartsWith(prefix))property=side+"Hand."+finger+"."+property.Substring(prefix.Length);}Linear(curves[i]);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property),curves[i]);}
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.startTime=0;settings.stopTime=source.length;settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);return clip;
        }
        finally{graph.Destroy();Object.DestroyImmediate(go);}
    }
    private static void SolveLeg(Animator a,HumanBodyBones upperId,HumanBodyBones lowerId,HumanBodyBones footId,Vector3 target)
    {
        var upper=a.GetBoneTransform(upperId);var lower=a.GetBoneTransform(lowerId);var foot=a.GetBoneTransform(footId);var start=upper.position;
        float l1=Vector3.Distance(start,lower.position),l2=Vector3.Distance(lower.position,foot.position);var dir=(target-start).normalized;
        float d=Mathf.Clamp(Vector3.Distance(start,target),Mathf.Abs(l1-l2)+.001f,l1+l2-.003f),along=(l1*l1+d*d-l2*l2)/(2*d);
        var bend=Vector3.ProjectOnPlane(Vector3.forward,dir).normalized;var knee=start+dir*along+bend*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
        upper.rotation=Quaternion.FromToRotation(lower.position-start,knee-start)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(foot.position-lower.position,start+dir*d-lower.position)*lower.rotation;
    }
    private static void Linear(AnimationCurve curve){for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}}

    public static void RefineCrouch()
    {
        var native=ImportedCharacterMotion.ImportClip(SurvivalistSetup.Root+"/Motion/Crouch_Walk.FBX");
        var calibrated=PlayerLocomotionRepair.Bake("CrouchSource",native,null,0,0,Root);
        var temp=CrouchedSide("Lowered native crouch",calibrated,.12f,6);
        var forward=PlayerLocomotionRepair.Bake("CrouchForward",temp,null,0,0,Root);Object.DestroyImmediate(temp);
        var reverse=Object.Instantiate(forward);Reverse(reverse);
        var backward=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/CrouchBackward.anim");EditorUtility.CopySerialized(reverse,backward);backward.name="CrouchBackward";Object.DestroyImmediate(reverse);
        // A two-foot standing source yields a planted idle, unlike freezing a walking stride.
        temp=CrouchedSide("Planted crouch idle",AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerLocomotionRepair.Root+"/GroundedIdle.anim"));
        var idle=PlayerLocomotionRepair.Bake("CrouchIdle",temp,null,0,0,Root);Object.DestroyImmediate(temp);
        temp=CrouchedSide("Crouch left",AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerLocomotionRepair.Root+"/GroundedLeft.anim"));
        var left=PlayerLocomotionRepair.Bake("CrouchLeft",temp,null,0,0,Root);Object.DestroyImmediate(temp);
        temp=CrouchedSide("Crouch right",AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerLocomotionRepair.Root+"/GroundedRight.anim"));
        var right=PlayerLocomotionRepair.Bake("CrouchRight",temp,null,0,0,Root);Object.DestroyImmediate(temp);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Root+"/Survivor.controller");
        var tree=(BlendTree)controller.layers[0].stateMachine.states.First(s=>s.state.name=="Crouch Locomotion").state.motion;
        var clips=new[]{idle,forward,backward,left,right};var children=tree.children;var report=new System.Text.StringBuilder();
        for(int i=1;i<5;i++){float stride=PlayerLocomotionRepair.MeasureStride(clips[i],i>=3?Vector3.right:Vector3.forward);children[i].timeScale=1.05f/stride;report.AppendLine(clips[i].name+" stance speed="+stride);}
        tree.children=children;EditorUtility.SetDirty(tree);EditorUtility.SetDirty(backward);
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player/PlayerPhase3.prefab");
        try{OutbreakEditor.Values(prefab.GetComponent<PlayerStanceController>(),s=>s.FindProperty("crouchedHeight").floatValue=1.55f);PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/Player/PlayerPhase3.prefab");}finally{PrefabUtility.UnloadPrefabContents(prefab);}
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        OutbreakEditor.Values(Object.FindAnyObjectByType<PlayerStanceController>(),s=>s.FindProperty("crouchedHeight").floatValue=1.55f);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/StealthLocomotionCalibration.txt",report.ToString()+"Planted crouch idle; capsule height=1.55m; feet retained during pelvis lowering.\n");
    }
    public static void PrepareFootContact()
    {
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player/PlayerPhase3.prefab");
        try{prefab.GetComponentInChildren<HumanoidFootPlacement>().BakeSoles();PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/Player/PlayerPhase3.prefab");}finally{PrefabUtility.UnloadPrefabContents(prefab);}
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        var feet=Object.FindAnyObjectByType<ThirdPersonPlayerController>().GetComponentInChildren<HumanoidFootPlacement>();feet.BakeSoles();EditorUtility.SetDirty(feet);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
    }
}
```


## StealthPosePreview

Full file path: D:/GV project/Assets/Editor/Art/StealthPosePreview.cs

```csharp
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
```


## StealthReferenceDressing

Full file path: D:/GV project/Assets/Editor/Art/StealthReferenceDressing.cs

```csharp
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
using Leaves=ReclaimedCityPresentation.Leaves;

/// <summary>Video-reference surface detail, inside existing street-edge and facade footprints.</summary>
public static class StealthReferenceDressing
{
    private static readonly System.Random random=new System.Random(22941);
    private static float Range(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
    private static Color Green()=>Color.Lerp(new Color(.085f,.16f,.035f),new Color(.26f,.35f,.075f),Range(0,1));
    [MenuItem("Tools/Dead Zone/Apply stealth reference environment detail")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Leave Play Mode first.");
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        var district=GameObject.Find("OutbreakDistrict").transform;
        if(district.Find("Reference foliage detail")!=null)throw new Exception("Reference dressing is already present; edit the existing group.");
        var transforms=district.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale));
        var colliders=district.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
        var nav=district.GetComponent<NavMeshSurface>().navMeshData;
        Directory.CreateDirectory("Previews/StealthReference");
        ReclaimedCityPresentation.Capture("StealthBefore",new Vector3(.45f,2.7f,-23),new Vector3(0,3,12),60);
        var root=new GameObject("Reference foliage detail").transform;root.SetParent(district,false);
        var foliage=AssetDatabase.LoadAssetAtPath<Material>(ReclaimedCityPresentation.Root+"/Materials/Foliage.mat");
        var litter=new Material(foliage){name="Dry leaf litter"};litter.SetFloat("_Wind",0);AssetDatabase.CreateAsset(litter,ReclaimedCityPresentation.Root+"/Materials/Leaf litter.mat");
        Physics.SyncTransforms();
        for(int zone=0;zone<8;zone++)
        {
            var ferns=new Leaves();var groundLeaves=new Leaves();float start=-34+zone*8;
            for(int patch=0;patch<12;patch++)
            {
                float side=patch%2==0?-1:1;var point=new Vector3(side*Range(5.7f,7.5f),3,start+Range(0,8));
                if(!Physics.Raycast(point,Vector3.down,out var hit,4,1<<8,QueryTriggerInteraction.Ignore)||hit.point.y>.5f)continue;
                Fern(ferns,hit.point,Range(.25f,.45f));
                for(int leaf=0;leaf<28;leaf++)
                {
                    var p=hit.point+new Vector3(Range(-.65f,.65f),.008f,Range(-.65f,.65f));
                    float angle=Range(0,Mathf.PI*2);var dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    groundLeaves.Leaf(p,dir,Vector3.Cross(dir,Vector3.up),Range(.055f,.16f),Range(.018f,.045f),Color.Lerp(new Color(.12f,.08f,.035f),new Color(.33f,.24f,.085f),Range(0,1)));
                }
            }
            ferns.Save(root,"Curb ferns "+zone,foliage);groundLeaves.Save(root,"Leaf litter "+zone,litter);
        }
        int id=0;
        foreach(float side in new[]{-1f,1f})foreach(float z in new[]{-32f,-17f,4f,23f})
        {
            var point=new Vector3(side*10.7f,4,z);
            if(!Physics.Raycast(point,Vector3.down,out var hit,5,1<<8,QueryTriggerInteraction.Ignore)||hit.point.y>.6f)continue;
            var bush=new Leaves();
            for(int stem=0;stem<20;stem++)
            {
                float angle=Range(0,Mathf.PI*2);var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var basePoint=hit.point+radial*Range(0,.3f);var end=basePoint+Vector3.up*Range(.3f,.65f)+radial*.22f;
                for(int k=1;k<=7;k++)
                {
                    var p=Vector3.Lerp(basePoint,end,k/7f);float theta=angle+k*2.3f;var dir=new Vector3(Mathf.Cos(theta),.35f,Mathf.Sin(theta));
                    bush.Leaf(p,dir,Vector3.Cross(dir,Vector3.up),Range(.12f,.24f),Range(.045f,.075f),Green());
                }
            }
            bush.Save(root,"Wall base shrubs "+id++,foliage);
        }
        id=0;
        foreach(var face in district.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Weathered facade").ToArray())
        {
            var ledges=face.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Floor ledge").ToArray();if(ledges.Length==0)continue;
            float width=ledges.Max(t=>t.localScale.x),height=ledges.Max(t=>t.localPosition.y)+1;
            var ivy=new Leaves();
            for(int patch=0;patch<18;patch++)
            {
                float sign=patch%2==0?-1:1;float cx=width*sign*Range(.31f,.43f),cy=Range(3,Mathf.Max(4,height));
                for(int leaf=0;leaf<45;leaf++)
                {
                    float angle=Range(0,Mathf.PI*2),r=Mathf.Sqrt(Range(0,1));
                    var p=face.TransformPoint(new Vector3(cx+Mathf.Cos(angle)*r*.65f,cy+Mathf.Sin(angle)*r*.9f,-Range(.20f,.31f)));
                    var d=face.TransformDirection(new Vector3(Range(-.8f,.8f),Range(.2f,1),-.05f));
                    var side=Vector3.Cross(face.forward,d).normalized;
                    ivy.Leaf(p,d,side,Range(.15f,.29f),Range(.06f,.115f),Green());
                }
            }
            ivy.Save(root,"Layered facade ivy "+id++,foliage);
        }
        foreach(var material in AssetDatabase.FindAssets("t:Material",new[]{ReclaimedCityPresentation.Root+"/Materials"}).Select(g=>AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))))
        {
            string name=material.name.ToLowerInvariant();
            if(name.Contains("asphalt")&&material.HasProperty("_Wetness")){material.SetColor("_Color",new Color(.57f,.60f,.55f));material.SetFloat("_Wetness",.42f);material.SetFloat("_Moss",.88f);}
            if(name.Contains("puddle")||name.Contains("rainwater"))PolishWater(material);
            EditorUtility.SetDirty(material);
        }
        foreach(var entry in transforms)if(entry.Key==null||Matrix4x4.TRS(entry.Key.localPosition,entry.Key.localRotation,entry.Key.localScale)!=entry.Value)throw new Exception("Existing layout changed.");
        foreach(var entry in colliders)if(entry.Key==null||EditorJsonUtility.ToJson(entry.Key)!=entry.Value)throw new Exception("Existing collider changed.");
        if(district.GetComponentsInChildren<Collider>(true).Length!=colliders.Count||district.GetComponent<NavMeshSurface>().navMeshData!=nav)throw new Exception("Navigation/physics changed.");
        ExistingCityEnhancer.ValidateNavigation();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/StealthReferenceLayout.txt","PASS: "+transforms.Count+" existing environment transforms unchanged; "+colliders.Count+" collider definitions unchanged; same navigation data.\nAdditional foliage triangles="+root.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.triangles.Length/3));
        ReclaimedCityPresentation.Capture("StealthAfter",new Vector3(.45f,2.7f,-23),new Vector3(0,3,12),60);
        ReclaimedCityPresentation.Capture("StealthStreet",new Vector3(-2.3f,1.9f,-8),new Vector3(-9,4,14),62);
    }
    private static void PolishWater(Material material)
    {
        // The puddle uses the same photographic asphalt as the road, so keep its base tint.
        material.SetColor("_Color",new Color(.57f,.60f,.55f));
        if(material.HasProperty("_Wetness"))material.SetFloat("_Wetness",.55f);
        if(material.HasProperty("_SmoothnessFloor"))material.SetFloat("_SmoothnessFloor",.68f);
        if(material.HasProperty("_NormalStrength"))material.SetFloat("_NormalStrength",.2f);
        if(material.HasProperty("_Moss"))material.SetFloat("_Moss",.88f);
        EditorUtility.SetDirty(material);
    }
    public static void RefineWater()
    {
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{ReclaimedCityPresentation.Root+"/Materials"}))
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            string name=material.name.ToLowerInvariant();if(name.Contains("puddle")||name.Contains("rainwater"))PolishWater(material);
        }
        AssetDatabase.SaveAssets();
        ReclaimedCityPresentation.Capture("StealthAfter",new Vector3(.45f,2.7f,-23),new Vector3(0,3,12),60);
        ReclaimedCityPresentation.Capture("StealthStreet",new Vector3(-2.3f,1.9f,-8),new Vector3(-9,4,14),62);
    }
    private static void Fern(Leaves mesh,Vector3 p,float height)
    {
        for(int frond=0;frond<7;frond++)
        {
            float angle=frond*Mathf.PI*2/7+Range(-.15f,.15f);var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            var side=Vector3.Cross(radial,Vector3.up);
            for(int k=1;k<=8;k++)
            {
                float u=k/9f;var origin=p+Vector3.up*(Mathf.Sin(u*Mathf.PI*.75f)*height)+radial*(u*height*1.15f);
                float length=Mathf.Sin(u*Mathf.PI)*height*.32f;
                foreach(float sign in new[]{-1f,1f})mesh.Leaf(origin,side*sign+radial*.3f+Vector3.up*.12f,radial,length,length*.18f,Green());
            }
        }
    }
}
```


## SurvivalistSetup

Full file path: D:/GV project/Assets/Editor/Art/SurvivalistSetup.cs

```csharp
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Player-only import and assembly for the selected Slayver Survivalist (P2).</summary>
public static class SurvivalistSetup
{
    public const string Root = "Assets/Art/ImportedCharacters/Survivalist";
    public const string MotionRoot = "Assets/Animations/Survivalist";
    public const string Model = Root + "/Vendor/Prefab/Survivalist (1).prefab";

    [MenuItem("Zombie Outbreak/Art/Apply selected P2 Survivalist player")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before applying P2.");
        EditorSceneManager.SaveOpenScenes();
        Phase2AnimationAssets.Folder(MotionRoot);
        CorrectPelvis((ModelImporter)AssetImporter.GetAtPath(Root + "/Vendor/Basemesh/SK_Military_Survivalist.fbx"));
        PrepareMotion();
        BuildController();
        OutbreakEditor.Open();
        Replace(Object.FindAnyObjectByType<ThirdPersonPlayerController>().gameObject);
        var prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player/PlayerPhase3.prefab");
        try { Replace(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Prefabs/Player/PlayerPhase3.prefab"); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        OutbreakEditor.Save("P2_SURVIVALIST");
    }

    private static void PrepareMotion()
    {
        foreach (string file in Directory.GetFiles(Root + "/Motion"))
        {
            if (!file.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
            string path = file.Replace('\\', '/');
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            CorrectPelvis(importer);
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = Path.GetFileNameWithoutExtension(path);
                clip.loopTime = clip.name != "Death" && clip.name != "Jump";
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = false;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = false;
                clip.lockRootHeightY = clip.name != "Death";
                clip.keepOriginalPositionY = false;
                clip.heightFromFeet = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static void CorrectPelvis(ModelImporter importer)
    {
        var description = importer.humanDescription;
        if (!description.skeleton.Any(b => b.name == "pelvis")) return;
        var bones = description.human;
        int index = Array.FindIndex(bones, b => b.humanName == "Hips");
        if (index < 0 || bones[index].boneName == "pelvis") return;
        bones[index].boneName = "pelvis";
        description.human = bones;
        importer.humanDescription = description;
        importer.SaveAndReimport();
    }

    private static AnimationClip Clip(string file) => ImportedCharacterMotion.ImportClip(Root + "/Motion/" + file);

    private static void BuildController()
    {
        string controllerPath = MotionRoot + "/Survivalist.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) == null)
            AssetDatabase.CopyAsset(ImportedCharacterSetup.MotionRoot + "/Survivor.controller", controllerPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        var layers = controller.layers;
        layers[0].iKPass = true;
        // The authored arm pose has no body-translation curves. It belongs ONLY on
        // the masked carry layer; using it as full-body aim idle sinks the hips.
        var carryClip = ImportedShooterPoses.Create("Carry", 2.4f, true, Model, MotionRoot);
        layers.First(l => l.name == "Weapon Carry").stateMachine.defaultState.motion = carryClip;
        controller.layers = layers;
        var normal = layers[0].stateMachine.states.First(s => s.state.name == "Locomotion").state;
        var tree = (BlendTree)normal.motion;
        var children = tree.children;
        // One speed blend: nesting an idle/walk tree here blends idle twice and makes feet slide.
        children[1].motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(Phase2AnimationAssets.Root + "/Clips/WalkForward.anim");
        // Calibrated against median planted-foot travel on this Avatar (metres/second).
        children[1].timeScale = 1.6f / 1.414194f;
        children[2].timeScale = 3.6f / 3.244947f;
        children[3].timeScale = 6f / 5.991491f;
        tree.children = children;
        var aimingTree = (BlendTree)layers[0].stateMachine.states.First(s => s.state.name == "Aim Locomotion").state.motion;
        var directions = aimingTree.children;
        float[] stanceSpeeds = { 1f, 1.346661f, 1.494087f, .8224851f, .8242143f };
        for (int i = 1; i < directions.Length; i++) directions[i].timeScale = 1.25f / stanceSpeeds[i];
        aimingTree.children = directions;
        EditorUtility.SetDirty(aimingTree);
        foreach (var layer in layers)
            foreach (var state in layer.stateMachine.states)
                foreach (var transition in state.state.transitions)
                {
                    transition.hasFixedDuration = true;
                    transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
                }

        string path = MotionRoot + "/Survivalist.overrideController";
        var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (overrides == null) { overrides = new AnimatorOverrideController(controller); AssetDatabase.CreateAsset(overrides, path); }
        overrides.runtimeAnimatorController = controller;
        var old = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ImportedCharacterSetup.MotionRoot + "/Survivor.overrideController");
        var previous = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
        old.GetOverrides(previous);
        foreach (var baseClip in controller.animationClips.Distinct())
        {
            var pair = previous.FirstOrDefault(p => p.Key.name == baseClip.name);
            if (pair.Value != null) overrides[baseClip.name] = pair.Value;
        }
        overrides["Idle"] = Clip("Idle.fbx");
        overrides["WalkForward"] = Clip("Walk.FBX");
        overrides["Run"] = Clip("Run.FBX");
        overrides["Jump"] = Clip("Jump.FBX");
        overrides["Death"] = Clip("Death.fbx");
        overrides["AimIdle"] = Clip("Idle.fbx");
        overrides["Fire"] = ImportedShooterPoses.Create("ImportedFire", .18f, false, Model, MotionRoot);
        overrides["Reload"] = ImportedShooterPoses.Create("ImportedReload", 1.2f, false, Model, MotionRoot);
        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(overrides);
        AssetDatabase.SaveAssets();
    }

    private static void Replace(GameObject player)
    {
        var old = player.GetComponentInChildren<Animator>(true);
        var nextObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model), player.transform);
        PrefabUtility.UnpackPrefabInstance(nextObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        nextObject.name = "SurvivalistVisual";
        nextObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        nextObject.transform.localScale = Vector3.one;
        var next = nextObject.GetComponentInChildren<Animator>(true);
        if (next == null || next.avatar == null || !next.avatar.isValid || !next.avatar.isHuman)
            throw new InvalidOperationException("P2 requires a valid Humanoid Avatar.");
        next.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MotionRoot + "/Survivalist.overrideController");
        next.applyRootMotion = false;
        next.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (Transform child in nextObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = player.layer;
        foreach (var r in nextObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.name.Contains("FPS")) r.gameObject.SetActive(false);
            if (r.name == "SK_Military_Vest1_4" || r.name == "SK_Military_Cap1" || r.name == "SK_Military_Backpack1_vest") r.gameObject.SetActive(true);
            r.updateWhenOffscreen = true;
            foreach (var m in r.sharedMaterials)
                if (m == null || m.shader == null) throw new InvalidOperationException("Missing P2 material on " + r.name);
        }
        next.Rebind();
        next.SetBool("IsGrounded", true);
        next.Play("Locomotion", 0, 0f);
        next.Update(.001f);
        var weapons = player.GetComponent<PlayerWeaponController>();
        if (weapons != null)
        {
            var socket = new SerializedObject(weapons).FindProperty("weaponSocket").objectReferenceValue as Transform;
            if (socket == null) socket = new GameObject("WeaponSocket").transform;
            var hand = next.GetBoneTransform(HumanBodyBones.RightHand);
            socket.SetParent(hand, false);
            socket.position = hand.position + player.transform.forward * .04f;
            socket.rotation = player.transform.rotation;
            socket.localScale = Vector3.one;
            OutbreakEditor.Set(weapons, "weaponSocket", socket);
        }
        var driver = player.GetComponent<PlayerAnimatorDriver>();
        OutbreakEditor.Set(driver, "animator", next);
        // The motor already eases speed; a second long parameter lag would delay the feet.
        OutbreakEditor.Values(driver, s => s.FindProperty("parameterDampTime").floatValue = .035f);
        OutbreakEditor.Values(player.GetComponent<ThirdPersonPlayerController>(), s =>
        {
            s.FindProperty("acceleration").floatValue = 14f;
            s.FindProperty("braking").floatValue = 20f;
            s.FindProperty("turnSmoothTime").floatValue = .09f;
            s.FindProperty("walkSpeed").floatValue = 1.6f;
            s.FindProperty("runSpeed").floatValue = 3.6f;
            s.FindProperty("sprintSpeed").floatValue = 6f;
        });
        OutbreakEditor.Values(player.GetComponent<PlayerAimController>(), s => s.FindProperty("aimMoveSpeed").floatValue = 1.25f);
        next.gameObject.AddComponent<SupportHandGrip>();
        next.gameObject.AddComponent<ShooterBodyPose>();
        next.gameObject.AddComponent<HumanoidFootPlacement>();
        if (old != null) Object.DestroyImmediate(old.gameObject);
    }

    public static void Inspect()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new StringBuilder();
        foreach (string file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
        {
            string path = file.Replace('\\', '/');
            if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.importCameras = importer.importLights = false;
            importer.SaveAndReimport();
            CorrectPelvis(importer);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var a = source.GetComponent<Animator>();
            report.AppendLine(path + " human=" + (a != null && a.avatar != null && a.avatar.isHuman && a.avatar.isValid));
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                report.AppendLine("  " + clip.name + " length=" + clip.length + " velocity=" + clip.averageSpeed + " human=" + clip.isHumanMotion);
            foreach (var mesh in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                report.AppendLine("  mesh " + mesh.name + " bounds=" + mesh.bounds + " mats=" + string.Join(",", mesh.sharedMaterials.Select(m => m != null ? m.name : "null")));
        }
        File.WriteAllText("Logs/SurvivalistImport.txt", report.ToString());
        Debug.Log(report.ToString());
    }
}
```


## WeaponEquipmentArt

Full file path: D:/GV project/Assets/Editor/Art/WeaponEquipmentArt.cs

```csharp
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
```


## EnemyPrefabFactory

Full file path: D:/GV project/Assets/Editor/Outbreak/EnemyPrefabFactory.cs

```csharp
using UnityEditor;
using UnityEngine;

public static class EnemyPrefabFactory
{
    public static void Create<T>(string name, float health, Color color, float scale = 1)
        where T : Component
    {
        string path = "Assets/Prefabs/Enemies/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Phase7Setup.BasePrefab));
        go.name = name;
        go.AddComponent<T>();
        OutbreakEditor.Values(go.GetComponent<EnemyHealth>(), s => s.FindProperty("maxHealth").floatValue = health);
        var visual = go.transform.Find("Visual");
        visual.localScale = Vector3.one * scale;
        var mat = OutbreakEditor.Material(name + "Skin", color);
        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterial = mat;
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }
}
```


## OutbreakCapture

Full file path: D:/GV project/Assets/Editor/Outbreak/OutbreakCapture.cs

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Native camera/Canvas capture used only by Editor verification tools.</summary>
public static class OutbreakCapture
{
    public static void Save(string name, int width = 1280, int height = 720)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            return;
        Camera camera = Camera.main;
        if (camera == null)
            camera = Object.FindAnyObjectByType<Camera>();
        var previous = camera.targetTexture;
        var previousActive = RenderTexture.active;
        float aspect = camera.aspect;
        var render = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var changed = new List<System.Action>();
        try
        {
            camera.targetTexture = render;
            camera.aspect = (float)width / height;
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    continue;
                var current = canvas;
                var oldCamera = canvas.worldCamera;
                float plane = canvas.planeDistance, scale = canvas.scaleFactor;
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                bool scalerEnabled = scaler != null && scaler.enabled;
                if (scaler != null)
                    scaler.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .5f;
                canvas.scaleFactor = Mathf.Sqrt(width / 1920f * height / 1080f);
                changed.Add(() =>
                {
                    current.renderMode = RenderMode.ScreenSpaceOverlay;
                    current.worldCamera = oldCamera;
                    current.planeDistance = plane;
                    current.scaleFactor = scale;
                    if (scaler != null)
                        scaler.enabled = scalerEnabled;
                });
            }

            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = render;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            var pixels = image.GetPixels32();
            Directory.CreateDirectory("Logs/Captures");
            using (var writer = new BinaryWriter(File.Create("Logs/Captures/" + name + ".ppm")))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("P6\n" + width + " " + height + "\n255\n"));
                for (int y = height - 1; y >= 0; y--)
                    for (int x = 0; x < width; x++)
                    {
                        var pixel = pixels[y * width + x];
                        writer.Write(pixel.r);
                        writer.Write(pixel.g);
                        writer.Write(pixel.b);
                    }
            }
        }
        finally
        {
            foreach (var restore in changed)
                restore();
            camera.targetTexture = previous;
            camera.aspect = aspect;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(image);
            render.Release();
            Object.DestroyImmediate(render);
        }
    }
}
```


## OutbreakEditor

Full file path: D:/GV project/Assets/Editor/Outbreak/OutbreakEditor.cs

```csharp
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Shared native asset helpers. Builders never hand-edit Unity YAML.</summary>
public static class OutbreakEditor
{
    public const string Scene = "Assets/Scenes/ZombieMap.unity";
    public static void Open() => EditorSceneManager.OpenScene(Scene);
    public static void Save(string phase)
    {
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("PHASE_" + phase + "_PASS");
    }

    public static T One<T>()
        where T : UnityEngine.Object => UnityEngine.Object.FindAnyObjectByType<T>();
    public static T Add<T>(GameObject go)
        where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    public static Transform Group(string name, Transform parent = null)
    {
        var found = parent != null ? parent.Find(name) : GameObject.Find(name)?.transform;
        if (found != null)
            return found;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    public static Material Material(string name, Color color)
    {
        Phase2AnimationAssets.Folder("Assets/Materials/Outbreak");
        string path = "Assets/Materials/Outbreak/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
            return mat;
        mat = new Material(Shader.Find("Standard"))
        {
            name = name,
            color = color
        };
        mat.SetFloat("_Glossiness", 0.15f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    public static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.layer = 8;
        return go;
    }

    public static void Set(UnityEngine.Object owner, string name, UnityEngine.Object value) => Phase4UIFactory.Ref(owner, name, value);
    public static void Values(UnityEngine.Object owner, Action<SerializedObject> edit)
    {
        var so = new SerializedObject(owner);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(owner);
    }

    public static void Layer(int id, string name)
    {
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        tags.FindProperty("layers").GetArrayElementAtIndex(id).stringValue = name;
        tags.ApplyModifiedProperties();
    }

    public static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
```


## Phase10Setup

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase10Setup.cs

```csharp
using UnityEditor;
using UnityEngine;

public static class Phase10Setup
{
    [MenuItem("Zombie Outbreak/Phase 10 - Tactical Ambusher")]
    public static void Build()
    {
        OutbreakEditor.Open();
        EnemyPrefabFactory.Create<AmbusherBrain>("Ambusher", 70, new Color(.37f, .3f, .48f));
        OutbreakEditor.Require(OutbreakEditor.One<EnemyWorld>().tacticalPoints.Length >= 6, "Ambusher requires authored tactical points.");
        OutbreakEditor.Save("10");
    }
}
```


## Phase11Setup

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase11Setup.cs

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class Phase11Setup
{
    [MenuItem("Zombie Outbreak/Phase 11 - Elite commander")]
    public static void Build()
    {
        OutbreakEditor.Open();
        EnemyPrefabFactory.Create<EliteBrain>("Elite", 350, new Color(.54f, .16f, .17f), 1.25f);
        const string path = "Assets/Prefabs/Enemies/Elite.prefab";
        var go = PrefabUtility.LoadPrefabContents(path);
        var capsule = go.GetComponent<CapsuleCollider>();
        capsule.height = 2.4f;
        capsule.center = Vector3.up * 1.2f;
        capsule.radius = .5f;
        var nav = go.GetComponent<NavMeshAgent>();
        nav.height = 2.4f;
        nav.radius = .5f;
        OutbreakEditor.Values(go.GetComponent<EnemyCombat>(), s =>
        {
            s.FindProperty("range").floatValue = 2.4f;
            s.FindProperty("damage").floatValue = 16;
            s.FindProperty("windup").floatValue = .7f;
        });
        PrefabUtility.SaveAsPrefabAsset(go, path);
        PrefabUtility.UnloadPrefabContents(go);
        var scores = new float[5];
        EliteBrain.Score(2, 0, false, false, 0, true, true, 2.4f, scores);
        OutbreakEditor.Require(scores[1] > scores[0], "Ready heavy attack should score highest in range.");
        OutbreakEditor.Save("11");
    }
}
```


## Phase12Setup

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase12Setup.cs

```csharp
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

public static class Phase12Setup
{
    public const string CatalogPath = "Assets/Data/LoadoutCatalog.asset";
    [MenuItem("Zombie Outbreak/Phase 12 - Loadout and equipment")]
    public static void Build()
    {
        OutbreakEditor.Open();
        Phase2AnimationAssets.Folder("Assets/Prefabs/Equipment");
        var catalog = AssetDatabase.LoadAssetAtPath<LoadoutCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<LoadoutCatalog>();
            catalog.primaries = new WeaponData[3];
            string[] names =
            {
                "Rifle",
                "SMG",
                "Shotgun"
            };
            for (int i = 0; i < 3; i++)
                catalog.primaries[i] = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/" + names[i] + ".asset");
            catalog.pistol = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/Pistol.asset");
            catalog.equipment = new[]
            {
                new EquipmentPack
                {
                    name = "MEDIC PACK: 3 MEDKITS / 1 GRENADE",
                    medkits = 3,
                    grenades = 1
                },
                new EquipmentPack
                {
                    name = "ASSAULT PACK: 1 MEDKIT / 3 GRENADES",
                    medkits = 1,
                    grenades = 3
                }
            };
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        OutbreakEditor.Require(catalog.Valid(0, 0) && catalog.Valid(1, 1) && catalog.Valid(2, 0), "Loadout catalog invalid.");
        const string grenadePath = "Assets/Prefabs/Equipment/Grenade.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(grenadePath) == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Grenade";
            go.layer = 8;
            go.transform.localScale = Vector3.one * .22f;
            go.GetComponent<Renderer>().sharedMaterial = OutbreakEditor.Material("Grenade", new Color(.2f, .27f, .12f));
            var body = go.AddComponent<Rigidbody>();
            body.mass = .6f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<GrenadeProjectile>();
            PrefabUtility.SaveAsPrefabAsset(go, grenadePath);
            Object.DestroyImmediate(go);
        }

        var player = OutbreakEditor.One<PlayerHealth>();
        var equipment = OutbreakEditor.Add<PlayerEquipment>(player.gameObject);
        OutbreakEditor.Set(equipment, "health", player);
        OutbreakEditor.Set(equipment, "viewCamera", Camera.main);
        OutbreakEditor.Set(equipment, "grenadePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(grenadePath).GetComponent<GrenadeProjectile>());
        var applier = OutbreakEditor.Add<PlayerLoadoutApplier>(player.gameObject);
        OutbreakEditor.Set(applier, "catalog", catalog);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        MakeMenu(false, catalog);
        MakeMenu(true, catalog);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Loadout.unity", true),
            new EditorBuildSettingsScene(OutbreakEditor.Scene, true)
        };
        PlayerSettings.productName = "Zombie Outbreak";
        OutbreakEditor.Open();
        OutbreakEditor.Save("12");
    }

    private static void MakeMenu(bool loadout, LoadoutCatalog catalog)
    {
        string path = "Assets/Scenes/" + (loadout ? "Loadout" : "MainMenu") + ".unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
        {
            EditorSceneManager.OpenScene(path);
            OutbreakEditor.Set(OutbreakEditor.One<LoadoutMenuController>(), "catalog", AssetDatabase.LoadAssetAtPath<LoadoutCatalog>(CatalogPath));
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            return;
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        catalog = AssetDatabase.LoadAssetAtPath<LoadoutCatalog>(CatalogPath);
        Phase4UIFactory.PrepareResources();
        var root = new GameObject("Menu");
        var menu = root.AddComponent<LoadoutMenuController>();
        OutbreakEditor.Set(menu, "catalog", catalog);
        OutbreakEditor.Values(menu, s => s.FindProperty("isLoadout").boolValue = loadout);
        var camera = new GameObject("MenuCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.018f, .03f, .045f);
        camera.gameObject.AddComponent<AudioListener>();
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var canvas = Phase4UIFactory.MakeCanvas(root.transform, "MenuCanvas", 0);
        var card = Phase4UIFactory.Card(canvas, "Content", new Vector2(1300, 900), new Vector2(.5f, .5f), Vector2.zero);
        var accent = new Color(.28f, .88f, .72f);
        Phase4UIFactory.Label(card, "Eyebrow", "SECTOR 07 / INDUSTRIAL QUARANTINE", 20, accent, new Vector2(60, 814), new Vector2(1180, 35));
        Phase4UIFactory.Label(card, "Title", loadout ? "PREPARE YOUR LOADOUT" : "ZOMBIE OUTBREAK", 64, Color.white, new Vector2(60, 710), new Vector2(1180, 100));
        Phase4UIFactory.Label(card, "Subtitle", loadout ? "CHOOSE A PRIMARY. YOUR SIDEARM IS A PISTOL." : "FIVE WAVES. FOUR HUNTERS. ONE WAY OUT.", 23, new Color(.65f, .72f, .78f), new Vector2(60, 660), new Vector2(1180, 40));
        var selection = Phase4UIFactory.Label(card, "Selection", loadout ? "" : "SURVIVE THE DISTRICT\n\nWASD  MOVE    SHIFT  RUN    CTRL  SPRINT    SPACE  JUMP\nRMB  AIM    LMB  FIRE    R  RELOAD    1 / 2  SWITCH\nE  INTERACT    Q  MEDKIT    G  GRENADE    ESC  PAUSE", 24, Color.white, new Vector2(60, loadout ? 240 : 315), new Vector2(1180, loadout ? 100 : 280), TextAlignmentOptions.Center);
        OutbreakEditor.Set(menu, "selectionText", selection);
        var start = Phase4UIFactory.Button(card, "Start", loadout ? "ENTER THE DISTRICT" : "PREPARE LOADOUT", 130, true);
        OutbreakEditor.Set(menu, "startButton", start);
        if (loadout)
        {
            var left = Phase4UIFactory.Rect(card, "PrimaryChoices", new Vector2(540, 280), Vector2.zero, new Vector2(60, 355));
            var right = Phase4UIFactory.Rect(card, "EquipmentChoices", new Vector2(540, 280), Vector2.zero, new Vector2(700, 355));
            var primaries = new UnityEngine.UI.Button[3];
            for (int i = 0; i < 3; i++)
                primaries[i] = Phase4UIFactory.Button(left, "Primary" + i, catalog.primaries[i].WeaponName.ToUpperInvariant(), 200 - i * 85, false);
            var packs = new UnityEngine.UI.Button[2];
            packs[0] = Phase4UIFactory.Button(right, "Medic", "MEDKIT PACK  [3 + 1]", 175, false);
            packs[1] = Phase4UIFactory.Button(right, "Assault", "GRENADE PACK  [1 + 3]", 80, false);
            OutbreakEditor.Values(menu, s =>
            {
                Assign(s.FindProperty("primaryButtons"), primaries);
                Assign(s.FindProperty("equipmentButtons"), packs);
            });
            OutbreakEditor.Set(menu, "backButton", Phase4UIFactory.Button(card, "Back", "BACK TO MENU", 45, false));
        }

        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
    }

    private static void Assign(SerializedProperty property, UnityEngine.UI.Button[] buttons)
    {
        property.arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
    }
}
```


## Phase13Setup

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase13Setup.cs

```csharp
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class Phase13Setup
{
    [MenuItem("Zombie Outbreak/Phase 13 - Five waves")]
    public static void Build()
    {
        OutbreakEditor.Open();
        Phase2AnimationAssets.Folder("Assets/Data/Waves");
        string[] titles =
        {
            "OUTBREAK",
            "RUNNERS",
            "THE HUNT",
            "THE SIEGE",
            "THE COMMANDER"
        };
        string[] types =
        {
            "Hunter",
            "Runner",
            "Ambusher",
            "Elite"
        };
        int[, ] counts =
        {
            {
                8,
                0,
                0,
                0
            },
            {
                8,
                5,
                0,
                0
            },
            {
                8,
                5,
                3,
                0
            },
            {
                12,
                5,
                4,
                0
            },
            {
                10,
                5,
                3,
                1
            }
        };
        var definitions = new WaveDefinition[5];
        for (int w = 0; w < 5; w++)
        {
            string path = "Assets/Data/Waves/Wave" + (w + 1) + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WaveDefinition>();
                data.title = titles[w];
                data.spawnInterval = .7f;
                var entries = new System.Collections.Generic.List<WaveEntry>();
                for (int kind = 0; kind < 4; kind++)
                    if (counts[w, kind] > 0)
                        entries.Add(new WaveEntry { prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + types[kind] + ".prefab").GetComponent<EnemyActor>(), count = counts[w, kind] });
                data.entries = entries.ToArray();
                AssetDatabase.CreateAsset(data, path);
            }

            definitions[w] = data;
        }

        var world = OutbreakEditor.One<EnemyWorld>();
        var waves = OutbreakEditor.Add<WaveManager>(world.gameObject);
        OutbreakEditor.Set(waves, "world", world);
        OutbreakEditor.Set(waves, "screens", OutbreakEditor.One<GameUIController>());
        var entrances = Object.FindObjectsByType<MapMarker>().Where(m => m.kind == MapMarkerKind.ZombieEntrance).OrderBy(m => m.name).ToArray();
        OutbreakEditor.Values(waves, s =>
        {
            var list = s.FindProperty("waves");
            list.arraySize = 5;
            for (int i = 0; i < 5; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            list = s.FindProperty("entrances");
            list.arraySize = entrances.Length;
            for (int i = 0; i < entrances.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = entrances[i];
        });
        var hud = GameObject.Find("GameUI/HUDCanvas").transform;
        var presenter = OutbreakEditor.Add<GameStatusPresenter>(hud.gameObject);
        OutbreakEditor.Set(presenter, "waves", waves);
        OutbreakEditor.Set(presenter, "equipment", OutbreakEditor.One<PlayerEquipment>());
        OutbreakEditor.Set(presenter, "interaction", OutbreakEditor.One<PlayerInteractor>());
        OutbreakEditor.Set(presenter, "waveText", hud.Find("WavePlaceholders/WaveValue").GetComponent<TMP_Text>());
        OutbreakEditor.Set(presenter, "remainingText", hud.Find("WavePlaceholders/RemainingValue").GetComponent<TMP_Text>());
        var slots = hud.Find("EquipmentPlaceholders/Slots");
        slots.Find("Slot1/Number").GetComponent<TMP_Text>().text = "Q";
        slots.Find("Slot2/Number").GetComponent<TMP_Text>().text = "G";
        slots.Find("Slot3/Number").GetComponent<TMP_Text>().text = "E";
        slots.Find("Slot3/Status").GetComponent<TMP_Text>().text = "USE";
        OutbreakEditor.Set(presenter, "medkitText", slots.Find("Slot1/Status").GetComponent<TMP_Text>());
        OutbreakEditor.Set(presenter, "grenadeText", slots.Find("Slot2/Status").GetComponent<TMP_Text>());
        Phase4UIFactory.PrepareResources();
        var announcement = hud.Find("WaveAnnouncement")?.GetComponent<TMP_Text>();
        if (announcement == null)
        {
            announcement = Phase4UIFactory.Label(hud, "WaveAnnouncement", "", 34, Color.white, Vector2.zero, new Vector2(1150, 60), TextAlignmentOptions.Center);
            var rt = announcement.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, 1);
            rt.anchoredPosition = new Vector2(0, -180);
        }

        var prompt = hud.Find("InteractionPrompt")?.GetComponent<TMP_Text>();
        if (prompt == null)
        {
            prompt = Phase4UIFactory.Label(hud, "InteractionPrompt", "", 24, Color.white, Vector2.zero, new Vector2(500, 45), TextAlignmentOptions.Center);
            var rt = prompt.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, 0);
            rt.anchoredPosition = new Vector2(0, 190);
        }

        OutbreakEditor.Set(presenter, "announcementText", announcement);
        OutbreakEditor.Set(presenter, "promptText", prompt);
        OutbreakEditor.Require(definitions.Sum(w => w.Total) == 77, "Expected exactly 77 enemies across five waves.");
        OutbreakEditor.Save("13");
    }
}
```


## Phase14Checks

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase14Checks.cs

```csharp
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Native Play Mode integration checks. Runtime objects are never saved.</summary>
[InitializeOnLoad]
public static class Phase14Checks
{
    private const string Key = "Outbreak.Phase14Checks";
    private static int stage;
    private static double deadline;
    private static float readyAt;
    private static EnemyWorld world;
    private static GameUIController screens;
    private static readonly List<EnemyActor> actors = new List<EnemyActor>();
    private static readonly List<Vector3> starts = new List<Vector3>();
    static Phase14Checks()
    {
        if (SessionState.GetBool(Key, false))
        {
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Batch-only check.");
        OutbreakEditor.Open();
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new Exception("Phase 14 checks timed out.");
            if (!EditorApplication.isPlaying)
                return;
            if (Time.time < .2f)
            {
                var ui = OutbreakEditor.One<GameUIController>();
                if (ui != null)
                    ui.Resume();
                return;
            }

            if (stage == 0)
            {
                world = OutbreakEditor.One<EnemyWorld>();
                screens = OutbreakEditor.One<GameUIController>();
                screens.Resume();
                OutbreakEditor.One<WaveManager>().enabled = false;
                world.movement.enabled = false;
                OutbreakEditor.Require(world.weapons.CurrentWeapon != null, "Weapon loadout initialized");
                OutbreakEditor.Require(!world.weapons.SelectWeapon(2), "Gameplay should equip only primary and pistol");
                var equipment = OutbreakEditor.One<PlayerEquipment>();
                int kits = equipment.Medkits;
                OutbreakEditor.Require(!equipment.UseMedkit() && equipment.Medkits == kits, "Full health must not consume medkit");
                world.player.TakeDamage(30);
                OutbreakEditor.Require(equipment.UseMedkit() && world.player.CurrentHealth == world.player.MaxHealth, "Medkit caps at max health");
                string[] names =
                {
                    "Hunter",
                    "Runner",
                    "Ambusher",
                    "Elite"
                };
                for (int i = 0; i < 4; i++)
                {
                    var p = world.player.transform.position + new Vector3((i - 1.5f) * 2.2f, 0, 9);
                    OutbreakEditor.Require(NavMesh.SamplePosition(p, out var hit, 3, NavMesh.AllAreas), "Agent spawn projection");
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + names[i] + ".prefab");
                    var actor = UnityEngine.Object.Instantiate(prefab, hit.position, Quaternion.identity).GetComponent<EnemyActor>();
                    actor.Initialize(world);
                    actors.Add(actor);
                    starts.Add(hit.position);
                }

                OutbreakEditor.Require(world.Living.Count == 4, "All four agents registered");
                var marker = world.tacticalPoints[0];
                OutbreakEditor.Require(world.Reserve(marker, actors[0]) && !world.Reserve(marker, actors[1]), "Exclusive tactical reservation");
                world.Release(actors[0]);
                OutbreakEditor.Require(world.Reserve(marker, actors[1]), "Reservation release");
                world.Release(actors[1]);
                OutbreakEditor.Require(!actors[0].Movement.Reachable(world.player.transform.position + Vector3.up * 30), "Unreachable target rejected");
                world.player.TakeDamage(60);
                actors[1].transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(
                    world.player.transform.position - actors[1].transform.position, Vector3.up));
                actors[1].Perception.Sample();
                OutbreakEditor.Require(actors[1].Perception.PlayerWeakness > .5f, "Low-health sensing");
                world.player.Heal(100);
                world.weapons.SelectWeapon(1);
                actors[1].Perception.Sample();
                OutbreakEditor.Require(actors[1].Perception.Weapon == WeaponCategory.Pistol, "Weapon switching sensed");
                world.weapons.SelectWeapon(0);
                var door = OutbreakEditor.One<InteractiveDoor>();
                door.Interact(world.player.gameObject);
                OutbreakEditor.Require(door.IsOpen, "Door interaction");
                readyAt = Time.time + 5;
                stage = 1;
            }
            else if (stage == 1)
            {
                screens.Resume();
                world.player.Heal(100);
                if (Time.time < readyAt)
                    return;
                for (int i = 0; i < actors.Count; i++)
                {
                    OutbreakEditor.Require(actors[i].GetComponent<NavMeshAgent>().isOnNavMesh, "Agent stayed on mesh");
                    OutbreakEditor.Require(Vector3.Distance(starts[i], actors[i].transform.position) > .2f, "Agent moved: " + actors[i].name);
                    OutbreakEditor.Require(actors[i].Decision != "Uninitialized", "Decision activated");
                }

                int deaths = 0;
                actors[0].Health.Died += _ => deaths++;
                actors[0].Health.TakeDamage(10000);
                actors[0].Health.TakeDamage(10000);
                OutbreakEditor.Require(deaths == 1 && world.Living.Count == 3, "Death emitted once and registry removed actor");
                world.player.Die();
                OutbreakEditor.Require(screens.State == GameScreenState.GameOver && Time.timeScale == 0, "Player death shows terminal HUD");
                foreach (var actor in actors)
                    OutbreakEditor.Require(!actor.CanThink && !actor.Combat.IsAttacking, "All decisions/combat stop after player death");
                Debug.Log("PHASE_14_PLAY_PASS: four agents move; health, equipment, weapon sensing, reservation, unreachable path, door and death lifecycle checks passed.");
                Finish(0);
            }
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Finish(1);
        }
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error)
        {
            SessionState.SetBool("Outbreak.Phase14Error", true);
        }
    }

    private static void Finish(int code)
    {
        if (SessionState.GetBool("Outbreak.Phase14Error", false))
            code = 1;
        SessionState.EraseBool("Outbreak.Phase14Error");
        SessionState.EraseBool(Key);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        EditorApplication.Exit(code);
    }
}
```


## Phase15Atmosphere

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase15Atmosphere.cs

```csharp
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

/// <summary>Completes the explicitly requested sparse atmosphere and grenade smoke/flash.</summary>
public static class Phase15Atmosphere
{
    public static void Build()
    {
        OutbreakEditor.Open();
        var district = GameObject.Find("OutbreakDistrict").transform;
        if (district.Find("AtmosphereDetails") == null)
        {
            var details = OutbreakEditor.Group("AtmosphereDetails", district);
            var fire = Phase15Setup.Effect("SmallFire", new Color(1, .35f, .04f, .8f), 8, .6f, 1, .32f);
            var embers = Phase15Setup.Effect("Embers", new Color(1, .7f, .12f), 6, 1.8f, 1.6f, .035f);
            var dust = Phase15Setup.Effect("Dust", new Color(.5f, .45f, .3f, .15f), 10, 5, .1f, .12f);
            Continuous(fire, details, new Vector3(-12, .25f, 24), 14, 40, false);
            Continuous(embers, details, new Vector3(-12, .35f, 24), 4, 20, false);
            Continuous(dust, details, new Vector3(-20, 1.8f, 14), 3, 24, true);
            var glow = new GameObject("SmallFireGlow");
            glow.transform.SetParent(details);
            glow.transform.position = new Vector3(-12, 1, 24);
            var light = glow.AddComponent<Light>();
            light.color = new Color(1, .28f, .035f);
            light.range = 6;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;
            glow.AddComponent<FlickeringLight>();
            var concrete = OutbreakEditor.Material("Rubble", new Color(.3f, .31f, .32f));
            for (int i = 0; i < 4; i++)
                OutbreakEditor.Box("BrokenWall", details, new Vector3(31 + i * 1.2f, .4f + i * .25f, 3), new Vector3(1.1f, .8f + i * .5f, .5f), concrete);
            var grime = OutbreakEditor.Material("DirtGrime", new Color(.12f, .1f, .075f));
            for (int i = 0; i < 4; i++)
            {
                var patch = OutbreakEditor.Box("GroundGrime", details, new Vector3(-14 - i * 3, .035f, 24), new Vector3(2 + i * .3f, .012f, 1.2f), grime);
                Object.DestroyImmediate(patch.GetComponent<Collider>());
            }
        }

        const string path = "Assets/Prefabs/Effects/Explosion.prefab";
        var explosion = PrefabUtility.LoadPrefabContents(path);
        if (explosion.transform.Find("ExplosionSmoke") == null)
        {
            var smoke = Phase15Setup.Effect("ExplosionSmoke", new Color(.2f, .22f, .24f, .55f), 22, 2.4f, 1.3f, .6f);
            var child = (GameObject)PrefabUtility.InstantiatePrefab(smoke.gameObject);
            child.name = "ExplosionSmoke";
            child.transform.SetParent(explosion.transform, false);
            var flash = new GameObject("Flash", typeof(Light), typeof(TransientLight));
            flash.transform.SetParent(explosion.transform, false);
            var lamp = flash.GetComponent<Light>();
            lamp.color = new Color(1, .55f, .2f);
            lamp.intensity = 7;
            lamp.range = 9;
            lamp.shadows = LightShadows.None;
        }

        PrefabUtility.SaveAsPrefabAsset(explosion, path);
        PrefabUtility.UnloadPrefabContents(explosion);
        district.GetComponent<NavMeshSurface>().BuildNavMesh();
        Phase5Setup.Validate();
        OutbreakEditor.Save("15_ATMOSPHERE");
    }

    private static void Continuous(ParticleSystem prefab, Transform parent, Vector3 position, float rate, int maximum, bool volume)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(-90, 0, 0);
        var ps = go.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.maxParticles = maximum;
        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[0]);
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = volume ? ParticleSystemShapeType.Box : ParticleSystemShapeType.Cone;
        shape.radius = .25f;
        shape.angle = 12;
        if (volume)
            shape.scale = new Vector3(8, 2, 8);
    }
}
```


## Phase15Setup

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase15Setup.cs

```csharp
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class Phase15Setup
{
    [MenuItem("Zombie Outbreak/Phase 15 - Night district and effects")]
    public static void Build()
    {
        OutbreakEditor.Open();
        Phase2AnimationAssets.Folder("Assets/Textures/Outbreak");
        Phase2AnimationAssets.Folder("Assets/Prefabs/Effects");
        Surface("Concrete", new Color(.38f, .4f, .42f), 0);
        Surface("Asphalt", new Color(.15f, .17f, .19f), 0);
        Surface("Brick", new Color(.4f, .27f, .22f), 1);
        Surface("Wood", new Color(.4f, .3f, .18f), 2);
        Surface("Metal", new Color(.22f, .3f, .31f), 0);
        var district = GameObject.Find("OutbreakDistrict").transform;
        if (district.Find("ArtPass") == null)
            Dress(district);
        foreach (var light in Object.FindObjectsByType<Light>())
            if (light.type == LightType.Directional)
            {
                light.transform.rotation = Quaternion.Euler(48, -35, 0);
                light.color = new Color(.55f, .68f, 1);
                light.intensity = .95f;
                light.shadows = LightShadows.Soft;
            }

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.28f, .36f, .5f);
        RenderSettings.ambientEquatorColor = new Color(.18f, .23f, .3f);
        RenderSettings.ambientGroundColor = new Color(.1f, .12f, .15f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.075f, .105f, .15f);
        RenderSettings.fogStartDistance = 26;
        RenderSettings.fogEndDistance = 88;
        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        Camera.main.backgroundColor = RenderSettings.fogColor;
        Camera.main.farClipPlane = 110;
        QualitySettings.shadowDistance = 40;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.pixelLightCount = 4;
        var impact = Effect("Impact", new Color(1, .7f, .3f), 12, .22f, 2, .07f);
        var hit = Effect("EnemyHit", new Color(.48f, .05f, .035f), 10, .3f, 1.8f, .09f);
        var death = Effect("EnemyDeath", new Color(.18f, .2f, .15f, .65f), 18, .9f, .8f, .24f);
        var explosion = Effect("Explosion", new Color(1, .42f, .08f), 55, .7f, 6, .35f);
        var muzzle = Effect("MuzzleFlash", new Color(1, .8f, .3f), 5, .055f, 2, .075f);
        var fx = OutbreakEditor.Add<CombatEffects>(OutbreakEditor.Group("CombatEffects").gameObject);
        OutbreakEditor.Set(fx, "weapons", OutbreakEditor.One<PlayerWeaponController>());
        OutbreakEditor.Set(fx, "waves", OutbreakEditor.One<WaveManager>());
        OutbreakEditor.Set(fx, "impactPrefab", impact);
        OutbreakEditor.Set(fx, "hitPrefab", hit);
        OutbreakEditor.Set(fx, "deathPrefab", death);
        var grenade = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Equipment/Grenade.prefab");
        OutbreakEditor.Set(grenade.GetComponent<GrenadeProjectile>(), "explosionEffect", explosion);
        PrefabUtility.SaveAsPrefabAsset(grenade, "Assets/Prefabs/Equipment/Grenade.prefab");
        PrefabUtility.UnloadPrefabContents(grenade);
        foreach (string name in new[]
        {
            "Rifle",
            "SMG",
            "Shotgun",
            "Pistol"
        }

        )
            OutbreakEditor.Set(AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/" + name + ".asset"), "muzzleFlashPrefab", muzzle);
        district.GetComponent<NavMeshSurface>().BuildNavMesh();
        Phase5Setup.Validate();
        OutbreakEditor.Save("15");
    }

    private static void Surface(string name, Color color, int pattern)
    {
        var mat = OutbreakEditor.Material(name, color);
        string path = "Assets/Textures/Outbreak/" + name + ".asset";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            texture = new Texture2D(128, 128, TextureFormat.RGB24, true)
            {
                name = name + "Procedural",
                wrapMode = TextureWrapMode.Repeat
            };
            var pixels = new Color[128 * 128];
            var random = new System.Random(15 + pattern);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float grain = .88f + (float)random.NextDouble() * .24f;
                    if (pattern == 1 && (y % 32 < 2 || (x + ((y / 32) % 2) * 32) % 64 < 2))
                        grain = .55f;
                    if (pattern == 2)
                        grain *= .85f + .15f * Mathf.Sin(x * .7f + Mathf.Sin(y * .08f));
                    pixels[y * 128 + x] = new Color(grain, grain, grain);
                }

            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
        }

        mat.mainTexture = texture;
        mat.mainTextureScale = pattern == 1 ? new Vector2(3, 3) : new Vector2(8, 8);
        mat.color = color;
        EditorUtility.SetDirty(mat);
    }

    private static void Dress(Transform district)
    {
        var art = OutbreakEditor.Group("ArtPass", district);
        var metal = OutbreakEditor.Material("Metal", Color.gray);
        var paint = OutbreakEditor.Material("RoadPaint", new Color(.8f, .7f, .36f));
        for (int z = -33; z <= 33; z += 7)
        {
            if (z > -13 && z < -3)
                continue;
            var stripe = OutbreakEditor.Box("LaneMark", art, new Vector3(0, .052f, z), new Vector3(.13f, .012f, 2.8f), paint);
            Object.DestroyImmediate(stripe.GetComponent<Collider>());
        }

        var lampMat = OutbreakEditor.Material("LampGlow", new Color(1, .74f, .3f));
        lampMat.EnableKeyword("_EMISSION");
        lampMat.SetColor("_EmissionColor", new Color(2, 1.2f, .4f));
        for (int i = 0; i < 6; i++)
        {
            float x = i % 2 == 0 ? -6 : 6, z = -29 + i * 11;
            OutbreakEditor.Box("StreetLampPole", art, new Vector3(x, 2.5f, z), new Vector3(.15f, 5, .15f), metal);
            OutbreakEditor.Box("StreetLampHead", art, new Vector3(x * .88f, 5, z), new Vector3(1.4f, .15f, .4f), lampMat);
            Point("StreetPool", district.Find("Lighting"), new Vector3(x * .88f, 4.7f, z), new Color(1, .65f, .3f), 3.4f, 12);
        }

        Point("WarehouseInterior", district.Find("Lighting"), new Vector3(-20, 3.8f, 14), new Color(.56f, .75f, 1), 3, 15);
        Point("OfficeInterior", district.Find("Lighting"), new Vector3(21, 3.2f, 15), new Color(1, .7f, .38f), 2.5f, 13);
        Point("MaintenanceEmergency", district.Find("Lighting"), new Vector3(-22, 2.8f, -24), new Color(1, .15f, .07f), 1.1f, 10).gameObject.AddComponent<FlickeringLight>();
        var glass = OutbreakEditor.Material("DirtyGlass", new Color(.13f, .25f, .32f));
        glass.SetFloat("_Glossiness", .75f);
        for (int i = 0; i < 4; i++)
            OutbreakEditor.Box("OfficeWindow", art, new Vector3(15 + i * 4, 2.2f, 5.68f), new Vector3(2, 1.2f, .05f), glass);
        Sign(art, "WAREHOUSE 07", new Vector3(-20, 3.8f, 3.65f), Color.white);
        Sign(art, "QUARANTINE", new Vector3(21, 3.45f, 5.62f), new Color(1, .6f, .25f));
        Sign(art, "HOLD THE LINE", new Vector3(19, 2.3f, -26), new Color(1, .4f, .2f));
        var carPaint = OutbreakEditor.Material("AbandonedVehicle", new Color(.2f, .28f, .34f));
        Car(art, new Vector3(13, 0, -6), Quaternion.Euler(0, 65, 0), carPaint, glass);
        Car(art, new Vector3(-9, 0, 27), Quaternion.Euler(0, -15, 0), carPaint, glass);
        var rubble = OutbreakEditor.Material("Rubble", new Color(.3f, .31f, .32f));
        var random = new System.Random(23);
        for (int i = 0; i < 20; i++)
        {
            float x = (i % 2 == 0 ? -1 : 1) * (29 + (float)random.NextDouble() * 6), z = -30 + (float)random.NextDouble() * 60;
            OutbreakEditor.Box("Debris", art, new Vector3(x, .15f, z), new Vector3(.4f, .3f, .65f), rubble);
        }

        var smoke = Effect("AmbientSmoke", new Color(.22f, .25f, .29f, .25f), 25, 3, 1, .7f);
        var smokeObject = (GameObject)PrefabUtility.InstantiatePrefab(smoke.gameObject);
        smokeObject.name = "WarehouseVentSmoke";
        smokeObject.transform.SetParent(art);
        smokeObject.transform.position = new Vector3(-28, 4, 20);
        var ps = smokeObject.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startSpeed = .4f;
        main.startLifetime = 5;
        main.maxParticles = 60;
        main.playOnAwake = true;
        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[0]);
        emission.rateOverTime = 5;
        foreach (var renderer in art.GetComponentsInChildren<Renderer>())
            if (renderer.GetComponent<ParticleSystem>() == null)
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
    }

    private static Light Point(string name, Transform parent, Vector3 position, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        var lamp = go.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = color;
        lamp.intensity = intensity;
        lamp.range = range;
        lamp.shadows = LightShadows.None;
        return lamp;
    }

    private static void Sign(Transform parent, string text, Vector3 position, Color color)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(TextMeshPro));
        go.transform.SetParent(parent);
        go.transform.position = position;
        var label = go.GetComponent<TextMeshPro>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Phase4UIFactory.FontPath);
        label.text = text;
        label.fontSize = 3;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(10, 1);
        label.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    private static void Car(Transform parent, Vector3 position, Quaternion rotation, Material paint, Material glass)
    {
        var car = OutbreakEditor.Group("AbandonedCar", parent); // Each car receives a distinct transform.
        if (car.childCount > 0)
            car = new GameObject("AbandonedCar_02").transform;
        car.SetParent(parent);
        car.position = position;
        car.rotation = rotation;
        OutbreakEditor.Box("Body", car, new Vector3(0, .6f, 0), new Vector3(1.9f, .7f, 4.1f), paint);
        OutbreakEditor.Box("Cab", car, new Vector3(0, 1.2f, -.1f), new Vector3(1.6f, .65f, 1.9f), glass);
        var rubber = OutbreakEditor.Material("Rubber", new Color(.06f, .065f, .07f));
        foreach (float x in new[]
        {
            -.95f,
            .95f
        }

        )
            foreach (float z in new[]
            {
                -1.3f,
                1.3f
            }

            )
                OutbreakEditor.Box("Wheel", car, new Vector3(x, .4f, z), new Vector3(.25f, .65f, .65f), rubber);
    }

    public static ParticleSystem Effect(string name, Color color, int count, float lifetime, float speed, float size)
    {
        string path = "Assets/Prefabs/Effects/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing.GetComponent<ParticleSystem>();
        var go = new GameObject(name);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.duration = 1;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = .08f;
        var colorOver = ps.colorOverLifetime;
        colorOver.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        colorOver.color = gradient;
        string matPath = "Assets/Materials/Outbreak/Particles.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Particles/Standard Unlit"));
            AssetDatabase.CreateAsset(material, matPath);
        }

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<ParticleSystem>();
    }
}
```


## Phase16Setup

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase16Setup.cs

```csharp
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class Phase16Setup
{
    [MenuItem("Zombie Outbreak/Phase 16 - Synchronize animations")]
    public static void Build()
    {
        OutbreakEditor.Open();
        Phase2AnimationAssets.Folder("Assets/Animations/Enemies/Clips");
        var rig = Phase2HumanoidFactory.Create("Assets/Animations/Enemies/EnemyAvatar.asset");
        string[] names =
        {
            "ZombieIdle",
            "ZombieWalkForward",
            "ZombieRun",
            "ZombieAttack",
            "EliteHeavy",
            "ZombieHit",
            "ZombieDeath"
        };
        foreach (string name in names)
        {
            string path = "Assets/Animations/Enemies/Clips/" + name + ".anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
                continue;
            bool loop = name == "ZombieIdle" || name == "ZombieWalkForward" || name == "ZombieRun";
            float duration = name == "ZombieRun" ? .6f : name == "ZombieHit" ? .3f : 1;
            AssetDatabase.CreateAsset(Phase2HumanoidFactory.CreateClip(rig, name, duration, loop), path);
        }

        Object.DestroyImmediate(rig.Root);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Phase7Setup.ControllerPath);
        if (!controller.parameters.Any(p => p.name == "GaitScale"))
            controller.AddParameter(new AnimatorControllerParameter { name = "GaitScale", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        foreach (var child in controller.layers[0].stateMachine.states)
        {
            var state = child.state;
            if (state.name == "Locomotion")
            {
                var tree = (BlendTree)state.motion;
                tree.children = new ChildMotion[0];
                tree.AddChild(Clip("ZombieIdle"), 0);
                tree.AddChild(Clip("ZombieWalkForward"), 1.5f);
                tree.AddChild(Clip("ZombieRun"), 3.5f);
                state.speedParameter = "GaitScale";
                state.speedParameterActive = true;
                EditorUtility.SetDirty(tree);
            }
            else
                state.motion = Clip(state.name == "Heavy" ? "EliteHeavy" : "Zombie" + state.name);
        }

        foreach (var transition in controller.layers[0].stateMachine.anyStateTransitions)
            if (transition.destinationState.name == "Attack" || transition.destinationState.name == "Heavy")
                transition.duration = 0;
        EditorUtility.SetDirty(controller);
        var player = AssetDatabase.LoadAssetAtPath<AnimatorController>(Phase2AnimationAssets.ControllerPath);
        if (!player.parameters.Any(p => p.name == "ReloadSpeed"))
            player.AddParameter(new AnimatorControllerParameter { name = "ReloadSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        var actions = player.layers[1].stateMachine;
        var reload = actions.states.First(s => s.state.name == "Reload").state;
        reload.speedParameter = "ReloadSpeed";
        reload.speedParameterActive = true;
        foreach (var transition in reload.transitions)
        {
            transition.exitTime = 1;
            transition.duration = 0;
        }

        foreach (var transition in actions.anyStateTransitions)
            if (transition.destinationState == reload)
                transition.duration = 0;
        EditorUtility.SetDirty(player);
        OutbreakEditor.Require(controller.layers[0].stateMachine.states.All(s => s.state.motion != null), "Enemy states require animation clips.");
        OutbreakEditor.Save("16");
    }

    private static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Enemies/Clips/" + name + ".anim");
}
```


## Phase17Checks

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase17Checks.cs

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Accelerated native integration run. This is not a claim of human balance playtesting.</summary>
[InitializeOnLoad]
public static class Phase17Checks
{
    private const string Key = "Outbreak.Phase17Checks";
    private static int stage, frames;
    private static double deadline;
    private static float waitUntil;
    private static EnemyWorld world;
    private static WaveManager waves;
    private static GameUIController ui;
    private static PlayerEquipment equipment;
    private static PlayerAimController aim;
    private static readonly List<EnemyActor> spawned = new List<EnemyActor>();
    private static readonly int[, ] counts = new int[5, 4];
    private static bool errors;
    private static int shots;
    private static EnemyActor target;
    private static float healthBefore;
    static Phase17Checks()
    {
        if (SessionState.GetBool(Key, false))
        {
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Batch-only check.");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new Exception("Phase 17 timed out at stage " + stage);
            if (!EditorApplication.isPlaying)
                return;
            string scene = SceneManager.GetActiveScene().name;
            if (stage == 0 && scene == "MainMenu")
            {
                if (++frames < 8)
                    return;
                OutbreakCapture.Save("MainMenu");
                OutbreakCapture.Save("MainMenu_4x3", 1024, 768);
                OutbreakEditor.One<LoadoutMenuController>().StartGame();
                stage = 1;
                frames = 0;
            }
            else if (stage == 1 && scene == "Loadout")
            {
                if (++frames < 8)
                    return;
                var menu = OutbreakEditor.One<LoadoutMenuController>();
                var session = LoadoutSession.Instance;
                Check(session.Select(1, 1) && session.IsValid, "Valid SMG/Assault loadout accepted");
                Check(!session.Select(99, 0), "Invalid primary rejected");
                // Exercise actual button listeners and capture their selected summary.
                GameObject.Find("Menu/MenuCanvas/Content/PrimaryChoices/Primary1").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                GameObject.Find("Menu/MenuCanvas/Content/EquipmentChoices/Assault").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                OutbreakCapture.Save("Loadout");
                OutbreakCapture.Save("Loadout_4x3", 1024, 768);
                menu.StartGame();
                stage = 2;
                frames = 0;
            }
            else if (stage == 2 && scene == "ZombieMap")
            {
                if (++frames < 10)
                {
                    var controller = OutbreakEditor.One<GameUIController>();
                    if (controller != null)
                        controller.Resume();
                    return;
                }

                world = OutbreakEditor.One<EnemyWorld>();
                waves = OutbreakEditor.One<WaveManager>();
                ui = OutbreakEditor.One<GameUIController>();
                ui.Resume();
                OutbreakEditor.Values(ui, s => s.FindProperty("pauseOnFocusLoss").boolValue = false);
                world.movement.enabled = false;
                equipment = world.player.GetComponent<PlayerEquipment>();
                aim = world.player.GetComponent<PlayerAimController>();
                Check(world.weapons.CurrentWeaponCategory == WeaponCategory.SMG && equipment.Medkits == 1 && equipment.Grenades == 3, "Selection persisted into gameplay");
                Check(world.weapons.GetComponentsInChildren<WeaponView>(true).Length == 2, "Exactly two visible weapon slots instantiated");
                world.weapons.ShotFired += _ => shots++;
                ui.Pause();
                Check(Time.timeScale == 0 && !world.weapons.TryFire() && !equipment.ThrowGrenade(), "Pause gates gameplay");
                OutbreakCapture.Save("Pause");
                ui.Resume();
                SetFastWaves();
                waves.EnemySpawned += OnSpawn;
                var camera = Camera.main;
                camera.GetComponent<ThirdPersonCameraController>().enabled = false;
                camera.transform.position = new Vector3(.6f, 1.8f, -20);
                camera.transform.LookAt(new Vector3(0, 1.4f, -10));
                // Large test-only enemy target to make random spread deterministic with respect to hits.
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Hunter.prefab");
                target = UnityEngine.Object.Instantiate(prefab, new Vector3(0, 0, -10), Quaternion.identity).GetComponent<EnemyActor>();
                target.Initialize(world);
                target.GetComponent<HunterBrain>().enabled = false;
                var capsule = target.GetComponent<CapsuleCollider>();
                capsule.radius = 1.5f;
                capsule.height = 3;
                capsule.center = Vector3.up * 1.5f;
                Physics.SyncTransforms();
                aim.SetAiming(true);
                int ammo = world.weapons.MagazineAmmo;
                healthBefore = target.Health.Current;
                Check(world.weapons.TryFire() && !world.weapons.LastShotBlocked, "Muzzle-to-crosshair shot clear");
                Check(shots == 1 && world.weapons.MagazineAmmo == ammo - 1 && target.Health.Current < healthBefore, "One round deals enemy damage and consumes ammo");
                Check(!world.weapons.TryFire(), "Rate limit rejects immediate duplicate shot");
                Check(GameObject.Find("GameUI/HUDCanvas/Crosshair").activeSelf, "Aiming crosshair shown");
                Check(Text("Weapon/MagazineAmmo") == world.weapons.MagazineAmmo.ToString("00"), "Ammo event updates HUD");
                Check(world.weapons.TryReload(), "Reload starts");
                Check(GameObject.Find("GameUI/HUDCanvas/Weapon") == null || world.weapons.IsReloading, "Reload active");
                waitUntil = Time.time + world.weapons.CurrentWeapon.ReloadTime + .2f;
                stage = 3;
            }
            else if (stage == 3)
            {
                ui.Resume();
                world.player.Heal(100);
                if (Time.time < waitUntil)
                    return;
                Check(!world.weapons.IsReloading && world.weapons.MagazineAmmo == 35 && world.weapons.ReserveAmmo == 139, "SMG timed reload transfers exactly one round");
                Check(world.weapons.SelectWeapon(1) && world.weapons.CurrentWeaponCategory == WeaponCategory.Pistol, "Pistol switching");
                Check(Text("Weapon/WeaponName") == "PISTOL", "Weapon event updates HUD");
                world.weapons.SelectWeapon(0);
                world.player.TakeDamage(25);
                Check(Text("Health/HealthValue").Contains("75"), "Health event updates HUD");
                Check(equipment.UseMedkit() && world.player.CurrentHealth == 100 && equipment.Medkits == 0, "Medkit caps health and consumes one");
                target.Health.TakeDamage(10000);
                Check(equipment.ThrowGrenade() && equipment.Grenades == 2, "Grenade consumes one equipment charge");
                var grenade = OutbreakEditor.One<GrenadeProjectile>();
                grenade.transform.position = new Vector3(0, 1, -7);
                grenade.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
                Physics.SyncTransforms();
                grenade.Explode();
                aim.SetAiming(true);
                OutbreakCapture.Save("GameplayAim");
                OutbreakCapture.Save("GameplayAim_4x3", 1024, 768);
                aim.SetAiming(false);
                Check(!GameObject.Find("GameUI/HUDCanvas").transform.Find("Crosshair").gameObject.activeSelf, "Crosshair hides when aim ends");
                Camera.main.GetComponent<ThirdPersonCameraController>().enabled = true;
                // Capture the real, unmodified gameplay camera on the following frames.
                waitUntil = Time.time + 1;
                stage = 4;
            }
            else if (stage == 4)
            {
                ui.Resume();
                world.player.Heal(100);
                if (Time.time < waitUntil)
                    return;
                OutbreakCapture.Save("GameplayExploration");
                Time.timeScale = 3;
                stage = 5;
            }
            else if (stage == 5)
            {
                if (ui.State == GameScreenState.Victory)
                {
                    int[, ] expected =
                    {
                        {
                            8,
                            0,
                            0,
                            0
                        },
                        {
                            8,
                            5,
                            0,
                            0
                        },
                        {
                            8,
                            5,
                            3,
                            0
                        },
                        {
                            12,
                            5,
                            4,
                            0
                        },
                        {
                            10,
                            5,
                            3,
                            1
                        }
                    };
                    for (int w = 0; w < 5; w++)
                        for (int kind = 0; kind < 4; kind++)
                            Check(counts[w, kind] == expected[w, kind], "Wave " + (w + 1) + " category " + kind + " exact count");
                    Check(waves.Remaining == 0 && waves.CurrentWave == 5, "Final wave cleared");
                    OutbreakCapture.Save("Victory");
                    ui.RestartLevel();
                    stage = 6;
                    frames = 0;
                    return;
                }

                ui.Resume();
                world.player.Heal(100);
                if (waves.CurrentWave >= 1)
                    Check(int.Parse(Text("WavePlaceholders/RemainingValue")) == waves.Remaining, "Wave event updates remaining HUD");
                foreach (var actor in spawned.ToArray())
                    if (actor != null && !actor.Health.IsDead)
                        actor.Health.TakeDamage(10000);
                spawned.Clear();
            }
            else if (stage == 6 && scene == "ZombieMap")
            {
                if (++frames < 10)
                    return;
                ui = OutbreakEditor.One<GameUIController>();
                ui.Resume();
                world = OutbreakEditor.One<EnemyWorld>();
                waves = OutbreakEditor.One<WaveManager>();
                Check(waves.CurrentWave <= 1 && world.weapons.MagazineAmmo == 35 && world.player.CurrentHealth == 100, "Restart resets runtime state");
                Check(UnityEngine.Object.FindObjectsByType<LoadoutSession>().Length == 1, "One persistent loadout session");
                world.player.Die();
                Check(ui.State == GameScreenState.GameOver && waves.State == WaveState.Stopped && !world.weapons.TryReload(), "Death stops wave progression and weapons");
                stage = 7;
                frames = 0;
            }
            else if (stage == 7)
            {
                if (++frames < 40)
                    return;
                OutbreakCapture.Save("GameOver");
                Debug.Log("PHASE_17_FLOW_PASS: MainMenu > Loadout > ZombieMap; shooting/reload/health/equipment/HUD; exact 77-enemy five-wave progression; Victory; clean restart; Game Over.");
                Finish(0);
            }
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Finish(1);
        }
    }

    private static string Text(string path) => GameObject.Find("GameUI/HUDCanvas").transform.Find(path).GetComponent<TMP_Text>().text;
    private static void SetFastWaves()
    {
        // Clone data in memory; the actual wave assets retain their balanced spawn intervals.
        var field = typeof(WaveManager).GetField("waves", BindingFlags.Instance | BindingFlags.NonPublic);
        var original = (WaveDefinition[])field.GetValue(waves);
        var copies = new WaveDefinition[original.Length];
        for (int i = 0; i < copies.Length; i++)
        {
            copies[i] = UnityEngine.Object.Instantiate(original[i]);
            copies[i].spawnInterval = .06f;
        }

        field.SetValue(waves, copies);
        OutbreakEditor.Values(waves, s => s.FindProperty("betweenWaves").floatValue = .2f);
    }

    private static void OnSpawn(EnemyActor actor)
    {
        int kind = actor.GetComponent<HunterBrain>() != null ? 0 : actor.GetComponent<RunnerBrain>() != null ? 1 : actor.GetComponent<AmbusherBrain>() != null ? 2 : 3;
        counts[waves.CurrentWave - 1, kind]++;
        spawned.Add(actor);
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception("CHECK FAILED: " + name);
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
            errors = true;
    }

    private static void Finish(int code)
    {
        SessionState.EraseBool(Key);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        EditorApplication.Exit(errors ? 1 : code);
    }
}
```


## Phase17MechanicsChecks

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase17MechanicsChecks.cs

```csharp
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class Phase17MechanicsChecks
{
    private const string Key = "Outbreak.Phase17Mechanics";
    private static int stage;
    private static double deadline;
    private static float waitUntil, healthBefore;
    private static EnemyWorld world;
    private static EnemyActor attacker;
    private static GameUIController ui;
    private static bool errors;
    static Phase17MechanicsChecks()
    {
        if (SessionState.GetBool(Key, false))
        {
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Batch only");
        OutbreakEditor.Open();
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    private static EnemyActor Spawn(string name, Vector3 position)
    {
        OutbreakEditor.Require(NavMesh.SamplePosition(position, out var hit, 2, NavMesh.AllAreas), "Test spawn on mesh");
        var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + name + ".prefab"), hit.position, Quaternion.identity).GetComponent<EnemyActor>();
        actor.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(
            world.player.transform.position - actor.transform.position, Vector3.up));
        actor.Initialize(world);
        if (actor.GetComponent<HunterBrain>() != null)
            actor.GetComponent<HunterBrain>().enabled = false;
        if (actor.GetComponent<EliteBrain>() != null)
            actor.GetComponent<EliteBrain>().enabled = false;
        return actor;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new Exception("Mechanics check timeout stage " + stage);
            if (!EditorApplication.isPlaying)
                return;
            if (ui == null)
                ui = OutbreakEditor.One<GameUIController>();
            if (ui == null)
                return;
            ui.Resume();
            if (Time.time < .2f)
                return;
            if (stage == 0)
            {
                world = OutbreakEditor.One<EnemyWorld>();
                OutbreakEditor.One<WaveManager>().enabled = false;
                world.movement.enabled = false;
                OutbreakEditor.Values(ui, s => s.FindProperty("pauseOnFocusLoss").boolValue = false);
                attacker = Spawn("Hunter", world.player.transform.position + Vector3.forward * 1.5f);
                Physics.SyncTransforms();
                attacker.Perception.Sample();
                healthBefore = world.player.CurrentHealth;
                OutbreakEditor.Require(attacker.Combat.TryAttack(), "Melee begins within range and LOS");
                attacker.Combat.AnimationImpact();
                OutbreakEditor.Require(world.player.CurrentHealth == healthBefore, "No damage before windup");
                waitUntil = Time.time + .65f;
                stage = 1;
            }
            else if (stage == 1 && Time.time >= waitUntil)
            {
                OutbreakEditor.Require(world.player.CurrentHealth == healthBefore - 10, "One timed melee hit deals configured damage");
                attacker.Combat.AnimationImpact();
                OutbreakEditor.Require(world.player.CurrentHealth == healthBefore - 10, "Animation event cannot duplicate hit");
                attacker.Health.TakeDamage(1000);
                world.player.Heal(100);
                attacker = Spawn("Hunter", world.player.transform.position + Vector3.forward * 1.5f);
                Physics.SyncTransforms();
                attacker.Perception.Sample();
                OutbreakEditor.Require(attacker.Combat.TryAttack(), "Second melee begins");
                world.player.transform.position += Vector3.back * 4;
                Physics.SyncTransforms();
                waitUntil = Time.time + .7f;
                stage = 2;
            }
            else if (stage == 2 && Time.time >= waitUntil)
            {
                OutbreakEditor.Require(world.player.CurrentHealth == 100, "Leaving windup range prevents damage");
                attacker.Health.TakeDamage(1000);
                var elite = Spawn("Elite", new Vector3(0, 0, -12));
                var child = new GameObject("Extra hit collider");
                child.layer = 9;
                child.transform.SetParent(elite.transform, false);
                child.transform.localPosition = Vector3.up;
                child.AddComponent<SphereCollider>();
                int hits = 0;
                elite.Health.Hit += _ => hits++;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Equipment/Grenade.prefab");
                var grenade = UnityEngine.Object.Instantiate(prefab, new Vector3(0, 1, -14), Quaternion.identity).GetComponent<GrenadeProjectile>();
                Physics.SyncTransforms();
                float before = elite.Health.Current;
                grenade.Explode();
                OutbreakEditor.Require(hits == 1 && elite.Health.Current < before && elite.Health.Current >= before - 100, "Grenade damages an enemy once across multiple colliders");
                elite.Health.TakeDamage(10000);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.layer = 8;
                wall.transform.position = new Vector3(0, 1.5f, -13);
                wall.transform.localScale = new Vector3(5, 3, .3f);
                var protectedEnemy = Spawn("Elite", new Vector3(0, 0, -11));
                grenade = UnityEngine.Object.Instantiate(prefab, new Vector3(0, 1, -15), Quaternion.identity).GetComponent<GrenadeProjectile>();
                Physics.SyncTransforms();
                before = protectedEnemy.Health.Current;
                grenade.Explode();
                OutbreakEditor.Require(protectedEnemy.Health.Current == before, "Solid wall blocks grenade enemy damage");
                UnityEngine.Object.Destroy(wall);
                protectedEnemy.Health.TakeDamage(10000);
                var barricade = OutbreakEditor.One<DestructibleBarricade>();
                int breaks = 0;
                barricade.Destroyed += () => breaks++;
                barricade.TakeDamage(200);
                barricade.TakeDamage(200);
                OutbreakEditor.Require(breaks == 1 && barricade.IsDestroyed, "Barricade destruction is idempotent");
                world.player.transform.position = new Vector3(0, .1f, -35.8f);
                Physics.SyncTransforms();
                waitUntil = Time.time + 1.2f;
                stage = 3;
            }
            else if (stage == 3 && Time.time >= waitUntil)
            {
                var camera = Camera.main;
                OutbreakEditor.Require(camera.transform.position.z > -37.7f, "Camera retracts before the rear boundary");
                OutbreakEditor.Require(!Physics.CheckSphere(camera.transform.position, .04f, 1 << 8, QueryTriggerInteraction.Ignore), "Camera is outside solid World colliders");
                Debug.Log("PHASE_17_MECHANICS_PASS: synchronized melee, range escape, idempotent hit, grenade multi-collider deduplication/occlusion, barricade break and camera collision.");
                Finish(0);
            }
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Finish(1);
        }
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
            errors = true;
    }

    private static void Finish(int code)
    {
        SessionState.EraseBool(Key);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        EditorApplication.Exit(errors ? 1 : code);
    }
}
```


## Phase17Setup

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase17Setup.cs

```csharp
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Phase17Setup
{
    [MenuItem("Zombie Outbreak/Phase 17 - Final integration fixes")]
    public static void Build()
    {
        OutbreakEditor.Open();
        Phase4UIFactory.PrepareResources();
        // Characters can pass between the camera and player without forcing a close-up.
        OutbreakEditor.Values(Camera.main.GetComponent<ThirdPersonCameraController>(), s => s.FindProperty("collisionLayers").intValue = 1 << 8);
        var presenter = OutbreakEditor.One<GamePanelsPresenter>();
        var canvas = presenter.transform;
        var buttons = new System.Collections.Generic.List<UnityEngine.UI.Button>();
        foreach (string panel in new[]
        {
            "PausePanel",
            "GameOverPanel",
            "VictoryPanel"
        }

        )
        {
            var content = canvas.Find(panel + "/Content");
            if (content == null)
                continue;
            var menu = content.Find("MainMenu")?.GetComponent<UnityEngine.UI.Button>();
            var existing = content.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var button in existing)
            {
                if (menu != null || button.name == "MainMenu")
                    continue;
                var rect = button.GetComponent<RectTransform>();
                rect.anchoredPosition += new Vector2(0, 40);
            }

            if (menu == null)
                menu = Phase4UIFactory.Button((RectTransform)content, "MainMenu", "MAIN MENU", 22, false);
            buttons.Add(menu);
        }

        OutbreakEditor.Values(presenter, s =>
        {
            var p = s.FindProperty("menuButtons");
            p.arraySize = buttons.Count;
            for (int i = 0; i < buttons.Count; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        });
        // Replace barrel-only visuals while retaining the stable authored box collision and Rigidbody.
        var props = GameObject.Find("OutbreakDistrict/PhysicsObjects").transform;
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var cylinderMesh = cylinder.GetComponent<MeshFilter>().sharedMesh;
        foreach (Transform prop in props)
            if (prop.name.StartsWith("Barrel"))
            {
                if (prop.Find("BarrelVisual") != null)
                    continue;
                prop.GetComponent<MeshRenderer>().enabled = false;
                var visual = new GameObject("BarrelVisual", typeof(MeshFilter), typeof(MeshRenderer));
                visual.transform.SetParent(prop, false);
                visual.transform.localScale = new Vector3(.95f, .5f, .95f);
                visual.GetComponent<MeshFilter>().sharedMesh = cylinderMesh;
                visual.GetComponent<MeshRenderer>().sharedMaterial = prop.GetComponent<MeshRenderer>().sharedMaterial;
            }

        Object.DestroyImmediate(cylinder);
        foreach (var target in Object.FindObjectsByType<ShootingTarget>())
            target.gameObject.SetActive(false);
        OutbreakEditor.Values(OutbreakEditor.One<PlayerWeaponController>(), s => s.FindProperty("drawShotRays").boolValue = false);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        Phase4UIFactory.PrepareResources();
        var contentRoot = (RectTransform)GameObject.Find("Menu/MenuCanvas/Content").transform;
        contentRoot.Find("Subtitle").GetComponent<TMP_Text>().text = "FIVE WAVES. FOUR THREATS. SURVIVE THE DISTRICT.";
        var quit = contentRoot.Find("Quit")?.GetComponent<UnityEngine.UI.Button>();
        if (quit == null)
            quit = Phase4UIFactory.Button(contentRoot, "Quit", "QUIT", 45, false);
        OutbreakEditor.Set(OutbreakEditor.One<LoadoutMenuController>(), "quitButton", quit);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        OutbreakEditor.Open();
        Phase3Checks.Run();
        Phase5Setup.Validate();
        OutbreakEditor.Require(buttons.Count == 3, "All terminal/pause panels require a Main Menu button.");
        OutbreakEditor.Save("17_SETUP");
    }
}
```


## Phase17StressChecks

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase17StressChecks.cs

```csharp
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class Phase17StressChecks
{
    private const string Key = "Outbreak.Phase17Stress";
    private static int stage;
    private static double deadline;
    private static float waitUntil;
    private static EnemyWorld world;
    private static GameUIController ui;
    private static bool errors;
    private static readonly List<EnemyActor> actors = new List<EnemyActor>();
    private static readonly List<Vector3> starts = new List<Vector3>();
    static Phase17StressChecks()
    {
        if (SessionState.GetBool(Key, false))
        {
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Batch only");
        OutbreakEditor.Open();
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new Exception("Stress check timed out");
            if (!EditorApplication.isPlaying)
                return;
            if (ui == null)
                ui = OutbreakEditor.One<GameUIController>();
            if (ui == null)
                return;
            ui.Resume();
            if (Time.time < .2f)
                return;
            if (stage == 0)
            {
                world = OutbreakEditor.One<EnemyWorld>();
                world.movement.enabled = false;
                OutbreakEditor.One<WaveManager>().enabled = false;
                OutbreakEditor.Values(ui, s => s.FindProperty("pauseOnFocusLoss").boolValue = false);
                for (int i = 0; i < 21; i++)
                {
                    float angle = (i / 21f) * Mathf.PI * 2;
                    Vector3 p = world.player.transform.position + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 12;
                    OutbreakEditor.Require(NavMesh.SamplePosition(p, out var hit, 4, NavMesh.AllAreas), "Stress position on NavMesh");
                    string type = i < 11 ? "Hunter" : i < 16 ? "Runner" : i < 20 ? "Ambusher" : "Elite";
                    var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + type + ".prefab"), hit.position, Quaternion.identity).GetComponent<EnemyActor>();
                    actor.Initialize(world);
                    actors.Add(actor);
                    starts.Add(hit.position);
                }

                var debug = OutbreakEditor.One<AIDebugPresenter>();
                debug.enabled = false;
                OutbreakEditor.Values(debug, s => s.FindProperty("visible").boolValue = true);
                debug.enabled = true;
                waitUntil = Time.time + 3;
                stage = 1;
            }
            else if (stage == 1)
            {
                world.player.Heal(100);
                if (Time.time < waitUntil)
                    return;
                OutbreakCapture.Save("AI_Debug_Crowd");
                world.player.transform.position += Vector3.forward * 3;
                Physics.SyncTransforms();
                world.weapons.SelectWeapon(1);
                waitUntil = Time.time + 4;
                stage = 2;
            }
            else if (stage == 2)
            {
                world.player.Heal(100);
                if (Time.time < waitUntil)
                    return;
                int moved = 0;
                for (int i = 0; i < actors.Count; i++)
                {
                    OutbreakEditor.Require(actors[i].GetComponent<NavMeshAgent>().isOnNavMesh, "Crowd agent stays on mesh");
                    OutbreakEditor.Require(actors[i].Perception.Weapon ==
                        (actors[i].Perception.Visible ? WeaponCategory.Pistol : WeaponCategory.None),
                        "Only visible targets expose the switched weapon");
                    if (Vector3.Distance(starts[i], actors[i].transform.position) > .5f)
                        moved++;
                }

                OutbreakEditor.Require(moved >= 18, "At least 18 of 21 agents progress toward decisions");
                UnityEngine.Debug.Log("PHASE_17_STRESS_PASS: 21 concurrent agents, " + moved + " moved, moving player and switched weapon observed, no runtime errors.");
                world.player.Die();
                foreach (var actor in actors)
                    OutbreakEditor.Require(!actor.CanThink, "All crowd decisions stop on death");
                Finish(0);
            }
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Finish(1);
        }
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
            errors = true;
    }

    private static void Finish(int code)
    {
        SessionState.EraseBool(Key);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        EditorApplication.Exit(errors ? 1 : code);
    }
}
```


## Phase18Setup

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase18Setup.cs

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Phase18Setup
{
    public static void FinalizeAndBuild()
    {
        Phase17Setup.Build();
        Build();
        BuildWindows();
    }

    [MenuItem("Zombie Outbreak/Phase 18 - Assessment display and validation")]
    public static void Build()
    {
        OutbreakEditor.Open();
        TeamArtProjectSetup.EnsureSetup();
        Phase4UIFactory.PrepareResources();
        var hud = GameObject.Find("GameUI/HUDCanvas").transform;
        var debug = OutbreakEditor.Add<AIDebugPresenter>(hud.gameObject);
        var panel = hud.Find("AIDebugPanel");
        if (panel == null)
        {
            panel = Phase4UIFactory.Card(hud, "AIDebugPanel", new Vector2(640, 660), new Vector2(0, 1), new Vector2(48, -48));
            var output = Phase4UIFactory.Label(panel, "Output", "", 22, Color.white, new Vector2(22, 22), new Vector2(596, 616));
            output.textWrappingMode = TextWrappingModes.Normal;
            OutbreakEditor.Set(debug, "output", output);
            panel.gameObject.SetActive(false);
        }

        panel.Find("Output").GetComponent<TMP_Text>().alignment = TextAlignmentOptions.TopLeft;
        OutbreakEditor.Set(debug, "panel", panel.gameObject);
        OutbreakEditor.Set(debug, "world", OutbreakEditor.One<EnemyWorld>());
        OutbreakEditor.Set(debug, "viewCamera", Camera.main);
        Validate();
        BenchmarkPaths();
        OutbreakEditor.Save("18");
    }

    public static void Validate()
    {
        Phase5Setup.Validate();
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                OutbreakEditor.Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "Missing script on " + t.name);
        var types = new[]
        {
            typeof(HunterBrain),
            typeof(RunnerBrain),
            typeof(AmbusherBrain),
            typeof(EliteBrain)
        };
        var names = new[]
        {
            "Hunter",
            "Runner",
            "Ambusher",
            "Elite"
        };
        for (int i = 0; i < 4; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + names[i] + ".prefab");
            OutbreakEditor.Require(prefab != null && prefab.GetComponent(types[i]) != null && prefab.GetComponent<EnemyHealth>() != null && prefab.GetComponent<EnemyMovement>() != null && prefab.GetComponent<EnemyCombat>() != null, "Shared composition + distinct decision script: " + names[i]);
            OutbreakEditor.Require(prefab.GetComponent<Rigidbody>() == null, "Do not combine dynamic Rigidbody with agent");
        }

        var enemy = AssetDatabase.LoadAssetAtPath<AnimatorController>(Phase7Setup.ControllerPath);
        foreach (var state in enemy.layers[0].stateMachine.states)
            OutbreakEditor.Require(state.state.motion != null, "Missing enemy motion");
        var player = AssetDatabase.LoadAssetAtPath<AnimatorController>(Phase2AnimationAssets.ControllerPath);
        OutbreakEditor.Require(player.parameters.Any(p => p.name == "ReloadSpeed"), "Reload speed parameter persisted");
        OutbreakEditor.Require(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)).SequenceEqual(new[] { "MainMenu", "Loadout", "ZombieMap" }), "Correct scene flow in build");
        foreach (var material in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" }).Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))))
            OutbreakEditor.Require(material.shader != null && material.shader.isSupported, "Unsupported material shader: " + material.name);
    }

    private static void BenchmarkPaths()
    {
        var graph = OutbreakEditor.One<HunterNavigationGraph>();
        graph.Build();
        var entrances = Object.FindObjectsByType<MapMarker>().Where(m => m.kind == MapMarkerKind.ZombieEntrance).ToArray();
        var path = new List<Vector3>();
        var timer = new Stopwatch();
        var samples = new List<double>();
        int failures = 0;
        for (int repeat = 0; repeat < 10; repeat++)
            foreach (var entrance in entrances)
            {
                timer.Restart();
                bool found = graph.FindPath(entrance.transform.position, OutbreakEditor.One<PlayerHealth>().transform.position, path);
                timer.Stop();
                samples.Add(timer.Elapsed.TotalMilliseconds);
                if (!found)
                    failures++;
            }

        samples.Sort();
        Directory.CreateDirectory("Logs");
        string report = "A* microbenchmark / Unity Editor / " + Application.unityVersion + "\nSearches: " + samples.Count + "\nFailures: " + failures + "\nMean ms: " + samples.Average().ToString("F3") + "\np95 ms: " + samples[(int)(samples.Count * .95) - 1].ToString("F3") + "\nMax ms: " + samples.Last().ToString("F3") + "\nThis measures path searches only, not whole-game FPS. Hardware, Editor overhead and cache state affect results.\n";
        File.WriteAllText("Logs/NavigationBenchmark.txt", report);
        UnityEngine.Debug.Log(report);
        OutbreakEditor.Require(failures == 0, "All benchmark paths must be complete");
    }

    [MenuItem("Zombie Outbreak/Build Windows Player")]
    public static void BuildWindows()
    {
        // Preserve the latest authored city when rebuilding from the normal menu.
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ExistingCityEnhancer.ScenePath) != null)
        {
            ReferenceCityBuild.Build();
            return;
        }
        OutbreakEditor.Open();
        Validate();
        Directory.CreateDirectory("Builds/Windows");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(), locationPathName = "Builds/Windows/ZombieOutbreak.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/WindowsBuildSummary.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings + "\nSize: " + report.summary.totalSize + "\nTime: " + report.summary.totalTime);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Windows build failed: " + report.summary.result);
        UnityEngine.Debug.Log("WINDOWS_BUILD_PASS: Builds/Windows/ZombieOutbreak.exe");
    }
}
```


## Phase5Setup

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase5Setup.cs

```csharp
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class Phase5Setup
{
    [MenuItem("Zombie Outbreak/Phase 5 - Build district")]
    public static void Build()
    {
        OutbreakEditor.Open();
        OutbreakEditor.Layer(8, "World");
        OutbreakEditor.Layer(9, "Enemies");
        if (GameObject.Find("OutbreakDistrict") == null)
        {
            var old = GameObject.Find("Environment");
            if (old != null)
            {
                old.name = "Phase1Environment_Archived";
                old.SetActive(false);
            }

            var district = OutbreakEditor.Group("OutbreakDistrict");
            var concrete = OutbreakEditor.Material("Concrete", new Color(.34f, .37f, .39f));
            var asphalt = OutbreakEditor.Material("Asphalt", new Color(.13f, .15f, .17f));
            var brick = OutbreakEditor.Material("Brick", new Color(.36f, .24f, .2f));
            var metal = OutbreakEditor.Material("Metal", new Color(.19f, .28f, .28f));
            var wood = OutbreakEditor.Material("Wood", new Color(.38f, .28f, .15f));
            var streets = OutbreakEditor.Group("Streets", district);
            OutbreakEditor.Box("DistrictGround", streets, new Vector3(0, -.25f, 0), new Vector3(76, .5f, 76), concrete);
            OutbreakEditor.Box("MainStreet", streets, new Vector3(0, .015f, 0), new Vector3(10, .03f, 74), asphalt);
            OutbreakEditor.Box("CrossStreet", streets, new Vector3(0, .018f, -8), new Vector3(74, .03f, 9), asphalt);
            var buildings = OutbreakEditor.Group("Buildings", district);
            Room("Warehouse", buildings, new Vector3(-20, 0, 14), new Vector2(20, 20), 5, metal);
            Room("OfficeInterior", buildings, new Vector3(21, 0, 15), new Vector2(18, 18), 4, brick);
            Room("Maintenance", buildings, new Vector3(-22, 0, -24), new Vector2(15, 14), 4, concrete);
            var props = OutbreakEditor.Group("Props", district);
            for (int i = 0; i < 6; i++)
                OutbreakEditor.Box("CoverBlock_" + i, props, new Vector3(i % 2 == 0 ? -8 : 8, .65f, -24 + i * 8), new Vector3(2.5f, 1.3f, 1.2f), concrete);
            OutbreakEditor.Box("AlleyDivider", props, new Vector3(29, 1.2f, -10), new Vector3(1, 2.4f, 12), brick);
            OutbreakEditor.Box("CentralCover", props, new Vector3(-5, .6f, 0), new Vector3(3, 1.2f, 2), metal);
            var barricades = OutbreakEditor.Group("Barricades", district);
            for (int i = 0; i < 3; i++)
                OutbreakEditor.Box("Barricade_" + i, barricades, new Vector3(14 + i * 3, .65f, -20), new Vector3(2.4f, 1.3f, .55f), wood);
            var physics = OutbreakEditor.Group("PhysicsObjects", district);
            for (int i = 0; i < 6; i++)
                OutbreakEditor.Box(i % 2 == 0 ? "Crate_" + i : "Barrel_" + i, physics, new Vector3(-14 + i * 2, .6f, -3), new Vector3(1.1f, 1.2f, 1.1f), i % 2 == 0 ? wood : metal);
            OutbreakEditor.Group("Doors", district);
            OutbreakEditor.Group("Lighting", district);
            var boundary = OutbreakEditor.Group("Boundaries", district);
            OutbreakEditor.Box("North", boundary, new Vector3(0, 2, 38), new Vector3(77, 4, 1), brick);
            OutbreakEditor.Box("South", boundary, new Vector3(0, 2, -38), new Vector3(77, 4, 1), brick);
            OutbreakEditor.Box("East", boundary, new Vector3(38, 2, 0), new Vector3(1, 4, 77), brick);
            OutbreakEditor.Box("West", boundary, new Vector3(-38, 2, 0), new Vector3(1, 4, 77), brick);
            var spawn = OutbreakEditor.Group("SpawnPoints", district);
            Marker("PlayerSpawn", spawn, new Vector3(0, .1f, -17), MapMarkerKind.PlayerSpawn);
            Vector3[] positions =
            {
                new Vector3(-32, 0, 30),
                new Vector3(0, 0, 33),
                new Vector3(33, 0, 28),
                new Vector3(33, 0, -30),
                new Vector3(-32, 0, -33),
                new Vector3(-33, 0, 0)
            };
            for (int i = 0; i < positions.Length; i++)
                Marker("Entrance_" + i, spawn, positions[i], MapMarkerKind.ZombieEntrance);
            var tactics = OutbreakEditor.Group("AmbushPoints", district);
            Vector3[] ambush =
            {
                new Vector3(-27, 0, 14),
                new Vector3(-12, 0, 18),
                new Vector3(20, 0, 21),
                new Vector3(25, 0, -8),
                new Vector3(-17, 0, -24),
                new Vector3(12, 0, -22),
                new Vector3(-6, 0, 4),
                new Vector3(8, 0, 20)
            };
            for (int i = 0; i < ambush.Length; i++)
                Marker("Tactical_" + i, tactics, ambush[i], MapMarkerKind.Ambush);
            Marker("EliteArena", tactics, new Vector3(21, 0, -27), MapMarkerKind.EliteArena);
        }

        var player = OutbreakEditor.One<ThirdPersonPlayerController>();
        var playerSpawn = Object.FindObjectsByType<MapMarker>().First(m => m.kind == MapMarkerKind.PlayerSpawn);
        player.transform.position = playerSpawn.transform.position;
        var surface = OutbreakEditor.Add<NavMeshSurface>(GameObject.Find("OutbreakDistrict"));
        surface.collectObjects = CollectObjects.Children;
        surface.layerMask = 1 << 8;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        Validate();
        OutbreakEditor.Save("5");
    }

    private static void Room(string name, Transform parent, Vector3 center, Vector2 size, float height, Material mat)
    {
        var room = OutbreakEditor.Group(name, parent);
        room.position = center;
        float x = size.x / 2, z = size.y / 2;
        // Four broad entrances support loops and flanks; lintels preserve the room silhouette.
        foreach (float side in new[]
        {
            -1f,
            1f
        }

        )
        {
            foreach (float end in new[]
            {
                -1f,
                1f
            }

            )
            {
                OutbreakEditor.Box("FrontWall", room, new Vector3(end * (x + 2) / 2, height / 2, side * z), new Vector3(x - 2, height, .55f), mat);
                OutbreakEditor.Box("SideWall", room, new Vector3(side * x, height / 2, end * (z + 2) / 2), new Vector3(.55f, height, z - 2), mat);
            }

            OutbreakEditor.Box("FrontLintel", room, new Vector3(0, height - .4f, side * z), new Vector3(4, .8f, .55f), mat);
            OutbreakEditor.Box("SideLintel", room, new Vector3(side * x, height - .4f, 0), new Vector3(.55f, .8f, 4), mat);
        }
    }

    private static void Marker(string name, Transform parent, Vector3 position, MapMarkerKind kind)
    {
        var marker = OutbreakEditor.Group(name, parent).gameObject.AddComponent<MapMarker>();
        marker.transform.position = position;
        marker.kind = kind;
    }

    public static void Validate()
    {
        var markers = Object.FindObjectsByType<MapMarker>();
        OutbreakEditor.Require(markers.Count(m => m.kind == MapMarkerKind.ZombieEntrance) >= 6, "Six entrances required.");
        var spawn = markers.First(m => m.kind == MapMarkerKind.PlayerSpawn);
        OutbreakEditor.Require(NavMesh.SamplePosition(spawn.transform.position, out var origin, 2, NavMesh.AllAreas), "Player spawn outside NavMesh.");
        foreach (var m in markers)
        {
            OutbreakEditor.Require(NavMesh.SamplePosition(m.transform.position, out var hit, 2, NavMesh.AllAreas), m.name + " outside NavMesh");
            var path = new NavMeshPath();
            OutbreakEditor.Require(NavMesh.CalculatePath(hit.position, origin.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, m.name + " unreachable");
        }
    }
}
```


## Phase6Setup

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase6Setup.cs

```csharp
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class Phase6Setup
{
    [MenuItem("Zombie Outbreak/Phase 6 - Add interactions")]
    public static void Build()
    {
        OutbreakEditor.Open();
        var district = GameObject.Find("OutbreakDistrict").transform;
        foreach (Transform t in district.Find("PhysicsObjects"))
            ConfigureProp(t.gameObject, t.name.StartsWith("Crate") ? 35 : 55);
        foreach (Transform t in district.Find("Barricades"))
        {
            ConfigureProp(t.gameObject, 70);
            OutbreakEditor.Add<DestructibleBarricade>(t.gameObject);
        }

        var doors = district.Find("Doors");
        if (doors.childCount == 0)
        {
            var hinge = OutbreakEditor.Group("WarehouseDoor", doors);
            hinge.position = new Vector3(-22, 0, 4);
            OutbreakEditor.Add<InteractiveDoor>(hinge.gameObject);
            var leaf = OutbreakEditor.Box("DoorLeaf", hinge, new Vector3(1, 1.5f, 0), new Vector3(2, 3, .18f), OutbreakEditor.Material("DoorMetal", new Color(.2f, .32f, .35f)));
            DynamicObstacle(leaf, false);
        }

        var player = OutbreakEditor.One<PlayerHealth>();
        var interact = OutbreakEditor.Add<PlayerInteractor>(player.gameObject);
        OutbreakEditor.Set(interact, "viewCamera", Camera.main);
        OutbreakEditor.Set(interact, "health", player);
        OutbreakEditor.Add<PlayerPhysicsPush>(player.gameObject);
        district.GetComponent<NavMeshSurface>().BuildNavMesh();
        Phase5Setup.Validate();
        OutbreakEditor.Save("6");
    }

    private static void ConfigureProp(GameObject go, float mass)
    {
        var body = OutbreakEditor.Add<Rigidbody>(go);
        body.mass = mass;
        body.linearDamping = 1.8f;
        body.angularDamping = 2;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        DynamicObstacle(go, true);
    }

    private static void DynamicObstacle(GameObject go, bool stationary)
    {
        var modifier = OutbreakEditor.Add<NavMeshModifier>(go);
        modifier.ignoreFromBuild = true;
        var obstacle = OutbreakEditor.Add<NavMeshObstacle>(go);
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = Vector3.one;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = stationary;
        obstacle.carvingMoveThreshold = .2f;
        obstacle.carvingTimeToStationary = .3f;
    }
}
```


## Phase7Setup

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase7Setup.cs

```csharp
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

public static class Phase7Setup
{
    public const string BasePrefab = "Assets/Prefabs/Enemies/EnemyBase.prefab";
    public const string ControllerPath = "Assets/Animations/Enemies/Enemy.controller";
    [MenuItem("Zombie Outbreak/Phase 7 - Shared enemies")]
    public static void Build()
    {
        OutbreakEditor.Open();
        Phase2AnimationAssets.Folder("Assets/Prefabs/Enemies");
        Phase2AnimationAssets.Folder("Assets/Animations/Enemies");
        var world = OutbreakEditor.Add<EnemyWorld>(OutbreakEditor.Group("EnemySystems").gameObject);
        world.player = OutbreakEditor.One<PlayerHealth>();
        world.weapons = OutbreakEditor.One<PlayerWeaponController>();
        world.movement = OutbreakEditor.One<ThirdPersonPlayerController>();
        world.tacticalPoints = Object.FindObjectsByType<MapMarker>().Where(m => m.kind == MapMarkerKind.Ambush || m.kind == MapMarkerKind.EliteArena).ToArray();
        var controller = MakeController();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab) == null)
        {
            var go = new GameObject("EnemyBase");
            go.layer = 9;
            var rig = Phase2HumanoidFactory.Create("Assets/Animations/Enemies/EnemyAvatar.asset");
            rig.Root.name = "Visual";
            rig.Root.transform.SetParent(go.transform, false);
            rig.Animator.runtimeAnimatorController = controller;
            rig.Animator.enabled = true;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = Vector3.up * .95f;
            capsule.height = 1.9f;
            capsule.radius = .35f;
            var agent = go.AddComponent<NavMeshAgent>();
            agent.height = 1.9f;
            agent.radius = .4f;
            agent.speed = 2.2f;
            agent.acceleration = 12;
            agent.angularSpeed = 540;
            agent.stoppingDistance = 1.2f;
            agent.autoBraking = true;
            go.AddComponent<EnemyHealth>();
            go.AddComponent<EnemyMovement>();
            go.AddComponent<EnemyCombat>();
            go.AddComponent<EnemyPerception>();
            go.AddComponent<EnemyActor>();
            var animation = go.AddComponent<EnemyAnimationController>();
            OutbreakEditor.Set(animation, "animator", rig.Animator);
            PrefabUtility.SaveAsPrefabAsset(go, BasePrefab);
            Object.DestroyImmediate(go);
        }

        OutbreakEditor.Save("7");
    }

    private static AnimatorController MakeController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
            return controller;
        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (string trigger in new[]
        {
            "Attack",
            "Heavy",
            "Hit"
        }

        )
            controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        var sm = controller.layers[0].stateMachine;
        var locomotion = sm.AddState("Locomotion");
        sm.defaultState = locomotion;
        var tree = new BlendTree
        {
            name = "EnemyLocomotion",
            blendParameter = "Speed",
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(Clip("Idle"), 0);
        tree.AddChild(Clip("WalkForward"), 1.5f);
        tree.AddChild(Clip("Run"), 3.5f);
        locomotion.motion = tree;
        foreach (string action in new[]
        {
            "Attack",
            "Heavy",
            "Hit",
            "Death"
        }

        )
        {
            var state = sm.AddState(action);
            state.motion = Clip(action == "Attack" || action == "Heavy" ? "Fire" : action);
            if (action == "Attack" || action == "Heavy")
            {
                state.speedParameter = "AttackSpeed";
                state.speedParameterActive = true;
            }

            var enter = sm.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = .06f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(action == "Death" ? AnimatorConditionMode.If : AnimatorConditionMode.If, 0, action == "Death" ? "Dead" : action);
            if (action != "Death")
            {
                enter.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                var exit = state.AddTransition(locomotion);
                exit.hasExitTime = true;
                exit.exitTime = 1;
                exit.duration = .1f;
            }
        }

        return controller;
    }

    private static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Phase2/Clips/" + name + ".anim");
}
```


## Phase8Setup

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase8Setup.cs

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class Phase8Setup
{
    [MenuItem("Zombie Outbreak/Phase 8 - Hunter A-star")]
    public static void Build()
    {
        OutbreakEditor.Open();
        var graph = OutbreakEditor.Add<HunterNavigationGraph>(OutbreakEditor.One<EnemyWorld>().gameObject);
        graph.Build();
        var markers = Object.FindObjectsByType<MapMarker>();
        var path = new List<Vector3>();
        foreach (var marker in markers)
            if (marker.kind == MapMarkerKind.ZombieEntrance)
                OutbreakEditor.Require(graph.FindPath(marker.transform.position, OutbreakEditor.One<PlayerHealth>().transform.position, path), "A* failed from " + marker.name);
        EnemyPrefabFactory.Create<HunterBrain>("Hunter", 60, new Color(.4f, .52f, .31f));
        OutbreakEditor.Save("8");
    }
}
```


## Phase9Setup

Full file path: D:/GV project/Assets/Editor/Outbreak/Phase9Setup.cs

```csharp
using UnityEditor;
using UnityEngine;

public static class Phase9Setup
{
    [MenuItem("Zombie Outbreak/Phase 9 - Runner utility AI")]
    public static void Build()
    {
        OutbreakEditor.Open();
        EnemyPrefabFactory.Create<RunnerBrain>("Runner", 45, new Color(.7f, .42f, .17f));
        var scores = new float[5];
        RunnerBrain.Score(5, 1, 0, true, true, true, true, scores);
        float caution = scores[2];
        float chase = scores[0];
        RunnerBrain.Score(5, 1, 1, true, true, true, true, scores);
        OutbreakEditor.Require(scores[0] > chase && scores[2] < caution, "Weak player should increase aggression.");
        RunnerBrain.Score(5, 1, 0, false, false, true, true, scores);
        OutbreakEditor.Require(scores[2] < caution, "Shotgun should increase caution.");
        OutbreakEditor.Save("9");
    }
}
```


## TeamArtIntegration

Full file path: D:/GV project/Assets/Editor/Outbreak/TeamArtIntegration.cs

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Opt-in integration of supplied, validated models; empty catalog fields leave greybox intact.</summary>
public static class TeamArtIntegration
{
    public const string CatalogPath = "Assets/Data/TeamArtCatalog.asset";

    [MenuItem("Zombie Outbreak/Team Art/Validate supplied models")]
    public static void ValidateSuppliedModels()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<TeamArtCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning("No TeamArtCatalog exists. Greybox placeholders remain active.");
            return;
        }

        bool hasAny = catalog.zombieVisualPrefab != null || catalog.barricadeVisualPrefab != null;
        if (hasAny && !catalog.modelsAuthoredByTeam)
            Debug.Log("Team model validation will check asset compatibility, but authorship has not been verified. Keep the contribution record accurate.");
        if (catalog.zombieVisualPrefab != null) ValidateZombie(catalog.zombieVisualPrefab);
        else Debug.LogWarning("Original zombie prefab is not assigned. See TEAM_MODEL_HANDOFF.md.");
        if (catalog.barricadeVisualPrefab != null) ValidateBarricade(catalog.barricadeVisualPrefab);
        else Debug.LogWarning("Original barricade prefab is not assigned. See TEAM_MODEL_HANDOFF.md.");
    }

    [MenuItem("Zombie Outbreak/Team Art/Apply validated models to game")]
    public static void Apply()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<TeamArtCatalog>(CatalogPath);
        if (catalog == null || (catalog.zombieVisualPrefab == null && catalog.barricadeVisualPrefab == null))
        {
            Debug.LogWarning("No original model prefabs are assigned. Nothing was changed; greybox placeholders remain active.");
            return;
        }

        if (catalog.zombieVisualPrefab != null) ApplyZombie(catalog.zombieVisualPrefab);
        if (catalog.barricadeVisualPrefab != null)
        {
            ApplyBarricades(catalog);
            RebuildNavigation();
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Supplied visual references applied. Verify materials, scale, Avatar, collider fit and navigation in Play Mode.");
    }

    private static void ValidateZombie(GameObject prefab)
    {
        OutbreakEditor.Require(PrefabUtility.IsPartOfPrefabAsset(prefab), "Zombie visual must be a saved prefab asset.");
        var animator = prefab.GetComponentInChildren<Animator>(true);
        OutbreakEditor.Require(animator != null, "Zombie visual prefab needs a Humanoid Animator.");
        OutbreakEditor.Require(animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman,
            "Zombie Animator Avatar must be valid and Humanoid.");
        OutbreakEditor.Require(animator.runtimeAnimatorController == null ||
            animator.runtimeAnimatorController == AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Phase7Setup.ControllerPath),
            "Use the shared Enemy controller so shared state parameters stay consistent.");
        OutbreakEditor.Require(prefab.GetComponentInChildren<NavMeshAgent>(true) == null && prefab.GetComponent<Rigidbody>() == null,
            "Visual-only prefab must not duplicate movement or root physics.");
        var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        OutbreakEditor.Require(renderers.Any(r => r.sharedMesh != null && r.sharedMesh.vertexCount > 100),
            "Zombie should include a skinned mesh with more than 100 vertices.");
        foreach (var renderer in renderers)
            OutbreakEditor.Require(renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(m => m != null),
                renderer.name + " needs assigned materials.");
        Debug.Log("Zombie prefab compatibility passed. Team authorship remains a team record, not a Unity-verifiable property.");
    }

    private static void ValidateBarricade(GameObject prefab)
    {
        OutbreakEditor.Require(PrefabUtility.IsPartOfPrefabAsset(prefab), "Barricade visual must be a saved prefab asset.");
        OutbreakEditor.Require(prefab.GetComponentInChildren<Renderer>(true) != null, "Barricade prefab needs a Renderer.");
        OutbreakEditor.Require(prefab.GetComponentInChildren<Collider>(true) == null && prefab.GetComponent<Rigidbody>() == null &&
            prefab.GetComponent<NavMeshObstacle>() == null, "Visual prefab must not carry physics or NavMesh components.");
        OutbreakEditor.Require(prefab.transform.localScale == Vector3.one, "Apply scale in the source model and keep prefab root scale one.");
        Debug.Log("Barricade prefab compatibility passed. Confirm its dimensions in metres before applying.");
    }

    private static void ApplyZombie(GameObject visualPrefab)
    {
        ValidateZombie(visualPrefab);
        const string path = Phase7Setup.BasePrefab;
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var anchor = root.transform.Find("Visual");
            OutbreakEditor.Require(anchor != null, "EnemyBase prefab is missing the Visual anchor.");
            foreach (Transform child in anchor.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, anchor);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            var animator = instance.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Phase7Setup.ControllerPath);
            animator.applyRootMotion = false;
            var driver = root.GetComponent<EnemyAnimationController>();
            OutbreakEditor.Set(driver, "animator", animator);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ApplyBarricades(TeamArtCatalog catalog)
    {
        ValidateBarricade(catalog.barricadeVisualPrefab);
        var scene = EditorSceneManager.OpenScene(OutbreakEditor.Scene);
        var parent = GameObject.Find("OutbreakDistrict/Barricades");
        OutbreakEditor.Require(parent != null, "ZombieMap has no Barricades group.");
        foreach (Transform barricade in parent.transform)
        {
            if (barricade.Find("TeamBarricadeVisual") != null) Object.DestroyImmediate(barricade.Find("TeamBarricadeVisual").gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(catalog.barricadeVisualPrefab, barricade);
            visual.name = "TeamBarricadeVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            var originalRenderer = barricade.GetComponent<Renderer>();
            if (originalRenderer != null) originalRenderer.enabled = false;
            var obstacle = barricade.GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.size = new Vector3(2.4f, 1.3f, .55f);
            var damage = barricade.GetComponent<DestructibleBarricade>();
            if (damage != null && catalog.brokenDebrisPrefab != null)
                OutbreakEditor.Set(damage, "debrisPrefab", catalog.brokenDebrisPrefab);
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void RebuildNavigation()
    {
        var surface = GameObject.Find("OutbreakDistrict")?.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
        if (surface == null) { Debug.LogWarning("No NavMeshSurface found; rebuild navigation after applying the model."); return; }
        surface.BuildNavMesh();
        Phase5Setup.Validate();
    }
}
```


## TeamArtProjectSetup

Full file path: D:/GV project/Assets/Editor/Outbreak/TeamArtProjectSetup.cs

```csharp
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the team-art drop locations and catalog available in a fresh checkout.
/// This creates project folders and an empty catalog only; it does not create or
/// claim authorship of any art assets.
/// </summary>
[InitializeOnLoad]
internal static class TeamArtProjectSetup
{
    private const string CatalogPath = "Assets/Data/TeamArtCatalog.asset";

    static TeamArtProjectSetup()
    {
        EditorApplication.delayCall += EnsureSetup;
    }

    public static void EnsureSetup()
    {
        EnsureFolder("Assets/Models", "Team");
        EnsureFolder("Assets/Models/Team", "Zombie");
        EnsureFolder("Assets/Models/Team/Zombie", "Source");
        EnsureFolder("Assets/Models/Team/Zombie", "Export");
        EnsureFolder("Assets/Models/Team/Zombie", "Textures");
        EnsureFolder("Assets/Models/Team", "Barricade");
        EnsureFolder("Assets/Models/Team/Barricade", "Source");
        EnsureFolder("Assets/Models/Team/Barricade", "Export");
        EnsureFolder("Assets/Models/Team/Barricade", "Textures");

        if (AssetDatabase.LoadAssetAtPath<TeamArtCatalog>(CatalogPath) != null)
            return;

        TeamArtCatalog catalog = ScriptableObject.CreateInstance<TeamArtCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        AssetDatabase.SaveAssets();
        Debug.Log("Created the Team Art Catalog. Add the team's original zombie and barricade exports when ready.");
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
```


## Phase1SceneBuilder

Full file path: D:/GV project/Assets/Editor/Phase1SceneBuilder.cs

```csharp
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Builds the Phase 1 greybox scene and player prefab from the Unity Editor menu.</summary>
public static class Phase1SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/ZombieMap.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
    private const int IgnoreRaycastLayer = 2;

    [MenuItem("Tools/Phase 1/Build ZombieMap Greybox")]
    public static void BuildZombieMap()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSettings.serializationMode = SerializationMode.ForceText;
        UnityEditor.VersionControlSettings.mode = "Visible Meta Files";
        EnsureFolders();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "ZombieMap";

        Material groundMaterial = CreateOrLoadMaterial(
            "Assets/Materials/Greybox/Ground_Grey.mat",
            new Color(0.34f, 0.36f, 0.38f));
        Material coverMaterial = CreateOrLoadMaterial(
            "Assets/Materials/Greybox/Cover_Grey.mat",
            new Color(0.46f, 0.48f, 0.5f));
        Material playerMaterial = CreateOrLoadMaterial(
            "Assets/Materials/Greybox/Player_Grey.mat",
            new Color(0.25f, 0.42f, 0.58f));

        Transform environment = new GameObject("Environment").transform;
        CreateEnvironment(environment, groundMaterial, coverMaterial);

        GameObject player = CreatePlayer(playerMaterial);
        ThirdPersonPlayerController playerController = player.GetComponent<ThirdPersonPlayerController>();
        Transform cameraTarget = player.transform.Find("CameraTarget");
        Transform aimTarget = player.transform.Find("CameraTarget/AimTarget");

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera playerCamera = cameraObject.AddComponent<Camera>();
        playerCamera.fieldOfView = 60f;
        playerCamera.nearClipPlane = 0.1f;
        playerCamera.farClipPlane = 500f;
        cameraObject.AddComponent<AudioListener>();
        ThirdPersonCameraController cameraController = cameraObject.AddComponent<ThirdPersonCameraController>();
        SetObjectReference(cameraController, "cameraTarget", cameraTarget);
        SetObjectReference(cameraController, "aimTarget", aimTarget);

        cameraObject.transform.position = player.transform.position + new Vector3(0f, 2.2f, -4.5f);
        cameraObject.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

        GameObject lightObject = new GameObject("Directional Light");
        Light directionalLight = lightObject.AddComponent<Light>();
        directionalLight.type = LightType.Directional;
        directionalLight.intensity = 1.1f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.52f, 0.52f, 0.52f);

        PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
        SetObjectReference(playerController, "cameraTransform", cameraObject.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool sceneAlreadyInBuild = false;
        for (int i = 0; i < buildScenes.Count; i++)
        {
            if (buildScenes[i].path == ScenePath)
            {
                buildScenes[i] = new EditorBuildSettingsScene(ScenePath, true);
                sceneAlreadyInBuild = true;
                break;
            }
        }

        if (!sceneAlreadyInBuild)
        {
            buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        }

        EditorBuildSettings.scenes = buildScenes.ToArray();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Phase 1 scene created: " + ScenePath + ". Player prefab: " + PlayerPrefabPath);
    }

    private static GameObject CreatePlayer(Material playerMaterial)
    {
        GameObject player = new GameObject("Player");
        player.layer = IgnoreRaycastLayer;
        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = 1.8f;
        characterController.radius = 0.32f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.stepOffset = 0.3f;
        characterController.slopeLimit = 45f;
        characterController.skinWidth = 0.08f;

        ThirdPersonPlayerController movement = player.AddComponent<ThirdPersonPlayerController>();
        player.AddComponent<PlayerHealth>();

        Transform visuals = new GameObject("Humanoid_Greybox").transform;
        visuals.SetParent(player.transform, false);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "Torso", new Vector3(0f, 1.08f, 0f), new Vector3(0.42f, 0.52f, 0.27f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.66f, 0f), new Vector3(0.28f, 0.28f, 0.28f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "LeftArm", new Vector3(-0.34f, 1.08f, 0f), new Vector3(0.14f, 0.4f, 0.14f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "RightArm", new Vector3(0.34f, 1.08f, 0f), new Vector3(0.14f, 0.4f, 0.14f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "LeftLeg", new Vector3(-0.13f, 0.47f, 0f), new Vector3(0.16f, 0.44f, 0.16f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "RightLeg", new Vector3(0.13f, 0.47f, 0f), new Vector3(0.16f, 0.44f, 0.16f), playerMaterial);

        Transform cameraTarget = CreateChild(player.transform, "CameraTarget", new Vector3(0f, 1.48f, 0f));
        CreateChild(cameraTarget, "AimTarget", new Vector3(0f, 0f, 20f));
        CreateChild(player.transform, "WeaponSocket", new Vector3(0.34f, 1.12f, 0.22f));
        Transform groundCheck = CreateChild(player.transform, "GroundCheck", new Vector3(0f, 0.08f, 0f));
        SetObjectReference(movement, "groundCheck", groundCheck);

        SetLayerRecursively(player.transform, IgnoreRaycastLayer);
        return player;
    }

    private static void CreateEnvironment(Transform parent, Material groundMaterial, Material coverMaterial)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Ground_Greybox";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = Vector3.zero;
        floor.transform.localScale = new Vector3(5f, 1f, 5f);
        ApplyMaterial(floor, groundMaterial);

        CreateBlock(parent, "Cover_West", new Vector3(-3.5f, 0.55f, 2.5f), new Vector3(1.4f, 1.1f, 0.7f), coverMaterial);
        CreateBlock(parent, "Cover_East", new Vector3(3.5f, 0.55f, 3f), new Vector3(1.4f, 1.1f, 0.7f), coverMaterial);
        CreateBlock(parent, "Cover_North", new Vector3(0f, 0.6f, 6f), new Vector3(3f, 1.2f, 0.7f), coverMaterial);
        CreateBlock(parent, "LowBarrier_West", new Vector3(-7f, 0.4f, -3f), new Vector3(3f, 0.8f, 0.6f), coverMaterial);
        CreateBlock(parent, "LowBarrier_East", new Vector3(7f, 0.4f, -3f), new Vector3(3f, 0.8f, 0.6f), coverMaterial);
        CreateBlock(parent, "Boundary_North", new Vector3(0f, 1.5f, 20f), new Vector3(40f, 3f, 0.5f), coverMaterial);
        CreateBlock(parent, "Boundary_South", new Vector3(0f, 1.5f, -20f), new Vector3(40f, 3f, 0.5f), coverMaterial);
        CreateBlock(parent, "Boundary_East", new Vector3(20f, 1.5f, 0f), new Vector3(0.5f, 3f, 40f), coverMaterial);
        CreateBlock(parent, "Boundary_West", new Vector3(-20f, 1.5f, 0f), new Vector3(0.5f, 3f, 40f), coverMaterial);
    }

    private static void CreateBlock(Transform parent, string objectName, Vector3 position, Vector3 size, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = position;
        block.transform.localScale = size;
        ApplyMaterial(block, material);
    }

    private static GameObject CreateBodyPart(
        Transform parent,
        PrimitiveType primitive,
        string objectName,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = objectName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            Object.DestroyImmediate(partCollider);
        }
        ApplyMaterial(part, material);
        SetLayerRecursively(part.transform, IgnoreRaycastLayer);
        return part;
    }

    private static Transform CreateChild(Transform parent, string childName, Vector3 localPosition)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        return child.transform;
    }

    private static Material CreateOrLoadMaterial(string assetPath, Color color)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (existing != null)
        {
            existing.color = color;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (shader == null)
        {
            Debug.LogError("No Standard or URP/Lit shader was found. The greybox will use Unity's default material.");
            return null;
        }

        Material material = new Material(shader);
        material.color = color;
        AssetDatabase.CreateAsset(material, assetPath);
        return material;
    }

    private static void ApplyMaterial(GameObject target, Material material)
    {
        if (material == null)
        {
            return;
        }

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Prefabs/Player");
        EnsureFolder("Assets/Materials/Greybox");
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string currentPath = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }
            currentPath = nextPath;
        }
    }
}
#endif
```


## Phase2AnimationAssets

Full file path: D:/GV project/Assets/Editor/Phase2/Phase2AnimationAssets.cs

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Creates missing native Humanoid clips and the shared Animator Controller; preserves existing assets.</summary>
public static class Phase2AnimationAssets
{
    public const string Root = "Assets/Animations/Phase2";
    public const string ControllerPath = Root + "/PlayerHumanoid.controller";
    public const string AvatarPath = Root + "/GreyboxHumanoidAvatar.asset";
    public const string ModelPath = "Assets/Prefabs/Player/Phase2Humanoid.prefab";

    public static AnimatorController EnsureAssets()
    {
        Folder(Root + "/Clips");
        Folder("Assets/Materials");
        Folder("Assets/Prefabs/Player");
        Phase2HumanoidFactory.Rig rig = Phase2HumanoidFactory.Create(AvatarPath);
        try
        {
            var clips = new Dictionary<string, AnimationClip>();
            string[] names =
            {
                "Idle", "WalkForward", "WalkBackward", "StrafeLeft", "StrafeRight", "Run", "Sprint",
                "Jump", "Falling", "AimIdle", "AimForward", "AimBackward", "AimLeft", "AimRight",
                "Fire", "Reload", "Hit", "Death"
            };
            foreach (string name in names)
            {
                string path = Root + "/Clips/" + name + ".anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    float duration = name == "Sprint" ? 0.55f : name == "Run" ? 0.7f :
                        name == "Fire" ? 0.18f : name == "Hit" ? 0.35f :
                        name == "Jump" ? 0.4f : name == "Reload" ? 1.2f : name == "Death" ? 1.1f : 1f;
                    bool loop = name != "Jump" && name != "Fire" && name != "Reload" && name != "Hit" && name != "Death";
                    clip = Phase2HumanoidFactory.CreateClip(rig, name, duration, loop);
                    AssetDatabase.CreateAsset(clip, path);
                }
                clips.Add(name, clip);
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = BuildController(clips);
            Phase2HumanoidFactory.ResetPose(rig);
            rig.Animator.runtimeAnimatorController = controller;
            rig.Animator.enabled = true;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
                PrefabUtility.SaveAsPrefabAsset(rig.Root, ModelPath);
            AssetDatabase.SaveAssets();
            return controller;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rig.Root);
        }
    }

    private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
    {
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (string name in new[] { "Speed", "Horizontal", "Vertical", "VerticalVelocity" })
            controller.AddParameter(name, AnimatorControllerParameterType.Float);
        foreach (string name in new[] { "IsAiming", "IsGrounded", "IsSprinting", "IsDead" })
            controller.AddParameter(name, AnimatorControllerParameterType.Bool);
        foreach (string name in new[] { "Fire", "Reload", "Hit", "Death" })
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        BlendTree walk = Directional(controller, "Walk Direction", clips["Idle"], clips["WalkForward"],
            clips["WalkBackward"], clips["StrafeLeft"], clips["StrafeRight"]);
        BlendTree locomotion = Tree(controller, "Exploration Speed", BlendTreeType.Simple1D, "Speed");
        locomotion.useAutomaticThresholds = false;
        locomotion.AddChild(clips["Idle"], 0f);
        locomotion.AddChild(walk, 1f);
        locomotion.AddChild(clips["Run"], 2f);
        locomotion.AddChild(clips["Sprint"], 3f);
        BlendTree aiming = Directional(controller, "Aim Direction", clips["AimIdle"], clips["AimForward"],
            clips["AimBackward"], clips["AimLeft"], clips["AimRight"]);

        AnimatorState normal = State(machine, "Locomotion", locomotion, new Vector3(250f, 70f));
        AnimatorState aim = State(machine, "Aim Locomotion", aiming, new Vector3(500f, 70f));
        AnimatorState jump = State(machine, "Jump", clips["Jump"], new Vector3(250f, 220f));
        AnimatorState fall = State(machine, "Falling", clips["Falling"], new Vector3(500f, 220f));
        AnimatorState death = State(machine, "Death", clips["Death"], new Vector3(750f, 70f));
        machine.defaultState = normal;

        // Air transitions take precedence over aim changes.
        foreach (AnimatorState ground in new[] { normal, aim })
        {
            AnimatorStateTransition rising = Transition(ground, jump, 0.06f);
            Bool(rising, "IsGrounded", false);
            rising.AddCondition(AnimatorConditionMode.Greater, 0f, "VerticalVelocity");
            AnimatorStateTransition dropping = Transition(ground, fall, 0.06f);
            Bool(dropping, "IsGrounded", false);
            dropping.AddCondition(AnimatorConditionMode.Less, 0.01f, "VerticalVelocity");
        }

        AnimatorStateTransition enterAim = Transition(normal, aim, 0.15f);
        Bool(enterAim, "IsAiming", true);
        Bool(enterAim, "IsGrounded", true);
        AnimatorStateTransition leaveAim = Transition(aim, normal, 0.15f);
        Bool(leaveAim, "IsAiming", false);
        Bool(leaveAim, "IsGrounded", true);
        Transition(jump, fall, 0.1f).AddCondition(AnimatorConditionMode.Less, 0.01f, "VerticalVelocity");
        foreach (AnimatorState air in new[] { jump, fall })
        {
            AnimatorStateTransition land = Transition(air, normal, 0.1f);
            Bool(land, "IsGrounded", true);
            Bool(land, "IsAiming", false);
            AnimatorStateTransition landAim = Transition(air, aim, 0.1f);
            Bool(landAim, "IsGrounded", true);
            Bool(landAim, "IsAiming", true);
        }
        AnimatorStateTransition die = machine.AddAnyStateTransition(death);
        SetupTransition(die, 0.12f);
        Bool(die, "IsDead", true);
        die.AddCondition(AnimatorConditionMode.If, 0f, "Death");

        AvatarMask mask = new AvatarMask { name = "UpperBody" };
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (AvatarMaskBodyPart part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers,
            AvatarMaskBodyPart.RightFingers })
            mask.SetHumanoidBodyPartActive(part, true);
        AssetDatabase.CreateAsset(mask, Root + "/UpperBody.mask");

        controller.AddLayer("Upper Body Actions");
        AnimatorControllerLayer[] layers = controller.layers;
        layers[1].avatarMask = mask;
        layers[1].defaultWeight = 0f; // Driver fades this layer only while an action is active.
        layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
        controller.layers = layers;
        AnimatorStateMachine actions = controller.layers[1].stateMachine;
        AnimatorState empty = State(actions, "Empty", null, new Vector3(230f, 40f));
        actions.defaultState = empty;
        int row = 0;
        foreach (string action in new[] { "Fire", "Reload", "Hit" })
        {
            AnimatorState state = State(actions, action, clips[action], new Vector3(450f, 40f + 80f * row++));
            AnimatorStateTransition start = actions.AddAnyStateTransition(state);
            SetupTransition(start, 0.04f);
            Bool(start, "IsDead", false);
            start.AddCondition(AnimatorConditionMode.If, 0f, action);
            AnimatorStateTransition finish = state.AddTransition(empty);
            SetupTransition(finish, 0.1f);
            finish.hasExitTime = true;
            finish.exitTime = 0.9f;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static BlendTree Directional(AnimatorController controller, string name, Motion idle,
        Motion forward, Motion backward, Motion left, Motion right)
    {
        BlendTree tree = Tree(controller, name, BlendTreeType.SimpleDirectional2D, "Horizontal");
        tree.blendParameterY = "Vertical";
        tree.AddChild(idle, Vector2.zero);
        tree.AddChild(forward, Vector2.up);
        tree.AddChild(backward, Vector2.down);
        tree.AddChild(left, Vector2.left);
        tree.AddChild(right, Vector2.right);
        return tree;
    }

    private static BlendTree Tree(AnimatorController controller, string name, BlendTreeType type, string parameter)
    {
        BlendTree tree = new BlendTree { name = name, blendType = type, blendParameter = parameter };
        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    private static AnimatorState State(AnimatorStateMachine machine, string name, Motion motion, Vector3 position)
    {
        AnimatorState state = machine.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = true;
        return state;
    }

    private static AnimatorStateTransition Transition(AnimatorState source, AnimatorState target, float duration)
    {
        AnimatorStateTransition transition = source.AddTransition(target);
        SetupTransition(transition, duration);
        Bool(transition, "IsDead", false);
        return transition;
    }

    private static void SetupTransition(AnimatorStateTransition transition, float duration)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.interruptionSource = TransitionInterruptionSource.None;
    }

    private static void Bool(AnimatorStateTransition transition, string name, bool value)
        => transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, name);

    public static void Folder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            if (!AssetDatabase.IsValidFolder(current + "/" + parts[i]))
                AssetDatabase.CreateFolder(current, parts[i]);
            current += "/" + parts[i];
        }
    }
}
```


## Phase2HumanoidFactory

Full file path: D:/GV project/Assets/Editor/Phase2/Phase2HumanoidFactory.cs

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Creates an original, jointed greybox humanoid and samples its poses into Humanoid clips.</summary>
public static class Phase2HumanoidFactory
{
    public sealed class Rig
    {
        public GameObject Root;
        public Animator Animator;
        public readonly Dictionary<HumanBodyBones, Transform> Bones = new Dictionary<HumanBodyBones, Transform>();
        public readonly Dictionary<HumanBodyBones, Vector3> BindPositions = new Dictionary<HumanBodyBones, Vector3>();
    }

    public static Rig Create(string avatarPath)
    {
        Rig rig = new Rig { Root = new GameObject("Humanoid_Phase2") };
        Add(rig, HumanBodyBones.Hips, rig.Root.transform, new Vector3(0f, 0.97f, 0f));
        Add(rig, HumanBodyBones.Spine, B(rig, HumanBodyBones.Hips), new Vector3(0f, 0.2f, 0f));
        Add(rig, HumanBodyBones.Chest, B(rig, HumanBodyBones.Spine), new Vector3(0f, 0.18f, 0f));
        Add(rig, HumanBodyBones.Neck, B(rig, HumanBodyBones.Chest), new Vector3(0f, 0.18f, 0f));
        Add(rig, HumanBodyBones.Head, B(rig, HumanBodyBones.Neck), new Vector3(0f, 0.15f, 0f));
        AddSide(rig, true);
        AddSide(rig, false);

        string[] names = HumanTrait.BoneName;
        HumanBone[] human = rig.Bones.Select(pair => new HumanBone
        {
            boneName = pair.Value.name,
            humanName = names.First(n => n.Replace(" ", "") == pair.Key.ToString()),
            limit = new HumanLimit { useDefaultValues = true }
        }).ToArray();
        SkeletonBone[] skeleton = rig.Root.GetComponentsInChildren<Transform>().Select(t => new SkeletonBone
        {
            name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale
        }).ToArray();

        Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
        if (avatar == null)
        {
            avatar = AvatarBuilder.BuildHumanAvatar(rig.Root, new HumanDescription
            {
                human = human, skeleton = skeleton,
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false
            });
            if (!avatar.isValid || !avatar.isHuman)
            {
                UnityEngine.Object.DestroyImmediate(rig.Root);
                UnityEngine.Object.DestroyImmediate(avatar);
                throw new InvalidOperationException("Generated Avatar is not a valid Humanoid. No scene was changed.");
            }
            avatar.name = "Phase2GreyboxHumanoidAvatar";
            AssetDatabase.CreateAsset(avatar, avatarPath);
        }

        rig.Animator = rig.Root.AddComponent<Animator>();
        rig.Animator.avatar = avatar;
        rig.Animator.applyRootMotion = false;
        rig.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        rig.Animator.enabled = false; // Sampling is driven by the factory, not by an Animator tick.
        BuildVisuals(rig);
        return rig;
    }

    private static void AddSide(Rig rig, bool left)
    {
        float side = left ? -1f : 1f;
        HumanBodyBones upperLeg = left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
        HumanBodyBones lowerLeg = left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
        HumanBodyBones foot = left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
        HumanBodyBones toes = left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes;
        HumanBodyBones shoulder = left ? HumanBodyBones.LeftShoulder : HumanBodyBones.RightShoulder;
        HumanBodyBones upperArm = left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
        HumanBodyBones lowerArm = left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
        HumanBodyBones hand = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;

        Add(rig, upperLeg, B(rig, HumanBodyBones.Hips), new Vector3(side * 0.13f, -0.02f, 0f));
        Add(rig, lowerLeg, B(rig, upperLeg), new Vector3(0f, -0.43f, 0.01f));
        Add(rig, foot, B(rig, lowerLeg), new Vector3(0f, -0.44f, -0.01f));
        Add(rig, toes, B(rig, foot), new Vector3(0f, -0.03f, 0.16f));
        Add(rig, shoulder, B(rig, HumanBodyBones.Chest), new Vector3(side * 0.14f, 0.1f, 0f));
        Add(rig, upperArm, B(rig, shoulder), new Vector3(side * 0.1f, 0f, 0f));
        Add(rig, lowerArm, B(rig, upperArm), new Vector3(side * 0.29f, 0f, 0f));
        Add(rig, hand, B(rig, lowerArm), new Vector3(side * 0.26f, 0f, 0f));
    }

    private static void Add(Rig rig, HumanBodyBones bone, Transform parent, Vector3 position)
    {
        Transform joint = new GameObject(bone.ToString()).transform;
        joint.SetParent(parent, false);
        joint.localPosition = position;
        rig.Bones.Add(bone, joint);
        rig.BindPositions.Add(bone, position);
    }

    private static Transform B(Rig rig, HumanBodyBones bone) => rig.Bones[bone];

    public static void ResetPose(Rig rig)
    {
        foreach (var pair in rig.Bones)
        {
            pair.Value.localPosition = rig.BindPositions[pair.Key];
            pair.Value.localRotation = Quaternion.identity;
        }
    }

    private static void Rotate(Rig rig, HumanBodyBones bone, float x, float y, float z)
        => B(rig, bone).localRotation = Quaternion.Euler(x, y, z);

    public static AnimationClip CreateClip(Rig rig, string clipName, float duration, bool loop, Action<Rig, string, float> poseBuilder = null)
    {
        AnimationClip clip = new AnimationClip { name = clipName, frameRate = 30f };
        int frames = Mathf.Max(2, Mathf.CeilToInt(duration * clip.frameRate));
        int channels = HumanTrait.MuscleCount + 7;
        List<Keyframe>[] samples = new List<Keyframe>[channels];
        for (int i = 0; i < channels; i++) samples[i] = new List<Keyframe>();

        using (HumanPoseHandler handler = new HumanPoseHandler(rig.Animator.avatar, rig.Root.transform))
        {
            HumanPose pose = new HumanPose();
            for (int frame = 0; frame <= frames; frame++)
            {
                float fraction = (float)frame / frames;
                if (poseBuilder == null) Pose(rig, clipName, fraction);
                else poseBuilder(rig, clipName, fraction);
                handler.GetHumanPose(ref pose);
                float time = fraction * duration;
                for (int muscle = 0; muscle < HumanTrait.MuscleCount; muscle++)
                    samples[muscle].Add(new Keyframe(time, pose.muscles[muscle]));
                float[] body =
                {
                    pose.bodyPosition.x, pose.bodyPosition.y, pose.bodyPosition.z,
                    pose.bodyRotation.x, pose.bodyRotation.y, pose.bodyRotation.z, pose.bodyRotation.w
                };
                for (int i = 0; i < body.Length; i++)
                    samples[HumanTrait.MuscleCount + i].Add(new Keyframe(time, body[i]));
            }
        }

        string[] bodyProperties = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
        for (int i = 0; i < channels; i++)
        {
            // No finger/eye/jaw bones exist on this greybox. Do not write unsupported bindings.
            if (i < HumanTrait.MuscleCount)
            {
                int bone = HumanTrait.BoneFromMuscle(i);
                if (bone < 0 || !rig.Bones.Keys.Any(b => b.ToString() == HumanTrait.BoneName[bone].Replace(" ", ""))) continue;
            }
            string property = i < HumanTrait.MuscleCount ? HumanTrait.MuscleName[i] : bodyProperties[i - HumanTrait.MuscleCount];
            AnimationCurve curve = new AnimationCurve(samples[i].ToArray());
            for (int k = 0; k < curve.length; k++) curve.SmoothTangents(k, 0f);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curve);
        }
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        settings.loopBlendOrientation = true;
        settings.loopBlendPositionY = true;
        settings.loopBlendPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        ResetPose(rig);
        return clip;
    }

    private static void Pose(Rig rig, string clip, float t)
    {
        ResetPose(rig);
        bool zombie = clip.StartsWith("Zombie", StringComparison.Ordinal) || clip == "EliteHeavy";
        bool heavy = clip == "EliteHeavy";
        if (clip.StartsWith("Zombie", StringComparison.Ordinal)) clip = clip.Substring(6);
        float phase = t * Mathf.PI * 2f;
        float swing = Mathf.Sin(phase);
        bool aiming = clip.StartsWith("Aim", StringComparison.Ordinal) ||
            clip == "Fire" || clip == "Reload";
        bool locomotion = clip.Contains("Walk") || clip.Contains("Strafe") ||
            clip == "Run" || clip == "Sprint" || (aiming && clip != "AimIdle" && clip != "Fire" && clip != "Reload");
        float stride = clip == "Sprint" ? 43f : clip == "Run" ? 34f : 23f;
        if (clip.Contains("Backward")) swing = -swing;

        Rotate(rig, HumanBodyBones.LeftUpperArm, 0f, 0f, 75f);
        Rotate(rig, HumanBodyBones.RightUpperArm, 0f, 0f, -75f);
        Rotate(rig, HumanBodyBones.LeftLowerArm, -12f, 0f, 0f);
        Rotate(rig, HumanBodyBones.RightLowerArm, -12f, 0f, 0f);
        Rotate(rig, HumanBodyBones.Chest, -1f + 1.2f * Mathf.Sin(phase), 0f, 0f);

        if (locomotion)
        {
            Rotate(rig, HumanBodyBones.LeftUpperLeg, -swing * stride, 0f, 0f);
            Rotate(rig, HumanBodyBones.RightUpperLeg, swing * stride, 0f, 0f);
            Rotate(rig, HumanBodyBones.LeftLowerLeg, Mathf.Max(0f, -swing) * 48f, 0f, 0f);
            Rotate(rig, HumanBodyBones.RightLowerLeg, Mathf.Max(0f, swing) * 48f, 0f, 0f);
            Rotate(rig, HumanBodyBones.LeftUpperArm, swing * stride * 0.7f, 0f, 75f);
            Rotate(rig, HumanBodyBones.RightUpperArm, -swing * stride * 0.7f, 0f, -75f);
            B(rig, HumanBodyBones.Hips).localPosition += Vector3.up * (0.012f * (1f - Mathf.Cos(phase * 2f)));
            if (clip.Contains("Left") || clip.Contains("Right"))
            {
                float side = clip.Contains("Left") ? -1f : 1f;
                Rotate(rig, HumanBodyBones.LeftUpperLeg, -swing * 7f, 0f, -side * swing * 20f);
                Rotate(rig, HumanBodyBones.RightUpperLeg, swing * 7f, 0f, side * swing * 20f);
            }
            if (clip == "Run" || clip == "Sprint")
                Rotate(rig, HumanBodyBones.Spine, clip == "Sprint" ? 12f : 6f, 0f, 0f);
        }

        if (aiming)
        {
            Rotate(rig, HumanBodyBones.LeftUpperArm, -66f, -10f, 67f);
            Rotate(rig, HumanBodyBones.RightUpperArm, -70f, 8f, -70f);
            Rotate(rig, HumanBodyBones.LeftLowerArm, -25f, 0f, 0f);
            Rotate(rig, HumanBodyBones.RightLowerArm, -38f, 0f, 0f);
            Rotate(rig, HumanBodyBones.Chest, 3f, -5f, 0f);
        }

        if (clip == "Jump" || clip == "Falling")
        {
            float lift = clip == "Jump" ? Mathf.Sin(t * Mathf.PI) : 0.35f;
            Rotate(rig, HumanBodyBones.LeftUpperLeg, -25f * lift, 0f, -4f);
            Rotate(rig, HumanBodyBones.RightUpperLeg, -12f * lift, 0f, 4f);
            Rotate(rig, HumanBodyBones.LeftLowerLeg, 45f * lift, 0f, 0f);
            Rotate(rig, HumanBodyBones.RightLowerLeg, 30f * lift, 0f, 0f);
            Rotate(rig, HumanBodyBones.LeftUpperArm, -15f, 0f, 45f);
            Rotate(rig, HumanBodyBones.RightUpperArm, -15f, 0f, -45f);
        }
        if (zombie)
        {
            float reach = clip == "Attack" || heavy ? (t < .6f ? Mathf.SmoothStep(0,1,t/.6f) : Mathf.SmoothStep(1,0,(t-.6f)/.4f)) : .15f;
            float raise = heavy ? -135f + 115f * reach : -30f - 65f * reach;
            Rotate(rig, HumanBodyBones.LeftUpperArm, raise, -8f, 60f);
            Rotate(rig, HumanBodyBones.RightUpperArm, raise, 8f, -60f);
            Rotate(rig, HumanBodyBones.LeftLowerArm, -18f, 0f, 0f);
            Rotate(rig, HumanBodyBones.RightLowerArm, -22f, 0f, 0f);
            Rotate(rig, HumanBodyBones.Chest, 10f + reach * 15f, 0f, 0f);
        }
        if (clip == "Fire")
            Rotate(rig, HumanBodyBones.Chest, -8f * Mathf.Sin(t * Mathf.PI), -5f, 0f);
        if (clip == "Reload")
        {
            float reach = Mathf.Sin(t * Mathf.PI);
            Rotate(rig, HumanBodyBones.LeftUpperArm, -40f, -20f * reach, 70f);
            Rotate(rig, HumanBodyBones.LeftLowerArm, -30f - 60f * reach, 0f, 0f);
        }
        if (clip == "Hit")
            Rotate(rig, HumanBodyBones.Chest, -18f * Mathf.Sin(t * Mathf.PI), 0f, 6f * Mathf.Sin(t * Mathf.PI));
        if (clip == "Death")
        {
            float collapse = Mathf.SmoothStep(0f, 1f, t);
            B(rig, HumanBodyBones.Hips).localPosition += Vector3.down * (0.72f * collapse);
            Rotate(rig, HumanBodyBones.Hips, -10f * collapse, 0f, 85f * collapse);
            Rotate(rig, HumanBodyBones.LeftLowerLeg, 40f * collapse, 0f, 0f);
            Rotate(rig, HumanBodyBones.RightLowerLeg, 55f * collapse, 0f, 0f);
        }
    }

    private static void BuildVisuals(Rig rig)
    {
        string path = "Assets/Materials/Phase2Greybox.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null ?
                Shader.Find("Standard") : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No compatible greybox shader found.");
            material = new Material(shader) { color = new Color(0.32f, 0.42f, 0.48f) };
            AssetDatabase.CreateAsset(material, path);
        }
        Part(B(rig, HumanBodyBones.Hips), new Vector3(0f, 0.14f, 0f), new Vector3(0.34f, 0.27f, 0.21f), material);
        Part(B(rig, HumanBodyBones.Chest), new Vector3(0f, 0.03f, 0f), new Vector3(0.41f, 0.25f, 0.23f), material);
        Part(B(rig, HumanBodyBones.Head), new Vector3(0f, 0.02f, 0f), new Vector3(0.24f, 0.27f, 0.24f), material);
        foreach (bool left in new[] { true, false })
        {
            float side = left ? -1f : 1f;
            Part(B(rig, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm), new Vector3(side * 0.145f, 0f, 0f), new Vector3(0.29f, 0.105f, 0.105f), material);
            Part(B(rig, left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm), new Vector3(side * 0.13f, 0f, 0f), new Vector3(0.26f, 0.09f, 0.09f), material);
            Part(B(rig, left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand), new Vector3(side * 0.035f, 0f, 0f), new Vector3(0.09f, 0.065f, 0.095f), material);
            Part(B(rig, left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg), new Vector3(0f, -0.215f, 0f), new Vector3(0.15f, 0.43f, 0.15f), material);
            Part(B(rig, left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg), new Vector3(0f, -0.22f, 0f), new Vector3(0.12f, 0.44f, 0.12f), material);
            Part(B(rig, left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot), new Vector3(0f, -0.02f, 0.065f), new Vector3(0.13f, 0.10f, 0.25f), material);
        }
    }

    private static void Part(Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mesh.name = "GreyboxMesh";
        mesh.transform.SetParent(parent, false);
        mesh.transform.localPosition = position;
        mesh.transform.localScale = scale;
        UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());
        mesh.GetComponent<Renderer>().sharedMaterial = material;
    }
}
```


## Phase2Setup

Full file path: D:/GV project/Assets/Editor/Phase2/Phase2Setup.cs

```csharp
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Upgrades ZombieMap in place; creates the Phase 1 greybox only when the scene does not exist.</summary>
public static class Phase2Setup
{
    private const string ScenePath = "Assets/Scenes/ZombieMap.unity";

    [MenuItem("Tools/Zombie Outbreak/Phase 2/Set Up ZombieMap")]
    public static void SetUpZombieMap()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before setting up Phase 2.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Build and validate reusable assets before changing the player's scene.
        AnimatorController controller = Phase2AnimationAssets.EnsureAssets();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Phase1SceneBuilder.BuildZombieMap();
                scene = SceneManager.GetActiveScene();
            }
            else scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        SceneManager.SetActiveScene(scene);
        ThirdPersonPlayerController[] players = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ThirdPersonPlayerController>(true)).ToArray();
        if (players.Length != 1)
            throw new InvalidOperationException("ZombieMap must have exactly one ThirdPersonPlayerController.");
        ThirdPersonPlayerController movement = players[0];
        GameObject player = movement.gameObject;
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health == null) health = Undo.AddComponent<PlayerHealth>(player);

        Animator animator = player.GetComponentsInChildren<Animator>(true)
            .FirstOrDefault(a => a.avatar != null && a.avatar.isValid && a.avatar.isHuman);
        if (animator == null)
        {
            Animator unrelatedAnimator = player.GetComponentInChildren<Animator>(true);
            if (unrelatedAnimator != null)
                throw new InvalidOperationException("Existing Animator has no valid Humanoid Avatar. Configure its Rig before rerunning setup.");
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(Phase2AnimationAssets.ModelPath), player.transform);
            model.name = "Humanoid_Phase2";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            animator = model.GetComponent<Animator>();
            Transform oldGreybox = player.transform.Find("Humanoid_Greybox");
            if (oldGreybox != null) oldGreybox.gameObject.SetActive(false);
        }
        animator.gameObject.SetActive(true);
        animator.enabled = true;
        AnimatorOverrideController overrides = animator.runtimeAnimatorController as AnimatorOverrideController;
        if (overrides == null || overrides.runtimeAnimatorController != controller)
            animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();

        PlayerAimController aim = player.GetComponent<PlayerAimController>();
        if (aim == null) aim = Undo.AddComponent<PlayerAimController>(player);
        PlayerAnimatorDriver driver = player.GetComponent<PlayerAnimatorDriver>();
        if (driver == null) driver = Undo.AddComponent<PlayerAnimatorDriver>(player);
        Transform target = Child(player.transform, "CameraTarget", new Vector3(0f, 1.48f, 0f));
        Transform aimTarget = Child(target, "AimTarget", new Vector3(0f, 0f, 20f));
        Transform ground = Child(player.transform, "GroundCheck", new Vector3(0f, 0.08f, 0f));

        Camera mainCamera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(camera => camera.CompareTag("MainCamera"));
        if (mainCamera == null)
            throw new InvalidOperationException("ZombieMap needs a Camera tagged MainCamera.");
        ThirdPersonCameraController cameraRig = mainCamera.GetComponent<ThirdPersonCameraController>();
        if (cameraRig == null) cameraRig = Undo.AddComponent<ThirdPersonCameraController>(mainCamera.gameObject);
        mainCamera.nearClipPlane = 0.1f;

        Transform modes = Child(mainCamera.transform, "Camera Modes", Vector3.zero);
        ThirdPersonCameraMode exploration = Mode(modes, "Exploration Camera", 4.5f, new Vector2(0.2f, 0.05f), 60f, 1f);
        ThirdPersonCameraMode aiming = Mode(modes, "Aim Camera", 2.25f, new Vector2(0.6f, 0.08f), 45f, 0.65f);
        Reference(movement, "cameraTransform", mainCamera.transform);
        Reference(movement, "groundCheck", ground);
        Reference(movement, "cameraRig", cameraRig);
        Reference(movement, "aim", aim);
        Reference(movement, "health", health);
        Reference(aim, "health", health);
        Reference(driver, "animator", animator);
        Reference(driver, "movement", movement);
        Reference(driver, "aim", aim);
        Reference(driver, "health", health);
        Reference(cameraRig, "cameraTarget", target);
        Reference(cameraRig, "aimTarget", aimTarget);
        Reference(cameraRig, "aim", aim);
        Reference(cameraRig, "explorationCamera", exploration);
        Reference(cameraRig, "aimCamera", aiming);

        // Keep the existing socket object; only reparent the original root-level placeholder.
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform socket = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "WeaponSocket");
        if (socket == null) socket = Child(hand, "WeaponSocket", new Vector3(0f, 0f, 0.08f));
        else if (socket.parent == player.transform && hand != null)
        {
            socket.SetParent(hand, false);
            socket.localPosition = new Vector3(0f, 0f, 0.08f);
            socket.localRotation = Quaternion.identity;
        }

        // Save a prefab without scene-only camera references.
        Transform previousCamera = new SerializedObject(movement).FindProperty("cameraTransform").objectReferenceValue as Transform;
        Reference(movement, "cameraTransform", null);
        Reference(movement, "cameraRig", null);
        try { PrefabUtility.SaveAsPrefabAsset(player, "Assets/Prefabs/Player/PlayerPhase2.prefab"); }
        finally
        {
            Reference(movement, "cameraTransform", previousCamera);
            Reference(movement, "cameraRig", cameraRig);
        }

        EditorSettings.serializationMode = SerializationMode.ForceText;
        UnityEditor.VersionControlSettings.mode = "Visible Meta Files";
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save ZombieMap.");
        AssetDatabase.SaveAssets();
        ValidateSetup();
        Selection.activeGameObject = player;
        Debug.Log("Phase 2 configured. Hold right mouse to aim. No firing gameplay is connected.");
    }

    private static ThirdPersonCameraMode Mode(Transform parent, string name, float distance, Vector2 offset, float fov, float sensitivity)
    {
        Transform transform = parent.Find(name);
        if (transform != null && transform.TryGetComponent(out ThirdPersonCameraMode existing)) return existing;
        if (transform == null) transform = Child(parent, name, Vector3.zero);
        ThirdPersonCameraMode mode = transform.gameObject.AddComponent<ThirdPersonCameraMode>();
        SerializedObject data = new SerializedObject(mode);
        data.FindProperty("distance").floatValue = distance;
        data.FindProperty("shoulderOffset").vector2Value = offset;
        data.FindProperty("fieldOfView").floatValue = fov;
        data.FindProperty("sensitivityMultiplier").floatValue = sensitivity;
        data.ApplyModifiedPropertiesWithoutUndo();
        return mode;
    }

    private static Transform Child(Transform parent, string name, Vector3 position)
    {
        if (parent == null) throw new InvalidOperationException("Missing required parent for " + name);
        Transform existing = parent.Find(name);
        if (existing != null) return existing;
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        child.localPosition = position;
        return child;
    }

    private static void Reference(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty field = data.FindProperty(name);
        if (field == null) throw new InvalidOperationException(target.name + " has no serialized property " + name);
        field.objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Zombie Outbreak/Phase 2/Validate Active Scene")]
    public static void ValidateSetup()
    {
        Scene scene = SceneManager.GetActiveScene();
        ThirdPersonPlayerController movement = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ThirdPersonPlayerController>()).Single();
        Animator animator = movement.GetComponentInChildren<Animator>();
        Require(animator != null && animator.avatar != null && animator.avatar.isHuman && animator.avatar.isValid,
            "Player needs a valid Humanoid Avatar.");
        Require(!animator.applyRootMotion, "Apply Root Motion must be off.");
        Require(animator.GetBoneTransform(HumanBodyBones.RightHand) != null, "Right hand mapping is missing.");
        RuntimeAnimatorController runtimeController = animator.runtimeAnimatorController;
        AnimatorOverrideController overrides = runtimeController as AnimatorOverrideController;
        AnimatorController controller = (overrides != null ? overrides.runtimeAnimatorController : runtimeController) as AnimatorController;
        Require(controller != null && controller.layers.Length == 2, "Expected Base Layer and Upper Body Actions.");
        string[] parameters =
        {
            "Speed", "Horizontal", "Vertical", "IsAiming", "IsGrounded", "IsSprinting",
            "VerticalVelocity", "IsDead", "Fire", "Reload", "Hit", "Death"
        };
        foreach (string parameter in parameters)
            Require(controller.parameters.Any(p => p.name == parameter), "Missing parameter: " + parameter);
        foreach (AnimationClip clip in runtimeController.animationClips.Distinct())
            Require(clip != null && !clip.empty && clip.humanMotion, "Empty or non-Humanoid clip: " + (clip != null ? clip.name : "null"));
        Require(runtimeController.animationClips.Distinct().Count() >= 18, "Expected all 18 locomotion and action clips.");

        ThirdPersonCameraController cameraRig = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ThirdPersonCameraController>()).Single();
        foreach (string reference in new[] { "cameraTarget", "aimTarget", "aim", "explorationCamera", "aimCamera" })
            Require(new SerializedObject(cameraRig).FindProperty(reference).objectReferenceValue != null,
                "Camera reference is missing: " + reference);
        foreach (string reference in new[] { "cameraTransform", "groundCheck", "cameraRig", "aim", "health" })
            Require(new SerializedObject(movement).FindProperty(reference).objectReferenceValue != null,
                "Movement reference is missing: " + reference);
        Camera[] activeCameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>())
            .Where(c => c.enabled).ToArray();
        Require(activeCameras.Length == 1, "Use one output Camera; camera modes are virtual poses.");
        Debug.Log("Phase 2 structure validation passed: Avatar, clips, parameters, camera and movement references.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Phase 2 validation: " + message);
    }
}
```


## Phase3Checks

Full file path: D:/GV project/Assets/Editor/Phase3/Phase3Checks.cs

```csharp
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Focused regression checks, run explicitly from the menu or batch -executeMethod.</summary>
public static class Phase3Checks
{
    [MenuItem("Tools/Zombie Outbreak/Phase 3/Run Ammo and Physics Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        AmmoChecks();
        PhysicsChecks();
        Debug.Log("PHASE3_CHECKS_PASS: ammo conservation, partial reload, cancellation, cooldown, independent owners, cover, overlaps, nearest hit, self exclusion, saturated query and spread cone.");
    }

    private static void AmmoChecks()
    {
        WeaponAmmoState rifle = new WeaponAmmoState(3, 2, 10, 2);
        Check(rifle.TryFire(0) && !rifle.TryFire(0.05), "Fire cadence");
        Check(rifle.TryFire(0.1) && rifle.TryFire(0.2) && !rifle.TryFire(0.4), "Empty magazine");
        Check(rifle.Magazine == 0 && rifle.Reserve == 2, "Rounds spent");
        Check(rifle.TryStartReload(0.5) && !rifle.TryStartReload(0.6), "One reload at a time");
        Check(!rifle.TryFire(1) && !rifle.TickReload(2.49), "Cannot fire or reload early");
        Check(rifle.TickReload(2.5) && rifle.Magazine == 2 && rifle.Reserve == 0, "Partial reserve transfer");
        Check(!rifle.TryStartReload(3), "No reserve reload");
        WeaponAmmoState pistol = new WeaponAmmoState(12, 60, 4, 1.5);
        Check(pistol.TryFire(0) && pistol.TryStartReload(0.1), "Tactical reload");
        pistol.CancelReload();
        Check(!pistol.TickReload(10) && pistol.Magazine == 11 && pistol.Reserve == 60, "Switch/death cancellation conserves ammo");
        Check(!pistol.TryFire(0.15), "Switching must not reset cooldown");
        Check(pistol.TryStartReload(1) && pistol.TickReload(2.5), "Reload after cancellation");
        Check(pistol.Magazine == 12 && pistol.Reserve == 59, "Tactical reload retains remaining magazine rounds");
        Check(!pistol.TryStartReload(3), "Full magazine does not reload");
        WeaponAmmoState secondOwner = new WeaponAmmoState(12, 60, 4, 1.5);
        Check(secondOwner.Reserve == 60 && secondOwner.Magazine == 12, "Independent weapon ownership");
    }

    private static void PhysicsChecks()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scratch);
            Vector3 origin = new Vector3(1000, 1000, 1000);
            GameObject owner = new GameObject("Check Owner");
            owner.transform.position = origin;
            GameObject self = Cube("Own collider", origin + Vector3.forward);
            self.transform.SetParent(owner.transform, true);
            GameObject trigger = Cube("Trigger", origin + Vector3.forward * 2);
            trigger.GetComponent<Collider>().isTrigger = true;
            GameObject closest = Cube("Closest wall", origin + Vector3.forward * 3);
            for (int i = 0; i < 45; i++) Cube("Far wall", origin + Vector3.forward * (5 + i * 2));
            Physics.SyncTransforms();
            WeaponHitscan query = new WeaponHitscan(owner.transform, Physics.DefaultRaycastLayers);
            Check(query.Raycast(origin, Vector3.forward, 100, out RaycastHit hit) && hit.collider.gameObject == closest,
                "Nearest external non-trigger hit with a saturated buffer");
            Check(!query.Raycast(origin, Vector3.forward, 2.4f, out _), "Range and owner/trigger exclusion");
            Check(query.OverlapsWorld(closest.transform.position, 0.02f), "Muzzle inside cover");
            Check(!query.OverlapsWorld(self.transform.position, 0.02f), "Own collider must not block muzzle overlap");
            Camera camera = new GameObject("Check Camera").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(origin + new Vector3(2, 0, -3), Quaternion.identity);
            Vector3 point = query.AimPoint(camera, 100, origin, out bool blocked);
            Check(!blocked && point.z > origin.z + 90, "Offset camera has a clear view");
            Check(query.Raycast(origin, (point - origin).normalized, 100, out hit) && hit.collider.gameObject == closest,
                "Muzzle ray still hits cover when camera can see past it");
            camera.transform.position = closest.transform.position - Vector3.forward * camera.nearClipPlane;
            query.AimPoint(camera, 100, origin, out blocked);
            Check(blocked, "Camera ray origin inside cover is blocked");
            for (int i = 0; i < 500; i++)
            {
                Vector3 spread = WeaponHitscan.SpreadDirection(Vector3.forward, 4.5f);
                Check(Mathf.Abs(spread.magnitude - 1f) < 0.001f && Vector3.Angle(spread, Vector3.forward) <= 4.51f,
                    "Shotgun pellets remain inside a normalized cone");
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scratch, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    private static GameObject Cube(string name, Vector3 position)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position;
        return go;
    }

    public static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Phase 3 check failed: " + label);
    }

    public static void BuildAndCheck()
    {
        Phase3Setup.SetUpZombieMap();
        Run();
    }
}
```


## Phase3PlayChecks

Full file path: D:/GV project/Assets/Editor/Phase3/Phase3PlayChecks.cs

```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch-only Play Mode integration check. Never saves its runtime changes.</summary>
[InitializeOnLoad]
public static class Phase3PlayChecks
{
    private const string Active = "ZombieOutbreak.Phase3.PlayChecks";
    private static int stage;
    private static double deadline;
    private static PlayerWeaponController weapons;
    private static ShootingTarget target;
    private static int shots;
    private static float totalDamage;
    private static double reloadEnds;
    private static bool failed;

    static Phase3PlayChecks()
    {
        if (SessionState.GetBool(Active, false))
        {
            deadline = EditorApplication.timeSinceStartup + 60d;
            EditorApplication.update += Tick;
        }
    }

    // Direct Editor invocation, without -quit. This method owns Editor exit.
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the documented batch command; this check exits its Editor process.");
        EditorSceneManager.OpenScene("Assets/Scenes/ZombieMap.unity");
        // Regression fixture: retain the original four-weapon assertions after the two-slot loadout phase.
        // These scene edits exist only in this batch process and are never saved.
        var applier = UnityEngine.Object.FindAnyObjectByType<PlayerLoadoutApplier>();
        if (applier != null) UnityEngine.Object.DestroyImmediate(applier);
        var waves = UnityEngine.Object.FindAnyObjectByType<WaveManager>();
        if (waves != null) waves.enabled = false;
        var ui = UnityEngine.Object.FindAnyObjectByType<GameUIController>();
        if (ui != null) OutbreakEditor.Values(ui,s=>s.FindProperty("pauseOnFocusLoss").boolValue=false);
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play Mode checks timed out.");
            if (!EditorApplication.isPlaying || Time.time < 0.2f)
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play Mode startup timed out.");
                return;
            }
            if (stage == 0)
            {
                var ui = UnityEngine.Object.FindAnyObjectByType<GameUIController>();
                if (ui != null) ui.Resume();
                weapons = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>();
                Phase3Checks.Check(weapons != null && weapons.enabled, "Shooter initialized in Play Mode");
                weapons.GetComponent<ThirdPersonPlayerController>().enabled = false;
                weapons.transform.position = new Vector3(0, 0, -10);
                Camera camera = Camera.main;
                camera.GetComponent<ThirdPersonCameraController>().enabled = false;
                camera.transform.position = new Vector3(0.65f, 1.65f, -13);
                camera.transform.LookAt(new Vector3(0, 1.4f, -2));
                camera.fieldOfView = 45;
                GameObject testTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
                testTarget.name = "Play Check Target";
                testTarget.transform.position = new Vector3(0, 1.4f, -2);
                testTarget.transform.localScale = new Vector3(4, 4, 0.25f);
                target = testTarget.AddComponent<ShootingTarget>();
                weapons.ShotFired += info =>
                {
                    shots++;
                    Phase3Checks.Check(info.Source == weapons.gameObject && info.Category == weapons.CurrentWeaponCategory && info.NoiseRadius > 0,
                        "AI shot observation payload");
                };
                weapons.HitDetected += (hit, damage) => totalDamage += damage;
                Animator animator = weapons.GetComponentInChildren<Animator>();
                animator.Play("Aim Locomotion", 0, 0);
                animator.Update(0.1f);
                Physics.SyncTransforms();
                for (int i = 0; i < 4; i++)
                {
                    if (i > 0) Phase3Checks.Check(weapons.SelectWeapon(i), "Weapon switching");
                    weapons.GetComponent<PlayerAimController>().SetAiming(true);
                    int before = weapons.MagazineAmmo;
                    Phase3Checks.Check(weapons.TryFire(), "Weapon can fire");
                    Phase3Checks.Check(!weapons.LastShotBlocked && weapons.MagazineAmmo == before - 1 && weapons.IsFiring, "Shot accounting");
                    Phase3Checks.Check(!weapons.TryFire(), "Immediate repeated call respects cooldown");
                    if (i == 0 && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) Capture(camera);
                    Phase3Checks.Check(weapons.TryReload() && !weapons.TryFire(), "Reload blocks fire");
                }
                Phase3Checks.Check(shots == 4, "One noise event per round, including shotgun");
                Phase3Checks.Check(Mathf.Abs(totalDamage - 142f) < 0.01f && Mathf.Abs(target.CurrentHealth - 58f) < 0.01f,
                    "All four weapons deal damage; shotgun has eight pellets");
                Phase3Checks.Check(weapons.SelectWeapon(0) && weapons.MagazineAmmo == 29 && weapons.ReserveAmmo == 120,
                    "Switch cancels reload and preserves rifle ammo");
                weapons.TryReload();
                weapons.enabled = false;
                weapons.enabled = true;
                Phase3Checks.Check(!weapons.IsReloading && weapons.MagazineAmmo == 29, "Disable cancels reload without losing ammo");
                Phase3Checks.Check(weapons.TryReload(), "Reload can restart");
                reloadEnds = Time.timeAsDouble + weapons.CurrentWeapon.ReloadTime + 0.1d;
                stage = 1;
            }
            else if (Time.timeAsDouble >= reloadEnds)
            {
                Phase3Checks.Check(!weapons.IsReloading && weapons.MagazineAmmo == 30 && weapons.ReserveAmmo == 119,
                    "Runtime reload timer transfers exactly one round");
                weapons.GetComponent<PlayerHealth>().Die();
                Phase3Checks.Check(!weapons.TryFire() && !weapons.TryReload() && !weapons.SelectWeapon(1) && !weapons.IsFiring,
                    "Death stops weapon activity");
                Debug.Log("PHASE3_PLAY_CHECKS_PASS: four live weapons, pellet damage, ammo persistence, reload completion/cancellation, AI event payload, death gating.");
                Finish(0);
            }
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            failed = true;
            Finish(1);
        }
    }

    private static void Finish(int code)
    {
        SessionState.EraseBool(Active);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(failed ? 1 : code);
    }

    private static void Capture(Camera camera)
    {
        RenderTexture previous = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture render = new RenderTexture(1280, 720, 24);
        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = render;
            camera.Render();
            RenderTexture.active = render;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            Color32[] pixels = image.GetPixels32();
            Directory.CreateDirectory("Logs");
            using (BinaryWriter writer = new BinaryWriter(File.Create("Logs/Phase3Preview.ppm")))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("P6\n1280 720\n255\n"));
                for (int y = 719; y >= 0; y--)
                    for (int x = 0; x < 1280; x++)
                    {
                        Color32 pixel = pixels[y * 1280 + x];
                        writer.Write(pixel.r); writer.Write(pixel.g); writer.Write(pixel.b);
                    }
            }
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(image);
            render.Release();
            UnityEngine.Object.DestroyImmediate(render);
        }
    }
}
```


## Phase3Setup

Full file path: D:/GV project/Assets/Editor/Phase3/Phase3Setup.cs

```csharp
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Phase3Setup
{
    private const string ScenePath = "Assets/Scenes/ZombieMap.unity";

    [MenuItem("Tools/Zombie Outbreak/Phase 3/Set Up ZombieMap")]
    public static void SetUpZombieMap()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Phase2Setup.SetUpZombieMap();
            else EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            scene = SceneManager.GetSceneByPath(ScenePath);
        }
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("ZombieMap setup was cancelled or failed.");
        SceneManager.SetActiveScene(scene);
        ThirdPersonPlayerController movement = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ThirdPersonPlayerController>(true)).Single();
        GameObject player = movement.gameObject;
        if (player.GetComponent<PlayerAimController>() == null || player.GetComponent<PlayerAnimatorDriver>() == null)
        {
            Phase2Setup.SetUpZombieMap();
            movement = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ThirdPersonPlayerController>()).Single();
            player = movement.gameObject;
        }
        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            throw new InvalidOperationException("Phase 2 requires a valid Humanoid Animator before Phase 3 setup.");
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform socket = hand != null ? hand.Find("WeaponSocket") : null;
        if (socket == null) throw new InvalidOperationException("Run Phase 2 setup to create RightHand/WeaponSocket.");
        Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>())
            .Single(c => c.CompareTag("MainCamera"));
        WeaponData[] presets = Phase3WeaponAssets.EnsureAssets();
        PlayerWeaponController weapons = player.GetComponent<PlayerWeaponController>();
        if (weapons == null) weapons = Undo.AddComponent<PlayerWeaponController>(player);
        SerializedObject so = new SerializedObject(weapons);
        so.FindProperty("viewCamera").objectReferenceValue = camera;
        so.FindProperty("cameraRig").objectReferenceValue = camera.GetComponent<ThirdPersonCameraController>();
        so.FindProperty("aim").objectReferenceValue = player.GetComponent<PlayerAimController>();
        so.FindProperty("health").objectReferenceValue = player.GetComponent<PlayerHealth>();
        so.FindProperty("animationDriver").objectReferenceValue = player.GetComponent<PlayerAnimatorDriver>();
        so.FindProperty("weaponSocket").objectReferenceValue = socket;
        so.FindProperty("shotOrigin").objectReferenceValue = player.transform.Find("CameraTarget");
        so.FindProperty("aimTarget").objectReferenceValue = player.transform.Find("CameraTarget/AimTarget");
        SerializedProperty list = so.FindProperty("loadout");
        if (list.arraySize == 0)
        {
            list.arraySize = presets.Length;
            for (int i = 0; i < presets.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = presets[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AddTargets(scene);
        SavePlayerPrefab(player, movement, weapons);
        EditorSettings.serializationMode = SerializationMode.ForceText;
        UnityEditor.VersionControlSettings.mode = "Visible Meta Files";
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save ZombieMap.");
        AssetDatabase.SaveAssets();
        ValidateActiveScene();
        Selection.activeGameObject = player;
        Debug.Log("Phase 3 configured: RMB aim, LMB fire, R reload, 1-4 or wheel switch. Target health prints in Console.");
    }

    private static void SavePlayerPrefab(GameObject player, ThirdPersonPlayerController movement, PlayerWeaponController weapons)
    {
        // Scene camera references must not be serialized into a reusable prefab.
        SerializedObject move = new SerializedObject(movement);
        SerializedObject shoot = new SerializedObject(weapons);
        string[] moveFields = { "cameraTransform", "cameraRig" };
        string[] shootFields = { "viewCamera", "cameraRig" };
        UnityEngine.Object[] refs = moveFields.Select(f => move.FindProperty(f).objectReferenceValue)
            .Concat(shootFields.Select(f => shoot.FindProperty(f).objectReferenceValue)).ToArray();
        foreach (string field in moveFields) move.FindProperty(field).objectReferenceValue = null;
        foreach (string field in shootFields) shoot.FindProperty(field).objectReferenceValue = null;
        move.ApplyModifiedPropertiesWithoutUndo();
        shoot.ApplyModifiedPropertiesWithoutUndo();
        try
        {
            if (PrefabUtility.SaveAsPrefabAsset(player, "Assets/Prefabs/Player/PlayerPhase3.prefab") == null)
                throw new InvalidOperationException("Could not save PlayerPhase3.prefab.");
        }
        finally
        {
            for (int i = 0; i < moveFields.Length; i++) move.FindProperty(moveFields[i]).objectReferenceValue = refs[i];
            for (int i = 0; i < shootFields.Length; i++) shoot.FindProperty(shootFields[i]).objectReferenceValue = refs[i + 2];
            move.ApplyModifiedPropertiesWithoutUndo();
            shoot.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void AddTargets(Scene scene)
    {
        if (scene.GetRootGameObjects().Any(go => go.name == "Phase3 Practice Range")) return;
        GameObject group = new GameObject("Phase3 Practice Range");
        SceneManager.MoveGameObjectToScene(group, scene);
        for (int i = 0; i < 4; i++)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Target " + (i + 1) + " (200 HP)";
            target.transform.SetParent(group.transform, false);
            target.transform.position = new Vector3(-6f + i * 4f, 1.35f, 12f);
            target.transform.localScale = new Vector3(1.4f, 2.2f, 0.25f);
            target.AddComponent<ShootingTarget>();
        }
    }

    [MenuItem("Tools/Zombie Outbreak/Phase 3/Validate Active Scene")]
    public static void ValidateActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        PlayerWeaponController weapons = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlayerWeaponController>()).Single();
        SerializedObject so = new SerializedObject(weapons);
        foreach (string field in new[] { "viewCamera", "cameraRig", "aim", "health", "animationDriver", "weaponSocket", "shotOrigin", "aimTarget" })
            Require(so.FindProperty(field).objectReferenceValue != null, "Missing player reference " + field);
        Transform socket = so.FindProperty("weaponSocket").objectReferenceValue as Transform;
        Animator animator = weapons.GetComponentInChildren<Animator>();
        Require(socket.parent == animator.GetBoneTransform(HumanBodyBones.RightHand), "WeaponSocket must be under RightHand.");
        SerializedProperty list = so.FindProperty("loadout");
        Require(list.arraySize == 4, "Expected four weapon slots.");
        WeaponCategory[] categories = new WeaponCategory[4];
        for (int i = 0; i < list.arraySize; i++)
        {
            WeaponData data = list.GetArrayElementAtIndex(i).objectReferenceValue as WeaponData;
            Require(data != null && data.WeaponPrefab != null && data.WeaponPrefab.Muzzle != null, "Invalid weapon slot " + i);
            Require(data.WeaponPrefab.GetComponentsInChildren<Collider>(true).Length == 0, "Weapon view prefabs must have no colliders.");
            categories[i] = data.Category;
        }
        Require(categories.Distinct().Count() == 4 && !categories.Contains(WeaponCategory.None), "Need Rifle, SMG, Shotgun and Pistol categories.");
        Require(so.FindProperty("hitLayers").intValue != 0, "Hit Layers must include environment and targets.");
        Debug.Log("Phase 3 structure validation passed: four weapons, muzzle references, socket and player wiring.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Phase 3 validation: " + message);
    }
}
```


## Phase3WeaponAssets

Full file path: D:/GV project/Assets/Editor/Phase3/Phase3WeaponAssets.cs

```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Creates editable weapon assets once. Reruns preserve existing tuning and prefabs.</summary>
public static class Phase3WeaponAssets
{
    public const string DataFolder = "Assets/Data/Weapons";
    public const string PrefabFolder = "Assets/Prefabs/Weapons";

    private sealed class Preset
    {
        public string Name;
        public WeaponCategory Category;
        public float Damage, Rate, Reload, Range, Spread, Length, Recoil, Noise;
        public int Magazine, Reserve, Pellets = 1;
        public bool Auto;
    }

    public static WeaponData[] EnsureAssets()
    {
        Phase2AnimationAssets.Folder(DataFolder);
        Phase2AnimationAssets.Folder(PrefabFolder);
        Phase2AnimationAssets.Folder("Assets/Audio/Weapons");
        Phase2AnimationAssets.Folder("Assets/Materials/Weapons");
        Preset[] presets =
        {
            new Preset { Name="Rifle", Category=WeaponCategory.Rifle, Damage=25, Rate=8, Magazine=30,
                Reserve=120, Reload=2.2f, Range=100, Auto=true, Spread=0.6f, Length=0.7f, Recoil=0.65f, Noise=35 },
            new Preset { Name="SMG", Category=WeaponCategory.SMG, Damage=12, Rate=12, Magazine=35,
                Reserve=140, Reload=1.8f, Range=65, Auto=true, Spread=1.1f, Length=0.43f, Recoil=0.4f, Noise=28 },
            new Preset { Name="Shotgun", Category=WeaponCategory.Shotgun, Damage=10, Rate=1.1f, Magazine=8,
                Reserve=32, Reload=2.8f, Range=35, Spread=4.5f, Length=0.8f, Recoil=2.2f, Noise=45, Pellets=8 },
            new Preset { Name="Pistol", Category=WeaponCategory.Pistol, Damage=25, Rate=4, Magazine=12,
                Reserve=60, Reload=1.45f, Range=70, Spread=0.45f, Length=0.23f, Recoil=1.1f, Noise=22 }
        };
        Material body = MaterialAt("WeaponGrey", new Color(0.22f, 0.24f, 0.26f), false);
        Material accent = MaterialAt("WeaponLightGrey", new Color(0.48f, 0.5f, 0.52f), false);
        Material flash = MaterialAt("MuzzleFlash", new Color(1f, 0.7f, 0.15f), true);
        WeaponData[] result = new WeaponData[presets.Length];
        for (int i = 0; i < presets.Length; i++)
        {
            Preset preset = presets[i];
            string path = DataFolder + "/" + preset.Name + ".asset";
            result[i] = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (result[i] != null) continue;
            WeaponView prefab = Prefab(preset, body, accent, flash);
            AudioClip audio = Audio(preset.Name, 140f + i * 80f, 700 + i, false);
            AudioClip reload = Audio("ReloadClick", 700f, 777, true);
            WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
            SerializedObject so = new SerializedObject(data);
            so.FindProperty("weaponName").stringValue = preset.Name;
            so.FindProperty("category").intValue = (int)preset.Category;
            so.FindProperty("damage").floatValue = preset.Damage;
            so.FindProperty("fireRate").floatValue = preset.Rate;
            so.FindProperty("magazineSize").intValue = preset.Magazine;
            so.FindProperty("reserveAmmo").intValue = preset.Reserve;
            so.FindProperty("reloadTime").floatValue = preset.Reload;
            so.FindProperty("range").floatValue = preset.Range;
            so.FindProperty("automatic").boolValue = preset.Auto;
            so.FindProperty("spread").floatValue = preset.Spread;
            so.FindProperty("pellets").intValue = preset.Pellets;
            so.FindProperty("weaponPrefab").objectReferenceValue = prefab;
            so.FindProperty("fireAudio").objectReferenceValue = audio;
            so.FindProperty("reloadAudio").objectReferenceValue = reload;
            so.FindProperty("recoilPitch").floatValue = preset.Recoil;
            so.FindProperty("recoilYaw").floatValue = preset.Recoil * 0.3f;
            so.FindProperty("noiseRadius").floatValue = preset.Noise;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(data, path);
            result[i] = data;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    private static WeaponView Prefab(Preset preset, Material body, Material accent, Material flashMaterial)
    {
        string path = PrefabFolder + "/" + preset.Name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing.GetComponent<WeaponView>();
        GameObject root = new GameObject(preset.Name);
        try
        {
            root.layer = 2;
            WeaponView view = root.AddComponent<WeaponView>();
            Part(root.transform, "Receiver", new Vector3(0, 0.04f, preset.Length * 0.25f),
                new Vector3(0.075f, 0.09f, preset.Length * 0.65f), body);
            Part(root.transform, "Barrel", new Vector3(0, 0.04f, preset.Length * 0.73f),
                new Vector3(0.035f, 0.035f, preset.Length * 0.55f), accent);
            Part(root.transform, "Grip", new Vector3(0, -0.07f, 0), new Vector3(0.065f, 0.16f, 0.075f), body);
            if (preset.Category != WeaponCategory.Pistol)
            {
                Part(root.transform, "Stock", new Vector3(0, 0.015f, -0.17f), new Vector3(0.065f, 0.11f, 0.24f), body);
                Part(root.transform, "Magazine", new Vector3(0, -0.075f, preset.Length * 0.3f), new Vector3(0.05f, 0.16f, 0.09f), accent);
            }
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0, 0.04f, preset.Length + 0.02f);
            GameObject flash = Part(muzzle, "Greybox Flash", new Vector3(0, 0, 0.07f), new Vector3(0.04f, 0.04f, 0.14f), flashMaterial);
            flash.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            flash.SetActive(false);
            AudioSource shot = Source(root);
            AudioSource reload = Source(root);
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("muzzle").objectReferenceValue = muzzle;
            so.FindProperty("greyboxFlash").objectReferenceValue = flash;
            so.FindProperty("shotAudio").objectReferenceValue = shot;
            so.FindProperty("reloadAudio").objectReferenceValue = reload;
            so.ApplyModifiedPropertiesWithoutUndo();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (saved == null) throw new InvalidOperationException("Could not save " + path);
            return saved.GetComponent<WeaponView>();
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static AudioSource Source(GameObject root)
    {
        AudioSource source = root.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.minDistance = 2f;
        source.maxDistance = 45f;
        source.dopplerLevel = 0f;
        return source;
    }

    private static GameObject Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.layer = 2;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }

    private static Material MaterialAt(string name, Color color, bool emission)
    {
        string path = "Assets/Materials/Weapons/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        Shader shader = GraphicsSettings.currentRenderPipeline == null ? Shader.Find("Standard") : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Expected Standard or URP/Lit shader.");
        Material material = new Material(shader) { color = color };
        if (emission)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 3f);
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Original synthesized greybox clicks; no downloaded or licensed audio is required.
    private static AudioClip Audio(string name, float frequency, int seed, bool reload)
    {
        string path = "Assets/Audio/Weapons/" + name + ".wav";
        if (!File.Exists(path))
        {
            const int rate = 22050;
            int samples = (int)(rate * (reload ? 0.16f : 0.11f));
            System.Random random = new System.Random(seed);
            using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                for (int i = 0; i < samples; i++)
                {
                    double time = (double)i / rate;
                    double envelope = Math.Exp(-time * (reload ? 45d : 35d));
                    double value = (0.65d * (random.NextDouble() * 2d - 1d) + 0.35d * Math.Sin(2d * Math.PI * frequency * time)) * envelope;
                    writer.Write((short)(value * short.MaxValue * 0.5d));
                }
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
```


## Phase4Setup

Full file path: D:/GV project/Assets/Editor/Phase4/Phase4Setup.cs

```csharp
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Adds Phase 4 UI to the existing scene. Reruns only rebind sources, preserving layout edits.</summary>
public static class Phase4Setup
{
    public const string ScenePath = "Assets/Scenes/ZombieMap.unity";
    public const string PrefabPath = "Assets/Prefabs/UI/GameUI.prefab";
    private static bool waitingForText;
    private static bool ownsBatchProcess;
    private static double textDeadline;

    // Run with the Editor binary directly, without -quit, so TMP resource import can finish.
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("RunBatch is only for a temporary batch Editor.");
        ownsBatchProcess = true;
        try { SetUp(); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/Zombie Outbreak/Phase 4/Set Up HUD and Panels")]
    public static void SetUp()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before UI setup.");
        if (!Phase4UIFactory.TextResourcesReady)
        {
            if (waitingForText) return;
            waitingForText = true;
            textDeadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += WaitForText;
            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        PlayerWeaponController player = All<PlayerWeaponController>(scene).Single();
        ThirdPersonCameraController cameraRig = All<ThirdPersonCameraController>(scene).Single();
        GameUIController[] found = All<GameUIController>(scene);
        if (found.Length > 1) throw new InvalidOperationException("ZombieMap has multiple GameUIControllers.");
        GameObject root;
        if (found.Length == 1) root = found[0].gameObject;
        else
        {
            // Assets are created through the Editor; never patch Canvas/prefab YAML manually.
            root = Phase4UIFactory.Create();
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create Phase 4 UI");
        }
        UnityEngine.EventSystems.EventSystem[] systems = All<UnityEngine.EventSystems.EventSystem>(scene);
        if (systems.Length > 1) throw new InvalidOperationException("Keep exactly one EventSystem in ZombieMap.");
        UnityEngine.EventSystems.EventSystem system;
        if (systems.Length == 0)
        {
            GameObject go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(go, scene);
            system = go.GetComponent<UnityEngine.EventSystems.EventSystem>();
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }
        else system = systems[0];
        if (system.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null)
            throw new InvalidOperationException("This project uses legacy input. Configure one StandaloneInputModule on the EventSystem.");

        GameUIController screens = root.GetComponent<GameUIController>();
        PlayerHudPresenter hud = root.GetComponentInChildren<PlayerHudPresenter>(true);
        GamePanelsPresenter panels = root.GetComponentInChildren<GamePanelsPresenter>(true);
        if (hud == null || panels == null) throw new InvalidOperationException("Existing GameUI is incomplete. Repair its presenter references before rerunning.");
        Phase4UIFactory.Ref(screens, "health", player.GetComponent<PlayerHealth>());
        Phase4UIFactory.Ref(screens, "aim", player.GetComponent<PlayerAimController>());
        Phase4UIFactory.Ref(screens, "cameraRig", cameraRig);
        Phase4UIFactory.Ref(hud, "health", player.GetComponent<PlayerHealth>());
        Phase4UIFactory.Ref(hud, "weapons", player);
        Phase4UIFactory.Ref(hud, "aim", player.GetComponent<PlayerAimController>());
        Phase4UIFactory.Ref(hud, "screens", screens);
        Phase4UIFactory.Ref(panels, "screens", screens);
        Phase4UIFactory.Ref(panels, "eventSystem", system);
        SavePrefab(root, screens, hud, panels);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save ZombieMap.");
        AssetDatabase.SaveAssets();
        Validate();
        Selection.activeGameObject = root;
        Debug.Log("Phase 4 HUD and panels saved. Escape pauses; death shows Game Over; Victory is an explicit preview hook.");
        if (ownsBatchProcess) EditorApplication.Exit(0);
    }

    private static void WaitForText()
    {
        bool timedOut = EditorApplication.timeSinceStartup > textDeadline;
        if (!timedOut && (!Phase4UIFactory.TextResourcesReady || EditorApplication.isUpdating)) return;
        EditorApplication.update -= WaitForText;
        waitingForText = false;
        try
        {
            if (timedOut) throw new InvalidOperationException("TMP Essentials import timed out.");
            SetUp();
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (ownsBatchProcess) EditorApplication.Exit(1);
        }
    }

    private static void SavePrefab(GameObject root, GameUIController screens, PlayerHudPresenter hud, GamePanelsPresenter panels)
    {
        Phase2AnimationAssets.Folder("Assets/Prefabs/UI");
        UnityEngine.Object[] owners = { screens, screens, screens, hud, hud, hud, panels };
        string[] fields = { "health", "aim", "cameraRig", "health", "weapons", "aim", "eventSystem" };
        UnityEngine.Object[] values = new UnityEngine.Object[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            values[i] = new SerializedObject(owners[i]).FindProperty(fields[i]).objectReferenceValue;
            Phase4UIFactory.Ref(owners[i], fields[i], null);
        }
        try
        {
            if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null) throw new InvalidOperationException("Could not save GameUI.prefab.");
        }
        finally
        {
            for (int i = 0; i < fields.Length; i++) Phase4UIFactory.Ref(owners[i], fields[i], values[i]);
        }
    }

    [MenuItem("Tools/Zombie Outbreak/Phase 4/Validate HUD and Panels")]
    public static void Validate()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameUIController screens = All<GameUIController>(scene).Single();
        PlayerHudPresenter hud = screens.GetComponentInChildren<PlayerHudPresenter>(true);
        GamePanelsPresenter panels = screens.GetComponentInChildren<GamePanelsPresenter>(true);
        Require(hud != null && panels != null, "Both presenters must exist.");
        References(screens, "health", "aim", "cameraRig");
        References(hud, "health", "weapons", "aim", "screens", "crosshair", "healthFill", "healthText", "weaponText", "magazineText", "reserveText", "reloadingIndicator", "hudGroup");
        References(panels, "screens", "eventSystem", "pausePanel", "gameOverPanel", "victoryPanel", "resumeButton", "pauseRestartButton", "gameOverRestartButton", "victoryRestartButton");
        Require(All<UnityEngine.EventSystems.EventSystem>(scene).Length == 1, "Exactly one EventSystem is required.");
        foreach (Canvas canvas in screens.GetComponentsInChildren<Canvas>(true))
        {
            UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay && scaler != null, "Overlay Canvas and CanvasScaler required.");
            Require(scaler.uiScaleMode == UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                scaler.referenceResolution == new Vector2(1920, 1080), "Use the 1920x1080 scalable layout.");
            Require(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null, "Missing GraphicRaycaster.");
        }
        foreach (TMPro.TMP_Text label in screens.GetComponentsInChildren<TMPro.TMP_Text>(true))
            Require(label.font != null && !label.raycastTarget, "Labels need a font and must not intercept pointer clicks.");
        Require(!hud.GetComponent<CanvasGroup>().blocksRaycasts, "HUD must not block gameplay input.");
        Debug.Log("PHASE4_STRUCTURE_PASS: HUD bindings, panels, text fonts, EventSystem and Canvas scaling.");
    }

    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray();

    private static void References(UnityEngine.Object owner, params string[] names)
    {
        SerializedObject so = new SerializedObject(owner);
        foreach (string name in names) Require(so.FindProperty(name)?.objectReferenceValue != null, owner.name + ": missing " + name);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Phase 4 validation: " + message);
    }
}
```


## Phase4UIFactory

Full file path: D:/GV project/Assets/Editor/Phase4/Phase4UIFactory.cs

```csharp
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Creates the Phase 4 Canvas layout with native Unity UI components.</summary>
public static class Phase4UIFactory
{
    private static readonly Color Panel = new Color(0.035f, 0.055f, 0.075f, 0.93f);
    private static readonly Color Ink = new Color(0.95f, 0.97f, 0.99f);
    private static readonly Color Muted = new Color(0.65f, 0.72f, 0.78f);
    private static readonly Color Accent = new Color(0.28f, 0.88f, 0.72f);
    private static TMP_FontAsset font;
    private static Sprite solid;
    public const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    public static bool TextResourcesReady => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) != null;

    public static void PrepareResources()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) throw new InvalidOperationException("Import TMP Essentials before creating the layout.");
        Phase2AnimationAssets.Folder("Assets/UI/HUD");
        const string spritePath = "Assets/UI/HUD/SolidUI.asset";
        solid = AssetDatabase.LoadAllAssetsAtPath(spritePath).OfType<Sprite>().FirstOrDefault();
        if (solid != null) return;
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "SolidUI", filterMode = FilterMode.Point };
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();
        AssetDatabase.CreateAsset(texture, spritePath);
        solid = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        solid.name = "Solid";
        AssetDatabase.AddObjectToAsset(solid, texture);
        AssetDatabase.SaveAssets();
    }

    public static GameObject Create()
    {
        PrepareResources();
        GameObject root = new GameObject("GameUI");
        GameUIController screens = root.AddComponent<GameUIController>();
        RectTransform hud = MakeCanvas(root.transform, "HUDCanvas", 10);
        CanvasGroup group = hud.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        PlayerHudPresenter presenter = hud.gameObject.AddComponent<PlayerHudPresenter>();
        Ref(presenter, "screens", screens);
        Ref(presenter, "hudGroup", group);

        RectTransform health = Card(hud, "Health", new Vector2(360, 136), Vector2.zero, new Vector2(48, 48));
        Label(health, "HealthLabel", "HEALTH", 18, Muted, new Vector2(22, 96), new Vector2(220, 26));
        TMP_Text healthValue = Label(health, "HealthValue", "100 / 100", 38, Ink, new Vector2(22, 43), new Vector2(306, 54));
        RectTransform track = Rect(health, "HealthTrack", new Vector2(316, 9), Vector2.zero, new Vector2(22, 22));
        Picture(track, new Color(0.15f, 0.2f, 0.24f));
        RectTransform fill = Stretch(track, "HealthFill");
        UnityEngine.UI.Image healthFill = Picture(fill, Accent);
        healthFill.type = UnityEngine.UI.Image.Type.Filled;
        healthFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
        healthFill.fillAmount = 1f;
        Ref(presenter, "healthFill", healthFill);
        Ref(presenter, "healthText", healthValue);

        RectTransform weapon = Card(hud, "Weapon", new Vector2(300, 180), new Vector2(1, 0), new Vector2(-48, 48));
        RectTransform stripe = Rect(weapon, "Accent", new Vector2(4, 32), Vector2.zero, new Vector2(0, 126));
        Picture(stripe, Accent);
        TMP_Text weaponName = Label(weapon, "WeaponName", "RIFLE", 28, Ink, new Vector2(22, 126), new Vector2(256, 42));
        TMP_Text magazine = Label(weapon, "MagazineAmmo", "30", 74, Ink, new Vector2(20, 38), new Vector2(130, 92));
        TMP_Text reserve = Label(weapon, "ReserveAmmo", "120", 32, Muted, new Vector2(168, 61), new Vector2(112, 43));
        Label(weapon, "MagazineLabel", "MAGAZINE", 16, Muted, new Vector2(24, 21), new Vector2(132, 24));
        Label(weapon, "ReserveLabel", "RESERVE", 16, Muted, new Vector2(168, 37), new Vector2(112, 24));
        RectTransform divider = Rect(weapon, "Divider", new Vector2(1, 56), Vector2.zero, new Vector2(150, 44));
        Picture(divider, new Color(0.35f, 0.43f, 0.49f, 0.6f));
        Ref(presenter, "weaponText", weaponName);
        Ref(presenter, "magazineText", magazine);
        Ref(presenter, "reserveText", reserve);
        RectTransform reloading = Card(hud, "ReloadingIndicator", new Vector2(300, 42), new Vector2(1, 0), new Vector2(-48, 242));
        Label(reloading, "Label", "RELOADING", 20, new Color(1f, 0.78f, 0.32f), new Vector2(20, 6), new Vector2(260, 30));
        Ref(presenter, "reloadingIndicator", reloading.gameObject);
        reloading.gameObject.SetActive(false);

        RectTransform equipment = Rect(hud, "EquipmentPlaceholders", new Vector2(336, 120), new Vector2(0.5f, 0), new Vector2(0, 48));
        Label(equipment, "Label", "EQUIPMENT", 17, Muted, new Vector2(0, 90), new Vector2(336, 26), TextAlignmentOptions.Center);
        RectTransform slots = Rect(equipment, "Slots", new Vector2(336, 80), Vector2.zero, Vector2.zero);
        UnityEngine.UI.HorizontalLayoutGroup layout = slots.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 12;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        for (int i = 1; i <= 3; i++)
        {
            RectTransform slot = Card(slots, "Slot" + i, new Vector2(104, 80), Vector2.zero, Vector2.zero);
            Label(slot, "Number", "0" + i, 21, Muted, new Vector2(10, 40), new Vector2(84, 30), TextAlignmentOptions.Center);
            Label(slot, "Status", "EMPTY", 16, Muted, new Vector2(10, 12), new Vector2(84, 24), TextAlignmentOptions.Center);
        }

        RectTransform wave = Card(hud, "WavePlaceholders", new Vector2(340, 104), Vector2.one, new Vector2(-48, -48));
        Label(wave, "WaveLabel", "CURRENT WAVE", 18, Muted, new Vector2(22, 59), new Vector2(245, 30));
        Label(wave, "WaveValue", "--", 28, Ink, new Vector2(278, 55), new Vector2(40, 38), TextAlignmentOptions.Right);
        Label(wave, "RemainingLabel", "ZOMBIES REMAINING", 18, Muted, new Vector2(22, 17), new Vector2(245, 30));
        Label(wave, "RemainingValue", "--", 28, Ink, new Vector2(278, 13), new Vector2(40, 38), TextAlignmentOptions.Right);

        RectTransform crosshair = Rect(hud, "Crosshair", new Vector2(44, 44), new Vector2(0.5f, 0.5f), Vector2.zero);
        CrosshairPart(crosshair, "Dot", new Vector2(4, 4), Vector2.zero);
        CrosshairPart(crosshair, "Left", new Vector2(10, 3), new Vector2(-13, 0));
        CrosshairPart(crosshair, "Right", new Vector2(10, 3), new Vector2(13, 0));
        CrosshairPart(crosshair, "Top", new Vector2(3, 10), new Vector2(0, 13));
        CrosshairPart(crosshair, "Bottom", new Vector2(3, 10), new Vector2(0, -13));
        crosshair.gameObject.SetActive(false);
        Ref(presenter, "crosshair", crosshair.gameObject);

        RectTransform modals = MakeCanvas(root.transform, "ModalCanvas", 20);
        GamePanelsPresenter panels = modals.gameObject.AddComponent<GamePanelsPresenter>();
        Ref(panels, "screens", screens);
        RectTransform pause = Modal(modals, "PausePanel", "PAUSED", "Your run is on hold.", Accent);
        RectTransform pauseContent = (RectTransform)pause.Find("Content");
        UnityEngine.UI.Button resume = Button(pauseContent, "ResumeButton", "RESUME", 134, true);
        UnityEngine.UI.Button pauseRestart = Button(pauseContent, "RestartButton", "RESTART", 54, false);
        Ref(panels, "pausePanel", pause.gameObject);
        Ref(panels, "resumeButton", resume);
        Ref(panels, "pauseRestartButton", pauseRestart);

        RectTransform gameOver = Modal(modals, "GameOverPanel", "GAME OVER", "Your run has ended.", new Color(1f, 0.38f, 0.34f));
        Ref(panels, "gameOverPanel", gameOver.gameObject);
        Ref(panels, "gameOverRestartButton", Button((RectTransform)gameOver.Find("Content"), "RestartButton", "TRY AGAIN", 72, true));
        RectTransform victory = Modal(modals, "VictoryPanel", "VICTORY", "You made it through.", Accent);
        Ref(panels, "victoryPanel", victory.gameObject);
        Ref(panels, "victoryRestartButton", Button((RectTransform)victory.Find("Content"), "RestartButton", "PLAY AGAIN", 72, true));
        pause.gameObject.SetActive(false);
        gameOver.gameObject.SetActive(false);
        victory.gameObject.SetActive(false);
        return root;
    }

    public static RectTransform MakeCanvas(Transform parent, string name, int order)
    {
        RectTransform rect = Rect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero);
        Canvas canvas = rect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        UnityEngine.UI.CanvasScaler scaler = rect.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        rect.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return rect;
    }

    private static RectTransform Modal(RectTransform parent, string name, string title, string description, Color accent)
    {
        RectTransform overlay = Stretch(parent, name);
        Picture(overlay, new Color(0.015f, 0.025f, 0.04f, 0.8f), true);
        RectTransform content = Card(overlay, "Content", new Vector2(620, 460), new Vector2(0.5f, 0.5f), Vector2.zero);
        RectTransform line = Rect(content, "Accent", new Vector2(620, 4), new Vector2(0, 1), Vector2.zero);
        Picture(line, accent);
        Label(content, "Eyebrow", "ZOMBIE OUTBREAK", 18, Muted, new Vector2(50, 382), new Vector2(520, 30), TextAlignmentOptions.Center);
        Label(content, "Title", title, 54, accent, new Vector2(50, 297), new Vector2(520, 75), TextAlignmentOptions.Center);
        Label(content, "Description", description, 23, Ink, new Vector2(50, 250), new Vector2(520, 42), TextAlignmentOptions.Center);
        return overlay;
    }

    public static UnityEngine.UI.Button Button(RectTransform parent, string name, string caption, float y, bool primary)
    {
        RectTransform rect = Rect(parent, name, new Vector2(468, 64), new Vector2(0.5f, 0), new Vector2(0, y));
        UnityEngine.UI.Image image = Picture(rect, primary ? Accent : new Color(0.14f, 0.19f, 0.23f), true);
        UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        UnityEngine.UI.ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.selectedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        Label(rect, "Label", caption, 24, primary ? new Color(0.025f, 0.1f, 0.1f) : Ink,
            Vector2.zero, new Vector2(468, 64), TextAlignmentOptions.Center);
        return button;
    }

    private static void CrosshairPart(RectTransform parent, string name, Vector2 size, Vector2 position)
    {
        RectTransform rect = Rect(parent, name, size, new Vector2(0.5f, 0.5f), position);
        Picture(rect, Ink);
        UnityEngine.UI.Shadow shadow = rect.gameObject.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(1, -1);
    }

    public static RectTransform Card(Transform parent, string name, Vector2 size, Vector2 anchor, Vector2 position)
    {
        RectTransform rect = Rect(parent, name, size, anchor, position);
        Picture(rect, Panel);
        return rect;
    }

    public static UnityEngine.UI.Image Picture(RectTransform rect, Color color, bool raycast = false)
    {
        UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = solid;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    public static TMP_Text Label(Transform parent, string name, string value, float size, Color color,
        Vector2 position, Vector2 dimensions, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        RectTransform rect = Rect(parent, name, dimensions, Vector2.zero, position);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.text = value;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 anchor, Vector2 position)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5; // UI
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static RectTransform Stretch(Transform parent, string name)
    {
        RectTransform rect = Rect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static void Ref(UnityEngine.Object owner, string field, UnityEngine.Object value)
    {
        SerializedObject so = new SerializedObject(owner);
        SerializedProperty property = so.FindProperty(field);
        if (property == null) throw new InvalidOperationException(owner.name + " has no field " + field);
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
```


## PackageInstaller

Full file path: D:/GV project/Assets/Editor/ProjectBootstrap/PackageInstaller.cs

```csharp
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ProjectBootstrap
{
    /// <summary>Enables only the built-in engine modules used by Phases 1-3.</summary>
    public static class PackageInstaller
    {
        private static AddAndRemoveRequest request;
        private static double deadline;
        public static void Install()
        {
            request = Client.AddAndRemove(new[]
            {
                "com.unity.modules.animation@1.0.0", "com.unity.modules.audio@1.0.0",
                "com.unity.modules.physics@1.0.0",
                "com.unity.modules.particlesystem@1.0.0"
            });
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Poll;
        }

        // Invoke headlessly without -quit; Package Manager must finish asynchronously.
        public static void InstallHUD()
        {
            request = Client.AddAndRemove(new[] { "com.unity.ugui@2.5.0" });
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Poll;
        }
        public static void InstallNavigation()
        {
            request = Client.AddAndRemove(new[] { "com.unity.ai.navigation@2.0.15" });
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Poll;
        }
        private static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            bool success = request.IsCompleted && request.Status == StatusCode.Success;
            if (success) Debug.Log("Requested project packages resolved successfully.");
            else Debug.LogError(request.IsCompleted ? request.Error.message : "Package resolution timed out.");
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
```


## AmbusherBrain

Full file path: D:/GV project/Assets/Scripts/AI/Ambusher/AmbusherBrain.cs

```csharp
using UnityEngine;

public enum AmbusherState { Observe, Evaluate, Move, Wait, Strike, Reposition, Warn }

/// <summary>Member 3: concealed positioning, observation-based interception and explicit state exits.</summary>
public sealed class AmbusherBrain : MonoBehaviour
{
    [SerializeField] private float travelSpeed = 3.1f;
    [SerializeField] private float strikeSpeed = 4.4f;
    [SerializeField] private float flankWeight = .3f;
    [SerializeField] private float concealmentWeight = .25f;
    [SerializeField] private float travelWeight = .15f;
    [SerializeField] private float interceptWeight = .15f;
    [SerializeField] private float proximityWeight = .15f;
    [SerializeField, Min(.2f)] private float warningSeconds = .6f;
    [SerializeField, Min(1)] private float opportunityRange = 7;
    private EnemyActor actor;
    private float nextThink, stateUntil, nextAmbush;
    private MapMarker selected, rejected;
    public AmbusherState State { get; private set; }
    public MapMarker Selected => selected;
    public float[] PointScores { get; private set; }

    private void Awake() { actor = GetComponent<EnemyActor>(); stateUntil = Time.time + .75f; }
    private void OnDisable() => ReleasePosition();

    private void Update()
    {
        if (actor.CanThink && !actor.Combat.IsAttacking && actor.Perception.HasMemory &&
            (State == AmbusherState.Wait || State == AmbusherState.Warn))
            actor.Movement.Face(actor.Perception.LastKnownPosition);
        if (!actor.TickDue(ref nextThink, .2f) || actor.Combat.IsAttacking) return;
        var s = actor.Perception;
        if (!s.HasMemory)
        {
            SetState(AmbusherState.Observe, .75f);
            actor.Investigate(travelSpeed);
            return;
        }
        if (State == AmbusherState.Observe && Time.time >= stateUntil)
            SetState(AmbusherState.Evaluate, 0);
        if (State == AmbusherState.Evaluate || State == AmbusherState.Reposition)
        {
            if (Time.time < nextAmbush) { SetState(AmbusherState.Strike, 4); }
            else
            {
                var candidate = ChoosePoint();
                if (candidate != null && actor.World.Reserve(candidate, actor))
                {
                    selected = candidate;
                    actor.Movement.ClearFailure();
                    SetState(AmbusherState.Move, 12);
                }
                else { nextAmbush = Time.time + 5; SetState(AmbusherState.Strike, 4); }
            }
        }
        if (State == AmbusherState.Move)
        {
            if (selected == null || Time.time >= stateUntil || actor.Movement.Failed)
            { rejected = selected; SetState(AmbusherState.Reposition, 0); return; }
            if (s.Visible && s.Distance <= actor.Combat.Range + .5f)
            { SetState(AmbusherState.Warn, warningSeconds); return; }
            if (Vector3.Distance(transform.position, selected.transform.position) < 1.3f)
            { actor.Movement.Stop(); SetState(AmbusherState.Wait, 8); }
            else if (!actor.Movement.MoveTo(selected.transform.position, travelSpeed, .6f))
            { rejected = selected; SetState(AmbusherState.Reposition, 0); }
            actor.Movement.OpenNearbyDoor();
        }
        if (State == AmbusherState.Wait)
        {
            bool playerLooking = s.Visible && Vector3.Dot(s.ObservedForward,
                (transform.position - s.TargetPosition).normalized) > .55f;
            bool opportunity = s.Visible && s.Distance <= opportunityRange && (!playerLooking || s.PlayerReloading);
            if (opportunity) SetState(AmbusherState.Warn, warningSeconds);
            else if (playerLooking || Time.time >= stateUntil)
            { rejected = selected; nextAmbush = Time.time + 1; SetState(AmbusherState.Reposition, 0); }
            // Waiting expires into relocation, never a blind strike.
        }
        if (State == AmbusherState.Warn)
        {
            actor.Movement.Stop();
            actor.Movement.Face(s.LastKnownPosition);
            if (Time.time >= stateUntil) SetState(AmbusherState.Strike, 4);
            return;
        }
        if (State == AmbusherState.Strike)
        {
            if (!s.Visible) { actor.Investigate(travelSpeed); if (Time.time >= stateUntil) SetState(AmbusherState.Reposition, 0); return; }
            if (actor.Combat.TryAttack())
            { nextAmbush = Time.time + actor.Combat.Duration + 2; SetState(AmbusherState.Reposition, 0); return; }
            if (Time.time >= stateUntil)
            { SetState(AmbusherState.Reposition, 0); actor.Movement.Stop(); return; }
            actor.Approach(strikeSpeed);
        }
    }

    private void SetState(AmbusherState next, float seconds)
    {
        if (next != AmbusherState.Move && next != AmbusherState.Wait) ReleasePosition();
        State = next;
        stateUntil = Time.time + seconds;
        actor.Decision = "Tactical: " + next;
    }

    private void ReleasePosition()
    {
        if (actor != null && actor.World != null) actor.World.Release(actor);
        selected = null;
    }

    public MapMarker ChoosePoint()
    {
        var world = actor.World;
        var points = world.tacticalPoints;
        if (points == null) return null;
        if (PointScores == null || PointScores.Length != points.Length) PointScores = new float[points.Length];
        MapMarker best = null;
        float bestScore = float.NegativeInfinity;
        var s = actor.Perception;
        Vector3 player = s.LastKnownPosition, predicted = player + s.ObservedVelocity * 1.5f;
        for (int i = 0; i < points.Length; i++)
        {
            var point = points[i];
            PointScores[i] = -1;
            if (point == null || point == rejected || (point.kind != MapMarkerKind.Ambush && point.kind != MapMarkerKind.Cover) ||
                !world.Available(point, actor) || !actor.Movement.TryPathLength(point.transform.position, out float pathLength)) continue;
            Vector3 p = point.transform.position, relative = p - player;
            bool concealed = Physics.Linecast(player + Vector3.up, p + Vector3.up, world.obstructionMask, QueryTriggerInteraction.Ignore);
            float flank = (1 - Vector3.Dot(s.ObservedForward, relative.normalized)) * .5f;
            if (!concealed && flank < .65f) continue;
            float travel = 1 - Mathf.Clamp01(pathLength / 40);
            float proximity = 1 - Mathf.Clamp01(Mathf.Abs(relative.magnitude - 8) / 25);
            float intercept = 1 - Mathf.Clamp01(Vector3.Distance(p, predicted) / 30);
            float score = flank * flankWeight + (concealed ? 1 : 0) * concealmentWeight + travel * travelWeight +
                intercept * interceptWeight + proximity * proximityWeight;
            PointScores[i] = score;
            if (score > bestScore) { bestScore = score; best = point; }
        }
        return best;
    }
}
```


## EliteBrain

Full file path: D:/GV project/Assets/Scripts/AI/Elite/EliteBrain.cs

```csharp
using UnityEngine;

public enum EliteAction { DirectAttack, HeavyAttack, Push, MaintainDistance, Reposition, Retreat }

/// <summary>Member 4: survival and action feasibility above committed utility decisions.</summary>
public sealed class EliteBrain : MonoBehaviour
{
    [SerializeField] private float advanceSpeed = 2.8f;
    [SerializeField] private float pushSpeed = 4;
    [SerializeField] private float heavyCooldown = 6;
    [SerializeField, Range(0, 1)] private float retreatHealth = .25f;
    [SerializeField] private float aggressionWeight = 1;
    [SerializeField] private float tacticalWeight = 1;
    [SerializeField, Min(.3f)] private float commitmentSeconds = 1.3f;
    private EnemyActor actor;
    private float nextThink, nextHeavy, nextRetreat, retreatUntil, committedUntil, actionUntil, advanceUntil;
    private float healthSnapshot = 1, damagedUntil;
    private MapMarker cover;
    public EliteAction Action { get; private set; }
    public float[] Scores { get; } = new float[5];
    public int NearbyAllies { get; private set; }
    public MapMarker Cover => cover;

    private void Awake() { actor = GetComponent<EnemyActor>(); nextThink = Random.value * .3f; }
    private void OnDisable() => ReleaseCover();

    public static void Score(float distance, float weakness, bool shotgun, bool firing, int allies,
        bool reachable, bool heavyReady, float range, float[] scores)
    {
        scores[0] = reachable ? Mathf.Clamp01(.45f + .3f * weakness + (distance <= range ? .2f : 0)) : 0;
        scores[1] = heavyReady && distance <= range ? 1.5f : 0;
        scores[2] = reachable ? Mathf.Clamp01(.2f + .35f * Mathf.Clamp01(allies / 4f) + .35f * weakness) : 0;
        scores[3] = shotgun ? Mathf.Clamp01((1 - distance / 12) * .95f * (1 - weakness * .6f)) : 0;
        scores[4] = Mathf.Clamp01(.2f + (reachable ? 0 : .55f) + (firing ? .25f : 0));
    }

    private void Update()
    {
        if (!actor.TickDue(ref nextThink, .3f) || actor.Combat.IsAttacking) return;
        var s = actor.Perception;
        if (actor.Health.Fraction < healthSnapshot) damagedUntil = Time.time + 3;
        healthSnapshot = actor.Health.Fraction;
        if (!s.Visible)
        {
            ReleaseCover();
            retreatUntil = 0;
            actor.Investigate(advanceSpeed);
            return;
        }
        if (actor.Health.Fraction < retreatHealth && Time.time < damagedUntil && Time.time >= nextRetreat)
        {
            retreatUntil = Time.time + 3;
            nextRetreat = Time.time + 12;
            Set(EliteAction.Retreat);
        }
        if (Time.time < retreatUntil)
        {
            actor.Decision = "Elite / retreat under pressure";
            if (!MoveToCover(true))
            {
                retreatUntil = 0;
                actor.Movement.Stop(); // A failed retreat must not become a blind charge.
            }
            return;
        }
        if (Action == EliteAction.Retreat)
        { ReleaseCover(); Set(EliteAction.DirectAttack); advanceUntil = Time.time + 2; }
        NearbyAllies = actor.World.AlliesNear(actor, 12);
        bool reachable = actor.Movement.Reachable(s.TargetPosition);
        bool canStrike = actor.Combat.CanStartAttack;
        Score(s.Distance, s.PlayerWeakness, s.Weapon == WeaponCategory.Shotgun, s.PlayerFiring,
            NearbyAllies, reachable, Time.time >= nextHeavy && canStrike, actor.Combat.Range, Scores);
        Scores[0] *= aggressionWeight;
        Scores[2] *= aggressionWeight;
        Scores[3] *= tacticalWeight;
        Scores[4] *= tacticalWeight;
        if (Time.time < advanceUntil) Scores[3] = Scores[4] = 0;
        int best = 0;
        for (int i = 1; i < Scores.Length; i++) if (Scores[i] > Scores[best]) best = i;
        bool expired = Time.time >= actionUntil;
        bool invalid = Action == EliteAction.HeavyAttack && !canStrike || actor.Movement.Failed;
        if (expired && (Action == EliteAction.Reposition || Action == EliteAction.MaintainDistance))
        {
            advanceUntil = Time.time + 2;
            best = 0;
        }
        if (expired || invalid || best == 1 ||
            (Time.time >= committedUntil && Scores[best] > Scores[(int)Action] + .1f))
            Set((EliteAction)best);
        actor.Decision = "Hierarchy / utility: " + Action;
        if (Action == EliteAction.HeavyAttack)
        {
            if (actor.Combat.TryAttack(true))
            { nextHeavy = Time.time + heavyCooldown; actionUntil = 0; }
            else { Set(EliteAction.DirectAttack); actor.Approach(advanceSpeed); }
            return;
        }
        if (Action == EliteAction.Reposition)
        {
            if (!MoveToCover(false)) { Set(EliteAction.DirectAttack); actor.Approach(advanceSpeed); }
            return;
        }
        if (Action == EliteAction.MaintainDistance)
        {
            Vector3 away = Vector3.ProjectOnPlane(transform.position - s.TargetPosition, Vector3.up).normalized;
            if (!actor.Movement.MoveTo(s.TargetPosition + away * 7, advanceSpeed))
            { Set(EliteAction.DirectAttack); actor.Approach(advanceSpeed); }
            return;
        }
        if (Action == EliteAction.Push) actor.World.EncourageAdvance(actor, 12, 1.5f);
        if (actor.Combat.TryAttack()) { actionUntil = 0; return; }
        actor.Approach(Action == EliteAction.Push ? pushSpeed : advanceSpeed);
    }

    private void Set(EliteAction value)
    {
        if (Action != value) ReleaseCover();
        Action = value;
        committedUntil = Time.time + commitmentSeconds;
        actionUntil = Time.time + 3;
        actor.Movement.ClearFailure();
        actor.Decision = "Hierarchy / utility: " + value;
    }

    private void ReleaseCover()
    {
        if (actor != null && actor.World != null) actor.World.Release(actor);
        cover = null;
    }

    private bool MoveToCover(bool retreat)
    {
        if (cover == null)
        {
            float highest = float.NegativeInfinity;
            var points = actor.World.tacticalPoints;
            if (points == null) return false;
            foreach (var p in points)
            {
                if (p == null || (p.kind != MapMarkerKind.Cover && p.kind != MapMarkerKind.Ambush) ||
                    !actor.World.Available(p, actor) || !actor.Movement.TryPathLength(p.transform.position, out float length)) continue;
                float distance = Vector3.Distance(p.transform.position, actor.Perception.LastKnownPosition);
                bool concealed = Physics.Linecast(actor.Perception.LastKnownPosition + Vector3.up,
                    p.transform.position + Vector3.up, actor.World.obstructionMask, QueryTriggerInteraction.Ignore);
                if (retreat && !concealed && distance < actor.Perception.Distance + 2) continue;
                float score = (retreat ? Mathf.Min(distance, 18) : -Mathf.Abs(distance - 7)) - .4f * length + (concealed ? 4 : 0);
                if (score > highest) { highest = score; cover = p; }
            }
            if (cover == null || !actor.World.Reserve(cover, actor)) { cover = null; return false; }
        }
        if (!actor.Movement.MoveTo(cover.transform.position, retreat ? pushSpeed : advanceSpeed, .6f))
        { ReleaseCover(); return false; }
        if (actor.Movement.Arrived) actor.Movement.Stop();
        return true;
    }
}
```


## HunterBrain

Full file path: D:/GV project/Assets/Scripts/AI/Hunter/HunterBrain.cs

```csharp
using System.Collections.Generic;
using UnityEngine;

public sealed class HunterBrain : MonoBehaviour
{
    [SerializeField] private float speed = 2.25f;
    [SerializeField, Min(.5f)] private float replanInterval = 1.5f;
    [SerializeField] private bool drawPath;
    private EnemyActor actor;
    private HunterNavigationGraph graph;
    private float nextThink, nextPlan, earliestPlan;
    private Vector3 plannedGoal;
    private bool plannedVisible;
    private readonly List<Vector3> path = new List<Vector3>();
    private int waypoint;
    public int ExpandedNodes { get; private set; }
    public float PathCost { get; private set; }

    private void Awake() { actor = GetComponent<EnemyActor>(); nextThink = Random.value * .3f; }

    private void Update()
    {
        if (!actor.TickDue(ref nextThink, .15f)) return;
        if (graph == null) graph = actor.World.GetComponent<HunterNavigationGraph>();
        if (actor.Combat.IsAttacking) { actor.Decision = "Hunter / committed strike"; return; }
        var sensed = actor.Perception;
        if (actor.Combat.TryAttack()) { path.Clear(); return; }
        Vector3 goal;
        if (sensed.Visible) goal = actor.World.ApproachPosition(actor);
        else if (!actor.InvestigationGoal(out goal))
        { actor.Movement.Stop(); path.Clear(); return; }
        bool changed = (goal - plannedGoal).sqrMagnitude > 4 || sensed.Visible != plannedVisible || actor.Movement.Failed;
        if (Time.time >= earliestPlan && (changed || Time.time >= nextPlan || path.Count == 0))
        {
            earliestPlan = Time.time + .45f;
            nextPlan = Time.time + replanInterval;
            plannedGoal = goal;
            plannedVisible = sensed.Visible;
            bool found = graph != null && graph.FindPath(transform.position, goal, path);
            waypoint = 0;
            ExpandedNodes = graph != null ? graph.LastExpanded : 0;
            PathCost = graph != null ? graph.LastPathCost : 0;
            if (!found)
            {
                path.Clear();
                actor.Decision = "Hunter / A* route unavailable";
                actor.Movement.Stop();
                return;
            }
            actor.Movement.ClearFailure();
        }
        while (waypoint < path.Count && Vector3.Distance(transform.position, path[waypoint]) < .6f) waypoint++;
        if (waypoint < path.Count)
        {
            actor.Decision = sensed.Visible ? "Hunter / track with A*" : "Hunter / investigate with A*";
            bool final = waypoint == path.Count - 1;
            if (!actor.Movement.MoveTo(path[waypoint], speed, final ? 1.2f : .2f)) nextPlan = 0;
            actor.Movement.OpenNearbyDoor();
        }
        else actor.Movement.Stop();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawPath) return;
        Gizmos.color = Color.yellow;
        for (int i = 1; i < path.Count; i++) Gizmos.DrawLine(path[i - 1] + Vector3.up * .1f, path[i] + Vector3.up * .1f);
    }
}
```


## HunterNavigationGraph

Full file path: D:/GV project/Assets/Scripts/AI/Hunter/HunterNavigationGraph.cs

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Member 1: explicit A* with f=g+h over a NavMesh-sampled grid. Euclidean h is admissible.</summary>
[DefaultExecutionOrder(-50)]
public sealed class HunterNavigationGraph : MonoBehaviour
{
    [SerializeField]
    private Vector3 center;
    [SerializeField]
    private Vector2 size = new Vector2(72, 72);
    [SerializeField, Min(1)]
    private float spacing = 2;
    [SerializeField, Min(64)]
    private int expansionBudget = 1600;
    private Vector3[] nodes;
    private bool[] walkable, closed, opened;
    private float[] g;
    private int[] parent;
    private int columns, rows;
    private readonly List<int> open = new List<int>(256);
    public int LastExpanded { get; private set; }
    public float LastPathCost { get; private set; }

    private void Awake() => Build();
    public void Build()
    {
        columns = Mathf.CeilToInt(size.x / spacing) + 1;
        rows = Mathf.CeilToInt(size.y / spacing) + 1;
        int count = columns * rows;
        nodes = new Vector3[count];
        walkable = new bool[count];
        closed = new bool[count];
        opened = new bool[count];
        g = new float[count];
        parent = new int[count];
        for (int z = 0; z < rows; z++)
            for (int x = 0; x < columns; x++)
            {
                int id = z * columns + x;
                var p = center + new Vector3(x * spacing - size.x / 2, 0, z * spacing - size.y / 2);
                walkable[id] = NavMesh.SamplePosition(p, out var hit, spacing * .42f, NavMesh.AllAreas);
                nodes[id] = walkable[id] ? hit.position : p;
            }
    }

    public bool FindPath(Vector3 start, Vector3 goal, List<Vector3> result)
    {
        result.Clear();
        if (nodes == null)
            Build();
        int first = Nearest(start), last = Nearest(goal);
        LastExpanded = 0;
        LastPathCost = 0;
        if (first < 0 || last < 0)
            return false;
        if (NavMesh.Raycast(start, nodes[first], out _, NavMesh.AllAreas))
            return false;
        System.Array.Clear(closed, 0, closed.Length);
        System.Array.Clear(opened, 0, opened.Length);
        open.Clear();
        for (int i = 0; i < g.Length; i++)
        {
            g[i] = float.PositiveInfinity;
            parent[i] = -1;
        }

        g[first] = 0;
        open.Add(first);
        opened[first] = true;
        while (open.Count > 0 && LastExpanded < expansionBudget)
        {
            int best = 0;
            float score = float.PositiveInfinity;
            for (int i = 0; i < open.Count; i++)
            {
                int id = open[i];
                float f = g[id] + Vector3.Distance(nodes[id], nodes[last]);
                if (f < score)
                {
                    score = f;
                    best = i;
                }
            }

            int current = open[best];
            open.RemoveAt(best);
            opened[current] = false;
            closed[current] = true;
            LastExpanded++;
            if (current == last)
            {
                if (NavMesh.Raycast(nodes[last], goal, out _, NavMesh.AllAreas))
                    return false;
                LastPathCost = g[last];
                for (int at = last; at >= 0; at = parent[at])
                    result.Add(nodes[at]);
                result.Reverse();
                result.Add(goal);
                return true;
            }

            int cx = current % columns, cz = current / columns;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (dx == 0 && dz == 0 || x < 0 || z < 0 || x >= columns || z >= rows)
                        continue;
                    int next = z * columns + x;
                    if (!walkable[next] || closed[next])
                        continue;
                    float candidate = g[current] + Vector3.Distance(nodes[current], nodes[next]);
                    if (candidate >= g[next])
                        continue;
                    // Dynamic carving can invalidate a previously walkable edge; query it at expansion time.
                    if (NavMesh.Raycast(nodes[current], nodes[next], out _, NavMesh.AllAreas))
                        continue;
                    g[next] = candidate;
                    parent[next] = current;
                    if (!opened[next])
                    {
                        opened[next] = true;
                        open.Add(next);
                    }
                }
        }

        return false;
    }

    private int Nearest(Vector3 p)
    {
        int best = -1;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < nodes.Length; i++)
        {
            if (!walkable[i])
                continue;
            float d = (nodes[i] - p).sqrMagnitude;
            if (d < distance)
            {
                distance = d;
                best = i;
            }
        }

        return best;
    }
}
```


## RunnerBrain

Full file path: D:/GV project/Assets/Scripts/AI/Runner/RunnerBrain.cs

```csharp
using UnityEngine;

public enum RunnerAction { Chase, Attack, Caution, Flank, Disengage }

/// <summary>Member 2: utility scores plus bounded action commitments and rush recovery.</summary>
public sealed class RunnerBrain : MonoBehaviour
{
    [SerializeField] private float chaseSpeed = 4.8f;
    [SerializeField] private float cautionSpeed = 2.5f;
    [SerializeField] private float aggressionWeight = 1;
    [SerializeField] private float survivalWeight = 1;
    [SerializeField] private float tacticalWeight = 1;
    [SerializeField, Range(0, .3f)] private float hysteresis = .08f;
    [SerializeField, Min(.2f)] private float commitmentSeconds = .8f;
    [SerializeField, Min(.5f)] private float rushSeconds = 2.4f;
    [SerializeField, Min(.3f)] private float recoverySeconds = 1.2f;
    private EnemyActor actor;
    private float nextThink, changedAt, actionUntil, recoverUntil, nextRetreat, rushUntil;
    private Vector3 tacticalGoal;
    private bool hasTacticalGoal;
    public RunnerAction Action { get; private set; }
    public float[] Scores { get; } = new float[5];
    public bool PathAvailable { get; private set; }
    public float ShotgunThreat { get; private set; }

    private void Awake()
    {
        actor = GetComponent<EnemyActor>();
        nextThink = Random.value * .3f;
    }
    private void OnEnable() => GetComponent<EnemyCombat>().AttackStarted += OnAttack;
    private void OnDisable() => GetComponent<EnemyCombat>().AttackStarted -= OnAttack;
    private void OnAttack(bool heavy, float duration)
    {
        recoverUntil = Time.time + duration + recoverySeconds;
        rushUntil = 0;
    }

    public static void Score(float distance, float ownHealth, float weakness, bool shotgun, bool firing,
        bool visible, bool reachable, float[] scores, float attackRange = 1.8f, bool attackReady = true)
    {
        float threat = shotgun ? Mathf.Clamp01(1 - distance / 12) : 0;
        scores[0] = Mathf.Clamp01((.38f + .48f * weakness + .2f * Mathf.Clamp01(distance / 20)) * (reachable ? 1 : 0) - .3f * threat);
        scores[1] = visible && attackReady && distance <= attackRange ? 1.5f : 0;
        scores[2] = Mathf.Clamp01(.9f * threat * (1 - .7f * weakness));
        scores[3] = Mathf.Clamp01(.2f + .4f * threat + (firing ? .15f : 0) + (reachable ? 0 : .35f));
        scores[4] = Mathf.Clamp01((1 - ownHealth) * .8f + threat * .2f - .25f * weakness);
    }

    private void Update()
    {
        if (!actor.TickDue(ref nextThink, .25f) || actor.Combat.IsAttacking) return;
        var s = actor.Perception;
        if (Time.time < recoverUntil)
        {
            actor.Decision = "Runner / recover from rush";
            actor.Movement.Stop();
            return;
        }
        if (!s.Visible)
        {
            rushUntil = 0;
            hasTacticalGoal = false;
            actor.Investigate(cautionSpeed);
            return;
        }
        if (rushUntil > 0 && Time.time >= rushUntil)
        {
            rushUntil = 0;
            recoverUntil = Time.time + recoverySeconds;
            actor.Movement.Stop();
            actor.Decision = "Runner / missed rush recovery";
            return;
        }
        PathAvailable = actor.Movement.Reachable(s.TargetPosition);
        ShotgunThreat = s.Weapon == WeaponCategory.Shotgun ? Mathf.Clamp01(1 - s.Distance / 12) : 0;
        Score(s.Distance, actor.Health.Fraction, s.PlayerWeakness, s.Weapon == WeaponCategory.Shotgun,
            s.PlayerFiring, s.Visible, PathAvailable, Scores, actor.Combat.Range, actor.Combat.CanStartAttack);
        Scores[0] = (Scores[0] + (s.PlayerReloading ? .65f : 0) + (actor.IsEncouraged ? .25f : 0)) * aggressionWeight;
        Scores[2] *= survivalWeight;
        Scores[3] *= tacticalWeight;
        Scores[4] = Time.time >= nextRetreat ? Scores[4] * survivalWeight : 0;
        int best = 0;
        for (int i = 1; i < Scores.Length; i++) if (Scores[i] > Scores[best]) best = i;
        bool expired = Time.time >= actionUntil || actor.Movement.Failed ||
            (hasTacticalGoal && actor.Movement.Arrived);
        bool invalidAttack = Action == RunnerAction.Attack && !actor.Combat.CanStartAttack;
        if (expired || invalidAttack || best == 1 ||
            (Time.time - changedAt >= commitmentSeconds && Scores[best] > Scores[(int)Action] + hysteresis))
        {
            // Retreats and circling must yield an approach opportunity, even at low health.
            if (expired && (Action == RunnerAction.Caution || Action == RunnerAction.Disengage || Action == RunnerAction.Flank))
                best = actor.Combat.CanStartAttack ? 1 : 0;
            Select((RunnerAction)best);
        }
        actor.Decision = "Utility: " + Action + (s.PlayerReloading ? " / observed reload" : "");
        if (Action == RunnerAction.Attack)
        {
            if (!actor.Combat.TryAttack()) actor.Approach(cautionSpeed);
            return;
        }
        if (Action == RunnerAction.Chase)
        {
            if (s.Distance < 12 && rushUntil <= 0) rushUntil = Time.time + rushSeconds;
            actor.Approach(chaseSpeed);
            return;
        }
        if (!hasTacticalGoal || !actor.Movement.MoveTo(tacticalGoal, Action == RunnerAction.Caution ? cautionSpeed : chaseSpeed, .6f))
        {
            hasTacticalGoal = false;
            actionUntil = 0;
            actor.Approach(cautionSpeed);
        }
    }

    private void Select(RunnerAction next)
    {
        bool changed = Action != next;
        Action = next;
        changedAt = Time.time;
        actionUntil = Time.time + (next == RunnerAction.Chase ? rushSeconds : 2.5f);
        if (next != RunnerAction.Chase) rushUntil = 0;
        hasTacticalGoal = false;
        if (next == RunnerAction.Disengage) nextRetreat = actionUntil + 5;
        if (next == RunnerAction.Flank || next == RunnerAction.Caution || next == RunnerAction.Disengage)
            hasTacticalGoal = ChooseRoute(next, out tacticalGoal);
        if (changed && next == RunnerAction.Chase) rushUntil = 0;
    }

    private bool ChooseRoute(RunnerAction kind, out Vector3 goal)
    {
        var s = actor.Perception;
        Vector3 away = Vector3.ProjectOnPlane(transform.position - s.TargetPosition, Vector3.up).normalized;
        if (away.sqrMagnitude < .1f) away = -transform.forward;
        goal = transform.position;
        float best = float.NegativeInfinity;
        for (int i = 0; i < 4; i++)
        {
            float angle = kind == RunnerAction.Flank ? (i % 2 == 0 ? 1 : -1) * (60 + i / 2 * 35) : (i - 1.5f) * 25;
            float radius = kind == RunnerAction.Disengage ? 10 : kind == RunnerAction.Flank ? 5 : 7;
            Vector3 candidate = s.TargetPosition + Quaternion.Euler(0, angle, 0) * away * radius;
            if (!actor.Movement.TryPathLength(candidate, out float cost)) continue;
            float side = 1 - Vector3.Dot(s.ObservedForward, (candidate - s.TargetPosition).normalized);
            float score = side * 3 - cost * .2f;
            if (score <= best) continue;
            best = score;
            goal = candidate;
        }
        return best > float.NegativeInfinity;
    }
}
```


## EnemyActor

Full file path: D:/GV project/Assets/Scripts/AI/Shared/EnemyActor.cs

```csharp
using UnityEngine;

/// <summary>Composition root. Decisions live in the individual member folders.</summary>
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMovement), typeof(EnemyCombat))]
public sealed class EnemyActor : MonoBehaviour
{
    public EnemyWorld World { get; private set; }
    public EnemyHealth Health { get; private set; }
    public EnemyMovement Movement { get; private set; }
    public EnemyCombat Combat { get; private set; }
    public EnemyPerception Perception { get; private set; }
    public string Decision { get; set; } = "Uninitialized";
    public float EncouragedUntil { get; set; }
    public bool IsEncouraged => Time.time < EncouragedUntil;
    private Vector3 searchGoal, searchCenter;
    private int searchVersion = -1, searchStep;
    private bool hasSearchGoal, initialVisitComplete;
    private float searchPauseUntil;
    private float searchAngle;
    public bool CanThink => World != null && World.player != null && !World.player.IsDead && !Health.IsDead && Time.timeScale > 0;

    private void Awake()
    {
        searchAngle = Random.Range(0f, 360f);
        Health = GetComponent<EnemyHealth>();
        Movement = GetComponent<EnemyMovement>();
        Combat = GetComponent<EnemyCombat>();
        Perception = GetComponent<EnemyPerception>();
    }

    private void OnEnable() => Health.Died += Die;
    private void OnDisable()
    {
        Health.Died -= Die;
        if (World != null)
            World.Remove(this);
    }

    public void Initialize(EnemyWorld world)
    {
        World = world;
        world.Register(this);
        Perception.Initialize(this);
        Combat.Initialize(this);
        Decision = "Searching";
    }

    public bool TickDue(ref float next, float interval)
    {
        if (!CanThink || Time.time < next)
            return false;
        next = Time.time + interval;
        return true;
    }

    /// <summary>Finite search around evidence; never substitutes the live player's position.</summary>
    public bool InvestigationGoal(out Vector3 goal)
    {
        if (Perception.HasMemory && searchVersion != Perception.ObservationVersion)
        {
            searchVersion = Perception.ObservationVersion;
            searchCenter = Perception.LastKnownPosition;
            searchGoal = searchCenter;
            searchStep = 0;
            hasSearchGoal = true;
            searchPauseUntil = 0;
        }
        if (!Perception.HasMemory)
        {
            if (searchVersion >= 0 || initialVisitComplete)
            { goal = transform.position; Decision = "Alert idle / no target"; return false; }
            searchGoal = World.InitialSearchPosition;
            hasSearchGoal = true;
        }
        if (hasSearchGoal && (Vector3.Distance(transform.position, searchGoal) < 1.5f || Movement.Failed))
        {
            hasSearchGoal = false;
            searchPauseUntil = Time.time + .8f;
            if (!Perception.HasMemory) initialVisitComplete = true;
        }
        if (!hasSearchGoal && Perception.HasMemory && Time.time >= searchPauseUntil && searchStep < 4)
        {
            for (; searchStep < 4; searchStep++)
            {
                float angle = searchAngle + searchStep * 97;
                Vector3 candidate = searchCenter + Quaternion.Euler(0, angle, 0) * Vector3.forward * (3 + searchStep);
                if (!Movement.Reachable(candidate)) continue;
                searchGoal = candidate;
                searchStep++;
                hasSearchGoal = true;
                break;
            }
        }
        goal = searchGoal;
        Decision = hasSearchGoal ? searchStep == 0 ? "Investigate last evidence" : "Search nearby routes" : "Listen / search pause";
        return hasSearchGoal;
    }

    public void Investigate(float speed)
    {
        if (InvestigationGoal(out var goal))
        {
            Movement.MoveTo(goal, speed, .7f);
            Movement.OpenNearbyDoor();
        }
        else Movement.Stop();
    }

    private void Update()
    {
        if (CanThink && !Combat.IsAttacking && Perception.Visible && Perception.Distance <= Combat.Range + .5f)
        {
            Movement.Face(Perception.TargetPosition);
            return;
        }
        if (CanThink && !Combat.IsAttacking && !Perception.Visible && Movement.Status == "Stopped" &&
            (Decision.Contains("Listen") || Decision.Contains("idle")))
            transform.Rotate(0, 35 * Time.deltaTime, 0);
    }

    public void Approach(float speed)
    {
        Vector3 target = World.ApproachPosition(this);
        if (!Movement.MoveTo(target, speed))
        {
            Movement.Stop();
            Decision = "Route blocked / waiting";
        }
        Movement.OpenNearbyDoor();
    }

    private void Die(EnemyHealth health)
    {
        Decision = "Dead";
        Movement.Stop();
        Combat.Cancel();
        if (World != null)
            World.Remove(this);
        GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = false;
        Destroy(gameObject, 5);
    }
}
```


## EnemyAnimationController

Full file path: D:/GV project/Assets/Scripts/AI/Shared/EnemyAnimationController.cs

```csharp
using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class EnemyAnimationController : MonoBehaviour
{
    [SerializeField]
    private Animator animator;
    [Header("Measured foot travel at clip playback speed 1")]
    [SerializeField, Min(.01f)] private float walkStrideSpeed = 1.675673f;
    [SerializeField, Min(.01f)] private float chaseStrideSpeed = 4.528451f;
    [SerializeField, Min(.01f)] private float visualStrideScale = 1f;
    [Header("Locomotion variation")]
    [SerializeField] private bool randomizeLocomotionPhase = true;
    [Header("Contact-timed action clips")]
    [SerializeField] private AnimationClip attackClip;
    [SerializeField] private AnimationClip heavyAttackClip;
    private EnemyHealth health;
    private EnemyMovement movement;
    private EnemyCombat combat;
    private static readonly int Speed = Animator.StringToHash("Speed"), Attack = Animator.StringToHash("Attack"), Heavy = Animator.StringToHash("Heavy"), Hit = Animator.StringToHash("Hit"), Dead = Animator.StringToHash("Dead"), AttackSpeed = Animator.StringToHash("AttackSpeed");
    private static readonly int Locomotion = Animator.StringToHash("Base Layer.Locomotion");
    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement>();
        combat = GetComponent<EnemyCombat>();
    }

    private void Start()
    {
        // Offset only the initial movement cycle so a spawned group does not march
        // in lockstep. Attacks, hits and death always keep their authored timing.
        if (randomizeLocomotionPhase && animator != null && animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController != null && animator.avatar != null &&
            animator.avatar.isValid && animator.avatar.isHuman && animator.HasState(0, Locomotion) &&
            !health.IsDead && !combat.IsAttacking)
            animator.Play(Locomotion, 0, Random.value);
    }

    private void OnEnable()
    {
        health.Hit += OnHit;
        health.Died += OnDeath;
        combat.AttackStarted += OnAttack;
    }

    private void OnDisable()
    {
        health.Hit -= OnHit;
        health.Died -= OnDeath;
        combat.AttackStarted -= OnAttack;
    }

    private void Update()
    {
        if (animator != null && !health.IsDead)
        {
            float speed = Vector3.ProjectOnPlane(movement.Velocity, Vector3.up).magnitude;
            animator.SetFloat(Speed, speed < .05f ? 0f : speed, speed < .05f ? 0f : .04f, Time.deltaTime);
            float reference = animator.GetCurrentAnimatorStateInfo(0).IsName("Chase") ? chaseStrideSpeed : walkStrideSpeed;
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                float nextReference = next.IsName("Chase") ? chaseStrideSpeed : walkStrideSpeed;
                reference = Mathf.Lerp(reference, nextReference, Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime));
            }
            // Only locomotion states consume this parameter; attack/death timing is separate.
            float cadence = speed < .05f ? 1f : Mathf.Clamp(speed / (reference * visualStrideScale), .08f, 3f);
            animator.SetFloat("GaitScale", cadence);
        }
    }

    private void OnHit(float amount)
    {
        if (animator != null && !combat.IsAttacking)
            animator.SetTrigger(Hit);
    }

    private void OnDeath(EnemyHealth value)
    {
        if (animator != null)
        {
            animator.ResetTrigger(Attack);
            animator.ResetTrigger(Heavy);
            animator.ResetTrigger(Hit);
            animator.SetBool(Dead, true);
        }
    }

    private void OnAttack(bool heavy, float duration)
    {
        if (animator != null)
        {
            var clip = heavy ? heavyAttackClip : attackClip;
            // Clip lengths are not necessarily one second. Match the actual action
            // to the combat clock so the visible strike and damage share a contact time.
            float length = clip != null ? clip.length : 1f;
            animator.SetFloat(AttackSpeed, length / Mathf.Max(.05f, duration));
            animator.ResetTrigger(Attack);
            animator.ResetTrigger(Heavy);
            animator.ResetTrigger(Hit);
            animator.CrossFadeInFixedTime(heavy ? "Base Layer.Heavy" : "Base Layer.Attack", .06f, 0, 0f);
        }
    }
}
```


## EnemyCombat

Full file path: D:/GV project/Assets/Scripts/AI/Shared/EnemyCombat.cs

```csharp
using System;
using UnityEngine;

/// <summary>One attack clock controls animation and hit timing; no damage every frame.</summary>
public sealed class EnemyCombat : MonoBehaviour
{
    [SerializeField]
    private float range = 1.8f;
    [SerializeField]
    private float damage = 10;
    [SerializeField]
    private float cooldown = 1.6f;
    [SerializeField]
    private float windup = .5f;
    [SerializeField, Range(.2f, .85f)] private float contactNormalizedTime = .56f;
    [SerializeField, Range(30f, 180f)] private float hitArc = 110f;
    private EnemyActor actor;
    private float readyAt, impactAt, endsAt, trackingEndsAt;
    private bool impacted;
    private bool heavy;
    public bool IsAttacking { get; private set; }
    public float Range => range;
    public float Duration => windup / Mathf.Max(.2f, contactNormalizedTime);
    public float CooldownRemaining => Mathf.Max(0, readyAt - Time.time);
    public bool IsReady => actor != null && actor.CanThink && !IsAttacking && Time.time >= readyAt;
    public bool CanStartAttack => IsReady && actor.Perception.Visible &&
        actor.Perception.Distance <= range && actor.World.AttackSlotAvailable(actor) && ClearStrike();
    public string Phase => IsAttacking ? Time.time < impactAt ? "Anticipation" : "Strike / recovery" :
        CooldownRemaining > 0 ? "Cooldown" : "Ready";

    private bool ClearStrike()
    {
        var target = actor.World.player.transform.position;
        Vector3 direction = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
        return Vector3.Distance(transform.position, target) <= range &&
            (direction.sqrMagnitude < .01f || Vector3.Angle(transform.forward, direction) <= hitArc * .5f) &&
            !Physics.Linecast(transform.position + Vector3.up, target + Vector3.up,
                actor.World.obstructionMask, QueryTriggerInteraction.Ignore);
    }

    public event Action<bool, float> AttackStarted;
    public void Initialize(EnemyActor value) => actor = value;
    public bool TryAttack(bool heavyAttack = false)
    {
        if (!CanStartAttack || !actor.World.TryAcquireAttack(actor))
            return false;
        IsAttacking = true;
        impacted = false;
        heavy = heavyAttack;
        float duration = heavy ? Duration * 1.4f : Duration;
        impactAt = Time.time + (heavy ? windup * 1.4f : windup);
        endsAt = Time.time + duration;
        trackingEndsAt = Time.time + duration * .38f;
        readyAt = endsAt + cooldown * (heavy ? 1.4f : 1);
        actor.Movement.Stop();
        AttackStarted?.Invoke(heavy, duration);
        return true;
    }

    private void Update()
    {
        if (!IsAttacking)
            return;
        if (Time.timeScale <= 0) return;
        if (actor == null || !actor.CanThink)
        {
            Cancel();
            return;
        }

        // Track during anticipation, then commit to the strike. A dodge can miss;
        // the enemy cannot spin around during the contact/recovery pose.
        if (Time.time < trackingEndsAt && actor.Perception.Visible)
            actor.Movement.Face(actor.Perception.TargetPosition);
        if (Time.time >= impactAt)
            AnimationImpact();
        if (Time.time >= endsAt) Cancel();
    }

    // Optional imported-clip AnimationEvent calls this same idempotent damage gate.
    public void AnimationImpact()
    {
        if (!IsAttacking || impacted || Time.time < impactAt || actor == null || !actor.CanThink)
            return;
        impacted = true;
        var target = actor.World.player.transform;
        if (Vector3.Distance(transform.position, target.position) > range + .25f)
            return;
        var direction = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
        if (direction.sqrMagnitude > .01f && Vector3.Angle(transform.forward, direction) > hitArc * .5f)
            return;
        if (Physics.Linecast(transform.position + Vector3.up, target.position + Vector3.up, actor.World.obstructionMask, QueryTriggerInteraction.Ignore))
            return;
        actor.World.player.TakeDamage(damage * (heavy ? 1.8f : 1));
    }

    public void Cancel()
    {
        IsAttacking = false;
        impacted = true;
        if (actor != null && actor.World != null) actor.World.ReleaseAttack(actor);
    }

    private void OnDisable() => Cancel();
}
```


## EnemyHealth

Full file path: D:/GV project/Assets/Scripts/AI/Shared/EnemyHealth.cs

```csharp
using System;
using UnityEngine;

public sealed class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1)]
    private float maxHealth = 60;
    public float Current { get; private set; }
    public float Fraction => Current / maxHealth;
    public bool IsDead { get; private set; }

    public event Action<float> Hit;
    public event Action<EnemyHealth> Died;
    private void Awake() => Current = maxHealth;
    public void TakeDamage(float amount)
    {
        if (amount <= 0 || IsDead || !isActiveAndEnabled)
            return;
        Current = Mathf.Max(0, Current - amount);
        if (Current > 0)
            Hit?.Invoke(amount);
        else
        {
            IsDead = true;
            Died?.Invoke(this);
        }
    }
}
```


## EnemyMovement

Full file path: D:/GV project/Assets/Scripts/AI/Shared/EnemyMovement.cs

```csharp
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public sealed class EnemyMovement : MonoBehaviour
{
    [SerializeField, Min(.5f)] private float stuckSeconds = 2;
    [SerializeField, Min(0)] private int localRetryLimit = 2;
    private NavMeshAgent agent;
    private NavMeshPath probe;
    private float nextDestination, progressAt, retryUntil, blockedUntil;
    private Vector3 requested, progressPosition, blockedGoal;
    private bool moving, failed;
    private int retries;
    public Vector3 Velocity => Ready ? agent.velocity : Vector3.zero;
    private bool Ready => agent != null && agent.enabled && agent.isOnNavMesh;
    public bool Arrived => Ready && moving && !agent.pathPending && !failed &&
        agent.remainingDistance <= agent.stoppingDistance + .25f;
    public bool HasPath => Ready && agent.hasPath && !agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathComplete;
    public bool Failed => failed;
    public string Status { get; private set; } = "Stopped";
    public int RetryCount => retries;
    public void ClearFailure() => failed = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        probe = new NavMeshPath();
    }

    public bool TryPathLength(Vector3 point, out float length)
    {
        length = 0;
        if (!Ready || !NavMesh.SamplePosition(point, out var hit, 2, agent.areaMask) ||
            !agent.CalculatePath(hit.position, probe) || probe.status != NavMeshPathStatus.PathComplete) return false;
        var corners = probe.corners;
        for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
        return true;
    }

    public bool Reachable(Vector3 point) => TryPathLength(point, out _);

    public bool MoveTo(Vector3 destination, float speed, float stoppingDistance = 1.2f)
    {
        if (!Ready) { Status = "Off navigation mesh"; failed = true; return false; }
        if (Time.time < blockedUntil && (destination - blockedGoal).sqrMagnitude < 4) return false;
        if (moving && Time.time < retryUntil) return true;
        bool newJourney = !moving || (requested - destination).sqrMagnitude > 4;
        agent.speed = speed;
        agent.stoppingDistance = Mathf.Max(.1f, stoppingDistance);
        if (moving && Time.time < nextDestination && (requested - destination).sqrMagnitude < 1)
            return !failed; // A pending path is not a failed path.
        nextDestination = Time.time + .3f;
        requested = destination;
        if (!NavMesh.SamplePosition(destination, out var hit, 2, agent.areaMask))
        { Fail("Destination off mesh"); return false; }
        if (newJourney)
        {
            retries = 0;
            progressAt = Time.time;
            progressPosition = transform.position;
        }
        failed = false;
        moving = true;
        agent.isStopped = false;
        Status = "Moving";
        if (!agent.SetDestination(hit.position)) { Fail("Path rejected"); return false; }
        return true;
    }

    private void Update()
    {
        if (!Ready || !moving || agent.isStopped || Time.timeScale <= 0 || agent.pathPending) return;
        if (agent.pathStatus != NavMeshPathStatus.PathComplete) { Fail("Incomplete path"); return; }
        if (Arrived) { Status = "Arrived"; progressAt = Time.time; return; }
        if ((transform.position - progressPosition).sqrMagnitude > .12f)
        {
            progressPosition = transform.position;
            progressAt = Time.time;
        }
        if (Time.time - progressAt < stuckSeconds) return;
        if (retries++ >= localRetryLimit) { Fail("Stuck / retry budget exhausted"); return; }
        OpenNearbyDoor();
        // A short reachable sidestep can clear a crowded lane, then the brain resumes its goal.
        Vector3 side = transform.right * (retries % 2 == 0 ? -1 : 1) * 1.5f;
        if (NavMesh.SamplePosition(transform.position + side, out var hit, 1, agent.areaMask) && Reachable(hit.position))
        {
            agent.SetDestination(hit.position);
            retryUntil = Time.time + 1;
        }
        else agent.SetDestination(requested);
        progressAt = Time.time;
        Status = "Retrying blocked route";
    }

    private void Fail(string reason)
    {
        blockedGoal = requested;
        blockedUntil = Time.time + 3;
        Stop();
        failed = true;
        Status = reason;
    }

    public void Stop()
    {
        if (Ready) { agent.isStopped = true; agent.ResetPath(); }
        moving = false;
        retryUntil = 0;
        Status = "Stopped";
    }

    public void Face(Vector3 target)
    {
        var d = target - transform.position;
        d.y = 0;
        if (d.sqrMagnitude > .01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360 * Time.deltaTime);
    }

    public void OpenNearbyDoor()
    {
        if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out var hit, 1.6f, 1 << 8, QueryTriggerInteraction.Ignore))
        {
            var door = hit.collider.GetComponentInParent<InteractiveDoor>();
            if (door != null) door.OpenForAgent();
        }
    }
}
```


## EnemyPerception

Full file path: D:/GV project/Assets/Scripts/AI/Shared/EnemyPerception.cs

```csharp
using UnityEngine;

/// <summary>Only this sensor reads live player state. Brains consume bounded observations.</summary>
public sealed class EnemyPerception : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float sampleInterval = .2f;
    [SerializeField, Min(1)] private float sightRange = 45;
    [SerializeField, Range(30, 360)] private float fieldOfView = 130;
    [SerializeField, Min(0)] private float closeAwarenessRange = 2.4f;
    [SerializeField, Min(1)] private float memorySeconds = 16;
    [SerializeField, Min(.1f)] private float eyeHeight = 1.5f;
    private EnemyActor actor;
    private CharacterController playerCapsule;
    private float nextSample, heardUntil, lastKnowledgeAt = float.NegativeInfinity;
    private int noiseCursor;
    public float Distance { get; private set; } = float.MaxValue;
    public bool Visible { get; private set; }
    public bool Heard => Time.time < heardUntil;
    public float PlayerWeakness { get; private set; }
    public Vector3 TargetPosition { get; private set; }
    public Vector3 LastKnownPosition { get; private set; }
    public Vector3 ObservedForward { get; private set; }
    public Vector3 ObservedVelocity { get; private set; }
    public WeaponCategory Weapon { get; private set; }
    public bool PlayerFiring { get; private set; }
    public bool PlayerReloading { get; private set; }
    public bool HasMemory { get; private set; }
    public float MemoryRemaining => HasMemory ? Mathf.Max(0, memorySeconds - (Time.time - lastKnowledgeAt)) : 0;
    public int ObservationVersion { get; private set; }
    public string Knowledge => Visible ? "Visible" : HasMemory ? Heard ? "Heard / investigate" : "Last known / search" : "Unaware";

    public void Initialize(EnemyActor value)
    {
        actor = value;
        playerCapsule = actor.World.player != null ? actor.World.player.GetComponent<CharacterController>() : null;
        Sample();
    }

    private void Update()
    {
        if (actor != null && actor.CanThink && Time.time >= nextSample) Sample();
    }

    public void Sample()
    {
        if (actor == null || actor.World == null || actor.World.player == null) return;
        nextSample = Time.time + sampleInterval;
        var world = actor.World;
        Vector3 position = world.player.transform.position;
        Vector3 delta = position - transform.position;
        float distance = delta.magnitude;
        Vector3 planar = Vector3.ProjectOnPlane(delta, Vector3.up);
        bool inView = distance <= closeAwarenessRange || Vector3.Angle(transform.forward, planar) <= fieldOfView * .5f;
        Vector3 targetChest = playerCapsule != null ? playerCapsule.bounds.center : position + Vector3.up;
        bool wasVisible = Visible;
        Visible = !world.player.IsDead && distance <= sightRange && inView &&
            !Physics.Linecast(transform.position + Vector3.up * eyeHeight, targetChest, world.obstructionMask, QueryTriggerInteraction.Ignore);
        bool heardNow = world.TryHear(transform.position, ref noiseCursor, out var noise);
        if (heardNow) heardUntil = Time.time + .8f;
        PlayerFiring = Visible && world.weapons != null && world.weapons.IsFiring;
        PlayerReloading = Visible && world.weapons != null && world.weapons.IsReloading;
        Weapon = Visible && world.weapons != null ? world.weapons.CurrentWeaponCategory : WeaponCategory.None;
        PlayerWeakness = Visible ? 1 - world.player.CurrentHealth / Mathf.Max(1, world.player.MaxHealth) : 0;
        ObservedVelocity = Visible && world.movement != null ? world.movement.PlanarVelocity : Vector3.zero;
        if (Visible)
        {
            ObservedForward = world.player.transform.forward;
            Remember(position, !wasVisible);
        }
        else if (heardNow) Remember(noise.Position, true);
        else if (HasMemory && Time.time - lastKnowledgeAt >= memorySeconds) Forget();
        TargetPosition = HasMemory ? LastKnownPosition : Vector3.zero;
        Distance = HasMemory ? Vector3.Distance(transform.position, LastKnownPosition) : float.MaxValue;
    }

    private void Remember(Vector3 position, bool newEvidence)
    {
        if (!HasMemory || newEvidence || (position - LastKnownPosition).sqrMagnitude > 4) ObservationVersion++;
        LastKnownPosition = position;
        lastKnowledgeAt = Time.time;
        HasMemory = true;
    }

    public void Forget()
    {
        HasMemory = false;
        Visible = false;
        LastKnownPosition = TargetPosition = ObservedVelocity = ObservedForward = Vector3.zero;
        PlayerFiring = PlayerReloading = false;
        PlayerWeakness = 0;
        Weapon = WeaponCategory.None;
        Distance = float.MaxValue;
        ObservationVersion++;
    }
}
```


## EnemyWorld

Full file path: D:/GV project/Assets/Scripts/AI/Shared/EnemyWorld.cs

```csharp
using System.Collections.Generic;
using UnityEngine;

public enum EnemyNoiseKind { Gunshot, Sprint, Explosion }
public struct EnemyNoise
{
    public int Sequence;
    public Vector3 Position;
    public float Radius, Time;
    public EnemyNoiseKind Kind;
}

/// <summary>Scene references, bounded hearing history and shared tactical reservations.</summary>
public sealed class EnemyWorld : MonoBehaviour
{
    public PlayerHealth player;
    public PlayerWeaponController weapons;
    public ThirdPersonPlayerController movement;
    public MapMarker[] tacticalPoints;
    public LayerMask obstructionMask = 1 << 8;
    [Header("Group pressure")]
    [SerializeField, Min(1)] private int maximumAttackers = 2;
    [SerializeField, Min(2)] private float waitingRadius = 3.2f;
    [Header("Hearing")]
    [SerializeField, Min(0)] private float sprintNoiseRadius = 12;
    [SerializeField, Min(.1f)] private float sprintNoiseInterval = .65f;
    private readonly HashSet<EnemyActor> living = new HashSet<EnemyActor>();
    private readonly HashSet<EnemyActor> attackers = new HashSet<EnemyActor>();
    private readonly Dictionary<MapMarker, EnemyActor> reservations = new Dictionary<MapMarker, EnemyActor>();
    private readonly Dictionary<EnemyActor, int> formation = new Dictionary<EnemyActor, int>();
    private readonly EnemyNoise[] noises = new EnemyNoise[32];
    private int noiseSequence, nextFormation;
    private float nextFootstep;
    private PlayerHealth subscribedPlayer;
    private PlayerWeaponController subscribedWeapons;
    public IReadOnlyCollection<EnemyActor> Living => living;
    public WeaponShotInfo LastNoise { get; private set; }
    public bool HasNoise { get; private set; }
    public Vector3 InitialSearchPosition { get; private set; }
    public int ActiveAttackers => attackers.Count;
    public int MaximumAttackers => maximumAttackers;

    private void Awake()
    {
        if (player != null) InitialSearchPosition = player.transform.position;
    }

    private void OnEnable()
    {
        subscribedPlayer = player;
        subscribedWeapons = weapons;
        if (subscribedPlayer != null) subscribedPlayer.Died += StopAll;
        if (subscribedWeapons != null) subscribedWeapons.ShotFired += RememberNoise;
        GrenadeProjectile.Detonated += OnExplosion;
    }

    private void OnDisable()
    {
        if (subscribedPlayer != null) subscribedPlayer.Died -= StopAll;
        if (subscribedWeapons != null) subscribedWeapons.ShotFired -= RememberNoise;
        GrenadeProjectile.Detonated -= OnExplosion;
        StopAll();
        reservations.Clear();
    }

    private void Update()
    {
        if (player == null || player.IsDead || movement == null || Time.timeScale <= 0) return;
        if (movement.IsSprinting && movement.PlanarSpeed > .5f && Time.time >= nextFootstep)
        {
            nextFootstep = Time.time + sprintNoiseInterval;
            ReportNoise(player.transform.position, sprintNoiseRadius, EnemyNoiseKind.Sprint);
        }
    }

    private void RememberNoise(WeaponShotInfo shot)
    {
        LastNoise = shot;
        HasNoise = true;
        ReportNoise(shot.Position, shot.NoiseRadius, EnemyNoiseKind.Gunshot);
    }

    private void OnExplosion(Vector3 position, float radius) => ReportNoise(position, radius, EnemyNoiseKind.Explosion);

    public void ReportNoise(Vector3 position, float radius, EnemyNoiseKind kind)
    {
        int sequence = ++noiseSequence;
        noises[sequence % noises.Length] = new EnemyNoise
        { Sequence = sequence, Position = position, Radius = radius, Time = Time.time, Kind = kind };
    }

    // Examine each event once. A quiet footstep cannot overwrite a still-audible gunshot.
    public bool TryHear(Vector3 listener, ref int cursor, out EnemyNoise heard)
    {
        heard = default;
        float strongest = float.NegativeInfinity;
        for (int i = Mathf.Max(cursor + 1, noiseSequence - noises.Length + 1); i <= noiseSequence; i++)
        {
            var noise = noises[i % noises.Length];
            float distance = Vector3.Distance(listener, noise.Position);
            if (noise.Sequence != i || Time.time - noise.Time > 2 || distance > noise.Radius) continue;
            float strength = noise.Radius - distance;
            if (strength > strongest) { strongest = strength; heard = noise; }
        }
        cursor = noiseSequence;
        return strongest > float.NegativeInfinity;
    }

    public void Register(EnemyActor actor)
    {
        if (living.Add(actor)) formation[actor] = nextFormation++;
    }

    public void Remove(EnemyActor actor)
    {
        living.Remove(actor);
        formation.Remove(actor);
        Release(actor);
        ReleaseAttack(actor);
    }

    public bool HasAttackSlot(EnemyActor actor) => attackers.Contains(actor);
    public bool AttackSlotAvailable(EnemyActor actor) => HasAttackSlot(actor) || attackers.Count < maximumAttackers;
    public bool TryAcquireAttack(EnemyActor actor)
    {
        if (!actor.CanThink || !AttackSlotAvailable(actor)) return false;
        attackers.Add(actor);
        return true;
    }
    public void ReleaseAttack(EnemyActor actor) => attackers.Remove(actor);

    public Vector3 ApproachPosition(EnemyActor actor)
    {
        var senses = actor.Perception;
        if (!senses.Visible || senses.Distance > 8 || (actor.Combat.IsReady && AttackSlotAvailable(actor))) return senses.TargetPosition;
        formation.TryGetValue(actor, out int index);
        float radius = Mathf.Max(waitingRadius, actor.Combat.Range + 1) + (index / 8 % 3) * .8f;
        return senses.TargetPosition + Quaternion.Euler(0, index * 137.508f, 0) * Vector3.forward * radius;
    }

    public int AlliesNear(EnemyActor owner, float range)
    {
        int count = 0;
        foreach (var a in living)
            if (a != null && a != owner && a.CanThink && (a.transform.position - owner.transform.position).sqrMagnitude < range * range) count++;
        return count;
    }

    public void EncourageAdvance(EnemyActor leader, float range, float seconds)
    {
        foreach (var a in living)
            if (a != null && a != leader && a.CanThink && a.Perception.Visible &&
                (a.transform.position - leader.transform.position).sqrMagnitude <= range * range)
                a.EncouragedUntil = Time.time + seconds;
    }

    public bool Available(MapMarker marker, EnemyActor owner) => marker != null &&
        (!reservations.TryGetValue(marker, out var occupied) || occupied == null || occupied == owner);
    public bool Reserve(MapMarker marker, EnemyActor owner)
    {
        if (!Available(marker, owner)) return false;
        Release(owner);
        reservations[marker] = owner;
        return true;
    }

    public void Release(EnemyActor owner)
    {
        MapMarker found = null;
        foreach (var pair in reservations)
            if (pair.Value == owner) { found = pair.Key; break; }
        if (found != null) reservations.Remove(found);
    }

    private void StopAll()
    {
        foreach (var a in living)
            if (a != null) { a.Movement.Stop(); a.Combat.Cancel(); }
        attackers.Clear();
    }
}
```


## CombatEffects

Full file path: D:/GV project/Assets/Scripts/Core/CombatEffects.cs

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>Small reusable particle pool. Wave and weapon events provide the effect locations.</summary>
public sealed class CombatEffects : MonoBehaviour
{
    [SerializeField]
    private PlayerWeaponController weapons;
    [SerializeField]
    private WaveManager waves;
    [SerializeField]
    private ParticleSystem impactPrefab;
    [SerializeField]
    private ParticleSystem hitPrefab;
    [SerializeField]
    private ParticleSystem deathPrefab;
    [SerializeField, Range(4, 32)]
    private int poolSize = 16;
    private ParticleSystem[] impacts, hits, deaths;
    private int impactIndex, hitIndex, deathIndex;
    private readonly Dictionary<EnemyHealth, EnemyActor> tracked = new Dictionary<EnemyHealth, EnemyActor>();
    private readonly Dictionary<EnemyHealth, System.Action<float>> hitListeners = new Dictionary<EnemyHealth, System.Action<float>>();
    private void Awake()
    {
        impacts = MakePool(impactPrefab);
        hits = MakePool(hitPrefab);
        deaths = MakePool(deathPrefab);
    }

    private ParticleSystem[] MakePool(ParticleSystem prefab)
    {
        var pool = new ParticleSystem[poolSize];
        if (prefab != null)
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = Instantiate(prefab, transform);
                pool[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

        return pool;
    }

    private void OnEnable()
    {
        weapons.HitDetected += OnImpact;
        waves.EnemySpawned += Track;
    }

    private void OnDisable()
    {
        weapons.HitDetected -= OnImpact;
        waves.EnemySpawned -= Track;
        foreach (var pair in tracked)
            if (pair.Key != null)
            {
                pair.Key.Died -= OnDeath;
                pair.Key.Hit -= hitListeners[pair.Key];
            }

        tracked.Clear();
        hitListeners.Clear();
    }

    private void OnImpact(RaycastHit hit, float damage) => Play(impacts, ref impactIndex, hit.point + hit.normal * .03f);
    private void Track(EnemyActor actor)
    {
        if (tracked.ContainsKey(actor.Health))
            return;
        tracked[actor.Health] = actor;
        System.Action<float> listener = _ => Play(hits, ref hitIndex, actor.transform.position + Vector3.up);
        hitListeners[actor.Health] = listener;
        actor.Health.Hit += listener;
        actor.Health.Died += OnDeath;
    }

    private void OnDeath(EnemyHealth health)
    {
        if (!tracked.TryGetValue(health, out var actor))
            return;
        Play(deaths, ref deathIndex, actor.transform.position + Vector3.up * .7f);
        health.Hit -= hitListeners[health];
        health.Died -= OnDeath;
        hitListeners.Remove(health);
        tracked.Remove(health);
    }

    private static void Play(ParticleSystem[] pool, ref int index, Vector3 position)
    {
        if (pool == null || pool.Length == 0 || pool[index] == null)
            return;
        var effect = pool[index];
        index = (index + 1) % pool.Length;
        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.transform.position = position;
        effect.Play(true);
    }
}
```


## IDamageable

Full file path: D:/GV project/Assets/Scripts/Core/IDamageable.cs

```csharp
/// <summary>A collider or one of its parents may implement this to receive hitscan damage.</summary>
public interface IDamageable
{
    void TakeDamage(float amount);
}
```


## IInteractable

Full file path: D:/GV project/Assets/Scripts/Core/IInteractable.cs

```csharp
using UnityEngine;

/// <summary>Contract for future world objects the player can interact with.</summary>
public interface IInteractable
{
    string InteractionPrompt { get; }

    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
}
```


## CinematicCityCamera

Full file path: D:/GV project/Assets/Scripts/Environment/CinematicCityCamera.cs

```csharp
using UnityEngine;

/// <summary>
/// Built-in renderer presentation: linear HDR bloom, restrained height haze,
/// filmic highlight roll-off and optional edge antialiasing. Does not affect gameplay.
/// Keep this after contact occlusion and disable other grading/tonemapping effects.
/// </summary>
[ExecuteAlways, RequireComponent(typeof(Camera)), DisallowMultipleComponent]
public sealed class CinematicCityCamera : MonoBehaviour
{
    [SerializeField] private Shader presentationShader;
    [Header("Exposure and colour")]
    [SerializeField, Range(-3f, 3f)] private float exposureEV = .2f;
    [SerializeField, Range(0f, 1.5f)] private float saturation = .94f;
    [SerializeField, Range(.75f, 1.25f)] private float contrast = 1.02f;
    [SerializeField, Range(0f, .3f)] private float vignette = .035f;
    [Header("HDR bloom")]
    [SerializeField, Range(0f, .3f)] private float bloomStrength = .055f;
    [SerializeField, Range(.3f, 4f)] private float bloomThreshold = 1.1f;
    [SerializeField, Range(.05f, 1f)] private float bloomSoftKnee = .5f;
    [Header("Atmospheric depth")]
    [SerializeField] private Color hazeColour = new Color(.53f, .60f, .64f, 1f);
    [SerializeField, Range(0f, .04f)] private float hazeDensity = .0035f;
    [SerializeField] private float hazeBaseHeight = .6f;
    [SerializeField, Range(.01f, 1f)] private float hazeHeightFalloff = .12f;
    [SerializeField, Range(0f, .8f)] private float maximumHaze = .28f;
    [Header("Edges")]
    [SerializeField] private bool edgeAntialiasing = true;
    [SerializeField, Range(.03f, .2f)] private float edgeThreshold = .09f;

    private Camera attachedCamera;
    private Material effect;

    public void Configure(Shader shader) { presentationShader = shader; }

    private void OnEnable()
    {
        attachedCamera = GetComponent<Camera>();
        attachedCamera.depthTextureMode |= DepthTextureMode.Depth;
        if (presentationShader == null)
            presentationShader = Shader.Find("Hidden/Outbreak/Cinematic City");
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (presentationShader == null || !presentationShader.isSupported || attachedCamera == null)
        {
            Graphics.Blit(source, destination);
            return;
        }
        if (effect != null && effect.shader != presentationShader) ReleaseMaterial();
        if (effect == null)
            effect = new Material(presentationShader) { hideFlags = HideFlags.HideAndDontSave };

        effect.SetVector("_Grade", new Vector4(Mathf.Pow(2f, exposureEV), saturation, contrast, vignette));
        effect.SetVector("_BloomSettings", new Vector4(bloomThreshold, bloomSoftKnee, bloomStrength, 0f));
        // Use a vector to avoid any implicit shader colour-property conversion.
        // The fog colour enters the composition in linear space in either project mode.
        Color linearHaze = hazeColour.linear;
        effect.SetVector("_HazeColour", new Vector4(linearHaze.r, linearHaze.g, linearHaze.b, linearHaze.a));
        effect.SetVector("_HazeSettings", new Vector4(hazeDensity, hazeBaseHeight, hazeHeightFalloff, maximumHaze));
        effect.SetMatrix("_InverseProjection", attachedCamera.projectionMatrix.inverse);
        effect.SetMatrix("_CityViewToWorld", attachedCamera.cameraToWorldMatrix);
        effect.SetVector("_CameraPosition", attachedCamera.transform.position);
        effect.SetFloat("_EdgeThreshold", edgeThreshold);

        RenderTexture bloomA = null, bloomB = null, graded = null;
        RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
            ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.Default;
        try
        {
            if (bloomStrength > .0001f)
            {
                int width = Mathf.Max(1, source.width / 2), height = Mathf.Max(1, source.height / 2);
                bloomA = RenderTexture.GetTemporary(width, height, 0, format, RenderTextureReadWrite.Linear);
                bloomB = RenderTexture.GetTemporary(width, height, 0, format, RenderTextureReadWrite.Linear);
                bloomA.filterMode = bloomB.filterMode = FilterMode.Bilinear;
                bloomA.wrapMode = bloomB.wrapMode = TextureWrapMode.Clamp;
                Graphics.Blit(source, bloomA, effect, 0);
                Graphics.Blit(bloomA, bloomB, effect, 1);
                Graphics.Blit(bloomB, bloomA, effect, 2);
                effect.SetTexture("_BloomTex", bloomA);
            }
            else effect.SetTexture("_BloomTex", Texture2D.blackTexture);

            if (edgeAntialiasing)
            {
                graded = RenderTexture.GetTemporary(source.width, source.height, 0, format, RenderTextureReadWrite.Linear);
                graded.filterMode = FilterMode.Bilinear;
                graded.wrapMode = TextureWrapMode.Clamp;
                Graphics.Blit(source, graded, effect, 3);
                Graphics.Blit(graded, destination, effect, 4);
            }
            else Graphics.Blit(source, destination, effect, 3);
        }
        finally
        {
            if (bloomA != null) RenderTexture.ReleaseTemporary(bloomA);
            if (bloomB != null) RenderTexture.ReleaseTemporary(bloomB);
            if (graded != null) RenderTexture.ReleaseTemporary(graded);
        }
    }

    private void ReleaseMaterial()
    {
        if (effect == null) return;
        if (Application.isPlaying) Destroy(effect); else DestroyImmediate(effect);
        effect = null;
    }

    private void OnDisable()
    {
        ReleaseMaterial();
        // Other camera effects may share the depth texture; retain the shared request.
    }
}
```


## CityCameraAtmosphere

Full file path: D:/GV project/Assets/Scripts/Environment/CityCameraAtmosphere.cs

```csharp
using UnityEngine;

/// <summary>Lightweight Built-in pipeline grading and quarter-resolution bloom. No gameplay dependencies.</summary>
[ExecuteAlways, RequireComponent(typeof(Camera)), DisallowMultipleComponent]
public sealed class CityCameraAtmosphere : MonoBehaviour
{
    [SerializeField] private Shader atmosphereShader;
    [SerializeField, Range(.5f,2)] private float exposure=1.06f;
    [SerializeField, Range(0,1.5f)] private float saturation=.85f;
    [SerializeField, Range(.5f,1.5f)] private float contrast=1.035f;
    [SerializeField, Range(0,.5f)] private float vignette=.15f;
    [SerializeField, Range(0,.8f)] private float bloom=.16f;
    private Material effect;

    public void Configure(Shader shader) { atmosphereShader=shader; }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if(atmosphereShader==null || !atmosphereShader.isSupported) { Graphics.Blit(source,destination); return; }
        if(effect==null) effect=new Material(atmosphereShader) { hideFlags=HideFlags.HideAndDontSave };
        var small=RenderTexture.GetTemporary(Mathf.Max(1,source.width/4),Mathf.Max(1,source.height/4),0,source.format);
        var blurred=RenderTexture.GetTemporary(small.width,small.height,0,source.format);
        try
        {
            Graphics.Blit(source,small,effect,0);
            Graphics.Blit(small,blurred,effect,1);
            effect.SetTexture("_BloomTex",blurred);
            effect.SetVector("_Grade",new Vector4(exposure,saturation,contrast,vignette));
            effect.SetFloat("_Bloom",bloom);
            Graphics.Blit(source,destination,effect,2);
        }
        finally { RenderTexture.ReleaseTemporary(small); RenderTexture.ReleaseTemporary(blurred); }
    }

    private void OnDisable()
    {
        if(effect==null)return;
        if(Application.isPlaying)Destroy(effect);else DestroyImmediate(effect);
        effect=null;
    }
}
```


## CityContactShadows

Full file path: D:/GV project/Assets/Scripts/Environment/CityContactShadows.cs

```csharp
using UnityEngine;

/// <summary>Half-resolution contact occlusion with depth-aware blur for the Built-in pipeline.</summary>
[ExecuteAlways, RequireComponent(typeof(Camera)), DisallowMultipleComponent]
public sealed class CityContactShadows : MonoBehaviour
{
    [SerializeField] private Shader shader;
    [SerializeField, Range(.1f,1.5f)] private float radius=.55f;
    [SerializeField, Range(0,3)] private float strength=1.15f;
    private Camera cameraComponent;
    private Material material;
    public void Configure(Shader value){shader=value;}
    private void OnEnable()
    {
        cameraComponent=GetComponent<Camera>();
        cameraComponent.depthTextureMode|=DepthTextureMode.DepthNormals;
    }
    private void OnRenderImage(RenderTexture source,RenderTexture destination)
    {
        if(shader==null||!shader.isSupported||SystemInfo.graphicsShaderLevel<30){Graphics.Blit(source,destination);return;}
        if(material==null)material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        material.SetMatrix("_InverseProjection",cameraComponent.projectionMatrix.inverse);
        material.SetVector("_Settings",new Vector4(radius,strength,cameraComponent.farClipPlane,Mathf.Tan(cameraComponent.fieldOfView*Mathf.Deg2Rad*.5f)));
        int w=Mathf.Max(1,source.width/2),h=Mathf.Max(1,source.height/2);
        var raw=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var smooth=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        try
        {
            raw.filterMode=smooth.filterMode=FilterMode.Bilinear;
            Graphics.Blit(source,raw,material,0);
            Graphics.Blit(raw,smooth,material,1);
            material.SetTexture("_OcclusionTex",smooth);
            Graphics.Blit(source,destination,material,2);
        }
        finally{RenderTexture.ReleaseTemporary(raw);RenderTexture.ReleaseTemporary(smooth);}
    }
    private void OnDisable()
    {
        if(material==null)return;
        if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);
        material=null;
        // Another camera effect may also require depth normals; leave the shared flag intact.
    }
}
```


## DestructibleBarricade

Full file path: D:/GV project/Assets/Scripts/Environment/DestructibleBarricade.cs

```csharp
using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
public sealed class DestructibleBarricade : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1)]
    private float maxHealth = 100;
    [SerializeField]
    private GameObject debrisPrefab;
    public float Health { get; private set; }
    public bool IsDestroyed { get; private set; }

    public event Action Destroyed;
    private void Awake() => Health = maxHealth;
    public void TakeDamage(float amount)
    {
        if (amount <= 0 || IsDestroyed)
            return;
        Health = Mathf.Max(0, Health - amount);
        if (Health > 0)
            return;
        IsDestroyed = true;
        var obstacle = GetComponent<NavMeshObstacle>();
        if (obstacle != null)
            obstacle.enabled = false;
        if (debrisPrefab != null)
            Instantiate(debrisPrefab, transform.position, transform.rotation);
        else
        {
            // The intact prop falls over visibly, then clears the route.
            var body = GetComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.None;
            body.AddTorque(transform.right * 80, ForceMode.Impulse);
        }

        Destroyed?.Invoke();
        Destroy(gameObject, 2f);
    }
}
```


## FlickeringLight

Full file path: D:/GV project/Assets/Scripts/Environment/FlickeringLight.cs

```csharp
using UnityEngine;

[RequireComponent(typeof(Light))]
public sealed class FlickeringLight : MonoBehaviour
{
    [SerializeField]
    private float minimum = .6f;
    [SerializeField]
    private float maximum = 1.3f;
    private Light lamp;
    private float next;
    private void Awake() => lamp = GetComponent<Light>();
    private void Update()
    {
        if (Time.time < next)
            return;
        next = Time.time + Random.Range(.08f, .35f);
        lamp.intensity = Random.Range(minimum, maximum);
    }
}
```


## InteractiveDoor

Full file path: D:/GV project/Assets/Scripts/Environment/InteractiveDoor.cs

```csharp
using UnityEngine;

/// <summary>Hinged door shared by player interaction and nearby enemy traversal.</summary>
public sealed class InteractiveDoor : MonoBehaviour, IInteractable
{
    [SerializeField]
    private float openAngle = 100f;
    [SerializeField, Min(10)]
    private float degreesPerSecond = 140f;
    [SerializeField]
    private bool locked;
    private Quaternion closedRotation;
    public bool IsOpen { get; private set; }
    public bool IsLocked => locked;
    public string InteractionPrompt => locked ? "LOCKED" : IsOpen ? "E  CLOSE DOOR" : "E  OPEN DOOR";

    private void Awake() => closedRotation = transform.localRotation;
    public bool CanInteract(GameObject interactor) => !locked;
    public void Interact(GameObject interactor)
    {
        if (!locked)
            IsOpen = !IsOpen;
    }

    public void OpenForAgent()
    {
        if (!locked)
            IsOpen = true;
    }

    private void Update()
    {
        var target = closedRotation * Quaternion.Euler(0, IsOpen ? openAngle : 0, 0);
        transform.localRotation = Quaternion.RotateTowards(transform.localRotation, target, degreesPerSecond * Time.deltaTime);
    }
}
```


## MapMarker

Full file path: D:/GV project/Assets/Scripts/Environment/MapMarker.cs

```csharp
using UnityEngine;

public enum MapMarkerKind
{
    PlayerSpawn,
    ZombieEntrance,
    Ambush,
    Cover,
    EliteArena
}

/// <summary>Authored positions consumed by spawning and tactical decisions.</summary>
public sealed class MapMarker : MonoBehaviour
{
    public MapMarkerKind kind;
    public float radius = 1.5f;
    private void OnDrawGizmos()
    {
        Gizmos.color = kind == MapMarkerKind.ZombieEntrance ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.DrawRay(transform.position, transform.forward * 2);
    }
}
```


## ShootingTarget

Full file path: D:/GV project/Assets/Scripts/Environment/ShootingTarget.cs

```csharp
using UnityEngine;

/// <summary>Stationary greybox damage receiver for the Phase 3 practice range.</summary>
public sealed class ShootingTarget : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)]
    private float maxHealth = 200f;
    [SerializeField, Min(0.1f)]
    private float resetDelay = 3f;
    [SerializeField]
    private Renderer targetRenderer;
    [SerializeField]
    private bool logHits = true;
    private MaterialPropertyBlock properties;
    private float resetAt = float.PositiveInfinity;
    public float CurrentHealth { get; private set; }

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();
        properties = new MaterialPropertyBlock();
        ResetTarget();
    }

    public void TakeDamage(float amount)
    {
        if (CurrentHealth <= 0f || amount <= 0f)
            return;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        SetColor(CurrentHealth <= 0f ? Color.red : new Color(0.8f, 0.55f, 0.2f));
        if (logHits)
            Debug.Log($"{name}: {amount:0.##} damage, {CurrentHealth:0.##}/{maxHealth:0.##} HP", this);
        if (CurrentHealth <= 0f)
            resetAt = Time.time + resetDelay;
    }

    private void Update()
    {
        if (Time.time >= resetAt)
            ResetTarget();
    }

    private void ResetTarget()
    {
        CurrentHealth = maxHealth;
        resetAt = float.PositiveInfinity;
        SetColor(new Color(0.6f, 0.65f, 0.7f));
    }

    private void SetColor(Color color)
    {
        if (targetRenderer == null)
            return;
        targetRenderer.GetPropertyBlock(properties);
        properties.SetColor("_Color", color);
        properties.SetColor("_BaseColor", color);
        targetRenderer.SetPropertyBlock(properties);
    }
}
```


## TeamArtCatalog

Full file path: D:/GV project/Assets/Scripts/Environment/TeamArtCatalog.cs

```csharp
using UnityEngine;

/// <summary>
/// Optional handoff for original team models. Empty fields keep the playable greybox in place.
/// This asset records references only; imported or generated art must keep its real authorship.
/// </summary>
[CreateAssetMenu(menuName = "Zombie Outbreak/Team Art Catalog")]
public sealed class TeamArtCatalog : ScriptableObject
{
    [Header("Original humanoid model")]
    public GameObject zombieVisualPrefab;
    [Header("Original defensive barricade")]
    public GameObject barricadeVisualPrefab;
    public GameObject brokenDebrisPrefab;
    [Header("Source and contribution record")]
    public string authorOrTeam = "Unassigned";
    public string sourceProjectPath = "Assets/Models/Team/";
    public bool modelsAuthoredByTeam;
}
```


## TransientLight

Full file path: D:/GV project/Assets/Scripts/Environment/TransientLight.cs

```csharp
using UnityEngine;

[RequireComponent(typeof(Light))]
public sealed class TransientLight : MonoBehaviour
{
    [SerializeField, Min(.01f)]
    private float duration = .18f;
    private Light lamp;
    private float intensity, started;
    private void Awake()
    {
        lamp = GetComponent<Light>();
        intensity = lamp.intensity;
        started = Time.time;
    }

    private void Update()
    {
        float fraction = (Time.time - started) / duration;
        lamp.intensity = intensity * Mathf.Clamp01(1 - fraction);
        if (fraction >= 1)
        {
            lamp.enabled = false;
            enabled = false;
        }
    }
}
```


## GrenadeProjectile

Full file path: D:/GV project/Assets/Scripts/Inventory/GrenadeProjectile.cs

```csharp
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public sealed class GrenadeProjectile : MonoBehaviour
{
    public static event System.Action<Vector3, float> Detonated;
    [SerializeField, Min(.1f)]
    private float fuse = 2.5f;
    [SerializeField, Min(.1f)]
    private float radius = 6;
    [SerializeField, Min(0)]
    private float damage = 100;
    [SerializeField, Min(0)]
    private float explosionForce = 850;
    [SerializeField]
    private ParticleSystem explosionEffect;
    [SerializeField]
    private AudioClip explosionAudio;
    private float detonateAt;
    private bool exploded;
    private readonly Collider[] buffer = new Collider[128];
    private readonly HashSet<EnemyHealth> damaged = new HashSet<EnemyHealth>();
    private readonly HashSet<Rigidbody> bodies = new HashSet<Rigidbody>();
    private void Awake() => detonateAt = Time.time + fuse;
    public void Launch(GameObject owner, Vector3 velocity)
    {
        var own = GetComponent<Collider>();
        foreach (var c in owner.GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(own, c);
        GetComponent<Rigidbody>().linearVelocity = velocity;
    }

    private void Update()
    {
        if (Time.time >= detonateAt)
            Explode();
    }

    public void Explode()
    {
        if (exploded)
            return;
        exploded = true;
        Detonated?.Invoke(transform.position, radius * 7);
        GetComponent<Collider>().enabled = false;
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, buffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Collider[] hits = buffer;
        if (count == buffer.Length)
        {
            hits = Physics.OverlapSphere(transform.position, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        for (int i = 0; i < count; i++)
        {
            var c = hits[i];
            var enemy = c.GetComponentInParent<EnemyHealth>();
            bool occluded = Physics.Linecast(transform.position, c.bounds.center, 1 << 8, QueryTriggerInteraction.Ignore);
            if (enemy != null && !occluded && damaged.Add(enemy))
                enemy.TakeDamage(damage * Mathf.Lerp(1, .25f, Mathf.Clamp01(Vector3.Distance(transform.position, c.bounds.center) / radius)));
            var barricade = c.GetComponentInParent<DestructibleBarricade>();
            if (barricade != null)
                barricade.TakeDamage(damage);
            var body = c.attachedRigidbody;
            if (body != null && !body.isKinematic && bodies.Add(body))
                body.AddExplosionForce(explosionForce, transform.position, radius, 1, ForceMode.Impulse);
        }

        if (explosionEffect != null)
        {
            var effect = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, 5);
        }

        if (explosionAudio != null)
            AudioSource.PlayClipAtPoint(explosionAudio, transform.position, .7f);
        Destroy(gameObject);
    }
}
```


## LoadoutCatalog

Full file path: D:/GV project/Assets/Scripts/Inventory/LoadoutCatalog.cs

```csharp
using System;
using UnityEngine;

[Serializable]
public sealed class EquipmentPack
{
    public string name;
    [Min(0)]
    public int medkits;
    [Min(0)]
    public int grenades;
}

[CreateAssetMenu(menuName = "Zombie Outbreak/Loadout Catalog")]
public sealed class LoadoutCatalog : ScriptableObject
{
    public WeaponData[] primaries;
    public WeaponData pistol;
    public EquipmentPack[] equipment;
    public bool Valid(int primary, int pack)
    {
        if (primaries == null || equipment == null || primary < 0 || primary >= primaries.Length || pack < 0 || pack >= equipment.Length || pistol == null)
            return false;
        var weapon = primaries[primary];
        var selected = equipment[pack];
        return weapon != null && weapon.WeaponPrefab != null && pistol.WeaponPrefab != null && pistol.Category == WeaponCategory.Pistol && (weapon.Category == WeaponCategory.Rifle || weapon.Category == WeaponCategory.SMG || weapon.Category == WeaponCategory.Shotgun) && selected != null && selected.medkits >= 0 && selected.grenades >= 0;
    }
}
```


## LoadoutSession

Full file path: D:/GV project/Assets/Scripts/Inventory/LoadoutSession.cs

```csharp
using UnityEngine;

/// <summary>The only persistent session object; stores selection, never runtime ammo or enemy state.</summary>
public sealed class LoadoutSession : MonoBehaviour
{
    public static LoadoutSession Instance { get; private set; }
    public LoadoutCatalog Catalog { get; private set; }
    public int Primary { get; private set; }
    public int Equipment { get; private set; }
    public bool IsValid => Catalog != null && Catalog.Valid(Primary, Equipment);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static LoadoutSession Ensure(LoadoutCatalog catalog)
    {
        if (Instance == null)
            new GameObject("LoadoutSession").AddComponent<LoadoutSession>();
        if (Instance.Catalog != catalog)
        {
            Instance.Catalog = catalog;
            Instance.Primary = 0;
            Instance.Equipment = 0;
        }

        return Instance;
    }

    public bool Select(int primary, int equipment)
    {
        if (Catalog == null || !Catalog.Valid(primary, equipment))
            return false;
        Primary = primary;
        Equipment = equipment;
        return true;
    }
}
```


## CharacterMotionPolish

Full file path: D:/GV project/Assets/Scripts/Player/CharacterMotionPolish.cs

```csharp
using UnityEngine;

/// <summary>Small additive torso inertia and breathing after animation, before weapon/hand IK.
/// Never changes the root, pelvis, legs, movement speed, or combat timing.</summary>
[DefaultExecutionOrder(130), RequireComponent(typeof(Animator)), DisallowMultipleComponent]
public sealed class CharacterMotionPolish : MonoBehaviour
{
    [SerializeField, Range(0, 1)] private float amount = .8f;
    [SerializeField, Range(0, 1)] private float breathing = .45f;
    private Animator animator;
    private Transform chest, spine, owner;
    private ThirdPersonPlayerController player;
    private PlayerHealth playerHealth;
    private PlayerAimController aim;
    private PlayerWeaponController weapons;
    private EnemyMovement enemy;
    private EnemyHealth enemyHealth;
    private EnemyCombat combat;
    private Vector3 lastVelocity, smoothedAcceleration;
    private float lastYaw, turn, phase;
    private Quaternion chestBase, spineBase;
    private bool applied, initialized;

    private void Awake()
    {
        animator=GetComponent<Animator>();
        if(!animator.isHuman){enabled=false;return;}
        chest=animator.GetBoneTransform(HumanBodyBones.Chest);
        spine=animator.GetBoneTransform(HumanBodyBones.Spine);
        player=GetComponentInParent<ThirdPersonPlayerController>();
        playerHealth=GetComponentInParent<PlayerHealth>();aim=GetComponentInParent<PlayerAimController>();
        weapons=GetComponentInParent<PlayerWeaponController>();
        enemy=GetComponentInParent<EnemyMovement>();enemyHealth=GetComponentInParent<EnemyHealth>();combat=GetComponentInParent<EnemyCombat>();
        owner=player!=null?player.transform:enemy!=null?enemy.transform:transform;
        phase=Mathf.Abs(GetEntityId().GetHashCode()%97)*.17f;
        lastYaw=owner.eulerAngles.y;
    }

    private void Update()=>RestorePose();

    private void LateUpdate()
    {
        float dt=Time.deltaTime;
        if(dt<=0||chest==null||spine==null||!animator.enabled)return;
        bool inactive=(playerHealth!=null&&playerHealth.IsDead)||(enemyHealth!=null&&enemyHealth.IsDead)||
            (player!=null&&!player.IsGrounded)||(weapons!=null&&weapons.IsReloading)||(combat!=null&&combat.IsAttacking);
        Vector3 velocity=player!=null?player.PlanarVelocity:enemy!=null?enemy.Velocity:Vector3.zero;
        if(!initialized){lastVelocity=velocity;lastYaw=owner.eulerAngles.y;initialized=true;}
        Vector3 acceleration=Vector3.ClampMagnitude((velocity-lastVelocity)/Mathf.Max(.016f,dt),18);
        lastVelocity=velocity;
        float yaw=owner.eulerAngles.y;
        float yawRate=Mathf.Clamp(Mathf.DeltaAngle(lastYaw,yaw)/Mathf.Max(.016f,dt),-180,180);
        lastYaw=yaw;
        float blend=1-Mathf.Exp(-9*dt);
        smoothedAcceleration=Vector3.Lerp(smoothedAcceleration,inactive?Vector3.zero:acceleration,blend);
        turn=Mathf.Lerp(turn,inactive?0:yawRate,blend);
        if(inactive)return;
        float weight=amount*(aim!=null&&aim.IsAiming?.22f:1);
        Vector3 local=owner.InverseTransformDirection(smoothedAcceleration);
        float pitch=Mathf.Clamp(local.z*.22f,-3,4)*weight;
        float roll=Mathf.Clamp(-local.x*.17f-turn*.012f,-3,3)*weight;
        float breath=Mathf.Sin(Time.time*1.65f+phase)*breathing*Mathf.Lerp(1,.3f,Mathf.Clamp01(velocity.magnitude/2));
        chestBase=chest.localRotation;spineBase=spine.localRotation;
        spine.rotation=Quaternion.AngleAxis(pitch*.4f,owner.right)*Quaternion.AngleAxis(roll*.4f,owner.forward)*spine.rotation;
        chest.rotation=Quaternion.AngleAxis(pitch*.6f+breath,owner.right)*Quaternion.AngleAxis(roll*.6f,owner.forward)*chest.rotation;
        applied=true;
    }

    private void RestorePose()
    {
        if(!applied)return;
        if(spine!=null)spine.localRotation=spineBase;
        if(chest!=null)chest.localRotation=chestBase;
        applied=false;
    }
    private void OnDisable(){RestorePose();initialized=false;smoothedAcceleration=Vector3.zero;turn=0;}
}
```


## FirstPersonArms

Full file path: D:/GV project/Assets/Scripts/Player/FirstPersonArms.cs

```csharp
using UnityEngine;

/// <summary>Positions the existing Survivalist FPS arm mesh at the current weapon grips.</summary>
[DefaultExecutionOrder(350), DisallowMultipleComponent]
public sealed class FirstPersonArms : MonoBehaviour
{
    [System.Serializable]
    public sealed class Arm
    {
        public Transform upper, lower, hand;
        public Quaternion wristRotation;
        [System.NonSerialized] public Quaternion upperRest, lowerRest;
    }
    [SerializeField] private Arm left = new Arm();
    [SerializeField] private Arm right = new Arm();
    private PlayerWeaponController weapons;
    private FirstPersonWeaponRig rig;

    public void Bind(PlayerWeaponController source, FirstPersonWeaponRig presentation)
    { weapons = source; rig = presentation; }

    private void Awake()
    {
        Cache(left); Cache(right);
    }
    private static void Cache(Arm arm)
    {
        if (arm.upper == null || arm.lower == null) return;
        arm.upperRest = arm.upper.localRotation; arm.lowerRest = arm.lower.localRotation;
    }
    private void LateUpdate()
    {
        if (weapons == null || weapons.ActiveView == null || Time.deltaTime <= 0) return;
        var view = weapons.ActiveView;
        Vector3 rightTarget = view.transform.TransformPoint(new Vector3(0, -.015f, -.025f));
        Vector3 leftTarget = view.SupportHand != null ? view.SupportHand.position : view.transform.TransformPoint(new Vector3(-.04f, -.025f, .1f));
        if (weapons.IsReloading && rig != null)
        {
            float amount = Mathf.Sin(Mathf.Clamp01((rig.ReloadProgress - .08f) / .84f) * Mathf.PI);
            leftTarget = Vector3.Lerp(leftTarget, view.transform.TransformPoint(new Vector3(-.045f, -.15f, .1f)), amount);
        }
        Solve(right, rightTarget, view.transform.rotation, new Vector3(.7f, -.7f, -.15f));
        Solve(left, leftTarget, view.transform.rotation, new Vector3(-.7f, -.7f, -.15f));
    }

    private void Solve(Arm arm, Vector3 target, Quaternion weaponRotation, Vector3 pole)
    {
        if (arm.upper == null || arm.lower == null || arm.hand == null) return;
        arm.upper.localRotation = arm.upperRest; arm.lower.localRotation = arm.lowerRest;
        Vector3 start = arm.upper.position;
        float a = Vector3.Distance(start, arm.lower.position), b = Vector3.Distance(arm.lower.position, arm.hand.position);
        if (a < .001f || b < .001f) return;
        Vector3 delta = target - start;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .002f);
        Vector3 direction = delta.sqrMagnitude > .00001f ? delta.normalized : transform.forward;
        Vector3 bend = Vector3.ProjectOnPlane(transform.TransformDirection(pole), direction).normalized;
        if (bend.sqrMagnitude < .001f) bend = transform.right;
        float along = (a * a + distance * distance - b * b) / (2 * distance);
        Vector3 elbow = start + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
        arm.upper.rotation = Quaternion.FromToRotation(arm.lower.position - start, elbow - start) * arm.upper.rotation;
        arm.lower.rotation = Quaternion.FromToRotation(arm.hand.position - arm.lower.position, start + direction * distance - arm.lower.position) * arm.lower.rotation;
        arm.hand.rotation = weaponRotation * arm.wristRotation;
    }
}
```


## FirstPersonWeaponRig

Full file path: D:/GV project/Assets/Scripts/Player/FirstPersonWeaponRig.cs

```csharp
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Camera-relative weapon presentation; the existing weapon controller owns shots and ammo.</summary>
[DefaultExecutionOrder(160), DisallowMultipleComponent]
[RequireComponent(typeof(PlayerWeaponController), typeof(ThirdPersonPlayerController))]
public sealed class FirstPersonWeaponRig : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Camera weaponCamera;
    [SerializeField] private Transform viewRoot;
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private FirstPersonArms arms;
    [SerializeField] private Renderer[] bodyRenderers = System.Array.Empty<Renderer>();
    [SerializeField, Range(0, 31)] private int viewLayer = 30;
    [SerializeField, Min(0)] private float swayAmount = .45f;
    [SerializeField, Min(0)] private float bobAmount = .004f;
    [SerializeField, Min(.01f)] private float poseResponse = 14f;
    private PlayerWeaponController weapons;
    private ThirdPersonPlayerController movement;
    private PlayerAimController aim;
    private PlayerHealth health;
    private ThirdPersonCameraController cameraRig;
    private ShadowCastingMode[] originalShadows;
    private WeaponHitscan cover;
    private Vector3 position, angles;
    private float recoil, phase, reloadStart, obstruction, equip;
    private bool reloading, initialized;
    public bool IsSprinting => movement != null && movement.IsSprinting;
    public float ReloadProgress => reloading && weapons.CurrentWeapon != null ?
        Mathf.Clamp01((Time.time - reloadStart) / weapons.CurrentWeapon.ReloadTime) : 0f;

    private void Awake()
    {
        weapons = GetComponent<PlayerWeaponController>();
        movement = GetComponent<ThirdPersonPlayerController>();
        aim = GetComponent<PlayerAimController>(); health = GetComponent<PlayerHealth>();
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera != null) cameraRig = worldCamera.GetComponent<ThirdPersonCameraController>();
        if (worldCamera == null || weaponCamera == null || viewRoot == null || weaponSocket == null || arms == null)
        {
            Debug.LogError("FirstPersonWeaponRig needs its camera, socket and arms references.", this);
            enabled = false; return;
        }
        cover = new WeaponHitscan(transform, Physics.DefaultRaycastLayers & ~(1 << viewLayer));
        arms.Bind(weapons, this);
        originalShadows = new ShadowCastingMode[bodyRenderers.Length];
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] == null) continue;
            originalShadows[i] = bodyRenderers[i].shadowCastingMode;
        }
    }

    private void OnEnable()
    {
        if (weapons == null) return;
        weapons.ShotFired += OnShot;
        weapons.WeaponChanged += OnWeaponChanged;
        weapons.ReloadStateChanged += OnReloadChanged;
        for (int i = 0; i < bodyRenderers.Length; i++)
            if (bodyRenderers[i] != null) bodyRenderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        if (weaponCamera != null) weaponCamera.enabled = true;
    }

    public void ConfigureView(WeaponView view)
    {
        foreach (var child in view.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = viewLayer;
        foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
        { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
    }

    private void OnShot(WeaponShotInfo shot) => recoil = Mathf.Min(.065f, recoil + .025f);
    private void OnWeaponChanged(WeaponData data) { equip = .1f; recoil = 0; reloading = false; }
    private void OnReloadChanged(bool value) { reloading = value; reloadStart = Time.time; }

    private void LateUpdate()
    {
        if (worldCamera == null || viewRoot == null) return;
        bool visible = health != null && !health.IsDead;
        viewRoot.gameObject.SetActive(visible);
        weaponCamera.enabled = visible;
        if (!visible || Time.deltaTime <= 0f) return;
        viewRoot.SetPositionAndRotation(worldCamera.transform.position, worldCamera.transform.rotation);
        weaponCamera.transform.SetPositionAndRotation(worldCamera.transform.position, worldCamera.transform.rotation);
        weaponCamera.fieldOfView = worldCamera.fieldOfView;
        weaponCamera.rect = worldCamera.rect;
        weaponCamera.aspect = worldCamera.aspect;

        float dt = Time.deltaTime;
        float blend = cameraRig != null ? cameraRig.AimBlend : aim.IsAiming ? 1 : 0;
        Vector3 hip, sight;
        switch (weapons.CurrentWeaponCategory)
        {
            case WeaponCategory.Pistol: hip = new Vector3(.16f, -.18f, .42f); sight = new Vector3(0, -.10f, .46f); break;
            case WeaponCategory.Shotgun: hip = new Vector3(.20f, -.25f, .09f); sight = new Vector3(0, -.125f, .13f); break;
            case WeaponCategory.SMG: hip = new Vector3(.19f, -.22f, .27f); sight = new Vector3(0, -.17f, .29f); break;
            default: hip = new Vector3(.20f, -.23f, .20f); sight = new Vector3(0, -.158f, .22f); break;
        }
        Vector3 target = Vector3.Lerp(hip, sight, blend);
        float motion = movement.IsGrounded ? Mathf.Clamp01(movement.PlanarSpeed / movement.RunSpeed) : 0;
        phase += movement.PlanarSpeed * dt * 4.5f;
        target += new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 2) * .6f, 0) * (bobAmount * motion * (1 - blend * .85f));
        bool lookAllowed = Application.isFocused && Cursor.lockState == CursorLockMode.Locked &&
            (cameraRig == null || cameraRig.GameplayInputEnabled);
        Vector3 targetAngles = lookAllowed ? new Vector3(Input.GetAxisRaw("Mouse Y"), -Input.GetAxisRaw("Mouse X"), 0) * (swayAmount * (1 - blend * .85f)) : Vector3.zero;
        targetAngles = Vector3.ClampMagnitude(targetAngles, 2f);
        float lower = IsSprinting ? 1 : 0;
        if (reloading)
        {
            float t = ReloadProgress;
            float envelope = Mathf.Sin(Mathf.Clamp01(t / .9f) * Mathf.PI);
            target += new Vector3(-.015f, -.13f, -.06f) * envelope;
            targetAngles += new Vector3(18, -10, -18) * envelope;
        }
        float clearance = .95f;
        if (cover.Raycast(worldCamera.transform.position, worldCamera.transform.forward, clearance, out var hit))
            clearance = hit.distance;
        obstruction = Mathf.Lerp(obstruction, 1 - Mathf.InverseLerp(.25f, .85f, clearance), 1 - Mathf.Exp(-18 * dt));
        lower = Mathf.Max(lower, obstruction);
        target += new Vector3(0, -.10f, -.10f) * lower;
        targetAngles += new Vector3(22, 0, -8) * lower;
        recoil = Mathf.MoveTowards(recoil, 0, dt * .22f);
        equip = Mathf.MoveTowards(equip, 0, dt * .6f);
        target += new Vector3(0, -equip, -recoil);
        targetAngles.x -= recoil * 90;
        if (!initialized) { position = target; initialized = true; }
        float response = 1 - Mathf.Exp(-poseResponse * dt);
        position = Vector3.Lerp(position, target, response);
        angles = Vector3.Lerp(angles, targetAngles, response);
        weaponSocket.localPosition = position;
        weaponSocket.localRotation = Quaternion.Euler(angles);
    }

    private void OnDisable()
    {
        if (weapons != null)
        {
            weapons.ShotFired -= OnShot; weapons.WeaponChanged -= OnWeaponChanged;
            weapons.ReloadStateChanged -= OnReloadChanged;
        }
        if (originalShadows != null)
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null) bodyRenderers[i].shadowCastingMode = originalShadows[i];
        if (weaponCamera != null) weaponCamera.enabled = false;
    }
}
```


## HumanoidFootPlacement

Full file path: D:/GV project/Assets/Scripts/Player/HumanoidFootPlacement.cs

```csharp
using UnityEngine;

/// <summary>Small terrain corrections, weighted out during the swing phase and in the air.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Animator))]
public sealed class HumanoidFootPlacement : MonoBehaviour
{
    [SerializeField] private LayerMask groundLayers = 1 << 8;
    [SerializeField, Range(0f, 1f)] private float weight = 0.85f;
    [SerializeField, Min(0.01f)] private float maximumCorrection = 0.08f;
    [SerializeField] private bool useAnimationContacts;
    private Animator animator;
    private ThirdPersonPlayerController movement;
    private PlayerHealth health;
    private Transform owner;
    private CharacterController capsule;
    private PlayerStanceController stance;
    private float blend;
    private float leftContact, rightContact;
    private bool hasContactCurves;
#if UNITY_EDITOR
    public Vector4 LastLeftContact { get; private set; }
    public Vector4 LastRightContact { get; private set; }
    public int IkCallbacks { get; private set; }
    public Vector4 IkProbe { get; private set; }
#endif
    private readonly RaycastHit[] hits = new RaycastHit[12];
    [System.Serializable]
    private struct SolePoint { public Transform Bone; public Vector3 Position; public bool Left; }
    [SerializeField] private SolePoint[] solePoints = System.Array.Empty<SolePoint>();

    private void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponentInParent<ThirdPersonPlayerController>();
        health = GetComponentInParent<PlayerHealth>();
        owner = movement != null ? movement.transform : transform;
        capsule = owner.GetComponent<CharacterController>();
        stance = owner.GetComponent<PlayerStanceController>();
        bool left = false, right = false;
        foreach (var parameter in animator.parameters)
        { left |= parameter.name == "LeftFootContact"; right |= parameter.name == "RightFootContact"; }
        hasContactCurves = useAnimationContacts && left && right;
    }

#if UNITY_EDITOR
    [ContextMenu("Bake boot sole contact samples")]
    public void BakeSoles()
    {
        // Cache boot vertices in their dominant foot/toe bone's space once.
        // This avoids per-frame mesh baking and tracks the sole as the ankle rolls.
        var points=new System.Collections.Generic.List<SolePoint>();
        animator=GetComponent<Animator>();
        var lf=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var lt=animator.GetBoneTransform(HumanBodyBones.LeftToes);
        var rf=animator.GetBoneTransform(HumanBodyBones.RightFoot);var rt=animator.GetBoneTransform(HumanBodyBones.RightToes);
        foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if(!skin.name.Contains("Boots")||skin.sharedMesh==null)continue;
            var vertices=skin.sharedMesh.vertices;var weights=skin.sharedMesh.boneWeights;var bind=skin.sharedMesh.bindposes;var bones=skin.bones;
            for(int i=0;i<vertices.Length;i++)
            {
                int b=weights[i].boneIndex0;var bone=bones[b];
                if(bone!=lf&&bone!=lt&&bone!=rf&&bone!=rt)continue;
                points.Add(new SolePoint{Bone=bone,Position=bind[b].MultiplyPoint3x4(vertices[i]),Left=bone==lf||bone==lt});
            }
        }
        solePoints=points.ToArray();
        if(solePoints.Length==0)throw new System.InvalidOperationException("No boot sole contact samples found.");
    }
#endif
    private float SoleY(bool left,float fallback)
    {
        float low=float.PositiveInfinity;
        foreach(var point in solePoints)if(point.Left==left&&point.Bone!=null)low=Mathf.Min(low,point.Bone.TransformPoint(point.Position).y);
        return float.IsPositiveInfinity(low)?fallback:low;
    }

    private void OnAnimatorIK(int layerIndex)
    {
#if UNITY_EDITOR
        IkCallbacks++;
        IkProbe=new Vector4(layerIndex, movement!=null&&movement.IsGrounded?1:0, blend, animator.isHuman?1:0);
#endif
        if (layerIndex != 0 || !animator.isHuman) return;
        bool active = movement != null && movement.IsGrounded && (health == null || !health.IsDead);
        // Death continues with an unscaled Animator behind the frozen Game Over panel.
        // Release contact immediately when gameplay delta time is zero on death.
        blend = health != null && health.IsDead ? 0f :
            Mathf.MoveTowards(blend, active ? weight : 0f, Time.deltaTime * 10f);
        Place(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, HumanBodyBones.LeftLowerLeg, animator.leftFeetBottomHeight, ref leftContact, "LeftFootContact");
        Place(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, HumanBodyBones.RightLowerLeg, animator.rightFeetBottomHeight, ref rightContact, "RightFootContact");
    }

    private void Place(AvatarIKGoal goal, AvatarIKHint hint, HumanBodyBones kneeBone, float soleHeight, ref float smoothedContact, string contactParameter)
    {
        animator.SetIKPositionWeight(goal, 0f);
        animator.SetIKRotationWeight(goal, 0f);
        animator.SetIKHintPositionWeight(hint, 0f);
        float authoredContact = hasContactCurves ? Mathf.Clamp01(animator.GetFloat(contactParameter)) : 1f;
        smoothedContact = Mathf.MoveTowards(smoothedContact, authoredContact, Time.deltaTime * 12f);
        if (blend <= 0f) { smoothedContact = 0f; return; }
        // IK goals and imported foot bones have different rotation conventions.
        // Preserve the animated ankle rotation and adjust only the goal height.
        bool stanceBlend = stance != null && (stance.IsCrouching || stance.Blend > .001f);
        // Authored muscle-only clips do not carry reliable IK goal curves. Their
        // reported goal can remain near the pelvis; probe the evaluated ankle.
        Vector3 position = stanceBlend
            ? animator.GetBoneTransform(goal==AvatarIKGoal.LeftFoot?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot).position
            : animator.GetIKPosition(goal);
        float reach = stanceBlend ? Mathf.Max(.16f, maximumCorrection) : maximumCorrection;
        int count = Physics.RaycastNonAlloc(position + Vector3.up * reach,
            Vector3.down, hits, reach * 2f + soleHeight, groundLayers, QueryTriggerInteraction.Ignore);
#if UNITY_EDITOR
        IkProbe=new Vector4(position.y, soleHeight, count, blend);
#endif
        int best = -1;
        float closest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.transform.IsChildOf(owner) || hits[i].normal.y < 0.65f) continue;
            if (hits[i].distance < closest) { best = i; closest = hits[i].distance; }
        }
        if (best < 0) return;
        RaycastHit hit = hits[best];
#if UNITY_EDITOR
        var diagnostic = new Vector4(position.y, soleHeight, hit.point.y, hit.point.y + soleHeight + .006f - position.y);
        if(goal==AvatarIKGoal.LeftFoot)LastLeftContact=diagnostic;else LastRightContact=diagnostic;
#endif
        // Muscle interpolation between standing and deeply bent knees can push a
        // sole through the floor even when both source clips are calibrated.
        // Correct penetration only; never pull a lifted swing foot down.
        if (stanceBlend)
        {
            float penetration = hit.point.y + .006f - SoleY(goal==AvatarIKGoal.LeftFoot,position.y-soleHeight);
            if (penetration > .004f && penetration < .16f)
            {
                animator.SetIKPositionWeight(goal, 1f);
                animator.SetIKPosition(goal, position + Vector3.up * penetration);
                animator.SetIKHintPositionWeight(hint, .6f);
                animator.SetIKHintPosition(hint, animator.GetBoneTransform(kneeBone).position + owner.forward * .18f);
                return;
            }
        }
        // The corrected clips already have sole contact on a level street. Avoid
        // interpreting normal ankle roll as terrain error and pulling the foot down.
        if (hasContactCurves && capsule != null && hit.normal.y > .995f)
        {
            float level = owner.position.y + capsule.center.y - capsule.height * .5f - capsule.skinWidth;
            if (Mathf.Abs(hit.point.y - level) < .012f) return;
        }
        float clearance = position.y - soleHeight - hit.point.y;
        // Lifted feet must remain lifted; IK only adjusts feet near a planted stance.
        float contact = 1f - Mathf.InverseLerp(0.04f, 0.16f, clearance);
        float amount = blend * contact * smoothedContact;
        Vector3 target = position;
        float correction = hit.point.y + soleHeight + 0.006f - position.y;
        // Do not perturb a planted pose for tiny flat-ground differences or pull
        // the legs toward a surface beyond the small terrain-adjustment budget.
        if (Mathf.Abs(correction) > maximumCorrection) return;
        amount *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.004f, .025f, Mathf.Abs(correction)));
        if (amount < .001f) return;
        target.y += correction;
        animator.SetIKPositionWeight(goal, amount);
        animator.SetIKPosition(goal, target);
        Transform knee = animator.GetBoneTransform(kneeBone);
        animator.SetIKHintPositionWeight(hint, amount * .5f);
        animator.SetIKHintPosition(hint, knee.position + owner.forward * .12f);
    }
}
```


## PlayerAimController

Full file path: D:/GV project/Assets/Scripts/Player/PlayerAimController.cs

```csharp
using System;
using UnityEngine;

/// <summary>Owns aim intent. Camera and animation read this state; future UI can subscribe.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class PlayerAimController : MonoBehaviour
{
    [SerializeField]
    private PlayerHealth health;
    [SerializeField]
    private KeyCode aimKey = KeyCode.Mouse1;
    [SerializeField]
    private bool requireLockedCursor = true;
    [SerializeField, Min(0f)]
    private float aimMoveSpeed = 2.2f;
    public bool IsAiming { get; private set; }
    public float AimMoveSpeed => aimMoveSpeed;

    public event Action<bool> AimChanged;
    private bool inputSuppressed;
    public void SetInputSuppressed(bool suppressed)
    {
        inputSuppressed = suppressed;
        if (suppressed)
            SetAiming(false);
    }

    private void Awake()
    {
        if (health == null)
            health = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        bool canAim = !inputSuppressed && Time.timeScale > 0f && (health == null || !health.IsDead) && Application.isFocused && (!requireLockedCursor || Cursor.lockState == CursorLockMode.Locked);
        SetAiming(canAim && Input.GetKey(aimKey));
    }

    public void SetAiming(bool value)
    {
        value = value && !inputSuppressed && isActiveAndEnabled && (health == null || !health.IsDead);
        if (IsAiming == value)
            return;
        IsAiming = value;
        AimChanged?.Invoke(value);
    }

    private void OnDisable() => SetAiming(false);
}
```


## PlayerAnimatorDriver

Full file path: D:/GV project/Assets/Scripts/Player/PlayerAnimatorDriver.cs

```csharp
using UnityEngine;

/// <summary>Translates controller state into Animator parameters and exposes animation-only action hooks.</summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(ThirdPersonPlayerController))]
public sealed class PlayerAnimatorDriver : MonoBehaviour
{
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private ThirdPersonPlayerController movement;
    [SerializeField]
    private PlayerAimController aim;
    [SerializeField]
    private PlayerHealth health;
    [SerializeField, Min(0f)]
    private float parameterDampTime = 0.1f;
    private int actionsLayer = -1;
    private bool deathRequested;
    private PlayerStanceController stance;
    private bool supportsCrouch;
    private float previousHealth;
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Horizontal = Animator.StringToHash("Horizontal");
    private static readonly int Vertical = Animator.StringToHash("Vertical");
    private static readonly int IsAiming = Animator.StringToHash("IsAiming");
    private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int IsSprinting = Animator.StringToHash("IsSprinting");
    private static readonly int VerticalVelocity = Animator.StringToHash("VerticalVelocity");
    private static readonly int IsDead = Animator.StringToHash("IsDead");
    private static readonly int Fire = Animator.StringToHash("Fire");
    private static readonly int Reload = Animator.StringToHash("Reload");
    private static readonly int Hit = Animator.StringToHash("Hit");
    private static readonly int Death = Animator.StringToHash("Death");
    private void Awake()
    {
        stance=GetComponent<PlayerStanceController>();
        if (movement == null)
            movement = GetComponent<ThirdPersonPlayerController>();
        if (aim == null)
            aim = GetComponent<PlayerAimController>();
        if (health == null)
            health = GetComponent<PlayerHealth>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("PlayerAnimatorDriver needs an Animator with the Phase 2 controller.", this);
            enabled = false;
            return;
        }

        animator.applyRootMotion = false;
        foreach(var parameter in animator.parameters)if(parameter.name=="IsCrouching")supportsCrouch=true;
        actionsLayer = animator.GetLayerIndex("Upper Body Actions");
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Died += PlayDeath;
            health.HealthChanged += OnHealthChanged;
            previousHealth = health.CurrentHealth;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= PlayDeath;
            health.HealthChanged -= OnHealthChanged;
        }
    }

    private void Update()
    {
        if (health != null && health.IsDead && !deathRequested)
            PlayDeath();
        bool dead = deathRequested || (health != null && health.IsDead);
        bool aiming = aim != null && aim.IsAiming && !dead;
        Vector3 localVelocity = transform.InverseTransformDirection(movement.PlanarVelocity);
        float referenceSpeed = aiming ? aim.AimMoveSpeed : movement.WalkSpeed;
        bool crouching=stance!=null&&stance.IsCrouching&&!dead;
        if(crouching)referenceSpeed=stance.CrouchedSpeed;
        Vector2 local = Vector2.ClampMagnitude(new Vector2(localVelocity.x, localVelocity.z) / Mathf.Max(.01f, referenceSpeed), 1f);
        float dt = Time.deltaTime;
        // Speed uses semantic knots: idle 0, walk 1, run 2, sprint 3.
        float normalizedSpeed = NormalizeSpeed(movement.PlanarSpeed);
        // The motor already accelerates/brakes smoothly. Smoothing Speed a second
        // time leaves the feet walking after the capsule slows or hits a wall.
        // Keep gait weights tied to measured displacement; damp direction only.
        animator.SetFloat(Speed, dead ? 0f : normalizedSpeed);
        animator.SetFloat(Horizontal, dead ? 0f : local.x, parameterDampTime, dt);
        animator.SetFloat(Vertical, dead ? 0f : local.y, parameterDampTime, dt);
        animator.SetBool(IsAiming, aiming);
        animator.SetBool(IsGrounded, movement.IsGrounded);
        animator.SetBool(IsSprinting, movement.IsSprinting && !dead);
        animator.SetFloat(VerticalVelocity, movement.VerticalVelocity);
        animator.SetBool(IsDead, dead);
        if(supportsCrouch)animator.SetBool("IsCrouching",crouching);
        if (actionsLayer >= 0)
        {
            bool active = !animator.GetCurrentAnimatorStateInfo(actionsLayer).IsName("Empty") || (animator.IsInTransition(actionsLayer) && !animator.GetNextAnimatorStateInfo(actionsLayer).IsName("Empty"));
            float weight = Mathf.MoveTowards(animator.GetLayerWeight(actionsLayer), !dead && active ? 1f : 0f, dt / 0.08f);
            animator.SetLayerWeight(actionsLayer, dead ? 0f : weight);
        }
    }

    private float NormalizeSpeed(float speed)
    {
        if (speed <= movement.WalkSpeed)
            return speed / movement.WalkSpeed;
        if (speed <= movement.RunSpeed)
            return 1f + Mathf.InverseLerp(movement.WalkSpeed, movement.RunSpeed, speed);
        return 2f + Mathf.InverseLerp(movement.RunSpeed, movement.SprintSpeed, speed);
    }

    // These methods animate only. They do not deal damage, spawn objects, consume ammo, or read firing keys.
    public void PlayFire()
    {
        if (CanPlayAction())
            animator.SetTrigger(Fire);
    }

    public void PlayReload(float duration = 1.2f)
    {
        if (CanPlayAction())
        {
            animator.SetFloat("ReloadSpeed", 1.2f / Mathf.Max(.05f, duration));
            animator.SetTrigger(Reload);
        }
    }

    public void PlayHit()
    {
        if (CanPlayAction())
            animator.SetTrigger(Hit);
    }

    private void OnHealthChanged(float current, float maximum)
    {
        if (animator == null)
            return;
        bool reloading = actionsLayer >= 0 && (animator.GetCurrentAnimatorStateInfo(actionsLayer).IsName("Reload") || animator.GetNextAnimatorStateInfo(actionsLayer).IsName("Reload"));
        if (current > 0 && current < previousHealth && !reloading)
            PlayHit();
        previousHealth = current;
    }

    public void CancelWeaponAction()
    {
        if (animator == null)
            return;
        animator.ResetTrigger(Fire);
        animator.ResetTrigger(Reload);
        if (actionsLayer >= 0)
        {
            animator.Play("Empty", actionsLayer, 0f);
            animator.SetLayerWeight(actionsLayer, 0f);
        }
    }

    public void PlayDeath()
    {
        if (animator == null)
            return;
        deathRequested = true;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime; // Finish death behind the frozen Game Over panel.
        animator.ResetTrigger(Fire);
        animator.ResetTrigger(Reload);
        animator.ResetTrigger(Hit);
        animator.SetBool(IsDead, true);
        animator.SetTrigger(Death);
    }

    private bool CanPlayAction() => animator != null && isActiveAndEnabled && !deathRequested && (health == null || !health.IsDead);
    [ContextMenu("Animation Preview/Fire (Play Mode)")]
    private void PreviewFire()
    {
        if (Application.isPlaying)
            PlayFire();
    }

    [ContextMenu("Animation Preview/Reload (Play Mode)")]
    private void PreviewReload()
    {
        if (Application.isPlaying)
            PlayReload();
    }

    [ContextMenu("Animation Preview/Hit (Play Mode)")]
    private void PreviewHit()
    {
        if (Application.isPlaying)
            PlayHit();
    }

    [ContextMenu("Animation Preview/Death via PlayerHealth (Play Mode)")]
    private void PreviewDeath()
    {
        if (Application.isPlaying && health != null)
            health.Die();
    }
}
```


## PlayerEquipment

Full file path: D:/GV project/Assets/Scripts/Player/PlayerEquipment.cs

```csharp
using System;
using UnityEngine;

public sealed class PlayerEquipment : MonoBehaviour
{
    [SerializeField]
    private PlayerHealth health;
    [SerializeField]
    private Camera viewCamera;
    [SerializeField]
    private GrenadeProjectile grenadePrefab;
    [SerializeField, Min(1)]
    private float healAmount = 45;
    [SerializeField]
    private float throwSpeed = 14;
    private float nextThrow;
    private ThirdPersonCameraController cameraRig;
    public int Medkits { get; private set; }
    public int Grenades { get; private set; }

    public event Action<int, int> Changed;
    private void Awake()
    {
        if (health == null) health = GetComponent<PlayerHealth>();
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera != null) cameraRig = viewCamera.GetComponent<ThirdPersonCameraController>();
    }
    public void Configure(EquipmentPack pack)
    {
        Medkits = pack.medkits;
        Grenades = pack.grenades;
        Changed?.Invoke(Medkits, Grenades);
    }

    private void Update()
    {
        if (Time.timeScale <= 0 || health.IsDead || Cursor.lockState != CursorLockMode.Locked || !Application.isFocused)
            return;
        if (Input.GetKeyDown(KeyCode.Q))
            UseMedkit();
        if (Input.GetKeyDown(KeyCode.G))
            ThrowGrenade();
    }

    public bool UseMedkit()
    {
        if (Time.timeScale <= 0 || health.IsDead || Medkits <= 0 || health.CurrentHealth >= health.MaxHealth)
            return false;
        Medkits--;
        health.Heal(healAmount);
        Changed?.Invoke(Medkits, Grenades);
        return true;
    }

    public bool ThrowGrenade()
    {
        if (Time.timeScale <= 0 || health.IsDead || Grenades <= 0 || grenadePrefab == null || viewCamera == null || Time.time < nextThrow)
            return false;
        var origin = cameraRig != null && cameraRig.IsFirstPerson ? viewCamera.transform.position : transform.position + Vector3.up * 1.5f;
        var direction = (viewCamera.transform.forward + Vector3.up * .18f).normalized;
        // Spawn at the body, never beyond nearby cover, and ignore only the owner's colliders.
        var projectile = Instantiate(grenadePrefab, origin, Quaternion.identity);
        projectile.Launch(gameObject, direction * throwSpeed);
        Grenades--;
        nextThrow = Time.time + .6f;
        Changed?.Invoke(Medkits, Grenades);
        return true;
    }
}
```


## PlayerHealth

Full file path: D:/GV project/Assets/Scripts/Player/PlayerHealth.cs

```csharp
using System;
using UnityEngine;

public sealed class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)]
    private float maxHealth = 100f;
    public float MaxHealth
    {
        get
        {
            return maxHealth;
        }
    }

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    /// <summary>Raised with the new current health and the configured maximum health.</summary>
    public event Action<float, float> HealthChanged;
    public event Action Died;
    private void Awake()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        RaiseHealthChanged();
        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        RaiseHealthChanged();
    }

    public void Die()
    {
        if (IsDead)
        {
            return;
        }

        bool healthChanged = CurrentHealth > 0f;
        IsDead = true;
        CurrentHealth = 0f;
        if (healthChanged)
        {
            RaiseHealthChanged();
        }

        Died?.Invoke();
    }

    private void RaiseHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
    }
}
```


## PlayerInteractor

Full file path: D:/GV project/Assets/Scripts/Player/PlayerInteractor.cs

```csharp
using System;
using UnityEngine;

public sealed class PlayerInteractor : MonoBehaviour
{
    [SerializeField]
    private Camera viewCamera;
    [SerializeField]
    private PlayerHealth health;
    [SerializeField]
    private float reach = 3.2f;
    private IInteractable current;
    private string prompt = "";
    private float nextScan;
    public string Prompt => prompt;

    public event Action<string> PromptChanged;
    private void Awake()
    {
        if (health == null) health = GetComponent<PlayerHealth>();
        if (viewCamera == null) viewCamera = Camera.main;
        if (health == null || viewCamera == null)
        {
            Debug.LogError("PlayerInteractor requires PlayerHealth and the gameplay camera.", this);
            enabled = false;
        }
    }
    private void Update()
    {
        if (Time.timeScale <= 0 || health.IsDead || Cursor.lockState != CursorLockMode.Locked)
        {
            SetPrompt("");
            return;
        }

        if (Time.time >= nextScan)
        {
            nextScan = Time.time + .1f;
            current = null;
            var ray = viewCamera.ViewportPointToRay(new Vector3(.5f, .5f));
            if (Physics.Raycast(ray, out var hit, 10, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && Vector3.Distance(transform.position, hit.point) <= reach)
                current = hit.collider.GetComponentInParent<IInteractable>();
            SetPrompt(current != null ? current.InteractionPrompt : "");
        }

        if (Input.GetKeyDown(KeyCode.E) && current != null && current.CanInteract(gameObject))
        {
            current.Interact(gameObject);
            SetPrompt(current.InteractionPrompt);
        }
    }

    private void SetPrompt(string value)
    {
        if (prompt == value)
            return;
        prompt = value;
        PromptChanged?.Invoke(value);
    }

    private void OnDisable()
    {
        current = null;
        SetPrompt("");
    }
}
```


## PlayerLoadoutApplier

Full file path: D:/GV project/Assets/Scripts/Player/PlayerLoadoutApplier.cs

```csharp
using UnityEngine;

[DefaultExecutionOrder(-150)]
public sealed class PlayerLoadoutApplier : MonoBehaviour
{
    [SerializeField]
    private LoadoutCatalog catalog;
    private void Awake()
    {
        var session = LoadoutSession.Ensure(catalog);
        if (!session.IsValid)
        {
            Debug.LogError("Assign a valid LoadoutCatalog before entering ZombieMap.", this);
            return;
        }

        GetComponent<PlayerWeaponController>().ConfigureLoadout(new[] { catalog.primaries[session.Primary], catalog.pistol });
        GetComponent<PlayerEquipment>().Configure(catalog.equipment[session.Equipment]);
    }
}
```


## PlayerPhysicsPush

Full file path: D:/GV project/Assets/Scripts/Player/PlayerPhysicsPush.cs

```csharp
using UnityEngine;

public sealed class PlayerPhysicsPush : MonoBehaviour
{
    [SerializeField, Min(0)]
    private float acceleration = 15f;
    [SerializeField, Min(0)]
    private float maximumSpeed = 2f;
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var body = hit.collider.attachedRigidbody;
        if (Time.timeScale <= 0 || body == null || body.isKinematic || hit.moveDirection.y < -.3f || body.linearVelocity.magnitude > maximumSpeed)
            return;
        var horizontal = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
        body.AddForce(horizontal * acceleration, ForceMode.Acceleration);
    }
}
```


## PlayerStanceController

Full file path: D:/GV project/Assets/Scripts/Player/PlayerStanceController.cs

```csharp
using System;
using UnityEngine;

/// <summary>Crouch intent, safe capsule resizing, and a shared blend for animation/camera presentation.</summary>
[DefaultExecutionOrder(-50), RequireComponent(typeof(CharacterController)), DisallowMultipleComponent]
public sealed class PlayerStanceController : MonoBehaviour
{
    [SerializeField] private KeyCode crouchKey=KeyCode.C;
    [SerializeField, Min(.7f)] private float crouchedHeight=1.55f;
    [SerializeField, Min(.1f)] private float crouchedSpeed=1.05f;
    [SerializeField, Min(.1f)] private float crouchedAimSpeed=.85f;
    [SerializeField, Min(.05f)] private float blendTime=.18f;
    [SerializeField] private LayerMask obstructionLayers=1<<8;
    private CharacterController capsule;
    private ThirdPersonPlayerController movement;
    private PlayerHealth health;
    private float standingHeight,bottom,blendVelocity;
    private Vector3 standingCenter;
    private readonly Collider[] overlaps=new Collider[24];
    public bool IsCrouching {get;private set;}
    public float Blend {get;private set;}
    public float CrouchedSpeed=>crouchedSpeed;
    public float CrouchedAimSpeed=>crouchedAimSpeed;
    public event Action<bool> StanceChanged;

    private void Awake()
    {
        capsule=GetComponent<CharacterController>();movement=GetComponent<ThirdPersonPlayerController>();health=GetComponent<PlayerHealth>();
        standingHeight=capsule.height;standingCenter=capsule.center;bottom=standingCenter.y-standingHeight*.5f;
        crouchedHeight=Mathf.Clamp(crouchedHeight,capsule.radius*2+.05f,standingHeight);
    }
    private void Update()
    {
        if(Time.deltaTime<=0||capsule==null||!capsule.enabled)return;
        bool alive=health==null||!health.IsDead;
        if(alive&&Application.isFocused&&Cursor.lockState==CursorLockMode.Locked&&Input.GetKeyDown(crouchKey))TrySetCrouching(!IsCrouching);
        // A moving obstacle can enter the standing volume during the blend.
        if(!IsCrouching&&Blend>.001f&&!CanStand()){IsCrouching=true;StanceChanged?.Invoke(true);}
        Blend=Mathf.SmoothDamp(Blend,IsCrouching?1:0,ref blendVelocity,blendTime);
        float height=Mathf.Lerp(standingHeight,crouchedHeight,Blend);
        capsule.height=height;capsule.center=new Vector3(standingCenter.x,bottom+height*.5f,standingCenter.z);
    }
    public bool TrySetCrouching(bool value)
    {
        if(!isActiveAndEnabled||Time.timeScale<=0||capsule==null||(health!=null&&health.IsDead))return false;
        if(value==IsCrouching)return true;
        if(value&&movement!=null&&!movement.IsGrounded)return false;
        if(!value&&!CanStand())return false;
        IsCrouching=value;StanceChanged?.Invoke(value);return true;
    }
    public bool CanStand()
    {
        if(capsule==null)return false;
        float radius=Mathf.Max(.01f,capsule.radius-.015f);
        Vector3 lower=transform.TransformPoint(new Vector3(standingCenter.x,bottom+radius+.025f,standingCenter.z));
        Vector3 upper=transform.TransformPoint(new Vector3(standingCenter.x,bottom+standingHeight-radius,standingCenter.z));
        int count=Physics.OverlapCapsuleNonAlloc(lower,upper,radius,overlaps,obstructionLayers,QueryTriggerInteraction.Ignore);
        if(count==overlaps.Length)return false;
        for(int i=0;i<count;i++)if(overlaps[i]!=capsule&&!overlaps[i].transform.IsChildOf(transform))return false;
        return true;
    }
    // Keep a safe crouched capsule if this component is temporarily disabled under a ceiling.
    private void OnDisable(){blendVelocity=0;}
}
```


## ShooterBodyPose

Full file path: D:/GV project/Assets/Scripts/Player/ShooterBodyPose.cs

```csharp
using UnityEngine;

/// <summary>Blends the weapon carry pose over retargeted locomotion without changing movement or firing.</summary>
[DefaultExecutionOrder(150), RequireComponent(typeof(Animator)), DisallowMultipleComponent]
public sealed class ShooterBodyPose : MonoBehaviour
{
    [SerializeField, Min(.01f)] private float blendSpeed = 8f;
    private Animator animator;
    private ThirdPersonPlayerController movement;
    private PlayerHealth health;
    private PlayerAimController aim;
    private PlayerWeaponController weapons;
    private Transform chest, view;
    private float pitch;
    private int layer;
    private void Awake()
    {
        animator=GetComponent<Animator>();
        movement=GetComponentInParent<ThirdPersonPlayerController>();
        health=GetComponentInParent<PlayerHealth>();
        aim=GetComponentInParent<PlayerAimController>();
        weapons=GetComponentInParent<PlayerWeaponController>();
        chest=animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Chest):null;
        if(Camera.main!=null)view=Camera.main.transform;
        layer=animator.GetLayerIndex("Weapon Carry");
    }
    private void Update()
    {
        if(layer<0)return;
        bool carry=movement!=null&&movement.IsGrounded&&!movement.IsSprinting&&health!=null&&!health.IsDead;
        animator.SetLayerWeight(layer,Mathf.MoveTowards(animator.GetLayerWeight(layer),carry?1:0,Time.deltaTime*blendSpeed));
    }
    private void LateUpdate()
    {
        if(chest==null||view==null||health==null||health.IsDead||Time.deltaTime<=0)return;
        bool active=aim!=null&&aim.IsAiming&&weapons!=null&&!weapons.IsReloading&&movement!=null&&movement.IsGrounded;
        float target=active?Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(view.forward.y,-1,1))*Mathf.Rad2Deg,-45,45):0;
        pitch=Mathf.Lerp(pitch,target,1-Mathf.Exp(-blendSpeed*Time.deltaTime));
        // Runs before weapon aim and support-hand IK, so their targets include this upper-body motion.
        chest.rotation=Quaternion.AngleAxis(pitch*.75f,transform.right)*chest.rotation;
    }
}
```


## SupportHandGrip

Full file path: D:/GV project/Assets/Scripts/Player/SupportHandGrip.cs

```csharp
using UnityEngine;

/// <summary>Optional visual-only two-bone support arm alignment, after weapon aiming. No firing or root motion.</summary>
[DefaultExecutionOrder(300), DisallowMultipleComponent, RequireComponent(typeof(Animator))]
public sealed class SupportHandGrip : MonoBehaviour
{
    [SerializeField, Range(0,1)] private float maximumWeight = .95f;
    [SerializeField, Min(.01f)] private float blendSeconds = .16f;
    private Transform upper, lower, hand, chest;
    private PlayerWeaponController weapons;
    private PlayerHealth health;
    private ThirdPersonPlayerController movement;
    private Animator animator;
    private float weight;
    private void Awake()
    {
        animator=GetComponent<Animator>();
        if(!animator.isHuman){enabled=false;return;}
        upper=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        lower=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
        chest=animator.GetBoneTransform(HumanBodyBones.Chest);
        weapons=GetComponentInParent<PlayerWeaponController>();health=GetComponentInParent<PlayerHealth>();
        movement=GetComponentInParent<ThirdPersonPlayerController>();
    }
    private void LateUpdate()
    {
        if(upper==null || lower==null || hand==null || chest==null || weapons==null || Time.deltaTime<=0)return;
        var view=weapons.ActiveView;var target=view!=null?view.SupportHand:null;
        bool active=target!=null && animator.enabled && health!=null && !health.IsDead && !weapons.IsReloading && movement!=null && movement.IsGrounded && !movement.IsSprinting;
        weight=Mathf.MoveTowards(weight,active?maximumWeight:0,Time.deltaTime/blendSeconds);
        if(target==null || weight<=.001f || health==null || health.IsDead)return;
        Vector3 start=upper.position, end=Vector3.Lerp(hand.position,target.position,weight);
        float a=Vector3.Distance(start,lower.position), b=Vector3.Distance(lower.position,hand.position);
        if(a<.001f || b<.001f)return;
        Vector3 direction=end-start;float d=Mathf.Clamp(direction.magnitude,Mathf.Abs(a-b)+.001f,a+b-.003f);
        direction=direction.sqrMagnitude>.00001f?direction.normalized:transform.forward;
        Vector3 pole=-transform.right*.7f-transform.forward*.25f-Vector3.up*.5f;
        Vector3 bend=Vector3.ProjectOnPlane(pole,direction).normalized;
        if(bend.sqrMagnitude<.01f)bend=Vector3.Cross(direction,transform.up).normalized;
        float along=(a*a+d*d-b*b)/(2*d), height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        Vector3 elbow=start+direction*along+bend*height;
        Quaternion wristRotation=hand.rotation;
        upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,start+direction*d-lower.position)*lower.rotation;
        hand.rotation=wristRotation;
    }
    private void OnDisable(){weight=0;}
}
```


## ThirdPersonCameraController

Full file path: D:/GV project/Assets/Scripts/Player/ThirdPersonCameraController.cs

```csharp
using UnityEngine;

/// <summary>Shared look/input camera with first-person and legacy shoulder views.</summary>
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class ThirdPersonCameraController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField]
    private Transform cameraTarget;
    [SerializeField]
    private Transform aimTarget;
    [SerializeField]
    private PlayerAimController aim;
    [Header("Virtual camera modes")]
    [SerializeField]
    private ThirdPersonCameraMode explorationCamera;
    [SerializeField]
    private ThirdPersonCameraMode aimCamera;
    [SerializeField, Min(0.01f)]
    private float modeBlendTime = 0.2f;
    [Header("Orbit and fallback exploration pose")]
    [SerializeField, Min(0.1f)]
    private float followDistance = 4.5f;
    [SerializeField]
    private float lookAtHeightOffset = 0.05f;
    [SerializeField]
    private float startingPitch = 12f;
    [SerializeField]
    private float minimumPitch = -25f;
    [SerializeField]
    private float maximumPitch = 65f;
    [SerializeField, Min(0f)]
    private float sensitivity = 2.5f;
    [SerializeField, Min(0f)]
    private float positionSmoothTime = 0.08f;
    [SerializeField, Min(1f)]
    private float aimTargetDistance = 100f;
    [Header("Collision")]
    [SerializeField, Min(0.01f)]
    private float collisionRadius = 0.2f;
    [SerializeField, Min(0f)]
    private float collisionPadding = 0.05f;
    [SerializeField]
    private LayerMask collisionLayers = Physics.DefaultRaycastLayers;
    [Header("Mouse")]
    [SerializeField]
    private bool lockCursorAtStart = true;
    [SerializeField]
    private bool invertMouseY;
    [Header("First person")]
    [SerializeField] private bool firstPerson;
    [SerializeField, Range(45f, 100f)] private float firstPersonFieldOfView = 75f;
    [SerializeField, Range(30f, 80f)] private float firstPersonAimFieldOfView = 55f;
    [SerializeField, Min(.05f)] private float eyeInset = .14f;
    private CharacterController playerCapsule;
    private readonly RaycastHit[] collisionHits = new RaycastHit[32];
    private Camera outputCamera;
    private Transform playerRoot;
    private PlayerStanceController stance;
    private float yaw;
    private float pitch;
    private float aimBlend;
    private float blendVelocity;
    private Vector3 pivot;
    private Vector3 pivotVelocity;
    private bool initialized;
    private bool cursorManagedByUI;
    private bool gameplayInputEnabled = true;
    public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    public bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;
    public float AimBlend => aimBlend;
    public int CursorLockFrame { get; private set; } = -1;
    public bool GameplayInputEnabled => gameplayInputEnabled;
    public bool IsFirstPerson => firstPerson;

    public void SetCursorManagedByUI(bool managed) => cursorManagedByUI = managed;
    public void SetGameplayInputEnabled(bool value)
    {
        gameplayInputEnabled = value;
        SetCursorLocked(value);
    }

    /// <summary>Permanent orbit kick in degrees. Positive pitch kicks the view upward.</summary>
    public void ApplyRecoil(float upwardDegrees, float sidewaysDegrees)
    {
        pitch = Mathf.Clamp(pitch - upwardDegrees, minimumPitch, maximumPitch);
        yaw = Mathf.Repeat(yaw + sidewaysDegrees, 360f);
    }

    public float FollowDistance
    {
        get => explorationCamera != null ? explorationCamera.Distance : followDistance;
        set
        {
            followDistance = Mathf.Max(0.3f, value);
            if (explorationCamera != null)
                explorationCamera.Distance = followDistance;
        }
    }

    public float Sensitivity { get => sensitivity; set => sensitivity = Mathf.Max(0f, value); }

    private void Awake()
    {
        outputCamera = GetComponent<Camera>();
        if (cameraTarget == null)
        {
            ThirdPersonPlayerController player = FindAnyObjectByType<ThirdPersonPlayerController>();
            if (player != null)
                cameraTarget = player.transform.Find("CameraTarget") ?? player.transform;
        }

        if (cameraTarget != null)
        {
            ThirdPersonPlayerController player = cameraTarget.GetComponentInParent<ThirdPersonPlayerController>();
            playerRoot = player != null ? player.transform : null;
            if (playerRoot != null) playerCapsule = playerRoot.GetComponent<CharacterController>();
            if(playerRoot!=null)stance=playerRoot.GetComponent<PlayerStanceController>();
            if (aim == null && playerRoot != null)
                aim = playerRoot.GetComponent<PlayerAimController>();
            yaw = cameraTarget.eulerAngles.y;
        }

        pitch = Mathf.Clamp(startingPitch, minimumPitch, maximumPitch);
    }

    private void OnEnable() => SetCursorLocked(gameplayInputEnabled && lockCursorAtStart);
    private void Update()
    {
        if (!gameplayInputEnabled || Time.timeScale <= 0f)
            return;
        if (!cursorManagedByUI)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                SetCursorLocked(false);
            else if (!CursorLocked && Application.isFocused && Input.GetMouseButtonDown(0))
                SetCursorLocked(true);
        }

        if (!CursorLocked || !Application.isFocused || Time.timeScale <= 0f)
            return;
        float multiplier = Mathf.Lerp(explorationCamera != null ? explorationCamera.SensitivityMultiplier : 1f, aimCamera != null ? aimCamera.SensitivityMultiplier : 0.65f, aimBlend);
        yaw = Mathf.Repeat(yaw + Input.GetAxisRaw("Mouse X") * sensitivity * multiplier, 360f);
        pitch = Mathf.Clamp(pitch + Input.GetAxisRaw("Mouse Y") * sensitivity * multiplier * (invertMouseY ? 1f : -1f), minimumPitch, maximumPitch);
    }

    private void LateUpdate()
    {
        if (cameraTarget == null || Time.deltaTime <= 0f)
            return;
        aimBlend = Mathf.SmoothDamp(aimBlend, aim != null && aim.IsAiming ? 1f : 0f, ref blendVelocity, modeBlendTime);
        if (firstPerson && playerRoot != null && playerCapsule != null)
        {
            // The capsule owns the eye position. Bone motion and follow damping
            // cannot introduce head bob, wall peeking or locomotion lag.
            Vector3 eye = playerCapsule.center + Vector3.up * (playerCapsule.height * .5f - eyeInset);
            transform.SetPositionAndRotation(playerRoot.TransformPoint(eye), Quaternion.Euler(pitch, yaw, 0f));
            outputCamera.fieldOfView = Mathf.Lerp(firstPersonFieldOfView, firstPersonAimFieldOfView, aimBlend);
            UpdateAimTarget();
            return;
        }
        Vector3 targetPivot = cameraTarget.position;
        if(stance!=null)targetPivot+=Vector3.down*(.38f*stance.Blend);
        if (!initialized)
        {
            pivot = targetPivot;
            initialized = true;
        }
        else
            pivot = Vector3.SmoothDamp(pivot, targetPivot, ref pivotVelocity, positionSmoothTime);
        float distance = Mathf.Lerp(explorationCamera != null ? explorationCamera.Distance : followDistance, aimCamera != null ? aimCamera.Distance : 2.25f, aimBlend);
        Vector2 offset = Vector2.Lerp(explorationCamera != null ? explorationCamera.ShoulderOffset : new Vector2(0.2f, lookAtHeightOffset), aimCamera != null ? aimCamera.ShoulderOffset : new Vector2(0.6f, 0.08f), aimBlend);
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desired = pivot + orbit * new Vector3(offset.x, offset.y, -distance);
        // Re-solve collision from the current player pivot after smoothing, including the shoulder offset.
        // This avoids interpolating the camera through a wall during a mode change.
        outputCamera.fieldOfView = Mathf.Lerp(explorationCamera != null ? explorationCamera.FieldOfView : 60f, aimCamera != null ? aimCamera.FieldOfView : 45f, aimBlend);
        float radius = Mathf.Max(collisionRadius, NearPlaneRadius());
        Vector3 safe = ConstrainCamera(targetPivot, desired, radius);
        transform.SetPositionAndRotation(safe, orbit);
        UpdateAimTarget();
    }

    private void UpdateAimTarget()
    {
        if (aimTarget != null)
        {
            Ray ray = outputCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float distanceToAim = NearestHit(ray.origin, 0f, ray.direction, aimTargetDistance);
            aimTarget.position = ray.GetPoint(distanceToAim);
        }
    }

    private Vector3 ConstrainCamera(Vector3 origin, Vector3 desired, float radius)
    {
        Vector3 delta = desired - origin;
        float distance = delta.magnitude;
        if (distance < 0.001f)
            return origin;
        float allowed = NearestHit(origin, radius, delta / distance, distance);
        // A ray still detects the wall if a large probe starts overlapping it.
        float rayDistance = NearestHit(origin, 0f, delta / distance, distance);
        if (rayDistance < distance)
            allowed = Mathf.Min(allowed, Mathf.Max(0f, rayDistance - radius));
        return origin + delta / distance * Mathf.Max(0f, allowed < distance ? allowed - collisionPadding : distance);
    }

    private float NearestHit(Vector3 origin, float radius, Vector3 direction, float distance)
    {
        int count = radius > 0f ? Physics.SphereCastNonAlloc(origin, radius, direction, collisionHits, distance, collisionLayers, QueryTriggerInteraction.Ignore) : Physics.RaycastNonAlloc(origin, direction, collisionHits, distance, collisionLayers, QueryTriggerInteraction.Ignore);
        float nearest = distance;
        for (int i = 0; i < count; i++)
        {
            Transform hit = collisionHits[i].transform;
            if (hit == null || (playerRoot != null && hit.IsChildOf(playerRoot)))
                continue;
            nearest = Mathf.Min(nearest, collisionHits[i].distance);
        }

        return nearest;
    }

    private float NearPlaneRadius()
    {
        float halfHeight = Mathf.Tan(outputCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * outputCamera.nearClipPlane;
        return Mathf.Sqrt(halfHeight * halfHeight * (1f + outputCamera.aspect * outputCamera.aspect));
    }

    private void SetCursorLocked(bool locked)
    {
        if (locked && !CursorLocked)
            CursorLockFrame = Time.frameCount;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            SetCursorLocked(false);
    }

    private void OnDisable() => SetCursorLocked(false);
}
```


## ThirdPersonCameraMode

Full file path: D:/GV project/Assets/Scripts/Player/ThirdPersonCameraMode.cs

```csharp
using UnityEngine;

/// <summary>A virtual camera pose; only Main Camera renders or owns an AudioListener.</summary>
[DisallowMultipleComponent]
public sealed class ThirdPersonCameraMode : MonoBehaviour
{
    [SerializeField, Min(0.3f)]
    private float distance = 4.5f;
    [SerializeField]
    private Vector2 shoulderOffset = new Vector2(0.2f, 0.05f);
    [SerializeField, Range(20f, 100f)]
    private float fieldOfView = 60f;
    [SerializeField, Min(0f)]
    private float sensitivityMultiplier = 1f;
    public float Distance { get => distance; set => distance = Mathf.Max(0.3f, value); }
    public Vector2 ShoulderOffset => shoulderOffset;
    public float FieldOfView => fieldOfView;
    public float SensitivityMultiplier => sensitivityMultiplier;
}
```


## ThirdPersonPlayerController

Full file path: D:/GV project/Assets/Scripts/Player/ThirdPersonPlayerController.cs

```csharp
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public sealed class ThirdPersonPlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform cameraTransform;
    [SerializeField]
    private Transform groundCheck;
    [SerializeField]
    private ThirdPersonCameraController cameraRig;
    [SerializeField]
    private PlayerAimController aim;
    [SerializeField]
    private PlayerHealth health;
    [Header("Speeds in metres per second")]
    [SerializeField, Min(0.01f)]
    private float walkSpeed = 2.2f;
    [SerializeField, Min(0.01f)]
    private float runSpeed = 4.2f;
    [SerializeField, Min(0.01f)]
    private float sprintSpeed = 6.5f;
    [SerializeField, Min(0f)]
    private float rotationDegreesPerSecond = 720f;
    [Header("Movement smoothing")]
    [SerializeField, Min(1f)] private float acceleration = 14f;
    [SerializeField, Min(1f)] private float braking = 20f;
    [SerializeField, Min(0.01f)] private float turnSmoothTime = 0.09f;
    private Vector3 smoothedVelocity;
    private float turnVelocity;
    [Header("Jump and gravity")]
    [SerializeField, Min(0f)]
    private float jumpHeight = 1.25f;
    [SerializeField]
    private float gravity = -20f;
    [SerializeField]
    private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField]
    private KeyCode sprintKey = KeyCode.LeftControl;
    [SerializeField]
    private KeyCode jumpKey = KeyCode.Space;
    [Header("Ground probe")]
    [SerializeField, Min(0.01f)]
    private float groundProbeRadius = 0.2f;
    [SerializeField]
    private LayerMask groundLayers = Physics.DefaultRaycastLayers;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private CharacterController characterController;
    private PlayerStanceController stance;
    private float verticalVelocity;
    public Vector2 MoveInput { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsSprinting { get; private set; }
    public float VerticalVelocity => verticalVelocity;
    public float PlanarSpeed { get; private set; }
    public Vector3 PlanarVelocity { get; private set; }
    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float SprintSpeed => sprintSpeed;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        stance = GetComponent<PlayerStanceController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (cameraRig == null && cameraTransform != null)
            cameraRig = cameraTransform.GetComponent<ThirdPersonCameraController>();
        if (aim == null)
            aim = GetComponent<PlayerAimController>();
        if (health == null)
            health = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || !characterController.enabled)
            return;
        bool alive = health == null || !health.IsDead;
        bool inputAllowed = alive && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        bool aiming = aim != null && aim.IsAiming && alive;
        bool firstPerson = cameraRig != null && cameraRig.IsFirstPerson;
        float x = inputAllowed ? (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f) : 0f;
        float z = inputAllowed ? (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f) : 0f;
        MoveInput = Vector2.ClampMagnitude(new Vector2(x, z), 1f);
        // Never let a ground probe re-ground an ascending jump.
        IsGrounded = verticalVelocity <= 0f && (characterController.isGrounded || ProbeGround());
        if (IsGrounded)
            verticalVelocity = -2f;
        Vector3 forward = cameraRig != null ? cameraRig.PlanarForward : Vector3.ProjectOnPlane(cameraTransform != null ? cameraTransform.forward : transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f)
            forward = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 direction = Vector3.ClampMagnitude(forward * MoveInput.y + right * MoveInput.x, 1f);
        if (alive && firstPerson)
        {
            // Mouse yaw always defines forward; A/D strafe and S backs up.
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            turnVelocity = 0f;
        }
        else if (alive && (aiming || direction.sqrMagnitude > 0.0001f))
        {
            // In aim mode S walks backward and A/D strafe without turning the whole body.
            Vector3 facing = aiming ? forward : direction;
            float targetYaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref turnVelocity,
                aiming ? turnSmoothTime * 0.65f : turnSmoothTime, rotationDegreesPerSecond, dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        bool crouching = stance != null && stance.IsCrouching;
        IsSprinting = inputAllowed && !aiming && !crouching && MoveInput.sqrMagnitude > 0.001f &&
            (!firstPerson || MoveInput.y > .1f) && Input.GetKey(sprintKey);
        float speed = aiming ? aim.AimMoveSpeed : IsSprinting ? sprintSpeed : inputAllowed && Input.GetKey(runKey) ? runSpeed : walkSpeed;
        if(crouching)speed=aiming?stance.CrouchedAimSpeed:stance.CrouchedSpeed;
        // Turn into a new direction before accelerating, rather than sliding sideways at full speed.
        if(!firstPerson&&!aiming&&direction.sqrMagnitude>.001f)
            speed*=Mathf.Lerp(.15f,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.2f,.95f,Vector3.Dot(transform.forward,direction))));
        Vector3 targetVelocity = direction * speed;
        float rate = targetVelocity.sqrMagnitude < smoothedVelocity.sqrMagnitude ||
            Vector3.Dot(targetVelocity, smoothedVelocity) < 0f ? braking : acceleration;
        smoothedVelocity = inputAllowed ? Vector3.MoveTowards(smoothedVelocity, targetVelocity, rate * dt) : Vector3.zero;
        if (inputAllowed && IsGrounded && !crouching && (stance==null||stance.Blend<.1f) && Input.GetKeyDown(jumpKey))
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            IsGrounded = false;
        }

        verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, -50f);
        Vector3 before = transform.position;
        CollisionFlags flags = characterController.Move((smoothedVelocity + Vector3.up * verticalVelocity) * dt);
        PlanarVelocity = Vector3.ProjectOnPlane((transform.position - before) / dt, Vector3.up);
        PlanarSpeed = PlanarVelocity.magnitude;
        // Feed collisions back into the motor so it does not retain speed against a wall.
        if ((flags & CollisionFlags.Sides) != 0)
            smoothedVelocity = PlanarVelocity;
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
        IsGrounded = verticalVelocity <= 0f && (((flags & CollisionFlags.Below) != 0) || ProbeGround());
        if (IsGrounded)
            verticalVelocity = -2f;
        IsSprinting &= IsGrounded && PlanarSpeed > walkSpeed;
    }

    private void OnDisable()
    {
        smoothedVelocity = PlanarVelocity = Vector3.zero;
        PlanarSpeed = turnVelocity = verticalVelocity = 0f;
        MoveInput = Vector2.zero;
        IsSprinting = false;
    }

    private bool ProbeGround()
    {
        Vector3 point = groundCheck != null ? groundCheck.position : transform.position + Vector3.up * 0.08f;
        // Start above the feet and inspect the contact normal: side walls are not ground.
        Vector3 origin = point + Vector3.up * (groundProbeRadius + 0.04f);
        int count = Physics.SphereCastNonAlloc(origin, groundProbeRadius, Vector3.down, groundHits, 0.16f, groundLayers, QueryTriggerInteraction.Ignore);
        float minimumNormalY = Mathf.Cos(characterController.slopeLimit * Mathf.Deg2Rad);
        for (int i = 0; i < count; i++)
        {
            Collider hit = groundHits[i].collider;
            if (hit == null || hit == characterController || hit.transform.IsChildOf(transform))
                continue;
            if (groundHits[i].normal.y >= minimumNormalY)
                return true;
        }

        return false;
    }

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0.01f, walkSpeed);
        runSpeed = Mathf.Max(walkSpeed, runSpeed);
        sprintSpeed = Mathf.Max(runSpeed, sprintSpeed);
        gravity = Mathf.Min(-0.1f, gravity);
        groundProbeRadius = Mathf.Max(0.01f, groundProbeRadius);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;
        Gizmos.color = IsGrounded ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundProbeRadius);
    }
}
```


## AIDebugPresenter

Full file path: D:/GV project/Assets/Scripts/UI/AIDebugPresenter.cs

```csharp
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>F3 opt-in viva display. Aiming at an enemy selects it; otherwise inspect the nearest living agent.</summary>
public sealed class AIDebugPresenter : MonoBehaviour
{
    [SerializeField]
    private EnemyWorld world;
    [SerializeField]
    private Camera viewCamera;
    [SerializeField]
    private GameObject panel;
    [SerializeField]
    private TMP_Text output;
    [SerializeField]
    private bool visible;
    private float nextRefresh;
    private readonly StringBuilder text = new StringBuilder(1800);
    private void OnEnable()
    {
        output.enableAutoSizing = true;
        output.fontSizeMin = 15;
        output.fontSizeMax = 22;
        panel.SetActive(visible);
        nextRefresh = 0;
    }

    private void OnDisable()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3))
        {
            visible = !visible;
            panel.SetActive(visible);
            nextRefresh = 0;
        }

        if (!visible || Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + .25f;
        EnemyActor selected = null;
        if (Physics.Raycast(viewCamera.ViewportPointToRay(new Vector3(.5f, .5f)), out var hit, 100, 1 << 9, QueryTriggerInteraction.Ignore))
            selected = hit.collider.GetComponentInParent<EnemyActor>();
        if (selected == null)
        {
            float nearest = float.PositiveInfinity;
            foreach (var actor in world.Living)
            {
                if (actor == null)
                    continue;
                float distance = (actor.transform.position - world.player.transform.position).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    selected = actor;
                }
            }
        }

        text.Clear();
        text.AppendLine("AI INSPECTOR  /  F3 TO CLOSE");
        text.Append("Living: ").Append(world.Living.Count).AppendLine("   Aim at an agent to inspect");
        if (selected == null)
        {
            text.AppendLine("Waiting for a living agent.");
            output.text = text.ToString();
            return;
        }

        var senses = selected.Perception;
        text.AppendLine(selected.name.Replace("(Clone)", ""));
        text.AppendLine(selected.Decision);
        text.Append("Health ").Append(selected.Health.Current.ToString("0")).Append(" | Distance ")
            .Append(senses.HasMemory ? senses.Distance.ToString("0.0") + " m" : "unknown").AppendLine();
        text.Append("LOS ").Append(senses.Visible).Append(" | Heard ").Append(senses.Heard).AppendLine();
        text.Append("Player weakness ").Append(senses.PlayerWeakness.ToString("0.00")).Append(" | ").Append(senses.Weapon).AppendLine();
        text.Append("Player firing ").Append(senses.PlayerFiring).AppendLine();
        text.Append(senses.Knowledge).Append(" | Memory ").Append(senses.MemoryRemaining.ToString("0.0")).AppendLine(" s");
        text.Append("Observed reload ").Append(senses.PlayerReloading).Append(" | Rally ").Append(selected.IsEncouraged).AppendLine();
        text.Append(selected.Combat.Phase).Append(" | Cooldown ").Append(selected.Combat.CooldownRemaining.ToString("0.0")).AppendLine(" s");
        text.Append("Attack slot ").Append(world.HasAttackSlot(selected)).Append(" | Active ")
            .Append(world.ActiveAttackers).Append('/').Append(world.MaximumAttackers).AppendLine();
        text.Append("Navigation: ").Append(selected.Movement.Status).Append(" | Retries ").Append(selected.Movement.RetryCount).AppendLine();
        if (selected.TryGetComponent<HunterBrain>(out var hunter))
        {
            text.AppendLine("MEMBER 1 / A*  f = g + h");
            text.Append("Expanded ").Append(hunter.ExpandedNodes).Append(" | Path cost ").Append(hunter.PathCost.ToString("0.0")).AppendLine();
        }

        if (selected.TryGetComponent<RunnerBrain>(out var runner))
        {
            text.AppendLine("MEMBER 2 / UTILITY + HYSTERESIS");
            text.Append("Reachable ").Append(runner.PathAvailable).Append(" | Shotgun threat ").Append(runner.ShotgunThreat.ToString("0.00")).AppendLine();
            Scores(new[] { "Chase", "Attack", "Caution", "Flank", "Disengage" }, runner.Scores);
        }

        if (selected.TryGetComponent<AmbusherBrain>(out var ambusher))
        {
            text.AppendLine("MEMBER 3 / TACTICAL POSITION SCORING");
            text.Append("Reserved ").Append(ambusher.Selected != null ? ambusher.Selected.name : "none").AppendLine();
            if (ambusher.PointScores != null)
                for (int i = 0, shown = 0; i < ambusher.PointScores.Length && shown < 5; i++)
                    if (world.tacticalPoints[i] != null)
                    {
                        text.Append(world.tacticalPoints[i].name).Append("  ").Append(ambusher.PointScores[i].ToString("0.00")).AppendLine();
                        shown++;
                    }
        }

        if (selected.TryGetComponent<EliteBrain>(out var elite))
        {
            text.AppendLine("MEMBER 4 / HIERARCHY + UTILITY");
            text.Append("Nearby allies ").Append(elite.NearbyAllies).AppendLine();
            text.Append("Cover ").Append(elite.Cover != null ? elite.Cover.name : "none").AppendLine();
            Scores(new[] { "Direct", "Heavy", "Push", "Distance", "Reposition" }, elite.Scores);
        }

        output.text = text.ToString();
    }

    private void Scores(string[] names, float[] values)
    {
        for (int i = 0; i < values.Length; i++)
            text.Append(names[i]).Append("  ").Append(values[i].ToString("0.00")).AppendLine();
    }
}
```


## GamePanelsPresenter

Full file path: D:/GV project/Assets/Scripts/UI/GamePanelsPresenter.cs

```csharp
using UnityEngine;

/// <summary>Shows one modal panel at a time and wires explicit button callbacks.</summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class GamePanelsPresenter : MonoBehaviour
{
    [SerializeField]
    private GameUIController screens;
    [SerializeField]
    private UnityEngine.EventSystems.EventSystem eventSystem;
    [SerializeField]
    private GameObject pausePanel;
    [SerializeField]
    private GameObject gameOverPanel;
    [SerializeField]
    private GameObject victoryPanel;
    [SerializeField]
    private UnityEngine.UI.Button resumeButton;
    [SerializeField]
    private UnityEngine.UI.Button pauseRestartButton;
    [SerializeField]
    private UnityEngine.UI.Button gameOverRestartButton;
    [SerializeField]
    private UnityEngine.UI.Button victoryRestartButton;
    private bool subscribed;
    [SerializeField]
    private UnityEngine.UI.Button[] menuButtons = System.Array.Empty<UnityEngine.UI.Button>();
    private void OnEnable()
    {
        if (screens == null || eventSystem == null || pausePanel == null || gameOverPanel == null || victoryPanel == null || resumeButton == null || pauseRestartButton == null || gameOverRestartButton == null || victoryRestartButton == null)
        {
            Debug.LogError("GamePanelsPresenter needs its controller, EventSystem, panels and buttons.", this);
            enabled = false;
            return;
        }

        screens.StateChanged += Show;
        resumeButton.onClick.AddListener(screens.Resume);
        pauseRestartButton.onClick.AddListener(screens.RestartLevel);
        gameOverRestartButton.onClick.AddListener(screens.RestartLevel);
        victoryRestartButton.onClick.AddListener(screens.RestartLevel);
        foreach (var button in menuButtons)
            if (button != null)
                button.onClick.AddListener(screens.ReturnToMenu);
        subscribed = true;
        Show(screens.State);
    }

    private void OnDisable()
    {
        if (!subscribed)
            return;
        screens.StateChanged -= Show;
        resumeButton.onClick.RemoveListener(screens.Resume);
        pauseRestartButton.onClick.RemoveListener(screens.RestartLevel);
        gameOverRestartButton.onClick.RemoveListener(screens.RestartLevel);
        victoryRestartButton.onClick.RemoveListener(screens.RestartLevel);
        foreach (var button in menuButtons)
            if (button != null)
                button.onClick.RemoveListener(screens.ReturnToMenu);
        subscribed = false;
    }

    private void Show(GameScreenState state)
    {
        pausePanel.SetActive(state == GameScreenState.Paused);
        gameOverPanel.SetActive(state == GameScreenState.GameOver);
        victoryPanel.SetActive(state == GameScreenState.Victory);
        UnityEngine.UI.Button first = state == GameScreenState.Paused ? resumeButton : state == GameScreenState.GameOver ? gameOverRestartButton : state == GameScreenState.Victory ? victoryRestartButton : null;
        eventSystem.SetSelectedGameObject(null);
        if (first != null)
            eventSystem.SetSelectedGameObject(first.gameObject);
    }
}
```


## GameStatusPresenter

Full file path: D:/GV project/Assets/Scripts/UI/GameStatusPresenter.cs

```csharp
using TMPro;
using UnityEngine;

/// <summary>Event-driven wave, equipment and interaction UI. Only the timed announcement expires in Update.</summary>
public sealed class GameStatusPresenter : MonoBehaviour
{
    [SerializeField]
    private WaveManager waves;
    [SerializeField]
    private PlayerEquipment equipment;
    [SerializeField]
    private PlayerInteractor interaction;
    [SerializeField]
    private TMP_Text waveText, remainingText, medkitText, grenadeText, promptText, announcementText;
    private float hideAt;
    private string previousAnnouncement;
    private void Awake()
    {
        if (waves != null && equipment != null && interaction != null && waveText != null && remainingText != null && medkitText != null && grenadeText != null && promptText != null && announcementText != null)
            return;
        Debug.LogError("GameStatusPresenter requires its wave, equipment, interaction and text references.", this);
        enabled = false;
    }

    private void OnEnable()
    {
        waves.Changed += RefreshWave;
        equipment.Changed += RefreshEquipment;
        interaction.PromptChanged += RefreshPrompt;
        Refresh();
    }

    private void Start() => Refresh();
    private void OnDisable()
    {
        if (waves != null)
            waves.Changed -= RefreshWave;
        if (equipment != null)
            equipment.Changed -= RefreshEquipment;
        if (interaction != null)
            interaction.PromptChanged -= RefreshPrompt;
    }

    private void Refresh()
    {
        RefreshWave();
        RefreshEquipment(equipment.Medkits, equipment.Grenades);
        RefreshPrompt(interaction.Prompt);
    }

    private void RefreshWave()
    {
        waveText.text = waves.CurrentWave == 0 ? "--" : waves.CurrentWave.ToString();
        remainingText.text = waves.Remaining.ToString();
        if (previousAnnouncement == waves.Announcement)
            return;
        previousAnnouncement = waves.Announcement;
        announcementText.text = waves.Announcement;
        hideAt = Time.time + 4;
    }

    private void RefreshEquipment(int medkits, int grenades)
    {
        medkitText.text = "MED " + medkits;
        grenadeText.text = "FRAG " + grenades;
    }

    private void RefreshPrompt(string value) => promptText.text = value;
    private void Update()
    {
        if (hideAt > 0 && Time.time >= hideAt)
        {
            announcementText.text = "";
            hideAt = 0;
        }
    }
}
```


## GameUIController

Full file path: D:/GV project/Assets/Scripts/UI/GameUIController.cs

```csharp
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameScreenState
{
    Playing,
    Paused,
    GameOver,
    Victory
}

/// <summary>Owns UI screen state, pause time/audio and cursor. Contains no wave or victory-condition logic.</summary>
[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public sealed class GameUIController : MonoBehaviour
{
    [SerializeField]
    private PlayerHealth health;
    [SerializeField]
    private PlayerAimController aim;
    [SerializeField]
    private ThirdPersonCameraController cameraRig;
    [SerializeField]
    private bool pauseOnFocusLoss = true;
    private bool ownsFreeze;
    private float previousTimeScale;
    private bool previousAudioPause;
    private bool restarting;
    public GameScreenState State { get; private set; } = GameScreenState.Playing;
    public bool IsPlaying => State == GameScreenState.Playing;

    public event Action<GameScreenState> StateChanged;
    private void Awake()
    {
        if (health != null && aim != null && cameraRig != null)
            return;
        Debug.LogError("GameUIController requires PlayerHealth, PlayerAimController and the camera rig.", this);
        enabled = false;
    }

    private void OnEnable()
    {
        if (health == null || aim == null || cameraRig == null)
            return;
        health.Died += ShowGameOver;
        cameraRig.SetCursorManagedByUI(true);
        ApplyState();
    }

    private void Start()
    {
        if (health != null && health.IsDead)
            ShowGameOver();
    }

    private void OnDisable()
    {
        if (health != null)
            health.Died -= ShowGameOver;
        ReleaseFreeze();
        if (aim != null)
            aim.SetInputSuppressed(false);
        if (cameraRig != null)
        {
            cameraRig.SetCursorManagedByUI(false);
            cameraRig.SetGameplayInputEnabled(health != null && !health.IsDead);
        }
    }

    private void Update()
    {
        // Input and EventSystem Update still run while timeScale is zero.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (State == GameScreenState.Playing)
                Pause();
            else if (State == GameScreenState.Paused)
                Resume();
        }
    }

    public void Pause()
    {
        if (State == GameScreenState.Playing)
            ChangeState(GameScreenState.Paused);
    }

    public void Resume()
    {
        if (State != GameScreenState.Paused)
            return;
        if (health.IsDead)
            ShowGameOver();
        else
            ChangeState(GameScreenState.Playing);
    }

    public void ShowGameOver()
    {
        // A completed victory screen remains terminal until restart.
        if (State != GameScreenState.Victory)
            ChangeState(GameScreenState.GameOver);
    }

    public void ShowVictory()
    {
        if (health != null && !health.IsDead && State != GameScreenState.GameOver)
            ChangeState(GameScreenState.Victory);
    }

    private void ChangeState(GameScreenState next)
    {
        if (!isActiveAndEnabled || State == next || restarting)
            return;
        State = next;
        ApplyState();
        StateChanged?.Invoke(State);
    }

    private void ApplyState()
    {
        if (IsPlaying)
            ReleaseFreeze();
        else if (!ownsFreeze)
        {
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            ownsFreeze = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        aim.SetInputSuppressed(!IsPlaying);
        cameraRig.SetGameplayInputEnabled(IsPlaying);
    }

    private void ReleaseFreeze()
    {
        if (!ownsFreeze)
            return;
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
        ownsFreeze = false;
    }

    public void RestartLevel()
    {
        if (!isActiveAndEnabled || restarting)
            return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex < 0)
        {
            Debug.LogError("Add ZombieMap to Build Profiles > Scene List before using Restart.", this);
            return;
        }

        restarting = true;
        ReleaseFreeze();
        SceneManager.LoadScene(scene.buildIndex);
    }

    public void ReturnToMenu()
    {
        if (restarting)
            return;
        restarting = true;
        ReleaseFreeze();
        Time.timeScale = 1;
        AudioListener.pause = false;
        SceneManager.LoadScene("MainMenu");
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && pauseOnFocusLoss && isActiveAndEnabled)
            Pause();
    }

    [ContextMenu("Phase 4/Preview Victory (Play Mode)")]
    private void PreviewVictory()
    {
        if (Application.isPlaying)
            ShowVictory();
    }

    [ContextMenu("Phase 4/Preview Game Over (Play Mode)")]
    private void PreviewGameOver()
    {
        if (Application.isPlaying && health != null)
            health.Die();
    }
}
```


## LoadoutMenuController

Full file path: D:/GV project/Assets/Scripts/UI/LoadoutMenuController.cs

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public sealed class LoadoutMenuController : MonoBehaviour
{
    [SerializeField]
    private LoadoutCatalog catalog;
    [SerializeField]
    private bool isLoadout;
    [SerializeField]
    private UnityEngine.UI.Button startButton;
    [SerializeField]
    private UnityEngine.UI.Button backButton;
    [SerializeField]
    private UnityEngine.UI.Button quitButton;
    [SerializeField]
    private UnityEngine.UI.Button[] primaryButtons = System.Array.Empty<UnityEngine.UI.Button>();
    [SerializeField]
    private UnityEngine.UI.Button[] equipmentButtons = System.Array.Empty<UnityEngine.UI.Button>();
    [SerializeField]
    private TMP_Text selectionText;
    private LoadoutSession session;
    private readonly List<KeyValuePair<UnityEngine.UI.Button, UnityAction>> listeners = new List<KeyValuePair<UnityEngine.UI.Button, UnityAction>>();
    private void OnEnable()
    {
        if (catalog == null || startButton == null)
        {
            Debug.LogError("Loadout menu requires a catalog and Start button.", this);
            enabled = false;
            return;
        }

        Time.timeScale = 1;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        session = LoadoutSession.Ensure(catalog);
        Bind(startButton, StartGame);
        if (backButton != null)
            Bind(backButton, Back);
        if (quitButton != null)
            Bind(quitButton, Application.Quit);
        for (int i = 0; i < primaryButtons.Length; i++)
        {
            int selected = i;
            Bind(primaryButtons[i], () =>
            {
                session.Select(selected, session.Equipment);
                Refresh();
            });
        }

        for (int i = 0; i < equipmentButtons.Length; i++)
        {
            int selected = i;
            Bind(equipmentButtons[i], () =>
            {
                session.Select(session.Primary, selected);
                Refresh();
            });
        }

        Refresh();
    }

    private void Bind(UnityEngine.UI.Button button, UnityAction action)
    {
        button.onClick.AddListener(action);
        listeners.Add(new KeyValuePair<UnityEngine.UI.Button, UnityAction>(button, action));
    }

    private void OnDisable()
    {
        foreach (var entry in listeners)
            if (entry.Key != null)
                entry.Key.onClick.RemoveListener(entry.Value);
        listeners.Clear();
    }

    private void Refresh()
    {
        startButton.interactable = !isLoadout || session.IsValid;
        if (!isLoadout)
            return;
        selectionText.text = session.IsValid ? catalog.primaries[session.Primary].WeaponName.ToUpperInvariant() + " + PISTOL\n" + catalog.equipment[session.Equipment].name : "SELECT A VALID LOADOUT";
        for (int i = 0; i < primaryButtons.Length; i++)
            primaryButtons[i].interactable = i != session.Primary;
        for (int i = 0; i < equipmentButtons.Length; i++)
            equipmentButtons[i].interactable = i != session.Equipment;
    }

    public void StartGame()
    {
        if (!isLoadout)
            SceneManager.LoadScene("Loadout");
        else if (session.IsValid)
            SceneManager.LoadScene("ZombieMap");
    }

    public void Back() => SceneManager.LoadScene("MainMenu");
}
```


## PlayerHudPresenter

Full file path: D:/GV project/Assets/Scripts/UI/PlayerHudPresenter.cs

```csharp
using TMPro;
using UnityEngine;

/// <summary>Event-driven HUD. Serialized references only; no Update, polling or scene searches.</summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class PlayerHudPresenter : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField]
    private PlayerHealth health;
    [SerializeField]
    private PlayerWeaponController weapons;
    [SerializeField]
    private PlayerAimController aim;
    [SerializeField]
    private GameUIController screens;
    [Header("Views")]
    [SerializeField]
    private GameObject crosshair;
    [SerializeField]
    private UnityEngine.UI.Image healthFill;
    [SerializeField]
    private TMP_Text healthText;
    [SerializeField]
    private TMP_Text weaponText;
    [SerializeField]
    private TMP_Text magazineText;
    [SerializeField]
    private TMP_Text reserveText;
    [SerializeField]
    private GameObject reloadingIndicator;
    [SerializeField]
    private CanvasGroup hudGroup;
    [Header("Health colours")]
    [SerializeField]
    private Color healthyColor = new Color(0.28f, 0.88f, 0.72f);
    [SerializeField]
    private Color lowHealthColor = new Color(1f, 0.32f, 0.3f);
    [SerializeField, Range(0f, 1f)]
    private float lowHealthFraction = 0.25f;
    private bool subscribed;
    private void OnEnable()
    {
        if (health == null || weapons == null || aim == null || screens == null || crosshair == null || healthFill == null || healthText == null || weaponText == null || magazineText == null || reserveText == null || reloadingIndicator == null || hudGroup == null)
        {
            Debug.LogError("PlayerHudPresenter is missing a source or UI reference. Run Phase 4 setup.", this);
            enabled = false;
            return;
        }

        health.HealthChanged += OnHealthChanged;
        health.Died += OnDied;
        weapons.WeaponChanged += OnWeaponChanged;
        weapons.AmmoChanged += OnAmmoChanged;
        weapons.ReloadStateChanged += OnReloadChanged;
        aim.AimChanged += OnAimChanged;
        screens.StateChanged += OnScreenChanged;
        subscribed = true;
        RefreshAll();
    }

    // Read again after every scene object's Awake. Covers initial values emitted before subscription.
    private void Start() => RefreshAll();
    private void OnDisable()
    {
        if (!subscribed)
            return;
        health.HealthChanged -= OnHealthChanged;
        health.Died -= OnDied;
        weapons.WeaponChanged -= OnWeaponChanged;
        weapons.AmmoChanged -= OnAmmoChanged;
        weapons.ReloadStateChanged -= OnReloadChanged;
        aim.AimChanged -= OnAimChanged;
        screens.StateChanged -= OnScreenChanged;
        subscribed = false;
    }

    public void RefreshAll()
    {
        if (!subscribed)
            return;
        OnHealthChanged(health.CurrentHealth, health.MaxHealth);
        OnWeaponChanged(weapons.CurrentWeapon);
        OnAmmoChanged(weapons.MagazineAmmo, weapons.ReserveAmmo);
        OnReloadChanged(weapons.IsReloading);
        OnScreenChanged(screens.State);
    }

    private void OnHealthChanged(float current, float maximum)
    {
        float fraction = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        healthFill.fillAmount = fraction;
        healthFill.color = fraction <= lowHealthFraction ? lowHealthColor : healthyColor;
        healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
        RefreshCrosshair();
    }

    private void OnWeaponChanged(WeaponData data)
    {
        weaponText.text = data != null ? data.WeaponName.ToUpperInvariant() : "UNARMED";
        OnAmmoChanged(weapons.MagazineAmmo, weapons.ReserveAmmo);
        OnReloadChanged(weapons.IsReloading);
    }

    private void OnAmmoChanged(int magazine, int reserve)
    {
        magazineText.text = magazine.ToString("00");
        reserveText.text = reserve.ToString("000");
    }

    private void OnReloadChanged(bool reloading)
    {
        reloadingIndicator.SetActive(reloading);
        RefreshCrosshair();
    }
    private void OnAimChanged(bool aiming) => RefreshCrosshair();
    private void OnDied() => RefreshCrosshair();
    private void OnScreenChanged(GameScreenState state)
    {
        hudGroup.alpha = state == GameScreenState.Playing ? 1f : 0f;
        RefreshCrosshair();
    }

    private void RefreshCrosshair()
    {
        crosshair.SetActive((aim.IsAiming || weapons.IsFirstPerson) && weapons.CurrentWeapon != null &&
            !weapons.IsReloading && !health.IsDead && health.CurrentHealth > 0f && screens.IsPlaying);
    }
}
```


## WaveDefinition

Full file path: D:/GV project/Assets/Scripts/Waves/WaveDefinition.cs

```csharp
using System;
using UnityEngine;

[Serializable]
public sealed class WaveEntry
{
    public EnemyActor prefab;
    [Min(1)]
    public int count = 1;
}

[CreateAssetMenu(menuName = "Zombie Outbreak/Wave Definition")]
public sealed class WaveDefinition : ScriptableObject
{
    public string title;
    [Min(.05f)]
    public float spawnInterval = .8f;
    public WaveEntry[] entries;
    public int Total
    {
        get
        {
            int total = 0;
            if (entries != null)
                foreach (var entry in entries)
                    if (entry != null)
                        total += Mathf.Max(0, entry.count);
            return total;
        }
    }

    public bool Valid
    {
        get
        {
            if (entries == null || entries.Length == 0)
                return false;
            foreach (var e in entries)
                if (e == null || e.prefab == null || e.count < 1)
                    return false;
            return true;
        }
    }
}
```


## WaveManager

Full file path: D:/GV project/Assets/Scripts/Waves/WaveManager.cs

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum WaveState
{
    Waiting,
    Spawning,
    Combat,
    Intermission,
    Victory,
    Stopped
}

/// <summary>Tracks only its own spawned enemies through death events; never scans the scene.</summary>
public sealed class WaveManager : MonoBehaviour
{
    [SerializeField]
    private WaveDefinition[] waves;
    [SerializeField]
    private MapMarker[] entrances;
    [SerializeField]
    private EnemyWorld world;
    [SerializeField]
    private GameUIController screens;
    [SerializeField]
    private float initialDelay = 6;
    [SerializeField]
    private float betweenWaves = 7;
    [SerializeField]
    private int supplyMagazines = 2;
    private readonly HashSet<EnemyHealth> living = new HashSet<EnemyHealth>();
    private Coroutine sequence;
    private int unspawned, spawnCursor;
    private bool spawningDone;
    public int CurrentWave { get; private set; }
    public int Remaining => living.Count + unspawned;
    public int LivingCount => living.Count;
    public WaveState State { get; private set; }
    public string Announcement { get; private set; } = "PREPARE TO SURVIVE";

    public event Action Changed;
    public event Action<EnemyActor> EnemySpawned;
    private void OnEnable()
    {
        if (world != null && world.player != null)
            world.player.Died += StopProgression;
    }

    private void Start()
    {
        if (world == null || screens == null || entrances == null || entrances.Length == 0 || waves == null || waves.Length != 5)
        {
            Debug.LogError("WaveManager needs five waves, entrances, EnemyWorld and GameUI.", this);
            enabled = false;
            return;
        }

        foreach (var w in waves)
            if (w == null || !w.Valid)
            {
                Debug.LogError("Invalid WaveDefinition", this);
                enabled = false;
                return;
            }

        sequence = StartCoroutine(Run());
    }

    private void OnDisable()
    {
        if (world != null && world.player != null)
            world.player.Died -= StopProgression;
        if (sequence != null)
            StopCoroutine(sequence);
        foreach (var enemy in living)
            if (enemy != null)
                enemy.Died -= OnDeath;
        living.Clear();
    }

    private IEnumerator Run()
    {
        State = WaveState.Waiting;
        Changed?.Invoke();
        yield return new WaitForSeconds(initialDelay);
        for (int index = 0; index < waves.Length; index++)
        {
            if (world.player.IsDead)
                yield break;
            var wave = waves[index];
            CurrentWave = index + 1;
            unspawned = wave.Total;
            spawningDone = false;
            State = WaveState.Spawning;
            Announcement = "WAVE " + CurrentWave + " / " + wave.title;
            Changed?.Invoke();
            int[] pending = new int[wave.entries.Length];
            for (int i = 0; i < pending.Length; i++)
                pending[i] = wave.entries[i].count;
            int failures = 0;
            while (unspawned > 0 && !world.player.IsDead)
            {
                int slot = UnityEngine.Random.Range(0, pending.Length);
                while (pending[slot] == 0)
                    slot = (slot + 1) % pending.Length;
                if (!TrySpawn(wave.entries[slot].prefab))
                {
                    if (++failures >= 10)
                    {
                        State = WaveState.Stopped;
                        Announcement = "SPAWN ROUTES BLOCKED";
                        Changed?.Invoke();
                        Debug.LogError("All spawn entrances are blocked. Inspect entrance markers.", this);
                        yield break;
                    }

                    yield return new WaitForSeconds(.5f);
                    continue;
                }

                failures = 0;
                pending[slot]--;
                unspawned--;
                Changed?.Invoke();
                yield return new WaitForSeconds(wave.spawnInterval);
            }

            spawningDone = true;
            State = WaveState.Combat;
            Changed?.Invoke();
            while (living.Count > 0 && !world.player.IsDead)
                yield return null;
            if (world.player.IsDead)
                yield break;
            if (index == waves.Length - 1)
            {
                State = WaveState.Victory;
                Announcement = "DISTRICT SECURED";
                Changed?.Invoke();
                screens.ShowVictory();
                yield break;
            }

            State = WaveState.Intermission;
            Announcement = "WAVE CLEAR / AMMO SUPPLY RECEIVED";
            world.weapons.GrantReserveMagazines(supplyMagazines);
            Changed?.Invoke();
            yield return new WaitForSeconds(betweenWaves);
        }
    }

    private bool TrySpawn(EnemyActor prefab)
    {
        for (int offset = 0; offset < entrances.Length; offset++)
        {
            int index = (spawnCursor + offset) % entrances.Length;
            var marker = entrances[index];
            if (marker == null)
                continue;
            if (Vector3.Distance(marker.transform.position, world.player.transform.position) < 8)
                continue;
            if (!NavMesh.SamplePosition(marker.transform.position, out var hit, 2, NavMesh.AllAreas))
                continue;
            if (Physics.CheckSphere(hit.position + Vector3.up, .55f, (1 << 8) | (1 << 9), QueryTriggerInteraction.Ignore))
                continue;
            var actor = Instantiate(prefab, hit.position, marker.transform.rotation);
            actor.Initialize(world);
            living.Add(actor.Health);
            actor.Health.Died += OnDeath;
            spawnCursor = (index + 1) % entrances.Length;
            EnemySpawned?.Invoke(actor);
            return true;
        }

        return false;
    }

    private void OnDeath(EnemyHealth enemy)
    {
        if (!living.Remove(enemy))
            return;
        enemy.Died -= OnDeath;
        Changed?.Invoke();
        // Run waits for both spawningDone and zero living; early kills never skip queued spawns.
        if (spawningDone && living.Count == 0)
            Announcement = "WAVE CLEAR";
    }

    private void StopProgression()
    {
        if (sequence != null)
            StopCoroutine(sequence);
        sequence = null;
        State = WaveState.Stopped;
        Announcement = "OUTBREAK OVERRAN THE DISTRICT";
        Changed?.Invoke();
    }
}
```


## PlayerWeaponController

Full file path: D:/GV project/Assets/Scripts/Weapons/PlayerWeaponController.cs

```csharp
using System;
using UnityEngine;

/// <summary>One shooter for all four weapons. LateUpdate fires after camera and animated hand updates.</summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAimController), typeof(PlayerHealth))]
public sealed class PlayerWeaponController : MonoBehaviour
{
    [Header("Phase 2 references")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private ThirdPersonCameraController cameraRig;
    [SerializeField] private PlayerAimController aim;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerAnimatorDriver animationDriver;
    [SerializeField] private Transform weaponSocket;
    [Tooltip("A point inside the player's upper body; checks for a barrel pushed through cover.")]
    [SerializeField] private Transform shotOrigin;
    [SerializeField] private Transform aimTarget;

    [Header("Fixed loadout: Rifle, SMG, Shotgun, Pistol")]
    [SerializeField] private WeaponData[] loadout = Array.Empty<WeaponData>();
    [SerializeField, Min(0)] private int startingWeapon;
    [SerializeField] private LayerMask hitLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.001f)] private float muzzleClearance = 0.025f;
    [SerializeField, Min(0.01f)] private float firingActivitySeconds = 0.2f;
    [SerializeField] private bool drawShotRays = true;

    private WeaponAmmoState[] ammo;
    private WeaponView[] views;
    private float[] bloom;
    private WeaponHitscan hitscan;
    private PlayerStanceController stance;
    private FirstPersonWeaponRig firstPersonRig;
    public bool IsFirstPerson => cameraRig != null && cameraRig.IsFirstPerson;
    private bool FiringPoseAllowed => IsFirstPerson ? firstPersonRig != null && !firstPersonRig.IsSprinting : aim.IsAiming;
    private Vector3 GuardPosition => IsFirstPerson ? viewCamera.transform.position : shotOrigin.position-Vector3.up*(stance!=null?.38f*stance.Blend:0);
    private int selected = -1;
    private bool fireRequested;
    private bool triggerArmed;
    private bool initialized;

    public WeaponData CurrentWeapon => selected >= 0 ? loadout[selected] : null;
    public WeaponCategory CurrentWeaponCategory => CurrentWeapon != null ? CurrentWeapon.Category : WeaponCategory.None;
    public int CurrentWeaponIndex => selected;
    public int MagazineAmmo => CurrentAmmo != null ? CurrentAmmo.Magazine : 0;
    public int ReserveAmmo => CurrentAmmo != null ? CurrentAmmo.Reserve : 0;
    public bool IsReloading => CurrentAmmo != null && CurrentAmmo.IsReloading;
    public bool IsFiring => isActiveAndEnabled && health != null && !health.IsDead &&
        Time.timeAsDouble - LastShotTime <= firingActivitySeconds;
    public double LastShotTime { get; private set; } = double.NegativeInfinity;
    public Vector3 LastShotPosition { get; private set; }
    public Vector3 CurrentAimPoint { get; private set; }
    public bool LastShotBlocked { get; private set; }
    public event Action<WeaponData> WeaponChanged;
    public event Action<int, int> AmmoChanged;
    public event Action<bool> ReloadStateChanged;
    public event Action<WeaponShotInfo> ShotFired;
    public event Action<RaycastHit, float> HitDetected;
    private WeaponAmmoState CurrentAmmo => selected >= 0 && ammo != null ? ammo[selected] : null;
    private WeaponView CurrentView => selected >= 0 && views != null ? views[selected] : null;
    public WeaponView ActiveView => CurrentView;

    public bool ConfigureLoadout(WeaponData[] selection)
    {
        if (initialized || selection == null || selection.Length == 0) return false;
        foreach (var data in selection) if (data == null || data.WeaponPrefab == null) return false;
        loadout = (WeaponData[])selection.Clone(); startingWeapon = 0; return true;
    }

    public void GrantReserveMagazines(int magazines)
    {
        if (!initialized || magazines <= 0) return;
        for (int i=0;i<ammo.Length;i++) ammo[i].AddReserve(loadout[i].MagazineSize * magazines);
        AmmoChanged?.Invoke(MagazineAmmo, ReserveAmmo);
    }

    private void Awake()
    {
        stance=GetComponent<PlayerStanceController>();
        firstPersonRig=GetComponent<FirstPersonWeaponRig>();
        if (aim == null) aim = GetComponent<PlayerAimController>();
        if (health == null) health = GetComponent<PlayerHealth>();
        if (animationDriver == null) animationDriver = GetComponent<PlayerAnimatorDriver>();
        if (viewCamera == null) viewCamera = Camera.main;
        if (cameraRig == null && viewCamera != null) cameraRig = viewCamera.GetComponent<ThirdPersonCameraController>();
        if (weaponSocket == null || shotOrigin == null || viewCamera == null || aim == null || health == null || loadout.Length == 0)
        {
            Debug.LogError("PlayerWeaponController needs its Phase 2 references and a non-empty weapon loadout.", this);
            enabled = false;
            return;
        }
        for (int i = 0; i < loadout.Length; i++)
        {
            if (loadout[i] == null || loadout[i].WeaponPrefab == null || loadout[i].WeaponPrefab.Muzzle == null)
            {
                Debug.LogError("Weapon slot " + i + " needs WeaponData, a WeaponView prefab and a Muzzle reference.", this);
                enabled = false;
                return;
            }
        }
        hitscan = new WeaponHitscan(transform, hitLayers);
        ammo = new WeaponAmmoState[loadout.Length];
        views = new WeaponView[loadout.Length];
        bloom = new float[loadout.Length];
        for (int i = 0; i < loadout.Length; i++)
        {
            WeaponData data = loadout[i];
            ammo[i] = new WeaponAmmoState(data.MagazineSize, data.ReserveAmmo, data.FireRate, data.ReloadTime);
            views[i] = Instantiate(data.WeaponPrefab, weaponSocket, false);
            views[i].name = data.WeaponName;
            views[i].Initialize(data);
            if (IsFirstPerson && firstPersonRig != null) firstPersonRig.ConfigureView(views[i]);
            views[i].gameObject.SetActive(false);
        }
        initialized = true;
        SelectWeapon(Mathf.Clamp(startingWeapon, 0, loadout.Length - 1));
    }

    private void OnEnable()
    {
        if (health != null) health.Died += OnDeath;
        if (CurrentView != null && health != null && !health.IsDead) CurrentView.gameObject.SetActive(true);
        triggerArmed = false;
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= OnDeath;
        CancelReload();
        if (CurrentView != null) CurrentView.gameObject.SetActive(false);
        fireRequested = false;
        triggerArmed = false;
    }

    private bool CanUseWeapons() => initialized && isActiveAndEnabled && !health.IsDead;
    private bool InputAllowed() => CanUseWeapons() && Application.isFocused &&
        Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0f;

    private void Update()
    {
        fireRequested = false;
        if (!CanUseWeapons()) return;
        for (int i = 0; i < bloom.Length; i++)
            bloom[i] = Mathf.MoveTowards(bloom[i], 0f, loadout[i].SpreadRecovery * Time.deltaTime);
        if (CurrentAmmo.TickReload(Time.timeAsDouble))
        {
            FinishReloadPresentation();
            AmmoChanged?.Invoke(MagazineAmmo, ReserveAmmo);
            ReloadStateChanged?.Invoke(false);
        }
        if (!InputAllowed()) { triggerArmed = false; return; }
        bool held = Input.GetMouseButton(0);
        if (!held) triggerArmed = true;
        if (cameraRig != null && Time.frameCount <= cameraRig.CursorLockFrame) triggerArmed = false;

        for (int i = 0; i < Mathf.Min(loadout.Length, 9); i++)
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) SelectWeapon(i);
        float wheel = Input.GetAxisRaw("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) > 0.01f)
            SelectWeapon((selected + (wheel > 0f ? 1 : loadout.Length - 1)) % loadout.Length);
        if (Input.GetKeyDown(KeyCode.R)) TryReload();
        fireRequested = triggerArmed && FiringPoseAllowed &&
            (CurrentWeapon.Automatic ? held : Input.GetMouseButtonDown(0));
    }

    private void LateUpdate()
    {
        if (!CanUseWeapons()) return;
        WeaponData data = CurrentWeapon;
        CurrentAimPoint = hitscan.AimPoint(viewCamera, data.Range, GuardPosition, out _);
        if (aimTarget != null) aimTarget.position = CurrentAimPoint;
        if (!IsFirstPerson) CurrentView.AimAt(CurrentAimPoint, aim.IsAiming && !IsReloading);
        if (!fireRequested || !InputAllowed() || !FiringPoseAllowed) return;
        fireRequested = false;
        TryFire();
    }

    /// <summary>Attempts one round. Enforces the firing stance, life, ammo and cooldown.</summary>
    public bool TryFire()
    {
        if (!CanUseWeapons() || !FiringPoseAllowed || Time.timeScale <= 0f) return false;
        CurrentAimPoint = hitscan.AimPoint(viewCamera, CurrentWeapon.Range, GuardPosition, out bool cameraBlocked);
        if (!IsFirstPerson) CurrentView.AimAt(CurrentAimPoint, !IsReloading);
        return Fire(CurrentWeapon, cameraBlocked);
    }

    public bool SelectWeapon(int index)
    {
        if (!CanUseWeapons() || index < 0 || index >= loadout.Length || index == selected) return false;
        CancelReload();
        if (animationDriver != null) animationDriver.CancelWeaponAction();
        if (CurrentView != null) CurrentView.gameObject.SetActive(false);
        selected = index;
        CurrentView.gameObject.SetActive(true);
        triggerArmed = false; // Release the trigger after switching; never fires an unexpected semi-auto shot.
        fireRequested = false;
        WeaponChanged?.Invoke(CurrentWeapon);
        AmmoChanged?.Invoke(MagazineAmmo, ReserveAmmo);
        return true;
    }

    public bool TryReload()
    {
        if (!CanUseWeapons() || Time.timeScale <= 0f || !CurrentAmmo.TryStartReload(Time.timeAsDouble)) return false;
        fireRequested = false;
        if (animationDriver != null) animationDriver.PlayReload(CurrentWeapon.ReloadTime);
        CurrentView.PlayReload();
        ReloadStateChanged?.Invoke(true);
        return true;
    }

    public void CancelReload()
    {
        if (!IsReloading) return;
        CurrentAmmo.CancelReload();
        FinishReloadPresentation();
        ReloadStateChanged?.Invoke(false);
    }

    private void FinishReloadPresentation()
    {
        if (CurrentView != null) CurrentView.StopReload();
        if (animationDriver != null) animationDriver.CancelWeaponAction();
    }

    private void OnDeath()
    {
        CancelReload();
        fireRequested = false;
        triggerArmed = false;
        if (CurrentView != null) CurrentView.gameObject.SetActive(false);
    }

    private bool Fire(WeaponData data, bool cameraBlocked)
    {
        if (!CurrentAmmo.TryFire(Time.timeAsDouble)) return false;
        Transform muzzle = CurrentView.Muzzle;
        Vector3 origin = muzzle.position;
        Vector3 direction = CurrentAimPoint - origin;
        Vector3 guard = origin - GuardPosition;
        // Raycasts starting inside colliders miss them. Also prevent a protruding barrel firing through a thin wall.
        LastShotBlocked = cameraBlocked || direction.sqrMagnitude < 0.0001f ||
            Vector3.Dot(direction, viewCamera.transform.forward) <= 0f ||
            hitscan.OverlapsWorld(origin, muzzleClearance) ||
            hitscan.OverlapsWorld(GuardPosition, muzzleClearance) ||
            (guard.sqrMagnitude > 0.0001f && hitscan.Raycast(GuardPosition, guard.normalized, guard.magnitude, out _));
        float spread = Mathf.Min(45f, data.Spread * (IsFirstPerson && !aim.IsAiming ? 1.8f : 1f) + bloom[selected]);
        if (!LastShotBlocked)
        {
            for (int pellet = 0; pellet < data.Pellets; pellet++)
            {
                Vector3 shot = WeaponHitscan.SpreadDirection(direction.normalized, spread);
                Vector3 endpoint = origin + shot * data.Range;
                if (hitscan.Raycast(origin, shot, data.Range, out RaycastHit hit))
                {
                    endpoint = hit.point;
                    IDamageable receiver = hit.collider.GetComponentInParent<IDamageable>();
                    receiver?.TakeDamage(data.Damage);
                    HitDetected?.Invoke(hit, data.Damage);
                }
                if (drawShotRays) Debug.DrawLine(origin, endpoint, Color.yellow, 0.15f);
            }
        }
        else if (drawShotRays) Debug.DrawLine(GuardPosition, origin, Color.red, 0.3f);

        bloom[selected] = Mathf.Min(data.MaximumExtraSpread, bloom[selected] + data.SpreadPerShot);
        CurrentView.PlayShot();
        if (animationDriver != null) animationDriver.PlayFire();
        if (cameraRig != null) cameraRig.ApplyRecoil(data.RecoilPitch, UnityEngine.Random.Range(-data.RecoilYaw, data.RecoilYaw));
        LastShotTime = Time.timeAsDouble;
        LastShotPosition = origin;
        AmmoChanged?.Invoke(MagazineAmmo, ReserveAmmo);
        ShotFired?.Invoke(new WeaponShotInfo(gameObject, data, origin, LastShotTime));
        return true;
    }

    [ContextMenu("Phase 3/Log Current Weapon and Ammo (Play Mode)")]
    private void LogAmmo()
    {
        if (Application.isPlaying && CurrentWeapon != null)
            Debug.Log($"{CurrentWeapon.WeaponName}: {MagazineAmmo}/{ReserveAmmo}; reload={IsReloading}; firing={IsFiring}; last shot blocked={LastShotBlocked}", this);
    }
}
```


## WeaponAmmoState

Full file path: D:/GV project/Assets/Scripts/Weapons/WeaponAmmoState.cs

```csharp
using System;

/// <summary>Per-owner state, independent of Unity, input, visuals and damage. Uses scaled seconds.</summary>
public sealed class WeaponAmmoState
{
    public int Magazine { get; private set; }
    public int Reserve { get; private set; }
    public int Capacity { get; }
    public bool IsReloading { get; private set; }
    public double ReloadEndsAt { get; private set; }
    public double NextFireTime { get; private set; }
    private readonly double fireInterval;
    private readonly double reloadDuration;

    public WeaponAmmoState(int capacity, int reserve, double shotsPerSecond, double reloadSeconds)
    {
        if (capacity < 1 || reserve < 0 || !FinitePositive(shotsPerSecond) || !FinitePositive(reloadSeconds))
            throw new ArgumentOutOfRangeException(nameof(capacity), "Invalid weapon ammo/timing settings.");
        Capacity = capacity;
        Magazine = capacity;
        Reserve = reserve;
        fireInterval = 1d / shotsPerSecond;
        reloadDuration = reloadSeconds;
    }

    public bool TryFire(double now)
    {
        if (IsReloading || Magazine <= 0 || now < NextFireTime) return false;
        Magazine--;
        // No burst of queued shots after a frame hitch. Cooldown survives weapon switching.
        NextFireTime = now + fireInterval;
        return true;
    }

    public bool TryStartReload(double now)
    {
        if (IsReloading || Magazine == Capacity || Reserve <= 0) return false;
        IsReloading = true;
        ReloadEndsAt = now + reloadDuration;
        return true;
    }

    public bool TickReload(double now)
    {
        if (!IsReloading || now < ReloadEndsAt) return false;
        int transferred = Math.Min(Capacity - Magazine, Reserve);
        Magazine += transferred;
        Reserve -= transferred;
        IsReloading = false;
        return true;
    }

    // Cancelling never deducts reserve rounds or fills a magazine.
    public void CancelReload() => IsReloading = false;
    public void AddReserve(int amount) { if (amount > 0) Reserve += amount; }
    private static bool FinitePositive(double value) => value > 0d && !double.IsInfinity(value) && !double.IsNaN(value);
}
```


## WeaponCategory

Full file path: D:/GV project/Assets/Scripts/Weapons/WeaponCategory.cs

```csharp
public enum WeaponCategory
{
    None = 0,
    Rifle = 1,
    SMG = 2,
    Shotgun = 3,
    Pistol = 4
}
```


## WeaponData

Full file path: D:/GV project/Assets/Scripts/Weapons/WeaponData.cs

```csharp
using UnityEngine;

/// <summary>Shared, read-only-at-runtime weapon tuning. Ammo belongs to WeaponAmmoState.</summary>
[CreateAssetMenu(menuName = "Zombie Outbreak/Weapon Data", fileName = "NewWeapon")]
public sealed class WeaponData : ScriptableObject
{
    [SerializeField] private string weaponName = "Rifle";
    [SerializeField] private WeaponCategory category = WeaponCategory.Rifle;
    [SerializeField, Min(0f)] private float damage = 25f;
    [Tooltip("Shots per second, not rounds per minute.")]
    [SerializeField, Min(0.01f)] private float fireRate = 8f;
    [SerializeField, Min(1)] private int magazineSize = 30;
    [Tooltip("Starting loose rounds. The initial full magazine is additional.")]
    [SerializeField, Min(0)] private int reserveAmmo = 120;
    [SerializeField, Min(0.01f)] private float reloadTime = 2.2f;
    [SerializeField, Min(1f)] private float range = 100f;
    [SerializeField] private bool automatic = true;
    [Tooltip("Cone half-angle in degrees, per pellet.")]
    [SerializeField, Range(0f, 30f)] private float spread = 0.7f;
    [SerializeField, Range(1, 32)] private int pellets = 1;

    [Header("Presentation hooks")]
    [SerializeField] private WeaponView weaponPrefab;
    [SerializeField] private ParticleSystem muzzleFlashPrefab;
    [SerializeField] private AudioClip fireAudio;
    [SerializeField] private AudioClip reloadAudio;
    [SerializeField, Range(0f, 1f)] private float audioVolume = 0.35f;
    [SerializeField, Min(0f)] private float recoilPitch = 0.7f;
    [SerializeField, Min(0f)] private float recoilYaw = 0.25f;
    [Tooltip("Extra spread added per shot, recovers over time.")]
    [SerializeField, Min(0f)] private float spreadPerShot = 0.25f;
    [SerializeField, Range(0f, 15f)] private float maximumExtraSpread = 2f;
    [SerializeField, Min(0f)] private float spreadRecovery = 3f;

    [Header("Future AI observation")]
    [Tooltip("Gameplay hearing radius in metres; independent of AudioSource volume.")]
    [SerializeField, Min(0f)] private float noiseRadius = 35f;

    public string WeaponName => weaponName;
    public WeaponCategory Category => category;
    public float Damage => Mathf.Max(0f, damage);
    public float FireRate => Mathf.Max(0.01f, fireRate);
    public int MagazineSize => Mathf.Max(1, magazineSize);
    public int ReserveAmmo => Mathf.Max(0, reserveAmmo);
    public float ReloadTime => Mathf.Max(0.01f, reloadTime);
    public float Range => Mathf.Max(1f, range);
    public bool Automatic => automatic;
    public float Spread => Mathf.Clamp(spread, 0f, 30f);
    public int Pellets => Mathf.Clamp(pellets, 1, 32);
    public WeaponView WeaponPrefab => weaponPrefab;
    public ParticleSystem MuzzleFlashPrefab => muzzleFlashPrefab;
    public AudioClip FireAudio => fireAudio;
    public AudioClip ReloadAudio => reloadAudio;
    public float AudioVolume => Mathf.Clamp01(audioVolume);
    public float RecoilPitch => Mathf.Max(0f, recoilPitch);
    public float RecoilYaw => Mathf.Max(0f, recoilYaw);
    public float SpreadPerShot => Mathf.Max(0f, spreadPerShot);
    public float MaximumExtraSpread => Mathf.Clamp(maximumExtraSpread, 0f, 15f);
    public float SpreadRecovery => Mathf.Max(0f, spreadRecovery);
    public float NoiseRadius => Mathf.Max(0f, noiseRadius);
}
```


## WeaponHitscan

Full file path: D:/GV project/Assets/Scripts/Weapons/WeaponHitscan.cs

```csharp
using UnityEngine;

/// <summary>Camera selection and muzzle collision queries. Never applies damage itself.</summary>
public sealed class WeaponHitscan
{
    private readonly Transform owner;
    private readonly int layers;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly Collider[] overlaps = new Collider[16];

    public WeaponHitscan(Transform owner, int layers) { this.owner = owner; this.layers = layers; }

    private bool IsExternal(Collider collider) => collider != null && !collider.transform.IsChildOf(owner);

    public bool Raycast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, layers, QueryTriggerInteraction.Ignore);
        // NonAlloc order is undefined; a full buffer may have omitted the nearest hit.
        RaycastHit[] results = count == hits.Length ?
            Physics.RaycastAll(origin, direction, distance, layers, QueryTriggerInteraction.Ignore) : hits;
        if (results != hits) count = results.Length;
        nearest = default;
        float best = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (!IsExternal(results[i].collider) || results[i].distance >= best) continue;
            nearest = results[i];
            best = nearest.distance;
        }
        return best < float.PositiveInfinity;
    }

    public bool OverlapsWorld(Vector3 position, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(position, radius, overlaps, layers, QueryTriggerInteraction.Ignore);
        Collider[] results = count == overlaps.Length ?
            Physics.OverlapSphere(position, radius, layers, QueryTriggerInteraction.Ignore) : overlaps;
        if (results != overlaps) count = results.Length;
        for (int i = 0; i < count; i++) if (IsExternal(results[i])) return true;
        return false;
    }

    public Vector3 AimPoint(Camera camera, float range, Vector3 fallbackOrigin, out bool cameraBlocked)
    {
        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        cameraBlocked = OverlapsWorld(ray.origin, 0.01f);
        if (cameraBlocked) return fallbackOrigin + camera.transform.forward * range;
        return Raycast(ray.origin, ray.direction, range, out RaycastHit hit) ? hit.point : ray.GetPoint(range);
    }

    public static Vector3 SpreadDirection(Vector3 forward, float degrees)
    {
        // Uniform solid-angle cone, not a square pattern or a centre-biased disk.
        float cosTheta = Mathf.Lerp(Mathf.Cos(degrees * Mathf.Deg2Rad), 1f, Random.value);
        float sinTheta = Mathf.Sqrt(Mathf.Max(0f, 1f - cosTheta * cosTheta));
        float phi = Random.value * Mathf.PI * 2f;
        return Quaternion.LookRotation(forward, Vector3.up) *
            new Vector3(sinTheta * Mathf.Cos(phi), sinTheta * Mathf.Sin(phi), cosTheta);
    }
}
```


## WeaponMechanism

Full file path: D:/GV project/Assets/Scripts/Weapons/WeaponMechanism.cs

```csharp
using UnityEngine;

/// <summary>Cosmetic slide/bolt, pump and magazine motion. Ammo and impacts remain in the weapon controller.</summary>
[DisallowMultipleComponent]
public sealed class WeaponMechanism : MonoBehaviour
{
    [SerializeField] private Transform bolt;
    [SerializeField] private Transform magazine;
    [SerializeField] private bool pumpAction;
    [SerializeField, Min(0)] private float travel = .035f;
    private Vector3 boltRest, magazineRest;
    private float shotStart = -100, reloadStart = -100, reloadDuration;
    private bool reloading;

    private void Awake()
    {
        if (bolt != null) boltRest = bolt.localPosition;
        if (magazine != null) magazineRest = magazine.localPosition;
    }
    public void Fire() { shotStart = Time.time; }
    public void Reload(float seconds) { reloadStart = Time.time; reloadDuration = Mathf.Max(.01f, seconds); reloading = true; }
    public void StopReload() { reloading = false; if (magazine != null) magazine.localPosition = magazineRest; }
    private void LateUpdate()
    {
        if (bolt != null)
        {
            float t = (Time.time - shotStart - (pumpAction ? .16f : 0)) / (pumpAction ? .38f : .10f);
            float amount = t >= 0 && t <= 1 ? Mathf.Sin(t * Mathf.PI) : 0;
            Vector3 direction = bolt.parent.InverseTransformDirection(-transform.forward);
            bolt.localPosition = boltRest + direction * (travel * amount);
        }
        if (magazine != null && reloading)
        {
            float t = Mathf.Clamp01((Time.time - reloadStart) / reloadDuration);
            float amount = Mathf.Sin(Mathf.Clamp01((t - .12f) / .76f) * Mathf.PI);
            magazine.localPosition = magazineRest + magazine.parent.InverseTransformDirection(-transform.up) * (amount * .13f);
            if (t >= 1) StopReload();
        }
    }
    private void OnDisable()
    {
        StopReload(); shotStart = -100;
        if (bolt != null) bolt.localPosition = boltRest;
    }
}
```


## WeaponShotInfo

Full file path: D:/GV project/Assets/Scripts/Weapons/WeaponShotInfo.cs

```csharp
using UnityEngine;

/// <summary>One event per spent round, including a shotgun discharge, for future AI hearing.</summary>
public readonly struct WeaponShotInfo
{
    public GameObject Source { get; }
    public WeaponData Weapon { get; }
    public WeaponCategory Category => Weapon.Category;
    public Vector3 Position { get; }
    public float NoiseRadius { get; }
    public double Time { get; }

    public WeaponShotInfo(GameObject source, WeaponData weapon, Vector3 position, double time)
    {
        Source = source;
        Weapon = weapon;
        Position = position;
        NoiseRadius = weapon.NoiseRadius;
        Time = time;
    }
}
```


## WeaponView

Full file path: D:/GV project/Assets/Scripts/Weapons/WeaponView.cs

```csharp
using UnityEngine;

/// <summary>Visible weapon with a grip-origin root, +Z barrel, muzzle, flash and audio hooks.</summary>
[DisallowMultipleComponent]
public sealed class WeaponView : MonoBehaviour
{
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform supportHand;
    [SerializeField] private ParticleSystem muzzleFlash;
    [Tooltip("A tiny emissive mesh used as a package-free greybox flash.")]
    [SerializeField] private GameObject greyboxFlash;
    [SerializeField] private AudioSource shotAudio;
    [SerializeField] private AudioSource reloadAudio;
    private WeaponData data;
    private float flashUntil;
    private Quaternion restRotation;
    private WeaponMechanism mechanism;

    public Transform Muzzle => muzzle;
    public Transform SupportHand => supportHand;

    public void Initialize(WeaponData settings)
    {
        data = settings;
        mechanism = GetComponent<WeaponMechanism>();
        restRotation = transform.localRotation;
        if (data.MuzzleFlashPrefab != null && muzzle != null)
        {
            if (muzzleFlash != null) muzzleFlash.gameObject.SetActive(false);
            muzzleFlash = Instantiate(data.MuzzleFlashPrefab, muzzle, false);
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if (greyboxFlash != null) greyboxFlash.SetActive(false);
    }

    public void AimAt(Vector3 point, bool aiming)
    {
        if (!aiming) { transform.localRotation = restRotation; return; }
        Vector3 forward = point - transform.position;
        if (forward.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    public void PlayShot()
    {
        if (mechanism != null) mechanism.Fire();
        if (muzzleFlash != null) muzzleFlash.Play(true);
        else if (greyboxFlash != null)
        {
            greyboxFlash.SetActive(true);
            flashUntil = Time.time + 0.045f;
        }
        if (shotAudio != null && data.FireAudio != null)
            shotAudio.PlayOneShot(data.FireAudio, data.AudioVolume);
    }

    public void PlayReload()
    {
        if (mechanism != null && data != null) mechanism.Reload(data.ReloadTime);
        if (reloadAudio != null && data.ReloadAudio != null)
            reloadAudio.PlayOneShot(data.ReloadAudio, data.AudioVolume);
    }

    public void StopReload()
    {
        if (reloadAudio != null) reloadAudio.Stop();
        if (mechanism != null) mechanism.StopReload();
    }

    private void Update()
    {
        if (greyboxFlash != null && Time.time >= flashUntil) greyboxFlash.SetActive(false);
    }

    private void OnDisable()
    {
        StopReload();
        if (shotAudio != null) shotAudio.Stop();
        if (greyboxFlash != null) greyboxFlash.SetActive(false);
        if (muzzleFlash != null) muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
```
