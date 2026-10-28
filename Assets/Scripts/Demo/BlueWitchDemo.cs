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

    // 下面的角度是 1 倍时的基础幅度。近侧大腿被外套挡住，肩膀和后片是补出来的，转大了会露缝。
    const float IdleHair = 1.5f;
    const float IdleCoat = 1.1f;
    const float IdleHead = 0.6f;
    const float WalkThigh = 7f;
    const float WalkShin = 9f;
    const float WalkArm = 6f;
    const float WalkForearm = 4f;

    [Header("幅度")]
    [SerializeField] float idleAmplitude = 3f; // 待机：摆动和点头乘这个数，速度快慢不变
    [SerializeField] float walkAmplitude = 2f; // 走路：摆动和身体起伏乘这个数；前臂固定弯曲和步频不变
    [SerializeField] float jumpAmplitude = 1f; // 起跳下蹲和上升姿势乘这个数
    [SerializeField] float fallAmplitude = 1f; // 下落和落地缓冲乘这个数
    [SerializeField] float wallAmplitude = 1f; // 贴墙下滑乘这个数。姿态按面向墙的一侧来写，左右翻转后会镜像

    const float Gravity = -26f;
    const float JumpVelocity = 8.4f;
    const float AnticipateTime = 0.1f;
    const float LandTime = 0.16f;
    const float WallFaceX = 4.4f;
    const float WallDrop = -1.4f;
    const float BlendSharpness = 12f;

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
    float verticalVelocity;
    float anticipateTimer;
    float landTimer;
    bool grounded = true;
    bool onWall;
    Pose shown;

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
        float dt = Time.deltaTime;
        float direction = ReadDirection();
        bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        StepMotion(direction, jumpPressed, dt);
        BlendAndApply(ChoosePose(direction), dt);

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            GameFlow.LoadTitle();
    }

    void StepMotion(float direction, bool jumpPressed, float dt)
    {
        if (jumpPressed && grounded && anticipateTimer <= 0f && landTimer <= 0f)
            anticipateTimer = AnticipateTime;

        if (anticipateTimer > 0f)
        {
            anticipateTimer -= dt;
            verticalVelocity = 0f;
            grounded = true;
            onWall = false;
            HoldHeight(GroundY);
            if (anticipateTimer <= 0f)
            {
                verticalVelocity = JumpVelocity;
                grounded = false;
            }

            return;
        }

        if (!grounded)
        {
            bool pressingAway = direction < -0.01f;
            bool hitWall = transform.position.x >= WallFaceX && !pressingAway;
            Vector3 position = transform.position;
            if (hitWall)
            {
                position.x = WallFaceX;
                Face(1f);
                verticalVelocity += Gravity * dt;
                if (verticalVelocity < WallDrop)
                    verticalVelocity = WallDrop;
                position.y += verticalVelocity * dt;
                onWall = verticalVelocity <= 0.2f;
            }
            else
            {
                onWall = false;
                verticalVelocity += Gravity * dt;
                position.y += verticalVelocity * dt;
                if (Mathf.Abs(direction) > 0.01f)
                {
                    position.x += direction * WalkSpeed * dt;
                    if (position.x > WallFaceX)
                        position.x = WallFaceX;
                    Face(direction);
                }
            }

            if (position.y <= GroundY && verticalVelocity <= 0f)
            {
                position.y = GroundY;
                verticalVelocity = 0f;
                grounded = true;
                onWall = false;
                landTimer = LandTime;
            }

            transform.position = position;
            return;
        }

        if (landTimer > 0f)
            landTimer -= dt;

        onWall = false;
        verticalVelocity = 0f;
        HoldHeight(GroundY);
        if (Mathf.Abs(direction) > 0.01f)
        {
            Vector3 position = transform.position;
            position.x += direction * WalkSpeed * dt;
            if (position.x > WallFaceX)
                position.x = WallFaceX;
            transform.position = position;
            Face(direction);
            walkPhase += dt * 5.5f;
        }
    }

    float GroundY => FeetBelowRoot;

    void HoldHeight(float y)
    {
        Vector3 position = transform.position;
        position.y = y;
        transform.position = position;
    }

    void Face(float direction)
    {
        if (rig == null || Mathf.Abs(direction) < 0.01f)
            return;

        Vector3 scale = rig.localScale;
        scale.x = direction > 0f ? 1f : -1f;
        rig.localScale = scale;
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

    Pose ChoosePose(float direction)
    {
        if (anticipateTimer > 0f)
            return Scale(CrouchPose(), jumpAmplitude);
        if (landTimer > 0f && grounded)
            return Scale(LandingPose(), fallAmplitude);
        if (!grounded && onWall)
            return Scale(WallPose(), wallAmplitude);
        if (!grounded && verticalVelocity > 0f)
            return Scale(RisePose(), jumpAmplitude);
        if (!grounded)
            return Scale(FallPose(), fallAmplitude);
        if (Mathf.Abs(direction) > 0.01f)
            return WalkPose(Mathf.Sin(walkPhase), Mathf.Cos(walkPhase));
        return IdlePose(Time.time);
    }

    void BlendAndApply(Pose target, float dt)
    {
        float blend = 1f - Mathf.Exp(-BlendSharpness * dt);
        shown = Pose.Lerp(shown, target, blend);
        SetAngle("torso", shown.torso);
        SetAngle("head", shown.head);
        SetAngle("hat", shown.hat);
        SetAngle("hair_back", shown.hairBack);
        SetAngle("hair_front", shown.hairFront);
        SetAngle("hair_front_r", shown.hairFrontR);
        SetAngle("coat_front", shown.coatFront);
        SetAngle("coat_back", shown.coatBack);
        SetAngle("leg_near_thigh", shown.nearThigh);
        SetAngle("leg_far_thigh", shown.farThigh);
        SetAngle("leg_near_shin", shown.nearShin);
        SetAngle("leg_far_shin", shown.farShin);
        SetAngle("arm_near_upper", shown.nearUpper);
        SetAngle("arm_far_upper", shown.farUpper);
        SetAngle("arm_near_lower", shown.nearLower);
        SetAngle("arm_far_lower", shown.farLower);
        Bob("head", shown.headBob);
        Bob("torso", shown.torsoBob);
    }

    // 正角度是逆时针。面向右时，近侧大腿正角度会把腿留在外套后面；近侧上臂不要超过大约 60 度。
    static Pose Scale(Pose pose, float amplitude)
    {
        pose.torso *= amplitude;
        pose.head *= amplitude;
        pose.hat *= amplitude;
        pose.hairBack *= amplitude;
        pose.hairFront *= amplitude;
        pose.hairFrontR *= amplitude;
        pose.coatFront *= amplitude;
        pose.coatBack *= amplitude;
        pose.nearThigh *= amplitude;
        pose.farThigh *= amplitude;
        pose.nearShin *= amplitude;
        pose.farShin *= amplitude;
        pose.nearUpper *= amplitude;
        pose.farUpper *= amplitude;
        pose.nearLower *= amplitude;
        pose.farLower *= amplitude;
        pose.torsoBob *= amplitude;
        pose.headBob *= amplitude;
        return pose;
    }

    Pose IdlePose(float time)
    {
        float idle = idleAmplitude;
        Pose pose = new Pose();
        pose.torso = Mathf.Sin(time * 1.3f) * 0.35f * idle;
        pose.head = Mathf.Sin(time * 1.2f) * IdleHead * idle;
        pose.hat = Mathf.Sin(time * 1.1f) * 0.45f * idle;
        pose.hairBack = Mathf.Sin(time * 1.15f) * IdleHair * idle;
        pose.hairFront = Mathf.Sin(time * 1.15f + 0.4f) * 1.1f * idle;
        pose.hairFrontR = Mathf.Sin(time * 1.4f) * 1.8f * idle;
        pose.coatFront = Mathf.Sin(time * 0.95f) * IdleCoat * idle;
        pose.coatBack = Mathf.Sin(time * 0.95f + 0.8f) * 0.8f * idle;
        pose.nearUpper = Mathf.Sin(time * 0.8f) * 0.8f * idle;
        pose.farUpper = Mathf.Sin(time * 0.8f + 0.5f) * 0.6f * idle;
        pose.nearLower = 1.5f;
        pose.farLower = 1.2f;
        pose.headBob = Mathf.Sin(time * 1.2f) * 0.012f * idle;
        return pose;
    }

    Pose WalkPose(float swing, float step)
    {
        float time = Time.time;
        float walk = walkAmplitude;
        Pose pose = new Pose();
        pose.torso = swing * 0.8f * walk;
        pose.head = -swing * 0.5f * walk;
        pose.hat = Mathf.Sin(time * 2f) * 0.4f * walk;
        pose.hairBack = (swing * 1.2f + Mathf.Sin(time * 2f) * 0.6f) * walk;
        pose.hairFront = swing * 0.8f * walk;
        pose.hairFrontR = swing * 1.4f * walk;
        pose.coatFront = swing * 1.3f * walk;
        pose.coatBack = -swing * 0.9f * walk;
        pose.nearThigh = swing * WalkThigh * walk;
        pose.farThigh = -swing * WalkThigh * walk;
        pose.nearShin = Mathf.Max(0f, -swing) * WalkShin * walk;
        pose.farShin = Mathf.Max(0f, swing) * WalkShin * walk;
        pose.nearUpper = -swing * WalkArm * walk;
        pose.farUpper = swing * WalkArm * walk;
        pose.nearLower = 2f + Mathf.Max(0f, -swing) * WalkForearm * walk;
        pose.farLower = 2f + Mathf.Max(0f, swing) * WalkForearm * walk;
        pose.torsoBob = Mathf.Abs(step) * 0.015f * walk;
        return pose;
    }

    // 起跳前的短下蹲。近侧腿往后收，让外套继续盖住髋部。
    static Pose CrouchPose()
    {
        return new Pose
        {
            torso = 10f,
            head = 8f,
            hat = 3f,
            hairBack = 5f,
            hairFront = 3f,
            hairFrontR = 4f,
            coatFront = 7f,
            coatBack = 4f,
            nearThigh = 18f,
            farThigh = -20f,
            nearShin = -26f,
            farShin = 28f,
            nearUpper = 22f,
            farUpper = 16f,
            nearLower = 6f,
            farLower = 4f,
            torsoBob = -0.05f,
            headBob = -0.008f
        };
    }

    // 上升：膝弯收起，手臂向后抬，头发和外套还垂着。
    static Pose RisePose()
    {
        return new Pose
        {
            torso = -8f,
            head = -14f,
            hat = -6f,
            hairBack = 12f,
            hairFront = 7f,
            hairFrontR = 9f,
            coatFront = 11f,
            coatBack = 8f,
            nearThigh = 22f,
            farThigh = -34f,
            nearShin = -30f,
            farShin = 38f,
            nearUpper = 38f,
            farUpper = 46f,
            nearLower = -16f,
            farLower = -12f,
            torsoBob = 0.012f,
            headBob = 0.008f
        };
    }

    // 下落：腿向下伸，手臂张开保持平衡，头发和外套向上扬。
    static Pose FallPose()
    {
        return new Pose
        {
            torso = 6f,
            head = 10f,
            hat = 5f,
            hairBack = 30f,
            hairFront = 16f,
            hairFrontR = 20f,
            coatFront = 22f,
            coatBack = 16f,
            nearThigh = 12f,
            farThigh = -8f,
            nearShin = -8f,
            farShin = 6f,
            nearUpper = 50f,
            farUpper = -42f,
            nearLower = -8f,
            farLower = 14f,
            headBob = 0.01f
        };
    }

    // 落地的短缓冲：膝弯，上身下沉。
    static Pose LandingPose()
    {
        return new Pose
        {
            torso = 14f,
            head = 12f,
            hat = 4f,
            hairBack = 8f,
            hairFront = 4f,
            hairFrontR = 6f,
            coatFront = 9f,
            coatBack = 5f,
            nearThigh = 16f,
            farThigh = -22f,
            nearShin = -32f,
            farShin = 36f,
            nearUpper = 14f,
            farUpper = 10f,
            nearLower = 4f,
            farLower = 2f,
            torsoBob = -0.07f,
            headBob = -0.015f
        };
    }

    // 贴墙：身体靠向墙，近侧手伸向墙，腿弯着。角度写在面向墙的局部空间里。
    static Pose WallPose()
    {
        return new Pose
        {
            torso = -16f,
            head = -6f,
            hat = -2f,
            hairBack = 18f,
            hairFront = 9f,
            hairFrontR = 12f,
            coatFront = 12f,
            coatBack = 10f,
            nearThigh = 14f,
            farThigh = -26f,
            nearShin = -22f,
            farShin = 40f,
            nearUpper = -50f,
            farUpper = -18f,
            nearLower = -28f,
            farLower = 6f,
            torsoBob = -0.01f
        };
    }

    struct Pose
    {
        public float torso;
        public float head;
        public float hat;
        public float hairBack;
        public float hairFront;
        public float hairFrontR;
        public float coatFront;
        public float coatBack;
        public float nearThigh;
        public float farThigh;
        public float nearShin;
        public float farShin;
        public float nearUpper;
        public float farUpper;
        public float nearLower;
        public float farLower;
        public float torsoBob;
        public float headBob;

        public static Pose Lerp(Pose from, Pose to, float t)
        {
            Pose pose = new Pose();
            pose.torso = Mathf.Lerp(from.torso, to.torso, t);
            pose.head = Mathf.Lerp(from.head, to.head, t);
            pose.hat = Mathf.Lerp(from.hat, to.hat, t);
            pose.hairBack = Mathf.Lerp(from.hairBack, to.hairBack, t);
            pose.hairFront = Mathf.Lerp(from.hairFront, to.hairFront, t);
            pose.hairFrontR = Mathf.Lerp(from.hairFrontR, to.hairFrontR, t);
            pose.coatFront = Mathf.Lerp(from.coatFront, to.coatFront, t);
            pose.coatBack = Mathf.Lerp(from.coatBack, to.coatBack, t);
            pose.nearThigh = Mathf.Lerp(from.nearThigh, to.nearThigh, t);
            pose.farThigh = Mathf.Lerp(from.farThigh, to.farThigh, t);
            pose.nearShin = Mathf.Lerp(from.nearShin, to.nearShin, t);
            pose.farShin = Mathf.Lerp(from.farShin, to.farShin, t);
            pose.nearUpper = Mathf.Lerp(from.nearUpper, to.nearUpper, t);
            pose.farUpper = Mathf.Lerp(from.farUpper, to.farUpper, t);
            pose.nearLower = Mathf.Lerp(from.nearLower, to.nearLower, t);
            pose.farLower = Mathf.Lerp(from.farLower, to.farLower, t);
            pose.torsoBob = Mathf.Lerp(from.torsoBob, to.torsoBob, t);
            pose.headBob = Mathf.Lerp(from.headBob, to.headBob, t);
            return pose;
        }
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

        GameObject wall = new GameObject("Wall");
        wall.transform.position = new Vector3(WallFaceX + 0.28f, 3.2f, 0f);
        wall.transform.localScale = new Vector3(0.55f, 6.6f, 1f);
        SpriteRenderer wallSprite = wall.AddComponent<SpriteRenderer>();
        wallSprite.sprite = RuntimeSprite.Pixel;
        wallSprite.color = new Color(0.23f, 0.28f, 0.34f, 1f);
        wallSprite.sortingOrder = -5;
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
        label.text = "A / D 走路，空格跳。跳起来靠上右边的墙会贴墙滑下。Esc 回标题。";
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -20f);
        rect.sizeDelta = new Vector2(900f, 48f);
    }
}
