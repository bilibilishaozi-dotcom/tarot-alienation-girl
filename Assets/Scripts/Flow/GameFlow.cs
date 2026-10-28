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
        Load(slot.sceneName);
    }

    public static void LoadFirstLevel()
    {
        LoadLevel(LevelCatalog.FirstPlayable());
    }

    public static bool LoadFoolTrial()
    {
        return LoadCatalogLevel(LevelCatalog.FoolTrialId, "关卡目录里没有可进入的愚者牌试用关。");
    }

    public static bool LoadChariotTrial()
    {
        return LoadCatalogLevel(LevelCatalog.ChariotTrialId, "关卡目录里没有可进入的战车试炼。");
    }

    private static bool LoadCatalogLevel(string levelId, string missingMessage)
    {
        LevelSlot slot = LevelCatalog.Find(levelId);
        if (slot == null)
        {
            Debug.LogWarning(missingMessage);
            return false;
        }

        LoadLevel(slot);
        return ActiveLevelId == levelId;
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
