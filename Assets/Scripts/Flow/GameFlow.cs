using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameFlow
{
    public const string TitleScene = "Title";
    public const string LevelSelectScene = "LevelSelect";

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

        Load(slot.sceneName);
    }

    public static void LoadFirstLevel()
    {
        LoadLevel(LevelCatalog.FirstPlayable());
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
