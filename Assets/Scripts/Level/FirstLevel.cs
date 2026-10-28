using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FirstLevel : MonoBehaviour
{
    private const float FloorTop = 0f;
    private const float GrateLeft = 24.2f;
    private const float GrateHeight = 3.8f;

    private const string ChapterLine = "裂隙暂时合上了。\n她收好愚者牌，往更深处走去。";

    private Player player;
    private PlayerCardLoadout loadout;
    private Transform root;
    private GameObject gate;
    private RiftGuardian boss;
    private BasicEnemy[] arena;
    private GameObject basicSeal;
    private bool basicExitOpen;
    private bool foolTrial;
    private bool chariotTrial;
    private bool chariotLaneOpen;
    private Text objectiveText;
    private Text bossNameSign;
    private CardSelectUI cardUi;
    private Image fadeImage;
    private Text fadeText;
    private GameObject endPanel;
    private SpriteRenderer guideArrow;
    private SpriteRenderer normalMark;
    private SpriteRenderer gapMark;
    private SpriteRenderer extensionMark;
    private Vector3 spawnPoint;
    private float standCenter;
    private float cardX = 20.6f;
    private bool gateOpen;
    private bool cleared;
    private bool busy;
    private bool endingStarted;

    private static Sprite pixel;

    private void Awake()
    {
        Time.timeScale = 1f;
        foolTrial = GameFlow.ActiveLevelId == LevelCatalog.FoolTrialId;
        chariotTrial = GameFlow.ActiveLevelId == LevelCatalog.ChariotTrialId;
        player = GetComponent<Player>();
        loadout = GetComponent<PlayerCardLoadout>();
        cardUi = GetComponent<CardSelectUI>();
        root = new GameObject("LevelOne").transform;
        DisableLegacyGround();
        Build();
        PlacePlayer(true);
        SnapCamera();
    }

    private void Start()
    {
        cardUi = GetComponent<CardSelectUI>();
        BuildObjective();
        BuildNotices();
        if (loadout != null)
            loadout.Changed += RefreshObjective;
        RefreshObjective();
    }

    private void OnDestroy()
    {
        if (loadout != null)
            loadout.Changed -= RefreshObjective;
        if (boss != null)
            boss.Died -= OnBossDefeated;
        if (arena == null)
            return;

        for (int i = 0; i < arena.Length; i++)
        {
            if (arena[i] != null)
                arena[i].Died -= OnArenaEnemyDied;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && !IsCardOpen)
            ReloadLevel();

        if (cleared && endPanel != null && endPanel.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ContinueLevel();

        if (player != null && !busy && !player.IsDefeated)
            UpdateLesson();

        if (busy || player == null || player.IsDefeated || ClearPanelOpen || IsCardOpen)
            return;

        if (player.transform.position.y < -6f)
            StartCoroutine(FallRespawn());
    }

    public void Complete()
    {
        if (cleared)
            return;

        cleared = true;
        RefreshObjective();
        if (endPanel == null)
        {
            Debug.LogWarning("通关面板不存在，无法进入结算。按 R 可以重载当前场景。");
            return;
        }

        if (cardUi != null)
            cardUi.HoldForClear();
        if (player != null)
            player.SetGameplayInputEnabled(false);
        Time.timeScale = 0f;
        endPanel.SetActive(true);
    }

    private void Build()
    {
        int ground = LayerMask.NameToLayer("Ground");
        if (ground < 0)
            ground = 6;

        if (foolTrial)
            BuildFoolTrial(ground);
        else if (chariotTrial)
            BuildChariotTrial(ground);
        else
            BuildBasicCombat(ground);
    }

    private void BuildBasicCombat(int ground)
    {
        const float floorTop = 0f;
        Solid("Floor", new Vector2(14.9f, floorTop - 1f), new Vector2(32.2f, 2f), new Color(0.31f, 0.27f, 0.23f), ground, 0);
        Solid("LeftWall", new Vector2(-1.7f, 3.2f), new Vector2(0.7f, 6.4f), new Color(0.22f, 0.19f, 0.17f), ground, 0);
        Solid("RightWall", new Vector2(31.7f, 3.4f), new Vector2(0.7f, 6.8f), new Color(0.22f, 0.19f, 0.17f), ground, 0);

        // 两座薄平台。哨兵只在台面上巡逻，追击和受击也不会走下边缘。
        const float deckTop = 1.7f;
        const float deckThickness = 0.4f;
        float deckCenterY = deckTop - deckThickness * 0.5f;
        Solid("PlatformA", new Vector2(5.8f, deckCenterY), new Vector2(4.8f, deckThickness), new Color(0.46f, 0.38f, 0.29f), ground, 1);
        Solid("PlatformB", new Vector2(12f, deckCenterY), new Vector2(4.8f, deckThickness), new Color(0.46f, 0.38f, 0.29f), ground, 1);
        Sign("AttackSign", root, new Vector3(5.8f, 2.65f, 0f), "鼠标左键", 180f);
        Sign("DashSign", root, new Vector3(12f, 2.65f, 0f), "左 Shift 躲开", 220f);

        // 低台大约 0.62 高。跳跃攻击的刀光贴近身体，离开低台、面向右侧敌人时按鼠标左键能打到。
        const float lipTop = 0.62f;
        Solid("LowLip", new Vector2(17.4f, lipTop * 0.5f), new Vector2(3.6f, lipTop), new Color(0.43f, 0.36f, 0.28f), ground, 1);
        const float plinthTop = 0.28f;
        Solid("EnemyPlinth", new Vector2(20.6f, plinthTop * 0.5f), new Vector2(1.8f, plinthTop), new Color(0.34f, 0.28f, 0.24f), ground, 1);
        Sign("AirSign", root, new Vector3(18.2f, 1.55f, 0f), "空中攻击", 180f);

        float feetLift = 0.92f;
        arena = new BasicEnemy[3];
        arena[0] = SpawnLeashed("SentryA", 5.8f, deckTop + feetLift, "哨兵甲", new Color(0.72f, 0.28f, 0.22f), 4f, 7.6f, ground);
        arena[1] = SpawnLeashed("SentryB", 12f, deckTop + feetLift, "哨兵乙", new Color(0.28f, 0.38f, 0.72f), 10.2f, 13.8f, ground);
        arena[1].SetAsCharger();
        arena[2] = SpawnLeashed("LowEnemy", 20.6f, plinthTop + feetLift, "低处敌人", new Color(0.62f, 0.28f, 0.78f), 20.25f, 20.95f, ground);

        // 普通跳约 2.46，蹬墙跳在贴墙后再抬约 2.10。柱底 2.40 让人能走过去，台面 3.40 让普通跳和冲刺都上不去。
        basicSeal = Solid("ExitSeal", new Vector2(22.35f, 1.55f), new Vector2(0.55f, 3.1f), new Color(0.45f, 0.2f, 0.22f), ground, 3);
        Solid("ClimbPillar", new Vector2(23.7f, 4.25f), new Vector2(0.7f, 3.7f), new Color(0.62f, 0.66f, 0.72f), ground, 2);
        Solid("HighLedge", new Vector2(27.85f, 1.7f), new Vector2(4.9f, 3.4f), new Color(0.38f, 0.32f, 0.28f), ground, 1);
        Sign("WallJumpSign", root, new Vector3(24.7f, 1.45f, 0f), "蹬墙跳", 160f);
        Sign("ExitLabel", root, new Vector3(29.2f, 4.55f, 0f), "出口", 140f);
        CreateBasicExit(29.2f, 4.7f);
    }

    private BasicEnemy SpawnLeashed(string objectName, float x, float y, string displayName, Color color, float minX, float maxX, int ground)
    {
        BasicEnemy enemy = SpawnEnemy(objectName, x, 3, false, displayName, color, "Player", 6);
        enemy.transform.position = new Vector3(x, y, 0f);
        enemy.SetAsPractice();
        enemy.StayOnPlatform(minX, maxX, 1 << ground);
        enemy.Died += OnArenaEnemyDied;
        return enemy;
    }

    private void CreateBasicExit(float x, float y)
    {
        GameObject exit = new GameObject("LevelExit");
        exit.transform.SetParent(root, false);
        exit.transform.position = new Vector3(x, y, 0f);
        BoxCollider2D trigger = exit.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.6f, 2.6f);
        exit.AddComponent<LevelExit>().Arm(this);
    }

    private void OnArenaEnemyDied()
    {
        if (basicExitOpen || arena == null)
            return;

        for (int i = 0; i < arena.Length; i++)
        {
            if (arena[i] != null && !arena[i].IsDead)
                return;
        }

        if (chariotTrial)
            Complete();
        else
            OpenBasicExit();
    }

    private void OpenBasicExit()
    {
        if (basicExitOpen)
            return;

        basicExitOpen = true;
        ReleaseSeal();
        RefreshObjective();
    }

    private void ReleaseSeal()
    {
        if (basicSeal == null)
            return;

        Collider2D[] blocking = basicSeal.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < blocking.Length; i++)
            blocking[i].enabled = false;
        AttackFeedback.Thud(basicSeal.transform.position);
        StartCoroutine(SinkSeal());
    }

    private IEnumerator SinkSeal()
    {
        if (basicSeal == null)
            yield break;

        Vector3 start = basicSeal.transform.position;
        Vector3 end = start + Vector3.down * 3.6f;
        float elapsed = 0f;
        const float duration = 0.45f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            basicSeal.transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        basicSeal.SetActive(false);
    }

    private void BuildFoolTrial(int ground)
    {
        Solid("Floor", new Vector2(16.25f, -1f), new Vector2(36.5f, 2f), new Color(0.31f, 0.27f, 0.23f), ground, 0);
        Solid("LeftWall", new Vector2(-2.35f, 3.75f), new Vector2(0.7f, 7.5f), new Color(0.22f, 0.19f, 0.17f), ground, 0);
        Solid("RightWall", new Vector2(34.15f, 3f), new Vector2(0.7f, 6f), new Color(0.22f, 0.19f, 0.17f), ground, 0);
        Solid("Arch", new Vector2(20.4f, 5.5f), new Vector2(0.75f, 3.8f), new Color(0.2f, 0.17f, 0.16f), ground, 1);

        MonsterCardData fool = Resources.Load<MonsterCardData>("Cards/Fool");
        if (fool == null)
            Debug.LogWarning("没有找到 Resources/Cards/Fool，愚者牌试用关无法获得愚者牌。");
        BuildReachGate(ground, fool);
        CreateCard(new Vector3(cardX, 2.2f, 0f), fool);
        CreateSprite(root, "CardMat", new Vector3(cardX, 0.16f, 0f), new Vector3(1.5f, 0.1f, 1f), new Color(0.95f, 0.78f, 0.28f, 0.9f), "Ground", 2);
        CreateExit();
        BuildGuideArrow();
    }

    private void BuildChariotTrial(int ground)
    {
        Solid("Floor", new Vector2(9f, -1f), new Vector2(20f, 2f), new Color(0.31f, 0.27f, 0.23f), ground, 0);
        Solid("LeftWall", new Vector2(-1.5f, 3f), new Vector2(0.7f, 6f), new Color(0.22f, 0.19f, 0.17f), ground, 0);
        Solid("RightWall", new Vector2(19.2f, 3f), new Vector2(0.7f, 6f), new Color(0.22f, 0.19f, 0.17f), ground, 0);

        MonsterCardData chariot = Resources.Load<MonsterCardData>("Cards/Chariot");
        if (chariot == null)
            Debug.LogWarning("没有找到 Resources/Cards/Chariot，战车试炼无法获得战车牌。");
        CreateCard(new Vector3(4.2f, 1.35f, 0f), chariot);

        basicSeal = Solid("LaneSeal", new Vector2(7.6f, 1.6f), new Vector2(0.55f, 3.2f), new Color(0.25f, 0.45f, 0.62f), ground, 3);
        Sign("DashThrough", root, new Vector3(12f, 2.55f, 0f), "冲过去", 180f);

        arena = new BasicEnemy[3];
        arena[0] = SpawnLane("LaneA", 9.6f, "列兵甲", new Color(0.25f, 0.55f, 0.72f), ground);
        arena[1] = SpawnLane("LaneB", 11f, "列兵乙", new Color(0.2f, 0.42f, 0.68f), ground);
        arena[2] = SpawnLane("LaneC", 12.4f, "列兵丙", new Color(0.16f, 0.32f, 0.58f), ground);
    }

    private BasicEnemy SpawnLane(string objectName, float x, string displayName, Color color, int ground)
    {
        BasicEnemy enemy = SpawnEnemy(objectName, x, 2, false, displayName, color, "Player", 6);
        enemy.transform.position = new Vector3(x, 0.92f, 0f);
        enemy.StayOnPlatform(x - 0.4f, x + 0.4f, 1 << ground);
        enemy.SetPushable();
        enemy.Died += OnArenaEnemyDied;
        return enemy;
    }

    private void OpenChariotLane()
    {
        if (chariotLaneOpen)
            return;

        chariotLaneOpen = true;
        ReleaseSeal();
        RefreshObjective();
    }

    private void BuildReachGate(int ground, MonsterCardData fool)
    {
        CapsuleCollider2D body = player.GetComponent<CapsuleCollider2D>();
        if (body == null)
            Debug.LogWarning("玩家没有 CapsuleCollider2D，第一关用默认身体尺寸计算攻击距离和 Boss 招式。");
        float halfBody = body != null ? body.size.x * 0.5f : 0.385f;
        standCenter = GrateLeft - halfBody - 0.06f;
        float normalReach = player.AttackFrontReach(1f);
        float foolReach = fool != null ? fool.reachMultiplier : 2.15f;
        float normalFront = standCenter + normalReach;
        float extendedFront = standCenter + player.AttackFrontReach(foolReach);
        float enemyLeft = Mathf.Lerp(normalFront, extendedFront, 0.38f);
        // 挥击只覆盖黄线站桩点。突进从这里再向后延伸一段普通攻击距离。
        float slashMinX = standCenter - 0.08f;
        float thrustMinX = slashMinX - normalReach;
        cardX = Mathf.Clamp(thrustMinX - 1.55f, 19.4f, 21.3f);

        GameObject enemyObject = new GameObject("RiftGuardian");
        enemyObject.transform.SetParent(root, false);
        enemyObject.transform.localScale = new Vector3(1.35f, 1.62f, 1f);
        SpriteRenderer enemySprite = enemyObject.AddComponent<SpriteRenderer>();
        Rigidbody2D enemyBody = enemyObject.AddComponent<Rigidbody2D>();
        enemyBody.bodyType = RigidbodyType2D.Kinematic;
        enemyBody.gravityScale = 0f;
        enemyBody.constraints = RigidbodyConstraints2D.FreezeAll;
        CapsuleCollider2D capsule = enemyObject.AddComponent<CapsuleCollider2D>();
        capsule.size = new Vector2(0.8f, 1.5f);
        capsule.offset = new Vector2(0f, -0.05f);
        boss = enemyObject.AddComponent<RiftGuardian>();
        float enemyHalf = capsule.size.x * 0.5f * Mathf.Abs(enemyObject.transform.lossyScale.x);
        float enemyBottom = capsule.offset.y - capsule.size.y * 0.5f;
        float enemyCenter = enemyLeft + enemyHalf;
        enemyObject.transform.position = new Vector3(enemyCenter, 0.04f - enemyObject.transform.localScale.y * enemyBottom, 0f);
        enemySprite.sortingLayerName = "Ground";
        enemySprite.sortingOrder = 2;
        CreateSprite(enemyObject.transform, "Core", new Vector3(0f, 0.2f, 0f), new Vector3(0.34f, 0.42f, 1f), new Color(1f, 0.86f, 0.45f), "Ground", 3);
        CreateSprite(enemyObject.transform, "Aura", Vector3.zero, new Vector3(1.25f, 1.85f, 1f), new Color(0.9f, 0.24f, 0.18f, 0.33f), "Player", -8);
        CreateSprite(enemyObject.transform, "HornL", new Vector3(-0.28f, 0.62f, 0f), new Vector3(0.16f, 0.32f, 1f), new Color(0.55f, 0.08f, 0.08f), "Ground", 3);
        CreateSprite(enemyObject.transform, "HornR", new Vector3(0.28f, 0.62f, 0f), new Vector3(0.16f, 0.32f, 1f), new Color(0.55f, 0.08f, 0.08f), "Ground", 3);

        float playerHeight = body != null ? body.size.y * Mathf.Abs(player.transform.lossyScale.y) : 1.9f;
        float playerWidth = body != null ? body.size.x * Mathf.Abs(player.transform.lossyScale.x) : 0.77f;
        boss.Bind(player, 12, "裂隙守卫", slashMinX, GrateLeft, thrustMinX, 0f, FloorTop + playerHeight + 0.22f, playerHeight * 0.4f, Mathf.Max(1.15f, playerWidth * 2.2f));
        boss.Died += OnBossDefeated;

        float grateWidth = Mathf.Max(2.4f, enemyCenter + enemyHalf + 0.32f - GrateLeft);
        gate = new GameObject("Grate");
        gate.layer = ground;
        gate.transform.SetParent(root, false);
        gate.transform.position = new Vector3(GrateLeft + grateWidth * 0.5f, GrateHeight * 0.5f, 0f);
        Rigidbody2D gateBody = gate.AddComponent<Rigidbody2D>();
        gateBody.bodyType = RigidbodyType2D.Kinematic;
        gateBody.gravityScale = 0f;
        gateBody.freezeRotation = true;
        BuildBars(gate.transform, ground, grateWidth);

        float plateLeft = enemyLeft - player.AttackFrontReach(foolReach);
        GroundMark("StandPlate", plateLeft, standCenter, 0.1f, 0.16f, new Color(0.95f, 0.72f, 0.22f, 1f), 3);
        GroundMark("StandLip", standCenter, GrateLeft, 0.16f, 0.1f, new Color(1f, 0.92f, 0.55f, 1f), 4);
        normalMark = GroundMark("NormalReach", GrateLeft, normalFront, 0.28f, 0.07f, new Color(0.75f, 0.84f, 1f, 0.9f), 3);
        gapMark = GroundMark("ReachGap", normalFront, enemyLeft, 0.28f, 0.07f, new Color(0.85f, 0.28f, 0.22f, 0.95f), 3);
        extensionMark = GroundMark("FoolReach", normalFront, extendedFront, 0.4f, 0.11f, new Color(1f, 0.78f, 0.2f, 0.95f), 4);
        extensionMark.enabled = false;

        bossNameSign = Sign("SentinelLabel", root, new Vector3(enemyCenter, 4.85f, 0f), "裂隙守卫", 240f);
    }

    private void BuildBars(Transform grate, int ground, float grateWidth)
    {
        const float barWidth = 0.24f;
        int count = 5;
        float gap = (grateWidth - barWidth * count) / (count - 1);
        while (gap > 0.48f && count < 8)
        {
            count++;
            gap = (grateWidth - barWidth * count) / (count - 1);
        }

        for (int i = 0; i < count; i++)
        {
            float left = -grateWidth * 0.5f + i * (barWidth + gap);
            GameObject bar = new GameObject("Bar" + i);
            bar.layer = ground;
            bar.transform.SetParent(grate, false);
            bar.transform.localPosition = new Vector3(left + barWidth * 0.5f, 0f, 0f);
            bar.transform.localScale = new Vector3(barWidth, GrateHeight, 1f);
            SpriteRenderer sprite = bar.AddComponent<SpriteRenderer>();
            sprite.sprite = PixelSprite();
            sprite.color = new Color(0.55f, 0.58f, 0.66f, 1f);
            sprite.sortingLayerName = "Ground";
            sprite.sortingOrder = 5;
            BoxCollider2D box = bar.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
        }

        CreateSprite(grate, "Lintel", new Vector3(0f, GrateHeight * 0.5f - 0.18f, 0f), new Vector3(grateWidth, 0.36f, 1f), new Color(0.24f, 0.2f, 0.28f), "Ground", 6);
    }

    private SpriteRenderer GroundMark(string name, float left, float right, float y, float height, Color color, int sortingOrder)
    {
        float width = Mathf.Max(0.08f, right - left);
        return CreateSprite(root, name, new Vector3((left + right) * 0.5f, y, 0f), new Vector3(width, height, 1f), color, "Ground", sortingOrder);
    }

    private void CreateCard(Vector3 position, MonsterCardData card)
    {
        bool chariot = card != null && card.effectType == CardEffectType.ChariotDash;
        GameObject pickup = new GameObject("CardPickup");
        pickup.transform.SetParent(root, false);
        pickup.transform.position = position;
        Color glow = chariot ? new Color(0.45f, 0.75f, 1f, 0.28f) : new Color(1f, 0.82f, 0.35f, 0.28f);
        Color face = chariot ? new Color(0.45f, 0.78f, 0.95f) : new Color(0.95f, 0.76f, 0.28f);
        CreateSprite(pickup.transform, "Glow", Vector3.zero, new Vector3(1.55f, 1.85f, 1f), glow, "Player", 5);
        CreateSprite(pickup.transform, "Face", Vector3.zero, new Vector3(0.72f, 1.08f, 1f), face, "Player", 6);
        CreateSprite(pickup.transform, "Mark", new Vector3(0f, 0.06f, 0f), new Vector3(0.36f, 0.48f, 1f), new Color(0.42f, 0.16f, 0.58f), "Player", 7);

        BoxCollider2D trigger = pickup.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.5f, 1.9f);
        pickup.AddComponent<CardPickup>().Arm(card);
        string label = card != null && !string.IsNullOrEmpty(card.displayName) ? card.displayName : "牌";
        Sign("CardLabel", pickup.transform, position + Vector3.up * 1.15f, label, 160f);
    }

    private void CreateExit()
    {
        CreateSprite(root, "ExitPole", new Vector3(31.15f, 1.15f, 0f), new Vector3(0.12f, 2.3f, 1f), new Color(0.35f, 0.28f, 0.2f), "Player", 3);
        CreateSprite(root, "ExitBanner", new Vector3(31.55f, 1.9f, 0f), new Vector3(0.7f, 0.48f, 1f), new Color(0.82f, 0.22f, 0.18f), "Player", 4);
        Sign("ExitLabel", root, new Vector3(31.3f, 2.7f, 0f), "出口", 140f);

        GameObject exit = new GameObject("LevelExit");
        exit.transform.SetParent(root, false);
        exit.transform.position = new Vector3(31.2f, 1.2f, 0f);
        BoxCollider2D trigger = exit.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.8f, 2.4f);
        exit.AddComponent<LevelExit>().Arm(this);
    }

    private BasicEnemy SpawnEnemy(string objectName, float x, int health, bool rooted, string displayName, Color color, string sortingLayer, int sortingOrder)
    {
        GameObject enemy = new GameObject(objectName);
        enemy.transform.SetParent(root, false);
        enemy.transform.position = new Vector3(x, 1.35f, 0f);
        SpriteRenderer sprite = enemy.AddComponent<SpriteRenderer>();
        enemy.AddComponent<Rigidbody2D>();
        enemy.AddComponent<CapsuleCollider2D>();
        BasicEnemy basic = enemy.AddComponent<BasicEnemy>();
        if (rooted)
            basic.RootAt(player, health, displayName);
        else
            basic.Configure(health, false, player, displayName);

        basic.SetBodyColor(color);
        sprite.sortingLayerName = sortingLayer;
        sprite.sortingOrder = sortingOrder;
        return basic;
    }

    private void OpenGate()
    {
        if (gateOpen)
            return;

        gateOpen = true;
        if (normalMark != null)
            normalMark.enabled = false;
        if (gapMark != null)
            gapMark.enabled = false;
        if (extensionMark != null)
            extensionMark.enabled = false;
        if (guideArrow != null)
            guideArrow.enabled = false;
        if (gate != null)
        {
            Collider2D[] blocking = gate.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < blocking.Length; i++)
                blocking[i].enabled = false;
            SpriteRenderer[] bars = gate.GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < bars.Length; i++)
                bars[i].color = new Color(0.9f, 0.94f, 1f, 1f);
            AttackFeedback.Thud(gate.transform.position);
            StartCoroutine(SinkGate());
        }

        RefreshObjective();
    }

    private IEnumerator SinkGate()
    {
        Vector3 start = gate.transform.position;
        Vector3 end = start + Vector3.down * (GrateHeight + 0.4f);
        float elapsed = 0f;
        const float duration = 0.85f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            gate.transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        gate.SetActive(false);
    }

    private void PlacePlayer(bool defineSpawn)
    {
        if (defineSpawn)
        {
            CapsuleCollider2D body = player.GetComponent<CapsuleCollider2D>();
            if (body == null)
            {
                Debug.LogWarning("玩家没有 CapsuleCollider2D，使用默认出生高度。");
                spawnPoint = new Vector3(0.4f, 1.4f, 0f);
            }
            else
            {
                float bottom = body.offset.y - body.size.y * 0.5f;
                spawnPoint = new Vector3(0.4f, FloorTop - bottom + 0.06f, 0f);
            }
        }

        player.transform.position = spawnPoint;
        player.rb.position = spawnPoint;
        player.rb.linearVelocity = Vector2.zero;
    }

    private void SnapCamera()
    {
        Vector3 focus = spawnPoint + Vector3.up * 1.4f;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 position = cam.transform.position;
            position.x = focus.x;
            position.y = focus.y;
            cam.transform.position = position;
        }

        GameObject vcam = GameObject.Find("CinemachineCamera");
        if (vcam == null)
            return;

        Vector3 vcamPosition = vcam.transform.position;
        vcamPosition.x = focus.x;
        vcamPosition.y = focus.y;
        vcam.transform.position = vcamPosition;
    }

    private void BuildObjective()
    {
        GameObject canvasObject = new GameObject("LevelObjective");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Image bar = CreateUiImage(canvasObject.transform, "Bar", new Color(0.05f, 0.04f, 0.07f, 0.78f));
        RectTransform barRect = bar.rectTransform;
        barRect.anchorMin = new Vector2(0.5f, 1f);
        barRect.anchorMax = new Vector2(0.5f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = new Vector2(0f, -18f);
        barRect.sizeDelta = new Vector2(820f, 96f);

        Text title = CreateUiText(bar.transform, "Title", 30, TextAnchor.UpperCenter, new Color(1f, 0.9f, 0.62f));
        title.text = chariotTrial ? "牌之回响  战车" : foolTrial ? "愚者牌试用" : "第一关  基础战斗";
        StretchTop(title.rectTransform, -8f, 40f);

        objectiveText = CreateUiText(bar.transform, "Objective", 26, TextAnchor.UpperCenter, Color.white);
        StretchTop(objectiveText.rectTransform, -46f, 40f);
    }

    private void RefreshObjective()
    {
        if (objectiveText == null)
            return;

        if (chariotTrial)
        {
            RefreshChariotObjective();
            return;
        }

        if (!foolTrial)
        {
            RefreshBasicObjective();
            return;
        }

        if (cleared)
        {
            SetObjective("愚者牌试用完成");
            return;
        }

        if (endingStarted)
        {
            SetObjective("裂隙正在关闭");
            return;
        }

        float x = player != null ? player.transform.position.x : 0f;
        if (boss != null && boss.IsAwake && !boss.IsDead)
        {
            if (boss.HasAttacked)
                SetObjective("");
            else if (boss.IsTelegraphing)
                SetObjective("看地面预兆再躲开");
            else
                SetObjective("守卫醒来了");
            return;
        }

        if (FoolEquipped)
        {
            SetObjective("站上黄线，鼠标左键攻击栏杆后的守卫");
            return;
        }

        if (loadout != null && loadout.PendingCard != null)
        {
            SetObjective("J 或回车装备，Esc 先用鼠标左键试试");
            return;
        }

        if (!HasFool && x > 12f)
        {
            SetObjective("拾取愚者");
            return;
        }

        if (x < 4f)
        {
            SetObjective("WASD 移动，空格跳跃，左 Shift 冲刺");
            return;
        }

        SetObjective("往前拾取愚者");
    }

    private void RefreshBasicObjective()
    {
        float x = player != null ? player.transform.position.x : 0f;
        if (basicExitOpen && x > 26.5f)
        {
            SetObjective("走进出口，进入愚者牌试用");
            return;
        }

        if (basicExitOpen)
        {
            SetObjective("贴灰柱按住 A，空格蹬出后松开 A");
            return;
        }

        if (x < 3f)
        {
            SetObjective("WASD 移动，空格跳跃，左 Shift 冲刺");
            return;
        }

        if (ArenaAlive(2) && x > 14.6f)
        {
            SetObjective("跳起后马上按鼠标左键，或离开低台时按");
            return;
        }

        if (ArenaAlive(1) && x > 8.4f)
        {
            SetObjective("哨兵挥刀前，左 Shift 冲刺躲开");
            return;
        }

        if (ArenaAlive(0) || ArenaAlive(1))
        {
            SetObjective("跳上平台，鼠标左键攻击哨兵");
            return;
        }

        SetObjective("三个敌人都倒下后，再去蹬墙跳");
    }

    private void RefreshChariotObjective()
    {
        if (cleared)
        {
            SetObjective("战车试炼完成");
            return;
        }

        if (loadout != null && loadout.PendingCard != null)
        {
            SetObjective("J 或回车装备战车");
            return;
        }

        if (!ChariotEquipped)
        {
            SetObjective("拾取战车。冲刺会拖出另一具自己");
            return;
        }

        if (!chariotLaneOpen)
        {
            SetObjective("装备之后，左 Shift 冲刺穿过他们");
            return;
        }

        if (ArenaAlive(0) || ArenaAlive(1) || ArenaAlive(2))
        {
            SetObjective("左 Shift 冲刺穿过。影子会补上一刀");
            return;
        }

        SetObjective("战车试炼完成");
    }

    private bool ArenaAlive(int index)
    {
        return arena != null && index >= 0 && index < arena.Length && arena[index] != null && !arena[index].IsDead;
    }

    private void SetObjective(string value)
    {
        if (objectiveText == null || objectiveText.text == value)
            return;

        objectiveText.text = value;
    }

    private bool HasFool =>
        FoolEquipped ||
        (loadout != null && loadout.PendingCard != null) ||
        OwnsFool();

    private bool OwnsFool()
    {
        if (loadout == null)
            return false;

        for (int i = 0; i < loadout.OwnedCards.Count; i++)
        {
            MonsterCardData card = loadout.OwnedCards[i];
            if (card != null && card.effectType == CardEffectType.FoolLunge)
                return true;
        }

        return false;
    }

    private void UpdateLesson()
    {
        if (chariotTrial)
        {
            if (!chariotLaneOpen && ChariotEquipped)
                OpenChariotLane();
            RefreshObjective();
            return;
        }

        bool fool = FoolEquipped;
        if (boss != null)
            boss.SetFoolReady(fool);
        RefreshObjective();
        if (normalMark != null)
            normalMark.enabled = !gateOpen;
        if (gapMark != null)
            gapMark.enabled = !fool && !gateOpen;
        if (extensionMark != null)
        {
            extensionMark.enabled = fool && !gateOpen;
            if (extensionMark.enabled)
            {
                float pulse = 0.55f + Mathf.PingPong(Time.time, 0.4f);
                extensionMark.color = new Color(1f, 0.78f, 0.2f, pulse);
            }
        }

        UpdateGuideArrow();
    }

    private void BuildGuideArrow()
    {
        Texture2D texture = new Texture2D(16, 16);
        texture.filterMode = FilterMode.Point;
        Color[] pixels = new Color[256];
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                bool shaft = x >= 1 && x <= 7 && Mathf.Abs(y - 7) <= 1;
                bool head = x >= 7 && x <= 14 && Mathf.Abs(y - 7) <= (14 - x) * 0.72f;
                pixels[y * 16 + x] = shaft || head ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);

        GameObject arrow = new GameObject("GuideArrow");
        arrow.transform.SetParent(root, false);
        guideArrow = arrow.AddComponent<SpriteRenderer>();
        guideArrow.sprite = sprite;
        guideArrow.color = new Color(1f, 0.82f, 0.15f, 1f);
        guideArrow.sortingLayerName = "Player";
        guideArrow.sortingOrder = 20;
        guideArrow.enabled = false;
    }

    private void UpdateGuideArrow()
    {
        if (guideArrow == null || player == null)
            return;

        const float flagX = 31.2f;
        float distance = flagX - player.transform.position.x;
        bool show = gateOpen && !cleared && !endingStarted && Mathf.Abs(distance) > 2.4f;
        if (!show)
        {
            guideArrow.enabled = false;
            return;
        }

        float dir = Mathf.Sign(distance);
        bool blinkOn = Mathf.Repeat(Time.time, 0.5f) < 0.26f;
        guideArrow.enabled = blinkOn;
        guideArrow.transform.position = player.transform.position + new Vector3(dir * 1.7f, 1.25f, 0f);
        guideArrow.transform.localScale = new Vector3(dir * 1.35f, 1.35f, 1f);
    }

    private bool IsCardOpen => cardUi != null && (cardUi.IsPanelOpen || cardUi.IsStatusOpen);
    private bool ClearPanelOpen => cleared && endPanel != null && endPanel.activeSelf;

    private void ReloadLevel()
    {
        GameFlow.ReloadActiveScene();
    }

    private IEnumerator FallRespawn()
    {
        busy = true;
        player.SetGameplayInputEnabled(false);
        if (fadeText != null)
            fadeText.text = "掉下去了，回到起点";
        AttackFeedback.Thud(player.transform.position);
        yield return Fade(0f, 0.82f, 0.2f);
        PlacePlayer(false);
        SnapCamera();
        yield return new WaitForSecondsRealtime(0.12f);
        yield return Fade(0.82f, 0f, 0.28f);
        if (!player.IsDefeated && !IsCardOpen && !ClearPanelOpen)
            player.SetGameplayInputEnabled(true);
        busy = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeImage == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetFade(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetFade(to);
    }

    private void SetFade(float alpha)
    {
        if (fadeImage == null)
            return;

        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
        if (fadeText != null)
        {
            Color textColor = fadeText.color;
            textColor.a = alpha > 0.2f ? 1f : alpha / 0.2f;
            fadeText.color = textColor;
        }
    }

    private void BuildNotices()
    {
        GameObject canvasObject = new GameObject("LevelNotice");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        fadeImage = CreateUiImage(canvasObject.transform, "Fade", new Color(0.02f, 0.02f, 0.03f, 0f));
        RectTransform fadeRect = fadeImage.rectTransform;
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        fadeImage.raycastTarget = false;

        fadeText = CreateUiText(canvasObject.transform, "FadeText", 36, TextAnchor.MiddleCenter, new Color(1f, 0.94f, 0.8f, 0f));
        RectTransform fadeTextRect = fadeText.rectTransform;
        fadeTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        fadeTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        fadeTextRect.sizeDelta = new Vector2(920f, 160f);

        endPanel = new GameObject("ClearPanel");
        endPanel.transform.SetParent(canvasObject.transform, false);
        Image endImage = endPanel.AddComponent<Image>();
        endImage.color = new Color(0.07f, 0.05f, 0.09f, 0.94f);
        RectTransform endRect = endPanel.GetComponent<RectTransform>();
        endRect.sizeDelta = chariotTrial ? new Vector2(640f, 480f) : new Vector2(640f, foolTrial ? 760f : 640f);
        Sprite panelSkin = UiArt.Load(UiArt.Panel);
        if (panelSkin != null)
        {
            endImage.sprite = panelSkin;
            endImage.color = Color.white;
        }

        Text endTitle = CreateUiText(endPanel.transform, "Title", 40, TextAnchor.UpperCenter, new Color(1f, 0.88f, 0.55f));
        endTitle.text = chariotTrial ? "战车试炼完成" : "愚者牌试用完成";
        StretchTop(endTitle.rectTransform, -28f, 56f);

        Text ending = CreateUiText(endPanel.transform, "Ending", 26, TextAnchor.UpperCenter, new Color(1f, 0.94f, 0.82f));
        ending.text = chariotTrial ? "冲刺拖出了另一具自己。" : ChapterLine;
        StretchTop(ending.rectTransform, -96f, chariotTrial ? 72f : 100f);

        if (chariotTrial)
        {
            CreateChoice(endPanel.transform, "再试一次", new Vector2(0f, 20f), ReplayLevel);
            CreateChoice(endPanel.transform, "返回标题", new Vector2(0f, -70f), GameFlow.LoadTitle);
            CreateChoice(endPanel.transform, "选择关卡", new Vector2(0f, -160f), GameFlow.LoadLevelSelect);
        }
        else
        {
            CreateChoice(endPanel.transform, "重玩本关", new Vector2(0f, 70f), ReplayLevel);
            CreateChoice(endPanel.transform, "返回标题", new Vector2(0f, -16f), GameFlow.LoadTitle);
            CreateChoice(endPanel.transform, "继续试玩当前关卡", new Vector2(0f, -102f), ContinueLevel);
            CreateChoice(endPanel.transform, "选择关卡", new Vector2(0f, -188f), GameFlow.LoadLevelSelect);
            if (foolTrial)
                CreateChoice(endPanel.transform, "牌之回响", new Vector2(0f, -274f), GameFlow.LoadChariotTrial);
        }
        endPanel.SetActive(false);
    }

    private void ContinueLevel()
    {
        if (endPanel == null || !endPanel.activeSelf)
            return;

        endPanel.SetActive(false);
        busy = false;
        if (fadeText != null)
            fadeText.text = "";
        if (cardUi != null)
            cardUi.ReleaseFromClear();
        if (!IsCardOpen)
            Time.timeScale = 1f;
        if (player != null && !player.IsDefeated && !IsCardOpen)
            player.SetGameplayInputEnabled(true);
        if (fadeImage != null && fadeImage.color.a > 0.01f)
            StartCoroutine(Fade(fadeImage.color.a, 0f, 0.25f));
    }

    private void ReplayLevel()
    {
        GameFlow.ReloadActiveScene();
    }

    private void OnBossDefeated()
    {
        if (endingStarted || (player != null && player.IsDefeated))
            return;

        endingStarted = true;
        busy = true;
        if (cardUi != null)
            cardUi.HoldForClear();
        Time.timeScale = 1f;
        if (player != null)
        {
            player.SetGameplayInputEnabled(false);
            if (player.rb != null)
                player.rb.linearVelocity = Vector2.zero;
        }

        if (boss != null)
            CreateSprite(root, "RiftAsh", new Vector3(boss.transform.position.x, 0.12f, 0f), new Vector3(1.8f, 0.22f, 1f), new Color(0.16f, 0.05f, 0.08f, 0.95f), "Ground", 2);
        CreateSprite(root, "RiftCalm", new Vector3(standCenter, 0.2f, 0f), new Vector3(1.8f, 0.12f, 1f), new Color(0.55f, 0.85f, 1f, 0.9f), "Ground", 4);
        if (bossNameSign != null)
            bossNameSign.transform.parent.gameObject.SetActive(false);

        OpenGate();
        StartCoroutine(PlayEnding());
    }

    private IEnumerator PlayEnding()
    {
        SetObjective("裂隙正在关闭");
        yield return new WaitForSeconds(1.05f);
        if (fadeText != null)
            fadeText.text = ChapterLine;
        yield return Fade(0f, 0.8f, 0.4f);
        yield return new WaitForSeconds(0.7f);
        Complete();
    }

    private void CreateChoice(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label);
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        Sprite skin = UiArt.Load(UiArt.Button);
        image.sprite = skin;
        image.color = skin != null ? Color.white : new Color(0.35f, 0.22f, 0.45f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360f, 64f);
        rect.anchoredPosition = position;
        Text text = CreateUiText(buttonObject.transform, "Label", 26, TextAnchor.MiddleCenter, Color.white);
        text.text = label;
        text.rectTransform.sizeDelta = rect.sizeDelta;
    }

    public bool UseExit()
    {
        if (!foolTrial && !basicExitOpen)
            return false;

        if (foolTrial)
        {
            Complete();
            return true;
        }

        return GameFlow.LoadFoolTrial();
    }

    private bool FoolEquipped =>
        loadout != null &&
        loadout.EquippedCard != null &&
        loadout.EquippedCard.effectType == CardEffectType.FoolLunge;

    private bool ChariotEquipped =>
        loadout != null &&
        loadout.EquippedCard != null &&
        loadout.EquippedCard.effectType == CardEffectType.ChariotDash;

    private void DisableLegacyGround()
    {
        GameObject legacy = GameObject.Find("Platform");
        if (legacy != null)
            legacy.SetActive(false);
    }

    private GameObject Solid(string name, Vector2 center, Vector2 size, Color color, int layer, int sortingOrder)
    {
        GameObject block = new GameObject(name);
        block.layer = layer;
        block.transform.SetParent(root, false);
        block.transform.position = new Vector3(center.x, center.y, 0f);
        block.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer sprite = block.AddComponent<SpriteRenderer>();
        sprite.sprite = PixelSprite();
        sprite.color = color;
        sprite.sortingLayerName = "Ground";
        sprite.sortingOrder = sortingOrder;
        BoxCollider2D box = block.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
        return block;
    }

    private void Decor(string name, Vector2 center, Vector2 size, Color color, int sortingOrder)
    {
        CreateSprite(root, name, new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 1f), color, "Ground", sortingOrder);
    }

    private static SpriteRenderer CreateSprite(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, string sortingLayer, int sortingOrder)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localScale = localScale;
        SpriteRenderer sprite = obj.AddComponent<SpriteRenderer>();
        sprite.sprite = PixelSprite();
        sprite.color = color;
        sprite.sortingLayerName = sortingLayer;
        sprite.sortingOrder = sortingOrder;
        return sprite;
    }

    private Text Sign(string name, Transform parent, Vector3 worldPosition, string message, float width)
    {
        GameObject canvasObject = new GameObject(name);
        canvasObject.transform.SetParent(parent, false);
        canvasObject.transform.position = worldPosition;
        canvasObject.transform.localScale = Vector3.one * 0.012f;
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = "Player";
        canvas.sortingOrder = 12;
        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, 70f);

        Text text = CreateUiText(canvasObject.transform, "Text", 32, TextAnchor.MiddleCenter, new Color(1f, 0.94f, 0.75f));
        text.text = message;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return text;
    }

    private static Text CreateUiText(Transform parent, string name, int size, TextAnchor anchor, Color color)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = GameFont.Get();
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateUiImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void StretchTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-24f, height);
    }

    private static Sprite PixelSprite()
    {
        if (pixel != null)
            return pixel;

        Texture2D texture = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point;
        texture.Apply();
        pixel = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        return pixel;
    }
}

public class CardPickup : MonoBehaviour
{
    private MonsterCardData card;
    private Vector3 origin;
    private bool taken;

    public void Arm(MonsterCardData data)
    {
        card = data;
        origin = transform.position;
    }

    private void Update()
    {
        if (taken)
            return;

        float wave = Mathf.Sin(Time.time * 2.2f) * 0.1f;
        transform.position = origin + Vector3.up * wave;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (taken || card == null || other.GetComponent<Player>() == null)
            return;

        taken = true;
        CardReward.Grant(card);
        gameObject.SetActive(false);
    }
}

public class LevelExit : MonoBehaviour
{
    private FirstLevel level;
    private bool used;

    public void Arm(FirstLevel owner)
    {
        level = owner;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used || level == null || other.GetComponent<Player>() == null)
            return;

        if (!level.UseExit())
            return;

        used = true;
    }
}
