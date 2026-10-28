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
    [SerializeField] float jumpAmplitude = 1f; // 下蹲、蹬地、上升的关节角度乘这个数
    [SerializeField] float fallAmplitude = 1f; // 下落和落地的关节角度乘这个数
    [SerializeField] float wallAmplitude = 1f; // 贴墙下滑乘这个数。姿态按面向墙的一侧来写，左右翻转后会镜像

    [Header("二次运动")]
    [Tooltip("头发、帽子、衣摆的弹簧刚度和起跳、落地冲量。0 时这几块与四肢一样做指数平滑。")]
    [SerializeField] float springStrength = 1f;
    [Tooltip("根节点压扁拉伸，以及跳跃时头发和前摆的派生缩放。鞋底贴地不乘这个数。")]
    [SerializeField] float squashStrength = 1f;

    const float Gravity = -26f;
    const float JumpVelocity = 8.4f;
    const float CrouchTime = 0.12f;
    const float LaunchTime = 0.07f;
    const float LandTime = 0.22f;
    const float ImpactHold = 0.04f;
    const float BoneSharpness = 30f;
    const float ImpactSharpness = 40f;
    const float ImpactSharpTime = 0.04f;
    const float AirBlendIn = 0.12f;
    const float SpringSettleTime = 0.7f;
    const float JumpBufferTime = 0.1f;
    const float AnticipationMove = 0.5f;
    const float ClampBlendTime = 0.1f;
    const float MaxDeltaTime = 1f / 30f;
    const float WallFaceX = 4.4f; // 髋部根点碰到墙的位置，不是身体外缘
    const float WallDrop = -1.4f;

    // 弹簧顺序：hair_back, hair_front, hair_front_r, hat, coat_front, coat_back。
    static readonly float[] SpringOmega = { 11f, 16f, 13f, 18f, 12f, 14f };
    static readonly float[] SpringZeta = { 0.35f, 0.45f, 0.38f, 0.5f, 0.4f, 0.45f };
    static readonly float[] SpringTakeoff = { 70f, 40f, 60f, 60f, 60f, -30f };
    static readonly float[] SpringLand = { 120f, 60f, 90f, 50f, 90f, -40f };
    static readonly float[] SpringMin = { -8f, -12f, -15f, -8f, -5f, -8f };
    static readonly float[] SpringMax = { 8f, 10f, 15f, 8f, 6f, 3f };
    const int SpringCount = 6;

    enum Phase
    {
        Ground,
        Crouch,
        Launch,
        Air,
        Land
    }

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
    float phaseTime;
    float airTime;
    float faceSign = 1f;
    float rigOffset;
    float springHold;
    float jumpBuffer;
    float impactSharpTimer;
    float clampWeight;
    float[] springX;
    float[] springV;
    Phase phase = Phase.Ground;
    bool onWall;
    Pose shown;

    void Awake()
    {
        Time.timeScale = 1f;
        shown = IdleRest();
        springX = new float[SpringCount];
        springV = new float[SpringCount];
        BuildRig();
        transform.position = new Vector3(0f, FeetBelowRoot, 0f);
        BuildGround();
        BuildHint();
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, MaxDeltaTime);
        float direction = ReadDirection();
        bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (jumpPressed)
            jumpBuffer = JumpBufferTime;
        bool canJump = phase == Phase.Ground || (phase == Phase.Land && phaseTime >= ImpactHold);
        if (jumpBuffer > 0f && canJump)
        {
            phase = Phase.Crouch;
            phaseTime = 0f;
            jumpBuffer = 0f;
        }

        if (phase == Phase.Crouch && phaseTime >= CrouchTime)
        {
            phase = Phase.Launch;
            phaseTime -= CrouchTime;
        }

        if (phase == Phase.Launch && phaseTime >= LaunchTime)
        {
            phase = Phase.Air;
            phaseTime = 0f;
            airTime = 0f;
            verticalVelocity = JumpVelocity;
            Kick(true);
        }

        if (phase == Phase.Land && phaseTime >= LandTime)
            phase = Phase.Ground;

        StepMotion(direction, dt);
        BlendAndApply(ChoosePose(direction), direction, dt);

        if (phase == Phase.Crouch || phase == Phase.Launch || phase == Phase.Land)
            phaseTime += dt;
        if (phase == Phase.Ground)
            springHold = Mathf.Max(0f, springHold - dt);
        if (jumpBuffer > 0f)
            jumpBuffer -= dt;
        if (impactSharpTimer > 0f)
            impactSharpTimer -= dt;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            GameFlow.LoadTitle();
    }

    void StepMotion(float direction, float dt)
    {
        if (phase == Phase.Crouch || phase == Phase.Launch)
        {
            verticalVelocity = 0f;
            onWall = false;
            HoldHeight(GroundY);
            if (Mathf.Abs(direction) > 0.01f)
            {
                Vector3 position = transform.position;
                position.x += direction * WalkSpeed * AnticipationMove * dt;
                if (position.x > WallFaceX)
                    position.x = WallFaceX;
                transform.position = position;
                Face(direction);
            }

            return;
        }

        if (phase == Phase.Air)
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
                float gravMul = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(0f, 2f, Mathf.Abs(verticalVelocity)));
                verticalVelocity += Gravity * gravMul * dt;
                position.y += verticalVelocity * dt;
                if (Mathf.Abs(direction) > 0.01f)
                {
                    position.x += direction * WalkSpeed * dt;
                    if (position.x > WallFaceX)
                        position.x = WallFaceX;
                    Face(direction);
                }
            }

            airTime += dt;
            if (position.y <= GroundY && verticalVelocity <= 0f)
            {
                position.y = GroundY;
                verticalVelocity = 0f;
                onWall = false;
                phase = Phase.Land;
                phaseTime = 0f;
                springHold = SpringSettleTime;
                impactSharpTimer = ImpactSharpTime;
                Kick(false);
            }

            transform.position = position;
            return;
        }

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
        if (Mathf.Abs(direction) < 0.01f)
            return;

        faceSign = direction > 0f ? 1f : -1f;
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
        if (phase == Phase.Crouch)
        {
            float t = EaseOutCubic(phaseTime / CrouchTime);
            return Pose.Lerp(IdleRest(), Scale(CrouchPose(), jumpAmplitude), t);
        }

        if (phase == Phase.Launch)
        {
            float t = EaseInQuad(phaseTime / LaunchTime);
            return Pose.Lerp(Scale(CrouchPose(), jumpAmplitude), Scale(LaunchPose(), jumpAmplitude), t);
        }

        if (phase == Phase.Air && onWall)
            return Scale(WallPose(), wallAmplitude);

        if (phase == Phase.Air)
        {
            float u = Mathf.Clamp(verticalVelocity / JumpVelocity, -1f, 1f);
            float riseWeight = SmoothRange(0.12f, 0.65f, u);
            float fallWeight = SmoothRange(0.12f, 0.85f, -u);
            Pose air = u >= 0f
                ? Pose.Lerp(ApexPose(), Scale(RisePose(), jumpAmplitude), riseWeight)
                : Pose.Lerp(ApexPose(), Scale(FallPose(), fallAmplitude), fallWeight);
            float intoAir = SmoothRange(0f, 1f, airTime / AirBlendIn);
            return Pose.Lerp(Scale(LaunchPose(), jumpAmplitude), air, intoAir);
        }

        if (phase == Phase.Land)
        {
            Pose land = Scale(LandPose(), fallAmplitude);
            if (phaseTime < ImpactHold)
                return land;

            float t = EaseOutCubic((phaseTime - ImpactHold) / (LandTime - ImpactHold));
            Pose recover = Mathf.Abs(direction) > 0.01f
                ? WalkPose(Mathf.Sin(walkPhase), Mathf.Cos(walkPhase))
                : IdleRest();
            return Pose.Lerp(land, recover, t);
        }

        if (Mathf.Abs(direction) > 0.01f)
            return WalkPose(Mathf.Sin(walkPhase), Mathf.Cos(walkPhase));
        return IdlePose(Time.time);
    }

    void BlendAndApply(Pose target, float direction, float dt)
    {
        float squash = Mathf.Max(0f, squashStrength);
        target.sx = Mathf.Lerp(1f, target.sx, squash);
        target.sy = Mathf.Lerp(1f, target.sy, squash);

        bool onWallPose = phase == Phase.Air && onWall;
        bool jumpPose = phase == Phase.Crouch || phase == Phase.Launch || phase == Phase.Land
            || (phase == Phase.Air && !onWall);
        bool recoveringWalk = phase == Phase.Land && phaseTime >= ImpactHold && Mathf.Abs(direction) > 0.01f;
        float clampGoal = jumpPose ? 1f : 0f;
        clampWeight = Mathf.MoveTowards(clampWeight, clampGoal, dt / ClampBlendTime);
        if (!onWallPose)
        {
            Pose limited = target;
            ClampCloth(ref limited);
            if (jumpPose && !recoveringWalk)
                ClampBody(ref limited);
            target = Pose.Lerp(target, limited, clampWeight);
        }

        float sharp = impactSharpTimer > 0f ? ImpactSharpness : BoneSharpness;
        float blend = 1f - Mathf.Exp(-sharp * dt);
        shown = Pose.Lerp(shown, target, blend);
        float response = Mathf.Max(0f, springStrength);
        // 落地后的摆动大约再持续 0.7 秒。平时的待机和走路用指数平滑，走路步频会把 hair_back 的弹簧放大。
        bool useSpring = !onWallPose && response > 0f && (phase != Phase.Ground || springHold > 0f);
        for (int i = 0; i < SpringCount; i++)
        {
            if (!useSpring)
            {
                springX[i] = SpringAngle(shown, i);
                springV[i] = 0f;
                continue;
            }

            float omega = SpringOmega[i] * response;
            float velocity = springV[i];
            float angle = springX[i];
            float goal = SpringAngle(target, i);
            velocity += (omega * omega * (goal - angle) - 2f * SpringZeta[i] * omega * velocity) * dt;
            angle += velocity * dt;
            float limited = Mathf.Clamp(angle, SpringMin[i], SpringMax[i]);
            bool outward = (angle > SpringMax[i] && velocity > 0f) || (angle < SpringMin[i] && velocity < 0f);
            if (outward)
                velocity = 0f;
            angle = Mathf.Lerp(angle, limited, clampWeight);
            springV[i] = velocity;
            springX[i] = angle;
            shown = WithSpring(shown, i, angle);
        }

        Pose display = shown;

        SetAngle("torso", display.torso);
        SetAngle("head", display.head);
        SetAngle("hat", display.hat);
        SetAngle("hair_back", display.hairBack);
        SetAngle("hair_front", display.hairFront);
        SetAngle("hair_front_r", display.hairFrontR);
        SetAngle("coat_front", display.coatFront);
        SetAngle("coat_back", display.coatBack);
        SetAngle("leg_near_thigh", display.nearThigh);
        SetAngle("leg_far_thigh", display.farThigh);
        SetAngle("leg_near_shin", display.nearShin);
        SetAngle("leg_far_shin", display.farShin);
        SetAngle("arm_near_upper", display.nearUpper);
        SetAngle("arm_far_upper", display.farUpper);
        SetAngle("arm_near_lower", display.nearLower);
        SetAngle("arm_far_lower", display.farLower);
        Bob("head", display.headBob);
        Bob("torso", display.torsoBob);
        ApplyClothScale(display, jumpPose);
        ApplyRoot(display, direction, blend);
    }

    void ApplyRoot(Pose display, float direction, float blend)
    {
        if (rig == null)
            return;

        bool plant = phase == Phase.Crouch || phase == Phase.Launch || phase == Phase.Land
            || (phase == Phase.Ground && Mathf.Abs(direction) <= 0.01f);
        float targetY = plant ? -FeetBelowRoot - display.sy * SoleLow(display) : 0f;
        if (plant)
            rigOffset = targetY;
        else
            rigOffset = Mathf.Lerp(rigOffset, targetY, blend);
        rig.localPosition = new Vector3(0f, rigOffset, 0f);
        rig.localScale = new Vector3(faceSign * display.sx, display.sy, 1f);
    }

    void ApplyClothScale(Pose display, bool jumpPose)
    {
        Transform hair = FindBone("hair_back");
        Transform coat = FindBone("coat_front");
        float squash = Mathf.Max(0f, squashStrength);
        float hairY = 1f;
        float coatX = 1f;
        float coatY = 1f;
        if (jumpPose)
        {
            hairY = Mathf.Clamp(1f + 0.0125f * display.hairBack, 0.90f, 1.04f);
            coatY = Mathf.Clamp(1f + 0.012f * display.coatFront, 0.94f, 1.03f);
            coatX = Mathf.Clamp(1f - 0.012f * display.coatFront, 0.97f, 1.06f);
        }

        hairY = Mathf.Lerp(1f, hairY, squash);
        coatY = Mathf.Lerp(1f, coatY, squash);
        coatX = Mathf.Lerp(1f, coatX, squash);
        if (hair != null)
            hair.localScale = new Vector3(1f, hairY, 1f);
        if (coat != null)
            coat.localScale = new Vector3(coatX, coatY, 1f);
    }

    void Kick(bool takeoff)
    {
        float scale = Mathf.Max(0f, springStrength);
        float[] kick = takeoff ? SpringTakeoff : SpringLand;
        for (int i = 0; i < SpringCount; i++)
            springV[i] += kick[i] * scale;
    }

    static void ClampCloth(ref Pose pose)
    {
        pose.hairBack = Mathf.Clamp(pose.hairBack, -8f, 8f);
        pose.hairFront = Mathf.Clamp(pose.hairFront, -12f, 10f);
        pose.hairFrontR = Mathf.Clamp(pose.hairFrontR, -15f, 15f);
        pose.hat = Mathf.Clamp(pose.hat, -8f, 8f);
        pose.coatFront = Mathf.Clamp(pose.coatFront, -5f, 6f);
        pose.coatBack = Mathf.Clamp(pose.coatBack, -8f, 3f);
    }

    static void ClampBody(ref Pose pose)
    {
        pose.torso = Mathf.Clamp(pose.torso, -12f, 8f);
        pose.head = Mathf.Clamp(pose.head, -8f, 8f);
        pose.nearUpper = Mathf.Clamp(pose.nearUpper, -20f, 45f);
        pose.farUpper = Mathf.Clamp(pose.farUpper, -8f, 25f);
        pose.nearLower = Mathf.Clamp(pose.nearLower, 0f, 40f);
        pose.farLower = Mathf.Clamp(pose.farLower, 0f, 40f);
        pose.nearThigh = Mathf.Clamp(pose.nearThigh, -15f, 36f);
        pose.nearShin = Mathf.Clamp(pose.nearShin, -55f, 2f);
        pose.farThigh = Mathf.Clamp(pose.farThigh, -16f, 26f);
        pose.farShin = Mathf.Clamp(pose.farShin, -50f, 2f);
    }

    static float SpringAngle(Pose pose, int index)
    {
        switch (index)
        {
            case 0: return pose.hairBack;
            case 1: return pose.hairFront;
            case 2: return pose.hairFrontR;
            case 3: return pose.hat;
            case 4: return pose.coatFront;
            default: return pose.coatBack;
        }
    }

    static Pose WithSpring(Pose pose, int index, float value)
    {
        switch (index)
        {
            case 0: pose.hairBack = value; break;
            case 1: pose.hairFront = value; break;
            case 2: pose.hairFrontR = value; break;
            case 3: pose.hat = value; break;
            case 4: pose.coatFront = value; break;
            default: pose.coatBack = value; break;
        }

        return pose;
    }

    static float RotY(float x, float y, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return x * Mathf.Sin(radians) + y * Mathf.Cos(radians);
    }

    // 鞋跟、鞋尖相对膝轴心。髋高 0.25，膝的局部坐标和骨骼一致。
    // 贴图最底一行和这两个点同高，左右各窄 1 像素。
    static float SoleLow(Pose pose)
    {
        const float hipY = 0.25f;
        float low = float.MaxValue;
        low = Mathf.Min(low, hipY + RotY(-0.20f, -1.25f, pose.nearThigh) + RotY(-0.27f, -1.47f, pose.nearThigh + pose.nearShin));
        low = Mathf.Min(low, hipY + RotY(-0.20f, -1.25f, pose.nearThigh) + RotY(0.43f, -1.47f, pose.nearThigh + pose.nearShin));
        low = Mathf.Min(low, hipY + RotY(-0.04f, -1.25f, pose.farThigh) + RotY(-0.17f, -1.47f, pose.farThigh + pose.farShin));
        low = Mathf.Min(low, hipY + RotY(-0.04f, -1.25f, pose.farThigh) + RotY(0.58f, -1.47f, pose.farThigh + pose.farShin));
        return low;
    }

    static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float remain = 1f - t;
        return 1f - remain * remain * remain;
    }

    static float EaseInQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t;
    }

    static float SmoothRange(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    // 面向右，局部 Z 正角度是逆时针。躯干正值后仰、负值前倾。
    // 小腿正值是反膝，前臂负值是反肘。头发正值扫进身体，前摆正值扫进腿。
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

    static Pose IdleRest()
    {
        return new Pose
        {
            nearLower = 1.5f,
            farLower = 1.2f,
            sx = 1f,
            sy = 1f
        };
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
        pose.sx = 1f;
        pose.sy = 1f;
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
        pose.nearShin = -Mathf.Max(0f, -swing) * WalkShin * walk;
        pose.farShin = -Mathf.Max(0f, swing) * WalkShin * walk;
        pose.nearUpper = -swing * WalkArm * walk;
        pose.farUpper = swing * WalkArm * walk;
        pose.nearLower = 2f + Mathf.Max(0f, -swing) * WalkForearm * walk;
        pose.farLower = 2f + Mathf.Max(0f, swing) * WalkForearm * walk;
        pose.torsoBob = Mathf.Abs(step) * 0.015f * walk;
        pose.sx = 1f;
        pose.sy = 1f;
        return pose;
    }

    static Pose CrouchPose()
    {
        return new Pose
        {
            torso = -10f,
            head = 6f,
            hat = -3f,
            hairBack = -4f,
            hairFront = -3f,
            hairFrontR = -4f,
            coatFront = -3f,
            coatBack = -3f,
            nearThigh = 36f,
            nearShin = -40f,
            farThigh = 26f,
            farShin = -30f,
            nearUpper = -18f,
            nearLower = 10f,
            farUpper = -8f,
            farLower = 12f,
            sx = 1.04f,
            sy = 0.94f
        };
    }

    static Pose LaunchPose()
    {
        return new Pose
        {
            torso = -7f,
            head = -2f,
            hat = 5f,
            hairBack = 6f,
            hairFront = 4f,
            hairFrontR = 5f,
            coatFront = 5f,
            coatBack = -4f,
            nearThigh = 10f,
            nearShin = -8f,
            farThigh = -14f,
            farShin = -6f,
            nearUpper = 28f,
            nearLower = 20f,
            farUpper = 16f,
            farLower = 16f,
            sx = 0.95f,
            sy = 1.07f
        };
    }

    static Pose RisePose()
    {
        return new Pose
        {
            torso = -4f,
            head = 2f,
            hat = 5f,
            hairBack = 5f,
            hairFront = 3f,
            hairFrontR = 4f,
            coatFront = 4f,
            coatBack = -3f,
            nearThigh = 34f,
            nearShin = -52f,
            farThigh = -10f,
            farShin = -30f,
            nearUpper = 20f,
            nearLower = 26f,
            farUpper = 10f,
            farLower = 20f,
            sx = 0.98f,
            sy = 1.03f
        };
    }

    static Pose ApexPose()
    {
        return new Pose
        {
            torso = 0f,
            head = 3f,
            hat = -3f,
            hairBack = -4f,
            hairFront = -5f,
            hairFrontR = -7f,
            coatFront = -3f,
            coatBack = 1f,
            nearThigh = 36f,
            nearShin = -55f,
            farThigh = 24f,
            farShin = -48f,
            nearUpper = 36f,
            nearLower = 18f,
            farUpper = -8f,
            farLower = 22f,
            sx = 1f,
            sy = 1f
        };
    }

    static Pose FallPose()
    {
        return new Pose
        {
            torso = 2f,
            head = -5f,
            hat = -7f,
            hairBack = -6f,
            hairFront = -9f,
            hairFrontR = -13f,
            coatFront = -3f,
            coatBack = 2f,
            nearThigh = 16f,
            nearShin = -20f,
            farThigh = 4f,
            farShin = -12f,
            nearUpper = 18f,
            nearLower = 32f,
            farUpper = -8f,
            farLower = 30f,
            sx = 0.98f,
            sy = 1.03f
        };
    }

    static Pose LandPose()
    {
        return new Pose
        {
            torso = -12f,
            head = 7f,
            hat = -4f,
            hairBack = -6f,
            hairFront = -5f,
            hairFrontR = -7f,
            coatFront = -4f,
            coatBack = -4f,
            nearThigh = 36f,
            nearShin = -42f,
            farThigh = 26f,
            farShin = -32f,
            nearUpper = 26f,
            nearLower = 20f,
            farUpper = 14f,
            farLower = 14f,
            sx = 1.05f,
            sy = 0.92f
        };
    }

    // 贴墙角度这次不动。近侧上臂 -50 是向后摆，手会离开右侧的墙；近侧前臂 -28 是反肘。
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
            torsoBob = -0.01f,
            sx = 1f,
            sy = 1f
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
        public float sx;
        public float sy;

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
            pose.sx = Mathf.Lerp(from.sx, to.sx, t);
            pose.sy = Mathf.Lerp(from.sy, to.sy, t);
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
