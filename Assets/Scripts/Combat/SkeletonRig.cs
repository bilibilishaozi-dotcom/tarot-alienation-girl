using System.Collections.Generic;
using UnityEngine;

// 暗影骷髅剑盾战士的骨骼动画（待机 + 举剑劈砍）。
// 部件来自 skel_src/parts.json（10 块），贴图在 Resources/Art/Skeleton/<id>（Sprite / PPU 100 / Point）。
public class SkeletonRig : MonoBehaviour
{
    private const float PPU = 100f;
    private const float RootPivotX = 690f;
    private const float RootPivotY = 920f;

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

    // 后 → 前；shield_arm 是只在代码里存在的肩关节，用来让盾牌单独摆动
    private static readonly PartDef[] Parts =
    {
        new PartDef("leg_l", "pelvis", 0, 458, 537, 214, 384, 182, 8),
        new PartDef("leg_r", "pelvis", 1, 744, 542, 174, 367, 26, 8),
        new PartDef("torso", "root", 2, 659, 289, 232, 178, 61, 216),
        new PartDef("pelvis", "root", 3, 624, 454, 186, 94, 96, 51),
        new PartDef("skirt", "pelvis", 4, 635, 508, 148, 270, 85, 7),
        new PartDef("pauldron", "torso", 5, 559, 233, 191, 198, 96, 67),
        new PartDef("skull", "torso", 6, 744, 192, 158, 117, 46, 113),
        new PartDef("arm_sword", "torso", 7, 788, 318, 145, 383, 7, 12),
        new PartDef("sword", "arm_sword", 8, 925, 543, 349, 203, 20, 17),
        // 虚拟肩关节：没有贴图，只提供盾牌的旋转轴心（画布坐标 620,350）
        new PartDef("shield_arm", "torso", -1, 620, 350, 0, 0, 0, 0),
        new PartDef("shield", "shield_arm", 9, 421, 369, 211, 295, 91, 143)
    };

    [SerializeField] private float targetHeight = 2.1f;
    [SerializeField] private int sortingBase = 6;
    [SerializeField] private string sortingLayer = "Player";

    private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    private readonly Dictionary<string, SpriteRenderer> arts = new Dictionary<string, SpriteRenderer>();
    private BasicEnemy enemy;
    private float baseScale = 1f;
    private float charge;
    private float burst;
    private float facing = 1f;

    public static SkeletonRig Attach(Transform parent, BasicEnemy owner, float height)
    {
        GameObject obj = new GameObject("SkeletonRig");
        obj.transform.SetParent(parent, false);
        SkeletonRig rig = obj.AddComponent<SkeletonRig>();
        rig.targetHeight = height;
        rig.enemy = owner;
        return rig;
    }

    private void Awake()
    {
        Build();
    }

    public void Build()
    {
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        for (int i = 0; i < Parts.Length; i++)
        {
            if (Parts[i].Order < 0)
                continue;
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

            if (def.Order < 0)
                continue;

            GameObject art = new GameObject("Art");
            art.transform.SetParent(bone, false);
            art.transform.localPosition = new Vector3(
                (def.W * 0.5f - def.Px) / PPU,
                (def.Py - def.H * 0.5f) / PPU,
                0f);

            SpriteRenderer sprite = art.AddComponent<SpriteRenderer>();
            sprite.sprite = Resources.Load<Sprite>("Art/Skeleton/" + def.Id);
            sprite.sortingLayerName = sortingLayer;
            sprite.sortingOrder = sortingBase + def.Order;
            if (sprite.sprite == null)
                Debug.LogWarning("骷髅缺少部件贴图：Art/Skeleton/" + def.Id);
            arts[def.Id] = sprite;
        }

        // 让 shield_arm 的轴心贴着肩，盾牌再挂在它下面
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

        Collider2D parentBody = transform.parent.GetComponent<Collider2D>();
        float groundWorldY = parentBody != null ? parentBody.bounds.min.y : transform.parent.position.y - 0.5f;
        transform.position += new Vector3(0f, groundWorldY - bounds.min.y, 0f);
    }

    private void LateUpdate()
    {
        if (bones.Count == 0)
            return;

        float dt = Time.deltaTime;

        if (enemy != null)
        {
            float wanted = enemy.FacingDir < 0 ? -1f : 1f;   // 原图面向右
            facing = Mathf.Approximately(facing, wanted) ? wanted : Mathf.MoveTowards(facing, wanted, dt * 14f);
            ApplyScale();
        }

        bool winding = enemy != null && enemy.IsWindingUp;
        bool striking = enemy != null && enemy.IsStrikeActive;
        if (striking)
        {
            burst = Mathf.MoveTowards(burst, 1f, dt * 14f);
            charge = 0f;
        }
        else if (winding)
        {
            charge = Mathf.MoveTowards(charge, 1f, dt * 3.2f);
            burst = Mathf.MoveTowards(burst, 0f, dt * 12f);
        }
        else
        {
            charge = Mathf.MoveTowards(charge, 0f, dt * 5f);
            burst = Mathf.MoveTowards(burst, 0f, dt * 6f);
        }

        float t = Time.time;
        float idle = 1f - Mathf.Max(charge, burst);

        // 躯干：站着轻微前后晃；蓄力后仰，劈砍前倾
        SetAngle("torso", 1.5f * Mathf.Sin(t * 1.1f) * idle - 5f * charge + 12f * burst);

        // 骨盆/裙摆：裙摆拖一点
        SetAngle("pelvis", 0.8f * Mathf.Sin(t * 1.3f + 0.4f) * idle);
        SetAngle("skirt", 2.5f * Mathf.Sin(t * 1.5f + 0.8f) * idle - 4f * charge + 8f * burst);

        // 头骨：点头 + 蓄力抬头
        SetAngle("skull", 2f * Mathf.Sin(t * 1.2f + 0.2f) * idle - 6f * charge + 6f * burst);

        // 剑臂：待机轻晃；蓄力抬高（负角度=往后上方），劈砍快速压下（正角度）
        SetAngle("arm_sword", 2f * Mathf.Sin(t * 1.4f + 0.6f) * idle - 52f * charge + 70f * burst);
        // 剑自身再补一点惯性
        SetAngle("sword", -6f * idle * Mathf.Sin(t * 1.4f + 1.1f) - 10f * charge + 18f * burst);

        // 盾：跟着呼吸轻摆
        SetAngle("shield_arm", 1.2f * Mathf.Sin(t * 1.6f) * idle + 3f * charge);
        SetAngle("shield", 1f * Mathf.Sin(t * 1.9f + 0.5f) * idle);

        // 腿：不动，只做极小的重心偏移，避免死板
        SetAngle("leg_l", 0.8f * Mathf.Sin(t * 1.1f + 1.5f) * idle);
        SetAngle("leg_r", 0.8f * Mathf.Sin(t * 1.1f + 2.6f) * idle);
    }

    private void SetAngle(string id, float degrees)
    {
        if (bones.TryGetValue(id, out Transform bone) && bone != null)
            bone.localRotation = Quaternion.Euler(0f, 0f, degrees);
    }
}
