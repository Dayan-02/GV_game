using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class Phase13Setup
{
    [MenuItem("Zombie Outbreak/Phase 13 - Five waves")]
    public static void Build()
    {
        OutbreakEditor.Open();
        Phase2AnimationAssets.Folder("Assets/Data/Waves");
        string[] titles =
        {
            "OUTBREAK",
            "RUNNERS",
            "THE HUNT",
            "THE SIEGE",
            "THE COMMANDER"
        };
        string[] types =
        {
            "Hunter",
            "Runner",
            "Ambusher",
            "Elite"
        };
        int[, ] counts =
        {
            {
                8,
                0,
                0,
                0
            },
            {
                8,
                5,
                0,
                0
            },
            {
                8,
                5,
                3,
                0
            },
            {
                12,
                5,
                4,
                0
            },
            {
                10,
                5,
                3,
                1
            }
        };
        var definitions = new WaveDefinition[5];
        for (int w = 0; w < 5; w++)
        {
            string path = "Assets/Data/Waves/Wave" + (w + 1) + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WaveDefinition>();
                data.title = titles[w];
                data.spawnInterval = .7f;
                var entries = new System.Collections.Generic.List<WaveEntry>();
                for (int kind = 0; kind < 4; kind++)
                    if (counts[w, kind] > 0)
                        entries.Add(new WaveEntry { prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + types[kind] + ".prefab").GetComponent<EnemyActor>(), count = counts[w, kind] });
                data.entries = entries.ToArray();
                AssetDatabase.CreateAsset(data, path);
            }

            definitions[w] = data;
        }

        var world = OutbreakEditor.One<EnemyWorld>();
        var waves = OutbreakEditor.Add<WaveManager>(world.gameObject);
        OutbreakEditor.Set(waves, "world", world);
        OutbreakEditor.Set(waves, "screens", OutbreakEditor.One<GameUIController>());
        var entrances = Object.FindObjectsByType<MapMarker>().Where(m => m.kind == MapMarkerKind.ZombieEntrance).OrderBy(m => m.name).ToArray();
        OutbreakEditor.Values(waves, s =>
        {
            var list = s.FindProperty("waves");
            list.arraySize = 5;
            for (int i = 0; i < 5; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            list = s.FindProperty("entrances");
            list.arraySize = entrances.Length;
            for (int i = 0; i < entrances.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = entrances[i];
        });
        var hud = GameObject.Find("GameUI/HUDCanvas").transform;
        var presenter = OutbreakEditor.Add<GameStatusPresenter>(hud.gameObject);
        OutbreakEditor.Set(presenter, "waves", waves);
        OutbreakEditor.Set(presenter, "equipment", OutbreakEditor.One<PlayerEquipment>());
        OutbreakEditor.Set(presenter, "interaction", OutbreakEditor.One<PlayerInteractor>());
        OutbreakEditor.Set(presenter, "waveText", hud.Find("WavePlaceholders/WaveValue").GetComponent<TMP_Text>());
        OutbreakEditor.Set(presenter, "remainingText", hud.Find("WavePlaceholders/RemainingValue").GetComponent<TMP_Text>());
        var slots = hud.Find("EquipmentPlaceholders/Slots");
        slots.Find("Slot1/Number").GetComponent<TMP_Text>().text = "Q";
        slots.Find("Slot2/Number").GetComponent<TMP_Text>().text = "G";
        slots.Find("Slot3/Number").GetComponent<TMP_Text>().text = "E";
        slots.Find("Slot3/Status").GetComponent<TMP_Text>().text = "USE";
        OutbreakEditor.Set(presenter, "medkitText", slots.Find("Slot1/Status").GetComponent<TMP_Text>());
        OutbreakEditor.Set(presenter, "grenadeText", slots.Find("Slot2/Status").GetComponent<TMP_Text>());
        Phase4UIFactory.PrepareResources();
        var announcement = hud.Find("WaveAnnouncement")?.GetComponent<TMP_Text>();
        if (announcement == null)
        {
            announcement = Phase4UIFactory.Label(hud, "WaveAnnouncement", "", 34, Color.white, Vector2.zero, new Vector2(1150, 60), TextAlignmentOptions.Center);
            var rt = announcement.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, 1);
            rt.anchoredPosition = new Vector2(0, -180);
        }

        var prompt = hud.Find("InteractionPrompt")?.GetComponent<TMP_Text>();
        if (prompt == null)
        {
            prompt = Phase4UIFactory.Label(hud, "InteractionPrompt", "", 24, Color.white, Vector2.zero, new Vector2(500, 45), TextAlignmentOptions.Center);
            var rt = prompt.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, 0);
            rt.anchoredPosition = new Vector2(0, 190);
        }

        OutbreakEditor.Set(presenter, "announcementText", announcement);
        OutbreakEditor.Set(presenter, "promptText", prompt);
        OutbreakEditor.Require(definitions.Sum(w => w.Total) == 77, "Expected exactly 77 enemies across five waves.");
        OutbreakEditor.Save("13");
    }
}
