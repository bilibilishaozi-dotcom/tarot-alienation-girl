using System;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelCatalog", menuName = "Flow/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    public const string BasicCombatId = "basic";
    public const string FoolTrialId = "fool";

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

    public static LevelSlot Find(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
            return null;

        LevelSlot[] slots = Current();
        for (int i = 0; i < slots.Length; i++)
        {
            LevelSlot slot = slots[i];
            if (slot != null && slot.levelId == levelId && slot.available && !string.IsNullOrEmpty(slot.sceneName))
                return slot;
        }

        return null;
    }

    private static LevelSlot[] Fallback => new[]
    {
        new LevelSlot { displayName = "第一关  基础战斗", sceneName = "SampleScene", levelId = BasicCombatId, available = true },
        new LevelSlot { displayName = "愚者牌试用", sceneName = "SampleScene", levelId = FoolTrialId, available = true }
    };
}

[Serializable]
public class LevelSlot
{
    public string displayName = "第一关";
    public string sceneName = "SampleScene";
    public string levelId = LevelCatalog.BasicCombatId;
    public Sprite preview;
    public bool available = true;
}
