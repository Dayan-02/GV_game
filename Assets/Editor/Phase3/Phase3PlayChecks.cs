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
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
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
