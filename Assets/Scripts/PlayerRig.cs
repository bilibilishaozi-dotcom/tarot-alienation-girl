using System.Collections.Generic;
using UnityEngine;

// 玩家角色的骨骼动画（5 块：后脚 / 前脚 / 斗篷身体 / 兜帽+头 / 法杖）。
// 素材来自设计图的「侧面」视图，贴图在 Resources/Art/Player/<id>。
public class PlayerRig : MonoBehaviour
{
    private const float PPU = 100f;
    private const float RootPivotX = 90f;
    private const float RootPivotY = 246f;

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
        new PartDef("leg_back", "body", 0, 49, 208, 24, 33, 4.8f, -8.34f),
        new PartDef("leg_front", "body", 1, 71, 205, 71, 50, 29.52f, -5.34f),
        new PartDef("body", "root", 2, 50, 116, 95, 93, 40.3f, 26.85f),
        new PartDef("hood", "body", 3, 54, 7, 103, 114, 36.3f, 98.8f),
        new PartDef("staff", "body", 4, 10, 33, 44, 200, 32.12f, 109.85f)
    };

    [SerializeField] private float targetHeight = 1.55f;
    [SerializeField] private int sortingBase = 8;
    [SerializeField] private string sortingLayer = "Player";

    private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    private readonly Dictionary<string, SpriteRenderer> arts = new Dictionary<string, SpriteRenderer>();
    private Player player;
    private float baseScale = 1f;
    private float facing = 1f;

    // 动画权重
    private float walk;
    private float air;
    private float attack;
    private float dash;
    private float hurt;
    private float deathT;

    public static PlayerRig Attach(Player owner, float height)
    {
        GameObject obj = new GameObject("PlayerRig");
        obj.SetActive(false);
        obj.transform.SetParent(owner.transform, false);
        obj.transform.localPosition = Vector3.zero;
        PlayerRig rig = obj.AddComponent<PlayerRig>();
        rig.targetHeight = height;
        rig.player = owner;
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
            sprite.sprite = Resources.Load<Sprite>("Art/Player/" + def.Id);
            sprite.sortingLayerName = sortingLayer;
            sprite.sortingOrder = sortingBase + def.Order;
            if (sprite.sprite == null)
                Debug.LogWarning("玩家缺少部件贴图：Art/Player/" + def.Id);
            arts[def.Id] = sprite;
        }

        ApplyScale();
        SnapFeetToGround();
    }

    private void ApplyScale()
    {
        transform.localScale = new Vector3(baseScale * facing, baseScale, 1f);
    }

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

        Collider2D body = transform.parent.GetComponent<Collider2D>();
        float groundY = body != null ? body.bounds.min.y : transform.parent.position.y - 0.8f;
        transform.position += new Vector3(0f, groundY - bounds.min.y, 0f);
    }

    private void LateUpdate()
    {
        if (bones.Count == 0 || player == null)
            return;

        float dt = Time.deltaTime;
        float t = Time.time;

        // 朝向：原图朝右
        float wanted = player.facingDir < 0 ? -1f : 1f;
        facing = Mathf.Approximately(facing, wanted) ? wanted : Mathf.MoveTowards(facing, wanted, dt * 16f);
        ApplyScale();

        bool moving = player.IsMoving && player.groundDetected && !player.IsDefeated;
        bool airborne = !player.groundDetected && !player.IsDefeated;
        walk = Mathf.MoveTowards(walk, moving ? 1f : 0f, dt * 7f);
        air = Mathf.MoveTowards(air, airborne ? 1f : 0f, dt * 9f);
        attack = Mathf.MoveTowards(attack, player.IsAttacking ? 1f : 0f, dt * (player.IsAttacking ? 16f : 6f));
        dash = Mathf.MoveTowards(dash, player.IsDodging ? 1f : 0f, dt * 12f);
        hurt = Mathf.MoveTowards(hurt, player.IsHurtNow ? 1f : 0f, dt * 14f);

        // 死亡：整只躺下并淡出
        if (player.IsDefeated)
            deathT = Mathf.Min(1f, deathT + dt * 1.6f);

        float step = Mathf.Sin(t * 9f);

        // 身体：走路上下起伏、前倾；冲刺前倾；攻击前压
        Transform body = Find("body");
        if (body != null)
        {
            float bob = Mathf.Abs(step) * 0.03f * walk + Mathf.Sin(t * 1.8f) * 0.008f * (1f - walk);
            body.localPosition = new Vector3(body.localPosition.x, bob, 0f);
            SetAngle("body",
                Mathf.Sin(t * 1.8f) * 0.8f * (1f - walk)      // 待机轻晃
                - 3.5f * walk                                  // 走路前倾
                - 14f * dash                                   // 冲刺更前倾
                + 8f * attack                                  // 攻击前压
                - 5f * air                                     // 空中略后仰
                - 10f * hurt
                + 78f * deathT);
        }

        // 兜帽：走路时前后摆，攻击时甩一下
        SetAngle("hood", Mathf.Sin(t * 2.6f) * 1.6f * (1f - walk) + 3.5f * walk + 6f * attack - 5f * dash);

        // 法杖：平时轻晃，攻击时向前劈
        SetAngle("staff", Mathf.Sin(t * 1.9f) * 2.5f * (1f - walk) - 4f * walk - 58f * attack + 10f * dash);

        // 两条腿：走路交替，空中收腿
        SetAngle("leg_front", step * 26f * walk - 18f * air + 6f * dash);
        SetAngle("leg_back", -step * 26f * walk - 10f * air + 2f * dash);

        // 受伤/无敌帧：红光；死亡：淡出
        Color tint = Color.white;
        if (deathT > 0f)
            tint = new Color(1f, 0.75f, 0.75f);
        else if (hurt > 0.01f)
            tint = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.4f), hurt);
        else if (player.IsHurtNow)
            tint = new Color(1f, 0.55f, 0.55f);

        Color finalTint = tint;
        finalTint.a = 1f - deathT;
        foreach (SpriteRenderer art in arts.Values)
        {
            if (art == null)
                continue;

            art.color = finalTint;
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
