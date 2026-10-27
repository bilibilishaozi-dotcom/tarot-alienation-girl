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
    private bool choiceHidden;
    private bool pausedForCard;

    public bool IsPanelOpen => panel != null && panel.activeSelf;

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

        if (pausedForCard)
        {
            pausedForCard = false;
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
        if (loadout == null || panel == null || !panel.activeSelf || Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            HideWithoutEquipping();
    }

    private void OnEquipPressed(InputAction.CallbackContext context)
    {
        if (!context.performed || loadout == null || loadout.PendingCard == null)
            return;

        loadout.EquipPending();
    }

    private void Refresh()
    {
        if (loadout == null)
            return;

        bool hasPending = loadout.PendingCard != null;
        bool showPanel = hasPending && !choiceHidden;
        panel.SetActive(showPanel);
        equipReminder.SetActive(hasPending && choiceHidden);
        if (player != null)
            player.SetGameplayInputEnabled(!showPanel);
        SetPaused(showPanel);

        if (showPanel)
        {
            MonsterCardData card = loadout.PendingCard;
            titleText.text = "获得占星牌";
            bodyText.text = card.displayName + "\n" + card.description
                + "\n\n刀光会变成金色，并且伸得更远，伤害变成两倍。"
                + "\n贴着栏杆攻击，可以打到对面的敌人。"
                + "\n\nJ 或回车：装备    Esc：稍后";
            if (equipButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(equipButton.gameObject);
        }
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

        equipButton = CreateButton(panel.transform, "装备", font, new Vector2(-120f, -168f), () => loadout.EquipPending());
        CreateButton(panel.transform, "稍后", font, new Vector2(120f, -168f), HideWithoutEquipping);
        equipReminder = CreateButton(canvasObject.transform, "装备愚者", font, new Vector2(220f, -90f), () =>
        {
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
    }

    private void HideWithoutEquipping()
    {
        choiceHidden = true;
        Refresh();
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
