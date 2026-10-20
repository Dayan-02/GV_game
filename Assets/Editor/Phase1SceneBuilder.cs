#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Builds the Phase 1 greybox scene and player prefab from the Unity Editor menu.</summary>
public static class Phase1SceneBuilder
{
    private const string ScenePath = ExistingCityEnhancer.ScenePath;
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
    private const int IgnoreRaycastLayer = 2;

    [MenuItem("Tools/Phase 1/Build ZombieMap Greybox")]
    public static void BuildZombieMap()
    {
        CurrentGameScenePolicy.RejectLegacySceneGeneration();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSettings.serializationMode = SerializationMode.ForceText;
        UnityEditor.VersionControlSettings.mode = "Visible Meta Files";
        EnsureFolders();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "ZombieMap";

        Material groundMaterial = CreateOrLoadMaterial(
            "Assets/Materials/Greybox/Ground_Grey.mat",
            new Color(0.34f, 0.36f, 0.38f));
        Material coverMaterial = CreateOrLoadMaterial(
            "Assets/Materials/Greybox/Cover_Grey.mat",
            new Color(0.46f, 0.48f, 0.5f));
        Material playerMaterial = CreateOrLoadMaterial(
            "Assets/Materials/Greybox/Player_Grey.mat",
            new Color(0.25f, 0.42f, 0.58f));

        Transform environment = new GameObject("Environment").transform;
        CreateEnvironment(environment, groundMaterial, coverMaterial);

        GameObject player = CreatePlayer(playerMaterial);
        ThirdPersonPlayerController playerController = player.GetComponent<ThirdPersonPlayerController>();
        Transform cameraTarget = player.transform.Find("CameraTarget");
        Transform aimTarget = player.transform.Find("CameraTarget/AimTarget");

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera playerCamera = cameraObject.AddComponent<Camera>();
        playerCamera.fieldOfView = 60f;
        playerCamera.nearClipPlane = 0.1f;
        playerCamera.farClipPlane = 500f;
        cameraObject.AddComponent<AudioListener>();
        ThirdPersonCameraController cameraController = cameraObject.AddComponent<ThirdPersonCameraController>();
        SetObjectReference(cameraController, "cameraTarget", cameraTarget);
        SetObjectReference(cameraController, "aimTarget", aimTarget);

        cameraObject.transform.position = player.transform.position + new Vector3(0f, 2.2f, -4.5f);
        cameraObject.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

        GameObject lightObject = new GameObject("Directional Light");
        Light directionalLight = lightObject.AddComponent<Light>();
        directionalLight.type = LightType.Directional;
        directionalLight.intensity = 1.1f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.52f, 0.52f, 0.52f);

        PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
        SetObjectReference(playerController, "cameraTransform", cameraObject.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool sceneAlreadyInBuild = false;
        for (int i = 0; i < buildScenes.Count; i++)
        {
            if (buildScenes[i].path == ScenePath)
            {
                buildScenes[i] = new EditorBuildSettingsScene(ScenePath, true);
                sceneAlreadyInBuild = true;
                break;
            }
        }

        if (!sceneAlreadyInBuild)
        {
            buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        }

        EditorBuildSettings.scenes = buildScenes.ToArray();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Phase 1 scene created: " + ScenePath + ". Player prefab: " + PlayerPrefabPath);
    }

    private static GameObject CreatePlayer(Material playerMaterial)
    {
        GameObject player = new GameObject("Player");
        player.layer = IgnoreRaycastLayer;
        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = 1.8f;
        characterController.radius = 0.32f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.stepOffset = 0.3f;
        characterController.slopeLimit = 45f;
        characterController.skinWidth = 0.08f;

        ThirdPersonPlayerController movement = player.AddComponent<ThirdPersonPlayerController>();
        player.AddComponent<PlayerHealth>();

        Transform visuals = new GameObject("Humanoid_Greybox").transform;
        visuals.SetParent(player.transform, false);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "Torso", new Vector3(0f, 1.08f, 0f), new Vector3(0.42f, 0.52f, 0.27f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.66f, 0f), new Vector3(0.28f, 0.28f, 0.28f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "LeftArm", new Vector3(-0.34f, 1.08f, 0f), new Vector3(0.14f, 0.4f, 0.14f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "RightArm", new Vector3(0.34f, 1.08f, 0f), new Vector3(0.14f, 0.4f, 0.14f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "LeftLeg", new Vector3(-0.13f, 0.47f, 0f), new Vector3(0.16f, 0.44f, 0.16f), playerMaterial);
        CreateBodyPart(visuals, PrimitiveType.Capsule, "RightLeg", new Vector3(0.13f, 0.47f, 0f), new Vector3(0.16f, 0.44f, 0.16f), playerMaterial);

        Transform cameraTarget = CreateChild(player.transform, "CameraTarget", new Vector3(0f, 1.48f, 0f));
        CreateChild(cameraTarget, "AimTarget", new Vector3(0f, 0f, 20f));
        CreateChild(player.transform, "WeaponSocket", new Vector3(0.34f, 1.12f, 0.22f));
        Transform groundCheck = CreateChild(player.transform, "GroundCheck", new Vector3(0f, 0.08f, 0f));
        SetObjectReference(movement, "groundCheck", groundCheck);

        SetLayerRecursively(player.transform, IgnoreRaycastLayer);
        return player;
    }

    private static void CreateEnvironment(Transform parent, Material groundMaterial, Material coverMaterial)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Ground_Greybox";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = Vector3.zero;
        floor.transform.localScale = new Vector3(5f, 1f, 5f);
        ApplyMaterial(floor, groundMaterial);

        CreateBlock(parent, "Cover_West", new Vector3(-3.5f, 0.55f, 2.5f), new Vector3(1.4f, 1.1f, 0.7f), coverMaterial);
        CreateBlock(parent, "Cover_East", new Vector3(3.5f, 0.55f, 3f), new Vector3(1.4f, 1.1f, 0.7f), coverMaterial);
        CreateBlock(parent, "Cover_North", new Vector3(0f, 0.6f, 6f), new Vector3(3f, 1.2f, 0.7f), coverMaterial);
        CreateBlock(parent, "LowBarrier_West", new Vector3(-7f, 0.4f, -3f), new Vector3(3f, 0.8f, 0.6f), coverMaterial);
        CreateBlock(parent, "LowBarrier_East", new Vector3(7f, 0.4f, -3f), new Vector3(3f, 0.8f, 0.6f), coverMaterial);
        CreateBlock(parent, "Boundary_North", new Vector3(0f, 1.5f, 20f), new Vector3(40f, 3f, 0.5f), coverMaterial);
        CreateBlock(parent, "Boundary_South", new Vector3(0f, 1.5f, -20f), new Vector3(40f, 3f, 0.5f), coverMaterial);
        CreateBlock(parent, "Boundary_East", new Vector3(20f, 1.5f, 0f), new Vector3(0.5f, 3f, 40f), coverMaterial);
        CreateBlock(parent, "Boundary_West", new Vector3(-20f, 1.5f, 0f), new Vector3(0.5f, 3f, 40f), coverMaterial);
    }

    private static void CreateBlock(Transform parent, string objectName, Vector3 position, Vector3 size, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = position;
        block.transform.localScale = size;
        ApplyMaterial(block, material);
    }

    private static GameObject CreateBodyPart(
        Transform parent,
        PrimitiveType primitive,
        string objectName,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = objectName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            Object.DestroyImmediate(partCollider);
        }
        ApplyMaterial(part, material);
        SetLayerRecursively(part.transform, IgnoreRaycastLayer);
        return part;
    }

    private static Transform CreateChild(Transform parent, string childName, Vector3 localPosition)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        return child.transform;
    }

    private static Material CreateOrLoadMaterial(string assetPath, Color color)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (existing != null)
        {
            existing.color = color;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (shader == null)
        {
            Debug.LogError("No Standard or URP/Lit shader was found. The greybox will use Unity's default material.");
            return null;
        }

        Material material = new Material(shader);
        material.color = color;
        AssetDatabase.CreateAsset(material, assetPath);
        return material;
    }

    private static void ApplyMaterial(GameObject target, Material material)
    {
        if (material == null)
        {
            return;
        }

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Prefabs/Player");
        EnsureFolder("Assets/Materials/Greybox");
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string currentPath = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }
            currentPath = nextPath;
        }
    }
}
#endif
