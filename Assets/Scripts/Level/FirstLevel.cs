using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 关卡入口：按关卡 id 选择基础战斗、愚者试用或牌之回响，并负责共用的界面和几何工具。
public partial class FirstLevel : MonoBehaviour
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
            BuildEchoRoom(ground);
        else
            BuildBasicCombat(ground);
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
        endTitle.text = chariotTrial ? "牌之回响完成" : "愚者牌试用完成";
        StretchTop(endTitle.rectTransform, -28f, 56f);

        Text ending = CreateUiText(endPanel.transform, "Ending", 26, TextAnchor.UpperCenter, new Color(1f, 0.94f, 0.82f));
        ending.text = chariotTrial ? "三张牌都在她身上留下了不同的动作。" : ChapterLine;
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
            {
                CreateChoice(endPanel.transform, "额外试炼：牌之回响", new Vector2(0f, -274f), GameFlow.LoadChariotTrial, 460f);
                Text echoNote = CreateUiText(endPanel.transform, "EchoNote", 22, TextAnchor.MiddleCenter, new Color(0.9f, 0.86f, 0.75f, 1f));
                echoNote.text = "约 1 分钟，切换体验三张牌";
                echoNote.rectTransform.anchoredPosition = new Vector2(0f, -332f);
                echoNote.rectTransform.sizeDelta = new Vector2(520f, 36f);
            }
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
        sprite.sprite = RuntimeSprite.Pixel;
        sprite.color = color;
        sprite.sortingLayerName = "Ground";
        sprite.sortingOrder = sortingOrder;
        BoxCollider2D box = block.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
        return block;
    }

    private static SpriteRenderer CreateSprite(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, string sortingLayer, int sortingOrder)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localScale = localScale;
        SpriteRenderer sprite = obj.AddComponent<SpriteRenderer>();
        sprite.sprite = RuntimeSprite.Pixel;
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

    private IEnumerator SinkDown(GameObject block, float distance, float duration)
    {
        if (block == null)
            yield break;

        Vector3 start = block.transform.position;
        Vector3 end = start + Vector3.down * distance;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            block.transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        block.SetActive(false);
    }
}
