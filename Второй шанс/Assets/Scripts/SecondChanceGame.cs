using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// «Второй шанс». История и смена состояний редактируемых элементов сцены.
/// Все UI-объекты создаются и СОХРАНЯЮТСЯ в Main.unity редакторским сборщиком.
/// Ничего не создаётся заново в Play Mode.
/// </summary>
public sealed class SecondChanceGame : MonoBehaviour
{
    [Serializable] private sealed class Save
    {
        public int day = 1;
        // Отдельно сохраняем решения всех четырёх возможных четвергов.
        public int[] max = { -1, -1, -1, -1 };
        public int[] boss = { -1, -1, -1, -1 };
        public int[] lera = { -1, -1, -1, -1 };
        public int[] leraFollow = { -1, -1, -1, -1 };
        public int[] maxFollow = { -1, -1, -1, -1 };
        public int[] screenFollow = { -1, -1, -1, -1 };
        public int[] bossFollow = { -1, -1, -1, -1 };
        public int[] crossing = { -1, -1, -1, -1 };
        // Четвёртый день повторяется, пока герой не изменит ВСЕ ключевые решения.
        public int fourthFailures = 0;
        public bool goodUnlocked;
        public bool badUnlocked;
        // Уникальные реплики, которые игрок ДЕЙСТВИТЕЛЬНО нажимал.
        // Отдельно от числовых массивов — индекс ответа меняется между циклами.
        public List<string> seenChoices = new List<string>();
    }

    [Header("Полностью редактируемая сцена")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RawImage bg;
    [SerializeField] private Image shade;
    [SerializeField] private GameObject menu;
    [SerializeField] private GameObject story;
    [SerializeField] private GameObject choices;
    [SerializeField] private GameObject ending;
    [SerializeField] private RawImage truck;
    [SerializeField] private RawImage maxActor;
    [SerializeField] private RawImage olgaActor;
    [SerializeField] private RawImage leraActor;
    [SerializeField] private RawImage pashaActor;
    [Header("Паша: положение на экране 1920x1080")]
    [Tooltip("Смещение спрайта Паши вправо/влево и вверх/вниз. Изменяется в Inspector на Second Chance Game.")]
    [SerializeField] private Vector2 pashaScreenPosition = new Vector2(440f, -8f);

    [Header("Меню: невидимые кнопки на рисунке")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button exitButton;
    [Header("Диалог: один полупрозрачный блок")]
    [SerializeField] private Button next;
    [SerializeField] private Text nameText;
    [SerializeField] private Text lineText;
    [Header("Выбор: все пять кнопок доступны в Hierarchy")]
    [SerializeField] private Button[] choiceButtons = new Button[5];
    [SerializeField] private Text[] choiceLabels = new Text[5];
    [Header("Финальные иллюстрации и подписи")]
    [SerializeField] private RawImage endingArtwork;
    [SerializeField] private Text endingTitleText;
    [SerializeField] private Text resultText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button backButton;
    [Header("Звуки — можно редактировать Audio Source")]
    [SerializeField] private AudioSource music;
    [SerializeField] private AudioSource hit;
    [SerializeField] private AudioSource alarm;

    [Header("Звук: настройки в Hierarchy → VN Canvas")]
    [SerializeField] private Button musicSettingsButton;
    [SerializeField] private GameObject musicSettingsPanel;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Text musicVolumePercent;

    [Header("Перемотка / автопролистывание")]
    [SerializeField] private Button fastForwardButton;
    [SerializeField] private Text fastForwardLabel;

    private Save save;
    private AudioClip impactClip;
    private AudioClip alarmClip;
    private readonly Dictionary<string, RawImage> actors = new Dictionary<string, RawImage>();
    private int answer = -1, day;
    private bool continueLine;
    private bool banner, slides, venue, safeRoad, late, personalNews, leraComforted, leraUpset;
    private int crossingChoice;
    private string bannerType, bossType;
    private Coroutine running;
    private int lastAdvanceFrame = -1;
    private int lastMenuClickFrame = -1;
    private int dreamChoiceIndex;
    private string pendingChoiceGroup;
    private readonly HashSet<string> castOnLocation = new HashSet<string>();
    private string currentBackgroundId;
    // Один общий ползунок для фоновой музыки и звуковых эффектов.
    // Значение 0 должно давать абсолютную тишину на КАЖДОМ AudioSource.
    private float musicVolume = 0.31f;
    private const float TruckSfxMaxVolume = 0.5202f;
    private const float AlarmSfxMaxVolume = 0.58f;
    private const float MusicDuckingFactor = 0.35f;
    private bool musicDuckedForImpact;
    private bool fastForwardEnabled;
    private float nextFastForwardAt;

    // Resilience for older/partially serialized Main.unity scenes. Unity may load
    // the Canvas, but lose references on Second Chance Game after a patch.
    // Rebinding existing objects does NOT rebuild or overwrite the user's layout.
    private static T FindUI<T>(Transform parent, string path) where T : Component
    {
        if (parent == null) return null;
        Transform child = parent.Find(path);
        return child == null ? null : child.GetComponent<T>();
    }

    private static GameObject FindUIObject(Transform parent, string path)
    {
        if (parent == null) return null;
        Transform child = parent.Find(path);
        return child == null ? null : child.gameObject;
    }

    private void RestoreExistingSceneReferences()
    {
        if (canvas == null)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name == "VN Canvas")
                {
                    canvas = root.GetComponent<Canvas>();
                    break;
                }
            }
        }
        if (canvas == null) return;
        Transform rootUI = canvas.transform;
        if (bg == null) bg = FindUI<RawImage>(rootUI, "Фон — меняй здесь картинку");
        if (shade == null) shade = FindUI<Image>(rootUI, "Лёгкое затемнение (прозрачность)");
        if (story == null) story = FindUIObject(rootUI, "Диалоги");
        if (menu == null) menu = FindUIObject(rootUI, "Меню — только исходная заставка");
        if (ending == null) ending = FindUIObject(rootUI, "Экран концовки");
        if (choices == null) choices = FindUIObject(rootUI, "Диалоги/Варианты ответов (редактируемые)");
        if (truck == null) truck = FindUI<RawImage>(rootUI, "Диалоги/Столкновение — 2D грузовик");
        if (maxActor == null) maxActor = FindUI<RawImage>(rootUI, "Диалоги/Спрайты персонажей/Макс — спрайт");
        if (olgaActor == null) olgaActor = FindUI<RawImage>(rootUI, "Диалоги/Спрайты персонажей/Ольга Викторовна — спрайт");
        if (leraActor == null) leraActor = FindUI<RawImage>(rootUI, "Диалоги/Спрайты персонажей/Лера — спрайт");
        if (pashaActor == null) pashaActor = FindUI<RawImage>(rootUI, "Диалоги/Спрайты персонажей/Паша — водитель — спрайт");
        if (startButton == null) startButton = FindUI<Button>(rootUI, "Меню — только исходная заставка/НАЧАТЬ (невидимая кнопка на рисунке)");
        if (exitButton == null) exitButton = FindUI<Button>(rootUI, "Меню — только исходная заставка/ВЫХОД (невидимая кнопка на рисунке)");
        if (next == null) next = FindUI<Button>(rootUI, "Диалоги/Окно диалога (клик = далее)");
        if (nameText == null) nameText = FindUI<Text>(rootUI, "Диалоги/Окно диалога (клик = далее)/Имя говорящего");
        if (lineText == null) lineText = FindUI<Text>(rootUI, "Диалоги/Окно диалога (клик = далее)/Текст диалога");
        if (choiceButtons == null || choiceButtons.Length != 5) choiceButtons = new Button[5];
        if (choiceLabels == null || choiceLabels.Length != 5) choiceLabels = new Text[5];
        for (int i = 0; i < 5; i++)
        {
            string choicePath = "Диалоги/Варианты ответов (редактируемые)/Ответ " + (i + 1);
            if (choiceButtons[i] == null) choiceButtons[i] = FindUI<Button>(rootUI, choicePath);
            if (choiceLabels[i] == null) choiceLabels[i] = FindUI<Text>(rootUI, choicePath + "/Надпись");
        }
        if (endingArtwork == null) endingArtwork = FindUI<RawImage>(rootUI, "Экран концовки/CG концовки — меняй изображение здесь");
        if (endingTitleText == null) endingTitleText = FindUI<Text>(rootUI, "Экран концовки/Название концовки");
        if (resultText == null) resultText = FindUI<Text>(rootUI, "Экран концовки/Короткий итог");
        if (retryButton == null) retryButton = FindUI<Button>(rootUI, "Экран концовки/Повторить четвёртый день");
        if (backButton == null) backButton = FindUI<Button>(rootUI, "Экран концовки/Главное меню");
        if (musicSettingsButton == null) musicSettingsButton = FindUI<Button>(rootUI, "♫ Настройки музыки");
        if (musicSettingsPanel == null) musicSettingsPanel = FindUIObject(rootUI, "Панель громкости музыки");
        if (musicVolumeSlider == null) musicVolumeSlider = FindUI<Slider>(rootUI, "Панель громкости музыки/Ползунок музыки");
        if (musicVolumePercent == null) musicVolumePercent = FindUI<Text>(rootUI, "Панель громкости музыки/Текущая громкость");
        if (fastForwardButton == null) fastForwardButton = FindUI<Button>(rootUI, "⏩ Перемотка вперёд");
        if (fastForwardLabel == null) fastForwardLabel = FindUI<Text>(rootUI, "⏩ Перемотка вперёд/Надпись Перемотка");
        AudioSource[] sources = GetComponents<AudioSource>();
        if (music == null && sources.Length > 0) music = sources[0];
        if (hit == null && sources.Length > 1) hit = sources[sources.Length - 2];
        if (alarm == null && sources.Length > 2) alarm = sources[sources.Length - 1];
    }

    private void Awake()
    {
        RestoreExistingSceneReferences();
        if (canvas == null || bg == null || story == null || menu == null || choices == null || ending == null ||
            next == null || nameText == null || lineText == null || maxActor == null || olgaActor == null || leraActor == null || pashaActor == null ||
            music == null || hit == null || alarm == null || startButton == null || exitButton == null ||
            musicSettingsButton == null || musicSettingsPanel == null || musicVolumeSlider == null ||
            fastForwardButton == null || fastForwardLabel == null ||
            endingArtwork == null || endingTitleText == null || resultText == null)
        {
            Debug.LogError("Не назначены элементы сцены. Откройте Tools → Второй шанс → Пересобрать редактируемую сцену.", this);
            enabled = false;
            return;
        }
        actors.Add("max", maxActor);
        actors.Add("olga", olgaActor);
        actors.Add("lera", leraActor);
        actors.Add("pasha", pashaActor);
        startButton.onClick.AddListener(StartOver);
        exitButton.onClick.AddListener(Exit);
        next.onClick.AddListener(Advance);
        if (retryButton != null) retryButton.onClick.AddListener(Retry);
        if (backButton != null) backButton.onClick.AddListener(ShowMenu);
        for (int i = 0; i < choiceButtons.Length; ++i)
        {
            int chosen = i;
            if (choiceButtons[i] != null) choiceButtons[i].onClick.AddListener(() => { if (answer < 0) answer = chosen; });
        }
        music.loop = true;
        music.playOnAwake = false;
        hit.loop = false;
        hit.playOnAwake = false;
        hit.spatialBlend = 0f;
        impactClip = Resources.Load<AudioClip>("Audio/automobile");
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("secondchance_musicvolume", 0.31f));
        musicVolumeSlider.SetValueWithoutNotify(musicVolume);
        ApplyMusicVolume(musicVolume);
        musicVolumeSlider.onValueChanged.AddListener(ApplyMusicVolume);
        musicSettingsButton.onClick.AddListener(() =>
            musicSettingsPanel.SetActive(!musicSettingsPanel.activeSelf));
        musicSettingsPanel.SetActive(false);
        fastForwardButton.onClick.AddListener(ToggleFastForward);
        SetFastForward(false);
        alarm.playOnAwake = false;
        alarm.loop = false;
        alarm.spatialBlend = 0f;
        alarmClip = Resources.Load<AudioClip>("Audio/alarm_clock");
        ApplyAllAudioVolumes();
        // Существующая Canvas-сцена совместима с патчем: меняется лишь надпись,
        // а названия и ссылки объектов в иерархии остаются прежними.
        Text soundButtonText = FindUI<Text>(canvas.transform,
            "♫ Настройки музыки/Надпись Музыка");
        if (soundButtonText != null) soundButtonText.text = "♫ Звук";
        try { save = JsonUtility.FromJson<Save>(PlayerPrefs.GetString("secondchance_save", "")); }
        catch (Exception ex) { Debug.LogWarning("Невозможно прочитать сохранение: " + ex.Message); }
        if (save == null) save = new Save();
        // Старые версии проекта сохраняли массивы длиной 3. Продлеваем, не стирая их.
        save.max = FixHistory(save.max);
        save.boss = FixHistory(save.boss);
        save.lera = FixHistory(save.lera);
        save.leraFollow = FixHistory(save.leraFollow);
        save.maxFollow = FixHistory(save.maxFollow);
        save.screenFollow = FixHistory(save.screenFollow);
        save.bossFollow = FixHistory(save.bossFollow);
        save.crossing = FixHistory(save.crossing);
        save.day = Mathf.Clamp(save.day, 1, 4);
        if (save.seenChoices == null) save.seenChoices = new List<string>();
        UpgradeRememberedChoices();
    }


    private static int[] FixHistory(int[] old)
    {
        var result = new[] { -1, -1, -1, -1 };
        if (old != null) Array.Copy(old, result, Mathf.Min(old.Length, result.Length));
        return result;
    }

    private void ApplyMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        ApplyAllAudioVolumes();
        if (musicVolumePercent != null)
            musicVolumePercent.text = Mathf.RoundToInt(musicVolume * 100f).ToString() + "%";
        PlayerPrefs.SetFloat("secondchance_musicvolume", musicVolume);
        PlayerPrefs.Save();
    }

    private void ApplyAllAudioVolumes()
    {
        // Никакой фиксированной громкости в сцене столкновения: иначе при 0%
        // звук внезапно возвращался в «Дне сурка» после первого грузовика.
        float master = Mathf.Clamp01(musicVolume);
        if (music != null)
            music.volume = master * (musicDuckedForImpact ? MusicDuckingFactor : 1f);
        if (hit != null) hit.volume = master * TruckSfxMaxVolume;
        if (alarm != null) alarm.volume = master * AlarmSfxMaxVolume;
    }

    private void RefreshFastForwardAvailability()
    {
        // Никакой перемотки поверх главного меню и финальных экранов.
        bool available = save != null && day > 1 &&
            story != null && story.activeInHierarchy &&
            menu != null && !menu.activeInHierarchy &&
            ending != null && !ending.activeInHierarchy;
        if (!available) SetFastForward(false);
        if (fastForwardButton != null)
            fastForwardButton.gameObject.SetActive(available);
    }

    private Color SpeakerColor(string who)
    {
        switch (who)
        {
            case "Артём": return new Color(.72f, .87f, 1f);
            case "Артём (мысли)": return new Color(.72f, .87f, 1f);
            case "Макс": return new Color(1f, .78f, .35f);
            case "Лера": return new Color(.64f, .95f, .70f);
            case "Ольга Викторовна": return new Color(1f, .70f, .86f);
            case "Паша": return new Color(.48f, .89f, .94f);
            default: return Color.white;
        }
    }

    private void ToggleFastForward()
    {
        SetFastForward(!fastForwardEnabled);
    }

    private void SetFastForward(bool enabledState)
    {
        bool available = fastForwardButton != null && fastForwardButton.gameObject.activeSelf;
        fastForwardEnabled = available && enabledState;
        nextFastForwardAt = Time.unscaledTime + 0.25f;
        if (fastForwardLabel != null)
            fastForwardLabel.text = fastForwardEnabled ? "⏩ Вперёд: ВКЛ" : "⏩ Вперёд";
        if (fastForwardButton != null)
        {
            Image image = fastForwardButton.GetComponent<Image>();
            if (image != null)
                image.color = fastForwardEnabled
                    ? new Color(.23f, .33f, .20f, .88f)
                    : new Color(.065f, .075f, .10f, .74f);
        }
    }

    private void Start() { if (enabled) ShowMenu(); }

    private void Update()
    {
        Vector2 pointer;
        bool released = MouseReleased(out pointer);
        if (FastForwardTogglePressed())
            ToggleFastForward();
        if (fastForwardEnabled && continueLine && story != null && story.activeInHierarchy &&
            (choices == null || !choices.activeInHierarchy) && Time.unscaledTime >= nextFastForwardAt)
        {
            Advance();
            nextFastForwardAt = Time.unscaledTime + 0.05f;
        }
        if (menu != null && menu.activeInHierarchy)
        {
            // Кнопки также работают напрямую через мышь, даже если EventSystem сломан.
            if (released && Inside(startButton, pointer)) { StartOver(); return; }
            if (released && Inside(exitButton, pointer)) { Exit(); return; }
            if (AcceptPressed()) StartOver();
            else if (EscapePressed()) Exit();
            else if (ContinuePressed() && save != null && save.day > 1) Resume();
            return;
        }
        if (choices != null && choices.activeInHierarchy && answer < 0)
        {
            for (int i = 0; i < choiceButtons.Length; ++i)
            {
                Button button = choiceButtons[i];
                if (button == null || !button.gameObject.activeInHierarchy) continue;
                if (NumberPressed(i + 1) || (released && Inside(button, pointer)))
                {
                    answer = i;
                    break;
                }
            }
            return;
        }
        if (continueLine && (AcceptPressed() || (released && Inside(next, pointer)))) Advance();
    }

    private void Advance()
    {
        // Одно физическое нажатие не должно пролистывать две реплики.
        if (lastAdvanceFrame == Time.frameCount) return;
        if (continueLine) { continueLine = false; lastAdvanceFrame = Time.frameCount; }
    }

    private static bool Inside(Button button, Vector2 point)
    {
        return button != null && button.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(button.GetComponent<RectTransform>(), point, null);
    }

    private static bool MouseReleased(out Vector2 point)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
        {
            point = mouse.position.ReadValue();
            return true;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonUp(0))
        {
            point = Input.mousePosition;
            return true;
        }
#endif
        point = Vector2.zero;
        return false;
    }

    // НЕ вызываем UnityEngine.Input, когда активен только новый Input System.
    private static bool AcceptPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
        return false;
#endif
    }
    private static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }
    private static bool ContinuePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.C);
#else
        return false;
#endif
    }
    private static bool FastForwardTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.tabKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.F);
#else
        return false;
#endif
    }
    private static bool NumberPressed(int number)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return false;
        switch (number)
        {
            case 1: return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
            case 2: return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
            case 3: return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
            case 4: return keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame;
            case 5: return keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame;
        }
        return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
        switch (number)
        {
            case 1: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
            case 2: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
            case 3: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
            case 4: return Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4);
            case 5: return Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5);
        }
        return false;
#else
        return false;
#endif
    }

    private void Store()
    {
        PlayerPrefs.SetString("secondchance_save", JsonUtility.ToJson(save));
        PlayerPrefs.Save();
    }
    private void ShowMenu()
    {
        if (running != null) { StopCoroutine(running); running = null; }
        story.SetActive(false);
        ending.SetActive(false);
        choices.SetActive(false);
        menu.SetActive(true);
        foreach (RawImage image in actors.Values) image.gameObject.SetActive(false);
        bg.texture = Picture("Art/Backgrounds/title");
        bg.color = Color.white;
        if (shade != null) shade.color = Color.clear;
        if (musicSettingsPanel != null) musicSettingsPanel.SetActive(false);
        if (musicSettingsButton != null) musicSettingsButton.gameObject.SetActive(true);
        SetFastForward(false);
        // Ранее сохраняемый 4-й день включал кнопку прямо поверх заставки.
        if (fastForwardButton != null) fastForwardButton.gameObject.SetActive(false);
        RefreshFastForwardAvailability();
        music.Stop();
        hit.Stop();
        alarm.Stop();
        musicDuckedForImpact = false;
        ApplyAllAudioVolumes();
    }
    private void StartOver()
    {
        if (lastMenuClickFrame == Time.frameCount) return;
        lastMenuClickFrame = Time.frameCount;
        save = new Save();
        Store();
        Resume();
    }
    private void Retry()
    {
        // После «Дня сурка» четвёртый цикл запускается заново,
        // но память о предыдущих попытках и пометки «уже выбирали» сохраняются.
        save.day = 4;
        Store();
        Resume();
    }
    private void Resume()
    {
        if (running != null) StopCoroutine(running);
        menu.SetActive(false);
        ending.SetActive(false);
        choices.SetActive(false);
        story.SetActive(true);
        // End() may have hidden this in older save sessions. Always restore it.
        if (musicSettingsButton != null) musicSettingsButton.gameObject.SetActive(true);
        if (musicSettingsPanel != null) musicSettingsPanel.SetActive(false);
        if (shade != null) shade.color = new Color(.02f, .03f, .05f, .09f);
        musicDuckedForImpact = false;
        ApplyAllAudioVolumes();
        day = save != null ? save.day : 1;
        RefreshFastForwardAvailability();
        nextFastForwardAt = Time.unscaledTime + 0.45f;
        running = StartCoroutine(Game());
    }
    private void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    // Unity 2D projects can import a PNG as either Texture2D or Sprite.
    // The game uses RawImage, so convert both import variants to a Texture2D.
    private Texture2D Picture(string path)
    {
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture != null) return texture;
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null) return sprite.texture;
        Debug.LogError("[Second Chance] Не загружено изображение Resources/" + path +
                       ". Проверьте импорт PNG и содержимое сборки.");
        return null;
    }
    private void SetBackground(string id)
    {
        currentBackgroundId = id;
        Texture2D picture = Picture("Art/Backgrounds/" + id);
        bg.texture = picture;
        bg.color = picture != null ? Color.white : new Color(.17f, .22f, .31f);
    }
    private void Place(string image, string location, string clock, params string[] cast)
    {
        SetBackground(image);
        castOnLocation.Clear();
        foreach (RawImage actor in actors.Values) actor.gameObject.SetActive(false);
        for (int i = 0; i < cast.Length; ++i)
        {
            castOnLocation.Add(cast[i]);
            RawImage sprite;
            if (!actors.TryGetValue(cast[i], out sprite)) continue;
            sprite.gameObject.SetActive(true);
            float x = cast.Length == 1 ? 0f : cast.Length == 2 ? (i == 0 ? -390f : 390f) : -590f + i * 590f;
            sprite.rectTransform.anchorMin = new Vector2(.5f, 0f);
            sprite.rectTransform.anchorMax = new Vector2(.5f, 0f);
            sprite.rectTransform.pivot = new Vector2(.5f, 0f);
            sprite.rectTransform.localScale = Vector3.one;
            sprite.rectTransform.anchoredPosition = new Vector2(x, -8f);
            Mood(cast[i], "neutral");
        }
    }
    private void HideActor(string id)
    {
        RawImage actor;
        if (actors.TryGetValue(id, out actor)) actor.gameObject.SetActive(false);
    }

    // Показывать спрайт Паши только во время его реплик, а не вместе с грузовиком.
    private void ShowPasha(string mood = "neutral")
    {
        if (pashaActor == null) return;
        pashaActor.gameObject.SetActive(true);
        RectTransform rect = pashaActor.rectTransform;
        rect.anchorMin = new Vector2(.5f, 0f);
        rect.anchorMax = new Vector2(.5f, 0f);
        rect.pivot = new Vector2(.5f, 0f);
        rect.anchoredPosition = pashaScreenPosition;
        rect.localScale = Vector3.one;
        Mood("pasha", mood);
    }

    private void RingAlarm()
    {
        if (alarmClip == null || alarm == null) return;
        alarm.Stop();
        alarm.clip = alarmClip;
        ApplyAllAudioVolumes();
        alarm.Play();
    }

    private void Mood(string id, string mood)
    {
        RawImage image;
        if (!actors.TryGetValue(id, out image) || !image.gameObject.activeSelf) return;
        if (id == "lera" && mood == "grateful") mood = "smile";
        // Все оригинальные эмоции Макса используются как задумано.
        // Файлы max_disappointed.png и max_relieved.png сохранены;
        // различное разрешение PNG компенсируется масштабированием с сохранением пропорций.
        string requestedPath = "Art/Characters/" + id + "_" + mood;
        Texture2D texture = Picture(requestedPath);
        if (texture == null && mood != "neutral")
            texture = Picture("Art/Characters/" + id + "_neutral");
        image.texture = texture;
        if (texture == null)
        {
            Debug.LogError("Отсутствует спрайт персонажа: Assets/Resources/Art/Characters/" + id + "_neutral.png");
            image.color = Color.clear;
            return;
        }
        // В «Маяке» одни и те же вечерние сумерки освещают ВСЕХ персонажей.
        // Лера теперь светлее прежнего (.74), а Макс и Ольга чуть приглушены.
        // Это одинаковый цветовой коэффициент для всех трёх спрайтов.
        if (currentBackgroundId == "mayak" &&
            (id == "lera" || id == "max" || id == "olga"))
        {
            image.color = new Color(.90f, .88f, .90f, 1f);
        }
        else if (currentBackgroundId == "evening_street" && id == "pasha")
        {
            image.color = new Color(.92f, .89f, .90f, 1f);
        }
        else
        {
            image.color = Color.white;
        }
        // Сохраняем точное соотношение ширины и высоты исходного PNG.
        const float height = 930f;
        image.rectTransform.sizeDelta = new Vector2(height * texture.width / (float)texture.height, height);
    }
    private void PlaySong(string id)
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + id);
        if (clip == null) { Debug.LogWarning("Музыка не найдена: " + id); return; }
        music.Stop();
        music.clip = clip;
        music.loop = true;
        musicDuckedForImpact = false;
        ApplyAllAudioVolumes();
        music.Play();
    }
    private IEnumerator Say(string who, string line, string mood = null, bool isNarration = false)
    {
        // Пунктуация не определяет эмоцию: благодарность с «!» — не паника.
        // Для сцены аварии явным образом передаётся mood="alert".
        if (who == "Паша" && mood == null) mood = "neutral";
        string visibleActor = who == "Макс" ? "max"
            : who == "Лера" ? "lera"
            : who == "Ольга Викторовна" ? "olga" : null;
        // Ensure a speaking character is visible even if previous story branches
        // did not include them in castOnLocation. This also works in built players.
        if (visibleActor != null)
        {
            RawImage inScene;
            if (actors.TryGetValue(visibleActor, out inScene) && inScene != null)
            {
                bool wasHidden = !inScene.gameObject.activeSelf;
                if (wasHidden) inScene.gameObject.SetActive(true);
                if (wasHidden)
                {
                    inScene.rectTransform.localScale = Vector3.one;
                    if (!castOnLocation.Contains(visibleActor))
                        inScene.rectTransform.anchoredPosition = new Vector2(0f, -8f);
                }
                if (wasHidden || inScene.texture == null)
                    Mood(visibleActor, mood ?? "neutral");
            }
        }
        if (mood != null)
        {
            if (who == "Макс") Mood("max", mood);
            else if (who == "Лера") Mood("lera", mood);
            else if (who == "Ольга Викторовна") Mood("olga", mood);
            else if (who == "Паша") Mood("pasha", mood);
        }
        if (who == "Паша" && pashaActor != null && !pashaActor.gameObject.activeSelf)
            ShowPasha(mood ?? "neutral");
        nameText.color = SpeakerColor(who);
        nameText.text = who;
        nameText.gameObject.SetActive(!string.IsNullOrEmpty(who));
        // Обстоятельства: курсив без имени. Мысли: «Артём (мысли)» обычным текстом.
        lineText.fontStyle = isNarration ? FontStyle.Italic : FontStyle.Normal;
        lineText.text = line.Replace(';', ',');
        next.interactable = true;
        choices.SetActive(false);
        continueLine = true;
        nextFastForwardAt = Time.unscaledTime + (fastForwardEnabled ? 0.32f : 9999f);
        while (continueLine) yield return null;
    }
    private IEnumerator Thought(string line) { yield return Say("Артём (мысли)", line); }
    private IEnumerator Hero(string line) { yield return Say("Артём", line); }
    private IEnumerator Narrate(string line) { yield return Say("", line, null, true); }
    private string HistoryGroup(int[] history)
    {
        if (object.ReferenceEquals(history, save.max)) return "max";
        if (object.ReferenceEquals(history, save.maxFollow)) return "max_delivery";
        if (object.ReferenceEquals(history, save.screenFollow)) return "max_screen";
        if (object.ReferenceEquals(history, save.boss)) return "olga";
        if (object.ReferenceEquals(history, save.bossFollow)) return "olga_follow";
        if (object.ReferenceEquals(history, save.lera)) return "lera";
        if (object.ReferenceEquals(history, save.leraFollow)) return "lera_follow";
        if (object.ReferenceEquals(history, save.crossing)) return "crossing";
        return "unknown";
    }

    // Главные решения сохраняются по СМЫСЛУ, а не по номеру кнопки / формулировке.
    // Порядок смыслов в max, olga, lera и crossing одинаков во всех циклах.
    // Дополнительные уточняющие решения, напротив, запоминаются по тексту внутри своей ветки:
    // например, «заказать доставку» и «подготовить экран» не смешиваются.
    private static string ChoiceMemoryKey(string group, int index, string variant)
    {
        if (group == "max" || group == "olga" || group == "lera" || group == "crossing")
            return "intent.v2::" + group + "::" + index;
        return group + "::" + variant;
    }

    private static int PreviousChoiceMeaning(string group, string variant)
    {
        // Миграция из v7: сохраняем и прошлые выборы четвёртого круга,
        // в том числе когда соответствующий массив истории был позднее перезаписан.
        if (group == "max")
        {
            if (variant.StartsWith("Ладно") || variant.StartsWith("Я сам")) return 0;
            if (variant.StartsWith("Извини") || variant.StartsWith("Не могу")) return 1;
            if (variant.StartsWith("А ты узнавал") || variant.StartsWith("Давай сначала")) return 2;
        }
        else if (group == "olga")
        {
            if (variant.StartsWith("Это не моя") || variant.StartsWith("Сначала докажу")) return 0;
            if (variant.StartsWith("Хорошо") || variant.StartsWith("Я заново")) return 1;
            if (variant.StartsWith("Давайте")) return 2;
        }
        else if (group == "lera")
        {
            if (variant.StartsWith("Слушай, я тоже") || variant.StartsWith("Я тоже") || variant.StartsWith("Скажу ей")) return 0;
            if (variant.StartsWith("Давай после") || variant.StartsWith("Давай поговорим") || variant.StartsWith("Попрошу")) return 1;
            if (variant.StartsWith("Давай сначала") || variant.StartsWith("Выслушаю")) return 2;
        }
        else if (group == "crossing")
        {
            if (variant.StartsWith("Он ещё")) return 0;
            if (variant.StartsWith("Лучше подожду")) return 1;
            if (variant.StartsWith("Пойду к следующему")) return 2;
        }
        return -1;
    }

    private void UpgradeRememberedChoices()
    {
        if (save.seenChoices == null) save.seenChoices = new List<string>();
        // История старой версии хранит номера внутри одного семейства решений.
        AddPastIntents("max", save.max);
        AddPastIntents("olga", save.boss);
        AddPastIntents("lera", save.lera);
        AddPastIntents("crossing", save.crossing);
        var migrated = new List<string>();
        foreach (string oldKey in save.seenChoices)
        {
            if (string.IsNullOrEmpty(oldKey)) continue;
            int separator = oldKey.IndexOf("::", StringComparison.Ordinal);
            if (separator < 0) continue;
            string group = oldKey.Substring(0, separator);
            string label = oldKey.Substring(separator + 2);
            int meaning = PreviousChoiceMeaning(group, label);
            if (meaning >= 0)
            {
                string key = ChoiceMemoryKey(group, meaning, label);
                if (!save.seenChoices.Contains(key) && !migrated.Contains(key)) migrated.Add(key);
            }
        }
        save.seenChoices.AddRange(migrated);
    }

    private void AddPastIntents(string group, int[] history)
    {
        if (history == null) return;
        foreach (int oldChoice in history)
        {
            if (oldChoice < 0 || oldChoice > 2) continue;
            string key = ChoiceMemoryKey(group, oldChoice, "");
            if (!save.seenChoices.Contains(key)) save.seenChoices.Add(key);
        }
    }

    // A previously selected answer stays selected-looking even after choosing it again.
    // Main choices keep their meaning across cycles, so use the persisted numeric history
    // as a fallback for older saves that did not include seenChoices.
    private bool WasChoicePreviouslySelected(string group, int index, string variant)
    {
        if (save.seenChoices != null &&
            (save.seenChoices.Contains(ChoiceMemoryKey(group, index, variant)) ||
             save.seenChoices.Contains(group + "::" + variant)))
            return true;

        int[] history = null;
        if (group == "max") history = save.max;
        else if (group == "olga") history = save.boss;
        else if (group == "lera") history = save.lera;
        else if (group == "crossing") history = save.crossing;
        if (history == null) return false;
        for (int previousDay = 0; previousDay < history.Length; previousDay++)
            if (history[previousDay] == index) return true;
        return false;
    }

    private IEnumerator PickHistory(int[] history, string question, params string[] variants)
    {
        pendingChoiceGroup = HistoryGroup(history);
        yield return Pick(question, variants);
    }

    private IEnumerator Pick(string question, params string[] variants)
    {
        string group = pendingChoiceGroup;
        pendingChoiceGroup = null;
        if (string.IsNullOrEmpty(group))
            group = "standalone:" + string.Join("|", variants);
        if (save.seenChoices == null) save.seenChoices = new List<string>();
        nameText.gameObject.SetActive(false);
        lineText.fontStyle = FontStyle.Normal;
        lineText.text = string.Empty;
        next.interactable = false;
        answer = -1;
        choices.SetActive(true);
        // Обновляем и старые сцены: никаких огромных чёрных подложек.
        RectTransform choiceLayout = choices.GetComponent<RectTransform>();
        if (choiceLayout != null)
        {
            choiceLayout.anchorMin = choiceLayout.anchorMax = new Vector2(.5f, .5f);
            choiceLayout.pivot = new Vector2(.5f, .5f);
            choiceLayout.sizeDelta = new Vector2(900f, 445f);
            choiceLayout.anchoredPosition = new Vector2(420f, 170f);
        }
        Image oldChoicePanel = choices.GetComponent<Image>();
        if (oldChoicePanel != null)
        {
            oldChoicePanel.color = Color.clear;
            oldChoicePanel.raycastTarget = false;
        }
        // Сдвигаем единственного собеседника влево, чтобы варианты его не закрывали.
        RawImage visibleSpeaker = null;
        int visibleCount = 0;
        foreach (RawImage actor in actors.Values)
        {
            if (actor != null && actor.gameObject.activeInHierarchy)
            {
                visibleSpeaker = actor;
                visibleCount++;
            }
        }
        Vector2 previousActorPosition = Vector2.zero;
        if (visibleCount == 1 && visibleSpeaker != null)
        {
            previousActorPosition = visibleSpeaker.rectTransform.anchoredPosition;
            visibleSpeaker.rectTransform.anchoredPosition = new Vector2(-430f, previousActorPosition.y);
        }
        int count = Mathf.Min(variants.Length, choiceButtons.Length);
        for (int i = 0; i < choiceButtons.Length; ++i)
        {
            bool visible = i < count;
            if (choiceButtons[i] == null) continue;
            choiceButtons[i].gameObject.SetActive(visible);
            if (!visible) continue;
            // No textual badge: dim the WHOLE button when this decision was chosen before.
            // Save data is append-only; repeating an answer never resets the visual mark.
            bool selectedEarlier = WasChoicePreviouslySelected(group, i, variants[i]);
            choiceLabels[i].supportRichText = false;
            choiceLabels[i].text = (i + 1) + ".  " + variants[i];
            Image choiceFill = choiceButtons[i].GetComponent<Image>();
            if (choiceFill != null)
                choiceFill.color = selectedEarlier
                    ? new Color(.77f, .73f, .66f, .97f)
                    : new Color(.98f, .95f, .88f, .96f);
            choiceLabels[i].color = selectedEarlier
                ? new Color(.25f, .25f, .27f)
                : new Color(.12f, .15f, .20f);
            // Unity's selected/highlighted Button state should not override the memory
            // colour when the same answer is picked on the next loop.
            var buttonColors = choiceButtons[i].colors;
            buttonColors.normalColor = Color.white;
            buttonColors.selectedColor = Color.white;
            buttonColors.highlightedColor = new Color(.98f, .97f, .94f);
            buttonColors.pressedColor = new Color(.92f, .90f, .86f);
            choiceButtons[i].colors = buttonColors;
            // Расстановка ответов сохраняет доступность персонажа слева.
            RectTransform optionRect = choiceButtons[i].GetComponent<RectTransform>();
            optionRect.anchorMin = optionRect.anchorMax = new Vector2(.5f, .5f);
            optionRect.pivot = new Vector2(.5f, .5f);
            // Немного выше: новые диалоговые ответы могут занимать две строки.
            optionRect.sizeDelta = new Vector2(860f, 84f);
            optionRect.anchoredPosition =
                new Vector2(0f, (count - 1) * 47f - i * 94f);
        }
        while (answer < 0) yield return null;
        if (answer >= 0 && answer < count)
        {
            string key = ChoiceMemoryKey(group, answer, variants[answer]);
            string legacyKey = group + "::" + variants[answer];
            // Только добавляем: повторное нажатие никогда не удаляет отметку.
            if (!save.seenChoices.Contains(key)) save.seenChoices.Add(key);
            if (!save.seenChoices.Contains(legacyKey)) save.seenChoices.Add(legacyKey);
            Store();
        }
        if (visibleCount == 1 && visibleSpeaker != null)
            visibleSpeaker.rectTransform.anchoredPosition = previousActorPosition;
        choices.SetActive(false);
    }

    private void ResetDay()
    {
        banner = slides = venue = safeRoad = late = false;
        personalNews = leraComforted = leraUpset = false;
        crossingChoice = -1;
        dreamChoiceIndex = 0;
        bannerType = "missing";
        bossType = "none";
        if (truck != null) truck.gameObject.SetActive(false);
        hit.Stop();
        if (alarm != null) alarm.Stop();
    }

    private IEnumerator Remember(int[] history, string accepted, string declined)
    {
        if (day == 2)
        {
            yield return Thought(history[0] == 0 ? accepted : declined);
            if (dreamChoiceIndex == 0)
                yield return Thought(history[0] == 0
                    ? "Тогда я согласился и всё усложнил. Стоит попробовать отказать."
                    : "Тогда я отказался и оставил человека одного. Попробую иначе.");
            else if (dreamChoiceIndex == 1)
                yield return Thought("Похоже, одних оправданий недостаточно. Надо выяснить, где возникла ошибка.");
            else
                yield return Thought("Каждый раз я тороплюсь ответить. Может, сначала дослушать её?");
            dreamChoiceIndex++;
        }
        else if (day == 3)
        {
            if (history[0] == 0 && history[1] == 0)
                yield return Thought("Два раза я соглашался. А последствия повторились. Может, я не вижу других вариантов?");
            else if (history[0] == 1 && history[1] == 1)
                yield return Thought("Два раза я отказывал. И два раза проблема оставалась. А можно решить её иначе?");
            else
                yield return Thought("Я уже и соглашался, и отказывал. Ни то ни другое не помогло. Нужно спросить о другом.");
        }
        else if (day == 4)
            yield return Thought("Я помню все прошлые разговоры. Кажется, людям вокруг по-прежнему приходится проживать их впервые.");
    }

    // Важное отличие от прежней версии: правильный переход через дорогу сам по себе
    // не разрывает петлю. Для выхода нужен ответственный, последовательный четверг.
    private bool FourthDaySolved()
    {
        return safeRoad && banner && !late && slides && venue && leraComforted && !leraUpset;
    }

    private IEnumerator Game()
    {
        while (true)
        {
            day = save.day;
            RefreshFastForwardAvailability();
            ResetDay();
            PlaySong(day <= 2 ? "first_two_days" : "third_day");
            yield return Morning();
            if (day == 4)
            {
                if (save.fourthFailures > 0)
                    yield return Thought("Снова этот четверг. Я даже не знаю, который раз.");

                // Два решения с разными последствиями: попробовать ещё раз или
                // прекратить попытки. Прежнее «останусь дома» повторяло отказ.
                yield return Pick("",
                    "Выйду и попробую разобраться со всем по-настоящему.",
                    "Я устал. Пусть этот день повторяется сколько угодно.");
                if (answer == 1)
                {
                    yield return GroundhogEnding();
                    yield break;
                }
                yield return Hero("Ладно. Ещё раз.");
            }
            yield return MeetMax();
            yield return MeetBoss();
            yield return MeetLera();
            yield return Opening();
            if (day <= 2)
            {
                yield return Crash();
                save.day = day + 1;
                Store();
                yield return Narrate("Темнота отступает. Издалека снова доносится знакомый будильник.");
                continue;
            }
            yield return Crossing();
            if (day == 3)
            {
                // Сами неудачи игрок уже видел на открытии. После удара
                // не перечисляем их и не подсказываем готовое решение.
                save.day = 4;
                Store();
                continue;
            }
            // Встреча с грузовиком неизбежна. Разница в том, наступит ли после
            // нового пробуждения пятница или снова повторится четверг.
            if (FourthDaySolved())
            {
                yield return GoodEnding();
                yield break;
            }
            save.fourthFailures++;
            save.day = 4;
            Store();
            // Неудачный исход четвёртого (или любого следующего) круга:
            // без лишнего выбора и без ночной спальни прямо переходим к
            // новому ЯСНОМУ утру четверга. Решение продолжать/сдаться даётся
            // один раз — утром после пробуждения.
            continue;
        }
    }

    private IEnumerator Morning()
    {
        // Пока будильник звонит, показываем ПУСТУЮ комнату.
        // CG с Артёмом появляется только когда он действительно просыпается,
        // а в первом цикле — ПОСЛЕ третьего сигнала и реплики «Да встаю я!».
        // Четвёртый и дальнейшие циклы показывают тревожный CG ПОСЛЕ обычного
        // светлого фона утренней комнаты (никакого ночного промежутка).
        string awakening = day == 1 ? "morning_day1" :
                           day == 2 ? "morning_day2" :
                           day == 3 ? "morning_day3" : "morning_day4";
        Place("bedroom", "Квартира Артёма", "08:40");
        if (day == 1)
        {
            RingAlarm(); // 1. Одновременно с текстом «Звенит будильник».
            yield return Narrate("Звенит будильник.");
            yield return Thought("Ещё пять минут...");
            RingAlarm(); // 2. Когда будильник продолжает звонить.
            yield return Narrate("Будильник продолжает звонить.");
            yield return Thought("Ладно, десять минут.");
            RingAlarm(); // 3. Сразу ПОСЛЕ реплики «Ладно, десять минут».
            yield return Narrate("Будильник снова настойчиво звонит.");
            yield return Hero("Да встаю я!");
            SetBackground(awakening); // ТРИ звонка прошли: теперь впервые видим Артёма.
            yield return Narrate("Артём наконец садится на кровати и выключает будильник.");
            yield return Thought("И всё-таки пора вставать. Только ещё секунду...");
            SetBackground("bedroom"); // Герой встал: возвращаем пустую комнату.
            yield return Narrate("Артём встаёт с кровати и ищет взглядом телефон.");
            yield return Thought("Четверг. День открытия «Маяка». Два месяца подготовки, куча нервов и презентация, которую я вчера до двух ночи собирал.");
            yield return Thought("Если всё пройдёт нормально, Ольга Викторовна, возможно, впервые за год улыбнётся. Хотя бы одним уголком рта.");
            yield return Narrate("На экране сообщение от Леры: «Доброе утро! Не забудь сегодня поесть. А то опять весь день на кофе :)»");
            yield return Thought("Лера... Надо бы сегодня с ней поговорить. По-настоящему. А не в стиле «привет, как дела, ой, мне пора».");
            yield return Narrate("Ещё три уведомления от Макса: «БРО». «ТЫ УЖЕ ВЫШЕЛ?» «НАДО ПОГОВОРИТЬ».");
            yield return Thought("...А вот и первая катастрофа дня.");
            yield return Narrate("Переодевшись, Артём выходит из квартиры. Макс просит встретиться по дороге в агентство, у небольшой кофейни.");
        }
        else if (day == 2)
        {
            RingAlarm();
            yield return Hero("А-А-А!");
            SetBackground(awakening); // Второй день: картинка после реакции на пробуждение.
            yield return Narrate("Артём резко садится на кровати, смотрит на руки, телефон, календарь.");
            yield return Hero("Четверг?..");
            yield return Thought("Нет, нет, подождите. Я же... Грузовик. Я помню грузовик!");
            yield return Thought("Говорят, сны со среды на четверг бывают вещими. Может, вчера мне приснился весь этот день? Настолько подробно, что он кажется настоящим?");
            yield return Thought("Но если я уже знаю, что произойдёт, стоит хотя бы попытаться поступить иначе.");
            yield return Narrate("Приходит то же сообщение от Леры. Затем ещё три знакомых уведомления от Макса.");
            yield return Thought("Те же сообщения. Неужели и Макс будет ждать на привычном месте?");
            yield return Hero("Ладно. В этот раз попробую ответить по-другому.");
            yield return Hero("Ну что, попробуем.");
            SetBackground("bedroom"); // Собирается; кабинет не появляется.
            yield return Narrate("Артём одевается и ещё раз смотрит на знакомую комнату. На работу всё равно пора.");
            yield return Narrate("На улице всё привычно. Даже знакомый голубь сидит у того же светофора. Макс уже ждёт у кофейни.");
        }
        else if (day == 3)
        {
            RingAlarm();
            yield return Narrate("Звенит будильник. Артём узнаёт знакомый звук, не открывая глаз.");
            yield return Hero("Я тебя ненавижу.");
            SetBackground(awakening); // Третий день: тревожный кадр после первых реплик.
            yield return Thought("Итак. Третий четверг. Если это сон, то у моего подсознания явно закончились идеи.");
            yield return Hero("Ладно. Что у нас сегодня? Макс с баннером. Ольга Викторовна с презентацией. Лера со своим загадочным разговором.");
            yield return Hero("И грузовик. Не забудем про грузовик.");
            SetBackground("bedroom"); // Третий день: возвращаем неизменный фон комнаты.
            yield return Narrate("Артём встаёт, кое-как собирается и проверяет телефон. Всё на своих местах, будто вчера не произошло ничего страшного.");
            yield return Narrate("Экран телефона вспыхивает: Макс снова пишет «БРО», «ТЫ УЖЕ ВЫШЕЛ?» и «НАДО ПОГОВОРИТЬ».");
            yield return Hero("Да-да, бро. Сейчас выйду. Обязательно поговорим.");
            yield return Thought("Может, на этот раз хотя бы не буду торопиться с ответами.");
            yield return Narrate("Кофейня на улице уже знакома до мелочей. Даже булочка у Макса кажется частью временной петли.");
        }
        else
        {
            RingAlarm();
            yield return Narrate("Будильник. Он звонит снова. И ещё раз.");
            yield return Hero("Знаешь что? Звони. Мне уже всё равно.");
            SetBackground(awakening); // Четвёртый день: тот же тревожный кадр после реплики.
            yield return Narrate("На телефоне привычная серия сообщений от Макса. Артём смотрит на экран и закрывает лицо руками.");
            yield return Thought(save.fourthFailures == 0
                ? "Уже четвёртый четверг. Максу снова нужен баннер, у Ольги опять неправильная презентация..."
                : "Опять четверг. Я уже сбился со счёта. Те же сообщения, те же люди, те же проблемы.");
            yield return Thought("И этот грузовик... Ненавижу даже думать об этой дороге.");
            yield return Hero("Да что я ему сделал?!");
            SetBackground("bedroom"); // Обстановка остаётся той же даже в четвёртом цикле.
            yield return Narrate("Артём опускается обратно на кровать. За окном обычное утро. Никто не заставляет его выходить из квартиры.");
            yield return Thought("А ведь я даже не обязан сегодня куда-то идти. Можно просто остаться дома. Или ещё раз попробовать всё изменить.");
        }
    }

    private IEnumerator MeetMax()
    {
        Place("day_street", "У кофейни", "09:10", "max");
        yield return Narrate("Макс стоит у кофейни. В одной руке у него телефон, в другой — недоеденная булочка.");
        yield return Say("Макс", "О! Живой!", "happy");
        if (day == 1)
        {
            yield return Hero("Твои сообщения производили впечатление, что кого-то уже надо хоронить.");
            yield return Say("Макс", "Не преувеличивай. Пока всё под контролем.", "neutral");
            yield return Hero("Ты написал «НАДО ПОГОВОРИТЬ» капсом. Я уже приготовился к худшему.");
            yield return Say("Макс", "Слушай... Не переживай. Если бы всё было совсем плохо, я бы поставил три восклицательных знака.", "neutral");
            yield return Hero("Прекрасная система оповещения. Надо её запатентовать.");
        }
        else if (day == 2)
        {
            yield return Thought("Да неужели. Особенно после вчерашнего.");
            yield return Say("Макс", "Чего ты так смотришь?", "surprised");
            yield return Hero("Ничего. Просто рад тебя видеть.");
            yield return Say("Макс", "Ты заболел?", "thinking");
            yield return Hero("Пока нет.");
        }
        else if (day == 3)
        {
            yield return Hero("Пока да.");
            yield return Say("Макс", "Чего?", "surprised");
            yield return Hero("Ничего. Как булочка?");
            yield return Say("Макс", "Нормальная. Будешь?", "happy");
            yield return Hero("Нет, спасибо.");
            yield return Thought("Удивительно, насколько бесит одна и та же булочка на третий день.");
        }
        else
        {
            yield return Hero("Технически.");
            yield return Say("Макс", "Чего?", "surprised");
            yield return Hero("Макс, у тебя ведь баннер забрать надо?");
            yield return Say("Макс", "Да! Откуда ты...", "surprised");
            yield return Hero("Слушай, давай хотя бы в этот раз без сюрпризов.");
            yield return Say("Макс", "В какой «этот раз»?", "thinking");
            yield return Hero("Долгая история.");
        }
        if (day == 1)
        {
            yield return Say("Макс", "Баннер для открытия готов. Надо забрать его из типографии до одиннадцати, а монтажники ждут меня в «Маяке».", "thinking");
            yield return Hero("И ты говоришь мне об этом за два часа до начала рабочего дня?");
            yield return Say("Макс", "Я надеялся, что всё само как-нибудь решится.", "embarrassed");
            yield return Hero("Сильная стратегия.");
            yield return Say("Макс", "Не осуждай, я на ней всю жизнь держусь.", "happy");
            yield return Narrate("Макс убирает телефон и перестаёт улыбаться.");
            yield return Say("Макс", "Тём, серьёзно. Выручишь? Мне самому никак не успеть.", "thinking");
            yield return Thought("Просто один раз прийти на работу вовремя — слишком роскошное желание.");
        }
        else
        {
            yield return Say("Макс", "Короче, баннер готов, но его надо забрать из типографии до одиннадцати. Я сейчас с монтажниками и оборудованием...", "thinking");
            if (day == 2)
                yield return Thought("Из типографии. До одиннадцати. Потому что монтажники. Помню.");
            if (day == 3)
            {
                yield return Hero("Баннер?");
                yield return Say("Макс", "Да! А ты откуда...", "surprised");
                yield return Hero("Ты вчера говорил про подготовку. В смысле... мы всё это обсуждали.");
            }
            yield return Say("Макс", "Тём, выручишь?", "thinking");
        }
        yield return Remember(save.max,
            "В прошлый раз я согласился. Макс был доволен, но потом пришлось выслушивать начальницу.",
            "В прошлый раз я отказался. На работу успел, зато Макс остался без баннера.");

        if (day <= 2)
            yield return PickHistory(save.max, "Что ответить Максу?",
                "Ладно, заберу. Скидывай адрес.",
                "Извини, но я не могу. Мне ещё на работу.");
        else if (day == 3)
            yield return PickHistory(save.max, "Что ответить Максу?",
                "Ладно, заберу сам.",
                "Извини, сегодня не могу.",
                "А ты узнавал, можно ли заказать доставку?");
        else
            yield return PickHistory(save.max, "Что ответить Максу?",
                "Я сам заберу баннер. Опоздаю, но успею помочь.",
                "Не могу заняться этим. Попробуй найти другого человека.",
                "Давай сначала проверим требования и договоримся о доставке.");
        int decision = answer;
        save.max[day - 1] = decision;
        Store();
        if (day == 2)
            yield return Thought(save.max[0] == decision
                ? "Опять так же? Ну, посмотрим. Может, в прошлый раз я просто неудачно всё сделал."
                : "Ладно, в этот раз попробуем иначе.");

        if (decision == 0)
        {
            banner = true; late = true; bannerType = "manual";
            yield return Say("Макс", "Ты лучший! Я знал, что не бросишь!", "happy");
            yield return Hero("Я ещё не сказал, что не пожалею.");
            yield return Say("Макс", "Считай, я должен тебе ужин.", "relieved");
            yield return Hero("Ты мне уже должен три.");
            yield return Say("Макс", "У меня плохая финансовая дисциплина.", "embarrassed");
            yield return Narrate("Макс убегает к монтажникам, а Артём открывает маршрут до типографии.");
            HideActor("max");
            yield return Thought("Сорок минут в одну сторону. Прекрасно. Надеюсь, Ольга Викторовна сегодня в хорошем настроении.");
            Place("day_street", "Дорога из типографии", "10:45");
            yield return Narrate("Заказ приходится немного подождать. Артём несёт баннер через полгорода и понимает, что теперь опоздает в офис.");
        }
        else if (decision == 1)
        {
            yield return Hero("Извини, но я не могу. Мне ещё на работу.");
            yield return Say("Макс", "А... Ну да. Точно. Попробую что-нибудь придумать.", "disappointed");
            yield return Hero("Ты не можешь попросить кого-нибудь из монтажников?");
            yield return Say("Макс", "Они и так в запаре. Я не обижаюсь.", "neutral");
            yield return Narrate("Макс откусывает булочку и делает обиженно-торжественное лицо.");
            yield return Say("Макс", "Я просто драматично жую.", "embarrassed");
            yield return Hero("Вижу.");
            yield return Thought("Неловко получилось. Но я же действительно не могу каждый раз бросать свои дела.");
            HideActor("max");
            yield return Narrate("Артём приходит на работу вовремя. Макс занят аппаратурой и так и не находит человека, который заберёт баннер.");
        }
        else if (decision == 2)
        {
            if (day == 4)
            {
                yield return Hero("Неужели выход только один — ехать за баннером через полгорода?");
                yield return Say("Макс", "Типография может привезти заказ. Или можно показать заставку на экране в зале.", "thinking");
                yield return Say("Макс", "Только в договоре партнёров указан баннер у входа. Экран его без согласования не заменит.", "thinking");
                yield return PickHistory(save.maxFollow, "Как поступить с оформлением?",
                    "Уточнить стоимость и точное время доставки баннера к входу.",
                    "Сделать красивую заставку на экране. Согласование потом.");
                int method = answer;
                save.maxFollow[day - 1] = method;
                Store();
                if (method == 0)
                {
                    banner = true;
                    bannerType = "delivery";
                    yield return Narrate("Макс звонит в типографию. Артём просит подтвердить время курьера письменно и договаривается с монтажниками о приёмке.");
                    yield return Say("Макс", "Курьер будет вовремя. И смету не превысили!", "relieved");
                    yield return Hero("Отлично. Напишешь, когда привезут?");
                }
                else
                {
                    banner = false;
                    bannerType = "screen_only";
                    yield return Say("Макс", "Под экран у нас есть красивый макет. Я могу всё настроить.", "happy");
                    yield return Hero("Вот и хорошо, хоть в зале будет нормально выглядеть.");
                    yield return Narrate("Артём отправляет макет на экран. Монтажники успевают его вывести, но партнёры не согласовывали замену баннера у входа.");
                }
            }
            else
            {
                banner = day != 3;
                bannerType = day == 3 ? "delivery_unconfirmed" : "delivery";
                yield return Hero("У них же обычно доставка есть. Ты спрашивал?");
                yield return Say("Макс", "Ну... нет.", "embarrassed");
                yield return Hero("Макс. Ты два часа ищешь человека, который заберёт баннер, и даже не спросил, могут ли его привезти?");
                yield return Say("Макс", "Когда ты так говоришь, звучит глупо.", "disappointed");
                yield return Hero("Оно и без меня глупо звучит.");
                yield return Narrate("Макс звонит в типографию. Артём терпеливо ждёт рядом.");
                yield return Say("Макс", "Здравствуйте. А вы можете доставить заказ?.. Сегодня?.. Серьёзно?", "surprised");
                yield return Say("Макс", "...За триста рублей.", "surprised");
                yield return Hero("Какая страшная цена за спасённый рабочий день.");
                yield return Say("Макс", "Ладно, ладно! Заказываю. Тём, я тебе должен.", "happy");
                yield return Hero("Не надо. Просто в следующий раз сначала позвони.");
                yield return Say("Макс", "Вот это уже сложнее.", "embarrassed");
                if (day == 3)
                    yield return Narrate("Максу обещают доставить заказ к открытию. Он убирает телефон, а Артём торопится в офис.");
                else
                    yield return Narrate("Баннер доставят к открытию. Артём успевает в агентство без опоздания.");
            }
        }
        else
        {
            yield return Say("Макс", "Ну... для оформления? По договору нужен логотип и название. Вроде бы.", "thinking");
            yield return Hero("А экран в зале? У нас есть подходящий макет?");
            yield return Say("Макс", "О. Точно, экран! Макет есть, но его надо подогнать под размер и сегодня отправить монтажникам.", "surprised");
            yield return PickHistory(save.maxFollow, "Что сделать с цифровым макетом?",
                "Сейчас подготовлю файл и отправлю монтажникам.",
                "Ладно, потом разберёмся.");
            save.maxFollow[day - 1] = answer;
            Store();
            if (answer == 0)
            {
                banner = true; bannerType = "screen";
                yield return Say("Макс", "Серьёзно? Это было бы идеально!", "happy");
                yield return Hero("Заодно не придётся тащить баннер через полгорода.");
                yield return Say("Макс", "Идеальный мир, где технологии работают на нас.", "relieved");
                yield return Narrate("Артём готовит изображение для экрана и отправляет его монтажникам. Оформление готово без бумажного баннера.");
            }
            else
            {
                yield return Say("Макс", "Ну... хорошо.", "disappointed");
                yield return Thought("Потом, потом... Надеюсь, мы не забудем.");
                yield return Narrate("Никто не отправляет файл. Вопрос с оформлением остаётся нерешённым.");
            }
        }
        if (day == 2)
        {
            yield return Say("Макс", "Слушай, ты сегодня какой-то странный.", "thinking");
            yield return Hero("Почему?");
            yield return Say("Макс", "Обычно ты минут пять ворчишь, прежде чем отвечать.", "happy");
            yield return Hero("Сегодня я ворчу мысленно.");
            yield return Say("Макс", "Удобно. Надо попробовать.", "relieved");
        }
        HideActor("max");
        yield return Narrate("Через пару часов Артём возвращается к работе над презентацией. От Ольги Викторовны приходит короткая просьба зайти в кабинет.");
    }

    private IEnumerator MeetBoss()
    {
        Place("office", "Офис. Кабинет Ольги Викторовны", "12:15", "olga");
        yield return Narrate("Артём подходит к кабинету начальницы. На мониторе открыта презентация; рядом с клавиатурой стоит давно остывший кофе.");
        if (day == 1)
        {
            yield return Say("Ольга Викторовна", "Артём, зайди, пожалуйста.", "neutral");
            yield return Thought("«Пожалуйста». Это либо премия, либо конец моей карьеры.");
            yield return Hero("Добрый день.");
            yield return Say("Ольга Викторовна", "Он был добрым до того, как мне позвонил заказчик.", "akimbo");
            yield return Hero("Понятно. Премия отменяется.");
            yield return Say("Ольга Викторовна", "Что?", "surprised");
            yield return Hero("Ничего.");
        }
        else if (day == 2)
        {
            yield return Thought("Сейчас она попросит зайти.");
            yield return Say("Ольга Викторовна", "Артём, зайди, пожалуйста.", "neutral");
            yield return Hero("Уже иду.");
            yield return Thought("Да. Мне определённо это не снится.");
            yield return Say("Ольга Викторовна", "У нас проблема с клиентом.", "akimbo");
            yield return Hero("Старые цифры? И надпись «ПОТОМ ИСПРАВИТЬ» на шестом слайде?");
            yield return Say("Ольга Викторовна", "Откуда ты знаешь?", "surprised");
            yield return Hero("Догадался.");
            yield return Say("Ольга Викторовна", "Пугающая догадливость. Жаль, она не помогла нам вчера.", "talking");
            yield return Thought("Вот сейчас обидно было.");
        }
        else if (day == 3)
        {
            yield return Thought("Старые цифры, шестой слайд, «ПОТОМ ИСПРАВИТЬ». Сейчас повторится.");
            yield return Say("Ольга Викторовна", "Артём, зайди.", "neutral");
            yield return Hero("Добрый день, Ольга Викторовна.");
            yield return Say("Ольга Викторовна", "Знаешь, мне кажется, он уже не очень добрый. В презентации...", "talking");
            yield return Hero("Старые данные. И надпись на шестом слайде.");
            yield return Say("Ольга Викторовна", "...Артём. Ты случайно не знаешь ещё и номер завтрашнего лотерейного билета?", "surprised");
            yield return Hero("Если узнаю, вы первая, кому скажу.");
            yield return Say("Ольга Викторовна", "Лучше бы ты узнал, как исправить эту презентацию.", "akimbo");
        }
        else
        {
            yield return Say("Ольга Викторовна", "Артём, у нас проблема.", "akimbo");
            yield return Hero("Я знаю.");
            yield return Say("Ольга Викторовна", "Почему мне кажется, что ты вообще не удивлён?", "surprised");
            yield return Hero("В последнее время меня сложно удивить.");
            yield return Say("Ольга Викторовна", "Прекрасно. Тогда, может, удивишь меня хорошей презентацией?", "talking");
        }
        if (late)
        {
            yield return Say("Ольга Викторовна", "Кстати, где тебя носило сегодня утром?", "gesturing");
            yield return Hero("Забирал баннер для открытия. Макс не успевал.");
            yield return Say("Ольга Викторовна", "Прекрасно. Значит, хотя бы баннер у нас будет.", "thinking");
        }
        else if (day == 1)
        {
            yield return Say("Ольга Викторовна", "Ты ведь с утра здесь. Почему не подошёл уточнить, всё ли готово?", "talking");
            yield return Hero("Я думал, мы вчера всё закончили.");
            yield return Say("Ольга Викторовна", "Я тоже так думала.", "thinking");
        }
        if (day == 1)
        {
            yield return Say("Ольга Викторовна", "В презентации старые цифры. И прекрасная надпись «ПОТОМ ИСПРАВИТЬ» на шестом слайде.", "gesturing");
            yield return Hero("Я эту надпись вчера убрал.");
            yield return Say("Ольга Викторовна", "А клиент, видимо, так впечатлился, что решил её вернуть.", "akimbo");
            yield return Hero("Вы точно открыли последнюю версию?");
            yield return Say("Ольга Викторовна", "Я открыла то, что мы отправили заказчику. Сейчас вопрос не в этом.", "talking");
            yield return Thought("Вообще-то вопрос именно в этом. Но спорить сейчас, наверное, не самая удачная идея.");
        }
        else if (day == 2)
        {
            yield return Say("Ольга Викторовна", "Мне вчера точно присылали что-то другое... Ладно, сейчас не до этого.", "thinking");
            yield return Thought("Странно. В прошлый раз она так не говорила. Может, дело вообще не в моём файле?");
        }
        else
        {
            yield return Thought("В прошлые разы мы только спорили. Или я обещал всё переделать. Но правильный файл уже должен где-то быть...");
        }
        yield return Remember(save.boss,
            "В прошлый раз мы поругались о том, кто виноват, и никто ничего не исправил.",
            "В прошлый раз я переделывал слайды, но утверждённые цифры от этого не появились.");
        if (day <= 2)
            yield return PickHistory(save.boss, "Что ответить Ольге Викторовне?",
                "Это не моя ошибка.",
                "Хорошо, я всё переделаю.");
        else if (day == 3)
            yield return PickHistory(save.boss, "Как разобраться с презентацией?",
                "Это не моя ошибка.",
                "Хорошо, я всё переделаю.",
                "Давайте посмотрим, что именно ушло клиенту.");
        else
            yield return PickHistory(save.boss, "Что ответить Ольге Викторовне?",
                "Сначала докажу, что это не моя вина.",
                "Я заново переделаю презентацию.",
                "Давайте разберёмся, какую версию получил заказчик.");
        int decision = answer;
        save.boss[day - 1] = decision;
        Store();
        if (decision == 0)
        {
            bossType = "conflict";
            yield return Hero("Я вчера отправил финальную версию. Я не виноват в том, что произошло потом.");
            yield return Say("Ольга Викторовна", "И ты предлагаешь мне сейчас искать виноватого?", "gesturing");
            yield return Hero("Я предлагаю не обвинять меня в том, чего я не делал.");
            yield return Say("Ольга Викторовна", "Артём, у меня заказчик на линии каждые пятнадцать минут!", "akimbo");
            yield return Hero("У меня тоже не железные нервы.");
            yield return Say("Ольга Викторовна", "Замечательно. Значит, нас двое. Можешь идти.", "facepalm");
            yield return Thought("Великолепно. Я только что поссорился с человеком, который решает, сколько я буду получать в следующем месяце.");
            yield return Narrate("Артём выходит из кабинета. Презентация осталась прежней, разговор — незаконченным.");
            HideActor("olga");
            if (day == 2)
            {
                yield return Thought("Уверенность не исправит файл. Вроде бы я это уже слышал.");
            }
        }
        else if (decision == 1)
        {
            bossType = "overwork";
            yield return Hero("Соберу новую версию. До вечера должно хватить времени.");
            yield return Say("Ольга Викторовна", "Ты уверен? Там ещё бюджет надо согласовать.", "thinking");
            yield return Hero("Разберусь.");
            yield return Say("Ольга Викторовна", "Хорошо. Только без сюрпризов.", "akimbo");
            yield return Narrate("Артём возвращается за рабочий стол и начинает собирать презентацию заново.");
            HideActor("olga");
            yield return Thought("Может, я и не виноват, но переделывать всё равно мне. На каком диске мы хранили утверждённый бюджет?");
            yield return Narrate("Часы идут. Оформление обновлено, но без последней сметы цифры остаются несогласованными.");
            if (day == 2)
                yield return Thought("Какие ещё «на этот раз»? Для неё ведь это первое прохождение...");
        }
        else if (decision == 2)
        {
            if (day == 4)
            {
                yield return Hero("Прежде чем всё переписывать, давайте сравним файлы.");
                yield return Say("Ольга Викторовна", "У нас мало времени. Как ты предлагаешь поступить?", "thinking");
                yield return PickHistory(save.bossFollow, "Как действовать с ошибкой в презентации?",
                    "Поднять историю согласований и сверить вложения с общей папкой.",
                    "Уточнить у клиента актуальные цифры перед следующей отправкой.");
                save.bossFollow[day - 1] = answer;
                Store();
                if (answer == 0)
                {
                    slides = true;
                    bossType = "resolved";
                    yield return Narrate("Артём сопоставляет переписку, утверждённую смету и вложения. Ошибка оказывается двойной: в письме старый файл, а в общей папке лежит ещё не согласованный черновик бюджета.");
                    yield return Hero("Вот версия, которую партнёры действительно утвердили. Давайте сверим итоговые цифры и только потом отправим.");
                    yield return Say("Ольга Викторовна", "Да. И... спасибо, что настоял на проверке.", "smile");
                    yield return Narrate("Клиент получает правильную презентацию. Путаница с версиями устранена.");
                }
                else
                {
                    bossType = "waiting";
                    yield return Hero("Спросим напрямую, какие цифры у клиента на руках, чтобы не внести лишних изменений.");
                    yield return Say("Ольга Викторовна", "Это разумно. Но у нас осталась переписка с согласованной сметой; её тоже стоило бы проверить.", "thinking");
                    yield return Narrate("Артём отправляет запрос. Клиент занят подготовкой и отвечает только после начала открытия. Между тем согласованный бюджет всё это время лежал в архиве переписки.");
                }
            }
            else
            {
                slides = day != 3;
                bossType = day == 3 ? "budget_unconfirmed" : "resolved";
                yield return Hero("Хочу понять, откуда взялся файл. Покажите отправленное письмо.");
                yield return Say("Ольга Викторовна", "Ладно. Вот письмо.", "talking");
                yield return Narrate("Ольга Викторовна открывает папку с отправленными сообщениями.");
                yield return Hero("Подождите. Это вложение из старой переписки. Видите дату? Финальную версию я загрузил позже.");
                yield return Say("Ольга Викторовна", "Что?.. Значит, я отправила старую версию.", "surprised");
                yield return Hero("Похоже на то.");
                yield return Say("Ольга Викторовна", "И ты всё это время говорил, что не виноват.", "thinking");
                yield return Hero("Я старался говорить это вежливо.");
                if (day == 3)
                {
                    yield return Say("Ольга Викторовна", "Получалось не всегда. Хорошо, источник ошибки нашли. Но какая из смет утверждена партнёрами?", "thinking");
                    yield return Hero("Та, что в общей папке... наверное.");
                    yield return Say("Ольга Викторовна", "Не будем отправлять новое вложение, пока это не проверим.", "akimbo");
                    yield return Narrate("Ольга Викторовна открывает общую папку с бюджетами. Напротив последней сметы стоит статус «На проверке». В дверь стучат: команду уже ждут на площадке.");
                }
                else
                {
                    yield return Say("Ольга Викторовна", "Получалось не всегда. Ладно. Спасибо, что разобрался. Отправим проверенную версию.", "smile");
                    yield return Narrate("Клиент получает согласованную презентацию. Ошибка обнаружена и исправлена без новой переработки.");
                }
            }
        }
        else
        {
            slides = true; bossType = "trust";
            yield return Hero("Давайте откроем общую папку проекта и сравним документы.");
            yield return Say("Ольга Викторовна", "Хорошо. Вижу окончательную смету. А что?", "thinking");
            yield return Hero("Вот. Вчера в 17:42 загрузили финальную версию. В письме почему-то прикреплён более ранний файл.");
            yield return Say("Ольга Викторовна", "...Вот же.", "thinking");
            yield return Hero("Бывает.");
            yield return Say("Ольга Викторовна", "Не надо меня утешать. Я ещё не настолько старая.", "akimbo");
            yield return Hero("Я ничего такого не говорил!");
            yield return Say("Ольга Викторовна", "Зато подумал. Ладно, исправляем. И да, Артём... хорошая работа.", "smile");
            yield return Thought("Сейчас бы записать это на диктофон.");
            yield return Narrate("Правильная презентация отправлена клиенту. Ольга Викторовна впервые за день выглядит спокойнее.");
        }
        HideActor("olga");
        yield return Narrate("Ближе к четырём Артём заканчивает свои дела. Впереди ещё открытие «Маяка». На улице собираются дождевые тучи.");
    }

    private IEnumerator MeetLera()
    {
        Place("mayak", "Двор молодёжного центра «Маяк»", "16:20", "lera");
        yield return Narrate("После работы Артём доходит до молодёжного центра. Лера стоит у входа с папкой и то и дело смотрит на небо.");
        if (day == 1)
        {
            yield return Say("Лера", "О, наконец-то! Я уже думала, ты не придёшь.", "smile");
            yield return Hero("Смотря что считать «прийти». Физически я здесь. Морально остался в офисе.");
            yield return Say("Лера", "Судя по лицу, день выдался весёлый.", "thinking");
            yield return Hero("Если коротко — хочу домой.");
            yield return Say("Лера", "Не поверишь, я тоже. И погода ещё... Ладно, неважно. Слушай, у нас тут кое-что поменялось.", "worried");
            yield return Hero("Надеюсь, не дата открытия.");
            yield return Say("Лера", "Нет, но... Вообще я хотела с тобой ещё кое о чём поговорить. Лично.", "confused");
            yield return Thought("Лично? Так. Спокойно. Не нужно делать никаких выводов. Хотя какие тут могут быть другие выводы?.. ");
        }
        else if (day == 2)
        {
            yield return Say("Лера", "О, наконец-то! Я уже думала, ты не придёшь.", "smile");
            yield return Hero("Привет, Лер.");
            yield return Say("Лера", "Выглядишь так, будто видел привидение.", "thinking");
            yield return Hero("Скорее очень большой автомобиль.");
            yield return Say("Лера", "Что?", "surprised");
            yield return Hero("Ничего. Слушай, открытие же теперь внутри здания?");
            yield return Say("Лера", "Да! Ты увидел сообщение? Только Макс, кажется, ещё не в курсе. Можешь его предупредить?", "smile");
            yield return Hero("Конечно.");
            yield return Narrate("Артём сразу же находит Макса у служебного входа, сообщает о переносе и просит передать информацию монтажникам и начальнице.");
            venue = true;
            Place("mayak", "У входа в «Маяк»", "16:25", "lera");
            yield return Say("Лера", "Спасибо. А ещё я хотела поговорить с тобой. Не про работу.", "thinking");
            yield return Thought("Точно. Этот разговор. Рабочий вопрос я исправил, но что она хотела сказать лично?");
        }
        else if (day == 3)
        {
            yield return Say("Лера", "О, наконец-то! Я уже думала, ты не придёшь.", "neutral");
            yield return Hero("Я тоже так думал.");
            yield return Say("Лера", "Ты сегодня странно себя ведёшь.", "thinking");
            yield return Hero("Все сегодня мне это говорят.");
            yield return Say("Лера", "Может, стоит задуматься?", "smile");
            yield return Hero("Уже задумался. Ничего хорошего.");
            yield return Say("Лера", "Ладно, слушай. У нас тут кое-что поменялось. И ещё я хотела с тобой поговорить. Не только про работу.", "worried");
            yield return Thought("Перенос открытия. Да, помню. Вот только что именно она хотела сказать лично?.. ");
        }
        else
        {
            yield return Say("Лера", "Ты сегодня какой-то уставший.", "worried");
            yield return Hero("Да так. Накопилось.");
            yield return Say("Лера", "Может, тебе отдохнуть?", "thinking");
            yield return Hero("Да я бы с удовольствием.");
            yield return Say("Лера", "Тём, я серьёзно.", "worried");
            yield return Hero("Я тоже.");
            yield return Say("Лера", "Ладно... Я вообще-то хотела рассказать тебе о том, что изменилось на открытии. И ещё кое о чём.", "neutral");
        }
        yield return Remember(save.lera,
            "В прошлый раз я вывалил на неё свои чувства, даже не дав договорить. Может, сейчас будет подходящий момент?",
            "В прошлый раз она явно расстроилась. Наверное, стоило уделить ей хотя бы пару минут.");
        if (day <= 2)
            yield return PickHistory(save.lera, "Что ответить Лере?",
                "Слушай, я тоже давно хотел тебе кое-что сказать...",
                "Давай после открытия? Сейчас голова вообще не варит.");
        else if (day == 3)
            yield return PickHistory(save.lera, "Как ответить Лере?",
                "Я тоже давно хотел признаться...",
                "Давай поговорим после открытия.",
                "Давай сначала решим вопрос с открытием. Что изменилось?");
        else
            yield return PickHistory(save.lera, "Как ответить Лере?",
                "Скажу ей сейчас о своих чувствах, пока не передумал.",
                "Попрошу поговорить позже: сейчас нужно спасать открытие.",
                "Выслушаю её сначала, даже если услышу что-то неприятное.");
        int decision = answer;
        save.lera[day - 1] = decision;
        Store();
        if (decision == 0)
        {
            yield return Hero("Ты мне нравишься, Лер. Уже довольно давно.");
            yield return Say("Лера", "...", "surprised");
            yield return Thought("Я это вслух сказал?");
            yield return Say("Лера", "Тём, я...", "confused");
            yield return Hero("Не обязательно отвечать прямо сейчас.");
            yield return Say("Лера", "Я не ожидала, что ты именно сегодня...", "worried");
            yield return Narrate("Из здания зовут Леру: монтажники ищут её из-за оборудования.");
            yield return Say("Лера", "Прости, мне надо бежать. Мы ещё поговорим, ладно?", "worried");
            yield return Hero("Ага. Конечно.");
            yield return Thought("Хотел красивое признание, а получилось как объявление о закрытии магазина.");
            yield return Narrate("Лера уходит к организаторам. Артём так и не спросил про перенос открытия.");
            HideActor("lera");
            // Во втором цикле он уже сообщил о переносе ДО личного разговора.
        }
        else if (decision == 1)
        {
            leraUpset = true;
            yield return Say("Лера", "Но я как раз про сегодня хотела...", "worried");
            yield return Hero("Прости. Правда. Просто если сейчас ещё что-нибудь случится, я лягу здесь на лавочку и останусь жить.");
            yield return Say("Лера", "Ясно. Ладно, тогда после открытия.", "worried");
            yield return Hero("Спасибо, что понимаешь.");
            yield return Say("Лера", "Угу.", "confused");
            yield return Thought("Кажется, она обиделась. Ну почему нельзя хотя бы один разговор провести нормально?");
            yield return Narrate("Лера опускает взгляд на папку и возвращается к подготовке. Её рабочее сообщение остаётся в общем чате.");
            HideActor("lera");
        }
        else if (decision == 2)
        {
            if (day == 4)
            {
                yield return Say("Лера", "Из-за дождя открытие перенесли в зал. Но ещё... мне предложили работу в другом городе.", "worried");
                yield return Hero("Ого. Теперь понимаю, почему тебе нелегко.");
                yield return PickHistory(save.leraFollow, "Что делать после её новости?",
                    "Сейчас предупрежу команду. К разговору вернёмся после работы.",
                    "Сразу попрошу не уезжать: мне важно её не потерять.",
                    "Спрошу, чего она хочет, выслушаю и лично предупрежу команду.");
                int follow = answer;
                save.leraFollow[day - 1] = follow;
                Store();
                if (follow == 0)
                {
                    venue = true;
                    yield return Hero("Я предупрежу остальных. Обещаю, что после открытия мы обязательно поговорим.");
                    yield return Say("Лера", "Хорошо. Я подожду.", "thinking");
                    yield return Narrate("Артём вовремя сообщает о переносе команде и возвращается к рабочим задачам. Лера остаётся ждать обещанного разговора после открытия.");
                    yield return Thought("Главное — действительно к ней вернуться. Я столько раз уже говорил «потом»...");
                }
                else if (follow == 1)
                {
                    leraUpset = true;
                    yield return Hero("Я не хочу тебя терять. Может, не надо туда ехать? Здесь ведь тоже всё неплохо.");
                    yield return Say("Лера", "Тём, я ещё даже не приняла решение. Я просила меня выслушать, а не решать за меня.", "angry");
                    yield return Hero("Прости. Меня это просто напугало.");
                    yield return Narrate("Леру зовут из здания. Разговор обрывается, а Артём, занятый своими чувствами, так и не предупреждает команду о переносе.");
                    HideActor("lera");
                }
                else
                {
                    venue = true;
                    personalNews = true;
                    leraComforted = true;
                    yield return Say("Лера", "Пока не знаю. Хорошая возможность, но мне страшно менять всё сразу.", "thinking");
                    yield return Hero("Тебе не обязательно решать это сейчас. Я предупрежу остальных, а после открытия мы спокойно поговорим.");
                    yield return Say("Лера", "Спасибо, Тём. Мне сейчас именно это и нужно.", "grateful");
                    yield return Narrate("Артём лично предупреждает Макса и Ольгу Викторовну. Команда переносит оборудование в зал, а Лера знает, что её выслушают.");
                }
            }
            else
            {
                venue = true;
                yield return Say("Лера", "Спасибо! Я уже второй час пытаюсь всех предупредить.", "smile");
                yield return Hero("Перенесли в зал? Я видел прогноз погоды.");
                yield return Say("Лера", "Да! Из-за дождя. Макса предупредишь? И Ольгу Викторовну тоже.", "worried");
                yield return Hero("Конечно. А после открытия можем спокойно поговорить о том, что тебя беспокоит.");
                yield return Say("Лера", "Правда?.. Договорились.", "smile");
                yield return Narrate("Артём лично предупреждает Макса и начальницу. Монтажники переносят оборудование в зал.");
            }
        }
        else if (decision == 3)
        {
            venue = true;
            personalNews = true;
            leraComforted = true;
            yield return Say("Лера", "...Просто не ожидала, что ты умеешь не перебивать.", "surprised");
            yield return Hero("Жестоко.");
            yield return Say("Лера", "Сам напросился. В общем, открытие перенесли в зал. Я отправила всем сообщение, но никто не отвечает.", "smile");
            yield return Hero("Я сейчас предупрежу остальных.");
            yield return Say("Лера", "Спасибо. И ещё... Мне предложили работу в другом городе.", "worried");
            yield return Hero("Ого. А ты сама чего хочешь?");
            yield return Say("Лера", "Вот в том-то и дело, что пока не знаю.", "thinking");
            yield return Hero("Тогда давай поговорим после открытия. Без спешки.");
            yield return Say("Лера", "Я бы хотела.", "smile");
            yield return Thought("Интересно. А я дважды был уверен, что она собиралась говорить обо мне.");
            yield return Narrate("Артём предупреждает команду лично. У них с Лерой остаётся ещё один, уже нерабочий разговор на потом.");
        }
        else
        {
            personalNews = true;
            yield return Say("Лера", "Заметил?.. Мне предложили работу в другом городе, и я теперь не знаю, как поступить.", "worried");
            yield return Hero("В другом городе?.. ");
            yield return Say("Лера", "Ага. Хорошая должность, но придётся переезжать.", "thinking");
            yield return Thought("Она может уехать. И, возможно, мы вообще перестанем видеться.");
            yield return PickHistory(save.leraFollow, "Что сказать Лере?",
                "Это важное решение. Хочешь обсудим? И что ты говорила про открытие?",
                "Я не хочу, чтобы ты уезжала.");
            save.leraFollow[day - 1] = answer;
            Store();
            if (answer == 0)
            {
                venue = true;
                leraComforted = true;
                yield return Say("Лера", "Спасибо, Тём. Да, нас перенесли внутрь. Можешь предупредить остальных?", "smile");
                yield return Hero("Сделаю.");
                yield return Say("Лера", "И... спасибо, что спросил, как я.", "smile");
                yield return Narrate("Артём предупреждает команду о переносе. Лера чувствует поддержку.");
            }
            else
            {
                leraUpset = true;
                yield return Hero("Просто я не хочу тебя терять.");
                yield return Say("Лера", "Я ещё ничего не решила. Я хотела поделиться, а теперь чувствую, будто должна оправдываться.", "angry");
                yield return Hero("Я не это имел в виду.");
                yield return Narrate("Из здания зовут Леру. Разговор прерван, и о переносе Артём так и не сообщил коллегам.");
                yield return Say("Лера", "Прости. Мне пора.", "worried");
                HideActor("lera");
                yield return Thought("Чёрт. Кажется, опять всё испортил.");
            }
        }
        if (day == 1 && !venue)
            yield return Narrate("Лера всё-таки публикует информацию о переносе в общем рабочем чате. Но Артём занят своими мыслями, а коллеги — подготовкой, и сообщение остаётся непрочитанным.");
        else if (day >= 3 && !venue)
            yield return Thought("Опять этот разговор... Макс, наверное, до сих пор у оборудования.");
        HideActor("lera");
        yield return Narrate("Время близится к вечеру. Гости уже направляются к «Маяку», а Артём пытается понять, что сегодня успели подготовить.");
    }

    private IEnumerator Opening()
    {
        Place("mayak", "«Маяк» — открытие", "18:30", "max");
        yield return Narrate("К шести вечера небо темнеет. Артём добирается до «Маяка», где Макс заканчивает расставлять оборудование.");
        if (day == 1)
        {
            yield return Say("Макс", "Тём, только скажи честно. Тебе не кажется, что здесь как-то... пустовато?", "thinking");
            yield return Hero("Возможно, люди просто не хотят приходить на мероприятия в четверг.");
            yield return Say("Макс", "А мы могли сделать в пятницу?", "thinking");
            yield return Hero("Нет.");
            yield return Say("Макс", "Тогда всё, вопросы сняты.", "neutral");
        }
        else if (day == 2)
        {
            yield return Say("Макс", "Слушай, а ведь мы почти успели!", "happy");
            yield return Hero("Не говори это вслух.");
            yield return Say("Макс", "Почему?", "surprised");
            yield return Hero("Просто поверь.");
        }
        else
        {
            yield return Say("Макс", "Тём!", "surprised");
            yield return Hero("Что?");
            yield return Say("Макс", "Я официально ненавижу организовывать мероприятия.", "thinking");
            yield return Hero("Ты же сам вызвался.");
            yield return Say("Макс", "Молодой был. Глупый.", "disappointed");
            yield return Hero("Это было два месяца назад.");
            yield return Say("Макс", "Многое изменилось.", "relieved");
        }
        // Разговоры проходят очно, но на сцене максимум два спрайта.
        // Так персонажи не перекрывают друг друга, а их пропорции сохраняются.
        Place("mayak", "«Маяк»", "18:33", "max", "olga");
        if (day == 1)
        {
            yield return Say("Ольга Викторовна", "Где гости?", "akimbo");
            yield return Say("Макс", "Вот! Я как раз хотел спросить.", "surprised");
            yield return Say("Ольга Викторовна", "Максим, я не у тебя спрашиваю.", "gesturing");
            yield return Say("Макс", "А жаль. У меня тоже нет ответа.", "embarrassed");
        }
        else if (day == 2)
        {
            yield return Say("Ольга Викторовна", "Артём. Нам нужно поговорить.", "akimbo");
            yield return Hero("Я же просил... В смысле, что случилось?");
            yield return Say("Ольга Викторовна", "Заказчик не согласовал презентацию. Данные не соответствуют финальной смете.", "gesturing");
            if (bossType == "overwork")
            {
                yield return Hero("Но я её заново собрал!");
                yield return Say("Ольга Викторовна", "А смету ты с чем сверял?", "thinking");
                yield return Hero("С той, что у меня была.");
                yield return Say("Ольга Викторовна", "Вот именно.", "facepalm");
            }
            else
            {
                yield return Hero("Я предупреждал, что это не моя версия.");
                yield return Say("Ольга Викторовна", "И что нам теперь делать с твоей правотой?", "akimbo");
            }
        }
        else
        {
            yield return Say("Ольга Викторовна", "Мальчики, вы закончили философствовать?", "akimbo");
            yield return Say("Макс", "Мы только начали.", "happy");
            yield return Say("Ольга Викторовна", "Замечательно. Продолжите после открытия.", "gesturing");
            yield return Thought("Даже немного приятно снова слышать их обычную перепалку.");
        }
        if (banner)
        {
            if (day == 1 || day == 2)
                yield return Say("Макс", "Ну хоть оформление привезли вовремя.", "relieved");
            else
            {
                yield return Say("Макс", "Кстати, с оформлением всё отлично. Даже удивительно.", "happy");
                yield return Say("Ольга Викторовна", "Максим, не сглазь.", "akimbo");
                yield return Say("Макс", "Всё, молчу.", "embarrassed");
            }
            yield return Narrate(bannerType == "screen"
                ? "На экране в зале появился цифровой макет. Логотип и название выглядят даже лучше, чем на обычном баннере."
                : bannerType == "delivery"
                    ? "Курьер доставил баннер прямо к открытию. Макс успел закрепить его у входа."
                    : "Баннер установлен, хотя Артёму утром и пришлось из-за него опоздать.");
        }
        else
        {
            if (bannerType == "delivery_unconfirmed")
            {
                yield return Say("Макс", "Курьер приезжал. А баннер всё ещё не здесь...", "disappointed");
                yield return Hero("Как? Он же обещал доставить вовремя!");
                yield return Say("Макс", "Поставил коробку у служебного входа. Там никого не было — её забрали на склад соседнего корпуса.", "thinking");
                yield return Narrate("Макс бежит на склад соседнего корпуса. У входа остаётся пустое крепление для баннера, а гости уже подходят.");
            }
            else if (bannerType == "screen_only")
            {
                yield return Say("Макс", "На экране заставка готова. Выглядит здорово...", "relieved");
                yield return Say("Ольга Викторовна", "А где обязательный баннер у входа? Партнёры уже спрашивали.", "akimbo");
                yield return Say("Макс", "Мы решили обойтись экраном. Я не подумал, что согласование ещё не получено.", "disappointed");
                yield return Narrate("Внутри зал оформлен красиво, но у входа нет предусмотренного договором баннера. Заказчик просит объяснений.");
            }
            else if (day == 1)
            {
                yield return Say("Макс", "С оформлением тоже... не очень сложилось.", "embarrassed");
                yield return Say("Ольга Викторовна", "Максим.", "facepalm");
                yield return Say("Макс", "Я знаю. Даже не начинайте.", "disappointed");
            }
            else if (day == 2)
            {
                yield return Say("Макс", "Может, сделаем вид, что минимализм был частью концепции?", "embarrassed");
                yield return Say("Ольга Викторовна", "Нет.", "akimbo");
                yield return Say("Макс", "Ладно, идея снята.", "disappointed");
            }
            else
            {
                yield return Say("Макс", "Так... Есть один маленький нюанс.", "embarrassed");
                yield return Say("Ольга Викторовна", "Какой?", "talking");
                yield return Say("Макс", "Оформления нет.", "disappointed");
                yield return Say("Ольга Викторовна", "Это не маленький нюанс.", "facepalm");
                yield return Say("Макс", "Я очень надеялся, что вы так не скажете.", "thinking");
            }
        }
        if (slides)
        {
            yield return Say("Ольга Викторовна", "С клиентом всё согласовано. Файл проверен.", "smile");
            yield return Hero("Дважды?");
            yield return Say("Ольга Викторовна", "Трижды.", "akimbo");
            if (day >= 3)
            {
                yield return Say("Макс", "Ого. Значит, можно выдохнуть?", "surprised");
                yield return Say("Ольга Викторовна", "После открытия.", "talking");
            }
        }
        else if (day == 1)
        {
            yield return Say("Ольга Викторовна", "И презентация у нас всё ещё неправильная.", "gesturing");
            yield return Hero(bossType == "conflict" ? "Мы это уже обсуждали." : "Я не успел согласовать цифры.");
            yield return Say("Ольга Викторовна", "К сожалению, обсуждением ничего не исправили.", "facepalm");
        }
        else if (day >= 3)
        {
            if (bossType == "budget_unconfirmed")
            {
                yield return Say("Ольга Викторовна", "Мы нашли старое вложение, но так и не подтвердили бюджет. Партнёры не приняли финальные слайды.", "thinking");
                yield return Hero("Я был уверен, что мы разобрались...");
                yield return Say("Ольга Викторовна", "Партнёры уже в зале, Артём. Позвоню им сама.", "akimbo");
            }
            else if (bossType == "waiting")
            {
                yield return Say("Ольга Викторовна", "Клиент не ответил на наш запрос. А прежнее вложение так и осталось неисправленным.", "thinking");
            }
            else
                yield return Say("Ольга Викторовна", "Клиент до сих пор не согласовал презентацию. Придётся работать без окончательного подтверждения.", "gesturing");
            yield return Hero("Я понимаю.");
            yield return Say("Ольга Викторовна", "Договорим об этом завтра.", "akimbo");
            yield return Thought("Завтра. Очень надеюсь, что оно вообще существует.");
        }
        // Ольга возвращается к заказчику. Лера подходит отдельно — не создаём
        // ещё одну искусственную сцену собрания ВСЕХ героев после открытия.
        Place("mayak", "«Маяк»", "18:40", "lera");
        if (venue)
        {
            yield return Narrate("Команда успела узнать о переносе. Гости находятся внутри, оборудование уже переместили в зал.");
            yield return Say("Лера", "Все на местах! Гости уже собираются.", "smile");
            yield return Hero("Отлично. Хотя бы сейчас без неожиданностей.");
            if (day >= 3)
                yield return Say("Лера", "Ты не поверишь, но некоторые всё равно собирались во дворе.", "happy");
        }
        else
        {
            yield return Narrate("Из-за дождя открытие перенесли внутрь, но часть оборудования до сих пор стоит на улице. Лера выбегает из здания.");
            yield return Say("Лера", "Артём! Почему оборудование всё ещё во дворе?!", "angry");
            yield return Hero("...Что?");
            yield return Say("Лера", "Я отправила сообщение в рабочий чат ещё днём! Вы его вообще открывали?", "worried");
            yield return Thought(day == 1 ? "О. Вот оно. «ВНИМАНИЕ! ИЗМЕНЕНИЕ МЕСТА ПРОВЕДЕНИЯ...»" : "Я знал о переносе. И опять не передал это остальным.");
            yield return Hero("Сейчас попробуем перенести.");
            yield return Say("Лера", "У нас десять минут!", "angry");
            yield return Hero("Да, я понимаю.");
            yield return Narrate("Артём помогает переставлять аппаратуру. Часть программы приходится сократить, гости ждут в зале.");
        }
        bool successfulOpening = banner && slides && venue;
        if (successfulOpening)
        {
            yield return Narrate("Открытие проходит спокойно. Когда заканчивается официальная часть, из зала доносятся аплодисменты.");
            if (day >= 3)
            {
                yield return Say("Лера", "Артём, спасибо за сегодня. Ты очень помог.", "smile");
                yield return Hero("Да не за что.");
                if (personalNews && leraComforted)
                {
                    yield return Say("Лера", "И спасибо за тот разговор. Мне стало легче.", "smile");
                    yield return Hero("Рад слышать. Ты решила насчёт работы?");
                    yield return Say("Лера", "Пока нет. Но уже не так страшно.", "thinking");
                    yield return Hero("Тогда не буду торопить.");
                }
                else if (day == 4 && save.lera[3] == 2 && save.leraFollow[3] == 0)
                {
                    yield return Say("Лера", "Ты обещал, что мы поговорим после открытия...", "thinking");
                    yield return Hero("Да, но меня сейчас ждут с документами. Я только на минуту.");
                    yield return Narrate("Минуты превращаются в час. Когда Артём выходит к дверям, Лера уже ушла.");
                }
                else
                {
                    yield return Say("Лера", "Кстати, я всё ещё хотела с тобой поговорить. Уже не про мероприятие.", "thinking");
                    yield return Hero("Я помню. Можем завтра?");
                    yield return Say("Лера", "Договорились.", "smile");
                    yield return Thought("Завтра... Очень надеюсь, что оно существует.");
                }
            }
        }
        else
        {
            yield return Narrate("Гости расходятся. Макс разбирает аппаратуру, а Ольга Викторовна остаётся у стойки регистрации с телефоном в руках.");
            if (day >= 3)
            {
                yield return Say("Лера", "Знаешь, не всё же было плохо.", "thinking");
                yield return Hero("Местами было очень плохо.");
                yield return Say("Лера", "Согласна. Но не все ошибки означают конец света.", "smile");
                if (leraUpset)
                {
                    yield return Say("Лера", "Тём, иногда мне кажется, что ты вообще не слышишь, что тебе говорят.", "worried");
                    yield return Hero("Наверное, так и есть. Прости.");
                    yield return Say("Лера", "Я не хочу ссориться. Просто давай потом поговорим нормально.", "thinking");
                }
                else if (leraComforted)
                {
                    yield return Say("Лера", "Спасибо, что всё-таки попытался разобраться. И выслушал меня.", "smile");
                    yield return Hero("Не всегда успешно, но я стараюсь.");
                }
                else if (day == 4 && save.lera[3] == 2 && save.leraFollow[3] == 0)
                {
                    yield return Say("Лера", "Ты говорил, что вернёшься к нашему разговору.", "worried");
                    yield return Hero("Я знаю. Прости. Я опять отвлёкся на работу.");
                    yield return Say("Лера", "Я не злюсь из-за работы. Просто не стоило обещать, если ты не готов.", "thinking");
                }
            }
            else
                yield return Thought("С трудом провели открытие. Гости недовольны, заказчик просил уточнить цифры. Просто хочу домой.");
        }
        foreach (RawImage actor in actors.Values) actor.gameObject.SetActive(false);
        yield return Narrate("После завершения программы Артём прощается с организаторами и идёт к остановке. Впереди — знакомый пешеходный переход.");
    }

    private IEnumerator Crash()
    {
        Place("evening_street", "Пешеходный переход", "19:10");
        yield return Narrate("Поздний вечер. На дороге отражается свет фонарей. Вдалеке виден грузовик, который заканчивает развозить оборудование.");
        if (day == 1)
        {
            yield return Thought("И всё-таки... мог ли я сегодня что-нибудь сделать иначе?");
            yield return Narrate("Телефон вибрирует. Артём мельком смотрит на экран и видит сообщение от Ольги Викторовны.");
            yield return Thought("Нет, спасибо. На сегодня с меня хватит.");
            yield return Narrate("Задумавшись, Артём ступает на проезжую часть, не проверив сигнал светофора.");
            yield return Hero("А?");
            yield return Say("Паша", "ЭЙ! ОСТОРОЖНО!", "alert");
            yield return Hero("ЧЕГО?!");
        }
        else
        {
            yield return Hero("Так. Сейчас должен быть грузовик.");
            yield return Narrate("Вдали появляются знакомые фары. Машина приближается к перекрёстку и замедляется.");
            yield return Hero("Ага. Я тебя вижу, железный гад.");
            yield return Thought("Отлично. Остановился. Пропускает.");
            yield return Narrate("Артём начинает переходить, не дождавшись зелёного сигнала. Паша тем временем заканчивает проверять дорогу и начинает поворот.");
            yield return Say("Паша", "ЭЙ! СТОЙ!", "alert");
            yield return Hero("ДА ЧТО Ж ТАКОЕ-ТО?!");
        }
        yield return Impact();
        yield return Say("Паша", day == 1
            ? "Да чтоб тебя!.. Скорую! Кто-нибудь, вызовите скорую!"
            : "Да я ж только начал сдавать смену...", "alert");
        HideActor("pasha");
        yield return Narrate("Несколько секунд тишины. Потом всё исчезает.");
    }

    private IEnumerator Impact()
    {
        // Только двухмерная анимация PNG: никаких 3D-объектов и физики.
        // Звук «Автомобиль» начинается на первом кадре движения грузовика,
        // после последней реплики героя. Во время удара не запускается повторно.
        nameText.text = "";
        nameText.gameObject.SetActive(false);
        lineText.text = "";
        lineText.fontStyle = FontStyle.Normal;
        next.interactable = false;
        HideActor("pasha");
        if (truck == null) yield break;
        hit.Stop();
        musicDuckedForImpact = true;
        ApplyAllAudioVolumes();
        if (impactClip != null)
        {
            hit.clip = impactClip;
            hit.loop = false;
            hit.spatialBlend = 0f;
            hit.time = 0f;
            hit.Play();
        }
        truck.gameObject.SetActive(true);
        float elapsed = 0f;
        const float duration = .68f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            truck.rectTransform.sizeDelta = Vector2.Lerp(new Vector2(158f, 160f), new Vector2(2040f, 2065f), eased);
            truck.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(310f, -80f), new Vector2(0f, -110f), eased);
            yield return null;
        }
        truck.gameObject.SetActive(false);
        bg.color = Color.black;
        yield return new WaitForSeconds(.55f);
        musicDuckedForImpact = false;
        ApplyAllAudioVolumes();
    }

    private IEnumerator Crossing()
    {
        Place("evening_street", "Пешеходный переход", "19:10");
        yield return Narrate("Снова та же улица. Вечерние огни отражаются на мокром асфальте. Неподалёку грузовик готовится повернуть у перехода.");
        if (day == 3)
        {
            yield return Thought("Опять эта дорога. В прошлый раз я даже специально смотрел на грузовик. И всё равно...");
            yield return Hero("Ну здравствуй.");
            yield return Thought("Главное сейчас — не торопиться.");
        }
        else
        {
            yield return Hero("Даже не верится, что я снова здесь. Ну привет, грузовик.");
            yield return Thought("Даже не знаю, как зовут водителя. Может, сегодня всё-таки получится добраться домой?");
        }
        yield return PickHistory(save.crossing, "",
            "Он ещё далеко. Успею перейти.",
            "Лучше подожду, пока машина проедет.",
            "Пойду к следующему переходу. Заодно проветрюсь.");
        crossingChoice = answer;
        save.crossing[day - 1] = crossingChoice;
        Store();
        // В 3-м прохождении авария неизбежна. Начиная с 4-го избежать
        // удара можно, ТОЛЬКО если дела доведены до конца И переход безопасен.
        safeRoad = crossingChoice != 0;
        if (day >= 4 && safeRoad && FourthDaySolved())
        {
            if (crossingChoice == 1)
            {
                yield return Narrate("Артём остаётся на тротуаре. Грузовик завершает поворот и скрывается за перекрёстком.");
                yield return Narrate("Загорелся зелёный свет. Артём спокойно переходит улицу.");
            }
            else
            {
                yield return Narrate("Артём сворачивает к следующему переходу. Грузовик остаётся на прежней улице.");
                yield return Narrate("Дождавшись зелёного сигнала, он переходит дорогу и направляется домой.");
            }
            yield return Thought("Тихо. Только шаги и шум вечернего города.");
            yield break; // Автомобиль и Impact() в счастливой ветке не вызываются.
        }

        if (crossingChoice == 0)
        {
            yield return Thought("Кажется, успею...");
            yield return Narrate("Артём ступает на дорогу до зелёного сигнала. Грузовик уже выходит из поворота.");
            yield return Say("Паша", "СТОЙ!!!", "alert");
            yield return Hero("Что?..");
        }
        else if (crossingChoice == 1)
        {
            yield return Hero("Подожду. Сегодня торопиться некуда.");
            yield return Narrate("Грузовик медленно проезжает перекрёсток. Артём стоит на тротуаре и ждёт, пока тот удалится.");
            yield return Narrate("Но машина вдруг резко сдаёт назад: водитель не замечает человека у края пешеходной зоны.");
            yield return Say("Паша", "ЭЙ! ОСТОРОЖНО!", "alert");
            yield return Hero("Нет... снова?..");
        }
        else
        {
            yield return Thought("Пойду другой дорогой. Хоть немного пройдусь спокойно.");
            yield return Narrate("Артём сворачивает к следующему переходу. Здесь почти пусто, слышны только шаги и редкие машины.");
            yield return Narrate("Из переулка выезжает знакомый грузовик. Водитель пытается затормозить на мокром асфальте, но машину заносит к тротуару.");
            yield return Say("Паша", "БЕРЕГИСЬ!", "alert");
            yield return Hero("Да сколько можно!..");
        }

        // Все 3 выбора приводят к удару в 3-м круге; на 4-м — только при ошибках.
        yield return Impact();
        yield return Say("Паша", "Скорую! Кто-нибудь, вызовите скорую!", "alert");
        HideActor("pasha");
        yield return Narrate("Шум улицы стихает. Наступает темнота.");
    }

    private IEnumerator GoodEnding()
    {
        // При удачной комбинации Артём впервые возвращается домой БЕЗ аварии.
        Place("bedroom_night", "Поздний вечер", "23:10");
        yield return Narrate("Дома Артём сбрасывает куртку на стул. День закончился — обычным вечером, без визга тормозов.");
        yield return Thought("Я дома. И завтра, похоже, действительно будет завтра.");
        yield return Narrate("Он выключает свет и ложится спать.");
        PlaySong("good_ending");
        Place("bedroom", "Следующее утро", "08:40");
        RingAlarm();
        yield return Narrate("Звенит будильник. Артём медленно открывает глаза и тянется к телефону.");
        yield return Narrate("На экране дата: ПЯТНИЦА, 08:40.");
        yield return Hero("...ПЯТНИЦА! ДА!");
        yield return Narrate("Телефон вибрирует. Макс пишет: «БРО. ТЫ УЖЕ ПРОСНУЛСЯ? У МЕНЯ ВОПРОС».");
        yield return Hero("МАКС, ТОЛЬКО НЕ СЕЙЧАС!");
        yield return Narrate("Новое сообщение: «Ты чё орёшь, я просто спросить хотел, какой кофе тебе брать».");
        yield return Hero("...Латте.");
        yield return Narrate("Макс: «И булочку?»");
        yield return Hero("Обязательно.");
        yield return Narrate("Макс: «Тогда жду тебя в кафе».");
        yield return Thought("Обычная пятница. Работа, сообщения, Макс со своими булочками... Как же я по этому скучал.");
        yield return Narrate("Позже Артём встречается с Максом в кафе. На столе латте, капучино и булочка — и ни одного срочного поручения.");
        End(true, "ЗАВТРА НАСТУПИТ", "Пятница. Впереди — настоящая жизнь.");
    }

    private IEnumerator GroundhogEnding()
    {
        // Плохая концовка — осознанный отказ продолжать исправлять четвёрг.
        // Может открыться даже после осторожного выбора на переходе,
        // если проблемы четвёртого дня остались.
        Place("bedroom", "Снова четверг", "08:40");
        yield return Narrate("Будильник. Артём открывает глаза и долго смотрит в потолок.");
        yield return Hero("...");
        yield return Narrate("Экран телефона: «БРО».");
        Place("office", "Знакомый кабинет", "12:15", "olga");
        yield return Say("Ольга Викторовна", "Артём, у нас проблема.", "akimbo");
        Place("mayak", "Снова «Маяк»", "16:20", "lera");
        yield return Say("Лера", "Нам нужно поговорить.", "worried");
        Place("evening_street", "Знакомый переход", "19:10");
        yield return Say("Паша", "СТОЙ!!!", "alert");
        yield return Impact();
        yield return Narrate("Вспышка. Тьма. Будильник.");
        Place("bedroom", "Снова четверг", "08:40");
        yield return Narrate("Макс опять пишет: «БРО».");
        Place("office", "Знакомый кабинет", "12:15", "olga");
        yield return Say("Ольга Викторовна", "Артём, зайди.", "talking");
        Place("mayak", "Снова «Маяк»", "16:20", "lera");
        yield return Say("Лера", "Ты меня вообще слушаешь?", "angry");
        Place("evening_street", "Знакомый переход", "19:10");
        yield return Say("Паша", "ЭЙ!!!", "alert");
        yield return Impact();
        yield return Narrate("Тишина.");
        Place("bedroom", "Утро", "08:40");
        yield return Thought("Я уже не помню, сколько раз пытался.");
        yield return Thought("Иногда день получается почти нормальным. Иногда я порчу всё с самого утра.");
        yield return Thought("Как ни менял дорогу, я всё равно слышал эти тормоза. И каждый раз просыпался в четверг.");
        yield return Narrate("Телефон вибрирует. Макс снова пишет «БРО».");
        yield return Hero("...Да, Макс? Что на этот раз?");
        End(false, "ДЕНЬ СУРКА", "Четверг. И снова тот же потолок.");
    }

    private void End(bool good, string title, string description)
    {
        if (good) save.goodUnlocked = true;
        else save.badUnlocked = true;
        Store();
        musicDuckedForImpact = false;
        ApplyAllAudioVolumes();
        story.SetActive(false);
        choices.SetActive(false);
        ending.SetActive(true);
        Texture2D finalPicture = Picture(good
            ? "Art/Backgrounds/ending_good"
            : "Art/Backgrounds/ending_bad");
        endingArtwork.texture = finalPicture;
        endingArtwork.color = finalPicture != null ? Color.white : new Color(.09f, .11f, .15f);
        if (finalPicture == null)
            Debug.LogWarning("Не найдена иллюстрация концовки в Assets/Resources/Art/Backgrounds.", this);
        endingTitleText.text = title;
        endingTitleText.color = good
            ? new Color(1f, .91f, .75f)
            : new Color(.83f, .85f, .95f);
        resultText.text = description;
        SetFastForward(false);
        if (fastForwardButton != null) fastForwardButton.gameObject.SetActive(false);
        RefreshFastForwardAvailability();
        if (musicSettingsPanel != null) musicSettingsPanel.SetActive(false);
        if (musicSettingsButton != null) musicSettingsButton.gameObject.SetActive(true);
        if (retryButton != null) retryButton.gameObject.SetActive(!good);
        if (backButton != null)
        {
            backButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(good ? 0f : 280f, -451f);
            Image background = backButton.GetComponent<Image>();
            if (background != null)
                background.color = good
                    ? new Color(.98f, .95f, .88f, .96f) // цвет светлых ответов
                    : new Color(.14f, .19f, .26f, .88f);
            Text label = backButton.GetComponentInChildren<Text>(true);
            if (label != null) label.color = good
                ? new Color(.12f, .15f, .20f) : Color.white;
        }
    }
}
