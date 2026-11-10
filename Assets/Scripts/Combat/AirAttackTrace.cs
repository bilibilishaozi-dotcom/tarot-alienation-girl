using UnityEngine;

// 默认关。只在进入、拒绝、退出时打一行，不每帧刷。
public static class AirAttackTrace
{
    public static bool Enabled;

    public static void Log(string message)
    {
        if (!Enabled)
            return;

        Debug.Log("[空中攻击] " + message);
    }
}
