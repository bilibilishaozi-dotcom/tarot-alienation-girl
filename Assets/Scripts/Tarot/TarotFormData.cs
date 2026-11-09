using UnityEngine;

// 一种塔罗形态的数据。换形态时只读这里，不在 Player 里写死资源路径。
[CreateAssetMenu(fileName = "TarotForm", menuName = "Tarot/Form")]
public class TarotFormData : ScriptableObject
{
    public TarotForm form = TarotForm.Original;
    public string displayName = "原版";

    [Tooltip("0 = 不换色。1 = 愚者调色（和 Tools/tarot_palette_preview.py 同一套规则）。")]
    public int paletteId;

    [Tooltip("使用 Tarot/PaletteSprite 的材质。运行时会复制一份，不会改到这份资源本身。")]
    public Material paletteMaterial;

    [Tooltip("留给以后轮廓不一样的形态。这版不套用：现在的走路图是 WitchFrames 在换，Animator 还负责攻击事件，中途换控制器会把攻击状态重置。")]
    public AnimatorOverrideController animatorOverride;

    [Tooltip("可选。换上这个形态时挂到玩家脚下，离开时拆掉。不改玩家的位置和碰撞。")]
    public GameObject effectPrefab;
}
