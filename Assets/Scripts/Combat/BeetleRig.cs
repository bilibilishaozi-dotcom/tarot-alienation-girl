using System.Collections.Generic;
using UnityEngine;

// 暗影甲虫：8 块部件的纸片骨骼。
// 待机会六足交替爬动 + 甲壳轻微起伏；普通攻击弹开（IsBlocked）时甲壳闪光并往后退一点；
// 从上方挨打（IsHurt）时甲壳压下去。
public class BeetleRig : MonoBehaviour
{
    private const float PPU = 100f;
    private const float RootPivotX = 450f;
    private const float RootPivotY = 520f;

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
        new PartDef("body", "root", 0, 302, 359, 441, 160, 148, 41),
        new PartDef("shell", "body", 1, 254, 30, 536, 369, 546, 310),
        new PartDef("horn", "body", 2, 16, 72, 317, 407, 284, 288),
        new PartDef("leg_far_front", "body", 3, 120, 392, 163, 142, 130, 8),
        new PartDef("leg_mid_a", "body", 4, 279, 378, 156, 157, 121, 7),
        new PartDef("leg_mid_b", "body", 5, 431, 383, 147, 152, 119, 7),
        new PartDef("leg_back_a", "body", 6, 563, 373, 158, 162, 97, 7),
        new PartDef("leg_back_b", "body", 7, 720, 368, 147, 166, 80, 2)
    };

    [SerializeField] private float targetHeight = 1.15f;
    [SerializeField] private int sortingBase = 6;
    [SerializeField] private string sortingLayer = "Player";

    private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    private readonly Dictionary<string, SpriteRenderer> arts = new Dictionary<string, SpriteRenderer>();
    private BasicEnemy enemy;
    private float baseScale = 1f;
    private float facing = 1f;
    private float blockedFlash;
    private float hurtPulse;

    public static BeetleRig Attach(Transform parent, BasicEnemy owner, float height)
    {
        GameObject obj = new GameObject("BeetleRig");
        obj.transform.SetParent(parent, false);
        BeetleRig rig = obj.AddComponent<BeetleRig>();
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
            sprite.sprite = Resources.Load<Sprite>("Art/Beetle/" + def.Id);
            sprite.sortingLayerName = sortingLayer;
            sprite.sortingOrder = sortingBase + def.Order;
            if (sprite.sprite == null)
                Debug.LogWarning("甲虫缺少部件贴图：Art/Beetle/" + def.Id);
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
            float wanted = enemy.FacingDir < 0 ? 1f : -1f;   // 原图朝左
            facing = Mathf.Approximately(facing, wanted) ? wanted : Mathf.MoveTowards(facing, wanted, dt * 12f);
            ApplyScale();

            if (enemy.IsBlocked)
                blockedFlash = 0.22f;
            else
                blockedFlash = Mathf.Max(0f, blockedFlash - dt);

            if (enemy.IsHurt)
                hurtPulse = Mathf.Min(1f, hurtPulse + dt * 8f);
            else
                hurtPulse = Mathf.Max(0f, hurtPulse - dt * 5f);
        }

        float t = Time.time;
        float walk = Mathf.Sin(t * 6f);
        float idle = 1f - Mathf.Max(blockedFlash * 3f, hurtPulse);

        // 身体：爬动时轻微上下 + 被弹开时后退
        Transform body = Find("body");
        if (body != null)
        {
            float bob = Mathf.Abs(walk) * 0.02f * idle;
            body.localPosition = new Vector3(0f, bob + hurtPulse * -0.01f, 0f);
            SetAngle("body", -blockedFlash * 24f + Mathf.Sin(t * 2f) * 1f * idle);
        }

        // 甲壳：呼吸起伏；被普通攻击弹开时整体一亮一缩
        SetAngle("shell", 1.2f * Mathf.Sin(t * 1.6f) * idle - 3.5f * hurtPulse + blockedFlash * 30f);

        // 角/头：慢慢晃，挨打时抬一下
        SetAngle("horn", 1.6f * Mathf.Sin(t * 1.3f + 0.5f) * idle + 4f * hurtPulse);

        // 六足交替（奇偶反相），被弹开时一起往后缩
        SetAngle("leg_far_front", walk * 7f + blockedFlash * 18f);
        SetAngle("leg_mid_a", -walk * 7f + blockedFlash * 10f);
        SetAngle("leg_mid_b", walk * 7f + blockedFlash * 10f);
        SetAngle("leg_back_a", -walk * 7f + blockedFlash * 8f);
        SetAngle("leg_back_b", walk * 7f + blockedFlash * 8f);

        // 被弹开：整只壳闪白
        Color tint = Color.white;
        if (blockedFlash > 0f)
            tint = Color.Lerp(Color.white, new Color(1f, 0.95f, 0.7f), Mathf.Clamp01(blockedFlash / 0.22f));
        else if (hurtPulse > 0f)
            tint = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.55f), hurtPulse);

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
