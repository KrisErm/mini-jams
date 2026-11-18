#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GodniyGod;

[InitializeOnLoad]
public static class GGProjectTools
{
    private const string ScenePath="Assets/GodniyGod/Scenes/Main.unity";
    private const string FixMarker="GG_SCENE_VERSION_19_RAIN_PREVIEW";
    private static bool building;
    private static double nextCheck;
    static GGProjectTools()
    {
        EditorApplication.delayCall += CheckOnLoad;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.update += RetryMissingScene;
    }
    private static void RetryMissingScene()
    {
        if(EditorApplication.timeSinceStartup<nextCheck)return;
        nextCheck=EditorApplication.timeSinceStartup+3.0;
        if(!EditorApplication.isPlayingOrWillChangePlaymode)
            EnsureFactoryVisible();
    }
    private static void CheckOnLoad()
    {
        CheckBuildScene();
        if(!EditorApplication.isPlayingOrWillChangePlaymode)
            EnsureFactoryVisible();
    }
    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += EnsureFactoryVisible;
    }
    private static void CheckBuildScene()
    {
        var asset=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if(asset==null)return;
        var scenes=EditorBuildSettings.scenes;
        if(scenes.Length!=1 || scenes[0].path!=ScenePath || !scenes[0].enabled)
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
    }
    [MenuItem("Годный год/Открыть игровую сцену")]
    public static void OpenScene()
    {
        if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
    }
    // This menu command is available in case automatic scene generation was
    // interrupted by an import; it does not require the user to edit YAML.
    [MenuItem("Годный год/Построить 3D-фабрику в сцене")]
    public static void EnsureFactoryVisible()
    {
        if(building || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        var scene=SceneManager.GetActiveScene();
        if(!scene.IsValid() || scene.path!=ScenePath)return;
        if(GameObject.Find(FixMarker)!=null && GameObject.Find("3D Фабрика времени")!=null)return;
        building=true;
        try
        {
            // Rebuild outdated scenes and clear missing MonoBehaviours in the previous edition.
            // A marker prevents rebuilding on every subsequent editor refresh.
            foreach(var go in scene.GetRootGameObjects())
            {
                if(go.name=="GG_GameRoot")continue;
                Object.DestroyImmediate(go);
            }
            var root=GameObject.Find("GG_GameRoot");
            if(root==null)root=new GameObject("GG_GameRoot");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            if(root.GetComponent<GGGame>()==null)root.AddComponent<GGGame>();
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var world=new GGWorld(font);
            world.Build();
            world.BuildSeason(0,0,GGData.GetDelivery(0,0));
            new GameObject(FixMarker);
            var camera=world.sceneCamera;
            if(camera!=null)
            {
                // Give Scene view a useful framing, not the empty default grid.
                SceneView view=SceneView.lastActiveSceneView;
                if(view!=null)
                {
                    view.pivot=new Vector3(0,1.5f,-.45f);
                    view.rotation=camera.transform.rotation;
                    view.size=9;
                    view.Repaint();
                }
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Годный год: Scene обновлена — миниатюры небольшого дождя содержат 3 капли, сильного дождя — 5.");
        }
        catch(System.Exception ex)
        {
            Debug.LogError("Годный год: не удалось построить сцену. " + ex);
        }
        finally {building=false;}
    }
    [MenuItem("Годный год/Обновить сцену и миниатюры дождя")]
    public static void RepairNow()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Годный год", "Сначала выйди из Play Mode.", "OK");
            return;
        }
        var scene=SceneManager.GetActiveScene();
        if(!scene.IsValid() || scene.path!=ScenePath)
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var marker=GameObject.Find(FixMarker);
        if(marker!=null)Object.DestroyImmediate(marker);
        EnsureFactoryVisible();
        Debug.Log("Годный год: обновлены предпросмотр и миниатюры дождя. Выбранная сцена сохранена.");
    }
    [MenuItem("Годный год/Пересоздать игровую сцену (полностью)")]
    public static void RebuildScene()
    {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("GG_GameRoot");root.AddComponent<GGGame>();
        EditorSceneManager.SaveScene(scene,ScenePath);
        EnsureFactoryVisible();
        CheckBuildScene();
    }
    [MenuItem("Годный год/Информация о DOTween")]
    public static void TweenHelp()
    {
        EditorUtility.DisplayDialog("DOTween / Годный год",
            "DOTween обязателен: Tools -> Demigiant -> DOTween Utility Panel -> Setup DOTween. Все анимации в GGAnimation.cs используют DG.Tweening напрямую: DOJump, DOMove, DOScale, DORotate, DOShakePosition, DOFade и Sequence. Символ DOTWEEN больше не требуется.","Понятно");
    }
}
#endif
