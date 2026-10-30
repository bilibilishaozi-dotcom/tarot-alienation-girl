using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 打击感工具箱：命中停顿、屏幕震动、飘字、程序生成的音效。
// 全部零资源依赖，任何一处调用 Juice.XXX 即可生效。
public static class Juice
{
    private static JuiceRoot root;

    private static JuiceRoot Root
    {
        get
        {
            if (root != null)
                return root;

            GameObject obj = new GameObject("JuiceRoot");
            Object.DontDestroyOnLoad(obj);
            root = obj.AddComponent<JuiceRoot>();
            return root;
        }
    }

    // 命中停顿：一瞬间把时间几乎冻住（0.03~0.08 秒），打击会"顿"一下。
    public static void HitStop(float seconds)
    {
        if (Time.timeScale <= 0.01f)
            return;   // 暂停中不要抢时间缩放

        Root.PlayHitStop(seconds);
    }

    // 屏幕震动，power 0.1~0.6
    public static void Shake(float power)
    {
        if (power <= 0f)
            return;

        JuiceCameraShake shake = JuiceCameraShake.Get();
        if (shake != null)
            shake.Play(power);
    }

    // 伤害飘字
    public static void Number(Vector3 worldPosition, int amount, bool heavy)
    {
        if (amount <= 0)
            return;

        Root.SpawnNumber(worldPosition, amount, heavy);
    }

    // 音效
    public static void Slash()
    {
        Root.PlayClip(JuiceRoot.ClipKind.Slash, 1f);
    }

    public static void Clang()
    {
        Root.PlayClip(JuiceRoot.ClipKind.Clang, 1f);
    }

    public static void Thump()
    {
        Root.PlayClip(JuiceRoot.ClipKind.Thump, 1f);
    }
}

// 挂在常驻对象上：命中停顿计时 + 飘字 + 音源池
public class JuiceRoot : MonoBehaviour
{
    public enum ClipKind { Slash, Clang, Thump }

    private float hitStopTimer;
    private float savedScale = 1f;

    private Canvas canvas;
    private RectTransform canvasRect;
    private AudioSource[] sources;
    private int sourceIndex;
    private readonly List<FloatText> numbers = new List<FloatText>();

    private static AudioClip slashClip;
    private static AudioClip clangClip;
    private static AudioClip thumpClip;

    private void Awake()
    {
        sources = new AudioSource[4];
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.6f;
            sources[i] = source;
        }

        BuildCanvas();
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("JuiceNumbers");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 130;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasRect = canvasObject.GetComponent<RectTransform>();
    }

    private void Update()
    {
        // 命中停顿用不受缩放影响的时间计时
        if (hitStopTimer > 0f)
        {
            hitStopTimer -= Time.unscaledDeltaTime;
            if (hitStopTimer <= 0f)
            {
                if (Time.timeScale > 0f)
                    Time.timeScale = savedScale;
            }
        }

        for (int i = numbers.Count - 1; i >= 0; i--)
        {
            if (numbers[i] == null || numbers[i].Tick(Time.unscaledDeltaTime))
                numbers.RemoveAt(i);
        }
    }

    public void PlayHitStop(float seconds)
    {
        if (hitStopTimer <= 0f)
            savedScale = Time.timeScale > 0.01f ? Time.timeScale : 1f;

        hitStopTimer = Mathf.Max(hitStopTimer, seconds);
        Time.timeScale = 0.04f;
    }

    public void PlayClip(ClipKind kind, float volume)
    {
        AudioClip clip = ClipFor(kind);
        if (clip == null || sources == null)
            return;

        AudioSource source = sources[sourceIndex];
        sourceIndex = (sourceIndex + 1) % sources.Length;
        source.pitch = Random.Range(0.92f, 1.08f);
        source.PlayOneShot(clip, volume);
    }

    public void SpawnNumber(Vector3 worldPosition, int amount, bool heavy)
    {
        if (canvas == null)
            return;

        GameObject obj = new GameObject("DamageNumber");
        obj.transform.SetParent(canvas.transform, false);
        Text text = obj.AddComponent<Text>();
        text.font = GameFont.Get();
        text.fontSize = heavy ? 46 : 32;
        text.fontStyle = heavy ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = heavy ? new Color(1f, 0.86f, 0.3f) : Color.white;
        text.text = amount.ToString();
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.sizeDelta = new Vector2(200f, 60f);

        FloatText floater = obj.AddComponent<FloatText>();
        floater.Init(worldPosition, canvasRect, rect, text);
        numbers.Add(floater);
    }

    private static AudioClip ClipFor(ClipKind kind)
    {
        switch (kind)
        {
            case ClipKind.Slash:
                return slashClip != null ? slashClip : (slashClip = BuildSlash());
            case ClipKind.Clang:
                return clangClip != null ? clangClip : (clangClip = BuildClang());
            default:
                return thumpClip != null ? thumpClip : (thumpClip = BuildThump());
        }
    }

    // 挥砍：一段被包络掐出来的噪声，听感像"嗖"
    private static AudioClip BuildSlash()
    {
        const int rate = 44100;
        int count = Mathf.RoundToInt(rate * 0.16f);
        float[] data = new float[count];
        System.Random random = new System.Random(7);
        float lowpass = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Sin(Mathf.Clamp01(t / 0.16f) * Mathf.PI);
            env *= env;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            lowpass = Mathf.Lerp(lowpass, noise, 0.35f);
            data[i] = lowpass * env * 0.5f;
        }

        return Make("slash", data, rate);
    }

    // 弹刀：两个金属泛音 + 一点噪声
    private static AudioClip BuildClang()
    {
        const int rate = 44100;
        int count = Mathf.RoundToInt(rate * 0.22f);
        float[] data = new float[count];
        System.Random random = new System.Random(11);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Exp(-t * 34f);
            float tone = Mathf.Sin(2f * Mathf.PI * 1180f * t) * 0.5f
                       + Mathf.Sin(2f * Mathf.PI * 1760f * t) * 0.28f
                       + Mathf.Sin(2f * Mathf.PI * 660f * t) * 0.22f;
            float click = (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 90f) * 0.35f;
            data[i] = (tone + click) * env * 0.55f;
        }

        return Make("clang", data, rate);
    }

    // 命中：低频闷响 + 冲击噪声
    private static AudioClip BuildThump()
    {
        const int rate = 44100;
        int count = Mathf.RoundToInt(rate * 0.18f);
        float[] data = new float[count];
        System.Random random = new System.Random(3);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Exp(-t * 22f) * (1f - Mathf.Exp(-t * 600f));
            float frequency = 120f - t * 240f;
            float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Max(45f, frequency) * t);
            float noise = (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 60f) * 0.3f;
            data[i] = (tone * 0.8f + noise) * env * 0.7f;
        }

        return Make("thump", data, rate);
    }

    private static AudioClip Make(string name, float[] data, int rate)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

// 单个伤害飘字：世界坐标 → 屏幕，向上飘 + 淡出
public class FloatText : MonoBehaviour
{
    private Vector3 worldPosition;
    private RectTransform canvasRect;
    private RectTransform rect;
    private Text text;
    private float life;
    private float duration = 0.72f;
    private Vector2 drift;

    public void Init(Vector3 world, RectTransform canvas, RectTransform target, Text label)
    {
        worldPosition = world;
        canvasRect = canvas;
        rect = target;
        text = label;
        life = duration;
        drift = new Vector2(Random.Range(-14f, 14f), 0f);
        transform.localScale = Vector3.one * 1.35f;
    }

    // 返回 true 表示已经结束
    public bool Tick(float dt)
    {
        life -= dt;
        if (life <= 0f)
        {
            Destroy(gameObject);
            return true;
        }

        float k = 1f - Mathf.Clamp01(life / duration);   // 0 → 1
        Camera camera = Camera.main;
        if (camera == null || canvasRect == null || rect == null)
            return false;

        Vector3 screen = camera.WorldToScreenPoint(worldPosition + new Vector3(0f, 0.55f + k * 0.95f, 0f));
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
            rect.anchoredPosition = local + drift * k;

        float scale = Mathf.Lerp(1.35f, 1f, Mathf.Clamp01(k * 4f));
        transform.localScale = Vector3.one * scale;

        if (text != null)
        {
            Color color = text.color;
            color.a = Mathf.Clamp01(1f - Mathf.Clamp01((k - 0.45f) / 0.55f));
            text.color = color;
        }

        return false;
    }
}

// 相机震动：LateUpdate 里叠加偏移，下一帧自动撤销，和 Cinemachine 不打架。
[DefaultExecutionOrder(10000)]
public class JuiceCameraShake : MonoBehaviour
{
    private static JuiceCameraShake instance;

    private Vector3 lastOffset;
    private Quaternion lastRotation = Quaternion.identity;
    private float power;
    private float time;
    private float duration;

    public static JuiceCameraShake Get()
    {
        if (instance != null)
            return instance;

        Camera camera = Camera.main;
        if (camera == null)
            return null;

        instance = camera.GetComponent<JuiceCameraShake>();
        if (instance == null)
            instance = camera.gameObject.AddComponent<JuiceCameraShake>();
        return instance;
    }

    public void Play(float strength)
    {
        power = Mathf.Max(power, strength);
        duration = Mathf.Max(duration, 0.16f + strength * 0.22f);
        time = duration;
    }

    private void LateUpdate()
    {
        // 先撤掉上一帧叠加的量，拿到"本帧相机真实位置"
        Vector3 basePosition = transform.position - lastOffset;
        Quaternion baseRotation = transform.rotation * Quaternion.Inverse(lastRotation);
        transform.position = basePosition;
        transform.rotation = baseRotation;
        lastOffset = Vector3.zero;
        lastRotation = Quaternion.identity;

        if (time <= 0f)
            return;

        time -= Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(time / Mathf.Max(0.01f, duration));
        float amplitude = power * k * k * 0.22f;

        lastOffset = new Vector3(Random.Range(-amplitude, amplitude), Random.Range(-amplitude, amplitude), 0f);
        lastRotation = Quaternion.Euler(0f, 0f, Random.Range(-amplitude, amplitude) * 3f);
        transform.position = basePosition + lastOffset;
        transform.rotation = baseRotation * lastRotation;

        if (time <= 0f)
            power = 0f;
    }
}
