using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Replace the four legacy square combat effects in place.</summary>
public static class CombatPresentationSetup
{
    const string Folder = "Assets/Materials/Combat";

    [MenuItem("Tools/Dead city/Apply soft combat effects")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before editing effect prefabs.");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader = Shader.Find("ZombieOutbreak/Soft Combat Particle");
        if (shader == null) throw new InvalidOperationException("Soft combat shader missing.");
        var flash = Material("MuzzleFlame", shader, true, 2.1f, 1f);
        var dust = Material("ImpactDust", shader, false, 1f, 0f);
        var flesh = Material("EnemyContact", shader, false, 1f, 0f);
        var smoke = Material("MuzzleSmoke", shader, false, 1f, 0f);

        Edit("MuzzleFlash", go =>
        {
            var ps = Configure(go, flash, new Color(1f, .65f, .22f, .9f),
                1, .025f, .045f, .075f, .13f, 0, .15f, 5, .002f, 0);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var fade = ps.colorOverLifetime;
            fade.color = Fade(.95f);
            var size = ps.sizeOverLifetime;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(
                new Keyframe(0, .7f), new Keyframe(.2f, 1), new Keyframe(1, .1f)));
            Transform child = go.transform.Find("SoftMuzzleSmoke");
            if (child == null)
            {
                child = new GameObject("SoftMuzzleSmoke").transform;
                child.SetParent(go.transform, false);
            }
            var puff = Configure(child.gameObject, smoke, new Color(.35f, .33f, .30f, .18f),
                0, .16f, .3f, .025f, .05f, .12f, .3f, 12, .005f, -.025f);
            // WeaponView emits one puff per shot; no automatic repeated bursts.
            var puffMain = puff.main; puffMain.maxParticles = 16;
        });
        Edit("Impact", go => Configure(go, dust, new Color(.38f, .34f, .28f, .5f),
            5, .12f, .28f, .025f, .065f, .3f, .8f, 32, .008f, .15f));
        Edit("EnemyHit", go => Configure(go, flesh, new Color(.28f, .035f, .024f, .72f),
            5, .10f, .24f, .012f, .034f, .45f, 1.25f, 24, .006f, .45f));
        Edit("EnemyDeath", go => Configure(go, dust, new Color(.26f, .24f, .21f, .3f),
            5, .2f, .45f, .045f, .09f, .08f, .28f, 65, .025f, -.015f));
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CombatPresentationImport.txt",
            "Updated MuzzleFlash, Impact, EnemyHit and EnemyDeath in place.\n" +
            "Procedural feathered alpha; no opaque square particles.\n" +
            "Zombie contacts use contact-point flesh response, not environment impacts.\n" +
            "Existing particle prefab GUIDs and root ParticleSystem components retained.\n");
        Debug.Log("COMBAT_PRESENTATION_IMPORTED");
    }

    static Material Material(string name, Shader shader, bool additive, float intensity, float flare)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_Tint", Color.white);
        material.SetFloat("_Intensity", intensity);
        material.SetFloat("_Flare", flare);
        material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void Edit(string name, Action<GameObject> change)
    {
        string path = "Assets/Prefabs/Effects/" + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            change(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static ParticleSystem Configure(GameObject go, Material material, Color color, int count,
        float lifeMin, float lifeMax, float sizeMin, float sizeMax, float speedMin, float speedMax,
        float angle, float radius, float gravity)
    {
        var ps = go.GetComponent<ParticleSystem>();
        if (ps == null) ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.duration = .5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startColor = color; main.gravityModifier = gravity; main.maxParticles = 32;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        var emission = ps.emission;
        emission.enabled = true; emission.rateOverTime = 0; emission.rateOverDistance = 0;
        emission.SetBursts(count == 0 ? Array.Empty<ParticleSystem.Burst>() :
            new[] { new ParticleSystem.Burst(0, (short)count) });
        var shape = ps.shape;
        shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = angle; shape.radius = radius; shape.length = .01f;
        var fade = ps.colorOverLifetime;
        fade.enabled = true; fade.color = Fade(.8f);
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .55f, 1, 1.3f));
        var noise = ps.noise;
        noise.enabled = true; noise.strength = .025f; noise.frequency = 1.2f;
        noise.scrollSpeed = .3f; noise.quality = ParticleSystemNoiseQuality.Low;
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.minParticleSize = 0; renderer.maxParticleSize = .15f;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return ps;
    }

    static Gradient Fade(float peak)
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(peak, 0), new GradientAlphaKey(peak * .7f, .2f),
                    new GradientAlphaKey(0, 1) });
        return gradient;
    }
}
