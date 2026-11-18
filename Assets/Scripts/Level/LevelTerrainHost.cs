using UnityEngine;

// 场景里已经摆好的地形优先用；没有才按关卡 id 实例化对应预制体。其它关保持关闭。
public static class LevelTerrainHost
{
    public const string ResourcePath = "LevelTerrain/LevelTerrainSet";

    public static LevelTerrain Show(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
            levelId = LevelCatalog.BasicCombatId;

        LevelTerrain[] existing = Object.FindObjectsByType<LevelTerrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        LevelTerrain match = null;
        for (int i = 0; i < existing.Length; i++)
        {
            bool on = existing[i] != null && existing[i].levelId == levelId;
            if (existing[i] != null)
                existing[i].gameObject.SetActive(on);
            if (on)
                match = existing[i];
        }

        if (match != null)
            return match;

        LevelTerrainSet set = Resources.Load<LevelTerrainSet>(ResourcePath);
        if (set == null)
        {
            Debug.LogWarning("没找到关卡地形表 Resources/" + ResourcePath + "，这一关没有 Tilemap 地面。");
            return null;
        }

        LevelTerrain prefab = set.For(levelId);
        if (prefab == null)
        {
            Debug.LogWarning("地形表里没有关卡 " + levelId + "。");
            return null;
        }

        LevelTerrain instance = Object.Instantiate(prefab);
        instance.name = "LevelTerrain_" + levelId;
        instance.gameObject.SetActive(true);
        return instance;
    }
}
