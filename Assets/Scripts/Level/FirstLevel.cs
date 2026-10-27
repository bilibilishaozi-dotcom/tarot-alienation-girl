using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FirstLevel : MonoBehaviour
{
    private const float FloorTop = 0f;
    private const float GrateLeft = 24.2f;
    private const float GrateHeight = 3.8f;

    private Player player;
    private PlayerCardLoadout loadout;
    private Transform root;
    private GameObject gate;
    private BasicEnemy sentinel;
    private Text objectiveText;
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
    private float normalFront;
    private float extendedFront;
    private bool gateOpen;
    private bool cleared;
    private bool busy;

    private static Sprite pixel;

    private void Awake()
    {
        Time.timeScale = 1f;
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
        if (sentinel != null)
            sentinel.Died -= OpenGate;
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

        Solid("Floor", new Vector2(16.25f, -1f), new Vector2(36.5f, 2f), new Color(0.31f, 0.27f, 0.23f), ground, 0);
        Solid("LeftWall", new Vector2(-2.35f, 3.75f), new Vector2(0.7f, 7.5f), new Color(0.22f, 0.19f, 0.17f), ground, 0);
        Solid("RightWall", new Vector2(34.15f, 3f), new Vector2(0.7f, 6f), new Color(0.22f, 0.19f, 0.17f), ground, 0);

        Solid("Step", new Vector2(3.05f, 0.725f), new Vector2(2.9f, 1.45f), new Color(0.43f, 0.36f, 0.28f), ground, 1);
        Solid("CardPlatform", new Vector2(7.55f, 1.45f), new Vector2(6.5f, 2.9f), new Color(0.46f, 0.38f, 0.29f), ground, 1);

        Solid("PenLeft", new Vector2(12.5f, 0.45f), new Vector2(0.45f, 0.9f), new Color(0.26f, 0.22f, 0.2f), ground, 1);
        Solid("PenRight", new Vector2(17.2f, 0.45f), new Vector2(0.45f, 0.9f), new Color(0.26f, 0.22f, 0.2f), ground, 1);
        Solid("Arch", new Vector2(20.4f, 5.5f), new Vector2(0.75f, 3.8f), new Color(0.2f, 0.17f, 0.16f), ground, 1);

        MonsterCardData fool = Resources.Load<MonsterCardData>("Cards/Fool");
        if (fool == null)
            Debug.LogWarning("没有找到 Resources/Cards/Fool，第一关无法获得愚者牌。");
        CreateCard(new Vector3(9.2f, 4.15f, 0f), fool);
        BasicEnemy spore = SpawnEnemy("PracticeSpore", 14.8f, 3, false, "孢子", new Color(0.62f, 0.28f, 0.78f), "Player", 5);
        spore.SetAsPractice();
        spore.SetVisualScale(new Vector3(0.78f, 0.78f, 1f));
        BuildReachGate(ground, fool);
        CreateExit();
        BuildGuideArrow();
    }

    private void BuildReachGate(int ground, MonsterCardData fool)
    {
        CapsuleCollider2D body = player.GetComponent<CapsuleCollider2D>();
        float halfBody = body.size.x * 0.5f;
        standCenter = GrateLeft - halfBody - 0.06f;
        float foolReach = fool != null ? fool.reachMultiplier : 2.15f;
        normalFront = standCenter + player.AttackFrontReach(1f);
        extendedFront = standCenter + player.AttackFrontReach(foolReach);
        float enemyLeft = Mathf.Lerp(normalFront, extendedFront, 0.38f);

        GameObject enemyObject = new GameObject("Sentinel");
        enemyObject.transform.SetParent(root, false);
        enemyObject.transform.localScale = new Vector3(1.2f, 1.35f, 1f);
        enemyObject.AddComponent<SpriteRenderer>();
        enemyObject.AddComponent<Rigidbody2D>();
        CapsuleCollider2D capsule = enemyObject.AddComponent<CapsuleCollider2D>();
        sentinel = enemyObject.AddComponent<BasicEnemy>();
        sentinel.RootAt(player, 4, "裂隙守卫");
        sentinel.SetBodyColor(new Color(0.9f, 0.24f, 0.18f));
        sentinel.Died += OpenGate;
        float enemyHalf = capsule.size.x * 0.5f * Mathf.Abs(enemyObject.transform.lossyScale.x);
        float enemyBottom = capsule.offset.y - capsule.size.y * 0.5f;
        float enemyCenter = enemyLeft + enemyHalf;
        enemyObject.transform.position = new Vector3(enemyCenter, 0.04f - enemyObject.transform.localScale.y * enemyBottom, 0f);
        SpriteRenderer enemySprite = enemyObject.GetComponent<SpriteRenderer>();
        enemySprite.sortingLayerName = "Ground";
        enemySprite.sortingOrder = 2;
        CreateSprite(enemyObject.transform, "Core", new Vector3(0f, 0.2f, 0f), new Vector3(0.34f, 0.42f, 1f), new Color(1f, 0.86f, 0.45f), "Ground", 3);

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

        Sign("SentinelLabel", root, new Vector3(enemyCenter, 4.25f, 0f), "裂隙守卫", 240f);
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
        GameObject pickup = new GameObject("FoolCardPickup");
        pickup.transform.SetParent(root, false);
        pickup.transform.position = position;
        CreateSprite(pickup.transform, "Glow", Vector3.zero, new Vector3(1.55f, 1.85f, 1f), new Color(1f, 0.82f, 0.35f, 0.28f), "Player", 5);
        CreateSprite(pickup.transform, "Face", Vector3.zero, new Vector3(0.72f, 1.08f, 1f), new Color(0.95f, 0.76f, 0.28f), "Player", 6);
        CreateSprite(pickup.transform, "Mark", new Vector3(0f, 0.06f, 0f), new Vector3(0.36f, 0.48f, 1f), new Color(0.42f, 0.16f, 0.58f), "Player", 7);

        BoxCollider2D trigger = pickup.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.5f, 1.9f);
        pickup.AddComponent<CardPickup>().Arm(card);
        Sign("CardLabel", pickup.transform, position + Vector3.up * 1.15f, "愚者", 160f);
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
            float bottom = body.offset.y - body.size.y * 0.5f;
            spawnPoint = new Vector3(0.4f, FloorTop - bottom + 0.06f, 0f);
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
        title.text = "第一关  愚者的裂隙";
        StretchTop(title.rectTransform, -8f, 40f);

        objectiveText = CreateUiText(bar.transform, "Objective", 26, TextAnchor.UpperCenter, Color.white);
        StretchTop(objectiveText.rectTransform, -46f, 40f);
    }

    private void RefreshObjective()
    {
        if (objectiveText == null)
            return;

        if (cleared)
            objectiveText.text = "第一关完成";
        else if (gateOpen)
            objectiveText.text = "";
        else if (FoolEquipped)
            objectiveText.text = "已装备愚者";
        else if (loadout != null && loadout.PendingCard != null)
            objectiveText.text = "按 J 或回车装备愚者";
        else
            objectiveText.text = "去高台拿牌";
    }

    private void UpdateLesson()
    {
        bool fool = FoolEquipped;
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
        bool show = gateOpen && !cleared && Mathf.Abs(distance) > 2.4f;
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

        fadeText = CreateUiText(canvasObject.transform, "FadeText", 42, TextAnchor.MiddleCenter, new Color(1f, 0.94f, 0.8f, 0f));
        RectTransform fadeTextRect = fadeText.rectTransform;
        fadeTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        fadeTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        fadeTextRect.sizeDelta = new Vector2(800f, 80f);

        endPanel = new GameObject("ClearPanel");
        endPanel.transform.SetParent(canvasObject.transform, false);
        Image endImage = endPanel.AddComponent<Image>();
        endImage.color = new Color(0.07f, 0.05f, 0.09f, 0.94f);
        RectTransform endRect = endPanel.GetComponent<RectTransform>();
        endRect.sizeDelta = new Vector2(560f, 460f);
        Sprite panelSkin = UiArt.Load(UiArt.Panel);
        if (panelSkin != null)
        {
            endImage.sprite = panelSkin;
            endImage.color = Color.white;
        }

        Text endTitle = CreateUiText(endPanel.transform, "Title", 40, TextAnchor.UpperCenter, new Color(1f, 0.88f, 0.55f));
        endTitle.text = "第一关完成";
        StretchTop(endTitle.rectTransform, -28f, 56f);

        CreateChoice(endPanel.transform, "继续试玩当前关卡", new Vector2(0f, 30f), ContinueLevel);
        CreateChoice(endPanel.transform, "选择关卡", new Vector2(0f, -60f), GameFlow.LoadLevelSelect);
        CreateChoice(endPanel.transform, "返回标题", new Vector2(0f, -150f), GameFlow.LoadTitle);
        endPanel.SetActive(false);
    }

    private void ContinueLevel()
    {
        if (endPanel == null || !endPanel.activeSelf)
            return;

        endPanel.SetActive(false);
        if (cardUi != null)
            cardUi.ReleaseFromClear();
        if (!IsCardOpen)
            Time.timeScale = 1f;
        if (player != null && !player.IsDefeated && !IsCardOpen)
            player.SetGameplayInputEnabled(true);
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

        used = true;
        level.Complete();
    }
}
