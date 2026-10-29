using UnityEngine;

// 切换卡牌后挂在玩家身上的一圈「主题光」。
// 用一张纯白贴图做柔光底，颜色跟着当前卡牌走，缓慢呼吸。
public class CardAura : MonoBehaviour
{
    private SpriteRenderer glow;
    private Color target = new Color(1f, 1f, 1f, 0f);
    private static Sprite whiteSprite;

    public static void Apply(Player player, MonsterCardData card)
    {
        if (player == null)
            return;

        CardAura aura = player.GetComponent<CardAura>();
        if (aura == null)
            aura = player.gameObject.AddComponent<CardAura>();
        aura.SetCard(card);
    }

    private void SetCard(MonsterCardData card)
    {
        EnsureGlow();
        if (glow == null)
            return;

        if (card == null)
        {
            glow.enabled = false;
            return;
        }

        target = Tint(card);
        glow.enabled = true;
        glow.color = target;
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

    private void LateUpdate()
    {
        if (glow == null || !glow.enabled)
            return;

        float pulse = 0.82f + Mathf.PingPong(Time.time * 0.55f, 0.36f);
        Color color = target;
        color.a = target.a * pulse;
        glow.color = color;
    }

    private static Color Tint(MonsterCardData card)
    {
        switch (card.effectType)
        {
            case CardEffectType.ChariotDash:
                return new Color(0.35f, 0.75f, 1f, 0.26f);
            case CardEffectType.StrengthSlam:
                return new Color(1f, 0.35f, 0.2f, 0.24f);
            default:
                return new Color(1f, 0.82f, 0.3f, 0.26f);
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
