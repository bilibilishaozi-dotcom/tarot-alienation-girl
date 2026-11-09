using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Esc：继续 / 重新开始本关 / 返回标题。
// 选牌、状态栏、结算、开场选牌已经占用 Esc 或把时间停在 0，这时不抢。
public class PauseMenu : MonoBehaviour
{
    public static int SuppressFrame = -1;

    private Player player;
    private GameObject root;
    private bool open;

    private void Awake()
    {
        player = GetComponent<Player>();
        Build();
        root.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (SuppressFrame == Time.frameCount)
            return;

        if (open)
        {
            Resume();
            return;
        }

        if (Blocked())
            return;

        Pause();
    }

    private bool Blocked()
    {
        CardSelectUI cards = GetComponent<CardSelectUI>();
        if (cards != null && (cards.IsPanelOpen || cards.IsStatusOpen))
            return true;

        FirstLevel level = GetComponent<FirstLevel>();
        if (level != null && level.BlocksPause)
            return true;

        if (player != null && player.IsDefeated)
            return true;

        // 得牌演出或别的界面已经把时间停住。
        return Time.timeScale <= 0.01f;
    }

    private void Pause()
    {
        open = true;
        Juice.CancelHitStop();
        Time.timeScale = 0f;
        if (player != null)
            player.SetGameplayInputEnabled(false);
        if (root != null)
            root.SetActive(true);
    }

    private void Resume()
    {
        open = false;
        if (root != null)
            root.SetActive(false);

        bool cardBusy = false;
        CardSelectUI cards = GetComponent<CardSelectUI>();
        if (cards != null && (cards.IsPanelOpen || cards.IsStatusOpen))
            cardBusy = true;

        if (!cardBusy)
            Time.timeScale = 1f;
        if (player != null && !cardBusy && !player.IsDefeated)
            player.SetGameplayInputEnabled(true);
    }

    private void RestartLevel()
    {
        open = false;
        Time.timeScale = 1f;
        GameFlow.ReloadActiveScene();
    }

    private void BackToTitle()
    {
        open = false;
        Time.timeScale = 1f;
        GameFlow.LoadTitle();
    }

    private void Build()
    {
        MenuKit.EnsureEventSystem();
        GameObject canvasObject = new GameObject("PauseCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 180;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();
        root = canvasObject;

        Image dim = MenuKit.CreateImage(canvasObject.transform, "Dim", null, new Color(0.04f, 0.03f, 0.06f, 0.82f));
        MenuKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Image panel = MenuKit.CreateImage(canvasObject.transform, "Panel", UiArt.Load(UiArt.Panel), new Color(0.1f, 0.08f, 0.14f, 0.96f));
        panel.rectTransform.sizeDelta = new Vector2(520f, 420f);
        panel.raycastTarget = true;

        Text title = MenuKit.CreateText(canvasObject.transform, "Title", "暂停", 40, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.55f));
        title.rectTransform.sizeDelta = new Vector2(400f, 60f);
        title.rectTransform.anchoredPosition = new Vector2(0f, 120f);

        MenuKit.CreateButton(canvasObject.transform, "继续", new Vector2(0f, 30f), Resume);
        MenuKit.CreateButton(canvasObject.transform, "重新开始本关", new Vector2(0f, -50f), RestartLevel);
        MenuKit.CreateButton(canvasObject.transform, "返回标题", new Vector2(0f, -130f), BackToTitle);
    }
}
