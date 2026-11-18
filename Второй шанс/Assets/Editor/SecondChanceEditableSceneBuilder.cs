#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Создаёт РЕАЛЬНЫЕ объекты Main.unity (НЕ при запуске игры).
// После сохранения любой элемент доступен в Hierarchy / Inspector.
[InitializeOnLoad]
public static class SecondChanceEditableSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    // Новый штамп заставляет редактор пересобрать старый Canvas, а не оставлять
    // устаревшие позиции кнопок и невидимые объекты персонажей.
    private const string LayoutVersionObject = "VN_LAYOUT_ENDINGS_2026_11_12_v4";
    private static bool busy;

    static SecondChanceEditableSceneBuilder()
    {
        EditorApplication.delayCall += EnsureScene;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == ScenePath) EditorApplication.delayCall += EnsureScene;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // The last preflight runs before any MonoBehaviour.Awake. This is especially
        // important when a user installs an update over a previously opened project.
        if (state == PlayModeStateChange.ExitingEditMode)
            EnsureSceneCore(true);
    }

    [MenuItem("Tools/Второй шанс/Проверить и создать редактируемый Canvas")]
    public static void EnsureScene() { EnsureSceneCore(false); }

    private static bool HasAllBindings(SecondChanceGame controller)
    {
        if (controller == null) return false;
        SerializedObject serialized = new SerializedObject(controller);
        string[] fields = {
            "canvas", "bg", "shade", "menu", "story", "choices", "ending", "truck",
            "maxActor", "olgaActor", "leraActor", "pashaActor", "startButton", "exitButton",
            "next", "nameText", "lineText", "endingArtwork", "endingTitleText", "resultText",
            "retryButton", "backButton", "music", "hit", "alarm", "musicSettingsButton",
            "musicSettingsPanel", "musicVolumeSlider", "musicVolumePercent", "fastForwardButton", "fastForwardLabel"
        };
        foreach (string field in fields)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || property.objectReferenceValue == null) return false;
        }
        foreach (string field in new[] { "choiceButtons", "choiceLabels" })
        {
            SerializedProperty array = serialized.FindProperty(field);
            if (array == null || !array.isArray || array.arraySize != 5) return false;
            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == null) return false;
        }
        return true;
    }

    private static void EnsureSceneCore(bool beforePlay)
    {
        if (busy || (!beforePlay && EditorApplication.isPlayingOrWillChangePlaymode)) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath) return;
        GameObject existing = FindRoot(scene, "VN Canvas");
        SecondChanceGame controller = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            controller = root.GetComponent<SecondChanceGame>();
            if (controller != null) break;
        }
        if (existing != null && controller != null &&
            existing.transform.Find(LayoutVersionObject) != null && HasAllBindings(controller))
            return;
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        Build(scene);
    }

    [MenuItem("Tools/Второй шанс/Пересобрать редактируемую сцену")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath);
        }
        if (!EditorUtility.DisplayDialog("Второй шанс", "Пересобрать интерфейс? Отредактированные вручную положения элементов Canvas будут сброшены.", "Пересобрать", "Отмена")) return;
        GameObject existing = FindRoot(scene, "VN Canvas");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        Build(scene);
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    private static RectTransform UI(string name, Transform parent, Vector2 min, Vector2 max,
                                    Vector2 pos, Vector2 size, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        if (parent != null) rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        rect.pivot = pivot;
        rect.localScale = Vector3.one;
        return rect;
    }
    private static RectTransform Stretch(string name, Transform parent)
    {
        return UI(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
    }
    private static RectTransform Center(string name, Transform parent, float x, float y, float w, float h)
    {
        return UI(name, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(x, y), new Vector2(w, h), new Vector2(.5f, .5f));
    }
    private static Image AddImage(RectTransform rect, Color color, bool raycast)
    {
        Image img = rect.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }
    private static RawImage AddTexture(RectTransform rect, Texture2D texture)
    {
        RawImage raw = rect.gameObject.AddComponent<RawImage>();
        raw.texture = texture;
        raw.raycastTarget = false;
        raw.color = Color.white;
        return raw;
    }
    private static Button AddButton(RectTransform rect)
    {
        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(.85f, .85f, .85f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        button.colors = colors;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        return button;
    }
    private static Text AddText(RectTransform rect, string value, int size,
                                TextAnchor alignment, Color color, Font font)
    {
        Text t = rect.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = alignment;
        t.color = color;
        t.text = value;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }
    private static Button DrawButton(string name, Transform parent, float x, float y,
                                     float w, float h, string label, Font font, out Text text)
    {
        RectTransform rect = Center(name, parent, x, y, w, h);
        AddImage(rect, new Color(.14f, .19f, .26f, .88f), true);
        Button button = AddButton(rect);
        RectTransform accent = UI("Золотая линия ответа", rect,
            new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(8f, 0f),
            new Vector2(4f, h - 22f), new Vector2(.5f, .5f));
        AddImage(accent, new Color(.91f, .71f, .45f, .85f), false);
        text = AddText(Stretch("Надпись", rect), label, 25,
            TextAnchor.MiddleCenter, Color.white, font);
        text.GetComponent<RectTransform>().sizeDelta = new Vector2(-24, -9);
        return button;
    }
    private static Button PaintedHotspot(string name, Transform parent,
                                         float x, float y, float w, float h)
    {
        // Нарисованные кнопки исходного изображения: 1920x1080 референс.
        Vector2 min = new Vector2((960 + x - w / 2f) / 1920f,
                                  (540 + y - h / 2f) / 1080f);
        Vector2 max = new Vector2((960 + x + w / 2f) / 1920f,
                                  (540 + y + h / 2f) / 1080f);
        RectTransform rect = UI(name, parent, min, max, Vector2.zero, Vector2.zero,
            new Vector2(.5f, .5f));
        AddImage(rect, new Color(1f, 1f, 1f, .002f), true);
        return AddButton(rect);
    }
    private static void Assign(UnityEditor.SerializedObject obj, string field, UnityEngine.Object reference)
    {
        SerializedProperty p = obj.FindProperty(field);
        if (p == null) throw new InvalidOperationException("Не найдено сериализованное поле: " + field);
        p.objectReferenceValue = reference;
    }
    private static void AssignArray(SerializedObject obj, string field, UnityEngine.Object[] values)
    {
        SerializedProperty p = obj.FindProperty(field);
        if (p == null) throw new InvalidOperationException("Не найден массив UI: " + field);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; ++i)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void Build(Scene scene)
    {
        busy = true;
        try
        {
            // Удаляем старый декоративный предпросмотр и случайно сохранённый Canvas.
            GameObject oldPreview = FindRoot(scene, "SecondChanceEditorPreview");
            if (oldPreview != null) UnityEngine.Object.DestroyImmediate(oldPreview);
            GameObject oldCanvas = FindRoot(scene, "Second Chance Canvas");
            if (oldCanvas != null) UnityEngine.Object.DestroyImmediate(oldCanvas);

            SecondChanceGame controller = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                controller = root.GetComponent<SecondChanceGame>();
                if (controller != null) break;
            }
            if (controller == null)
            {
                GameObject game = new GameObject("Second Chance Game");
                controller = game.AddComponent<SecondChanceGame>();
            }
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            if (camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();

            EventSystem eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
                eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            StandaloneInputModule legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
            InputSystemUIInputModule newModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (newModule == null) newModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            newModule.AssignDefaultActions();
#else
            if (eventSystem.GetComponent<StandaloneInputModule>() == null)
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
#endif
            RectTransform ui = UI("VN Canvas", null, Vector2.zero, Vector2.one,
                                  Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
            Canvas canvas = ui.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = ui.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            ui.gameObject.AddComponent<GraphicRaycaster>();
            // Сохраняем версию макета как дочерний объект прямо в Main.unity.
            UI(LayoutVersionObject, ui, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
               Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
            Texture2D cover = Resources.Load<Texture2D>("Art/Backgrounds/title");
            if (cover == null) Debug.LogError("Не найдена заставка Assets/Resources/Art/Backgrounds/title.png");
            RawImage bg = AddTexture(Stretch("Фон — меняй здесь картинку", ui), cover);
            Image shade = AddImage(Stretch("Лёгкое затемнение (прозрачность)", ui), Color.clear, false);

            RectTransform story = Stretch("Диалоги", ui);
            RectTransform cast = Stretch("Спрайты персонажей", story);
            RawImage[] images = new RawImage[4];
            string[] actorIds = { "max", "olga", "lera", "pasha" };
            string[] actorNames = { "Макс", "Ольга Викторовна", "Лера", "Паша — водитель" };
            for (int i = 0; i < images.Length; ++i)
            {
                RectTransform r = UI(actorNames[i] + " — спрайт", cast,
                    new Vector2(.5f, 0f), new Vector2(.5f, 0f), Vector2.zero,
                    new Vector2(350f, 930f), new Vector2(.5f, 0f));
                Texture2D neutral = Resources.Load<Texture2D>("Art/Characters/" + actorIds[i] + "_neutral");
                if (neutral != null)
                    r.sizeDelta = new Vector2(930f * neutral.width / (float)neutral.height, 930f);
                if (actorIds[i] == "pasha")
                    r.anchoredPosition = new Vector2(440f, -8f); // правая сторона перехода
                images[i] = AddTexture(r, neutral);
                images[i].gameObject.SetActive(false);
            }
            // Грузовик показывается только во время столкновения.
            RectTransform crashRect = Center("Столкновение — 2D грузовик", story,
                                            300f, -80f, 160f, 160f);
            RawImage crash = AddTexture(crashRect, Resources.Load<Texture2D>("Art/Effects/truck"));
            crashRect.gameObject.SetActive(false);

            RectTransform dialog = UI("Окно диалога (клик = далее)", story,
                new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 172f),
                new Vector2(1840f, 308f), new Vector2(.5f, .5f));
            AddImage(dialog, new Color(.025f, .031f, .048f, .77f), true);
            Button dialogueButton = AddButton(dialog);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            Text speaker = AddText(Center("Имя говорящего", dialog, 0, 106, 1640, 50), "",
                32, TextAnchor.MiddleLeft, new Color(1f, .81f, .61f), font);
            Text line = AddText(Center("Текст диалога", dialog, 0, -8, 1640, 160), "",
                35, TextAnchor.UpperLeft, Color.white, font);
            speaker.gameObject.SetActive(false);

            RectTransform choices = Center("Варианты ответов (редактируемые)", story, 420, 170, 900, 445);
            // Без общей чёрной подложки: видны только отдельные кнопки ответов.
            AddImage(choices, Color.clear, false);
            Button[] optionButtons = new Button[5];
            Text[] optionTexts = new Text[5];
            for (int i = 0; i < optionButtons.Length; ++i)
            {
                optionButtons[i] = DrawButton("Ответ " + (i + 1), choices,
                    0f, (optionButtons.Length - 1) * 38f - i * 76f, 860, 65,
                    "Вариант " + (i + 1), font, out optionTexts[i]);
                Image optionBackground = optionButtons[i].GetComponent<Image>();
                if (optionBackground != null) optionBackground.color = new Color(.98f, .95f, .88f, .96f);
                optionTexts[i].color = new Color(.12f, .15f, .20f);
            }
            choices.gameObject.SetActive(false);
            story.gameObject.SetActive(false);

            // Небольшая кнопка «Музыка» поверх любой сцены — вся панель доступна в Hierarchy.
            RectTransform soundToggleRect = Center("♫ Настройки музыки", ui,
                -855f, 480f, 185f, 57f);
            AddImage(soundToggleRect, new Color(.065f, .075f, .10f, .74f), true);
            Button soundToggle = AddButton(soundToggleRect);
            AddText(Center("Надпись Музыка", soundToggleRect, 0, 0, 175f, 49f),
                "♫  Звук", 26, TextAnchor.MiddleCenter,
                new Color(.98f, .91f, .81f), font);
            RectTransform soundPanel = Center("Панель громкости музыки", ui,
                -688f, 398f, 465f, 83f);
            AddImage(soundPanel, new Color(.055f, .066f, .094f, .9f), true);
            AddText(Center("Подпись громкости", soundPanel, -157f, 0, 115f, 45f),
                "Громкость", 22, TextAnchor.MiddleCenter, Color.white, font);
            RectTransform musicSliderRect = Center("Ползунок музыки", soundPanel,
                50f, 0f, 220f, 30f);
            Slider musicSlider = musicSliderRect.gameObject.AddComponent<Slider>();
            musicSlider.direction = Slider.Direction.LeftToRight;
            musicSlider.minValue = 0f;
            musicSlider.maxValue = 1f;
            musicSlider.wholeNumbers = false;
            RectTransform rail = Center("Дорожка", musicSliderRect, 0, 0, 220, 9);
            AddImage(rail, new Color(.21f, .25f, .29f, 1f), false);
            RectTransform fillArea = UI("Fill Area", musicSliderRect,
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-10f, -8f), new Vector2(.5f, .5f));
            RectTransform fill = UI("Fill", fillArea,
                new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
            AddImage(fill, new Color(.98f, .79f, .54f, 1f), false);
            musicSlider.fillRect = fill;
            RectTransform handleArea = UI("Handle Slide Area", musicSliderRect,
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-16f, 0f), new Vector2(.5f, .5f));
            RectTransform handle = Center("Handle", handleArea, 0f, 0f, 25f, 27f);
            AddImage(handle, new Color(.98f, .91f, .79f), true);
            musicSlider.handleRect = handle;
            musicSlider.targetGraphic = handle.GetComponent<Image>();
            Text musicPercent = AddText(Center("Текущая громкость", soundPanel,
                197f, 0f, 64f, 43f), "31%", 22, TextAnchor.MiddleCenter,
                new Color(.98f, .88f, .74f), font);
            soundPanel.gameObject.SetActive(false);

            RectTransform ffRect = Center("⏩ Перемотка вперёд", ui,
                -640f, 480f, 220f, 57f);
            AddImage(ffRect, new Color(.065f, .075f, .10f, .74f), true);
            Button fastForward = AddButton(ffRect);
            Text fastForwardText = AddText(Center("Надпись Перемотка", ffRect, 0, 0, 210f, 49f),
                "⏩ Вперёд", 23, TextAnchor.MiddleCenter,
                new Color(.98f, .91f, .81f), font);

            RectTransform menu = Stretch("Меню — только исходная заставка", ui);
            Button startButton = PaintedHotspot("НАЧАТЬ (невидимая кнопка на рисунке)", menu, 375, 91, 520, 96);
            Button exitButton = PaintedHotspot("ВЫХОД (невидимая кнопка на рисунке)", menu, 375, -24, 500, 98);

            RectTransform ending = Stretch("Экран концовки", ui);
            AddImage(ending, Color.black, true);
            RawImage endingImage = AddTexture(Stretch("CG концовки — меняй изображение здесь", ending),
                Resources.Load<Texture2D>("Art/Backgrounds/ending_good"));
            // Тонкая полупрозрачная полоса внизу, не заслоняющая героев и обстановку.
            RectTransform endingPanel = UI("Подложка подписей", ending,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, 332f), new Vector2(.5f, 0f));
            AddImage(endingPanel, new Color(.015f, .019f, .033f, .76f), false);
            Text endingTitle = AddText(Center("Название концовки", ending,
                0f, -271f, 1540f, 80f), "", 57, TextAnchor.MiddleCenter,
                new Color(1f, .91f, .75f), font);
            Text result = AddText(Center("Короткий итог", ending,
                0f, -341f, 1540f, 57f), "", 29, TextAnchor.MiddleCenter,
                new Color(.96f, .95f, .93f), font);
            Text retryLabel, menuLabel;
            Button retry = DrawButton("Повторить четвёртый день", ending,
                -280f, -451f, 500f, 66f, "ЕЩЁ ОДНА ПОПЫТКА", font, out retryLabel);
            Button back = DrawButton("Главное меню", ending,
                280f, -451f, 500f, 66f, "В ГЛАВНОЕ МЕНЮ", font, out menuLabel);
            ending.gameObject.SetActive(false);
            // Настройки звука и перемотка поверх меню и финала, а не за ними.
            soundToggleRect.SetAsLastSibling();
            soundPanel.SetAsLastSibling();
            ffRect.SetAsLastSibling();

            AudioSource[] soundSources = controller.GetComponents<AudioSource>();
            AudioSource music = soundSources.Length > 0 ? soundSources[0] : controller.gameObject.AddComponent<AudioSource>();
            AudioSource impact = soundSources.Length > 1 ? soundSources[1] : controller.gameObject.AddComponent<AudioSource>();
            AudioSource alarm = soundSources.Length > 2 ? soundSources[2] : controller.gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.volume = .31f;
            impact.playOnAwake = false;
            impact.loop = false;
            impact.volume = .5202f;
            alarm.playOnAwake = false;
            alarm.loop = false;
            alarm.spatialBlend = 0f;
            alarm.volume = .58f;
            // Редактор сохраняет объектные ссылки в самом компоненте игры.
            SerializedObject so = new SerializedObject(controller);
            Assign(so, "canvas", canvas);
            Assign(so, "bg", bg);
            Assign(so, "shade", shade);
            Assign(so, "menu", menu.gameObject);
            Assign(so, "story", story.gameObject);
            Assign(so, "choices", choices.gameObject);
            Assign(so, "ending", ending.gameObject);
            Assign(so, "truck", crash);
            Assign(so, "maxActor", images[0]);
            Assign(so, "olgaActor", images[1]);
            Assign(so, "leraActor", images[2]);
            Assign(so, "pashaActor", images[3]);
            Assign(so, "startButton", startButton);
            Assign(so, "exitButton", exitButton);
            Assign(so, "next", dialogueButton);
            Assign(so, "nameText", speaker);
            Assign(so, "lineText", line);
            AssignArray(so, "choiceButtons", optionButtons);
            AssignArray(so, "choiceLabels", optionTexts);
            Assign(so, "endingArtwork", endingImage);
            Assign(so, "endingTitleText", endingTitle);
            Assign(so, "resultText", result);
            Assign(so, "retryButton", retry);
            Assign(so, "backButton", back);
            Assign(so, "music", music);
            Assign(so, "hit", impact);
            Assign(so, "alarm", alarm);
            Assign(so, "musicSettingsButton", soundToggle);
            Assign(so, "musicSettingsPanel", soundPanel.gameObject);
            Assign(so, "musicVolumeSlider", musicSlider);
            Assign(so, "musicVolumePercent", musicPercent);
            Assign(so, "fastForwardButton", fastForward);
            Assign(so, "fastForwardLabel", fastForwardText);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = ui.gameObject;
            EditorGUIUtility.PingObject(ui.gameObject);
            Debug.Log("Второй шанс: создан редактируемый Canvas, реальные UI-кнопки и персонажи в Hierarchy. Сцена сохранена.");
        }
        catch (Exception ex) { Debug.LogException(ex); }
        finally { busy = false; }
    }
}
#endif
