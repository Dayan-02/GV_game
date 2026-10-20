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
