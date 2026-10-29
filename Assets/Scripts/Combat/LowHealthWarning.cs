using UnityEngine;
using UnityEngine.UI;

// 血量低于 33% 时的濒死提示：屏幕四周泛红 + 轻微心跳声。
// 贴图和音效都是运行时生成的，不依赖任何美术资源。
public class LowHealthWarning : MonoBehaviour
{
    private const float Threshold = 0.33f;
    private const float BeatPeriod = 0.9f;

    private Player player;
    private Image vignette;
    private AudioSource heartbeat;
    private float intensity;
    private float beatTimer;

    private static Sprite vignetteSprite;
    private static AudioClip heartbeatClip;

    public static void Attach(Player target)
    {
        if (target == null || target.GetComponent<LowHealthWarning>() != null)
            return;

        target.gameObject.AddComponent<LowHealthWarning>();
    }

    private void Awake()
    {
        player = GetComponent<Player>();
        BuildOverlay();
        BuildAudio();
    }

    private void BuildOverlay()
    {
        GameObject canvasObject = new GameObject("LowHealthCanvas");
        canvasObject.transform.SetParent(transform.root, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject imageObject = new GameObject("Vignette");
        imageObject.transform.SetParent(canvasObject.transform, false);
        vignette = imageObject.AddComponent<Image>();
        vignette.sprite = GetVignetteSprite();
        vignette.raycastTarget = false;
        vignette.type = Image.Type.Simple;
        RectTransform rect = vignette.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        vignette.color = new Color(1f, 1f, 1f, 0f);
    }

    private void BuildAudio()
    {
        heartbeat = gameObject.AddComponent<AudioSource>();
        heartbeat.clip = GetHeartbeatClip();
        heartbeat.loop = true;
        heartbeat.playOnAwake = false;
        heartbeat.spatialBlend = 0f;
        heartbeat.volume = 0f;
    }

    private void Update()
    {
        if (player == null || vignette == null)
            return;

        float max = Mathf.Max(1, player.MaxHealth);
        float ratio = Mathf.Clamp01((float)player.CurrentHealth / max);
        bool defeated = player.IsDefeated;

        // 越接近 0 血越强；33% 以上完全消失
        float target = defeated ? 0f : Mathf.Clamp01((Threshold - ratio) / Threshold);
        target = Mathf.SmoothStep(0f, 1f, target);
        intensity = Mathf.MoveTowards(intensity, target, Time.unscaledDeltaTime * 1.6f);

        // 心跳节奏（不受暂停影响）
        beatTimer -= Time.unscaledDeltaTime;
        if (beatTimer <= 0f)
            beatTimer += BeatPeriod;

        // 一拍两下：lub-dub，用指数衰减当脉冲包络
        float phase = 1f - Mathf.Clamp01(beatTimer / BeatPeriod);
        float pulse = Pulse(phase, 0.02f, 7f) + 0.55f * Pulse(phase, 0.30f, 9f);

        Color color = vignette.color;
        color.a = intensity * (0.26f + 0.42f * Mathf.Clamp01(pulse));
        vignette.color = color;
        vignette.enabled = color.a > 0.002f;

        if (intensity > 0.02f)
        {
            if (!heartbeat.isPlaying)
                heartbeat.Play();
            heartbeat.volume = Mathf.Lerp(0.1f, 0.34f, intensity);
            heartbeat.pitch = Mathf.Lerp(1.12f, 0.94f, intensity);
        }
        else if (heartbeat.isPlaying)
        {
            heartbeat.Stop();
        }
    }

    private static float Pulse(float phase, float at, float sharpness)
    {
        float d = phase - at;
        if (d < 0f)
            d += 1f;

        return Mathf.Exp(-d * sharpness);
    }

    private static Sprite GetVignetteSprite()
    {
        if (vignetteSprite != null)
            return vignetteSprite;

        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(nx * nx * 0.82f + ny * ny);
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 1.02f, distance));
                pixels[y * size + x] = new Color(0.78f, 0.04f, 0.06f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;

        vignetteSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        vignetteSprite.hideFlags = HideFlags.HideAndDontSave;
        return vignetteSprite;
    }

    private static AudioClip GetHeartbeatClip()
    {
        if (heartbeatClip != null)
            return heartbeatClip;

        const int rate = 44100;
        int samples = Mathf.RoundToInt(rate * BeatPeriod);
        float[] data = new float[samples];

        // 第一下重、第二下轻，间隔约 0.28 秒
        AddThump(data, rate, 0.02f, 0.17f, 0.95f);
        AddThump(data, rate, 0.30f, 0.15f, 0.62f);

        heartbeatClip = AudioClip.Create("Heartbeat", samples, 1, rate, false);
        heartbeatClip.SetData(data, 0);
        return heartbeatClip;
    }

    private static void AddThump(float[] data, int rate, float start, float length, float volume)
    {
        int from = Mathf.RoundToInt(start * rate);
        int count = Mathf.RoundToInt(length * rate);
        for (int i = 0; i < count; i++)
        {
            int index = from + i;
            if (index >= data.Length)
                break;

            float t = (float)i / rate;
            // 快速起音 + 指数衰减，音高随时间下滑，像胸腔里的闷响
            float envelope = Mathf.Exp(-t * 24f) * (1f - Mathf.Exp(-t * 800f));
            float frequency = 62f - t * 26f;
            float tone = Mathf.Sin(2f * Mathf.PI * frequency * t);
            data[index] += tone * envelope * volume;
        }
    }
}
