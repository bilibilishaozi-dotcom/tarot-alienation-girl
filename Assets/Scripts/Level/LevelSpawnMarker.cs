using UnityEngine;

// 空物体标记。拖它就能改站在这块地形上的敌人位置。
// 孢子和骷髅的点是轴心（在台面上方 0.5），甲虫的点就在台面上。
public class LevelSpawnMarker : MonoBehaviour
{
    public string spawnId;
    public float leashMin;
    public float leashMax;
}
