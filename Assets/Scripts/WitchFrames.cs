using System.Collections.Generic;
using UnityEngine;

// 冰蓝星辉魔法少女：帧动画（idle 4 帧呼吸 / walk 8 帧行走）。
// 朝向由 Player.Flip() 的 Y 旋转负责，这里不做镜像。
public class WitchFrames : MonoBehaviour
{
    private const float PPU = 121f;      // 255px 人物 → 约 2.1 世界单位
    private const float IdleFps = 5f;    // 4 帧待机呼吸 ≈ 0.8 秒一个循环（按 GIF 原始 200ms/帧）
    private const float WalkFps = 9f;    // 8 帧 ≈ 0.9 秒一个步循环
    private const float JumpFps = 12f;   // 4 帧起跳 ≈ 0.33 秒
    private const float FallFps = 12f;   // 3 帧下落 ≈ 0.25 秒，播完停在末帧
    private const float RunFps = 14f;    // 6 帧奔跑 ≈ 0.43 秒
    // 走路/奔跑的切换速度（玩家 moveSpeed = 8，冲刺另算）
    private const float RunEnterSpeed = 5f;
    private const float RunExitSpeed = 3.5f;

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
    private int airPhase;    // 0 未在空中 / 1 上升 / 2 下落
    private bool running;    // 当前是否在播奔跑

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
        bool airborne = !player.groundDetected && !player.IsDefeated;

        // 空中阶段用「锁存」判断：起跳时进入上升段，明确开始下落（vy < -0.4）才切到下落段。
        // 否则在最高点 vy≈0 时会在 jump / fall 之间来回切，每切一次就重播第一帧 → 抖动。
        if (!airborne)
        {
            airPhase = 0;
        }
        else if (airPhase == 0)
        {
            airPhase = player.VerticalSpeed > 0.15f ? 1 : 2;
        }
        else if (airPhase == 1 && player.VerticalSpeed < -0.4f)
        {
            airPhase = 2;
        }

        string wanted = "idle";
        if (airborne)
        {
            // 上升用「向上发力」那几帧，下落用「向下发力」那几帧，各播一次停在末帧
            if (airPhase == 1 && clips.ContainsKey("jump")) wanted = "jump";
            else if (airPhase == 2 && clips.ContainsKey("fall")) wanted = "fall";
            else if (clips.ContainsKey("jump")) wanted = "jump";
        }
        else if (moving)
        {
            // 慢走 / 快跑：带一点迟滞，避免在阈值附近来回切
            float speed = player.HorizontalSpeed;
            float threshold = running ? RunExitSpeed : RunEnterSpeed;
            running = speed > threshold;
            wanted = running && clips.ContainsKey("run") ? "run" : "walk";
        }
        else
        {
            running = false;
        }

        ApplyClip(wanted, false);

        float fps = IdleFps;
        if (currentClip == "walk") fps = WalkFps;
        else if (currentClip == "run") fps = RunFps;
        else if (currentClip == "jump") fps = JumpFps;
        else if (currentClip == "fall") fps = FallFps;

        frameTimer += dt;
        float step = 1f / Mathf.Max(1f, fps);
        while (frameTimer >= step)
        {
            frameTimer -= step;
            frameIndex++;
        }

        Sprite[] frames = clips[currentClip];
        if (frames != null && frames.Length > 0)
        {
            // 跳跃/下落都是一次性片段：播到最后停在最后一帧，落地再切回 idle/walk
            bool holdLast = currentClip == "jump" || currentClip == "fall";
            int index = holdLast ? Mathf.Min(frameIndex, frames.Length - 1) : frameIndex % frames.Length;
            renderer.sprite = frames[index];
        }

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
