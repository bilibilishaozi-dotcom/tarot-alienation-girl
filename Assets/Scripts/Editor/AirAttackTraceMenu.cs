#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AirAttackTraceMenu
{
    private const string Item = "Tarot/空中攻击诊断";

    [MenuItem(Item)]
    private static void Toggle()
    {
        AirAttackTrace.Enabled = !AirAttackTrace.Enabled;
        Menu.SetChecked(Item, AirAttackTrace.Enabled);
        Debug.Log("[空中攻击] 诊断" + (AirAttackTrace.Enabled ? "已打开" : "已关闭"));
    }

    [MenuItem(Item, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(Item, AirAttackTrace.Enabled);
        return true;
    }
}
#endif
