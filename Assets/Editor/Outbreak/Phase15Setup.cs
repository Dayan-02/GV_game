using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class Phase15Setup
{
    [MenuItem("Dead city/Phase 15 - Night district and effects")]
    public static void Build()
    {
        OutbreakEditor.Open();
        Phase2AnimationAssets.Folder("Assets/Textures/Outbreak");
        Phase2AnimationAssets.Folder("Assets/Prefabs/Effects");
        Surface("Concrete", new Color(.38f, .4f, .42f), 0);
        Surface("Asphalt", new Color(.15f, .17f, .19f), 0);
        Surface("Brick", new Color(.4f, .27f, .22f), 1);
        Surface("Wood", new Color(.4f, .3f, .18f), 2);
        Surface("Metal", new Color(.22f, .3f, .31f), 0);
        var district = GameObject.Find("OutbreakDistrict").transform;
        if (district.Find("ArtPass") == null)
            Dress(district);
        foreach (var light in Object.FindObjectsByType<Light>())
            if (light.type == LightType.Directional)
            {
                light.transform.rotation = Quaternion.Euler(48, -35, 0);
                light.color = new Color(.55f, .68f, 1);
                light.intensity = .95f;
                light.shadows = LightShadows.Soft;
            }

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.28f, .36f, .5f);
        RenderSettings.ambientEquatorColor = new Color(.18f, .23f, .3f);
        RenderSettings.ambientGroundColor = new Color(.1f, .12f, .15f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.075f, .105f, .15f);
        RenderSettings.fogStartDistance = 26;
        RenderSettings.fogEndDistance = 88;
        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        Camera.main.backgroundColor = RenderSettings.fogColor;
        Camera.main.farClipPlane = 110;
        QualitySettings.shadowDistance = 40;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.pixelLightCount = 4;
        var impact = Effect("Impact", new Color(1, .7f, .3f), 12, .22f, 2, .07f);
        var hit = Effect("EnemyHit", new Color(.48f, .05f, .035f), 10, .3f, 1.8f, .09f);
        var death = Effect("EnemyDeath", new Color(.18f, .2f, .15f, .65f), 18, .9f, .8f, .24f);
        var explosion = Effect("Explosion", new Color(1, .42f, .08f), 55, .7f, 6, .35f);
        var muzzle = Effect("MuzzleFlash", new Color(1, .8f, .3f), 5, .055f, 2, .075f);
        var fx = OutbreakEditor.Add<CombatEffects>(OutbreakEditor.Group("CombatEffects").gameObject);
        OutbreakEditor.Set(fx, "weapons", OutbreakEditor.One<PlayerWeaponController>());
        OutbreakEditor.Set(fx, "waves", OutbreakEditor.One<WaveManager>());
        OutbreakEditor.Set(fx, "impactPrefab", impact);
        OutbreakEditor.Set(fx, "hitPrefab", hit);
        OutbreakEditor.Set(fx, "deathPrefab", death);
        var grenade = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Equipment/Grenade.prefab");
        OutbreakEditor.Set(grenade.GetComponent<GrenadeProjectile>(), "explosionEffect", explosion);
        PrefabUtility.SaveAsPrefabAsset(grenade, "Assets/Prefabs/Equipment/Grenade.prefab");
        PrefabUtility.UnloadPrefabContents(grenade);
        foreach (string name in new[]
        {
            "Rifle",
            "SMG",
            "Shotgun",
            "Pistol"
        }

        )
            OutbreakEditor.Set(AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/" + name + ".asset"), "muzzleFlashPrefab", muzzle);
        district.GetComponent<NavMeshSurface>().BuildNavMesh();
        Phase5Setup.Validate();
        OutbreakEditor.Save("15");
    }

    private static void Surface(string name, Color color, int pattern)
    {
        var mat = OutbreakEditor.Material(name, color);
        string path = "Assets/Textures/Outbreak/" + name + ".asset";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            texture = new Texture2D(128, 128, TextureFormat.RGB24, true)
            {
                name = name + "Procedural",
                wrapMode = TextureWrapMode.Repeat
            };
            var pixels = new Color[128 * 128];
            var random = new System.Random(15 + pattern);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float grain = .88f + (float)random.NextDouble() * .24f;
                    if (pattern == 1 && (y % 32 < 2 || (x + ((y / 32) % 2) * 32) % 64 < 2))
                        grain = .55f;
                    if (pattern == 2)
                        grain *= .85f + .15f * Mathf.Sin(x * .7f + Mathf.Sin(y * .08f));
                    pixels[y * 128 + x] = new Color(grain, grain, grain);
                }

            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
        }

        mat.mainTexture = texture;
        mat.mainTextureScale = pattern == 1 ? new Vector2(3, 3) : new Vector2(8, 8);
        mat.color = color;
        EditorUtility.SetDirty(mat);
    }

    private static void Dress(Transform district)
    {
        var art = OutbreakEditor.Group("ArtPass", district);
        var metal = OutbreakEditor.Material("Metal", Color.gray);
        var paint = OutbreakEditor.Material("RoadPaint", new Color(.8f, .7f, .36f));
        for (int z = -33; z <= 33; z += 7)
        {
            if (z > -13 && z < -3)
                continue;
            var stripe = OutbreakEditor.Box("LaneMark", art, new Vector3(0, .052f, z), new Vector3(.13f, .012f, 2.8f), paint);
            Object.DestroyImmediate(stripe.GetComponent<Collider>());
        }

        var lampMat = OutbreakEditor.Material("LampGlow", new Color(1, .74f, .3f));
        lampMat.EnableKeyword("_EMISSION");
        lampMat.SetColor("_EmissionColor", new Color(2, 1.2f, .4f));
        for (int i = 0; i < 6; i++)
        {
            float x = i % 2 == 0 ? -6 : 6, z = -29 + i * 11;
            OutbreakEditor.Box("StreetLampPole", art, new Vector3(x, 2.5f, z), new Vector3(.15f, 5, .15f), metal);
            OutbreakEditor.Box("StreetLampHead", art, new Vector3(x * .88f, 5, z), new Vector3(1.4f, .15f, .4f), lampMat);
            Point("StreetPool", district.Find("Lighting"), new Vector3(x * .88f, 4.7f, z), new Color(1, .65f, .3f), 3.4f, 12);
        }

        Point("WarehouseInterior", district.Find("Lighting"), new Vector3(-20, 3.8f, 14), new Color(.56f, .75f, 1), 3, 15);
        Point("OfficeInterior", district.Find("Lighting"), new Vector3(21, 3.2f, 15), new Color(1, .7f, .38f), 2.5f, 13);
        Point("MaintenanceEmergency", district.Find("Lighting"), new Vector3(-22, 2.8f, -24), new Color(1, .15f, .07f), 1.1f, 10).gameObject.AddComponent<FlickeringLight>();
        var glass = OutbreakEditor.Material("DirtyGlass", new Color(.13f, .25f, .32f));
        glass.SetFloat("_Glossiness", .75f);
        for (int i = 0; i < 4; i++)
            OutbreakEditor.Box("OfficeWindow", art, new Vector3(15 + i * 4, 2.2f, 5.68f), new Vector3(2, 1.2f, .05f), glass);
        Sign(art, "WAREHOUSE 07", new Vector3(-20, 3.8f, 3.65f), Color.white);
        Sign(art, "QUARANTINE", new Vector3(21, 3.45f, 5.62f), new Color(1, .6f, .25f));
        Sign(art, "HOLD THE LINE", new Vector3(19, 2.3f, -26), new Color(1, .4f, .2f));
        var carPaint = OutbreakEditor.Material("AbandonedVehicle", new Color(.2f, .28f, .34f));
        Car(art, new Vector3(13, 0, -6), Quaternion.Euler(0, 65, 0), carPaint, glass);
        Car(art, new Vector3(-9, 0, 27), Quaternion.Euler(0, -15, 0), carPaint, glass);
        var rubble = OutbreakEditor.Material("Rubble", new Color(.3f, .31f, .32f));
        var random = new System.Random(23);
        for (int i = 0; i < 20; i++)
        {
            float x = (i % 2 == 0 ? -1 : 1) * (29 + (float)random.NextDouble() * 6), z = -30 + (float)random.NextDouble() * 60;
            OutbreakEditor.Box("Debris", art, new Vector3(x, .15f, z), new Vector3(.4f, .3f, .65f), rubble);
        }

        var smoke = Effect("AmbientSmoke", new Color(.22f, .25f, .29f, .25f), 25, 3, 1, .7f);
        var smokeObject = (GameObject)PrefabUtility.InstantiatePrefab(smoke.gameObject);
        smokeObject.name = "WarehouseVentSmoke";
        smokeObject.transform.SetParent(art);
        smokeObject.transform.position = new Vector3(-28, 4, 20);
        var ps = smokeObject.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startSpeed = .4f;
        main.startLifetime = 5;
        main.maxParticles = 60;
        main.playOnAwake = true;
        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[0]);
        emission.rateOverTime = 5;
        foreach (var renderer in art.GetComponentsInChildren<Renderer>())
            if (renderer.GetComponent<ParticleSystem>() == null)
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
    }

    private static Light Point(string name, Transform parent, Vector3 position, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        var lamp = go.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = color;
        lamp.intensity = intensity;
        lamp.range = range;
        lamp.shadows = LightShadows.None;
        return lamp;
    }

    private static void Sign(Transform parent, string text, Vector3 position, Color color)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(TextMeshPro));
        go.transform.SetParent(parent);
        go.transform.position = position;
        var label = go.GetComponent<TextMeshPro>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Phase4UIFactory.FontPath);
        label.text = text;
        label.fontSize = 3;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(10, 1);
        label.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    private static void Car(Transform parent, Vector3 position, Quaternion rotation, Material paint, Material glass)
    {
        var car = OutbreakEditor.Group("AbandonedCar", parent); // Each car receives a distinct transform.
        if (car.childCount > 0)
            car = new GameObject("AbandonedCar_02").transform;
        car.SetParent(parent);
        car.position = position;
        car.rotation = rotation;
        OutbreakEditor.Box("Body", car, new Vector3(0, .6f, 0), new Vector3(1.9f, .7f, 4.1f), paint);
        OutbreakEditor.Box("Cab", car, new Vector3(0, 1.2f, -.1f), new Vector3(1.6f, .65f, 1.9f), glass);
        var rubber = OutbreakEditor.Material("Rubber", new Color(.06f, .065f, .07f));
        foreach (float x in new[]
        {
            -.95f,
            .95f
        }

        )
            foreach (float z in new[]
            {
                -1.3f,
                1.3f
            }

            )
                OutbreakEditor.Box("Wheel", car, new Vector3(x, .4f, z), new Vector3(.25f, .65f, .65f), rubber);
    }

    public static ParticleSystem Effect(string name, Color color, int count, float lifetime, float speed, float size)
    {
        string path = "Assets/Prefabs/Effects/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing.GetComponent<ParticleSystem>();
        var go = new GameObject(name);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.duration = 1;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = .08f;
        var colorOver = ps.colorOverLifetime;
        colorOver.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        colorOver.color = gradient;
        string matPath = "Assets/Materials/Outbreak/Particles.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Particles/Standard Unlit"));
            AssetDatabase.CreateAsset(material, matPath);
        }

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<ParticleSystem>();
    }
}
