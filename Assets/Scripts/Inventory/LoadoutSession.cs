using UnityEngine;

/// <summary>The only persistent session object; stores selection, never runtime ammo or enemy state.</summary>
public sealed class LoadoutSession : MonoBehaviour
{
    public static LoadoutSession Instance { get; private set; }
    public LoadoutCatalog Catalog { get; private set; }
    public int Primary { get; private set; }
    public int Equipment { get; private set; }
    public bool IsValid => Catalog != null && Catalog.Valid(Primary, Equipment);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static LoadoutSession Ensure(LoadoutCatalog catalog)
    {
        if (Instance == null)
            new GameObject("LoadoutSession").AddComponent<LoadoutSession>();
        if (Instance.Catalog != catalog)
        {
            Instance.Catalog = catalog;
            Instance.Primary = 0;
            Instance.Equipment = 0;
        }

        return Instance;
    }

    public bool Select(int primary, int equipment)
    {
        if (Catalog == null || !Catalog.Valid(primary, equipment))
            return false;
        Primary = primary;
        Equipment = equipment;
        return true;
    }
}
