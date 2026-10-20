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
