using System.Collections.Generic;
using UnityEngine;

// 冰蓝星辉魔法少女：6 块部件的骨骼（后发 / 腿 / 衣裙 / 脸 / 巫师帽 / 星辉法杖）。
// 素材是整张立绘拆出来的，贴图在 Resources/Art/Witch/<id>（Sprite / PPU 100）。
public class WitchRig : MonoBehaviour
{
    private const float PPU = 100f;
    private const float RootPivotX = 520f;
    private const float RootPivotY = 1120f;

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

    private static readonly PartDef[] Parts =
    {
        new PartDef("hair_back", "head", 0, 17, 301, 387, 724, 383, -1),
        new PartDef("legs", "body", 1, 210, 638, 430, 476, 290, 22),
        new PartDef("body", "root", 2, 288, 328, 370, 358, 162, 152),
        new PartDef("head", "body", 3, 358, 301, 153, 72, 72, 49),
        new PartDef("hat", "head", 4, 126, 18, 562, 328, 324, 267),
        new PartDef("staff", "body", 5, 624, 21, 242, 910, 76, 479)
    };

    [SerializeField] private float targetHeight = 1.75f;
    [SerializeField] private int sortingBase = 9;
    [SerializeField] private string sortingLayer = "Player";
    [SerializeField] private float feetLocalY = -0.75f;

    private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    private readonly Dictionary<string, SpriteRenderer> arts = new Dictionary<string, SpriteRenderer>();
    private Player player;
    private float baseScale = 1f;
    private float facing = 1f;
    private Vector3 bodyBasePos;

    private float walk;
    private float rise;     // 起跳上升
    private float fall;     // 下落
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
                Debug.LogWarning("法师缺少部件贴图：Art/Witch/" + def.Id);
            arts[def.Id] = sprite;
        }

        ApplyScale();

        // 缓存 body 的基准位置——LateUpdate 里只在其上叠加呼吸起伏，不能覆盖它
        Transform bodyBone = Find("body");
        if (bodyBone != null)
            bodyBasePos = bodyBone.localPosition;

        // 脚底用「旧精灵的视觉底边」定位：旧模型站得对，新美术必然落在同一水平线
        transform.localPosition = new Vector3(0f, feetLocalY, 0f);
    }

    private void ApplyScale()
    {
        transform.localScale = new Vector3(baseScale * facing, baseScale, 1f);
    }

    private void LateUpdate()
    {
        if (bones.Count == 0 || player == null)
            return;

        float dt = Time.deltaTime;
        float t = Time.time;

        float wanted = player.facingDir < 0 ? -1f : 1f;
        facing = Mathf.Approximately(facing, wanted) ? wanted : Mathf.MoveTowards(facing, wanted, dt * 16f);
        ApplyScale();

        bool moving = player.IsMoving && player.groundDetected && !player.IsDefeated;
        bool airborne = !player.groundDetected && !player.IsDefeated;
        bool rising = airborne && player.VerticalSpeed > 0.15f;
        bool falling = airborne && !rising;

        walk = Mathf.MoveTowards(walk, moving ? 1f : 0f, dt * 7f);
        rise = Mathf.MoveTowards(rise, rising ? 1f : 0f, dt * 11f);
        fall = Mathf.MoveTowards(fall, falling ? 1f : 0f, dt * 9f);
        attack = Mathf.MoveTowards(attack, player.IsAttacking ? 1f : 0f, dt * (player.IsAttacking ? 16f : 7f));
        dash = Mathf.MoveTowards(dash, player.IsDodging ? 1f : 0f, dt * 12f);
        hurt = Mathf.MoveTowards(hurt, player.IsHurtNow ? 1f : 0f, dt * 12f);

        if (player.IsDefeated)
            deathT = Mathf.Min(1f, deathT + dt * 1.4f);

        float step = Mathf.Sin(t * 8f);

        // 身体：呼吸起伏 + 走路前倾 + 冲刺压前 + 攻击前压 + 起跳前倾/下落略后仰
        Transform body = Find("body");
        if (body != null)
        {
            float bob = Mathf.Abs(step) * 0.03f * walk + Mathf.Sin(t * 1.6f) * 0.01f * (1f - walk);
            body.localPosition = bodyBasePos + new Vector3(0f, bob, 0f);
            SetAngle("body",
                Mathf.Sin(t * 1.6f) * 1.2f * (1f - walk)
                - 4f * walk
                - 12f * dash
                + 7f * attack
                - 7f * rise
                + 4f * fall
                - 9f * hurt
                + 70f * deathT);
        }

        // 长发：走路/转身时甩；上升时往下坠、下落时往上飘（拖尾）
        SetAngle("hair_back",
            Mathf.Sin(t * 2.2f) * 2.4f * (1f - walk)
            + 6f * walk
            + 8f * dash
            - 5f * attack
            + 9f * rise
            - 11f * fall);

        // 帽子：比头发慢半拍，同样有拖尾
        SetAngle("hat",
            Mathf.Sin(t * 1.8f + 0.6f) * 1.8f * (1f - walk)
            + 4f * walk
            - 4f * attack
            + 6f * rise
            - 7f * fall);

        // 头：轻微点头
        SetAngle("head", Mathf.Sin(t * 2.4f + 0.3f) * 1.5f * (1f - walk) - 3f * hurt);

        // 法杖：待机微摆，攻击向前劈，冲刺向后收，跳跃时上举
        SetAngle("staff",
            Mathf.Sin(t * 1.5f) * 3f * (1f - walk)
            - 55f * attack
            + 12f * dash
            + Mathf.Sin(t * 8f) * 4f * walk
            + 14f * rise
            - 6f * fall);

        // 腿：走路前后摆；起跳收腿（向后摆），下落伸腿（向前摆）
        SetAngle("legs", step * 18f * walk - 16f * rise + 8f * fall + 5f * dash);

        Color tint = Color.white;
        if (deathT > 0f)
            tint = new Color(1f, 0.8f, 0.85f, 1f - deathT);
        else if (hurt > 0.01f)
            tint = Color.Lerp(Color.white, new Color(1f, 0.5f, 0.5f), hurt);

        foreach (SpriteRenderer art in arts.Values)
        {
            if (art == null)
                continue;

            Color color = art.color;
            color.r = tint.r;
            color.g = tint.g;
            color.b = tint.b;
            color.a = tint.a;
            art.color = color;
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
