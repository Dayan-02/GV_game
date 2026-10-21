using System.Linq;
using UnityEditor;
using UnityEngine;
public static class InspectDeathBindings
{
    public static string Read()
    {
        var text = new System.Text.StringBuilder();
        foreach (var path in new[] {
            "Assets/Art/CustomEnemies/Models/Hunter_Animated.fbx",
            "Assets/Art/CustomEnemies/DeathAnimations/Hunter_Deaths.fbx" })
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            text.AppendLine(path);
            foreach (var t in model.GetComponentsInChildren<Transform>().Take(5))
                text.AppendLine(t.name + " pos=" + t.localPosition + " rot=" + t.localRotation + " scale=" + t.localScale);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .First(c => c.name.Contains("Death") && !c.name.StartsWith("__"));
            foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.path.Split('/').Length < 3).Take(25))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                text.AppendLine(binding.path + " / " + binding.propertyName + ": " + curve.Evaluate(0));
            }
        }
        return text.ToString();
    }
}
