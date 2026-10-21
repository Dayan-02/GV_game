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

    [MenuItem("Dead city/AI/Run isolated behavior validation")]
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
