using System;
using UnityEngine;

[Serializable]
public sealed class EquipmentPack
{
    public string name;
    [Min(0)]
    public int medkits;
    [Min(0)]
    public int grenades;
}

[CreateAssetMenu(menuName = "Zombie Outbreak/Loadout Catalog")]
public sealed class LoadoutCatalog : ScriptableObject
{
    public WeaponData[] primaries;
    public WeaponData pistol;
    public EquipmentPack[] equipment;
    public bool Valid(int primary, int pack)
    {
        if (primaries == null || equipment == null || primary < 0 || primary >= primaries.Length || pack < 0 || pack >= equipment.Length || pistol == null)
            return false;
        var weapon = primaries[primary];
        var selected = equipment[pack];
        return weapon != null && weapon.WeaponPrefab != null && pistol.WeaponPrefab != null && pistol.Category == WeaponCategory.Pistol && (weapon.Category == WeaponCategory.Rifle || weapon.Category == WeaponCategory.SMG || weapon.Category == WeaponCategory.Shotgun) && selected != null && selected.medkits >= 0 && selected.grenades >= 0;
    }
}
