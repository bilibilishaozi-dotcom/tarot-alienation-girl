using UnityEngine;

// 换上某张牌的瞬间，玩家身后闪一下对应颜色的光，然后淡出。
// 不是常驻光环 —— 只在装备/切换的那一刻播一次。
public class CardAura : MonoBehaviour
{
    private const float FlashDuration = 0.45f;

    private SpriteRenderer glow;
    private Color tint = Color.white;
    private float flashLeft;
    private static Sprite whiteSprite;

    public static void Apply(Player player, MonsterCardData card)
    {
        if (player == null)
            return;

        // 正在停用/销毁（例如切场景时 PlayerCardLoadout.OnDisable）就不要挂组件了
        if (!player.isActiveAndEnabled)
            return;

        CardAura aura = player.GetComponent<CardAura>();
        if (aura == null)
            aura = player.gameObject.AddComponent<CardAura>();
        aura.Flash(card);
    }

    private void Flash(MonsterCardData card)
    {
        // 卸下卡牌（包括切场景时 PlayerCardLoadout.OnDisable 触发的这次）：
        // 只关掉已有的光，绝对不能在这里新建子物体——
        // 父物体正在停用/销毁时 SetParent 会报
        // "Cannot set the parent of the GameObject ... while activating or deactivating"，
        // 还会留下没清理干净的对象。
        if (card == null)
        {
            if (glow != null)
            {
                glow.enabled = false;
                flashLeft = 0f;
            }

            return;
        }

        if (!isActiveAndEnabled)
            return;

        EnsureGlow();
        if (glow == null)
            return;

        tint = Tint(card);
        flashLeft = FlashDuration;
        glow.enabled = true;
        glow.color = tint;
        glow.transform.localScale = new Vector3(2.7f * 0.8f, 3.2f * 0.8f, 1f);
    }

    private void Update()
    {
        if (glow == null || flashLeft <= 0f)
            return;

        // 状态栏里换卡时游戏是暂停的（timeScale = 0），所以用不受缩放影响的时间
        flashLeft -= Time.unscaledDeltaTime;
        if (flashLeft <= 0f)
        {
            flashLeft = 0f;
            glow.enabled = false;
            return;
        }

        float t = flashLeft / FlashDuration;          // 1 → 0
        Color color = tint;
        color.a = tint.a * t;                          // 逐渐淡出
        glow.color = color;

        float scale = Mathf.Lerp(0.8f, 1.2f, t);        // 一闪的同时往外扩
        glow.transform.localScale = new Vector3(2.7f * scale, 3.2f * scale, 1f);
    }

    private void EnsureGlow()
    {
        if (glow != null)
            return;

        GameObject obj = new GameObject("CardAura");
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        obj.transform.localScale = new Vector3(2.7f, 3.2f, 1f);

        glow = obj.AddComponent<SpriteRenderer>();
        glow.sprite = WhiteSprite();
        glow.sortingLayerName = "Player";
        glow.sortingOrder = -6;
        glow.enabled = false;
    }

    private static Color Tint(MonsterCardData card)
    {
        switch (card.effectType)
        {
            case CardEffectType.ChariotDash:
                return new Color(0.45f, 0.8f, 1f, 0.55f);
            case CardEffectType.StrengthSlam:
                return new Color(1f, 0.42f, 0.24f, 0.5f);
            default:
                return new Color(1f, 0.84f, 0.35f, 0.55f);
        }
    }

    private static Sprite WhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        Texture2D texture = new Texture2D(4, 4);
        texture.hideFlags = HideFlags.HideAndDontSave;
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point;
        texture.Apply();

        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return whiteSprite;
    }
}
