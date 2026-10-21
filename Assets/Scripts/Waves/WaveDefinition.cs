using System;
using UnityEngine;

[Serializable]
public sealed class WaveEntry
{
    public EnemyActor prefab;
    [Min(1)]
    public int count = 1;
}

[CreateAssetMenu(menuName = "Dead city/Wave Definition")]
public sealed class WaveDefinition : ScriptableObject
{
    public string title;
    [Min(.05f)]
    public float spawnInterval = .8f;
    public WaveEntry[] entries;
    public int Total
    {
        get
        {
            int total = 0;
            if (entries != null)
                foreach (var entry in entries)
                    if (entry != null)
                        total += Mathf.Max(0, entry.count);
            return total;
        }
    }

    public bool Valid
    {
        get
        {
            if (entries == null || entries.Length == 0)
                return false;
            foreach (var e in entries)
                if (e == null || e.prefab == null || e.count < 1)
                    return false;
            return true;
        }
    }
}
