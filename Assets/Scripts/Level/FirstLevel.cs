using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FirstLevel : MonoBehaviour
{
    private const float FloorTop = 0f;
    private const float GrateLeft = 24.2f;
    private const float GrateHeight = 3.8f;

    // 愚者试用结束：这一章的收尾，愚者牌到手。
    private const string ChapterLine = "裂隙暂时合上了。\n她收好愚者牌，往更深处走去。";
    // 终局结束：不再提「收好愚者牌」，这是整段演示的收尾。
    private const string FinaleLine = "裂隙彻底闭合了。\n三张塔罗牌在她手里安静下来。";

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
    private bool bossLevel;
    private bool bossChoiceOpen;
    private GameObject cardChoiceRoot;
    private bool echoFoolDone;
    private bool echoChariotDone;
    private bool echoStrengthDone;
    private bool finaleFoolBroken;
    private bool finaleLaneClear;
    private bool finaleEnding;
    private GameObject finaleGate;
    private FinaleTarget finaleSeal;
    private FinaleTarget finaleCore;
    private BasicEnemy[] finaleLane;
    private BasicEnemy echoDummy;
    private BasicEnemy[] echoLane;
    private BasicEnemy[] echoCluster;
    private Text echoFoolMark;
    private Text echoChariotMark;
    private Text echoStrengthMark;
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
        bossLevel = GameFlow.ActiveLevelId == LevelCatalog.BossId;
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

        // 终局关：开场先让玩家选一张要带的卡（从标题/选关直接进来也一样）
        if (bossLevel && (loadout == null || loadout.EquippedCard == null))
            ShowCardChoice(true);
    }

    private void OnDestroy()
    {
        if (loadout != null)
            loadout.Changed -= RefreshObjective;
        if (boss != null)
            boss.Died -= OnBossDefeated;
        if (arena != null)
        {
            for (int i = 0; i < arena.Length; i++)
            {
                if (arena[i] != null)
                    arena[i].Died -= OnArenaEnemyDied;
            }
        }

        Unhook(echoDummy, OnEchoFoolDied);
        UnhookGroup(echoLane, OnEchoLaneDied);
        UnhookGroup(echoCluster, OnEchoClusterDied);
        UnhookGroup(finaleLane, OnFinaleLaneDied);
    }

    private static void Unhook(BasicEnemy enemy, System.Action handler)
    {
        if (enemy != null && handler != null)
            enemy.Died -= handler;
    }

    private static void UnhookGroup(BasicEnemy[] group, System.Action handler)
    {
        if (group == null || handler == null)
            return;

        for (int i = 0; i < group.Length; i++)
            Unhook(group[i], handler);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && !IsCardOpen)
            ReloadLevel();

        // 终局的结算面板不允许用 Esc 关掉：那是演示的终点，只能选按钮。
        if (!bossLevel && cleared && endPanel != null && endPanel.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
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

        if (bossLevel)
            BuildBossArena(ground);
        else if (foolTrial)
            BuildFoolTrial(ground);
        else if (chariotTrial)
            BuildEchoRoom(ground);
        else
            BuildBasicCombat(ground);
    }

    private void BuildBasicCombat(int ground)
    {
        const float floorTop = 0f;
        // 三个教学段之间留出明显的空档，跑动时能看清一段一段的节奏。
        Solid("Floor", new Vector2(18.8f, floorTop - 1f), new Vector2(40f, 2f), new Color(0.31f, 0.27f, 0.23f), ground, 0);
        Solid("LeftWall", new Vector2(-1.7f, 3.2f), new Vector2(0.7f, 6.4f), new Color(0.22f, 0.19f, 0.17f), ground, 0);
        Solid("RightWall", new Vector2(39f, 3.4f), new Vector2(0.7f, 6.8f), new Color(0.22f, 0.19f, 0.17f), ground, 0);

        // 两座薄平台。哨兵只在台面上巡逻，追击和受击也不会走下边缘。
        const float deckTop = 1.7f;
        const float deckThickness = 0.4f;
        float deckCenterY = deckTop - deckThickness * 0.5f;
        Solid("PlatformA", new Vector2(6.3f, deckCenterY), new Vector2(4.8f, deckThickness), new Color(0.46f, 0.38f, 0.29f), ground, 1);
        Solid("PlatformB", new Vector2(14.6f, deckCenterY), new Vector2(4.8f, deckThickness), new Color(0.46f, 0.38f, 0.29f), ground, 1);

        // 低台大约 0.62 高。跳跃攻击的刀光贴近身体，离开低台、面向右侧敌人时按鼠标左键能打到。
        const float lipTop = 0.62f;
        Solid("LowLip", new Vector2(21.6f, lipTop * 0.5f), new Vector2(3.6f, lipTop), new Color(0.43f, 0.36f, 0.28f), ground, 1);
        const float plinthTop = 0.28f;
        Solid("EnemyPlinth", new Vector2(25.6f, plinthTop * 0.5f), new Vector2(1.8f, plinthTop), new Color(0.34f, 0.28f, 0.24f), ground, 1);

        float feetLift = 0.92f;
        arena = new BasicEnemy[3];

        // ① 孢子怪：守在平台 A，玩家靠近就鼓起来，约 1.1 秒后原地大范围喷发。
        arena[0] = SpawnArenaEnemy("Spore", 6.3f, deckTop + feetLift, 3, "孢子怪",
            new Color(0.42f, 0.7f, 0.3f), new Vector3(1.15f, 1f, 1f), ground);
        arena[0].SetAsSpore();
        arena[0].StayOnPlatform(4.4f, 8.2f, 1 << ground);
        CreateSprite(arena[0].transform, "SporeCap", new Vector3(0f, 0.45f, 0f), new Vector3(0.85f, 0.32f, 1f), new Color(0.26f, 0.5f, 0.18f), "Player", 7);

        // ② 骷髅兵：发现玩家就追过来挥刀；这一关先锁在平台 B 上，不往下跳。
        //    （以后的大关卡想让它跳下来追，只要不调用 StayOnPlatform 就行。）
        arena[1] = SpawnArenaEnemy("Skeleton", 14.6f, deckTop + feetLift, 3, "骷髅兵",
            new Color(0.88f, 0.86f, 0.78f), new Vector3(0.85f, 1.15f, 1f), ground);
        arena[1].SetAsSkeleton();
        arena[1].StayOnPlatform(12.4f, 16.8f, 1 << ground);
        CreateSprite(arena[1].transform, "Ribcage", new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.55f, 1f), new Color(0.66f, 0.64f, 0.56f), "Player", 7);

        // ③ 石甲虫：甲壳很硬，横向普通攻击只会被弹开，只有从上方砸下来的下落攻击能破甲。
        arena[2] = SpawnArenaEnemy("Armored", 25.6f, plinthTop + feetLift, 3, "石甲虫",
            new Color(0.4f, 0.42f, 0.48f), new Vector3(1.25f, 0.9f, 1f), ground);
        arena[2].SetAsArmored();
        arena[2].StayOnPlatform(24.95f, 26.45f, 1 << ground);
        CreateSprite(arena[2].transform, "Shell", new Vector3(0f, 0.38f, 0f), new Vector3(1.15f, 0.34f, 1f), new Color(0.58f, 0.63f, 0.72f), "Player", 7);

        // 普通跳约 2.46，蹬墙跳在贴墙后再抬约 2.10。柱底 2.40 让人能走过去，台面 3.40 让普通跳和冲刺都上不去。
        // 柱子右侧到高台左侧留 2.85 的空档，必须靠左右蹬墙才能上去。
        basicSeal = Solid("ExitSeal", new Vector2(27.8f, 1.55f), new Vector2(0.55f, 3.1f), new Color(0.45f, 0.2f, 0.22f), ground, 3);
        Solid("ClimbPillar", new Vector2(29f, 4.25f), new Vector2(0.7f, 3.7f), new Color(0.62f, 0.66f, 0.72f), ground, 2);
        Solid("HighLedge", new Vector2(35.4f, 1.7f), new Vector2(6.4f, 3.4f), new Color(0.38f, 0.32f, 0.28f), ground, 1);
        Sign("WallJumpSign", root, new Vector3(30.8f, 1.45f, 0f), "蹬墙跳", 160f);
        Sign("ExitLabel", root, new Vector3(36.4f, 4.55f, 0f), "出口", 140f);
        CreateBasicExit(36.4f, 4.7f);
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

    // 终局：一间封闭的 Boss 房。进来之前已经在「牌之回响」里选好了要带的牌。
    private void BuildBossArena(int ground)
    {
        Solid("Floor", new Vector2(14f, -1f), new Vector2(30.4f, 2f), new Color(0.26f, 0.21f, 0.2f), ground, 0);
        Solid("LeftWall", new Vector2(-1.5f, 3.6f), new Vector2(0.8f, 7.2f), new Color(0.18f, 0.15f, 0.16f), ground, 0);
        Solid("RightWall", new Vector2(29.5f, 3.6f), new Vector2(0.8f, 7.2f), new Color(0.18f, 0.15f, 0.16f), ground, 0);

        // 三层台子：给蹬墙跳、空中攻击和躲预兆留出空间
        Solid("LedgeA", new Vector2(8.5f, 1.6f), new Vector2(4.4f, 0.4f), new Color(0.42f, 0.34f, 0.32f), ground, 1);
        Solid("LedgeB", new Vector2(18.5f, 2.6f), new Vector2(4f, 0.4f), new Color(0.42f, 0.34f, 0.32f), ground, 1);
        Solid("LedgeC", new Vector2(13.5f, 4.2f), new Vector2(3f, 0.4f), new Color(0.34f, 0.27f, 0.26f), ground, 1);

        // 场中央的暗红地板，提示这是 Boss 房
        CreateSprite(root, "ArenaMark", new Vector3(20.5f, 0.02f, 0f), new Vector3(12f, 0.05f, 1f), new Color(0.55f, 0.16f, 0.18f, 0.75f), "Ground", 3);

        CreateBoss(24.5f, 24);
        bossNameSign = Sign("BossLabel", root, new Vector3(24.5f, 4.9f, 0f), "裂隙守卫", 240f);
    }

    // 复用愚者试用那套裂隙守卫，参数按玩家当前的攻击距离反算，普通攻击也够得到。
    private void CreateBoss(float x, int health)
    {
        CapsuleCollider2D body = player.GetComponent<CapsuleCollider2D>();
        float playerHeight = body != null ? body.size.y * Mathf.Abs(player.transform.lossyScale.y) : 1.9f;
        float playerWidth = body != null ? body.size.x * Mathf.Abs(player.transform.lossyScale.x) : 0.77f;
        float normalReach = player.AttackFrontReach(1f);
        float stand = x - 1.7f;
        standCenter = stand;
        float slashMin = stand - 0.08f;

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
        enemyObject.transform.position = new Vector3(x + enemyHalf, 0.04f - enemyObject.transform.localScale.y * enemyBottom, 0f);
        enemySprite.sortingLayerName = "Ground";
        enemySprite.sortingOrder = 2;
        CreateSprite(enemyObject.transform, "Core", new Vector3(0f, 0.2f, 0f), new Vector3(0.34f, 0.42f, 1f), new Color(1f, 0.86f, 0.45f), "Ground", 3);
        CreateSprite(enemyObject.transform, "Aura", Vector3.zero, new Vector3(1.25f, 1.85f, 1f), new Color(0.9f, 0.24f, 0.18f, 0.33f), "Player", -8);
        CreateSprite(enemyObject.transform, "HornL", new Vector3(-0.28f, 0.62f, 0f), new Vector3(0.16f, 0.32f, 1f), new Color(0.55f, 0.08f, 0.08f), "Ground", 3);
        CreateSprite(enemyObject.transform, "HornR", new Vector3(0.28f, 0.62f, 0f), new Vector3(0.16f, 0.32f, 1f), new Color(0.55f, 0.08f, 0.08f), "Ground", 3);

        boss.Bind(player, health, "裂隙守卫", slashMin, stand + normalReach, slashMin - normalReach, 0f,
            playerHeight + 0.22f, playerHeight * 0.4f, Mathf.Max(1.15f, playerWidth * 2.2f));
        boss.Died += OnBossDefeated;

        // 终局 Boss：会在场地里横向巡走，攻击区跟着一起动；玩家走近就醒
        boss.SetMobile(x - 4.2f, x + 3.2f, 1.7f);
        boss.SetArenaPad(14f);
    }

    // 第一关的小怪：统一的生成方式，具体种类和外观由调用方设置。
    private BasicEnemy SpawnArenaEnemy(string objectName, float x, float y, int health, string displayName, Color color, Vector3 scale, int ground)
    {
        BasicEnemy enemy = SpawnEnemy(objectName, x, health, false, displayName, color, "Player", 6);
        enemy.transform.position = new Vector3(x, y, 0f);
        enemy.SetVisualScale(scale);
        enemy.SetBodyColor(color);
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

    private void BuildEchoRoom(int ground)
    {
        Solid("Floor", new Vector2(29.25f, -1f), new Vector2(61.5f, 2f), new Color(0.31f, 0.27f, 0.23f), ground, 0);
        Solid("LeftWall", new Vector2(-1.8f, 3f), new Vector2(0.7f, 6f), new Color(0.22f, 0.19f, 0.17f), ground, 0);
        Solid("RightWall", new Vector2(60.4f, 3f), new Vector2(0.7f, 6f), new Color(0.22f, 0.19f, 0.17f), ground, 0);
        Sign("HubSign", root, new Vector3(2.4f, 2.3f, 0f), "左边单牌，右边收束", 280f);

        MonsterCardData fool = Resources.Load<MonsterCardData>("Cards/Fool");
        MonsterCardData chariot = Resources.Load<MonsterCardData>("Cards/Chariot");
        MonsterCardData strength = Resources.Load<MonsterCardData>("Cards/Strength");
        if (fool == null || chariot == null || strength == null)
            Debug.LogWarning("牌之回响缺少愚者、战车或力量的卡牌资源。");

        echoFoolMark = PlaceSeal(new Vector3(6.2f, 1.15f, 0f), fool, "愚者", new Color(0.95f, 0.78f, 0.28f));
        BuildFoolStation(ground);
        echoChariotMark = PlaceSeal(new Vector3(16.2f, 1.15f, 0f), chariot, "战车", new Color(0.45f, 0.78f, 0.95f));
        BuildChariotStation(ground);
        echoStrengthMark = PlaceSeal(new Vector3(25.2f, 1.15f, 0f), strength, "力量", new Color(0.86f, 0.28f, 0.18f));
        BuildStrengthStation(ground);
        BuildFinale(ground, fool, chariot, strength);
        RefreshEchoMarks();
    }

    private Text PlaceSeal(Vector3 position, MonsterCardData card, string label, Color color)
    {
        GameObject seal = new GameObject(label + "Seal");
        seal.transform.SetParent(root, false);
        seal.transform.position = position;
        CreateSprite(seal.transform, "Stone", Vector3.zero, new Vector3(0.7f, 1.35f, 1f), color, "Player", 4);
        BoxCollider2D trigger = seal.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.4f, 1.8f);
        seal.AddComponent<CardSeal>().Arm(card);
        Sign("SealName", seal.transform, position + Vector3.up * 1.15f, label, 140f);
        return Sign("SealMark", seal.transform, position + Vector3.up * 1.7f, "未完成", 160f);
    }

    private void BuildFoolStation(int ground)
    {
        const float stand = 8.4f;
        Solid("FoolRail", new Vector2(9.7f, 0.85f), new Vector2(0.35f, 1.7f), new Color(0.55f, 0.58f, 0.66f), ground, 3);
        Sign("FoolHint", root, new Vector3(stand, 2.15f, 0f), "打过栏杆", 180f);
        echoDummy = SpawnEnemy("FoolTarget", stand + 2.45f, 4, true, "靶子", new Color(0.85f, 0.62f, 0.2f), "Player", 6);
        echoDummy.transform.position = new Vector3(stand + 2.45f, 0.92f, 0f);
        echoDummy.Died += OnEchoFoolDied;
    }

    private void BuildChariotStation(int ground)
    {
        Sign("ChariotHint", root, new Vector3(19.4f, 2.35f, 0f), "冲过去", 160f);
        echoLane = new BasicEnemy[3];
        float[] xs = { 18.3f, 19.6f, 20.9f };
        Color[] colors =
        {
            new Color(0.25f, 0.55f, 0.72f),
            new Color(0.2f, 0.42f, 0.68f),
            new Color(0.16f, 0.32f, 0.58f)
        };
        for (int i = 0; i < echoLane.Length; i++)
        {
            echoLane[i] = SpawnEnemy("EchoLane" + i, xs[i], 2, false, "列兵", colors[i], "Player", 6);
            echoLane[i].transform.position = new Vector3(xs[i], 0.92f, 0f);
            echoLane[i].StayOnPlatform(xs[i] - 0.35f, xs[i] + 0.35f, 1 << ground);
            echoLane[i].SetPushable();
            echoLane[i].Died += OnEchoLaneDied;
        }
    }

    private void BuildStrengthStation(int ground)
    {
        Solid("StrengthLip", new Vector2(27.6f, 0.6f), new Vector2(2.4f, 1.2f), new Color(0.55f, 0.24f, 0.18f), ground, 1);
        Sign("StrengthHint", root, new Vector3(27.6f, 2.15f, 0f), "坠地", 120f);
        echoCluster = new BasicEnemy[3];
        float[] xs = { 29.7f, 30.5f, 31.3f };
        for (int i = 0; i < echoCluster.Length; i++)
        {
            echoCluster[i] = SpawnEnemy("EchoSlam" + i, xs[i], 2, true, "矮靶", new Color(0.72f, 0.22f, 0.16f), "Player", 6);
            echoCluster[i].transform.position = new Vector3(xs[i], 0.92f, 0f);
            echoCluster[i].Died += OnEchoClusterDied;
        }

        GameObject pot = new GameObject("BreakablePot");
        pot.transform.SetParent(root, false);
        pot.transform.position = new Vector3(30.4f, 0.45f, 0f);
        pot.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
        SpriteRenderer sprite = pot.AddComponent<SpriteRenderer>();
        sprite.sprite = PixelSprite();
        sprite.color = new Color(0.62f, 0.28f, 0.16f, 1f);
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = 4;
        BoxCollider2D box = pot.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
        pot.AddComponent<BreakableProp>().Arm(1);
    }

    private void BuildFinale(int ground, MonsterCardData fool, MonsterCardData chariot, MonsterCardData strength)
    {
        PlaceSeal(new Vector3(34.2f, 1.15f, 0f), fool, "换愚者", new Color(0.95f, 0.78f, 0.28f));
        const float stand = 36.2f;
        Solid("FinaleRail", new Vector2(37.35f, 0.9f), new Vector2(0.35f, 1.8f), new Color(0.55f, 0.58f, 0.66f), ground, 3);
        Sign("FinaleFoolHint", root, new Vector3(stand, 2.2f, 0f), "打第一印", 180f);
        finaleSeal = CreateFinaleTarget("FirstSeal", new Vector3(stand + 2.55f, 1.9f, 0f), new Vector3(0.7f, 1.4f, 1f), new Color(0.95f, 0.78f, 0.28f, 1f), 2, OnFinaleSealBroken);
        Sign("FirstSealLabel", root, new Vector3(stand + 2.55f, 2.9f, 0f), "第一印", 140f);

        finaleGate = Solid("FinaleGate", new Vector2(40.3f, 1.6f), new Vector2(0.55f, 3.2f), new Color(0.45f, 0.36f, 0.18f), ground, 4);
        PlaceSeal(new Vector3(42.2f, 1.15f, 0f), chariot, "换战车", new Color(0.45f, 0.78f, 0.95f));
        Sign("FinaleDashHint", root, new Vector3(44.4f, 2.35f, 0f), "冲到核心", 180f);
        Solid("FinaleLintel", new Vector2(44.6f, 2.45f), new Vector2(4.2f, 0.35f), new Color(0.22f, 0.2f, 0.24f), ground, 2);
        finaleLane = new BasicEnemy[3];
        float[] xs = { 43.4f, 44.6f, 45.8f };
        for (int i = 0; i < finaleLane.Length; i++)
        {
            finaleLane[i] = SpawnEnemy("FinaleLane" + i, xs[i], 2, false, "阻路", new Color(0.35f, 0.62f, 0.78f), "Player", 6);
            finaleLane[i].transform.position = new Vector3(xs[i], 0.92f, 0f);
            finaleLane[i].StayOnPlatform(xs[i] - 0.3f, xs[i] + 0.3f, 1 << ground);
            finaleLane[i].SetPushable();
            finaleLane[i].Died += OnFinaleLaneDied;
        }

        PlaceSeal(new Vector3(48.2f, 1.15f, 0f), strength, "换力量", new Color(0.86f, 0.28f, 0.18f));
        Solid("FinaleLip", new Vector2(50.1f, 0.6f), new Vector2(2.2f, 1.2f), new Color(0.62f, 0.22f, 0.16f), ground, 1);
        Sign("FinaleSlamHint", root, new Vector3(50.1f, 2.15f, 0f), "砸核心", 140f);
        finaleCore = CreateFinaleTarget("RiftCore", new Vector3(52.15f, 1.05f, 0f), new Vector3(1.15f, 1.7f, 1f), new Color(0.72f, 0.55f, 0.95f, 1f), 3, OnFinaleCoreBroken);
        CreateSprite(finaleCore.transform, "CoreHeart", new Vector3(0f, 0.05f, 0f), new Vector3(0.38f, 0.48f, 1f), new Color(1f, 0.9f, 0.7f, 1f), "Player", 8);
    }

    private FinaleTarget CreateFinaleTarget(string name, Vector3 position, Vector3 scale, Color color, int health, UnityEngine.Events.UnityAction onBroken)
    {
        GameObject target = new GameObject(name);
        target.transform.SetParent(root, false);
        target.transform.position = position;
        target.transform.localScale = scale;
        SpriteRenderer sprite = target.AddComponent<SpriteRenderer>();
        sprite.sprite = PixelSprite();
        sprite.color = color;
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = 6;
        BoxCollider2D box = target.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
        FinaleTarget finale = target.AddComponent<FinaleTarget>();
        finale.Arm(health, onBroken);
        return finale;
    }

    private void OnFinaleSealBroken()
    {
        finaleFoolBroken = true;
        if (finaleGate != null)
        {
            Collider2D[] blocking = finaleGate.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < blocking.Length; i++)
                blocking[i].enabled = false;
            AttackFeedback.Thud(finaleGate.transform.position);
            StartCoroutine(SinkObject(finaleGate, 3.4f));
        }

        RefreshObjective();
    }

    private void OnFinaleLaneDied()
    {
        if (!GroupDead(finaleLane))
            return;

        finaleLaneClear = true;
        RefreshObjective();
    }

    private void OnFinaleCoreBroken()
    {
        if (finaleEnding || cleared)
            return;

        finaleEnding = true;
        StartCoroutine(PlayCoreEcho());
    }

    private IEnumerator SinkObject(GameObject block, float distance)
    {
        if (block == null)
            yield break;

        Vector3 start = block.transform.position;
        Vector3 end = start + Vector3.down * distance;
        float elapsed = 0f;
        while (elapsed < 0.4f)
        {
            elapsed += Time.deltaTime;
            block.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / 0.4f));
            yield return null;
        }

        block.SetActive(false);
    }

    private IEnumerator PlayCoreEcho()
    {
        SetObjective("三张牌同时留下了痕迹");
        Vector3 point = finaleCore != null ? finaleCore.transform.position : new Vector3(52.15f, 1.05f, 0f);
        yield return ShowEchoBeat(point, new Color(1f, 0.82f, 0.28f, 0.9f), "愚者拉开距离");
        yield return ShowEchoBeat(point, new Color(0.45f, 0.82f, 1f, 0.9f), "战车冲到核心");
        yield return ShowEchoBeat(point, new Color(1f, 0.35f, 0.18f, 0.9f), "力量震碎核心");
        if (finaleCore != null)
            finaleCore.gameObject.SetActive(false);
        Complete();
    }

    // 选牌界面。applyHere = true 时把牌直接给当前场景的玩家（终局关开场用），
    // 否则带着选好的牌加载终局关。
    private void ShowCardChoice(bool applyHere)
    {
        if (bossChoiceOpen || cleared)
            return;

        bossChoiceOpen = true;
        SetObjective(applyHere ? "选一张开场装备的卡牌" : "选一张牌，带着它走进裂隙深处");
        Time.timeScale = 0f;
        if (player != null)
            player.SetGameplayInputEnabled(false);
        if (!applyHere && cardUi != null)
            cardUi.HoldForClear();

        MenuKit.EnsureEventSystem();

        GameObject canvasObject = new GameObject("BossCardChoice");
        cardChoiceRoot = canvasObject;
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 140;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();
        Image dim = canvasObject.AddComponent<Image>();
        dim.color = new Color(0.05f, 0.04f, 0.07f, 0.94f);
        dim.rectTransform.anchorMin = Vector2.zero;
        dim.rectTransform.anchorMax = Vector2.one;
        dim.rectTransform.offsetMin = Vector2.zero;
        dim.rectTransform.offsetMax = Vector2.zero;

        Text title = CreateUiText(canvasObject.transform, "Title", 46, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.55f));
        title.text = "选一张牌，走进裂隙深处";
        PlaceCenter(title.rectTransform, new Vector2(0f, 330f), new Vector2(1100f, 64f));

        Text hint = CreateUiText(canvasObject.transform, "Hint", 24, TextAnchor.MiddleCenter, new Color(0.85f, 0.82f, 0.75f));
        hint.text = applyHere
            ? "三张牌都会进背包，这里选的是开场装备的那张；打起来后按 C 还能换。"
            : "三张牌都会带进 Boss 战，这里选的是开场装备的那张。";
        PlaceCenter(hint.rectTransform, new Vector2(0f, 268f), new Vector2(1400f, 40f));

        string[] paths = { "Cards/Fool", "Cards/Chariot", "Cards/Strength" };
        float[] xs = { -560f, 0f, 560f };
        Color[] tints =
        {
            new Color(0.95f, 0.78f, 0.28f),
            new Color(0.45f, 0.78f, 0.95f),
            new Color(0.86f, 0.28f, 0.18f)
        };

        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            MonsterCardData card = Resources.Load<MonsterCardData>(path);
            string cardName = card != null && !string.IsNullOrEmpty(card.displayName) ? card.displayName : "牌";
            string cardDesc = card != null ? card.description : "";
            float x = xs[i];

            Image frame = CreateUiImage(canvasObject.transform, "Card" + i, new Color(0.12f, 0.1f, 0.15f, 0.96f));
            PlaceCenter(frame.rectTransform, new Vector2(x, -60f), new Vector2(460f, 380f));

            Image stripe = CreateUiImage(frame.transform, "Stripe", tints[i]);
            stripe.rectTransform.anchorMin = new Vector2(0f, 1f);
            stripe.rectTransform.anchorMax = new Vector2(1f, 1f);
            stripe.rectTransform.pivot = new Vector2(0.5f, 1f);
            stripe.rectTransform.anchoredPosition = Vector2.zero;
            stripe.rectTransform.sizeDelta = new Vector2(0f, 6f);

            Text nameText = CreateUiText(frame.transform, "Name", 38, TextAnchor.MiddleCenter, tints[i]);
            nameText.text = cardName;
            PlaceCenter(nameText.rectTransform, new Vector2(0f, 110f), new Vector2(420f, 60f));

            Text descText = CreateUiText(frame.transform, "Desc", 22, TextAnchor.UpperCenter, new Color(0.9f, 0.88f, 0.84f));
            descText.text = cardDesc;
            PlaceCenter(descText.rectTransform, new Vector2(0f, 10f), new Vector2(400f, 140f));

            CreateChoice(frame.transform, "带这张", new Vector2(0f, -150f), () => ChooseCard(path, applyHere), 300f);
        }
    }

    private void ChooseCard(string path, bool applyHere)
    {
        if (!applyHere)
        {
            GameFlow.EnterBossWithCard(path);
            return;
        }

        if (loadout != null)
        {
            loadout.GrantAllCards();
            loadout.EquipFromPath(path);
        }

        if (cardChoiceRoot != null)
            Destroy(cardChoiceRoot);
        cardChoiceRoot = null;
        bossChoiceOpen = false;
        Time.timeScale = 1f;
        if (player != null)
            player.SetGameplayInputEnabled(true);
        RefreshObjective();
    }

    private static void PlaceCenter(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private IEnumerator ShowEchoBeat(Vector3 point, Color color, string caption)
    {
        GameObject beat = new GameObject("CoreEcho");
        beat.transform.SetParent(root, false);
        beat.transform.position = point;
        beat.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
        SpriteRenderer sprite = beat.AddComponent<SpriteRenderer>();
        sprite.sprite = PixelSprite();
        sprite.color = color;
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = 20;
        Text label = Sign("EchoCaption", beat.transform, point + Vector3.up * 1.4f, caption, 280f);
        label.color = color;
        float elapsed = 0f;
        while (elapsed < 0.55f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.55f);
            beat.transform.localScale = Vector3.Lerp(new Vector3(0.4f, 0.4f, 1f), new Vector3(2.4f, 2.4f, 1f), t);
            Color faded = color;
            faded.a = 0.9f * (1f - t);
            sprite.color = faded;
            yield return null;
        }

        Destroy(beat);
    }

    private void OnEchoFoolDied()
    {
        echoFoolDone = true;
        RefreshEchoMarks();
    }

    private void OnEchoLaneDied()
    {
        if (!GroupDead(echoLane))
            return;

        echoChariotDone = true;
        RefreshEchoMarks();
    }

    private void OnEchoClusterDied()
    {
        if (!GroupDead(echoCluster))
            return;

        echoStrengthDone = true;
        RefreshEchoMarks();
    }

    private static bool GroupDead(BasicEnemy[] group)
    {
        if (group == null)
            return false;

        for (int i = 0; i < group.Length; i++)
        {
            if (group[i] != null && !group[i].IsDead)
                return false;
        }

        return true;
    }

    private void RefreshEchoMarks()
    {
        PaintMark(echoFoolMark, echoFoolDone);
        PaintMark(echoChariotMark, echoChariotDone);
        PaintMark(echoStrengthMark, echoStrengthDone);
    }

    private static void PaintMark(Text mark, bool done)
    {
        if (mark == null)
            return;

        mark.text = done ? "完成" : "未完成";
        mark.color = done ? new Color(1f, 0.86f, 0.35f, 1f) : new Color(0.75f, 0.75f, 0.75f, 1f);
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
        title.text = chariotTrial ? "牌之回响" : foolTrial ? "愚者牌试用" : "第一关  基础战斗";
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

        if (bossLevel)
        {
            RefreshBossObjective();
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
            SetObjective("WASD 移动，空格跳跃，左 Shift 冲刺，H 回血");
            return;
        }

        SetObjective("往前拾取愚者");
    }

    private void RefreshBossObjective()
    {
        if (cleared)
        {
            SetObjective("裂隙闭合了");
            return;
        }

        if (boss != null && !boss.IsDead)
        {
            if (boss.HasAttacked)
                SetObjective("");
            else if (boss.IsTelegraphing)
                SetObjective("看地面预兆再躲开");
            else
                SetObjective("击破裂隙守卫");
            return;
        }

        SetObjective("守卫倒下了");
    }

    private void RefreshBasicObjective()
    {
        float x = player != null ? player.transform.position.x : 0f;
        if (basicExitOpen && x > 33f)
        {
            SetObjective("走进出口，进入愚者牌试用");
            return;
        }

        if (basicExitOpen)
        {
            SetObjective("贴着柱子按住 A 往下滑，空格蹬墙，越过缺口后松开 A");
            return;
        }

        if (x < 3f)
        {
            SetObjective("WASD 移动，空格跳跃，左 Shift 冲刺，H 回血");
            return;
        }

        if (ArenaAlive(2) && x > 19f)
        {
            SetObjective("石甲虫的甲壳砍不动：跳起来从上方砸下去（下落攻击）");
            return;
        }

        if (ArenaAlive(1) && x > 9.5f)
        {
            SetObjective("骷髅兵会主动冲过来，等它抬手时冲刺躲开");
            return;
        }

        if (ArenaAlive(0) && x > 3f)
        {
            SetObjective("孢子怪会先鼓起来再喷发，看到鼓包就往后退");
            return;
        }

        SetObjective("三个敌人都倒下后，再去蹬墙跳");
    }

    private void RefreshChariotObjective()
    {
        if (cleared || finaleEnding)
        {
            SetObjective("三张牌一起合上了核心");
            return;
        }

        float x = player != null ? player.transform.position.x : 0f;
        if (x > 33f || finaleFoolBroken)
        {
            if (!finaleFoolBroken)
                SetObjective("换上愚者，鼠标左键打过栏杆，打断第一印");
            else if (!finaleLaneClear)
                SetObjective("换上战车，左 Shift 冲刺穿过，靠近核心");
            else
                SetObjective("换上力量，跳起后鼠标左键，砸碎核心");
            return;
        }

        if (loadout == null || loadout.EquippedCard == null)
        {
            SetObjective("左边试单牌。往右按印记换牌，收束核心");
            return;
        }

        switch (loadout.EquippedCard.effectType)
        {
            case CardEffectType.FoolLunge:
                SetObjective(echoFoolDone ? "愚者已完成。往右打第一印" : "站上栏杆前，鼠标左键打过栏杆");
                return;
            case CardEffectType.ChariotDash:
                SetObjective(echoChariotDone ? "战车已完成。往右冲向核心" : "左 Shift 冲刺穿过列兵");
                return;
            case CardEffectType.StrengthSlam:
                SetObjective(echoStrengthDone ? "力量已完成。往右砸碎核心" : "从台上跳起，鼠标左键坠地");
                return;
        }

        SetObjective("触碰印记换牌");
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
        if (bossLevel)
        {
            if (boss != null)
                boss.SetFoolReady(true);
            RefreshObjective();
            return;
        }

        if (chariotTrial)
        {
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
        endRect.sizeDelta = new Vector2(640f, 560f);
        Sprite panelSkin = UiArt.Load(UiArt.Panel);
        if (panelSkin != null)
        {
            endImage.sprite = panelSkin;
            endImage.color = Color.white;
        }

        Text endTitle = CreateUiText(endPanel.transform, "Title", 40, TextAnchor.UpperCenter, new Color(1f, 0.88f, 0.55f));
        endTitle.text = bossLevel ? "裂隙闭合"
            : chariotTrial ? "牌之回响完成"
            : foolTrial ? "愚者牌试用完成"
            : "第一关 完成";
        StretchTop(endTitle.rectTransform, -28f, 56f);

        Text ending = CreateUiText(endPanel.transform, "Ending", 26, TextAnchor.UpperCenter, new Color(1f, 0.94f, 0.82f));
        ending.text = bossLevel ? FinaleLine
            : chariotTrial ? "三张牌都在她身上留下了不同的动作。"
            : foolTrial ? ChapterLine
            : "哨兵清完了，栏杆后面的路开了。";
        StretchTop(ending.rectTransform, -96f, 110f);

        if (bossLevel)
        {
            // 演示的终点：只留「再来一次 / 回标题 / 选关」，不再有「继续」。
            CreateChoice(endPanel.transform, "再战一次", new Vector2(0f, 20f), ReplayLevel);
            CreateChoice(endPanel.transform, "返回标题", new Vector2(0f, -70f), GameFlow.LoadTitle);
            CreateChoice(endPanel.transform, "选择关卡", new Vector2(0f, -160f), GameFlow.LoadLevelSelect);
        }
        else
        {
            // 主线顺序：第一关 → 愚者牌试用 → 牌之回响 → 终局
            if (chariotTrial)
                CreateChoice(endPanel.transform, "继续：终局", new Vector2(0f, 70f), () => GameFlow.LoadBossLevel(), 420f);
            else if (foolTrial)
                CreateChoice(endPanel.transform, "继续：牌之回响", new Vector2(0f, 70f), () => GameFlow.LoadChariotTrial(), 420f);
            else
                CreateChoice(endPanel.transform, "继续：愚者牌试用", new Vector2(0f, 70f), () => GameFlow.LoadFoolTrial(), 420f);

            CreateChoice(endPanel.transform, "重玩本关", new Vector2(0f, -16f), ReplayLevel);
            CreateChoice(endPanel.transform, "选择关卡", new Vector2(0f, -102f), GameFlow.LoadLevelSelect);
            CreateChoice(endPanel.transform, "返回标题", new Vector2(0f, -188f), GameFlow.LoadTitle);
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
            fadeText.text = bossLevel ? FinaleLine : ChapterLine;
        yield return Fade(0f, 0.8f, 0.4f);
        yield return new WaitForSeconds(0.7f);
        Complete();
    }

    private void CreateChoice(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick, float width = 360f)
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
        rect.sizeDelta = new Vector2(width, 64f);
        rect.anchoredPosition = position;
        Text text = CreateUiText(buttonObject.transform, "Label", 26, TextAnchor.MiddleCenter, Color.white);
        text.text = label;
        text.rectTransform.sizeDelta = rect.sizeDelta;
    }

    public bool UseExit()
    {
        if (!foolTrial && !basicExitOpen)
            return false;

        // 每一关的出口都回到结算面板，由面板上的「继续」推进到下一关，
        // 不再从第一关直接硬切到愚者试用。
        Complete();
        return true;
    }

    private bool FoolEquipped =>
        loadout != null &&
        loadout.EquippedCard != null &&
        loadout.EquippedCard.effectType == CardEffectType.FoolLunge;

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

public class FinaleTarget : MonoBehaviour, IDamageable
{
    private int health = 1;
    private bool broken;
    private UnityEngine.Events.UnityAction brokenCallback;

    public void Arm(int amount, UnityEngine.Events.UnityAction onBroken)
    {
        health = Mathf.Max(1, amount);
        brokenCallback = onBroken;
    }

    public void TakeDamage(int amount, Vector2 hitFrom)
    {
        if (broken || amount <= 0)
            return;

        health -= amount;
        if (health > 0)
            return;

        broken = true;
        Collider2D body = GetComponent<Collider2D>();
        if (body != null)
            body.enabled = false;
        AttackFeedback.Thud(transform.position);
        if (brokenCallback != null)
            brokenCallback.Invoke();
    }
}

public class CardSeal : MonoBehaviour
{
    private MonsterCardData card;

    public void Arm(MonsterCardData data)
    {
        card = data;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Player player = other.GetComponent<Player>();
        if (player == null || card == null)
            return;

        PlayerCardLoadout loadout = player.GetComponent<PlayerCardLoadout>();
        if (loadout == null)
            return;

        loadout.Equip(card);
    }
}
