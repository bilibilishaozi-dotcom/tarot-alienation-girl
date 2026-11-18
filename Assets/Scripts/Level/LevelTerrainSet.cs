using UnityEngine;

// 四关地形预制体的入口。放在 Resources 里，FirstLevel 按关卡 id 取用。
public class LevelTerrainSet : ScriptableObject
{
    public LevelTerrain basicCombat;
    public LevelTerrain foolTrial;
    public LevelTerrain echoRoom;
    public LevelTerrain bossArena;

    public LevelTerrain For(string levelId)
    {
        if (levelId == LevelCatalog.FoolTrialId)
            return foolTrial;
        if (levelId == LevelCatalog.ChariotTrialId)
            return echoRoom;
        if (levelId == LevelCatalog.BossId)
            return bossArena;
        return basicCombat;
    }
}
