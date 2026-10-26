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
    private bool choiceHidden;

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
    }

    private void OnDisable()
    {
        if (loadout != null)
            loadout.Changed -= Refresh;
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        if (loadout == null || panel == null || !panel.activeSelf || Keyboard.current == null)
            return;

        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.jKey.wasPressedThisFrame)
            loadout.EquipPending();
        else if (Keyboard.current.escapeKey.wasPressedThisFrame)
            HideWithoutEquipping();
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

        if (showPanel)
        {
            MonsterCardData card = loadout.PendingCard;
            titleText.text = "获得怪物卡";
            bodyText.text = card.displayName + "\n" + card.description;
        }
    }

    private void BuildUi()
    {
        if (EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
        panelRect.sizeDelta = new Vector2(640f, 360f);

        titleText = CreateText(panel.transform, "Title", font, 36, TextAnchor.UpperCenter);
        SetTop(titleText.rectTransform, -24f, new Vector2(580f, 52f));

        bodyText = CreateText(panel.transform, "Body", font, 26, TextAnchor.UpperCenter);
        SetTop(bodyText.rectTransform, -88f, new Vector2(560f, 150f));

        CreateButton(panel.transform, "装备", font, new Vector2(-120f, -120f), () => loadout.EquipPending());
        CreateButton(panel.transform, "稍后", font, new Vector2(120f, -120f), HideWithoutEquipping);
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
