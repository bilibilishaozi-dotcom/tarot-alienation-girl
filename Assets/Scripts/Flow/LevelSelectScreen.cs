using UnityEngine;
using UnityEngine.UI;

public class LevelSelectScreen : MonoBehaviour
{
    private void Awake()
    {
        Time.timeScale = 1f;
        RectTransform canvas = MenuKit.CreateCanvas("LevelSelectCanvas");
        Text title = MenuKit.CreateText(canvas, "Title", "选择关卡", 48, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.55f));
        title.rectTransform.sizeDelta = new Vector2(600f, 80f);
        title.rectTransform.anchoredPosition = new Vector2(0f, 360f);

        LevelSlot[] slots = LevelCatalog.Current();
        int count = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                count++;
        }

        // 卡片高度按关卡数收缩：3 关时保持原来的 140，多过关卡也不会压到"返回标题"。
        const float top = 180f;
        const float lowest = -260f;
        float rowHeight = count > 1 ? Mathf.Min(140f, (top - lowest) / (count - 1) - 20f) : 140f;
        rowHeight = Mathf.Clamp(rowHeight, 72f, 140f);
        float step = rowHeight + 20f;
        float y = top;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            BuildRow(canvas, slots[i], y, rowHeight);
            y -= step;
        }

        MenuKit.CreateButton(canvas, "返回标题", new Vector2(0f, -360f), GameFlow.LoadTitle);
    }

    private static void BuildRow(Transform parent, LevelSlot slot, float y, float rowHeight)
    {
        Image card = MenuKit.CreateImage(parent, slot.displayName, UiArt.Load(UiArt.Panel), new Color(0.12f, 0.09f, 0.16f, 0.94f));
        card.rectTransform.sizeDelta = new Vector2(860f, rowHeight);
        card.rectTransform.anchoredPosition = new Vector2(0f, y);
        card.raycastTarget = true;

        Sprite preview = UiArt.LevelArt(slot.sceneName, slot.preview);
        Image art = MenuKit.CreateImage(card.transform, "Preview", preview, new Color(0.28f, 0.2f, 0.34f, 1f));
        art.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        art.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        art.rectTransform.pivot = new Vector2(0f, 0.5f);
        art.rectTransform.anchoredPosition = new Vector2(18f, 0f);
        art.rectTransform.sizeDelta = new Vector2(200f, Mathf.Max(40f, rowHeight - 36f));

        Text name = MenuKit.CreateText(card.transform, "Name", slot.displayName, 32, TextAnchor.MiddleLeft, Color.white);
        name.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        name.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        name.rectTransform.pivot = new Vector2(0f, 0.5f);
        name.rectTransform.anchoredPosition = new Vector2(240f, 0f);
        name.rectTransform.sizeDelta = new Vector2(320f, 80f);

        bool open = slot.available && !string.IsNullOrEmpty(slot.sceneName);
        Button button = MenuKit.CreateButton(card.transform, open ? "进入" : "未开放", new Vector2(310f, 0f), () => GameFlow.LoadLevel(slot));
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(180f, Mathf.Max(44f, rowHeight - 40f));
        Text buttonLabel = button.GetComponentInChildren<Text>();
        if (buttonLabel != null)
            buttonLabel.rectTransform.sizeDelta = buttonRect.sizeDelta;
        button.interactable = open;
        if (!open)
            button.GetComponent<Image>().color = new Color(0.25f, 0.22f, 0.28f, 1f);
    }
}
