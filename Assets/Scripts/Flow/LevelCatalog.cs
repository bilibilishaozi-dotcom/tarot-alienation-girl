using System;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelCatalog", menuName = "Flow/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    public LevelSlot[] levels;

    public static LevelSlot[] Current()
    {
        LevelCatalog catalog = Resources.Load<LevelCatalog>("Flow/LevelCatalog");
        if (catalog != null && catalog.levels != null && catalog.levels.Length > 0)
            return catalog.levels;

        return Fallback;
    }

    public static LevelSlot FirstPlayable()
    {
        LevelSlot[] slots = Current();
        for (int i = 0; i < slots.Length; i++)
        {
            LevelSlot slot = slots[i];
            if (slot != null && slot.available && !string.IsNullOrEmpty(slot.sceneName))
                return slot;
        }

        return null;
    }

    private static LevelSlot[] Fallback => new[]
    {
        new LevelSlot { displayName = "第一关  愚者的裂隙", sceneName = "SampleScene", available = true },
        new LevelSlot { displayName = "第二关", sceneName = "", available = false }
    };
}

[Serializable]
public class LevelSlot
{
    public string displayName = "第一关";
    public string sceneName = "SampleScene";
    public Sprite preview;
    public bool available = true;
}
