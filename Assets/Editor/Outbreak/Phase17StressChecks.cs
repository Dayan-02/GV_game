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
