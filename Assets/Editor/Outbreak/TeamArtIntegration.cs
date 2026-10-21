using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Opt-in integration of supplied, validated models; empty catalog fields leave greybox intact.</summary>
public static class TeamArtIntegration
{
    public const string CatalogPath = "Assets/Data/TeamArtCatalog.asset";

    [MenuItem("Dead city/Team Art/Validate supplied models")]
    public static void ValidateSuppliedModels()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<TeamArtCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning("No TeamArtCatalog exists. Greybox placeholders remain active.");
            return;
        }

        bool hasAny = catalog.zombieVisualPrefab != null || catalog.barricadeVisualPrefab != null;
        if (hasAny && !catalog.modelsAuthoredByTeam)
            Debug.Log("Team model validation will check asset compatibility, but authorship has not been verified. Keep the contribution record accurate.");
        if (catalog.zombieVisualPrefab != null) ValidateZombie(catalog.zombieVisualPrefab);
        else Debug.LogWarning("Original zombie prefab is not assigned. See TEAM_MODEL_HANDOFF.md.");
        if (catalog.barricadeVisualPrefab != null) ValidateBarricade(catalog.barricadeVisualPrefab);
        else Debug.LogWarning("Original barricade prefab is not assigned. See TEAM_MODEL_HANDOFF.md.");
    }

    [MenuItem("Dead city/Team Art/Apply validated models to game")]
    public static void Apply()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<TeamArtCatalog>(CatalogPath);
        if (catalog == null || (catalog.zombieVisualPrefab == null && catalog.barricadeVisualPrefab == null))
        {
            Debug.LogWarning("No original model prefabs are assigned. Nothing was changed; greybox placeholders remain active.");
            return;
        }

        if (catalog.zombieVisualPrefab != null) ApplyZombie(catalog.zombieVisualPrefab);
        if (catalog.barricadeVisualPrefab != null)
        {
            ApplyBarricades(catalog);
            RebuildNavigation();
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Supplied visual references applied. Verify materials, scale, Avatar, collider fit and navigation in Play Mode.");
    }

    private static void ValidateZombie(GameObject prefab)
    {
        OutbreakEditor.Require(PrefabUtility.IsPartOfPrefabAsset(prefab), "Zombie visual must be a saved prefab asset.");
        var animator = prefab.GetComponentInChildren<Animator>(true);
        OutbreakEditor.Require(animator != null, "Zombie visual prefab needs a Humanoid Animator.");
        OutbreakEditor.Require(animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman,
            "Zombie Animator Avatar must be valid and Humanoid.");
        OutbreakEditor.Require(animator.runtimeAnimatorController == null ||
            animator.runtimeAnimatorController == AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Phase7Setup.ControllerPath),
            "Use the shared Enemy controller so shared state parameters stay consistent.");
        OutbreakEditor.Require(prefab.GetComponentInChildren<NavMeshAgent>(true) == null && prefab.GetComponent<Rigidbody>() == null,
            "Visual-only prefab must not duplicate movement or root physics.");
        var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        OutbreakEditor.Require(renderers.Any(r => r.sharedMesh != null && r.sharedMesh.vertexCount > 100),
            "Zombie should include a skinned mesh with more than 100 vertices.");
        foreach (var renderer in renderers)
            OutbreakEditor.Require(renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(m => m != null),
                renderer.name + " needs assigned materials.");
        Debug.Log("Zombie prefab compatibility passed. Team authorship remains a team record, not a Unity-verifiable property.");
    }

    private static void ValidateBarricade(GameObject prefab)
    {
        OutbreakEditor.Require(PrefabUtility.IsPartOfPrefabAsset(prefab), "Barricade visual must be a saved prefab asset.");
        OutbreakEditor.Require(prefab.GetComponentInChildren<Renderer>(true) != null, "Barricade prefab needs a Renderer.");
        OutbreakEditor.Require(prefab.GetComponentInChildren<Collider>(true) == null && prefab.GetComponent<Rigidbody>() == null &&
            prefab.GetComponent<NavMeshObstacle>() == null, "Visual prefab must not carry physics or NavMesh components.");
        OutbreakEditor.Require(prefab.transform.localScale == Vector3.one, "Apply scale in the source model and keep prefab root scale one.");
        Debug.Log("Barricade prefab compatibility passed. Confirm its dimensions in metres before applying.");
    }

    private static void ApplyZombie(GameObject visualPrefab)
    {
        ValidateZombie(visualPrefab);
        const string path = Phase7Setup.BasePrefab;
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var anchor = root.transform.Find("Visual");
            OutbreakEditor.Require(anchor != null, "EnemyBase prefab is missing the Visual anchor.");
            foreach (Transform child in anchor.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, anchor);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            var animator = instance.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Phase7Setup.ControllerPath);
            animator.applyRootMotion = false;
            var driver = root.GetComponent<EnemyAnimationController>();
            OutbreakEditor.Set(driver, "animator", animator);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ApplyBarricades(TeamArtCatalog catalog)
    {
        ValidateBarricade(catalog.barricadeVisualPrefab);
        var scene = EditorSceneManager.OpenScene(OutbreakEditor.Scene);
        var parent = GameObject.Find("OutbreakDistrict/Barricades");
        OutbreakEditor.Require(parent != null, "ZombieMap has no Barricades group.");
        foreach (Transform barricade in parent.transform)
        {
            if (barricade.Find("TeamBarricadeVisual") != null) Object.DestroyImmediate(barricade.Find("TeamBarricadeVisual").gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(catalog.barricadeVisualPrefab, barricade);
            visual.name = "TeamBarricadeVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            var originalRenderer = barricade.GetComponent<Renderer>();
            if (originalRenderer != null) originalRenderer.enabled = false;
            var obstacle = barricade.GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.size = new Vector3(2.4f, 1.3f, .55f);
            var damage = barricade.GetComponent<DestructibleBarricade>();
            if (damage != null && catalog.brokenDebrisPrefab != null)
                OutbreakEditor.Set(damage, "debrisPrefab", catalog.brokenDebrisPrefab);
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void RebuildNavigation()
    {
        var surface = GameObject.Find("OutbreakDistrict")?.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
        if (surface == null) { Debug.LogWarning("No NavMeshSurface found; rebuild navigation after applying the model."); return; }
        surface.BuildNavMesh();
        Phase5Setup.Validate();
    }
}
