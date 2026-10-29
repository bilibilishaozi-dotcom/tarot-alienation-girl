using System.Collections.Generic;
using UnityEngine;

// 孢子怪的骨骼动画（待机 + 蓄力喷发）。
// 部件来自美术拆件的 parts.json：pixels、原点左上、y 向下、轴心为部件内像素点。
// 贴图放在 Resources/Art/Spore/<id>，导入设置：Sprite / PPU 100 / Point。
public class SporeRig : MonoBehaviour
{
    private const float PPU = 100f;
    private const float RootPivotX = 740f;
    private const float RootPivotY = 945f;

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

    // 顺序与 parts.json 的 layer_order 一致（从后到前）
    private static readonly PartDef[] Parts =
    {
        new PartDef("tentacle_r3", "tentacle_r2", 0, 1104, 316, 157, 179, 54, 146),
        new PartDef("tentacle_r2", "tentacle_r1", 1, 1046, 449, 192, 264, 40, 169),
        new PartDef("tentacle_r1", "body", 2, 860, 545, 221, 161, 75, 67),
        new PartDef("tentacle_l3", "tentacle_l2", 3, 202, 544, 160, 163, 114, 78),
        new PartDef("tentacle_l2", "tentacle_l1", 4, 305, 603, 204, 219, 165, 109),
        new PartDef("tentacle_l1", "body", 5, 470, 610, 196, 155, 126, 62),
        new PartDef("leg_3", "body", 6, 786, 735, 177, 220, 64, 65),
        new PartDef("leg_2", "body", 7, 663, 735, 126, 209, 59, 65),
        new PartDef("leg_1", "body", 8, 485, 720, 178, 235, 95, 60),
        new PartDef("body", "root", 9, 592, 430, 469, 401, 148, 350),
        new PartDef("leg_4b", "leg_4a", 10, 935, 760, 212, 218, 95, 42),
        new PartDef("leg_4a", "body", 11, 925, 650, 162, 151, 47, 18),
        new PartDef("head", "body", 12, 288, 160, 789, 491, 472, 340),
        new PartDef("lower_jaw", "head", 13, 334, 470, 279, 181, 226, 0),
        new PartDef("mouth_glow", "head", 14, 430, 417, 83, 92, 35, 28),
        new PartDef("mushroom_1", "head", 15, 756, 149, 156, 152, 66, 143),
        new PartDef("mushroom_2", "head", 16, 920, 327, 82, 92, 26, 85)
    };

    [SerializeField] private float targetHeight = 1.9f;   // 世界单位（脚到头顶）
    [SerializeField] private int sortingBase = 6;
    [SerializeField] private string sortingLayer = "Player";

    private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    private readonly Dictionary<string, SpriteRenderer> arts = new Dictionary<string, SpriteRenderer>();
    private BasicEnemy enemy;
    private float baseScale = 1f;
    private float charge;   // 蓄力权重
    private float burst;    // 喷发权重
    private float facing = 1f;   // 1 = 面向左（原图朝向），-1 = 面向右
    private bool dying;
    private float deathFade;
    private float hurtFlash;

    public static SporeRig Attach(Transform parent, BasicEnemy owner, float height)
    {
        GameObject obj = new GameObject("SporeRig");
        obj.transform.SetParent(parent, false);
        SporeRig rig = obj.AddComponent<SporeRig>();
        rig.targetHeight = height;
        rig.enemy = owner;
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

            // 关节在父关节坐标系里的位置（canvas 的 y 向下，转成 Unity 的 y 向上）
            bone.localPosition = new Vector3(
                (def.JointX - parentJointX) / PPU,
                -(def.JointY - parentJointY) / PPU,
                0f);

            // 贴图中心相对关节的偏移（贴图轴心在中心）
            GameObject art = new GameObject("Art");
            art.transform.SetParent(bone, false);
            art.transform.localPosition = new Vector3(
                (def.W * 0.5f - def.Px) / PPU,
                (def.Py - def.H * 0.5f) / PPU,
                0f);

            SpriteRenderer sprite = art.AddComponent<SpriteRenderer>();
            sprite.sprite = Resources.Load<Sprite>("Art/Spore/" + def.Id);
            sprite.sortingLayerName = sortingLayer;
            sprite.sortingOrder = sortingBase + def.Order;
            if (sprite.sprite == null)
                Debug.LogWarning("孢子怪缺少部件贴图：Art/Spore/" + def.Id);
            arts[def.Id] = sprite;
        }

        // 先按 parts.json 的根轴心粗放，再用真实渲染包围盒把「最低像素」精确压到父物体碰撞盒底边。
        // 只靠手算锚点容易漏掉脚趾/触手尖比根轴心更低的情况（leg_4b 就比根轴心低 33px）。
        float footDrop = (maxY - RootPivotY) / PPU;
        float footOffset = -0.5f;
        Collider2D parentBody = transform.parent != null ? transform.parent.GetComponent<Collider2D>() : null;
        if (parentBody != null)
            footOffset = parentBody.bounds.min.y - transform.parent.position.y;
        transform.localPosition = new Vector3(0f, footOffset + footDrop * baseScale, 0f);
        ApplyScale();
        SnapFeetToGround();
    }

    // 用所有部件的世界包围盒，把整只怪平移到「最低点贴着碰撞盒底边」。
    private void SnapFeetToGround()
    {
        if (transform.parent == null)
            return;

        Bounds bounds = new Bounds();
        bool hasBounds = false;
        foreach (SpriteRenderer art in arts.Values)
        {
            if (art == null || art.sprite == null)
                continue;

            if (!hasBounds)
            {
                bounds = art.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(art.bounds);
            }
        }

        if (!hasBounds)
            return;

        Collider2D parentBody = transform.parent.GetComponent<Collider2D>();
        float groundWorldY = parentBody != null
            ? parentBody.bounds.min.y
            : transform.parent.position.y - 0.5f;

        float delta = groundWorldY - bounds.min.y;
        transform.position += new Vector3(0f, delta, 0f);
    }

    private void ApplyScale()
    {
        transform.localScale = new Vector3(baseScale * facing, baseScale, 1f);
    }

    private void LateUpdate()
    {
        if (bones.Count == 0)
            return;

        float dt = Time.deltaTime;

        // 死亡：先脱离父节点（不然会被占位体的「缩小到 0.2」带着一起缩），再淡出+上飘后销毁
        if (!dying && enemy != null && enemy.IsDead)
        {
            dying = true;
            transform.SetParent(null, true);
        }

        if (dying)
        {
            deathFade += dt / 0.5f;
            float alpha = Mathf.Clamp01(1f - deathFade);
            foreach (SpriteRenderer art in arts.Values)
            {
                if (art == null)
                    continue;
                Color color = art.color;
                color.a = alpha;
                art.color = color;
            }

            transform.position += new Vector3(0f, dt * 0.7f, 0f);
            if (deathFade >= 1f)
                Destroy(gameObject);
            return;
        }

        // 受击闪白（只改 RGB，保留光核自己控制的不透明度）
        float hurtGoal = enemy != null && enemy.IsHurt ? 1f : 0f;
        hurtFlash = Mathf.MoveTowards(hurtFlash, hurtGoal, dt * 12f);
        Color tint = hurtFlash > 0f ? Color.Lerp(Color.white, new Color(1f, 0.7f, 0.62f), hurtFlash) : Color.white;
        foreach (SpriteRenderer art in arts.Values)
        {
            if (art == null)
                continue;
            Color color = art.color;
            color.r = tint.r;
            color.g = tint.g;
            color.b = tint.b;
            art.color = color;
        }

        bool winding = enemy != null && enemy.IsWindingUp;
        bool striking = enemy != null && enemy.IsStrikeActive;

        if (striking)
        {
            burst = Mathf.MoveTowards(burst, 1f, dt * 12f);
            charge = 0f;
        }
        else if (winding)
        {
            charge = Mathf.MoveTowards(charge, 1f, dt * 2.2f);
            burst = Mathf.MoveTowards(burst, 0f, dt * 10f);
        }
        else
        {
            charge = Mathf.MoveTowards(charge, 0f, dt * 4f);
            burst = Mathf.MoveTowards(burst, 0f, dt * 5f);
        }

        if (enemy != null)
        {
            float wanted = enemy.FacingDir < 0 ? 1f : -1f;   // 原图面向左
            facing = Mathf.Approximately(facing, wanted) ? wanted : Mathf.MoveTowards(facing, wanted, dt * 14f);
            ApplyScale();
        }

        float t = Time.time;
        float idle = 1f - Mathf.Max(charge, burst);

        // 身体：呼吸 + 蓄力压扁 + 喷发拉长
        Transform body = Find("body");
        if (body != null)
        {
            float breath = Mathf.Sin(t * 1.6f);
            float sy = (1f + 0.025f * breath) * Mathf.Lerp(1f, 0.86f, charge) * Mathf.Lerp(1f, 1.12f, burst);
            float sx = (1f - 0.012f * breath) * Mathf.Lerp(1f, 1.1f, charge) * Mathf.Lerp(1f, 0.94f, burst);
            body.localScale = new Vector3(sx, sy, 1f);
            SetAngle(body, 1.2f * Mathf.Sin(t * 1.1f) * idle);
        }

        // 头：轻微点头；蓄力往后仰，喷发往前压
        Transform head = Find("head");
        if (head != null)
            SetAngle(head, 2.2f * Mathf.Sin(t * 1.25f) * idle - 7f * charge + 10f * burst);

        // 下颚：平时小幅开合；蓄力微张，喷发大张
        Transform jaw = Find("lower_jaw");
        if (jaw != null)
            SetAngle(jaw, 3f * Mathf.Sin(t * 1.9f) * idle + 3f + 18f * charge + 34f * burst);

        // 口中光核：呼吸闪烁，蓄力/喷发时变大
        SpriteRenderer glow = Art("mouth_glow");
        if (glow != null && glow.sprite != null)
        {
            float pulse = 1f + 0.12f * Mathf.Sin(t * 3.1f);
            float scale = pulse * (1f + 0.35f * charge + 0.8f * burst);
            glow.transform.localScale = new Vector3(scale, scale, 1f);
            Color color = glow.color;
            color.a = Mathf.Clamp01(0.85f + 0.15f * Mathf.Sin(t * 2.3f) + 0.2f * burst);
            glow.color = color;
        }

        // 左右长触手：错相摆动；蓄力往回收，喷发往外甩
        Sway("tentacle_l1", 4.5f, 0.0f, 1.1f, t, idle, -6f, -16f);
        Sway("tentacle_l2", 6f, 0.5f, 1.1f, t, idle, -8f, -20f);
        Sway("tentacle_l3", 7.5f, 1.0f, 1.1f, t, idle, -10f, -24f);
        Sway("tentacle_r1", -4.5f, 0.35f, 1.05f, t, idle, 6f, 16f);
        Sway("tentacle_r2", -6f, 0.85f, 1.05f, t, idle, 8f, 20f);
        Sway("tentacle_r3", -7.5f, 1.35f, 1.05f, t, idle, 10f, 24f);

        // 头顶两朵蘑菇：慢慢晃
        Transform mushroom1 = Find("mushroom_1");
        if (mushroom1 != null)
            SetAngle(mushroom1, 2.5f * Mathf.Sin(t * 1.7f + 0.3f) * idle + 4f * burst);
        Transform mushroom2 = Find("mushroom_2");
        if (mushroom2 != null)
            SetAngle(mushroom2, 3f * Mathf.Sin(t * 1.45f + 1.2f) * idle + 5f * burst);
    }

    private void Sway(string id, float amplitude, float phase, float speed, float t, float idle, float chargeOffset, float burstOffset)
    {
        Transform bone = Find(id);
        if (bone == null)
            return;

        float angle = amplitude * Mathf.Sin(t * speed + phase) * idle;
        angle += chargeOffset * charge;
        angle += burstOffset * burst;
        SetAngle(bone, angle);
    }

    private Transform Find(string id)
    {
        return bones.TryGetValue(id, out Transform bone) ? bone : null;
    }

    private SpriteRenderer Art(string id)
    {
        return arts.TryGetValue(id, out SpriteRenderer art) ? art : null;
    }

    private static void SetAngle(Transform bone, float degrees)
    {
        bone.localRotation = Quaternion.Euler(0f, 0f, degrees);
    }
}
