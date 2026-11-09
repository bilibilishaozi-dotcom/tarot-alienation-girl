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

    public static void PlayQuiet(Player player)
    {
        if (player == null)
            return;

        CardPossession possession = player.GetComponent<CardPossession>();
        if (possession == null)
            possession = player.gameObject.AddComponent<CardPossession>();
        possession.player = player;
        // 卸下愚者：更短、更暗，不锁操作。火花自己会消失，后面的装备演出不会把它清掉。
        possession.BeginTransform(false);
    }

    private void BeginTransform(bool strong)
    {
        player = player != null ? player : GetComponent<Player>();
        float hitStop = strong ? 0.12f : 0.05f;
        Juice.HitStop(hitStop);
        WitchFrames frames = player != null ? player.GetComponentInChildren<WitchFrames>(true) : null;
        if (frames != null)
        {
            frames.Flash(strong ? new Color(1f, 0.97f, 0.86f) : new Color(0.82f, 0.84f, 0.9f), strong ? 0.22f : 0.16f);
        }

        Vector3 center = player != null ? player.transform.position + Vector3.up * 0.8f : transform.position;
        SpawnRing(center, strong ? 12 : 6, strong ? 1.15f : 0.62f, strong ? 0.62f : 0.36f);
        if (strong && frames != null)
            StartCoroutine(GoldAfterWhite(frames));
    }

    private static IEnumerator GoldAfterWhite(WitchFrames frames)
    {
        float elapsed = 0f;
        const float wait = 0.14f;
        while (elapsed < wait)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (frames != null)
            frames.Flash(new Color(1f, 0.84f, 0.38f), 0.48f);
    }

    private IEnumerator PlayRoutine(MonsterCardData card)
    {
        // 愚者演出单独一个方法，避免和下面的 token / elapsed 等同名局部变量冲突。
        if (card != null && card.effectType == CardEffectType.FoolLunge)
        {
            yield return PlayFoolRoutine(card);
            yield break;
        }

        // 顿帧把时间缩成 0.04。若把这个数记成「恢复值」，演出结束后游戏会一直慢动作。
        ownsPause = Time.timeScale > 0.01f;
        restoreTimeScale = Time.timeScale > 0.5f ? Time.timeScale : 1f;
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

    private void OnDisable()
    {
        if (!ownsPause)
            return;

        ownsPause = false;
        if (Time.timeScale <= 0.01f)
            Time.timeScale = restoreTimeScale > 0.5f ? restoreTimeScale : 1f;
    }

    // 愚者：异化约 0.7 秒。顿帧只占开头 0.12 秒，不把整段时间停成 0。
    private IEnumerator PlayFoolRoutine(MonsterCardData card)
    {
        player = GetComponent<Player>();
        BeginTransform(true);
        GameObject foolToken = CreateToken(card);
        float foolElapsed = 0f;
        const float foolDuration = 0.68f;
        Vector3 foolStart = player.transform.position + Vector3.up * 2.3f;
        Vector3 foolEnd = player.transform.position + Vector3.up * 0.35f;
        while (foolElapsed < foolDuration)
        {
            foolElapsed += Time.unscaledDeltaTime;
            float foolT = Mathf.Clamp01(foolElapsed / foolDuration);
            if (foolToken != null)
                foolToken.transform.position = Vector3.Lerp(foolStart, foolEnd, Mathf.SmoothStep(0f, 1f, foolT));
            yield return null;
        }

        if (foolToken != null)
            Destroy(foolToken);
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
        sprite.sprite = TokenSprite();
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

    private static void SpawnRing(Vector3 center, int count, float radius, float life)
    {
        int n = Mathf.Max(1, count);
        for (int i = 0; i < n; i++)
        {
            float angle = (i / (float)n) * Mathf.PI * 2f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject dot = new GameObject("FoolSpark");
            dot.transform.position = center + (Vector3)(dir * 0.2f);
            dot.transform.localScale = Vector3.one * (n > 8 ? 0.16f : 0.12f);
            SpriteRenderer sprite = dot.AddComponent<SpriteRenderer>();
            sprite.sprite = TokenSprite();
            sprite.color = new Color(1f, 0.86f, 0.38f, 1f);
            sprite.sortingLayerName = "Player";
            sprite.sortingOrder = 40;
            FoolSpark spark = dot.AddComponent<FoolSpark>();
            spark.Begin(dir, radius, life);
        }
    }

    private static Sprite token;

    private static Sprite TokenSprite()
    {
        if (token != null)
            return token;

        Texture2D texture = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point;
        texture.Apply();
        token = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        return token;
    }
}

// 异化时绕身一圈的小金点。用不受缩放的时间，顿帧时也在散开。
public class FoolSpark : MonoBehaviour
{
    private Vector2 direction;
    private float radius;
    private float life;
    private float age;
    private Vector3 origin;
    private SpriteRenderer sprite;
    private Color color;

    public void Begin(Vector2 dir, float distance, float duration)
    {
        direction = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.up;
        radius = distance;
        life = Mathf.Max(0.08f, duration);
        origin = transform.position;
        sprite = GetComponent<SpriteRenderer>();
        color = sprite != null ? sprite.color : Color.white;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(age / life);
        transform.position = origin + (Vector3)(direction * (radius * t));
        if (sprite != null)
        {
            Color faded = color;
            faded.a = 1f - t;
            sprite.color = faded;
        }

        if (t >= 1f)
            Destroy(gameObject);
    }
}
