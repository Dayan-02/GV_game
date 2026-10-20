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
