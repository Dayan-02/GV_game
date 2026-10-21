using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

public static class Phase12Setup
{
    public const string CatalogPath = "Assets/Data/LoadoutCatalog.asset";
    [MenuItem("Dead city/Phase 12 - Loadout and equipment")]
    public static void Build()
    {
        OutbreakEditor.Open();
        Phase2AnimationAssets.Folder("Assets/Prefabs/Equipment");
        var catalog = AssetDatabase.LoadAssetAtPath<LoadoutCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<LoadoutCatalog>();
            catalog.primaries = new WeaponData[3];
            string[] names =
            {
                "Rifle",
                "SMG",
                "Shotgun"
            };
            for (int i = 0; i < 3; i++)
                catalog.primaries[i] = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/" + names[i] + ".asset");
            catalog.pistol = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/Pistol.asset");
            catalog.equipment = new[]
            {
                new EquipmentPack
                {
                    name = "MEDIC PACK: 3 MEDKITS / 1 GRENADE",
                    medkits = 3,
                    grenades = 1
                },
                new EquipmentPack
                {
                    name = "ASSAULT PACK: 1 MEDKIT / 3 GRENADES",
                    medkits = 1,
                    grenades = 3
                }
            };
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        OutbreakEditor.Require(catalog.Valid(0, 0) && catalog.Valid(1, 1) && catalog.Valid(2, 0), "Loadout catalog invalid.");
        const string grenadePath = "Assets/Prefabs/Equipment/Grenade.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(grenadePath) == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Grenade";
            go.layer = 8;
            go.transform.localScale = Vector3.one * .22f;
            go.GetComponent<Renderer>().sharedMaterial = OutbreakEditor.Material("Grenade", new Color(.2f, .27f, .12f));
            var body = go.AddComponent<Rigidbody>();
            body.mass = .6f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<GrenadeProjectile>();
            PrefabUtility.SaveAsPrefabAsset(go, grenadePath);
            Object.DestroyImmediate(go);
        }

        var player = OutbreakEditor.One<PlayerHealth>();
        var equipment = OutbreakEditor.Add<PlayerEquipment>(player.gameObject);
        OutbreakEditor.Set(equipment, "health", player);
        OutbreakEditor.Set(equipment, "viewCamera", Camera.main);
        OutbreakEditor.Set(equipment, "grenadePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(grenadePath).GetComponent<GrenadeProjectile>());
        var applier = OutbreakEditor.Add<PlayerLoadoutApplier>(player.gameObject);
        OutbreakEditor.Set(applier, "catalog", catalog);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        MakeMenu(false, catalog);
        MakeMenu(true, catalog);
        OpeningVideoSetup.ConfigureSceneFlow();
        PlayerSettings.productName = "Dead city";
        OutbreakEditor.Open();
        OutbreakEditor.Save("12");
    }

    private static void MakeMenu(bool loadout, LoadoutCatalog catalog)
    {
        string path = "Assets/Scenes/" + (loadout ? "Loadout" : "MainMenu") + ".unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
        {
            EditorSceneManager.OpenScene(path);
            OutbreakEditor.Set(OutbreakEditor.One<LoadoutMenuController>(), "catalog", AssetDatabase.LoadAssetAtPath<LoadoutCatalog>(CatalogPath));
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            return;
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        catalog = AssetDatabase.LoadAssetAtPath<LoadoutCatalog>(CatalogPath);
        Phase4UIFactory.PrepareResources();
        var root = new GameObject("Menu");
        var menu = root.AddComponent<LoadoutMenuController>();
        OutbreakEditor.Set(menu, "catalog", catalog);
        OutbreakEditor.Values(menu, s => s.FindProperty("isLoadout").boolValue = loadout);
        var camera = new GameObject("MenuCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.018f, .03f, .045f);
        camera.gameObject.AddComponent<AudioListener>();
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var canvas = Phase4UIFactory.MakeCanvas(root.transform, "MenuCanvas", 0);
        var card = Phase4UIFactory.Card(canvas, "Content", new Vector2(1300, 900), new Vector2(.5f, .5f), Vector2.zero);
        var accent = new Color(.28f, .88f, .72f);
        Phase4UIFactory.Label(card, "Eyebrow", "SECTOR 07 / INDUSTRIAL QUARANTINE", 20, accent, new Vector2(60, 814), new Vector2(1180, 35));
        Phase4UIFactory.Label(card, "Title", loadout ? "PREPARE YOUR LOADOUT" : "DEAD CITY", 64, Color.white, new Vector2(60, 710), new Vector2(1180, 100));
        Phase4UIFactory.Label(card, "Subtitle", loadout ? "CHOOSE A PRIMARY. YOUR SIDEARM IS A PISTOL." : "FIVE WAVES. FOUR HUNTERS. ONE WAY OUT.", 23, new Color(.65f, .72f, .78f), new Vector2(60, 660), new Vector2(1180, 40));
        var selection = Phase4UIFactory.Label(card, "Selection", loadout ? "" : "SURVIVE THE DISTRICT\n\nWASD  MOVE    SHIFT  RUN    CTRL  SPRINT    SPACE  JUMP\nRMB  AIM    LMB  FIRE    R  RELOAD    1 / 2  SWITCH\nE  INTERACT    Q  MEDKIT    G  GRENADE    ESC  PAUSE", 24, Color.white, new Vector2(60, loadout ? 240 : 315), new Vector2(1180, loadout ? 100 : 280), TextAlignmentOptions.Center);
        OutbreakEditor.Set(menu, "selectionText", selection);
        var start = Phase4UIFactory.Button(card, "Start", loadout ? "ENTER THE DISTRICT" : "PREPARE LOADOUT", 130, true);
        OutbreakEditor.Set(menu, "startButton", start);
        if (loadout)
        {
            var left = Phase4UIFactory.Rect(card, "PrimaryChoices", new Vector2(540, 280), Vector2.zero, new Vector2(60, 355));
            var right = Phase4UIFactory.Rect(card, "EquipmentChoices", new Vector2(540, 280), Vector2.zero, new Vector2(700, 355));
            var primaries = new UnityEngine.UI.Button[3];
            for (int i = 0; i < 3; i++)
                primaries[i] = Phase4UIFactory.Button(left, "Primary" + i, catalog.primaries[i].WeaponName.ToUpperInvariant(), 200 - i * 85, false);
            var packs = new UnityEngine.UI.Button[2];
            packs[0] = Phase4UIFactory.Button(right, "Medic", "MEDKIT PACK  [3 + 1]", 175, false);
            packs[1] = Phase4UIFactory.Button(right, "Assault", "GRENADE PACK  [1 + 3]", 80, false);
            OutbreakEditor.Values(menu, s =>
            {
                Assign(s.FindProperty("primaryButtons"), primaries);
                Assign(s.FindProperty("equipmentButtons"), packs);
            });
            OutbreakEditor.Set(menu, "backButton", Phase4UIFactory.Button(card, "Back", "BACK TO MENU", 45, false));
        }

        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
    }

    private static void Assign(SerializedProperty property, UnityEngine.UI.Button[] buttons)
    {
        property.arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
    }
}
