using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class CardSelectUI : MonoBehaviour
{
    private Player player;
    private PlayerCardLoadout loadout;
    private GameObject panel;
    private Text titleText;
    private Text bodyText;
    private GameObject equipReminder;
    private Button equipButton;
    private InputAction equipAction;
    private GameObject statusPanel;
    private Text statusText;
    private Button statusEquipButton;
    private bool choiceHidden;
    private bool pausedForCard;
    private bool pausedForStatus;
    private bool heldByClear;
    private int ignoreEscapeFrame = -1;

    public bool IsPanelOpen => panel != null && panel.activeSelf;
    public bool IsStatusOpen => statusPanel != null && statusPanel.activeSelf;

    private void Awake()
    {
        player = GetComponent<Player>();
        loadout = GetComponent<PlayerCardLoadout>();
        BuildUi();
    }

    private void OnEnable()
    {
        if (loadout != null)
            loadout.Changed += Refresh;

        equipAction = new InputAction(type: InputActionType.Button);
        equipAction.AddBinding("<Keyboard>/j");
        equipAction.AddBinding("<Keyboard>/enter");
        equipAction.AddBinding("<Keyboard>/numpadEnter");
        equipAction.performed += OnEquipPressed;
        equipAction.Enable();
    }

    private void OnDisable()
    {
        if (loadout != null)
            loadout.Changed -= Refresh;

        if (pausedForCard || pausedForStatus || heldByClear)
        {
            pausedForCard = false;
            pausedForStatus = false;
            heldByClear = false;
            Time.timeScale = 1f;
        }

        if (equipAction == null)
            return;

        equipAction.performed -= OnEquipPressed;
        equipAction.Disable();
        equipAction.Dispose();
        equipAction = null;
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        if (heldByClear || Keyboard.current == null)
            return;

        bool escape = Keyboard.current.escapeKey.wasPressedThisFrame && Time.frameCount != ignoreEscapeFrame;
        if (panel != null && panel.activeSelf)
        {
            if (escape)
                HideWithoutEquipping();
            return;
        }

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            if (IsStatusOpen)
                CloseStatus();
            else if (CanOpenStatus())
                OpenStatus();
            return;
        }

        if (IsStatusOpen && escape)
            CloseStatus();
    }

    private void OnEquipPressed(InputAction.CallbackContext context)
    {
        if (heldByClear || !context.performed || loadout == null || loadout.PendingCard == null)
            return;

        loadout.EquipPending();
    }

    public void HoldForClear()
    {
        heldByClear = true;
        HideModals();
        pausedForCard = false;
        pausedForStatus = false;
    }

    public void ReleaseFromClear()
    {
        if (!heldByClear)
            return;

        heldByClear = false;
        ignoreEscapeFrame = Time.frameCount;
        Refresh();
    }

    private void Refresh()
    {
        if (loadout == null)
            return;

        if (heldByClear)
        {
            HideModals();
            if (player != null)
                player.SetGameplayInputEnabled(false);
            return;
        }

        bool hasPending = loadout.PendingCard != null;
        bool showPanel = hasPending && !choiceHidden;
        panel.SetActive(showPanel);
        equipReminder.SetActive(hasPending && choiceHidden);
        if (player != null)
            player.SetGameplayInputEnabled(!showPanel && !IsStatusOpen);
        SetPaused(showPanel);

        if (showPanel)
        {
            MonsterCardData card = loadout.PendingCard;
            titleText.text = "获得占星牌";
            bodyText.text = card.displayName + "\n" + card.description
                + "\n\n可以先按 Esc，用现在的攻击去前面试一次。"
                + "\n装备之后再挥刀，就能看出距离和颜色的差别。"
                + "\n\nJ 或回车：装备    Esc：先去试试";
            if (equipButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(equipButton.gameObject);
        }

        if (IsStatusOpen)
            FillStatus();
    }

    private void SetPaused(bool pause)
    {
        if (pause)
        {
            pausedForCard = true;
            Time.timeScale = 0f;
            return;
        }

        if (!pausedForCard)
            return;

        pausedForCard = false;
        if (!pausedForStatus && !heldByClear)
            Time.timeScale = 1f;
    }

    private void BuildUi()
    {
        if (EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        Font font = GameFont.Get();

        GameObject canvasObject = new GameObject("CardCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        panel = new GameObject("CardPanel");
        panel.transform.SetParent(canvasObject.transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.06f, 0.1f, 0.92f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(720f, 480f);

        titleText = CreateText(panel.transform, "Title", font, 36, TextAnchor.UpperCenter);
        SetTop(titleText.rectTransform, -24f, new Vector2(580f, 52f));

        bodyText = CreateText(panel.transform, "Body", font, 26, TextAnchor.UpperCenter);
        SetTop(bodyText.rectTransform, -80f, new Vector2(640f, 250f));

        equipButton = CreateButton(panel.transform, "装备", font, new Vector2(-120f, -168f), EquipFromButton);
        CreateButton(panel.transform, "稍后", font, new Vector2(120f, -168f), HideWithoutEquipping);
        equipReminder = CreateButton(canvasObject.transform, "装备愚者", font, new Vector2(220f, -90f), () =>
        {
            if (heldByClear)
                return;

            choiceHidden = false;
            Refresh();
        }).gameObject;
        RectTransform reminderRect = equipReminder.GetComponent<RectTransform>();
        reminderRect.anchorMin = new Vector2(0f, 1f);
        reminderRect.anchorMax = new Vector2(0f, 1f);
        reminderRect.pivot = new Vector2(0f, 1f);
        reminderRect.anchoredPosition = new Vector2(24f, -112f);
        equipReminder.SetActive(false);
        panel.SetActive(false);
        BuildStatus(canvasObject.transform, font);
    }

    private void BuildStatus(Transform parent, Font font)
    {
        statusPanel = new GameObject("StatusPanel");
        statusPanel.transform.SetParent(parent, false);
        Image image = statusPanel.AddComponent<Image>();
        image.color = new Color(0.08f, 0.06f, 0.1f, 0.94f);
        RectTransform rect = statusPanel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(640f, 520f);

        Text title = CreateText(statusPanel.transform, "Title", font, 36, TextAnchor.UpperCenter);
        title.text = "状态";
        SetTop(title.rectTransform, -24f, new Vector2(520f, 52f));

        statusText = CreateText(statusPanel.transform, "Body", font, 26, TextAnchor.UpperLeft);
        SetTop(statusText.rectTransform, -88f, new Vector2(560f, 300f));

        statusEquipButton = CreateButton(statusPanel.transform, "装备", font, new Vector2(0f, -200f), EquipFromButton);
        statusEquipButton.gameObject.SetActive(false);
        statusPanel.SetActive(false);
    }

    private void OpenStatus()
    {
        if (statusPanel == null)
            return;

        statusPanel.SetActive(true);
        FillStatus();
        if (player != null)
            player.SetGameplayInputEnabled(false);
        pausedForStatus = true;
        Time.timeScale = 0f;
    }

    private void CloseStatus()
    {
        if (statusPanel != null)
            statusPanel.SetActive(false);
        if (player != null && (panel == null || !panel.activeSelf) && !heldByClear)
            player.SetGameplayInputEnabled(true);
        if (!pausedForStatus)
            return;

        pausedForStatus = false;
        if (!pausedForCard && !heldByClear)
            Time.timeScale = 1f;
    }

    private void FillStatus()
    {
        if (statusText == null || loadout == null)
            return;

        int health = player != null ? player.CurrentHealth : 0;
        int maxHealth = player != null ? player.MaxHealth : 0;
        int damage = player != null ? player.CurrentAttackDamage : 1;
        string equipped = loadout.EquippedCard != null ? loadout.EquippedCard.displayName : "无";
        string effect = loadout.EquippedCard != null ? "\n" + loadout.EquippedCard.description : string.Empty;

        string bag = "空";
        IReadOnlyList<MonsterCardData> cards = loadout.OwnedCards;
        if (cards != null && cards.Count > 0)
        {
            bag = string.Empty;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null)
                    continue;
                if (bag.Length > 0)
                    bag += "\n";
                bool wearing = loadout.EquippedCard != null && loadout.EquippedCard.cardId == cards[i].cardId;
                bag += cards[i].displayName + (wearing ? "    装备中" : "    持有");
            }
        }

        statusText.text = "生命    " + health + " / " + maxHealth
            + "\n攻击    " + damage
            + "\n\n当前装备    " + equipped
            + effect
            + "\n\n卡牌背包\n" + bag
            + "\n\nC 或 Esc 关闭";
        if (statusEquipButton != null)
            statusEquipButton.gameObject.SetActive(loadout.PendingCard != null);
    }

    private void HideWithoutEquipping()
    {
        if (heldByClear)
            return;

        choiceHidden = true;
        Refresh();
    }

    private void EquipFromButton()
    {
        if (heldByClear || loadout == null)
            return;

        loadout.EquipPending();
    }

    private bool CanOpenStatus()
    {
        if (player == null || player.input == null)
            return true;

        return player.input.Player.enabled;
    }

    private void HideModals()
    {
        if (panel != null)
            panel.SetActive(false);
        if (statusPanel != null)
            statusPanel.SetActive(false);
        if (equipReminder != null)
            equipReminder.SetActive(false);
    }

    private static void SetTop(RectTransform rect, float y, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = size;
    }

    private static Text CreateText(Transform parent, string name, Font font, int size, TextAnchor anchor)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Button CreateButton(Transform parent, string label, Font font, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label);
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.35f, 0.22f, 0.45f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(180f, 52f);
        rect.anchoredPosition = position;

        Text text = CreateText(buttonObject.transform, "Label", font, 26, TextAnchor.MiddleCenter);
        text.text = label;
        text.rectTransform.sizeDelta = rect.sizeDelta;
        return button;
    }
}
