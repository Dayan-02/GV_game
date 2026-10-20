using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the team-art drop locations and catalog available in a fresh checkout.
/// This creates project folders and an empty catalog only; it does not create or
/// claim authorship of any art assets.
/// </summary>
[InitializeOnLoad]
internal static class TeamArtProjectSetup
{
    private const string CatalogPath = "Assets/Data/TeamArtCatalog.asset";

    static TeamArtProjectSetup()
    {
        EditorApplication.delayCall += EnsureSetup;
    }

    public static void EnsureSetup()
    {
        EnsureFolder("Assets/Models", "Team");
        EnsureFolder("Assets/Models/Team", "Zombie");
        EnsureFolder("Assets/Models/Team/Zombie", "Source");
        EnsureFolder("Assets/Models/Team/Zombie", "Export");
        EnsureFolder("Assets/Models/Team/Zombie", "Textures");
        EnsureFolder("Assets/Models/Team", "Barricade");
        EnsureFolder("Assets/Models/Team/Barricade", "Source");
        EnsureFolder("Assets/Models/Team/Barricade", "Export");
        EnsureFolder("Assets/Models/Team/Barricade", "Textures");

        if (AssetDatabase.LoadAssetAtPath<TeamArtCatalog>(CatalogPath) != null)
            return;

        TeamArtCatalog catalog = ScriptableObject.CreateInstance<TeamArtCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        AssetDatabase.SaveAssets();
        Debug.Log("Created the Team Art Catalog. Add the team's original zombie and barricade exports when ready.");
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
