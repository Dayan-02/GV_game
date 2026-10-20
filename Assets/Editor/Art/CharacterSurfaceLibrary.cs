using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Deterministic, editable surface maps authored for the generated character/weapon art.</summary>
public static class CharacterSurfaceLibrary
{
    public const string Root = "Assets/Art/CharactersAndWeapons";
    private const int Size = 512;

    public static void Prepare()
    {
        Phase2AnimationAssets.Folder(Root + "/Textures");
        Phase2AnimationAssets.Folder(Root + "/Materials");
        Phase2AnimationAssets.Folder(Root + "/Meshes");
        foreach (string surface in new[] { "Canvas", "Denim", "Leather", "Skin", "Steel", "Polymer", "Wood" })
            CreateMaps(surface);
        File.WriteAllText(Root + "/Textures/SurfaceRevision2.txt", "Deterministic native surface maps, revision 2. See CharacterSurfaceLibrary.cs for source.\n");
    }

    public static Material Material(string name, string surface, Color tint, float metallic = 0, float smoothness = .2f)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
        material.color = tint;
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + surface + "_Albedo.tga");
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + surface + "_Normal.tga"));
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + surface + "_MetalSmooth.tga"));
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICGLOSSMAP");
        // Red stores full metallic mask; the scale is baked separately per material using a tint-independent shader value.
        material.SetFloat("_GlossMapScale", smoothness);
        material.SetFloat("_BumpScale", surface == "Skin" ? .18f : surface == "Steel" ? .22f : .48f);
        material.SetFloat("_Metallic", metallic);
        // Standard ignores _Metallic with a texture, so nonmetals use a black-red smoothness map.
        if (metallic < .5f)
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + surface + "_NonmetalSmooth.tga"));
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateMaps(string kind)
    {
        string prefix = Root + "/Textures/" + kind;
        if (File.Exists(prefix + "_NonmetalSmooth.tga") && File.Exists(Root + "/Textures/SurfaceRevision2.txt")) return;
        var albedo = new Color32[Size * Size]; var heights = new float[Size * Size];
        var normal = new Color32[Size * Size]; var metal = new Color32[Size * Size]; var nonmetal = new Color32[Size * Size];
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
        {
            float u = x / (float)Size, v = y / (float)Size;
            float noise = Hash(x, y), cloud = Periodic(u, v, 5), grain = Periodic(u, v, 31);
            float shade = .76f + .14f * cloud + .06f * noise, height = grain * .1f;
            if (kind == "Canvas" || kind == "Denim")
            {
                float weave = .5f + .5f * Mathf.Sin(x * Mathf.PI / 2) * Mathf.Cos(y * Mathf.PI / 2);
                float twill = kind == "Denim" ? .5f + .5f * Mathf.Sin((x + y) * Mathf.PI / 4) : weave;
                float wrinkle = Mathf.Pow(.5f + .5f * Mathf.Sin(v * Mathf.PI * 12 + Mathf.Sin(u * Mathf.PI * 6)), 14);
                shade = .69f + .08f * twill + .11f * cloud - .035f * wrinkle + .025f * noise;
                height = weave * .14f + twill * .07f + wrinkle * .025f;
            }
            else if (kind == "Leather")
            {
                float creases = Mathf.Pow(grain, 5);
                shade = .55f + .27f * cloud + .12f * noise - .22f * creases;
                height = grain * .35f + noise * .08f;
            }
            else if (kind == "Skin")
            {
                shade = .78f + .15f * cloud + .025f * noise;
                height = noise * .07f + grain * .06f;
            }
            else if (kind == "Steel")
            {
                float scratch = Hash(x / 2, y / 41) > .989f ? .28f : 0;
                shade = .58f + .16f * cloud + .06f * noise + scratch;
                height = noise * .035f - scratch * .12f;
            }
            else if (kind == "Polymer")
            {
                shade = .68f + .12f * cloud + .10f * noise;
                height = noise * .2f;
            }
            else if (kind == "Wood")
            {
                float rings = .5f + .5f * Mathf.Sin(u * Mathf.PI * 48 + 3 * Mathf.Sin(v * Mathf.PI * 2) + grain);
                shade = .5f + .22f * rings + .16f * cloud;
                height = rings * .14f;
            }
            int i = y * Size + x; heights[i] = height;
            float stain = Mathf.SmoothStep(.34f, .65f, cloud);
            albedo[i] = new Color(shade, shade * (kind == "Skin" ? .96f : .99f), shade * (kind == "Skin" ? .92f : .96f), 1);
            byte gloss = (byte)(Mathf.Lerp(.48f, 1, stain) * 255);
            metal[i] = new Color32(210, 0, 0, gloss); nonmetal[i] = new Color32(0, 0, 0, gloss);
        }
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
        {
            float dx = heights[y * Size + (x + 1) % Size] - heights[y * Size + (x + Size - 1) % Size];
            float dy = heights[((y + 1) % Size) * Size + x] - heights[((y + Size - 1) % Size) * Size + x];
            Vector3 n = new Vector3(-dx * 2, -dy * 2, 1).normalized;
            normal[y * Size + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f);
        }
        Write(prefix + "_Albedo.tga", albedo, false, false);
        Write(prefix + "_Normal.tga", normal, true, true);
        Write(prefix + "_MetalSmooth.tga", metal, false, true);
        Write(prefix + "_NonmetalSmooth.tga", nonmetal, false, true);
    }

    private static float Hash(int x, int y)
    { unchecked { uint n = (uint)(x * 374761393 + y * 668265263 + 104729); n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 65535) / 65535f; } }
    private static float Periodic(float u, float v, float frequency)
    {
        int cells=Mathf.RoundToInt(frequency);float x=u*cells,y=v*cells;
        int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);
        float sx=Mathf.SmoothStep(0,1,x-ix),sy=Mathf.SmoothStep(0,1,y-iy);
        return Mathf.Lerp(Mathf.Lerp(Hash(ix%cells,iy%cells),Hash((ix+1)%cells,iy%cells),sx),
            Mathf.Lerp(Hash(ix%cells,(iy+1)%cells),Hash((ix+1)%cells,(iy+1)%cells),sx),sy);
    }
    private static void Write(string path, Color32[] pixels, bool normal, bool linear)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(new byte[] { 0, 0, 2 }); w.Write(new byte[9]); w.Write((short)Size); w.Write((short)Size); w.Write((byte)32); w.Write((byte)8);
            foreach (var p in pixels) { w.Write(p.b); w.Write(p.g); w.Write(p.r); w.Write(p.a); }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !linear; importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true; importer.anisoLevel = 4; importer.maxTextureSize = Size;
        importer.textureCompression = TextureImporterCompression.Compressed; importer.SaveAndReimport();
    }
}
