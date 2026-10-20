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
