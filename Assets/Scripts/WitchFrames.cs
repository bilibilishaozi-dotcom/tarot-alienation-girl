using System.Collections.Generic;
using UnityEngine;

// 冰蓝星辉魔法少女：帧动画（初始待机 8 帧 / walk 8 帧行走）。
// 朝向由 Player.Flip() 的 Y 旋转负责，这里不做镜像。
public class WitchFrames : MonoBehaviour
{
    private const float PPU = 121f;      // 255px 人物 → 约 2.1 世界单位
    private const float IdleFps = 5f;    // 初始待机 8 帧，每帧 200ms，和 GIF 时长一致。形态待机仍是 8fps。
    private const float FormIdleFps = 8f;  // 形态 8 帧待机 = 1 秒一个呼吸循环
    private const float FormRunFps = 16f;  // 形态 8 帧奔跑 = 0.5 秒一个步循环，接近原版奔跑
    private const float FormJumpFps = 22.5f; // 形态 9 帧起跳 ≈ 0.40 秒，播完停在末帧
    private const float FormFallFps = 20f;   // 形态 9 帧下落 ≈ 0.45 秒，播完停在末帧
    private const float WalkFps = 9f;    // 8 帧 ≈ 0.9 秒一个步循环
    private const float JumpFps = 12f;   // 4 帧起跳 ≈ 0.33 秒
    private const float FallFps = 12f;   // 3 帧下落 ≈ 0.25 秒，播完停在末帧
    private const float RunFps = 14f;    // 6 帧奔跑 ≈ 0.43 秒
    private const float AttackFps = 20f; // 9 帧约 0.45 秒。不再被旧片段拉长，挥击更干脆。
    // 原版空中攻击停在 airattack_1：杖接近竖直，刀光在脚下。形态各看自己的下攻姿势，不共用这个帧号。
    // 朝左时由角色 Y 轴翻转，不另换帧。
    private const int AirAttackHoldFrame = 1;

    // LateUpdate 真正画出这一帧之后才为真。状态机的 Update 在它前面，不能拿同一帧的判断去退状态。
    public bool AirAttackHoldVisible { get; private set; }
    private const float HurtFps = 16f;   // 9 帧受击约 0.56 秒，之后停在最后一帧
    private const float WallSlideFps = 5f; // 9 帧滑墙，按 GIF 的 200 毫秒一帧循环
    private const float WallJumpFps = 36f; // 9 帧约 0.25 秒，蹬出更快，剩下的上升停在最后一帧
    // 第一、二段都是横扫；第三段是重砸 GIF 的水平翻转。
    private const bool UseAttackFrames = true;
    // 视觉上让脚「压进」地面一点点（像素靴底有描边，贴齐反而显浮）
    private const float GroundSink = 0.03f;
    // 走路/奔跑的切换速度（玩家 moveSpeed = 8，冲刺另算）
    private const float RunEnterSpeed = 5f;
    private const float RunExitSpeed = 3.5f;

    [SerializeField] private int sortingOrder = 10;
    [SerializeField] private string sortingLayer = "Player";
    [SerializeField] private float feetLocalY = -1.33f;

    private Player player;
    // Component 上已有过时的 renderer 属性，这里加 new，避免 CS0108。
    private new SpriteRenderer renderer;
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
    private int shownCombo = -1;
    private float lastVerticalSpeed;
    private bool running;    // 当前是否在播奔跑
    private float currentFps = 5f;

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
        LoadFolder("Art/Witch/Anim", null);
        LoadFolder("Art/Witch/Forms/Fool", "Fool");
        LoadFolder("Art/Witch/Forms/Chariot", "Chariot");
        LoadFolder("Art/Witch/Forms/Strength", "Strength");
    }

    // prefix 为空时用原版名字（idle、run）。形态帧存成 Fool_idle 这种名字，缺的动作不会占掉原版。
    private void LoadFolder(string resourcePath, string prefix)
    {
        Sprite[] all = Resources.LoadAll<Sprite>(resourcePath);
        Dictionary<string, List<Sprite>> grouped = new Dictionary<string, List<Sprite>>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;

            string id = all[i].name;
            int cut = id.LastIndexOf('_');
            if (cut <= 0)
                continue;

            string clip = id.Substring(0, cut);
            int index;
            if (!int.TryParse(id.Substring(cut + 1), out index))
                continue;

            string key = string.IsNullOrEmpty(prefix) ? clip : prefix + "_" + clip;
            if (!grouped.TryGetValue(key, out List<Sprite> list))
            {
                list = new List<Sprite>();
                grouped[key] = list;
            }

            while (list.Count <= index)
                list.Add(null);
            list[index] = all[i];
        }

        foreach (KeyValuePair<string, List<Sprite>> pair in grouped)
        {
            List<Sprite> list = pair.Value;
            list.RemoveAll(s => s == null);
            if (list.Count > 0)
                clips[pair.Key] = list.ToArray();
        }
    }

    // 装备愚者 / 战车 / 力量时，待机、奔跑、起跳、下落用那一套。
    // 有自己普攻帧的形态按段取。没有的牌仍用原版 attack1..3。
    private string ResolveClip(string wanted)
    {
        if (wanted == "attack1" || wanted == "attack2" || wanted == "attack3" || wanted == "airattack" || wanted == "dash")
        {
            string prefix = FormPrefix();
            if (prefix != null)
            {
                string own = prefix + "_" + wanted;
                if (clips.ContainsKey(own))
                    return own;
            }
            return wanted;
        }

        if (wanted != "idle" && wanted != "run" && wanted != "jump" && wanted != "fall")
            return wanted;

        string formKey = FormClip(wanted);
        if (formKey != null && clips.ContainsKey(formKey))
            return formKey;
        return wanted;
    }

    private string FormPrefix()
    {
        if (player == null)
            return null;

        PlayerCardLoadout loadout = player.GetComponent<PlayerCardLoadout>();
        if (loadout == null || loadout.EquippedCard == null)
            return null;

        if (loadout.EquippedCard.effectType == CardEffectType.FoolLunge)
            return "Fool";
        if (loadout.EquippedCard.effectType == CardEffectType.ChariotDash)
            return "Chariot";
        if (loadout.EquippedCard.effectType == CardEffectType.StrengthSlam)
            return "Strength";
        return null;
    }

    private string FormClip(string action)
    {
        if (player == null)
            return null;

        PlayerCardLoadout loadout = player.GetComponent<PlayerCardLoadout>();
        if (loadout == null || loadout.EquippedCard == null)
            return null;

        string prefix = FormPrefix();
        if (prefix == null)
            return null;
        return prefix + "_" + action;
    }

    private bool HasAirAttackClip()
    {
        if (clips.ContainsKey("airattack"))
            return true;
        string prefix = FormPrefix();
        return prefix != null && clips.ContainsKey(prefix + "_airattack");
    }

    // 不是空中攻击片段时返回 -1。帧号是对着合成后的角色姿势定的，不是沿用原版的 1。
    // 愚者 1：金白色下弧已经兜到身前，法杖斜指向下。第 2 帧是爆开的星光，不拿来停。
    // 战车 4：枪尖压到最下，蓝色月弧贴着枪身。第 5 帧枪身抬平，是跟着出去的那一下。
    // 力量 3：双爪带着三道爪痕挥向下方。第 4 帧爪已经收到身前，爪痕在收。
    private int AirHoldIndex(string clip)
    {
        if (clip == "Fool_airattack")
            return 1;
        if (clip == "Chariot_airattack")
            return 4;
        if (clip == "Strength_airattack")
            return 3;
        if (clip == "airattack")
            return AirAttackHoldFrame;
        return -1;
    }

    // 重开本关、牌装回来之后调用。脚底已经按原版待机吸过一次，这里只换图。
    public void SyncForm()
    {
        ApplyClip(ResolveClip("idle"), true);
    }

    // 把脚底压到地面线（所有帧共用同一张画布，所以只需要做一次）
    private void SnapFeetToGround()
    {
        if (transform.parent == null || renderer == null || renderer.sprite == null)
            return;

        Physics2D.SyncTransforms();
        // 稍微往下压一点点：像素角色的靴底有一圈描边，贴得严丝合缝反而看着像浮空
        float desiredMin = transform.parent.position.y + feetLocalY - GroundSink;
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
        currentFps = ResolveFps(clip, frames.Length);
        return true;
    }

    // 跳跃和下落仍按 Animator 时长铺帧。攻击用固定的较快帧率，不再跟着旧片段拉长。
    private float ResolveFps(string clip, int frameCount)
    {
        // 形态片段叫 Fool_idle / Chariot_run，待机 8fps、奔跑 16fps，单独设置。
        if (clip.EndsWith("_idle"))
            return FormIdleFps;
        if (clip.EndsWith("_run"))
            return FormRunFps;
        if (clip.EndsWith("_jump"))
            return FormJumpFps;
        if (clip.EndsWith("_fall"))
            return FormFallFps;
        // 空中攻击只播到各自的下攻帧就停。20fps 下愚者约 0.05 秒、力量约 0.15 秒、战车约 0.20 秒到保持帧。
        if (clip == "airattack" || clip.EndsWith("_airattack"))
            return AttackFps;
        // 一二段：动画事件和最短时间都是 0.48 秒，没有额外收招。9 帧用 20fps，约 0.45 秒播完。
        // 第三段：事件在 0.48 秒，Player_BasicAttack_3 之后还有 0.28 秒收招，一共约 0.76 秒才回待机。
        // 9 帧若用 20fps，0.40 秒就到末帧，收招里会停大约 0.36 秒。有自己第三段的形态才放慢显示。
        if (clip.EndsWith("_attack3"))
            return 11.5f;
        if (clip.Contains("_attack"))
            return AttackFps;

        float baseFps = IdleFps;
        if (clip == "walk") baseFps = WalkFps;
        else if (clip == "run") baseFps = RunFps;
        else if (clip == "jump") baseFps = JumpFps;
        else if (clip == "fall") baseFps = FallFps;
        else if (clip.StartsWith("attack") || clip == "airattack")
            return AttackFps;
        else if (clip == "hurt")
            return HurtFps;
        else if (clip == "wallslide")
            return WallSlideFps;
        else if (clip == "walljump")
            return WallJumpFps;
        else if ((clip == "dash" || clip.EndsWith("_dash")) && player != null)
        {
            // 读角色上的 dashDuration，不写死秒数。末帧放在时长的 90%。
            // 场景里现在是 0.35 秒，9 帧大约 25.4fps，第 9 帧在 0.315 秒出现。
            float duration = Mathf.Max(0.12f, player.dashDuration);
            return (frameCount - 1) / (duration * 0.9f);
        }

        bool oneShot = clip == "jump" || clip == "fall";
        if (!oneShot || player == null || player.anim == null)
            return baseFps;

        float length = player.anim.GetCurrentAnimatorStateInfo(0).length;
        if (length <= 0.05f)
            return baseFps;

        return Mathf.Clamp(frameCount / length, 6f, 30f);
    }

    private void LateUpdate()
    {
        if (player == null || renderer == null || spriteTransform == null)
            return;

        AirAttackHoldVisible = false;
        float dt = Time.deltaTime;

        bool moving = player.IsMoving && player.groundDetected && !player.IsDefeated;
        bool airborne = !player.groundDetected && !player.IsDefeated;

        // 空中阶段用「锁存」判断：起跳时进入上升段，明确开始下落（vy < -0.4）才切到下落段。
        // 否则在最高点 vy≈0 时会在 jump / fall 之间来回切，每切一次就重播第一帧 → 抖动。
        // 二段跳会把竖直速度重新拉高。这时回到上升段，jump 从第 0 帧再播。
        float verticalSpeed = player.VerticalSpeed;
        bool newHop = airborne && verticalSpeed > lastVerticalSpeed + 1f && verticalSpeed > 0.15f;
        lastVerticalSpeed = verticalSpeed;
        if (!airborne)
        {
            airPhase = 0;
        }
        else if (newHop || airPhase == 0)
        {
            airPhase = verticalSpeed > 0.15f ? 1 : 2;
        }
        else if (airPhase == 1 && verticalSpeed < -0.4f)
        {
            airPhase = 2;
        }

        string wanted = "idle";
        bool playingAttack = false;
        bool playingSpecial = false;
        if (!player.IsDefeated && player.IsDodging && clips.ContainsKey("dash"))
        {
            wanted = "dash";
            playingSpecial = true;
        }
        else if (!player.IsDefeated && player.IsHurtPose && clips.ContainsKey("hurt"))
        {
            wanted = "hurt";
            playingSpecial = true;
        }
        else if (!player.IsDefeated && player.IsWallSliding && clips.ContainsKey("wallslide"))
        {
            // 贴墙时人还在空中，要先于跳跃/下落选中，否则滑墙帧永远播不出来。
            wanted = "wallslide";
            playingSpecial = true;
        }
        else if (!player.IsDefeated && player.IsWallJumping && clips.ContainsKey("walljump"))
        {
            // 原图和滑墙一样朝向墙。蹬出时角色已经转向，这里不要再把图翻一次。
            wanted = "walljump";
            playingSpecial = true;
        }
        else if (UseAttackFrames && !player.IsDefeated && player.IsGroundAttacking)
        {
            // 只在地面连击里换图：第一、二段横扫，第三段翻转后的重砸。
            string combo = "attack" + Mathf.Clamp(player.AttackComboIndex, 1, 3);
            if (clips.ContainsKey(combo))
            {
                wanted = combo;
                playingAttack = true;
            }
        }
        else if (UseAttackFrames && !player.IsDefeated && player.IsAirAttack && HasAirAttackClip()
            && !player.groundDetected && !player.supportDetected)
        {
            // 只在还没落地时播。落地后状态还要等动画事件才退出，画面应立刻回到待机。
            wanted = "airattack";
            playingAttack = true;
        }

        bool jumpStrikeLanded = player.IsAirAttack && (player.groundDetected || player.supportDetected);

        if (playingAttack || playingSpecial || jumpStrikeLanded)
        {
            running = false;
        }
        else if (airborne)
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

        string clipName = ResolveClip(wanted);
        bool replayJump = newHop && (clipName == "jump" || clipName.EndsWith("_jump"));
        // 一二段是同一套帧。段数变了也要从第 0 帧再播，不能接着上一段的时间。
        bool restartCombo = false;
        if (player.IsGroundAttacking)
        {
            restartCombo = player.AttackComboIndex != shownCombo;
            shownCombo = player.AttackComboIndex;
        }
        else
        {
            shownCombo = -1;
        }
        ApplyClip(clipName, replayJump || restartCombo);

        frameTimer += dt;
        float step = 1f / Mathf.Max(1f, currentFps);
        while (frameTimer >= step)
        {
            frameTimer -= step;
            frameIndex++;
        }

        Sprite[] frames = clips[currentClip];
        if (frames != null && frames.Length > 0)
        {
            // 跳跃 / 下落 / 攻击都是一次性片段：播到最后停在最后一帧
            bool holdLast = currentClip == "jump" || currentClip == "fall" || currentClip == "hurt"
                || currentClip == "walljump"
                || currentClip.EndsWith("_jump") || currentClip.EndsWith("_fall")
                || currentClip.StartsWith("attack") || currentClip.Contains("_attack")
                || currentClip == "dash" || currentClip.EndsWith("_dash");
            int index;
            int airHold = AirHoldIndex(currentClip);
            if (airHold >= 0)
                index = Mathf.Min(frameIndex, airHold);
            else if (holdLast)
                index = Mathf.Min(frameIndex, frames.Length - 1);
            else
                index = frameIndex % frames.Length;
            renderer.sprite = frames[index];
            // 画到这一形态自己的下攻帧之后才算保持姿势。判定框要等这个标记，不能看片段名是否正好叫 airattack。
            AirAttackHoldVisible = airHold >= 0 && index >= airHold;
        }
        else
        {
            AirAttackHoldVisible = false;
        }

        attack = Mathf.MoveTowards(attack, player.IsAttacking ? 1f : 0f, dt * (player.IsAttacking ? 18f : 8f));
        dash = Mathf.MoveTowards(dash, player.IsDodging ? 1f : 0f, dt * 12f);
        hurt = Mathf.MoveTowards(hurt, player.IsHurtNow ? 1f : 0f, dt * 12f);

        if (player.IsDefeated)
            deathT = Mathf.Min(1f, deathT + dt * 1.6f);

        // 攻击、冲刺、受击已经有完整帧，不再把整个人拧一个角度。
        float attackTilt = UseAttackFrames ? 0f : 6f * attack;
        float dashTilt = currentClip == "dash" || currentClip.EndsWith("_dash") ? 0f : -9f * dash;
        float hurtTilt = currentClip == "hurt" ? 0f : -7f * hurt;
        float angle = attackTilt + dashTilt + hurtTilt + 78f * deathT;
        spriteTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

        // 死亡倒地时略微下沉，避免绕中心旋转后脚离地
        spriteTransform.localPosition = new Vector3(0f, baseY - 0.25f * deathT, 0f);

        Color tint = Color.white;
        if (deathT > 0f)
            tint = new Color(1f, 0.85f, 0.9f, 1f - deathT);
        else if (hurt > 0.01f && currentClip != "hurt")
            tint = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.55f, 1f), hurt);

        renderer.color = tint;
    }
}
