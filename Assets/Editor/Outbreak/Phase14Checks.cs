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
