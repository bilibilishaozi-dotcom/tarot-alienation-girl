using UnityEngine;

public interface ICardEffect
{
    CardEffectType Type { get; }
    void Attach(Player player, MonsterCardData card);
    void Detach(Player player);
}

public static class CardEffects
{
    public static ICardEffect Create(CardEffectType type)
    {
        switch (type)
        {
            case CardEffectType.FoolLunge:
                return new FoolCardEffect();
            case CardEffectType.ChariotDash:
                return new ChariotCardEffect();
            case CardEffectType.StrengthSlam:
                return new StrengthCardEffect();
            default:
                return new EmptyCardEffect();
        }
    }

    private sealed class EmptyCardEffect : ICardEffect
    {
        public CardEffectType Type => CardEffectType.FoolLunge;
        public void Attach(Player player, MonsterCardData card) { }
        public void Detach(Player player) { }
    }

    private sealed class FoolCardEffect : ICardEffect
    {
        public CardEffectType Type => CardEffectType.FoolLunge;

        public void Attach(Player player, MonsterCardData card)
        {
            PlayerCardLoadout loadout = player.GetComponent<PlayerCardLoadout>();
            if (loadout == null || card == null)
                return;

            loadout.ApplyFool(card.damageMultiplier, card.lungeMultiplier, card.reachMultiplier);
        }

        public void Detach(Player player)
        {
            PlayerCardLoadout loadout = player.GetComponent<PlayerCardLoadout>();
            if (loadout != null)
                loadout.ClearFool();
        }
    }

    private sealed class ChariotCardEffect : ICardEffect
    {
        public CardEffectType Type => CardEffectType.ChariotDash;

        public void Attach(Player player, MonsterCardData card)
        {
            PlayerCardLoadout loadout = player.GetComponent<PlayerCardLoadout>();
            if (loadout == null)
                return;

            int damage = card != null ? Mathf.Max(1, card.echoDamage) : 1;
            loadout.ApplyChariot(damage);
        }

        public void Detach(Player player)
        {
            PlayerCardLoadout loadout = player.GetComponent<PlayerCardLoadout>();
            if (loadout != null)
                loadout.ClearChariot();
        }
    }

    private sealed class StrengthCardEffect : ICardEffect
    {
        private StrengthClawVisual claw;

        public CardEffectType Type => CardEffectType.StrengthSlam;

        public void Attach(Player player, MonsterCardData card)
        {
            PlayerCardLoadout loadout = player.GetComponent<PlayerCardLoadout>();
            if (loadout == null)
                return;

            int damage = card != null && card.slamDamage > 0 ? card.slamDamage : 3;
            loadout.ApplyStrength(damage);
            claw = StrengthClawVisual.Create(player.transform);
        }

        public void Detach(Player player)
        {
            PlayerCardLoadout loadout = player.GetComponent<PlayerCardLoadout>();
            if (loadout != null)
                loadout.ClearStrength();
            if (claw != null)
            {
                Object.Destroy(claw.gameObject);
                claw = null;
            }
        }
    }
}

public class StrengthClawVisual : MonoBehaviour
{
    private Vector3 baseScale = Vector3.one;
    private float pulse;

    public static StrengthClawVisual Create(Transform player)
    {
        GameObject root = new GameObject("StrengthClaw");
        root.transform.SetParent(player, false);
        root.transform.localPosition = new Vector3(0.85f, 0.15f, 0f);
        root.transform.localScale = new Vector3(1.7f, 1.15f, 1f);
        StrengthClawVisual claw = root.AddComponent<StrengthClawVisual>();
        claw.baseScale = root.transform.localScale;
        Color clawColor = new Color(0.72f, 0.16f, 0.12f, 0.92f);
        AddPiece(root.transform, new Vector3(0.15f, 0.05f, 0f), new Vector3(0.85f, 0.22f, 1f), clawColor, 6);
        AddPiece(root.transform, new Vector3(0.42f, 0.28f, 0f), new Vector3(0.18f, 0.42f, 1f), clawColor, 7);
        AddPiece(root.transform, new Vector3(0.42f, -0.18f, 0f), new Vector3(0.18f, 0.42f, 1f), clawColor, 7);
        AddPiece(root.transform, new Vector3(0.62f, 0.05f, 0f), new Vector3(0.34f, 0.16f, 1f), new Color(0.95f, 0.55f, 0.2f, 0.95f), 8);
        return claw;
    }

    public void Pulse()
    {
        pulse = 0.16f;
    }

    private void Update()
    {
        if (pulse <= 0f)
            return;

        pulse -= Time.deltaTime;
        float scale = 1f + 0.28f * Mathf.Clamp01(pulse / 0.16f);
        transform.localScale = baseScale * scale;
    }

    private static void AddPiece(Transform parent, Vector3 localPosition, Vector3 localScale, Color color, int order)
    {
        GameObject piece = new GameObject("Claw");
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = localScale;
        SpriteRenderer sprite = piece.AddComponent<SpriteRenderer>();
        sprite.sprite = WhiteSprite();
        sprite.color = color;
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = order;
    }

    private static Sprite white;

    private static Sprite WhiteSprite()
    {
        if (white != null)
            return white;

        Texture2D texture = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point;
        texture.Apply();
        white = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        return white;
    }
}
