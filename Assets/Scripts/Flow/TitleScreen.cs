using UnityEngine;
using UnityEngine.UI;

public class TitleScreen : MonoBehaviour
{
    private void Awake()
    {
        Time.timeScale = 1f;
        RectTransform canvas = MenuKit.CreateCanvas("TitleCanvas");
        Sprite logo = UiArt.Load(UiArt.TitleLogo);
        if (logo != null)
        {
            Image mark = MenuKit.CreateImage(canvas, "Logo", logo, Color.white);
            mark.rectTransform.sizeDelta = new Vector2(720f, 240f);
            mark.rectTransform.anchoredPosition = new Vector2(0f, 220f);
        }
        else
        {
            Text title = MenuKit.CreateText(canvas, "Title", "愚者的裂隙", 64, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.55f));
            title.rectTransform.sizeDelta = new Vector2(800f, 100f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 220f);
        }

        MenuKit.CreateButton(canvas, "开始游戏", new Vector2(0f, 40f), GameFlow.LoadFirstLevel);
        MenuKit.CreateButton(canvas, "选择关卡", new Vector2(0f, -50f), GameFlow.LoadLevelSelect);
        MenuKit.CreateButton(canvas, "结束游戏", new Vector2(0f, -140f), GameFlow.Quit);
        MenuKit.CreateButton(canvas, "魔女演示", new Vector2(0f, -230f), GameFlow.LoadBlueWitchDemo);
    }
}
