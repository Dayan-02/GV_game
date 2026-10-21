using UnityEditor;
using UnityEngine;

public static class Phase9Setup
{
    [MenuItem("Dead city/Phase 9 - Runner utility AI")]
    public static void Build()
    {
        OutbreakEditor.Open();
        EnemyPrefabFactory.Create<RunnerBrain>("Runner", 45, new Color(.7f, .42f, .17f));
        var scores = new float[5];
        RunnerBrain.Score(5, 1, 0, true, true, true, true, scores);
        float caution = scores[2];
        float chase = scores[0];
        RunnerBrain.Score(5, 1, 1, true, true, true, true, scores);
        OutbreakEditor.Require(scores[0] > chase && scores[2] < caution, "Weak player should increase aggression.");
        RunnerBrain.Score(5, 1, 0, false, false, true, true, scores);
        OutbreakEditor.Require(scores[2] < caution, "Shotgun should increase caution.");
        OutbreakEditor.Save("9");
    }
}
