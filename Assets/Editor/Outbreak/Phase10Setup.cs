using UnityEditor;
using UnityEngine;

public static class Phase10Setup
{
    [MenuItem("Zombie Outbreak/Phase 10 - Tactical Ambusher")]
    public static void Build()
    {
        OutbreakEditor.Open();
        EnemyPrefabFactory.Create<AmbusherBrain>("Ambusher", 70, new Color(.37f, .3f, .48f));
        OutbreakEditor.Require(OutbreakEditor.One<EnemyWorld>().tacticalPoints.Length >= 6, "Ambusher requires authored tactical points.");
        OutbreakEditor.Save("10");
    }
}
