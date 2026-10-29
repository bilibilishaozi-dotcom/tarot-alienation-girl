using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameFlow
{
    public const string TitleScene = "Title";
    public const string LevelSelectScene = "LevelSelect";

    public static string ActiveLevelId { get; private set; } = LevelCatalog.BasicCombatId;

    public static void LoadTitle()
    {
        Load(TitleScene);
    }

    public static void LoadLevelSelect()
    {
        Load(LevelSelectScene);
    }

    public static void LoadLevel(LevelSlot slot)
    {
        if (slot == null || !slot.available || string.IsNullOrEmpty(slot.sceneName))
            return;

        if (!Application.CanStreamedLevelBeLoaded(slot.sceneName))
        {
            Debug.LogWarning("场景还没有放进 Build Settings：" + slot.sceneName);
            return;
        }

        ActiveLevelId = string.IsNullOrEmpty(slot.levelId) ? LevelCatalog.BasicCombatId : slot.levelId;
        // 选牌只在「进 Boss 关」这一次生效；重开 Boss 关（ReloadActiveScene）时保留，
        // 去别的关卡则清掉，免得下一关莫名其妙带着上一关选的牌。
        if (ActiveLevelId != LevelCatalog.BossId)
            SelectedCardPath = null;
        Load(slot.sceneName);
    }

    public static void LoadFirstLevel()
    {
        LoadLevel(LevelCatalog.FirstPlayable());
    }

    public static bool LoadFoolTrial()
    {
        LevelSlot slot = LevelCatalog.Find(LevelCatalog.FoolTrialId);
        if (slot == null)
        {
            Debug.LogWarning("关卡目录里没有可进入的愚者牌试用关。");
            return false;
        }

        LoadLevel(slot);
        return ActiveLevelId == LevelCatalog.FoolTrialId;
    }

    public static bool LoadChariotTrial()
    {
        LevelSlot slot = LevelCatalog.Find(LevelCatalog.ChariotTrialId);
        if (slot == null)
        {
            Debug.LogWarning("关卡目录里没有可进入的战车试炼。");
            return false;
        }

        LoadLevel(slot);
        return ActiveLevelId == LevelCatalog.ChariotTrialId;
    }

    // 进 Boss 关前在「牌之回响」里选的那张牌（Resources 路径）。
    // 跨场景保留，由 PlayerCardLoadout 在新的场景里读出来装上。
    public static string SelectedCardPath { get; private set; }

    public static bool LoadBossLevel()
    {
        LevelSlot slot = LevelCatalog.Find(LevelCatalog.BossId);
        if (slot == null)
        {
            Debug.LogWarning("关卡目录里没有可进入的 Boss 关。");
            return false;
        }

        LoadLevel(slot);
        return ActiveLevelId == LevelCatalog.BossId;
    }

    public static void EnterBossWithCard(string cardResourcePath)
    {
        SelectedCardPath = cardResourcePath;
        LoadBossLevel();
    }

    public static void ReloadActiveScene()
    {
        if (reloadFrame == Time.frameCount)
            return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("场景还没有放进 Build Settings：" + sceneName);
            return;
        }

        reloadFrame = Time.frameCount;
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public static void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static int reloadFrame = -1;

    private static void Load(string sceneName)
    {
        Time.timeScale = 1f;
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("场景还没有放进 Build Settings：" + sceneName);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
