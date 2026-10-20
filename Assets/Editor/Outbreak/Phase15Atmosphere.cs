using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

/// <summary>Completes the explicitly requested sparse atmosphere and grenade smoke/flash.</summary>
public static class Phase15Atmosphere
{
    public static void Build()
    {
        OutbreakEditor.Open();
        var district = GameObject.Find("OutbreakDistrict").transform;
        if (district.Find("AtmosphereDetails") == null)
        {
            var details = OutbreakEditor.Group("AtmosphereDetails", district);
            var fire = Phase15Setup.Effect("SmallFire", new Color(1, .35f, .04f, .8f), 8, .6f, 1, .32f);
            var embers = Phase15Setup.Effect("Embers", new Color(1, .7f, .12f), 6, 1.8f, 1.6f, .035f);
            var dust = Phase15Setup.Effect("Dust", new Color(.5f, .45f, .3f, .15f), 10, 5, .1f, .12f);
            Continuous(fire, details, new Vector3(-12, .25f, 24), 14, 40, false);
            Continuous(embers, details, new Vector3(-12, .35f, 24), 4, 20, false);
            Continuous(dust, details, new Vector3(-20, 1.8f, 14), 3, 24, true);
            var glow = new GameObject("SmallFireGlow");
            glow.transform.SetParent(details);
            glow.transform.position = new Vector3(-12, 1, 24);
            var light = glow.AddComponent<Light>();
            light.color = new Color(1, .28f, .035f);
            light.range = 6;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;
            glow.AddComponent<FlickeringLight>();
            var concrete = OutbreakEditor.Material("Rubble", new Color(.3f, .31f, .32f));
            for (int i = 0; i < 4; i++)
                OutbreakEditor.Box("BrokenWall", details, new Vector3(31 + i * 1.2f, .4f + i * .25f, 3), new Vector3(1.1f, .8f + i * .5f, .5f), concrete);
            var grime = OutbreakEditor.Material("DirtGrime", new Color(.12f, .1f, .075f));
            for (int i = 0; i < 4; i++)
            {
                var patch = OutbreakEditor.Box("GroundGrime", details, new Vector3(-14 - i * 3, .035f, 24), new Vector3(2 + i * .3f, .012f, 1.2f), grime);
                Object.DestroyImmediate(patch.GetComponent<Collider>());
            }
        }

        const string path = "Assets/Prefabs/Effects/Explosion.prefab";
        var explosion = PrefabUtility.LoadPrefabContents(path);
        if (explosion.transform.Find("ExplosionSmoke") == null)
        {
            var smoke = Phase15Setup.Effect("ExplosionSmoke", new Color(.2f, .22f, .24f, .55f), 22, 2.4f, 1.3f, .6f);
            var child = (GameObject)PrefabUtility.InstantiatePrefab(smoke.gameObject);
            child.name = "ExplosionSmoke";
            child.transform.SetParent(explosion.transform, false);
            var flash = new GameObject("Flash", typeof(Light), typeof(TransientLight));
            flash.transform.SetParent(explosion.transform, false);
            var lamp = flash.GetComponent<Light>();
            lamp.color = new Color(1, .55f, .2f);
            lamp.intensity = 7;
            lamp.range = 9;
            lamp.shadows = LightShadows.None;
        }

        PrefabUtility.SaveAsPrefabAsset(explosion, path);
        PrefabUtility.UnloadPrefabContents(explosion);
        district.GetComponent<NavMeshSurface>().BuildNavMesh();
        Phase5Setup.Validate();
        OutbreakEditor.Save("15_ATMOSPHERE");
    }

    private static void Continuous(ParticleSystem prefab, Transform parent, Vector3 position, float rate, int maximum, bool volume)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(-90, 0, 0);
        var ps = go.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.maxParticles = maximum;
        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[0]);
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = volume ? ParticleSystemShapeType.Box : ParticleSystemShapeType.Cone;
        shape.radius = .25f;
        shape.angle = 12;
        if (volume)
            shape.scale = new Vector3(8, 2, 8);
    }
}
