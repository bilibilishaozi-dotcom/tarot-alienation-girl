using UnityEngine;

// 挂在每一关地形预制体的根上。运行时按 levelId 只打开对应的那一套。
public class LevelTerrain : MonoBehaviour
{
    public string levelId = LevelCatalog.BasicCombatId;

    public bool TrySpawn(string spawnId, out Vector3 position, out float leashMin, out float leashMax)
    {
        position = Vector3.zero;
        leashMin = 0f;
        leashMax = 0f;
        LevelSpawnMarker[] markers = GetComponentsInChildren<LevelSpawnMarker>(true);
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null && markers[i].spawnId == spawnId)
            {
                position = markers[i].transform.position;
                leashMin = markers[i].leashMin;
                leashMax = markers[i].leashMax;
                return true;
            }
        }

        return false;
    }
}
