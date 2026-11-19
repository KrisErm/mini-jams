using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiniJam.Editor
{
    public sealed class JamSetupWindow : EditorWindow
    {
        JamConfig config;
        SerializedObject serializedConfig;
        Vector2 scroll;
        const string ConfigPath = "Assets/MiniJam/GameConfig.asset";

        [MenuItem("Mini Jam/1. Создать игру")]
        public static void Open() { GetWindow<JamSetupWindow>("Спаси принцессу"); }
        [MenuItem("Mini Jam/Проверить финал (в Play Mode)")]
        public static void TestFinale()
        {
            var game = JamGame.Instance;
            if (!EditorApplication.isPlaying || game == null || game.introScene || !game.CanPlay)
            { EditorUtility.DisplayDialog("Проверка финала", "Запусти MJ_Castle кнопкой Play. Затем снова выбери эту команду.", "OK"); return; }
            game.BeginFinale();
        }
        [MenuItem("Mini Jam/Обновить интерьер из моих ассетов")]
        public static void UpdateSuppliedInterior()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Останови игру", "Сначала выключи Play.", "OK"); return; }
            if (!File.Exists(JamSceneBuilder.IntroPath))
            {
                EditorUtility.DisplayDialog("Нужно вступление", "Сначала создай базовые сцены через Mini Jam → 1. Создать игру. Затем запусти обновление интерьера.", "OK"); return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!EditorUtility.DisplayDialog("Обновить интерьер?", "MJ_Castle будет пересоздана с твоими моделями, длинным маршрутом, ящиками и видео. MJ_Intro сохранится. Текущие сцены и настройки будут скопированы в MiniJam_Backups внутри папки проекта.", "Обновить", "Отмена")) return;
            var window = GetWindow<JamSetupWindow>("Спаси принцессу");
            if (window.config == null) window.OnEnable();
            AssetDatabase.SaveAssets();
            try { JamSceneBuilder.UpdateInterior(window.config); }
            catch (System.Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("Обновление не завершено", e.Message + "\nПодробности в Console. Резервная копия — MiniJam_Backups.", "OK"); }
            finally { EditorUtility.ClearProgressBar(); window.serializedConfig.Update(); }
        }
        void OnEnable()
        {
            minSize = new Vector2(480, 650);
            config = AssetDatabase.LoadAssetAtPath<JamConfig>(ConfigPath);
            if (config == null)
            {
                config = CreateInstance<JamConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }
            serializedConfig = new SerializedObject(config);
        }
        void OnGUI()
        {
            if (config == null) { OnEnable(); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(10);
            GUILayout.Label("СПАСИ ПРИНЦЕССУ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("ОБНОВЛЕНИЕ: нажми кнопку ниже. Модели и анимации из присланных архивов подключатся автоматически. Пересоздаётся только интерьер; вступление сохраняется.", MessageType.Info);
            if (GUILayout.Button("ОБНОВИТЬ ИНТЕРЬЕР ИЗ МОИХ АССЕТОВ", GUILayout.Height(44))) UpdateSuppliedInterior();
            EditorGUILayout.HelpBox("Игра от первого лица. Создаёт две редактируемые сцены: MJ_Intro и MJ_Castle. Три испытания, стража, ключ, ловушки, диалоги и финал уже связаны. Все поля ниже необязательные: без моделей игра использует собственные простые фигурки.", MessageType.Info);
            serializedConfig.Update();
            GUILayout.Label("1. Перетащи prefab из скачанных ассетов", EditorStyles.boldLabel);
            Property("hero", "Герой");
            Property("guard", "Стражник");
            Property("princess", "Принцесса");
            Property("castleExterior", "Замок для заставки (целый prefab)");
            EditorGUILayout.HelpBox("После обновления поля стражи, принцессы, стен, пола и анимаций заполняются автоматически. Герой и внешний замок сохраняют прежние настройки.", MessageType.None);
            Property("wallModule", "Ровная стена (не угол/арка)");
            Property("floorModule", "Плитка пола");
            Property("doorwayModule", "Каменный дверной проём");
            GUILayout.Space(8);
            GUILayout.Label("2. Ориентация и анимации", EditorStyles.boldLabel);
            Property("heroYaw", "Поворот модели героя, Y");
            Property("guardYaw", "Поворот модели стражи, Y");
            Property("princessYaw", "Поворот принцессы, Y");
            Property("idle", "Idle (совместимый с моделью)");
            Property("walk", "Walk/Run (совместимый с моделью)");
            Property("attack", "Attack (совместимый с моделью)");
            EditorGUILayout.HelpBox("Анимации необязательны. Для клипов нужен Animator и совместимый rig/avatar. Без клипов движение и удар работают, используются простые покачивания и отдельный меч. Если модель смотрит назад, поставь Y = 180 и пересобери сцены.", MessageType.None);
            GUILayout.Space(8);
            GUILayout.Label("3. Настройки", EditorStyles.boldLabel);
            Property("moveSpeed", "Скорость героя"); Property("jumpHeight", "Высота прыжка");
            Property("mouseSensitivity", "Чувствительность мыши"); Property("guardSpeed", "Скорость стражи");
            Property("princessToRescue", "Кого спасает герой"); Property("actualPrincess", "Настоящее имя принцессы");
            Property("mistakenPrincessLine", "Реплика: не та принцесса");
            Property("cleaningBillLine", "Реплика перед видео");
            serializedConfig.ApplyModifiedProperties();
            GUILayout.Space(12);
            if (GUILayout.Button("СОЗДАТЬ СЦЕНЫ ИГРЫ", GUILayout.Height(42)))
            {
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (File.Exists(JamSceneBuilder.IntroPath) && !EditorUtility.DisplayDialog("Пересоздать сцены?",
                    "MJ_Intro и MJ_Castle будут пересозданы. Ручные изменения в этих двух сценах будут потеряны. Скачанные ассеты и другие сцены останутся на месте.", "Пересоздать", "Отмена")) return;
                try { JamSceneBuilder.Build(config); }
                catch (System.Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("Создание не завершено", e.Message + "\nПодробности в Console.", "OK"); }
                finally { EditorUtility.ClearProgressBar(); }
            }
            if (GUILayout.Button("Открыть вступление")) OpenScene(JamSceneBuilder.IntroPath);
            if (GUILayout.Button("Открыть замок (сразу играть)")) OpenScene(JamSceneBuilder.CastlePath);
            if (GUILayout.Button("Проверить сцены")) JamSceneBuilder.Validate();
            if (GUILayout.Button("Собрать Windows .exe")) JamSceneBuilder.BuildWindows();
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("Рекомендуемый проект: Unity 6, 3D Built-in. Также предусмотрен URP для созданной здесь геометрии. Сторонние материалы при необходимости конвертируются средствами Unity. После создания сцен нажми Play.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        void Property(string field, string label) { EditorGUILayout.PropertyField(serializedConfig.FindProperty(field), new GUIContent(label)); }
        static void OpenScene(string path)
        {
            if (!File.Exists(path)) { EditorUtility.DisplayDialog("Сначала создай сцены", "Нажми «Создать сцены игры».", "OK"); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }
    }
}
