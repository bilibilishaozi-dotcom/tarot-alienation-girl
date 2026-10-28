using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CardPossession : MonoBehaviour
{
    private const float Duration = 0.34f;

    private Player player;
    private Coroutine playing;
    private float restoreTimeScale = 1f;
    private bool ownsPause;
    private SpriteRenderer body;
    private Color bodyColor;

    public static void Play(Player player, MonsterCardData card)
    {
        if (player == null || card == null)
            return;

        CardPossession possession = player.GetComponent<CardPossession>();
        if (possession == null)
            possession = player.gameObject.AddComponent<CardPossession>();
        possession.Begin(card);
    }

    private void Begin(MonsterCardData card)
    {
        player = GetComponent<Player>();
        if (playing != null)
        {
            StopCoroutine(playing);
            if (ownsPause)
                Time.timeScale = restoreTimeScale;
            playing = null;
        }

        playing = StartCoroutine(PlayRoutine(card));
    }

    private IEnumerator PlayRoutine(MonsterCardData card)
    {
        ownsPause = Time.timeScale > 0.01f;
        restoreTimeScale = ownsPause ? Time.timeScale : 1f;
        if (ownsPause)
            Time.timeScale = 0f;

        body = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
        bodyColor = body != null ? body.color : Color.white;

        GameObject token = CreateToken(card);
        Image flash = CreateFlash();
        float elapsed = 0f;
        Vector3 start = player.transform.position + Vector3.up * 2.3f;
        Vector3 end = player.transform.position + Vector3.up * 0.35f;
        while (elapsed < Duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            if (token != null)
                token.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            if (flash != null)
            {
                Color color = flash.color;
                color.a = t < 0.45f ? Mathf.Lerp(0f, 0.72f, t / 0.45f) : Mathf.Lerp(0.72f, 0f, (t - 0.45f) / 0.55f);
                flash.color = color;
            }

            if (body != null)
                body.color = Color.Lerp(Color.white, Tint(card), Mathf.Clamp01(t * 1.4f));
            yield return null;
        }

        if (body != null)
            body.color = bodyColor;
        if (token != null)
            Destroy(token);
        if (flash != null)
            Destroy(flash.gameObject);
        if (ownsPause)
            Time.timeScale = restoreTimeScale;
        playing = null;
    }

    private static Color Tint(MonsterCardData card)
    {
        if (card == null)
            return Color.white;

        switch (card.effectType)
        {
            case CardEffectType.ChariotDash:
                return new Color(0.65f, 0.88f, 1f, 1f);
            case CardEffectType.StrengthSlam:
                return new Color(1f, 0.45f, 0.32f, 1f);
            default:
                return new Color(1f, 0.86f, 0.45f, 1f);
        }
    }

    private GameObject CreateToken(MonsterCardData card)
    {
        GameObject token = new GameObject("PossessionCard");
        SpriteRenderer sprite = token.AddComponent<SpriteRenderer>();
        sprite.sprite = RuntimeSprite.Pixel;
        sprite.color = Tint(card);
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = 30;
        token.transform.localScale = new Vector3(0.7f, 1.05f, 1f);
        return token;
    }

    private static Image CreateFlash()
    {
        GameObject canvasObject = new GameObject("PossessionFlash");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        Image image = canvasObject.AddComponent<Image>();
        image.color = new Color(1f, 0.95f, 0.85f, 0f);
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

}
