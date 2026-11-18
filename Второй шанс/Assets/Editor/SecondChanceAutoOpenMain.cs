#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Open the existing VN scene once after the first import when Unity starts on
/// an empty Untitled scene. Creates/saves the editable Canvas in EDIT mode.
/// Does not replace authored scenes or run in Play Mode.
/// </summary>
[InitializeOnLoad]
public static class SecondChanceAutoOpenMain
{
    private const string MainPath = "Assets/Scenes/Main.unity";
    private const string SessionKey = "SecondChance.MainOpenAttempted.v3";

    static SecondChanceAutoOpenMain()
    {
        EditorApplication.delayCall += TryOpen;
    }

    [MenuItem("Tools/Второй шанс/Открыть игровую сцену Main")]
    public static void OpenManually()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainPath) == null)
        {
            Debug.LogError("Сцена не найдена: " + MainPath);
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
        SecondChanceEditableSceneBuilder.EnsureScene();
    }

    private static void TryOpen()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryOpen;
            return;
        }
        SessionState.SetBool(SessionKey, true);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainPath) == null) return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return;
        if (scene.path == MainPath)
        {
            SecondChanceEditableSceneBuilder.EnsureScene();
            return;
        }
        // Unity opens a default empty scene named Untitled in a newly imported project.
        // Never replace an existing user scene.
        if (!string.IsNullOrEmpty(scene.path) || scene.name != "Untitled") return;
        GameObject[] roots = scene.GetRootGameObjects();
        if (roots.Length > 1) return;
        if (roots.Length == 1 && roots[0].GetComponent<Camera>() == null) return;
        EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
        SecondChanceEditableSceneBuilder.EnsureScene();
        Debug.Log("Второй шанс: автоматически открыта Main.unity и создан редактируемый VN Canvas.");
    }
}
#endif
