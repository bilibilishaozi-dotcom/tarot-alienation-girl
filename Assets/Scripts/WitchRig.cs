using System.Collections.Generic;
using UnityEngine;

// 冰蓝星辉魔法少女（像素版）：6 块重叠拆分的骨骼。
// 每块保留自己矩形内的全部像素、按原图层顺序叠回去 = 原图，所以旋转时露出的是后面的块，不会有空洞。
public class WitchRig : MonoBehaviour
{
    private const float PPU = 100f;
    private const float RootPivotX = 430f;
    private const float RootPivotY = 1105f;

    private struct PartDef
    {
        public readonly string Id;
        public readonly string Parent;
        public readonly int Order;
        public readonly float Ox, Oy, W, H, Px, Py;

        public PartDef(string id, string parent, int order, float ox, float oy, float w, float h, float px, float py)
        {
            Id = id; Parent = parent; Order = order;
            Ox = ox; Oy = oy; W = w; H = h; Px = px; Py = py;
        }

        public float JointX => Ox + Px;
        public float JointY => Oy + Py;
    }

    // 后 → 前
    private static readonly PartDef[] Parts =
    {
        new PartDef("hair_back", "head", 0, 0, 290, 464, 771, 292, 45),
        new PartDef("legs", "body", 1, 241, 826, 396, 291, 189, 22),
        new PartDef("body", "root", 2, 137, 357, 569, 603, 293, 89),
        new PartDef("head", "body", 3, 241, 201, 378, 290, 189, 268),
        new PartDef("hat", "head", 4, 37, 0, 669, 402, 359, 312),
        new PartDef("staff", "body", 5, 551, 66, 310, 1001, 68, 514)
    };

    [SerializeField] private float targetHeight = 2.1f;
    [SerializeField] private int sortingBase = 8;
    [SerializeField] private string sortingLayer = "Player";
    [SerializeField] private float feetLocalY = -1.33f;

    private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    private readonly Dictionary<string, SpriteRenderer> arts = new Dictionary<string, SpriteRenderer>();
    private Player player;
    private float baseScale = 1f;
    private Vector3 bodyBasePos;

    private float walk;
    private float rise;
    private float fall;
    private float attack;
    private float dash;
    private float hurt;
    private float deathT;

    public static WitchRig Attach(Player owner, float feetY, float height)
    {
        GameObject obj = new GameObject("WitchRig");
        obj.SetActive(false);
        obj.transform.SetParent(owner.transform, false);
        WitchRig rig = obj.AddComponent<WitchRig>();
        rig.player = owner;
        rig.feetLocalY = feetY;
        rig.targetHeight = height;
        obj.SetActive(true);
        return rig;
    }

    private void Awake()
    {
        Build();
    }

    private void Build()
    {
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        for (int i = 0; i < Parts.Length; i++)
        {
            minY = Mathf.Min(minY, Parts[i].Oy);
            maxY = Mathf.Max(maxY, Parts[i].Oy + Parts[i].H);
        }

        float artHeight = Mathf.Max(0.1f, (maxY - minY) / PPU);
        baseScale = targetHeight / artHeight;

        for (int i = 0; i < Parts.Length; i++)
        {
            PartDef def = Parts[i];
            GameObject boneObject = new GameObject(def.Id);
            boneObject.transform.SetParent(transform, false);
            bones[def.Id] = boneObject.transform;
        }

        for (int i = 0; i < Parts.Length; i++)
        {
            PartDef def = Parts[i];
            Transform bone = bones[def.Id];
            Transform parentBone = def.Parent == "root" ? transform : bones[def.Parent];
            bone.SetParent(parentBone, false);

            float parentJointX = RootPivotX;
            float parentJointY = RootPivotY;
            if (def.Parent != "root")
            {
                for (int j = 0; j < Parts.Length; j++)
                {
                    if (Parts[j].Id != def.Parent)
                        continue;

                    parentJointX = Parts[j].JointX;
                    parentJointY = Parts[j].JointY;
                    break;
                }
            }

            bone.localPosition = new Vector3(
                (def.JointX - parentJointX) / PPU,
                -(def.JointY - parentJointY) / PPU,
                0f);

            GameObject art = new GameObject("Art");
            art.transform.SetParent(bone, false);
            art.transform.localPosition = new Vector3(
                (def.W * 0.5f - def.Px) / PPU,
                (def.Py - def.H * 0.5f) / PPU,
                0f);

            SpriteRenderer sprite = art.AddComponent<SpriteRenderer>();
            sprite.sprite = Resources.Load<Sprite>("Art/Witch/" + def.Id);
            sprite.sortingLayerName = sortingLayer;
            sprite.sortingOrder = sortingBase + def.Order;
            if (sprite.sprite == null)
                Debug.LogWarning("女巫缺少部件贴图：Art/Witch/" + def.Id);
            arts[def.Id] = sprite;
        }

        ApplyScale();

        Transform bodyBone = Find("body");
        if (bodyBone != null)
            bodyBasePos = bodyBone.localPosition;

        SnapFeetToGround();
    }

    private void ApplyScale()
    {
        transform.localScale = new Vector3(baseScale, baseScale, 1f);
    }

    // 用真实渲染包围盒把脚底压到地面线（不依赖枢轴和缩放的推算）
    private void SnapFeetToGround()
    {
        if (transform.parent == null)
            return;

        Physics2D.SyncTransforms();

        Bounds bounds = new Bounds();
        bool has = false;
        foreach (SpriteRenderer art in arts.Values)
        {
            if (art == null || art.sprite == null)
                continue;

            if (!has)
            {
                bounds = art.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(art.bounds);
            }
        }

        if (!has)
            return;

        float desiredMin = transform.parent.position.y + feetLocalY;
        transform.position += new Vector3(0f, desiredMin - bounds.min.y, 0f);
    }

    private void LateUpdate()
    {
        if (bones.Count == 0 || player == null)
            return;

        float dt = Time.deltaTime;
        float t = Time.time;

        // 朝向由 Player.Flip() 旋转玩家本体 180° 来完成（整条骨骼是玩家的子物体，会一起镜像）。
        // 这里**不能**再对 localScale.x 取反，否则两次镜像相抵消，就会出现「按左不转身」。
        if (!Mathf.Approximately(transform.localScale.x, baseScale))
            ApplyScale();

        bool moving = player.IsMoving && player.groundDetected && !player.IsDefeated;
        bool airborne = !player.groundDetected && !player.IsDefeated;
        bool rising = airborne && player.VerticalSpeed > 0.15f;

        walk = Mathf.MoveTowards(walk, moving ? 1f : 0f, dt * 8f);
        rise = Mathf.MoveTowards(rise, rising ? 1f : 0f, dt * 11f);
        fall = Mathf.MoveTowards(fall, airborne && !rising ? 1f : 0f, dt * 9f);
        attack = Mathf.MoveTowards(attack, player.IsAttacking ? 1f : 0f, dt * (player.IsAttacking ? 18f : 8f));
        dash = Mathf.MoveTowards(dash, player.IsDodging ? 1f : 0f, dt * 12f);
        hurt = Mathf.MoveTowards(hurt, player.IsHurtNow ? 1f : 0f, dt * 12f);

        if (player.IsDefeated)
            deathT = Mathf.Min(1f, deathT + dt * 1.6f);

        float step = Mathf.Sin(t * 9f);
        // 呼吸：待机时明显、走路时减弱（周期约 3.3 秒，跟帧动画的呼吸节奏接近）
        float breath = Mathf.Sin(t * 1.9f) * (1f - walk);

        // 身体：走路上下起伏 + 待机呼吸起伏；蓄力/冲刺/攻击时前压；死亡倒地
        Transform body = Find("body");
        if (body != null)
        {
            float bob = Mathf.Abs(step) * 0.03f * walk
                        + Mathf.Sin(t * 1.7f) * 0.01f * (1f - walk)
                        + breath * 0.012f;
            body.localPosition = bodyBasePos + new Vector3(0f, bob, 0f);

            // 胸腔起伏：横向略收、纵向略涨，只在待机时给到最大
            body.localScale = new Vector3(1f - 0.008f * breath, 1f + 0.016f * breath, 1f);

            SetAngle("body",
                Mathf.Sin(t * 1.7f) * 0.8f * (1f - walk)
                + 6f * attack
                - 9f * dash
                - 4f * rise
                + 3f * fall
                - 7f * hurt
                + 78f * deathT);
        }

        // 头：轻微点头，受击后仰
        SetAngle("head", Mathf.Sin(t * 2.3f + 0.4f) * 1.4f * (1f - walk) - 4f * hurt + 3f * attack);

        // 巫师帽：比头慢半拍
        SetAngle("hat", Mathf.Sin(t * 1.9f + 0.8f) * 1.6f * (1f - walk) + 2.5f * walk + 4f * rise - 5f * fall);

        // 长发：走路甩动、起跳下坠、下落上飘
        SetAngle("hair_back", Mathf.Sin(t * 2.1f) * 1.8f * (1f - walk) + 5f * walk + 7f * rise - 8f * fall + 4f * dash);

        // 法杖：待机微摆；攻击向前劈；冲刺后收
        SetAngle("staff", Mathf.Sin(t * 1.5f) * 2f * (1f - walk) - 34f * attack + 10f * dash + 4f * walk);

        // 腿：走路前后摆（重叠拆分，摆动时露出后面身体，不会破洞）
        SetAngle("legs", step * 9f * walk - 10f * rise + 6f * fall);

        Color tint = Color.white;
        if (deathT > 0f)
            tint = new Color(1f, 0.85f, 0.9f, 1f - deathT);
        else if (hurt > 0.01f)
            tint = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.55f, 1f), hurt);

        foreach (SpriteRenderer art in arts.Values)
        {
            if (art == null)
                continue;

            art.color = tint;
        }
    }

    private Transform Find(string id)
    {
        return bones.TryGetValue(id, out Transform bone) ? bone : null;
    }

    private void SetAngle(string id, float degrees)
    {
        if (bones.TryGetValue(id, out Transform bone) && bone != null)
            bone.localRotation = Quaternion.Euler(0f, 0f, degrees);
    }
}
