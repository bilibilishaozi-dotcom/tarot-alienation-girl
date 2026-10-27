using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class MenuKit
{
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    public static RectTransform CreateCanvas(string name)
    {
        EnsureEventSystem();
        GameObject canvasObject = new GameObject(name);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        Image background = CreateImage(canvasObject.transform, "Background", UiArt.Load(UiArt.TitleBackground), new Color(0.06f, 0.05f, 0.09f, 1f));
        Stretch(background.rectTransform);
        return canvasObject.GetComponent<RectTransform>();
    }

    public static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = sprite != null ? Color.white : color;
        image.raycastTarget = false;
        return image;
    }

    public static Text CreateText(Transform parent, string name, string message, int size, TextAnchor anchor, Color color)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = GameFont.Get();
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.text = message;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    public static Button CreateButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
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

        Text text = CreateText(buttonObject.transform, "Label", label, 28, TextAnchor.MiddleCenter, Color.white);
        text.rectTransform.sizeDelta = rect.sizeDelta;
        return button;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
