using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class Phase11Setup
{
    [MenuItem("Zombie Outbreak/Phase 11 - Elite commander")]
    public static void Build()
    {
        OutbreakEditor.Open();
        EnemyPrefabFactory.Create<EliteBrain>("Elite", 350, new Color(.54f, .16f, .17f), 1.25f);
        const string path = "Assets/Prefabs/Enemies/Elite.prefab";
        var go = PrefabUtility.LoadPrefabContents(path);
        var capsule = go.GetComponent<CapsuleCollider>();
        capsule.height = 2.4f;
        capsule.center = Vector3.up * 1.2f;
        capsule.radius = .5f;
        var nav = go.GetComponent<NavMeshAgent>();
        nav.height = 2.4f;
        nav.radius = .5f;
        OutbreakEditor.Values(go.GetComponent<EnemyCombat>(), s =>
        {
            s.FindProperty("range").floatValue = 2.4f;
            s.FindProperty("damage").floatValue = 16;
            s.FindProperty("windup").floatValue = .7f;
        });
        PrefabUtility.SaveAsPrefabAsset(go, path);
        PrefabUtility.UnloadPrefabContents(go);
        var scores = new float[5];
        EliteBrain.Score(2, 0, false, false, 0, true, true, 2.4f, scores);
        OutbreakEditor.Require(scores[1] > scores[0], "Ready heavy attack should score highest in range.");
        OutbreakEditor.Save("11");
    }
}
