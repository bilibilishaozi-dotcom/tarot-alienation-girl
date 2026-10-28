using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 蓝色魔女的演示骨骼。
// 每个部件是一个 Transform，轴心用的是 parts.json 里的关节。
// 子物体挂在父关节上，所以转父物体时子部件会跟着转。
// 没有用 Unity 2D Animation 包：这个工程没装它，而且骨骼可以全部用文件搭好。
public class BlueWitchDemo : MonoBehaviour
{
    const float PixelsPerUnit = 100f;
    // candidate.png 高 869。髋部根点在 y=605（从上往下），脚底大约再往下 248 像素。
    const float FeetBelowRoot = 248f / PixelsPerUnit;
    const float WalkSpeed = 1.15f;

    // 幅度故意很小。近侧大腿被外套挡住，肩膀和后片是补出来的，转大了会露缝。
    const float IdleHair = 1.5f;
    const float IdleCoat = 1.1f;
    const float IdleHead = 0.6f;
    const float WalkThigh = 7f;
    const float WalkShin = 9f;
    const float WalkArm = 6f;
    const float WalkForearm = 4f;

    [Serializable]
    public class PartSlot
    {
        public string id;
        public Sprite sprite;
    }

    public PartSlot[] parts;

    readonly struct Bone
    {
        public readonly string Id;
        public readonly string Parent;
        public readonly int Order;
        public readonly Vector2 LocalPosition;

        public Bone(string id, string parent, int order, float x, float y)
        {
            Id = id;
            Parent = parent;
            Order = order;
            LocalPosition = new Vector2(x, y);
        }
    }

    // 局部坐标来自 parts.json 的 pivot_canvas，换算成 Unity 的 x 向右、y 向上，再除以 100。
    static readonly Bone[] Skeleton =
    {
        new Bone("hair_back", "head", 0, -0.3500f, 0.8200f),
        new Bone("coat_back", "torso", 1, 0.5200f, 1.8800f),
        new Bone("arm_far_upper", "torso", 2, 0.6000f, 1.9600f),
        new Bone("arm_far_lower", "arm_far_upper", 3, 0.2000f, -0.9000f),
        new Bone("leg_far_shin", "leg_far_thigh", 4, -0.0400f, -1.2500f),
        new Bone("leg_far_thigh", "root", 5, 0.1200f, 0.2500f),
        new Bone("leg_near_shin", "leg_near_thigh", 6, -0.2000f, -1.2500f),
        new Bone("leg_near_thigh", "root", 7, -0.4600f, 0.2500f),
        new Bone("shorts", "torso", 8, -0.0200f, 0.0000f),
        new Bone("torso", "root", 9, 0.0000f, 0.5700f),
        new Bone("coat_front", "torso", 10, -0.5600f, 1.4800f),
        new Bone("head", "torso", 11, 0.0700f, 2.3600f),
        new Bone("hair_front", "head", 12, -0.2500f, 1.0200f),
        new Bone("hair_front_r", "head", 13, 0.4500f, 0.7200f),
        new Bone("hat", "head", 14, -0.0500f, 1.2200f),
        new Bone("arm_near_upper", "torso", 15, -0.6600f, 1.8600f),
        new Bone("arm_near_lower", "arm_near_upper", 16, -0.3000f, -0.8800f)
    };

    Transform rig;
    Transform[] bones;
    Transform ground;
    float walkPhase;

    void Awake()
    {
        Time.timeScale = 1f;
        BuildRig();
        transform.position = new Vector3(0f, FeetBelowRoot, 0f);
        BuildGround();
        BuildHint();
    }

    void Update()
    {
        float direction = ReadDirection();
        bool walking = Mathf.Abs(direction) > 0.01f;
        if (walking)
        {
            transform.position += Vector3.right * direction * WalkSpeed * Time.deltaTime;
            Vector3 scale = rig.localScale;
            scale.x = direction > 0f ? 1f : -1f;
            rig.localScale = scale;
            walkPhase += Time.deltaTime * 5.5f;
            ApplyWalk(Mathf.Sin(walkPhase), Mathf.Cos(walkPhase));
        }
        else
        {
            walkPhase = 0f;
            ApplyIdle(Time.time);
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            GameFlow.LoadTitle();
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 position = cam.transform.position;
        position.x = transform.position.x;
        cam.transform.position = position;
        if (ground != null)
        {
            Vector3 floor = ground.position;
            floor.x = transform.position.x;
            ground.position = floor;
        }
    }

    void BuildRig()
    {
        rig = new GameObject("Rig").transform;
        rig.SetParent(transform, false);
        bones = new Transform[Skeleton.Length];

        for (int i = 0; i < Skeleton.Length; i++)
        {
            Bone bone = Skeleton[i];
            GameObject part = new GameObject(bone.Id);
            bones[i] = part.transform;
            SpriteRenderer sprite = part.AddComponent<SpriteRenderer>();
            sprite.sprite = FindSprite(bone.Id);
            sprite.sortingOrder = bone.Order;
            if (sprite.sprite == null)
                Debug.LogWarning("蓝色魔女缺少部件：" + bone.Id);
        }

        for (int i = 0; i < Skeleton.Length; i++)
        {
            Bone bone = Skeleton[i];
            Transform parent = bone.Parent == "root" ? rig : FindBone(bone.Parent);
            bones[i].SetParent(parent, false);
            bones[i].localPosition = bone.LocalPosition;
            bones[i].localRotation = Quaternion.identity;
        }
    }

    void ApplyIdle(float time)
    {
        SetAngle("torso", Mathf.Sin(time * 1.3f) * 0.35f);
        SetAngle("head", Mathf.Sin(time * 1.2f) * IdleHead);
        SetAngle("hat", Mathf.Sin(time * 1.1f) * 0.45f);
        SetAngle("hair_back", Mathf.Sin(time * 1.15f) * IdleHair);
        SetAngle("hair_front", Mathf.Sin(time * 1.15f + 0.4f) * 1.1f);
        SetAngle("hair_front_r", Mathf.Sin(time * 1.4f) * 1.8f);
        SetAngle("coat_front", Mathf.Sin(time * 0.95f) * IdleCoat);
        SetAngle("coat_back", Mathf.Sin(time * 0.95f + 0.8f) * 0.8f);
        SetAngle("arm_near_upper", Mathf.Sin(time * 0.8f) * 0.8f);
        SetAngle("arm_far_upper", Mathf.Sin(time * 0.8f + 0.5f) * 0.6f);
        SetAngle("arm_near_lower", 1.5f);
        SetAngle("arm_far_lower", 1.2f);
        SetAngle("leg_near_thigh", 0f);
        SetAngle("leg_far_thigh", 0f);
        SetAngle("leg_near_shin", 0f);
        SetAngle("leg_far_shin", 0f);
        Bob("head", Mathf.Sin(time * 1.2f) * 0.012f);
        Bob("torso", 0f);
    }

    void ApplyWalk(float swing, float step)
    {
        float time = Time.time;
        SetAngle("torso", swing * 0.8f);
        SetAngle("head", -swing * 0.5f);
        SetAngle("hat", Mathf.Sin(time * 2f) * 0.4f);
        SetAngle("hair_back", swing * 1.2f + Mathf.Sin(time * 2f) * 0.6f);
        SetAngle("hair_front", swing * 0.8f);
        SetAngle("hair_front_r", swing * 1.4f);
        SetAngle("coat_front", swing * 1.3f);
        SetAngle("coat_back", -swing * 0.9f);

        SetAngle("leg_near_thigh", swing * WalkThigh);
        SetAngle("leg_far_thigh", -swing * WalkThigh);
        SetAngle("leg_near_shin", Mathf.Max(0f, -swing) * WalkShin);
        SetAngle("leg_far_shin", Mathf.Max(0f, swing) * WalkShin);

        SetAngle("arm_near_upper", -swing * WalkArm);
        SetAngle("arm_far_upper", swing * WalkArm);
        SetAngle("arm_near_lower", 2f + Mathf.Max(0f, -swing) * WalkForearm);
        SetAngle("arm_far_lower", 2f + Mathf.Max(0f, swing) * WalkForearm);
        Bob("torso", Mathf.Abs(step) * 0.015f);
        Bob("head", 0f);
    }

    void SetAngle(string id, float degrees)
    {
        Transform bone = FindBone(id);
        if (bone == null)
            return;

        bone.localRotation = Quaternion.Euler(0f, 0f, degrees);
    }

    void Bob(string id, float extraY)
    {
        Transform bone = FindBone(id);
        if (bone == null)
            return;

        Vector2 rest = RestPosition(id);
        Vector3 position = bone.localPosition;
        position.x = rest.x;
        position.y = rest.y + extraY;
        bone.localPosition = position;
    }

    Vector2 RestPosition(string id)
    {
        for (int i = 0; i < Skeleton.Length; i++)
        {
            if (Skeleton[i].Id == id)
                return Skeleton[i].LocalPosition;
        }

        return Vector2.zero;
    }

    Transform FindBone(string id)
    {
        for (int i = 0; i < Skeleton.Length; i++)
        {
            if (Skeleton[i].Id == id)
                return bones[i];
        }

        return null;
    }

    Sprite FindSprite(string id)
    {
        if (parts == null)
            return null;

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] != null && parts[i].id == id)
                return parts[i].sprite;
        }

        return null;
    }

    static float ReadDirection()
    {
        if (Keyboard.current == null)
            return 0f;

        float direction = 0f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            direction -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            direction += 1f;
        return Mathf.Clamp(direction, -1f, 1f);
    }

    void BuildGround()
    {
        GameObject groundObject = new GameObject("Ground");
        ground = groundObject.transform;
        ground.position = new Vector3(0f, -0.04f, 0f);
        ground.localScale = new Vector3(30f, 0.08f, 1f);
        SpriteRenderer sprite = groundObject.AddComponent<SpriteRenderer>();
        sprite.sprite = RuntimeSprite.Pixel;
        sprite.color = new Color(0.16f, 0.2f, 0.18f, 1f);
        sprite.sortingOrder = -10;
    }

    void BuildHint()
    {
        GameObject canvasObject = new GameObject("Hint");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        Text label = new GameObject("Label").AddComponent<Text>();
        label.transform.SetParent(canvasObject.transform, false);
        label.font = GameFont.Get();
        label.fontSize = 28;
        label.alignment = TextAnchor.UpperLeft;
        label.color = new Color(1f, 0.94f, 0.82f, 1f);
        label.text = "A / D 或方向键走路，松开待机。Esc 回标题。";
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -20f);
        rect.sizeDelta = new Vector2(900f, 48f);
    }
}
