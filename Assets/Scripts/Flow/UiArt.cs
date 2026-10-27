using UnityEngine;

// 换成自己的图时，把同名文件放进 Assets/Resources/UI/，贴图类型设为 Sprite。
// TitleBackground  主菜单和选关的全屏底图
// TitleLogo        主菜单标题图。放了这张图之后，程序生成的标题字会让开
// Button           按钮底图
// Panel            弹窗底图，通关面板也会用它
// LevelPreview     关卡卡片的默认预览
// Levels/场景名    某一关单独的预览，例如 Levels/SampleScene，优先于 LevelPreview
public static class UiArt
{
    public const string TitleBackground = "UI/TitleBackground";
    public const string TitleLogo = "UI/TitleLogo";
    public const string Button = "UI/Button";
    public const string Panel = "UI/Panel";
    public const string LevelPreview = "UI/LevelPreview";

    public static Sprite Load(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return null;

        return Resources.Load<Sprite>(resourcePath);
    }

    public static Sprite LevelArt(string sceneName, Sprite assigned)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            Sprite perLevel = Load("UI/Levels/" + sceneName);
            if (perLevel != null)
                return perLevel;
        }

        if (assigned != null)
            return assigned;

        return Load(LevelPreview);
    }
}
