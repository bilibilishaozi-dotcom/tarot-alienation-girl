using System.Collections.Generic;
using UnityEngine;

// 冰蓝星辉魔法少女：帧动画（idle 4 帧呼吸 / walk 8 帧行走）。
// 朝向由 Player.Flip() 的 Y 旋转负责，这里不做镜像。
public class WitchFrames : MonoBehaviour
{
    private const float PPU = 121f;      // 255px 人物 → 约 2.1 世界单位
    private const float IdleFps = 3f;    // 4 帧 ≈ 1.3 秒一次呼吸
    private const float WalkFps = 9f;    // 8 帧 ≈ 0.9 秒一个步循环

    [SerializeField] private int sortingOrder = 10;
    [SerializeField] private string sortingLayer = "Player";
    [SerializeField] private float feetLocalY = -1.33f;

    private Player player;
    private SpriteRenderer renderer;
    private Transform spriteTransform;
    private readonly Dictionary<string, Sprite[]> clips = new Dictionary<string, Sprite[]>();

    private string currentClip = "idle";
    private int frameIndex;
    private float frameTimer;
    private float baseY;
    private float attack;
    private float dash;
    private float hurt;
    private float deathT;

    public static WitchFrames Attach(Player owner, float feetY)
    {
        GameObject obj = new GameObject("WitchFrames");
        obj.SetActive(false);
        obj.transform.SetParent(owner.transform, false);
        WitchFrames frames = obj.AddComponent<WitchFrames>();
        frames.player = owner;
        frames.feetLocalY = feetY;
        obj.SetActive(true);
        return frames;
    }

    private void Awake()
    {
        LoadClips();

        GameObject obj = new GameObject("Sprite");
        obj.transform.SetParent(transform, false);
        spriteTransform = obj.transform;
        renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = sortingOrder;

        if (!ApplyClip("idle", true))
            Debug.LogWarning("女巫帧动画没有 idle 帧：Resources/Art/Witch/Anim");

        SnapFeetToGround();
    }

    private void LoadClips()
    {
        Sprite[] all = Resources.LoadAll<Sprite>("Art/Witch/Anim");
        Dictionary<string, List<Sprite>> grouped = new Dictionary<string, List<Sprite>>();

        for (int i = 0; i < all.Length; i++)
        {
            string id = all[i].name;
            int cut = id.LastIndexOf('_');
            if (cut <= 0)
                continue;

            string clip = id.Substring(0, cut);
            int index;
            if (!int.TryParse(id.Substring(cut + 1), out index))
                continue;

            if (!grouped.TryGetValue(clip, out List<Sprite> list))
            {
                list = new List<Sprite>();
                grouped[clip] = list;
            }

            while (list.Count <= index)
                list.Add(null);
            list[index] = all[i];
        }

        foreach (KeyValuePair<string, List<Sprite>> pair in grouped)
        {
            List<Sprite> list = pair.Value;
            list.RemoveAll(s => s == null);
            clips[pair.Key] = list.ToArray();
        }
    }

    // 把脚底压到地面线（所有帧共用同一张画布，所以只需要做一次）
    private void SnapFeetToGround()
    {
        if (transform.parent == null || renderer == null || renderer.sprite == null)
            return;

        Physics2D.SyncTransforms();
        float desiredMin = transform.parent.position.y + feetLocalY;
        spriteTransform.position += new Vector3(0f, desiredMin - renderer.bounds.min.y, 0f);
        baseY = spriteTransform.localPosition.y;
    }

    private bool ApplyClip(string clip, bool force)
    {
        if (!clips.TryGetValue(clip, out Sprite[] frames) || frames.Length == 0)
            return false;

        if (!force && currentClip == clip)
            return true;

        currentClip = clip;
        frameIndex = 0;
        frameTimer = 0f;
        renderer.sprite = frames[0];
        return true;
    }

    private void LateUpdate()
    {
        if (player == null || renderer == null || spriteTransform == null)
            return;

        float dt = Time.deltaTime;

        bool moving = player.IsMoving && player.groundDetected && !player.IsDefeated;
        string wanted = "idle";
        if (!player.IsDefeated && moving)
            wanted = "walk";

        ApplyClip(wanted, false);

        // 攻击/空中没有帧，用行走帧当姿态 + 整体前压
        if (!player.groundDetected && !player.IsDefeated)
            ApplyClip("walk", false);

        float fps = currentClip == "walk" ? WalkFps : IdleFps;
        frameTimer += dt;
        float step = 1f / Mathf.Max(1f, fps);
        while (frameTimer >= step)
        {
            frameTimer -= step;
            frameIndex++;
        }

        Sprite[] frames = clips[currentClip];
        if (frames != null && frames.Length > 0)
            renderer.sprite = frames[frameIndex % frames.Length];

        attack = Mathf.MoveTowards(attack, player.IsAttacking ? 1f : 0f, dt * (player.IsAttacking ? 18f : 8f));
        dash = Mathf.MoveTowards(dash, player.IsDodging ? 1f : 0f, dt * 12f);
        hurt = Mathf.MoveTowards(hurt, player.IsHurtNow ? 1f : 0f, dt * 12f);

        if (player.IsDefeated)
            deathT = Mathf.Min(1f, deathT + dt * 1.6f);

        float angle = 6f * attack - 9f * dash - 7f * hurt + 78f * deathT;
        spriteTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

        // 死亡倒地时略微下沉，避免绕中心旋转后脚离地
        spriteTransform.localPosition = new Vector3(0f, baseY - 0.25f * deathT, 0f);

        Color tint = Color.white;
        if (deathT > 0f)
            tint = new Color(1f, 0.85f, 0.9f, 1f - deathT);
        else if (hurt > 0.01f)
            tint = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.55f, 1f), hurt);

        renderer.color = tint;
    }
}
