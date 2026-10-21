using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class Phase16Setup
{
    [MenuItem("Dead city/Phase 16 - Synchronize animations")]
    public static void Build()
    {
        OutbreakEditor.Open();
        Phase2AnimationAssets.Folder("Assets/Animations/Enemies/Clips");
        var rig = Phase2HumanoidFactory.Create("Assets/Animations/Enemies/EnemyAvatar.asset");
        string[] names =
        {
            "ZombieIdle",
            "ZombieWalkForward",
            "ZombieRun",
            "ZombieAttack",
            "EliteHeavy",
            "ZombieHit",
            "ZombieDeath"
        };
        foreach (string name in names)
        {
            string path = "Assets/Animations/Enemies/Clips/" + name + ".anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
                continue;
            bool loop = name == "ZombieIdle" || name == "ZombieWalkForward" || name == "ZombieRun";
            float duration = name == "ZombieRun" ? .6f : name == "ZombieHit" ? .3f : 1;
            AssetDatabase.CreateAsset(Phase2HumanoidFactory.CreateClip(rig, name, duration, loop), path);
        }

        Object.DestroyImmediate(rig.Root);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Phase7Setup.ControllerPath);
        if (!controller.parameters.Any(p => p.name == "GaitScale"))
            controller.AddParameter(new AnimatorControllerParameter { name = "GaitScale", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        foreach (var child in controller.layers[0].stateMachine.states)
        {
            var state = child.state;
            if (state.name == "Locomotion")
            {
                var tree = (BlendTree)state.motion;
                tree.children = new ChildMotion[0];
                tree.AddChild(Clip("ZombieIdle"), 0);
                tree.AddChild(Clip("ZombieWalkForward"), 1.5f);
                tree.AddChild(Clip("ZombieRun"), 3.5f);
                state.speedParameter = "GaitScale";
                state.speedParameterActive = true;
                EditorUtility.SetDirty(tree);
            }
            else
                state.motion = Clip(state.name == "Heavy" ? "EliteHeavy" : "Zombie" + state.name);
        }

        foreach (var transition in controller.layers[0].stateMachine.anyStateTransitions)
            if (transition.destinationState.name == "Attack" || transition.destinationState.name == "Heavy")
                transition.duration = 0;
        EditorUtility.SetDirty(controller);
        var player = AssetDatabase.LoadAssetAtPath<AnimatorController>(Phase2AnimationAssets.ControllerPath);
        if (!player.parameters.Any(p => p.name == "ReloadSpeed"))
            player.AddParameter(new AnimatorControllerParameter { name = "ReloadSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        var actions = player.layers[1].stateMachine;
        var reload = actions.states.First(s => s.state.name == "Reload").state;
        reload.speedParameter = "ReloadSpeed";
        reload.speedParameterActive = true;
        foreach (var transition in reload.transitions)
        {
            transition.exitTime = 1;
            transition.duration = 0;
        }

        foreach (var transition in actions.anyStateTransitions)
            if (transition.destinationState == reload)
                transition.duration = 0;
        EditorUtility.SetDirty(player);
        OutbreakEditor.Require(controller.layers[0].stateMachine.states.All(s => s.state.motion != null), "Enemy states require animation clips.");
        OutbreakEditor.Save("16");
    }

    private static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Enemies/Clips/" + name + ".anim");
}
