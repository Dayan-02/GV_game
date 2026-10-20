using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Phase17Setup
{
    [MenuItem("Zombie Outbreak/Phase 17 - Final integration fixes")]
    public static void Build()
    {
        OutbreakEditor.Open();
        Phase4UIFactory.PrepareResources();
        // Characters can pass between the camera and player without forcing a close-up.
        OutbreakEditor.Values(Camera.main.GetComponent<ThirdPersonCameraController>(), s => s.FindProperty("collisionLayers").intValue = 1 << 8);
        var presenter = OutbreakEditor.One<GamePanelsPresenter>();
        var canvas = presenter.transform;
        var buttons = new System.Collections.Generic.List<UnityEngine.UI.Button>();
        foreach (string panel in new[]
        {
            "PausePanel",
            "GameOverPanel",
            "VictoryPanel"
        }

        )
        {
            var content = canvas.Find(panel + "/Content");
            if (content == null)
                continue;
            var menu = content.Find("MainMenu")?.GetComponent<UnityEngine.UI.Button>();
            var existing = content.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var button in existing)
            {
                if (menu != null || button.name == "MainMenu")
                    continue;
                var rect = button.GetComponent<RectTransform>();
                rect.anchoredPosition += new Vector2(0, 40);
            }

            if (menu == null)
                menu = Phase4UIFactory.Button((RectTransform)content, "MainMenu", "MAIN MENU", 22, false);
            buttons.Add(menu);
        }

        OutbreakEditor.Values(presenter, s =>
        {
            var p = s.FindProperty("menuButtons");
            p.arraySize = buttons.Count;
            for (int i = 0; i < buttons.Count; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        });
        // Replace barrel-only visuals while retaining the stable authored box collision and Rigidbody.
        var props = GameObject.Find("OutbreakDistrict/PhysicsObjects").transform;
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var cylinderMesh = cylinder.GetComponent<MeshFilter>().sharedMesh;
        foreach (Transform prop in props)
            if (prop.name.StartsWith("Barrel"))
            {
                if (prop.Find("BarrelVisual") != null)
                    continue;
                prop.GetComponent<MeshRenderer>().enabled = false;
                var visual = new GameObject("BarrelVisual", typeof(MeshFilter), typeof(MeshRenderer));
                visual.transform.SetParent(prop, false);
                visual.transform.localScale = new Vector3(.95f, .5f, .95f);
                visual.GetComponent<MeshFilter>().sharedMesh = cylinderMesh;
                visual.GetComponent<MeshRenderer>().sharedMaterial = prop.GetComponent<MeshRenderer>().sharedMaterial;
            }

        Object.DestroyImmediate(cylinder);
        foreach (var target in Object.FindObjectsByType<ShootingTarget>())
            target.gameObject.SetActive(false);
        OutbreakEditor.Values(OutbreakEditor.One<PlayerWeaponController>(), s => s.FindProperty("drawShotRays").boolValue = false);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        Phase4UIFactory.PrepareResources();
        var contentRoot = (RectTransform)GameObject.Find("Menu/MenuCanvas/Content").transform;
        contentRoot.Find("Subtitle").GetComponent<TMP_Text>().text = "FIVE WAVES. FOUR THREATS. SURVIVE THE DISTRICT.";
        var quit = contentRoot.Find("Quit")?.GetComponent<UnityEngine.UI.Button>();
        if (quit == null)
            quit = Phase4UIFactory.Button(contentRoot, "Quit", "QUIT", 45, false);
        OutbreakEditor.Set(OutbreakEditor.One<LoadoutMenuController>(), "quitButton", quit);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        OutbreakEditor.Open();
        Phase3Checks.Run();
        Phase5Setup.Validate();
        OutbreakEditor.Require(buttons.Count == 3, "All terminal/pause panels require a Main Menu button.");
        OutbreakEditor.Save("17_SETUP");
    }
}
