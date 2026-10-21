using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class Phase5Setup
{
    [MenuItem("Dead city/Phase 5 - Build district")]
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
