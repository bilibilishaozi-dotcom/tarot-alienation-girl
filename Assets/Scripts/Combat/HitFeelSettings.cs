using UnityEngine;

// 打击感总表。勾选「启用」才走这套；关掉就回到原来的顿帧和音效。
// 资源在 Assets/Resources/HitFeel/HitFeelSettings.asset，在 Project 窗口里点开就能改。
[CreateAssetMenu(fileName = "HitFeelSettings", menuName = "打击感/Hit Feel Settings")]
public class HitFeelSettings : ScriptableObject
{
    [Header("总开关")]
    public bool enabled = true;

    [Header("顿帧（秒）只冻攻击者和被击者，不改全局时间")]
    public float lightHitStop = 0.05f;
    public float heavyHitStop = 0.08f;
    public float blockHitStop = 0.04f;
    public float hurtHitStop = 0.05f;

    [Header("镜头震动（世界单位，越小越轻）")]
    public float hitShake = 0.035f;
    public float heavyShake = 0.055f;
    public float blockShake = 0.02f;
    public float hurtShake = 0.07f;
    public float impactShake = 0.05f;
    public float shakeDuration = 0.12f;

    [Header("受击")]
    public float flashDuration = 0.08f;
    public Color enemyFlash = Color.white;
    public Color playerFlash = new Color(1f, 0.28f, 0.28f, 1f);
    // 视觉上推开再拉回，不改刚体速度，所以不会改原来的击退数值。
    public float enemyShove = 0.12f;
    public float bossShove = 0f;

    [Header("火花")]
    public float sparkLife = 0.12f;
    public float sparkSize = 0.16f;
    public int sparkCount = 5;

    [Header("音效（空着就不响。仓库里没有音频，请自己拖进来）")]
    [Range(0f, 1f)] public float sfxVolume = 0.7f;
    public AudioClip swing;
    public AudioClip hit;
    public AudioClip block;
    public AudioClip hurt;
    public AudioClip jump;
    public AudioClip dash;
    public AudioClip bossTell;
    public AudioClip bossImpact;
}
