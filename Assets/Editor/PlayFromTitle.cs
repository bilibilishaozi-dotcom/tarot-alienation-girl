using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PlayFromTitle
{
    private const string TitlePath = "Assets/Scenes/Title.unity";

    static PlayFromTitle()
    {
        EditorApplication.delayCall += Apply;
    }

    private static void Apply()
    {
        SceneAsset title = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitlePath);
        if (title == null)
            return;

        EditorSceneManager.playModeStartScene = title;
    }
}
