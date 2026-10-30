using System.Collections.Generic;
using UnityEngine;

// 玩家的帧动画：从设计图裁出来的 idle / walk / run / jump / airattack / attack / dash / hurt / death。
// 每帧共用「脚底对齐」的裁剪框，所以只需要按 sprite 高度算一次偏移。
public class PlayerFrameAnimator : MonoBehaviour
{
    private const float PPU = 68f;

    private Player player;
    private SpriteRenderer renderer;
    private Transform spriteTransform;
    private float feetLocalY;

    private readonly Dictionary<string, Sprite[]> clips = new Dictionary<string, Sprite[]>();
    private readonly Dictionary<string, float> clipFps = new Dictionary<string, float>
    {
        { "idle", 6f },
        { "walk", 10f },
        { "run", 14f },
        { "jump", 10f },
        { "airattack", 12f },
        { "attack", 14f },
        { "dash", 13f },
        { "hurt", 8f },
        { "death", 8f }
    };

    private string currentClip = "idle";
    private int frameIndex;
    private float frameTimer;
    private bool wasAttacking;
    private bool wasHurt;

    public static PlayerFrameAnimator Attach(Player owner, float feetY)
    {
        GameObject obj = new GameObject("PlayerFrames");
        obj.SetActive(false);
        obj.transform.SetParent(owner.transform, false);
        PlayerFrameAnimator animator = obj.AddComponent<PlayerFrameAnimator>();
        animator.player = owner;
        animator.feetLocalY = feetY;
        obj.SetActive(true);
        return animator;
    }

    private void Awake()
    {
        LoadClips();

        GameObject obj = new GameObject("Sprite");
        obj.transform.SetParent(transform, false);
        spriteTransform = obj.transform;
        renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = "Player";
        renderer.sortingOrder = 10;

        Apply("idle");
        PlaceSprite();
    }

    private void LoadClips()
    {
        Sprite[] all = Resources.LoadAll<Sprite>("Art/Player/Anim");
        Dictionary<string, List<Sprite>> grouped = new Dictionary<string, List<Sprite>>();

        foreach (Sprite sprite in all)
        {
            string name = sprite.name;
            int cut = name.LastIndexOf('_');
            if (cut <= 0)
                continue;

            string clip = name.Substring(0, cut);
            int index = 0;
            int.TryParse(name.Substring(cut + 1), out index);

            if (!grouped.TryGetValue(clip, out List<Sprite> list))
            {
                list = new List<Sprite>();
                grouped[clip] = list;
            }

            while (list.Count <= index)
                list.Add(null);
            list[index] = sprite;
        }

        foreach (KeyValuePair<string, List<Sprite>> pair in grouped)
            clips[pair.Key] = pair.Value.ToArray();

        if (clips.Count == 0)
            Debug.LogWarning("玩家帧动画没加载到任何贴图：Resources/Art/Player/Anim");
    }

    private void LateUpdate()
    {
        if (player == null || renderer == null)
            return;

        string wanted = ChooseClip();
        if (wanted != currentClip)
        {
            Apply(wanted);
            PlaceSprite();
        }

        // 攻击/受伤在切换瞬间从第一帧开始播
        bool attacking = player.IsAttacking;
        if (attacking && !wasAttacking)
        {
            frameIndex = 0;
            frameTimer = 0f;
        }
        wasAttacking = attacking;

        bool hurt = player.IsHurtNow;
        if (hurt && !wasHurt)
        {
            Apply("hurt");
            PlaceSprite();
            frameIndex = 0;
            frameTimer = 0f;
        }
        wasHurt = hurt;

        Sprite[] frames = clips[currentClip];
        if (frames == null || frames.Length == 0)
            return;

        float fps = clipFps.TryGetValue(currentClip, out float value) ? value : 10f;
        frameTimer += Time.deltaTime;
        float step = 1f / Mathf.Max(1f, fps);
        while (frameTimer >= step)
        {
            frameTimer -= step;
            frameIndex++;
        }

        if (currentClip == "death")
            frameIndex = Mathf.Min(frameIndex, frames.Length - 1);
        else if (frameIndex >= frames.Length)
            frameIndex = 0;

        renderer.sprite = frames[frameIndex];
        renderer.flipX = player.facingDir < 0;
    }

    private void PlaceSprite()
    {
        if (spriteTransform == null || renderer == null)
            return;

        // 帧图是「脚底对齐」裁的，中心枢轴 → 抬高半个 sprite 高度
        float height = renderer.sprite != null ? renderer.sprite.rect.height / PPU : 1.5f;
        spriteTransform.localPosition = new Vector3(0f, feetLocalY + height * 0.5f, 0f);
    }

    private void Apply(string clip)
    {
        currentClip = clip;
        frameIndex = 0;
        frameTimer = 0f;

        if (clips.TryGetValue(clip, out Sprite[] frames) && frames.Length > 0 && frames[0] != null)
            renderer.sprite = frames[0];
        else if (clips.TryGetValue("idle", out Sprite[] idle) && idle.Length > 0)
            renderer.sprite = idle[0];
    }

    private string ChooseClip()
    {
        if (player.IsDefeated)
            return clips.ContainsKey("death") ? "death" : "hurt";
        if (player.IsAttacking)
            return player.groundDetected ? "attack" : "airattack";
        if (player.IsDodging)
            return "dash";
        if (!player.groundDetected)
            return "jump";
        if (player.IsMoving)
            return player.HorizontalSpeed > 6f && clips.ContainsKey("run") ? "run" : "walk";
        return "idle";
    }
}
