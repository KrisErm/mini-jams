using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace GoodIntentions.Editor
{
    public static class ProjectTools
    {
        const string ScenePath = "Assets/GoodIntentions/Scenes/Department.unity";
        [MenuItem("Tools/Отдел благих намерений/Открыть игровую сцену")]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Tools/Отдел благих намерений/Настроить проект по умолчанию")]
        public static void Configure()
        {
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            // Preserve existing scenes. Put the game scene first for File → Build Profiles.
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.path != ScenePath) scenes.Add(scene);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Отдел благих намерений/Исправить импорт ресурсов")]
        public static void RepairResources()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Сначала остановите Play, затем запустите исправление импорта.");
                return;
            }
            const string root = "Assets/GoodIntentions/Resources/";
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string name in new[] { "office", "portraits", "clerk", "intercom", "sign", "manhole", "cafe", "greenhouse" })
            {
                string path = root + "Art/" + name + ".png";
                if (!File.Exists(path)) throw new FileNotFoundException("В папке проекта отсутствует изображение", path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var texture = AssetImporter.GetAtPath(path) as TextureImporter;
                if (texture == null) throw new InvalidOperationException("Не распознан импортёр изображения: " + path);
                ConfigureArtType(texture, path);
                bool incidentImage = IsIncidentArt(path);
                texture.mipmapEnabled = incidentImage;
                texture.filterMode = incidentImage ? FilterMode.Trilinear : FilterMode.Bilinear;
                texture.npotScale = TextureImporterNPOTScale.None;
                texture.maxTextureSize = 4096;
                texture.textureCompression = TextureImporterCompression.Uncompressed;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.SaveAndReimport();
            }
            foreach (string name in new[] { "DejaVuSans", "DejaVuSans-Bold", "DejaVuSerif" })
            {
                string path = root + "Fonts/" + name + ".ttf";
                if (!File.Exists(path)) throw new FileNotFoundException("В папке проекта отсутствует шрифт", path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var font = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
                if (font == null) throw new InvalidOperationException("Не распознан импортёр шрифта: " + path);
                font.includeFontData = true;
                font.fontTextureCase = FontTextureCase.Dynamic;
                font.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Verify();
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Отдел благих намерений", "Ресурсы импортированы и проверки пройдены. Можно нажать Play.", "OK");
        }
        internal const string OfficePath = "Assets/GoodIntentions/Resources/Art/office.png";
        internal static bool IsIncidentArt(string path)
        {
            switch (Path.GetFileNameWithoutExtension(path))
            {
                case "sign": case "manhole": case "cafe": case "greenhouse": case "roof_night": return true;
                default: return false;
            }
        }

        internal static void ConfigureArtType(TextureImporter importer, string path)
        {
            if (path != OfficePath)
            {
                importer.textureType = TextureImporterType.Default;
                return;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spritePixelsPerUnit = 100;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        [MenuItem("Tools/Отдел благих намерений/Выбрать настройки")]
        public static void SelectSettings()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                OpenScene();
                if (SceneManager.GetActiveScene().path != ScenePath) return;
            }
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                DepartmentGame controller = root.GetComponentInChildren<DepartmentGame>(true);
                if (controller == null) continue;
                Selection.activeGameObject = controller.gameObject;
                EditorGUIUtility.PingObject(controller);
                return;
            }
        }

        [MenuItem("Tools/Отдел благих намерений/Проверить игру")]
        public static void Verify()
        {
            Require(File.Exists(ScenePath), "Missing scene");
            Scene preview = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                DepartmentGame controller = null;
                foreach (GameObject root in preview.GetRootGameObjects())
                {
                    DepartmentGame candidate = root.GetComponentInChildren<DepartmentGame>(true);
                    if (candidate == null) continue;
                    Require(controller == null, "В сцене найдено несколько контроллеров DepartmentGame.");
                    controller = candidate;
                }
                Require(controller != null, "В сцене отсутствует DepartmentGame.");
                var settings = new SerializedObject(controller);
                var background = settings.FindProperty("sceneBackground").objectReferenceValue as SpriteRenderer;
                var camera = settings.FindProperty("sceneCamera").objectReferenceValue as Camera;
                Require(camera != null && camera.enabled && background != null && background.enabled && background.sprite != null,
                    "В DepartmentGame должны быть назначены включённые Camera и SpriteRenderer со спрайтом.");
                var asset = settings.FindProperty("storySource").objectReferenceValue as TextAsset;
                if (asset == null) asset = Resources.Load<TextAsset>("story");
                Story story = VerifyStory(asset);
                VerifySceneAssets(settings, story);
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static Story VerifyStory(TextAsset asset)
        {
            Require(asset != null, "Resources/story.json missing");
            Story story = JsonUtility.FromJson<Story>(asset.text);
            Require(story != null && story.people != null && story.people.Length == 10 &&
                story.events != null && story.events.Length == 16, "Expected 10 people and 16 incidents");
            var ids = new HashSet<string>();
            foreach (Person person in story.people) Require(ids.Add(person.id), "Duplicate person ID");
            foreach (Incident incident in story.events)
            {
                Require(ids.Contains(incident.subject), "Invalid subject in " + incident.id);
                // The finale is paged narrative and is never resolved through a normal decision.
                if (incident.id == "case_16") continue;
                Require(incident.choices != null && incident.choices.Length == 3, "Expected 3 choices in " + incident.id);
                foreach (Choice choice in incident.choices)
                {
                    Require(choice.cost >= 0 && choice.cost <= GameModel.SealsPerShift, "Invalid seal cost");
                    Require(string.IsNullOrEmpty(choice.requiresRead) || ids.Contains(choice.requiresRead), "Unknown dossier");
                    Require(choice.outcomes != null && choice.outcomes.Length > 0, "No outcomes");
                    Outcome last = choice.outcomes[choice.outcomes.Length - 1];
                    Require(last.when == null || last.when.Length == 0, "Missing fallback outcome");
                    foreach (Outcome result in choice.outcomes)
                    {
                        Require(!result.orderFailed || !string.IsNullOrEmpty(incident.order),
                            "A disciplinary violation requires an explicit order in " + incident.id);
                        if (result.changes != null)
                            foreach (Change effect in result.changes)
                                Require(ids.Contains(effect.person), "Unknown affected person");
                    }
                }
            }
            // The story always ends at case_16, not through separate execution/refusal orders.
            Require(story.events[15].id == "case_16" && story.events[15].subject == "elena", "Missing final case");
            foreach (Incident incident in story.events)
                if (incident.id != "case_16")
                    foreach (Choice choice in incident.choices)
                        foreach (Outcome result in choice.outcomes)
                            if (result.changes != null)
                                foreach (Change effect in result.changes)
                                    Require(!(effect.person == "elena" && effect.kind == "kill"), "Elena dies before finale");
            int orderedCases = 0;
            foreach (Incident incident in story.events)
                if (!string.IsNullOrEmpty(incident.order)) orderedCases++;
            Require(orderedCases == 3, "Expected exactly three documented department orders");
            var game = new GameModel(story);
            Require(!game.State.introComplete && game.State.briefedShift == 0, "New game bypasses onboarding");
            foreach (Person person in story.people) game.Read(person.id);
            for (int i = 0; i < story.events.Length; i++)
            {
                Require(game.Shift == i / 4 + 1, "Shift mismatch at " + i);
                if (game.Current.id == "case_16")
                {
                    game.State.finalePage = GameModel.FinaleSceneCount - 1;
                    Require(game.ResolveFinale(), "Could not resolve the staged finale");
                }
                else
                {
                    int chosen = -1;
                    for (int n = 0; n < story.events[i].choices.Length; n++)
                        if (game.BlockReason(story.events[i].choices[n]).Length == 0) { chosen = n; break; }
                    Require(chosen >= 0 && game.Resolve(chosen), "No affordable choice at " + i);
                }
                game.Next();
                Require(game.State.seals >= 0 && game.State.seals <= GameModel.SealsPerShift, "Invalid seal count");
            }
            Require(game.Finished && game.Status("elena").health == 0, "Incorrect ending");
            VerifyFinaleBranches(story);
            Debug.Log("GOOD INTENTIONS: 16 cases, 4 shifts, three explicit orders, sanctions and endings verified.");
            return story;
        }

        static void VerifySceneAssets(SerializedObject settings, Story story)
        {
            foreach (string field in new[] { "portraits", "clerk", "intercom" })
                Require(settings.FindProperty(field).objectReferenceValue != null || Resources.Load<Texture2D>("Art/" + field) != null,
                    "Не назначена текстура: " + field);
            foreach (string phase in new[] { "menu", "light", "dark" })
                Require(settings.FindProperty(phase + "Track").objectReferenceValue != null || Resources.Load<AudioClip>("Audio/" + phase) != null,
                    "Не назначена музыка: " + phase);
            SerializedProperty overrides = settings.FindProperty("caseIllustrations");
            var assignedCases = new HashSet<string>();
            for (int i = 0; i < overrides.arraySize; i++)
            {
                SerializedProperty item = overrides.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative("image").objectReferenceValue == null) continue;
                string id = item.FindPropertyRelative("caseId").stringValue;
                Require(Array.Exists(story.events, incident => incident.id == id), "Неизвестный ID дела в списке иллюстраций: " + id);
                Require(assignedCases.Add(id), "Иллюстрация назначена повторно для дела " + id);
            }
            foreach (Incident incident in story.events)
            {
                if (string.IsNullOrEmpty(incident.art) || assignedCases.Contains(incident.id)) continue;
                Require(Resources.Load<Texture2D>("Art/" + incident.art) != null || Resources.Load<Sprite>("Art/" + incident.art) != null,
                    "Не найдена иллюстрация дела " + incident.id + ": " + incident.art);
            }
        }

        static void VerifyFinaleBranches(Story story)
        {
            string[] helpers = GameModel.ElenaHelpers;
            for (int mask = 0; mask < (1 << helpers.Length); mask++)
            {
                var game = new GameModel(story, new SaveState
                {
                    step = story.events.Length - 1, introComplete = true, briefedShift = 4
                });
                int ready = 0;
                for (int i = 0; i < helpers.Length; i++)
                {
                    bool available = (mask & (1 << i)) != 0;
                    game.Status(helpers[i]).health = available ? 2 : 1;
                    if (available) ready++;
                }
                int seals = game.State.seals;
                Require(!game.ResolveFinale(), "Finale resolved too early");
                game.State.finalePage = GameModel.FinaleSceneCount - 1;
                Require(game.ResolveFinale() && game.State.seals == seals, "Finale failed or spent a seal");
                Require(game.State.flags.Contains(ready >= 2 ? "elena_abundance" : "elena_shortage"), "Wrong finale branch");
                for (int i = 0; i < helpers.Length; i++)
                {
                    bool available = (mask & (1 << i)) != 0;
                    int expected = available ? (ready >= 2 ? 0 : 2) : 1;
                    Require(game.Status(helpers[i]).health == expected, "Wrong rescuer state: " + helpers[i]);
                }
                game.Next();
                Require(game.Finished && game.Status("elena").health == 0, "Finale did not finish");
            }
        }

        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        [MenuItem("Tools/Отдел благих намерений/Собрать Windows EXE")]
        public static void BuildWindows()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Сначала остановите Play, затем запустите сборку.");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Verify();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Установите Windows Build Support для этой версии Unity через Unity Hub > Installs > Add modules.");
            string output = "Builds/Windows_Fix27/DepartmentOfGoodIntentions.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build failed: " + report.summary.result);
            Debug.Log("WINDOWS BUILD READY: " + Path.GetFullPath(output));
            if (!Application.isBatchMode) EditorUtility.RevealInFinder(Path.GetFullPath(output));
        }

        [MenuItem("Tools/Отдел благих намерений/Собрать Windows EXE", true)]
        static bool CanBuildWindows() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    public sealed class ArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/GoodIntentions/Resources/Art/", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            if (assetPath != ProjectTools.OfficePath && !ProjectTools.IsIncidentArt(assetPath)) return;
            ProjectTools.ConfigureArtType(importer, assetPath);
            bool illustration = ProjectTools.IsIncidentArt(assetPath);
            importer.mipmapEnabled = illustration;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = illustration ? FilterMode.Trilinear : FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
        }
    }
}
