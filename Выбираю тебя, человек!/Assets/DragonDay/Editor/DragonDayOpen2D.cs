using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DragonDay
{
    [InitializeOnLoad]
    internal static class DragonDayOpen2D
    {
        internal const string Main = "Assets/DragonDay/Scenes/DragonDay.unity";

        static DragonDayOpen2D()
        {
            EditorSceneManager.sceneOpened += OnOpen;
            EditorApplication.delayCall += Prepare;
        }
        private static void Prepare()
        {
            EditorSettings.defaultBehaviorMode=EditorBehaviorMode.Mode2D;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (PlayerSettings.defaultScreenWidth==1040 && PlayerSettings.defaultScreenHeight==800)
            {
                PlayerSettings.defaultScreenWidth=1280;
                PlayerSettings.defaultScreenHeight=720;
            }
            var s=SceneManager.GetActiveScene();
            if(string.IsNullOrEmpty(s.path) && !s.isDirty && AssetDatabase.LoadAssetAtPath<SceneAsset>(Main)!=null)
                EditorSceneManager.OpenScene(Main);
            if(SceneManager.GetActiveScene().path==Main) SetView();
        }
        private static void OnOpen(Scene s,OpenSceneMode mode)
        {
            if(s.path==Main) EditorApplication.delayCall+=SetView;
        }
        private static void SetView()
        {
            foreach(var item in SceneView.sceneViews)
                if(item is SceneView view)
                {
                    view.in2DMode=true;
                    view.pivot=Vector3.zero;
                    view.size=6.8f;
                    view.Repaint();
                }
        }
        [MenuItem("Tools/Dragon Day/Open 2D Scene")]
        private static void Open()
        {
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(Main);
                SetView();
            }
        }
    }
}
