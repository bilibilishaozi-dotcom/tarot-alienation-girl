using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CombatHud : MonoBehaviour
{
    private const float DamagePopupDuration = 0.65f;
    private const float HitShakeDuration = 0.28f;

    private readonly List<EnemyHealthView> enemyViews = new List<EnemyHealthView>();

    private Player player;
    private PlayerCardLoadout loadout;
    private GameObject canvasObject;
    private RectTransform canvasRect;
    private RectTransform playerBar;
    private RectTransform playerFill;
    private Image playerFillImage;
    private Text playerHealthText;
    private Text cardNameText;
    private Image cardIcon;
    private Text defeatText;
    private Image damageFlash;
    private Text damagePopup;
    private Vector2 playerBarRest;
    private Vector2 damagePopupStart;
    private float flashAlpha;
    private float shakeTimer;
    private float damagePopupTimer;
    private bool built;

    private static Sprite whiteSprite;

    private sealed class EnemyHealthView
    {
        public BasicEnemy Enemy;
        public Action Handler;
        public RectTransform Root;
        public RectTransform Fill;
        public Text Label;
    }

    private void Awake()
    {
        player = GetComponent<Player>();
        loadout = GetComponent<PlayerCardLoadout>();
        Build();
    }

    private void OnEnable()
    {
        if (player != null)
        {
            player.HealthChanged += RefreshPlayerHealth;
            player.Damaged += ShowPlayerHit;
        }

        if (loadout != null)
            loadout.Changed += RefreshCard;

        BasicEnemy.Spawned += TrackEnemy;
        BasicEnemy.Removed += UntrackEnemy;
        RefreshPlayerHealth();
        RefreshCard();
    }

    private void Start()
    {
        BasicEnemy[] existing = FindObjectsByType<BasicEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
            TrackEnemy(existing[i]);
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.HealthChanged -= RefreshPlayerHealth;
            player.Damaged -= ShowPlayerHit;
        }

        if (loadout != null)
            loadout.Changed -= RefreshCard;

        BasicEnemy.Spawned -= TrackEnemy;
        BasicEnemy.Removed -= UntrackEnemy;

        for (int i = enemyViews.Count - 1; i >= 0; i--)
        {
            EnemyHealthView view = enemyViews[i];
            if (view.Enemy != null && view.Handler != null)
                view.Enemy.HealthChanged -= view.Handler;
            if (view.Root != null)
                Destroy(view.Root.gameObject);
            enemyViews.RemoveAt(i);
        }
    }

    private void OnDestroy()
    {
        if (canvasObject != null)
            Destroy(canvasObject);
    }

    private void Update()
    {
        if (player != null && player.IsDefeated && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        if (flashAlpha > 0f)
        {
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.deltaTime * 1.5f);
            SetAlpha(damageFlash, flashAlpha);
        }

        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            float magnitude = Mathf.Clamp01(shakeTimer / HitShakeDuration) * 7f;
            playerBar.anchoredPosition = playerBarRest + new Vector2(Mathf.Sin(Time.time * 48f) * magnitude, 0f);
        }
        else if (playerBar.anchoredPosition != playerBarRest)
        {
            playerBar.anchoredPosition = playerBarRest;
        }

        if (damagePopupTimer <= 0f)
            return;

        damagePopupTimer -= Time.deltaTime;
        float t = 1f - Mathf.Clamp01(damagePopupTimer / DamagePopupDuration);
        damagePopup.rectTransform.anchoredPosition = damagePopupStart + new Vector2(18f * t, -28f * t);
        Color color = damagePopup.color;
        color.a = 1f - t;
        damagePopup.color = color;
        if (damagePopupTimer <= 0f)
            damagePopup.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        for (int i = enemyViews.Count - 1; i >= 0; i--)
        {
            EnemyHealthView view = enemyViews[i];
            if (view.Enemy == null)
            {
                if (view.Root != null)
                    Destroy(view.Root.gameObject);
                enemyViews.RemoveAt(i);
                continue;
            }

            bool show = !view.Enemy.IsDead;
            if (show && cam != null)
            {
                Vector3 screen = cam.WorldToScreenPoint(view.Enemy.transform.position + Vector3.up * 1.15f);
                show = screen.z > 0f;
                if (show && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                    view.Root.anchoredPosition = local;
            }

            if (view.Root.gameObject.activeSelf != show)
                view.Root.gameObject.SetActive(show);
        }
    }

    private void RefreshPlayerHealth()
    {
        if (!built || player == null)
            return;

        int max = Mathf.Max(1, player.MaxHealth);
        float ratio = Mathf.Clamp01((float)player.CurrentHealth / max);
        SetFill(playerFill, ratio);
        playerFillImage.color = ratio <= 0.3f
            ? new Color(0.86f, 0.28f, 0.24f, 1f)
            : new Color(0.38f, 0.76f, 0.42f, 1f);
        playerHealthText.text = player.CurrentHealth + "/" + player.MaxHealth;
        defeatText.gameObject.SetActive(player.IsDefeated);
    }

    private void RefreshCard()
    {
        if (!built)
            return;

        MonsterCardData card = loadout != null ? loadout.EquippedCard : null;
        if (card == null)
        {
            cardNameText.text = "无";
            cardIcon.color = new Color(0.28f, 0.28f, 0.32f, 1f);
            return;
        }

        cardNameText.text = card.displayName;
        cardIcon.color = new Color(0.78f, 0.58f, 0.22f, 1f);
    }

    private void ShowPlayerHit(int amount)
    {
        flashAlpha = 0.4f;
        SetAlpha(damageFlash, flashAlpha);
        shakeTimer = HitShakeDuration;
        damagePopup.text = "-" + Mathf.Max(1, amount);
        damagePopup.color = new Color(1f, 0.32f, 0.28f, 1f);
        damagePopup.rectTransform.anchoredPosition = damagePopupStart;
        damagePopup.gameObject.SetActive(true);
        damagePopupTimer = DamagePopupDuration;
        RefreshPlayerHealth();
    }

    private void TrackEnemy(BasicEnemy enemy)
    {
        if (!built || enemy == null || enemyViews.Exists(view => view.Enemy == enemy))
            return;

        EnemyHealthView view = CreateEnemyView();
        view.Enemy = enemy;
        view.Handler = () => RefreshEnemy(view);
        enemy.HealthChanged += view.Handler;
        enemyViews.Add(view);
        RefreshEnemy(view);
    }

    private void UntrackEnemy(BasicEnemy enemy)
    {
        for (int i = enemyViews.Count - 1; i >= 0; i--)
        {
            if (enemyViews[i].Enemy != enemy)
                continue;

            if (enemy != null && enemyViews[i].Handler != null)
                enemy.HealthChanged -= enemyViews[i].Handler;
            if (enemyViews[i].Root != null)
                Destroy(enemyViews[i].Root.gameObject);
            enemyViews.RemoveAt(i);
        }
    }

    private void RefreshEnemy(EnemyHealthView view)
    {
        if (view.Enemy == null)
            return;

        int max = Mathf.Max(1, view.Enemy.MaxHealth);
        float ratio = Mathf.Clamp01((float)view.Enemy.CurrentHealth / max);
        SetFill(view.Fill, ratio);
        view.Label.text = view.Enemy.DisplayName + "  " + view.Enemy.CurrentHealth + "/" + view.Enemy.MaxHealth;
        view.Root.gameObject.SetActive(!view.Enemy.IsDead);
    }

    private void Build()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        canvasObject = new GameObject("CombatCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasRect = canvasObject.GetComponent<RectTransform>();

        damageFlash = CreateImage(canvasRect, "DamageFlash", new Color(0.7f, 0.05f, 0.08f, 0f));
        Stretch(damageFlash.rectTransform);

        RectTransform hud = CreateRect(canvasRect, "PlayerHud");
        hud.anchorMin = new Vector2(0f, 1f);
        hud.anchorMax = new Vector2(0f, 1f);
        hud.pivot = new Vector2(0f, 1f);
        hud.anchoredPosition = new Vector2(24f, -18f);
        hud.sizeDelta = new Vector2(760f, 44f);

        Text healthLabel = CreateText(hud, "HealthLabel", font, 24, TextAnchor.MiddleLeft);
        Place(healthLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(72f, 36f));
        healthLabel.text = "生命";

        playerBar = CreateRect(hud, "PlayerBar");
        playerBarRest = new Vector2(78f, 0f);
        Place(playerBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), playerBarRest, new Vector2(220f, 18f));
        Image barBack = CreateImage(playerBar, "Back", new Color(0.08f, 0.07f, 0.1f, 0.92f));
        Stretch(barBack.rectTransform);
        playerFillImage = CreateImage(playerBar, "Fill", new Color(0.38f, 0.76f, 0.42f, 1f));
        playerFill = playerFillImage.rectTransform;
        SetFill(playerFill, 1f);

        playerHealthText = CreateText(hud, "HealthValue", font, 24, TextAnchor.MiddleLeft);
        Place(playerHealthText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(310f, 0f), new Vector2(90f, 36f));

        Text cardLabel = CreateText(hud, "CardLabel", font, 24, TextAnchor.MiddleLeft);
        Place(cardLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(410f, 0f), new Vector2(72f, 36f));
        cardLabel.text = "卡牌";

        cardIcon = CreateImage(hud, "CardIcon", new Color(0.28f, 0.28f, 0.32f, 1f));
        Place(cardIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(488f, 0f), new Vector2(24f, 34f));

        cardNameText = CreateText(hud, "CardName", font, 24, TextAnchor.MiddleLeft);
        Place(cardNameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(522f, 0f), new Vector2(220f, 36f));
        cardNameText.text = "无";

        damagePopup = CreateText(hud, "DamagePopup", font, 28, TextAnchor.MiddleLeft);
        damagePopupStart = new Vector2(310f, -8f);
        Place(damagePopup.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), damagePopupStart, new Vector2(80f, 36f));
        damagePopup.gameObject.SetActive(false);

        defeatText = CreateText(canvasRect, "Defeat", font, 26, TextAnchor.UpperLeft);
        Place(defeatText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -68f), new Vector2(420f, 36f));
        defeatText.text = "倒下了    按 R 重试";
        defeatText.color = new Color(1f, 0.45f, 0.4f, 1f);
        defeatText.gameObject.SetActive(false);

        built = true;
    }

    private EnemyHealthView CreateEnemyView()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnemyHealthView view = new EnemyHealthView();
        view.Root = CreateRect(canvasRect, "EnemyHealth");
        view.Root.anchorMin = new Vector2(0.5f, 0.5f);
        view.Root.anchorMax = new Vector2(0.5f, 0.5f);
        view.Root.pivot = new Vector2(0.5f, 0f);
        view.Root.sizeDelta = new Vector2(150f, 36f);

        view.Label = CreateText(view.Root, "Label", font, 18, TextAnchor.MiddleCenter);
        Place(view.Label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(150f, 22f));

        RectTransform bar = CreateRect(view.Root, "Bar");
        Place(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(72f, 10f));
        Image back = CreateImage(bar, "Back", new Color(0.08f, 0.07f, 0.1f, 0.92f));
        Stretch(back.rectTransform);
        view.Fill = CreateImage(bar, "Fill", new Color(0.86f, 0.32f, 0.38f, 1f)).rectTransform;
        SetFill(view.Fill, 1f);
        return view;
    }

    private static void SetFill(RectTransform fill, float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(ratio, 1f);
        if (ratio <= 0.001f)
        {
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            return;
        }

        fill.offsetMin = new Vector2(1f, 1f);
        fill.offsetMax = new Vector2(-1f, -1f);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject rectObject = new GameObject(name);
        rectObject.transform.SetParent(parent, false);
        return rectObject.AddComponent<RectTransform>();
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        RectTransform rect = CreateRect(parent, name);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = WhiteSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(Transform parent, string name, Font font, int size, TextAnchor anchor)
    {
        RectTransform rect = CreateRect(parent, name);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    private static Sprite WhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        Texture2D texture = new Texture2D(1, 1);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return whiteSprite;
    }
}
