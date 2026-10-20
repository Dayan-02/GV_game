using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Creates editable weapon assets once. Reruns preserve existing tuning and prefabs.</summary>
public static class Phase3WeaponAssets
{
    public const string DataFolder = "Assets/Data/Weapons";
    public const string PrefabFolder = "Assets/Prefabs/Weapons";

    private sealed class Preset
    {
        public string Name;
        public WeaponCategory Category;
        public float Damage, Rate, Reload, Range, Spread, Length, Recoil, Noise;
        public int Magazine, Reserve, Pellets = 1;
        public bool Auto;
    }

    public static WeaponData[] EnsureAssets()
    {
        Phase2AnimationAssets.Folder(DataFolder);
        Phase2AnimationAssets.Folder(PrefabFolder);
        Phase2AnimationAssets.Folder("Assets/Audio/Weapons");
        Phase2AnimationAssets.Folder("Assets/Materials/Weapons");
        Preset[] presets =
        {
            new Preset { Name="Rifle", Category=WeaponCategory.Rifle, Damage=25, Rate=8, Magazine=30,
                Reserve=120, Reload=2.2f, Range=100, Auto=true, Spread=0.6f, Length=0.7f, Recoil=0.65f, Noise=35 },
            new Preset { Name="SMG", Category=WeaponCategory.SMG, Damage=12, Rate=12, Magazine=35,
                Reserve=140, Reload=1.8f, Range=65, Auto=true, Spread=1.1f, Length=0.43f, Recoil=0.4f, Noise=28 },
            new Preset { Name="Shotgun", Category=WeaponCategory.Shotgun, Damage=10, Rate=1.1f, Magazine=8,
                Reserve=32, Reload=2.8f, Range=35, Spread=4.5f, Length=0.8f, Recoil=2.2f, Noise=45, Pellets=8 },
            new Preset { Name="Pistol", Category=WeaponCategory.Pistol, Damage=25, Rate=4, Magazine=12,
                Reserve=60, Reload=1.45f, Range=70, Spread=0.45f, Length=0.23f, Recoil=1.1f, Noise=22 }
        };
        Material body = MaterialAt("WeaponGrey", new Color(0.22f, 0.24f, 0.26f), false);
        Material accent = MaterialAt("WeaponLightGrey", new Color(0.48f, 0.5f, 0.52f), false);
        Material flash = MaterialAt("MuzzleFlash", new Color(1f, 0.7f, 0.15f), true);
        WeaponData[] result = new WeaponData[presets.Length];
        for (int i = 0; i < presets.Length; i++)
        {
            Preset preset = presets[i];
            string path = DataFolder + "/" + preset.Name + ".asset";
            result[i] = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (result[i] != null) continue;
            WeaponView prefab = Prefab(preset, body, accent, flash);
            AudioClip audio = Audio(preset.Name, 140f + i * 80f, 700 + i, false);
            AudioClip reload = Audio("ReloadClick", 700f, 777, true);
            WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
            SerializedObject so = new SerializedObject(data);
            so.FindProperty("weaponName").stringValue = preset.Name;
            so.FindProperty("category").intValue = (int)preset.Category;
            so.FindProperty("damage").floatValue = preset.Damage;
            so.FindProperty("fireRate").floatValue = preset.Rate;
            so.FindProperty("magazineSize").intValue = preset.Magazine;
            so.FindProperty("reserveAmmo").intValue = preset.Reserve;
            so.FindProperty("reloadTime").floatValue = preset.Reload;
            so.FindProperty("range").floatValue = preset.Range;
            so.FindProperty("automatic").boolValue = preset.Auto;
            so.FindProperty("spread").floatValue = preset.Spread;
            so.FindProperty("pellets").intValue = preset.Pellets;
            so.FindProperty("weaponPrefab").objectReferenceValue = prefab;
            so.FindProperty("fireAudio").objectReferenceValue = audio;
            so.FindProperty("reloadAudio").objectReferenceValue = reload;
            so.FindProperty("recoilPitch").floatValue = preset.Recoil;
            so.FindProperty("recoilYaw").floatValue = preset.Recoil * 0.3f;
            so.FindProperty("noiseRadius").floatValue = preset.Noise;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(data, path);
            result[i] = data;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    private static WeaponView Prefab(Preset preset, Material body, Material accent, Material flashMaterial)
    {
        string path = PrefabFolder + "/" + preset.Name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing.GetComponent<WeaponView>();
        GameObject root = new GameObject(preset.Name);
        try
        {
            root.layer = 2;
            WeaponView view = root.AddComponent<WeaponView>();
            Part(root.transform, "Receiver", new Vector3(0, 0.04f, preset.Length * 0.25f),
                new Vector3(0.075f, 0.09f, preset.Length * 0.65f), body);
            Part(root.transform, "Barrel", new Vector3(0, 0.04f, preset.Length * 0.73f),
                new Vector3(0.035f, 0.035f, preset.Length * 0.55f), accent);
            Part(root.transform, "Grip", new Vector3(0, -0.07f, 0), new Vector3(0.065f, 0.16f, 0.075f), body);
            if (preset.Category != WeaponCategory.Pistol)
            {
                Part(root.transform, "Stock", new Vector3(0, 0.015f, -0.17f), new Vector3(0.065f, 0.11f, 0.24f), body);
                Part(root.transform, "Magazine", new Vector3(0, -0.075f, preset.Length * 0.3f), new Vector3(0.05f, 0.16f, 0.09f), accent);
            }
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0, 0.04f, preset.Length + 0.02f);
            GameObject flash = Part(muzzle, "Greybox Flash", new Vector3(0, 0, 0.07f), new Vector3(0.04f, 0.04f, 0.14f), flashMaterial);
            flash.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            flash.SetActive(false);
            AudioSource shot = Source(root);
            AudioSource reload = Source(root);
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("muzzle").objectReferenceValue = muzzle;
            so.FindProperty("greyboxFlash").objectReferenceValue = flash;
            so.FindProperty("shotAudio").objectReferenceValue = shot;
            so.FindProperty("reloadAudio").objectReferenceValue = reload;
            so.ApplyModifiedPropertiesWithoutUndo();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (saved == null) throw new InvalidOperationException("Could not save " + path);
            return saved.GetComponent<WeaponView>();
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static AudioSource Source(GameObject root)
    {
        AudioSource source = root.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.minDistance = 2f;
        source.maxDistance = 45f;
        source.dopplerLevel = 0f;
        return source;
    }

    private static GameObject Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.layer = 2;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }

    private static Material MaterialAt(string name, Color color, bool emission)
    {
        string path = "Assets/Materials/Weapons/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        Shader shader = GraphicsSettings.currentRenderPipeline == null ? Shader.Find("Standard") : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Expected Standard or URP/Lit shader.");
        Material material = new Material(shader) { color = color };
        if (emission)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 3f);
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Original synthesized greybox clicks; no downloaded or licensed audio is required.
    private static AudioClip Audio(string name, float frequency, int seed, bool reload)
    {
        string path = "Assets/Audio/Weapons/" + name + ".wav";
        if (!File.Exists(path))
        {
            const int rate = 22050;
            int samples = (int)(rate * (reload ? 0.16f : 0.11f));
            System.Random random = new System.Random(seed);
            using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                for (int i = 0; i < samples; i++)
                {
                    double time = (double)i / rate;
                    double envelope = Math.Exp(-time * (reload ? 45d : 35d));
                    double value = (0.65d * (random.NextDouble() * 2d - 1d) + 0.35d * Math.Sin(2d * Math.PI * frequency * time)) * envelope;
                    writer.Write((short)(value * short.MaxValue * 0.5d));
                }
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
