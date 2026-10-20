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
