using UnityEngine;

public static class AttackFeedback
{
    private static AudioClip swingLight;
    private static AudioClip swingHeavy;
    private static AudioClip hitLight;
    private static AudioClip hitHeavy;
    private static AudioClip thud;
    private static Sprite pixel;

    public static void Swing(Bounds bounds, int facing, bool empowered)
    {
        Color slash = empowered
            ? new Color(1f, 0.78f, 0.22f, 0.9f)
            : new Color(0.85f, 0.92f, 1f, 0.75f);
        Vector3 center = bounds.center;
        Vector3 scale = new Vector3(bounds.size.x, bounds.size.y * (empowered ? 0.95f : 0.62f), 1f);
        Spawn(center, scale, facing > 0 ? -32f : 32f, slash, empowered ? 0.18f : 0.11f, 18);

        if (empowered)
        {
            Vector3 tip = center + Vector3.right * facing * bounds.extents.x;
            Spawn(tip, new Vector3(0.28f, 0.28f, 1f), 0f, new Color(1f, 0.95f, 0.7f, 1f), 0.16f, 19);
        }

        Play(empowered ? HeavySwing : LightSwing, center, empowered ? 0.55f : 0.35f);
    }

    public static void Hit(Vector2 point, bool empowered)
    {
        Color spark = empowered
            ? new Color(1f, 0.72f, 0.18f, 1f)
            : new Color(1f, 1f, 1f, 0.95f);
        float size = empowered ? 0.72f : 0.34f;
        Spawn(point, new Vector3(size, size * 0.45f, 1f), 40f, spark, empowered ? 0.2f : 0.1f, 22);
        Play(empowered ? HeavyHit : LightHit, point, empowered ? 0.7f : 0.4f);
    }

    public static void Thud(Vector3 point)
    {
        Play(ThudClip, point, 0.6f);
    }

    private static AudioClip LightSwing => swingLight != null ? swingLight : swingLight = Tone(720f, 240f, 0.09f, 0.35f);
    private static AudioClip HeavySwing => swingHeavy != null ? swingHeavy : swingHeavy = Tone(420f, 110f, 0.16f, 0.7f);
    private static AudioClip LightHit => hitLight != null ? hitLight : hitLight = Tone(1400f, 700f, 0.06f, 0.2f);
    private static AudioClip HeavyHit => hitHeavy != null ? hitHeavy : hitHeavy = Tone(260f, 80f, 0.12f, 0.85f);
    private static AudioClip ThudClip => thud != null ? thud : thud = Tone(140f, 50f, 0.2f, 0.4f);

    private static void Play(AudioClip clip, Vector3 point, float volume)
    {
        if (clip == null)
            return;

        AudioSource.PlayClipAtPoint(clip, point, volume);
    }

    private static void Spawn(Vector3 position, Vector3 scale, float angle, Color color, float life, int order)
    {
        GameObject fx = new GameObject("AttackFx");
        fx.transform.position = position;
        fx.transform.localScale = scale;
        fx.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        SpriteRenderer sprite = fx.AddComponent<SpriteRenderer>();
        sprite.sprite = PixelSprite();
        sprite.color = color;
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = order;
        fx.AddComponent<AttackFxFade>().Begin(color, life);
    }

    private static AudioClip Tone(float startFreq, float endFreq, float duration, float noise)
    {
        const int sampleRate = 22050;
        int count = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float envelope = (1f - t) * (1f - t);
            float freq = Mathf.Lerp(startFreq, endFreq, t);
            float time = i / (float)sampleRate;
            float wave = Mathf.Sin(time * freq * Mathf.PI * 2f);
            float grit = Hash(i) * noise;
            data[i] = Mathf.Clamp((wave * (1f - noise * 0.45f) + grit) * envelope, -1f, 1f) * 0.8f;
        }

        AudioClip clip = AudioClip.Create("AttackTone", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Hash(int i)
    {
        uint x = (uint)(i * 374761393 + 668265263);
        x = (x ^ (x >> 13)) * 1274126177u;
        return ((x & 65535) / 32767.5f) - 1f;
    }

    private static Sprite PixelSprite()
    {
        if (pixel != null)
            return pixel;

        Texture2D texture = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point;
        texture.Apply();
        pixel = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        return pixel;
    }
}

public class AttackFxFade : MonoBehaviour
{
    private SpriteRenderer sprite;
    private Color color;
    private float life;
    private float age;

    public void Begin(Color startColor, float duration)
    {
        sprite = GetComponent<SpriteRenderer>();
        color = startColor;
        life = Mathf.Max(0.04f, duration);
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / life);
        Color faded = color;
        faded.a = color.a * (1f - t);
        if (sprite != null)
            sprite.color = faded;
        transform.localScale += Vector3.one * Time.deltaTime * 0.4f;
        if (t >= 1f)
            Destroy(gameObject);
    }
}
