using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoodIntentions
{
    public sealed class DepartmentGame : MonoBehaviour
    {
        const float W = 1600, H = 900;
        readonly Color ink = Hex("223A39"), paper = Hex("F2E6C9"), faded = Hex("52625B"), gold = Hex("C29D54"), red = Hex("9E463D");
        [Serializable]
        sealed class IncidentIllustration
        {
            public string caseId;
            public Texture2D image;
        }

        [Header("Сцена и иллюстрации")]
        [SerializeField] Camera sceneCamera;
        [SerializeField] SpriteRenderer sceneBackground;
        [SerializeField, Tooltip("Подогнать фон под кадр 16:9 при запуске. Снимите галочку, чтобы оставить масштаб из сцены.")]
        bool fitBackgroundToCamera = true;
        [SerializeField] Texture2D portraits, clerk, intercom;
        [SerializeField, Tooltip("Укажите ID дела, например case_01, и нужную картинку. Если переопределения нет, используется art из story.json.")]
        IncidentIllustration[] caseIllustrations = Array.Empty<IncidentIllustration>();
        [SerializeField, Tooltip("Вариант иллюстрации дела «Стеклянный двор», если Нина уже погибла. По умолчанию используется greenhouse_without_nina из Resources.")]
        Texture2D greenhouseWithoutNina;
        [SerializeField, Tooltip("Вариант «Стеклянного двора», если Павел уже погиб.")]
        Texture2D greenhouseWithoutPavel;
        [SerializeField, Tooltip("Вариант «Стеклянного двора», если Нина и Павел уже погибли.")]
        Texture2D greenhouseWithoutBoth;
        [SerializeField] TextAsset storySource;

        [Header("Главное меню")]
        [SerializeField, TextArea(2, 3)] string menuTitle = "Отдел благих\nнамерений";
        [SerializeField] Vector2 menuPanelPosition = new Vector2(160, 105);
        [SerializeField] Vector2 staffCardPosition = new Vector2(1080, 334);

        [Header("Шрифты")]
        [SerializeField] Font sans, bold, serif;
        [Header("Музыка")]
        [SerializeField] AudioClip menuTrack, lightTrack, darkTrack;
        [SerializeField, Range(0f, 1f)] float defaultVolume = .65f;

        Story story;
        GameModel game;
        Texture2D office;
        bool renderSceneBackground;
        Texture2D[] incidentArt;
        GUIStyle body, small, heading, title, button, tiny;
        AudioSource audioSource, musicA, musicB;
        AudioClip activeTrack;
        AudioSource activeMusic, fadingMusic;
        float masterVolume = .65f;
        AudioClip click, stamp;
        bool initialized, inMenu = true, storyStarted, journalOpen, journalJumpToFinale, helpOpen, newConfirm, muted, artOpen, orderConfirmOpen;
        string error = "", toast = "";
        float toastUntil, stampAt = -10, darkness;
        float uiScale = 1, stylesAtScale = -1;
        int selectedPerson, selectedChoice = -1, dossierTab, endingPage;
        Vector2 dossierScroll, journalScroll, introScroll, caseScroll, briefingScroll, helpScroll, endingScroll, revealScroll, reportScroll;

        void Awake()
        {
            Application.targetFrameRate = 60;
            error = "";
            activeTrack = null;
            try
            {
                TextAsset data = storySource != null ? storySource : Resources.Load<TextAsset>("story");
                if (data == null) throw new InvalidOperationException("Не найден Resources/story.json");
                story = JsonUtility.FromJson<Story>(data.text);
                if (sceneCamera == null) sceneCamera = Camera.main;
                renderSceneBackground = sceneCamera != null && sceneCamera.isActiveAndEnabled && sceneBackground != null
                    && sceneBackground.enabled && sceneBackground.gameObject.activeInHierarchy && sceneBackground.sprite != null;
                if (renderSceneBackground)
                {
                    FitSceneBackground();
                    UpdateCameraViewport();
                }
                else office = LoadTexture("Art/office"); // Also supports older scene files.
                if (portraits == null) portraits = LoadTexture("Art/portraits");
                if (clerk == null) clerk = LoadTexture("Art/clerk");
                if (intercom == null) intercom = LoadTexture("Art/intercom");
                incidentArt = new Texture2D[story.events.Length];
                for (int i = 0; i < story.events.Length; i++) incidentArt[i] = GetIncidentImage(story.events[i]);
                if (greenhouseWithoutNina == null) greenhouseWithoutNina = LoadTexture("Art/greenhouse_without_nina");
                if (greenhouseWithoutPavel == null) greenhouseWithoutPavel = LoadTexture("Art/greenhouse_without_pavel");
                if (greenhouseWithoutBoth == null) greenhouseWithoutBoth = LoadTexture("Art/greenhouse_without_both");
                if (sans == null) sans = LoadFont("Fonts/DejaVuSans");
                if (bold == null) bold = LoadFont("Fonts/DejaVuSans-Bold");
                if (serif == null) serif = LoadFont("Fonts/DejaVuSerif");
                var missing = new List<string>();
                if (!renderSceneBackground && office == null) missing.Add("Art/office.png");
                if (portraits == null) missing.Add("Art/portraits.png");
                if (clerk == null) missing.Add("Art/clerk.png");
                if (intercom == null) missing.Add("Art/intercom.png");
                if (sans == null) missing.Add("Fonts/DejaVuSans.ttf");
                if (bold == null) missing.Add("Fonts/DejaVuSans-Bold.ttf");
                if (serif == null) missing.Add("Fonts/DejaVuSerif.ttf");
                for (int i = 0; i < story.events.Length; i++)
                    if (!string.IsNullOrEmpty(story.events[i].art) && incidentArt[i] == null)
                        missing.Add("Art/" + story.events[i].art + ".png");
                if (missing.Count > 0)
                    throw new InvalidOperationException("Не загружены ресурсы:\n" + string.Join("\n", missing.ToArray())
                        + "\n\nОстановите Play и выберите Tools → Отдел благих намерений → Исправить импорт ресурсов.");
                ResetRun(true);
                muted = PlayerPrefs.GetInt("department-muted", 0) == 1;
                audioSource = gameObject.AddComponent<AudioSource>();
                musicA = gameObject.AddComponent<AudioSource>();
                musicB = gameObject.AddComponent<AudioSource>();
                musicA.playOnAwake = musicB.playOnAwake = false;
                musicA.loop = musicB.loop = true;
                musicA.spatialBlend = musicB.spatialBlend = 0;
                audioSource.spatialBlend = 0;
                if (menuTrack == null) menuTrack = Resources.Load<AudioClip>("Audio/menu");
                if (lightTrack == null) lightTrack = Resources.Load<AudioClip>("Audio/light");
                if (darkTrack == null) darkTrack = Resources.Load<AudioClip>("Audio/dark");
                if (menuTrack == null || lightTrack == null || darkTrack == null)
                    throw new InvalidOperationException("Не найдены музыкальные файлы в Resources/Audio.");
                masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("department-volume", defaultVolume));
                click = MakeSound(false); stamp = MakeSound(true);
                activeMusic = musicA; fadingMusic = musicB;
                SwitchMusic(menuTrack);
            }
            catch (Exception ex) { error = ex.ToString(); Debug.LogException(ex); }
        }
        Texture2D GetIncidentImage(Incident incident)
        {
            if (caseIllustrations != null)
                foreach (IncidentIllustration item in caseIllustrations)
                    if (item != null && item.caseId == incident.id && item.image != null) return item.image;
            return string.IsNullOrEmpty(incident.art) ? null : LoadTexture("Art/" + incident.art);
        }

        Texture2D CurrentIncidentIllustration()
        {
            Incident incident = game.Current;
            if (incident == null) return null;
            // If a variant is missing, leave it blank rather than showing a character who died.
            if (incident.id == "case_15")
            {
                bool ninaDead = game.Matches("dead:nina");
                bool pavelDead = game.Matches("dead:pavel");
                if (ninaDead && pavelDead) return greenhouseWithoutBoth;
                if (pavelDead) return greenhouseWithoutPavel;
                if (ninaDead) return greenhouseWithoutNina;
            }
            return incidentArt[game.State.step];
        }

        void FitSceneBackground()
        {
            if (!fitBackgroundToCamera || !sceneCamera.orthographic) return;
            Bounds spriteBounds = sceneBackground.sprite.bounds;
            if (spriteBounds.size.x <= 0 || spriteBounds.size.y <= 0) return;
            float height = sceneCamera.orthographicSize * 2f;
            float scale = Mathf.Max(height * (W / H) / spriteBounds.size.x, height / spriteBounds.size.y);
            sceneBackground.transform.localScale = new Vector3(scale, scale, 1);
        }

        void UpdateCameraViewport()
        {
            if (!renderSceneBackground || Screen.width <= 0 || Screen.height <= 0) return;
            float scale = Mathf.Min(Screen.width / W, Screen.height / H);
            float w = W * scale / Screen.width, h = H * scale / Screen.height;
            sceneCamera.rect = new Rect((1f - w) / 2, (1f - h) / 2, w, h);
            sceneCamera.aspect = W / H;
        }

        // Resolve both main assets and sub-assets; some import presets change the main object type.
        static Texture2D LoadTexture(string path)
        {
            Texture2D result = Resources.Load<Texture2D>(path);
            if (result != null) return result;
            foreach (Texture2D texture in Resources.LoadAll<Texture2D>(path)) if (texture != null) return texture;
            Sprite sprite = Resources.Load<Sprite>(path);
            return sprite != null ? sprite.texture : null;
        }
        static Font LoadFont(string path)
        {
            Font result = Resources.Load<Font>(path);
            if (result == null)
                foreach (Font font in Resources.LoadAll<Font>(path)) if (font != null) { result = font; break; }
            if (result != null) return result;
            Debug.LogWarning("Не импортирован шрифт Resources/" + path
                + ". Используется встроенный шрифт. Выполните Tools → Отдел благих намерений → Исправить импорт ресурсов.");
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        void Update()
        {
            if (game == null || error.Length > 0) return;
            UpdateCameraViewport();
            darkness = Mathf.MoveTowards(darkness, game.Dead > 0 ? 1 : 0, Time.unscaledDeltaTime * .6f);
            // During the successful-looking rescue route, switch to dark music at the
            // collapse itself; casualties are registered only after the final story card.
            bool rescueCollapse = !inMenu && game.Current != null
                && game.Current.id == "case_16" && !game.State.pendingReport
                && game.ElenaHasRescueTeam && game.State.finalePage >= 5;
            AudioClip wanted = inMenu ? menuTrack : (game.Dead > 0 || rescueCollapse ? darkTrack : lightTrack);
            if (wanted != null && wanted != activeTrack) SwitchMusic(wanted);
            // Audio tracks are loudness-matched during import; the slider must reach 100% gain.
            float target = muted ? 0f : masterVolume;
            if (activeMusic != null) activeMusic.volume = Mathf.MoveTowards(activeMusic.volume, target, Time.unscaledDeltaTime * .42f);
            if (fadingMusic != null)
            {
                fadingMusic.volume = Mathf.MoveTowards(fadingMusic.volume, 0, Time.unscaledDeltaTime * .42f);
                if (fadingMusic.volume <= .001f && fadingMusic.isPlaying) fadingMusic.Stop();
            }
        }
        void SwitchMusic(AudioClip next)
        {
            if (next == null || next == activeTrack) return;
            AudioSource old = activeMusic;
            AudioSource incoming = fadingMusic;
            incoming.Stop();
            incoming.clip = next;
            incoming.volume = 0;
            incoming.Play();
            activeMusic = incoming;
            fadingMusic = old;
            activeTrack = next;
        }
        void DrawVolume(Rect r, Color color)
        {
            Text(new Rect(r.x, r.y, 135, r.height), "Громкость", small, color);
            var slider = new GUIStyle(GUI.skin.horizontalSlider);
            var thumb = new GUIStyle(GUI.skin.horizontalSliderThumb);
            float value = GUI.HorizontalSlider(Px(new Rect(r.x + 136, r.y + 7, r.width - 222, 22)), masterVolume, 0f, 1f, slider, thumb);
            if (!Mathf.Approximately(value, masterVolume))
            {
                masterVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat("department-volume", masterVolume);
                PlayerPrefs.Save();
            }
            Text(new Rect(r.xMax - 77, r.y, 77, r.height), Mathf.RoundToInt(masterVolume * 100) + "%", small, color);
        }
        void OnDestroy()
        {
            if (click != null) Destroy(click);
            if (stamp != null) Destroy(stamp);

        }
        void InitStyles()
        {
            if (initialized && Mathf.Approximately(stylesAtScale, uiScale)) return;
            stylesAtScale = uiScale;
            body = Style(sans, 21, ink); small = Style(sans, 17, faded); tiny = Style(sans, 14, faded);
            heading = Style(bold, 23, ink); title = Style(serif, 37, ink);
            button = Style(bold, 19, paper); button.alignment = TextAnchor.MiddleCenter;
            initialized = true;
        }
        GUIStyle Style(Font font, int size, Color color)
        {
            // Plain text has no inherited skin hover/focus/background states.
            var s = new GUIStyle { font = font, fontSize = PixelSize(size), wordWrap = true,
                richText = false, alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            TextColor(s, color); return s;
        }
        void OnGUI()
        {
            GUI.depth = 0;
            GUI.matrix = Matrix4x4.identity;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            uiScale = Mathf.Min(Screen.width / W, Screen.height / H);
            float left = Mathf.Round((Screen.width - W * uiScale) / 2);
            float top = Mathf.Round((Screen.height - H * uiScale) / 2);
            if (!renderSceneBackground || error.Length > 0)
                Fill(new Rect(0, 0, Screen.width / uiScale, Screen.height / uiScale), ink);
            else
            {
                float wholeW = Screen.width / uiScale, wholeH = Screen.height / uiScale;
                float x = left / uiScale, y = top / uiScale;
                if (x > 0)
                {
                    Fill(new Rect(0, 0, x, wholeH), ink);
                    Fill(new Rect(x + W, 0, Mathf.Max(0, wholeW - x - W), wholeH), ink);
                }
                if (y > 0)
                {
                    Fill(new Rect(0, 0, wholeW, y), ink);
                    Fill(new Rect(0, y + H, wholeW, Mathf.Max(0, wholeH - y - H)), ink);
                }
            }
            // Translation only. Glyphs are rasterized at their final pixel size instead of magnifying a small font atlas.
            GUI.matrix = Matrix4x4.TRS(new Vector3(left, top, 0), Quaternion.identity, Vector3.one);
            if (error.Length > 0)
            {
                var errorStyle = new GUIStyle(GUI.skin.label) { fontSize = PixelSize(23), wordWrap = true };
                errorStyle.normal.textColor = paper;
                GUI.Label(Px(new Rect(340, 250, 920, 500)), "Ошибка загрузки проекта:\n" + error, errorStyle);
                GUI.matrix = Matrix4x4.identity; return;
            }
            InitStyles();
            if (!renderSceneBackground) GUI.DrawTexture(Px(new Rect(0, 0, W, H)), office, ScaleMode.ScaleAndCrop);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                if (orderConfirmOpen) orderConfirmOpen = false;
                else if (artOpen) artOpen = false;
                else if (newConfirm) newConfirm = false;
                else if (helpOpen) helpOpen = false;
                else if (journalOpen) journalOpen = false;
                else if (storyStarted && !ShowFirstHurtMessage()) inMenu = !inMenu;
                Event.current.Use();
            }
            bool bossPopup = ShowFirstHurtMessage();
            bool modal = orderConfirmOpen || artOpen || journalOpen || helpOpen || newConfirm || bossPopup || (!inMenu && !game.Finished && game.Dead > 0 && !game.State.revealAcknowledged);
            GUI.enabled = !modal;
            if (inMenu) DrawMenu();
            else if (!game.State.introComplete) DrawIntro();
            else if (game.Finished) DrawEnding();
            else if (game.State.briefedShift < game.Shift) DrawBriefing();
            else if (game.Current.id == "case_16" && !game.State.pendingReport) DrawFinalIncident();
            else DrawDesk();
            GUI.enabled = true;
            if (artOpen) DrawArt();
            if (journalOpen) DrawJournal();
            if (helpOpen) DrawHelp();
            if (newConfirm) DrawNewConfirmation();
            if (orderConfirmOpen) DrawOrderWarning();
            if (!inMenu && !game.Finished && game.Dead > 0 && !game.State.revealAcknowledged) DrawReveal();
            else if (bossPopup) DrawFirstHurtMessage();
            if (toastUntil > Time.realtimeSinceStartup)
            {
                Fill(new Rect(350, 850, 900, 38), ink);
                Text(new Rect(365, 856, 870, 30), toast, small, paper);
            }
            GUI.matrix = Matrix4x4.identity;
        }
        Rect MenuRect(float x, float y, float w, float h)
        {
            return new Rect(menuPanelPosition.x + x - 160, menuPanelPosition.y + y - 105, w, h);
        }

        Rect StaffRect(float x, float y, float w, float h)
        {
            return new Rect(staffCardPosition.x + x - 1080, staffCardPosition.y + y - 334, w, h);
        }

        void DrawMenu()
        {
            Paper(MenuRect(160, 105, 760, 685));
            Text(MenuRect(211, 149, 640, 30), "НЕБЕСНАЯ КАНЦЕЛЯРИЯ  /  УЧАСТОК 07", small, faded);
            Text(MenuRect(205, 207, 660, 160), menuTitle, Style(serif, 58, ink));
            Rect divider = MenuRect(210, 390, 650, 1);
            Line(divider.x, divider.y, divider.width, gold);
            Text(MenuRect(210, 419, 640, 90), "Десять подопечных. Четыре смены.\nУ каждого спасения есть последствия.", body);
            Text(MenuRect(210, 532, 650, 65), "Читайте досье. Учитывайте связи.\nУтверждайте решения осторожно.", small);
            if (Btn(MenuRect(210, 620, 305, 56), storyStarted ? "Продолжить" : "Начать историю", ink))
            {
                if (storyStarted) inMenu = false;
                else StartNew();
            }
            if (storyStarted && Btn(MenuRect(533, 620, 325, 56), "Новая история", red)) newConfirm = true;
            DrawVolume(MenuRect(210, 697, 475, 32), ink);
            if (Btn(MenuRect(705, 690, 153, 43), muted ? "Включить" : "Выключить", ink, paper, 16)) ToggleSound();
            Paper(StaffRect(1080, 334, 330, 475));
            DrawClerk(StaffRect(1104, 352, 282, 282));
            Text(StaffRect(1100, 649, 300, 25), "ЛИЧНОЕ ДЕЛО СОТРУДНИКА", tiny, ink);
            string staffText = game.Dead > 0
                ? "ДОПУСК: ИЗМЕНЁН\n\nНазначение уточнено после первого закрытого дела.\n\nОставшийся срок службы: до окончания четвёртой смены."
                : "ДОПУСК: ВРЕМЕННЫЙ\n\nЛичные сведения скрыты.\nНазначение: участок 07.\nСрок: четыре смены.";
            Text(StaffRect(1100, 683, 298, 115), staffText, Style(sans, 16, faded));
        }
        static readonly string[] IntroTitles = {
            "Добро пожаловать на участок", "Десять личных дел", "Как вмешиваться", "Ваша первая смена"
        };
        static readonly string[] IntroWords = {
            "А, новый хранитель. Проходите. Крылья в дверях не прищемили? Уже неплохое начало.\n\nЯ Аврелий, заведующий седьмым участком. Вы поступили в Отдел благих намерений. Здесь мы следим, чтобы человеческая жизнь не оборвалась раньше положенного.\n\nВаш стол у окна. Чернила не трогайте — печать сама знает, куда ложиться.",
            "Перед вами десять человек. Они не знают ни вас, ни меня. Для них наша работа выглядит как внезапная мысль, знакомый голос или очень удачное опоздание.\n\nСначала прочитайте личные дела. Кто кому доверяет, кто бросится помогать, даже если не должен, и чья привычка может изменить исход. Иногда одна такая подробность полезнее любого чуда.",
            "Входящие происшествия будут появляться в центре стола. Прочтите карточку, найдите нужное досье и выберите вмешательство. Пока не утвердили решение, его можно изменить.\n\nНа каждую смену вам положены три печати для особых вмешательств. Обычный толчок судьбы ничего не стоит. Настоящее чудо требует расхода.\n\nПосле подписи придёт акт. Читайте его целиком: последствия иногда уходят совсем в другое дело.",
            "Четыре смены, по четыре поручения. Сначала — несколько повседневных происшествий. Для каждого найдётся не один способ вмешаться.\n\nПроведите людей через происшествия. После каждого вмешательства сверяйте их состояние. Особое распоряжение № 010: сохранить жизнь Елены Воронцовой до конца отчётного периода.\n\nНе пугайтесь, если спасение окажется неидеальным. Отдел ценит прежде всего благие намерения.\n\nГотовы принять участок?"
        };
        static readonly string[] IntroReplies = {
            "Кого мне предстоит оберегать?", "Как я смогу им помочь?", "Какой у нас план?", "Принять участок"
        };
        void DrawIntro()
        {
            int page = game.State.introPage;
            Paper(new Rect(190, 111, 1220, 683));
            Text(new Rect(235, 148, 1110, 30), "ВНУТРЕННЯЯ СВЯЗЬ   /   ПЕРВИЧНЫЙ ИНСТРУКТАЖ", small);
            Line(235, 195, 1130, gold);
            Text(new Rect(235, 233, 322, 65), "Аврелий", title);
            DrawClerk(new Rect(250, 416, 265, 265));
            Text(new Rect(246, 695, 290, 34), "Вы — новый хранитель", small);
            Text(new Rect(600, 232, 750, 68), IntroTitles[page], Style(serif, 31, ink));
            GUIStyle introBody = Style(sans, 21, ink);
            float introHeight = TextHeight(introBody, IntroWords[page], 735) + 45;
            introScroll = GUI.BeginScrollView(Px(new Rect(600, 306, 752, 352)), introScroll,
                Px(new Rect(0, 0, 720, Mathf.Max(352, introHeight))));
            Text(new Rect(0, 0, 720, introHeight), IntroWords[page], introBody);
            GUI.EndScrollView();
            Text(new Rect(600, 657, 300, 29), "Инструктаж  " + (page + 1) + " / 4", small);
            if (page > 0 && Btn(new Rect(600, 713, 150, 44), "Назад", Color.clear, ink, 17))
            {
                introScroll = Vector2.zero;
                game.State.introPage--;
            }
            if (Btn(new Rect(231, 714, 338, 44), "Пропустить инструктаж", Color.clear, ink, 17))
            {
                CompleteIntro(true);
                Sound(click);
                return;
            }
            if (Btn(new Rect(770, 707, 578, 53), IntroReplies[page], ink, paper, 18))
            {
                Sound(click);
                if (page < 3)
                {
                    introScroll = Vector2.zero;
                    game.State.introPage++;
                }
                else CompleteIntro(false);
            }
        }
        void DrawBriefing()
        {
            int shift = game.Shift;
            Paper(new Rect(200, 123, 1200, 652));
            Text(new Rect(245, 157, 1100, 31), "РАСПОРЯЖЕНИЕ ЗАВЕДУЮЩЕГО   /   СМЕНА " + shift + " ИЗ 4", small);
            string[] titles = { "Первый обход", "Дела не сходятся", "Ночной реестр", "До рассвета" };
            Text(new Rect(243, 218, 1090, 70), titles[shift - 1], title);
            string note;
            if (shift == 1)
                note = "Начните с Сони Беловой. Она идёт под вывеской и ещё не знает, что ей нужна помощь.\n\nОткройте её досье, прочитайте важную деталь и только после этого решайте, куда подтолкнуть судьбу.\n\nЧетыре поручения. Три печати. Особое распоряжение № 010: сохранить Елене Воронцовой жизнь до завершения отчётного периода.";
            else if (shift == 2)
                note = game.Dead > 0
                    ? "Первое закрытое дело принято. Вижу, вы уже заметили перемену в оперении. Продолжайте работу.\n\nПодопечные начали сравнивать свои истории. Следите за событиями в библиотеке и за журналисткой на крыше.\n\nПлан выдан. Дополнительные вопросы можно подшить к отчёту."
                    : "Утренние дела прошли без потерь. Любопытно. Продолжайте внимательнее следить за состоянием подопечных.\n\nТеперь библиотека, эвакуация и крыша архива. Люди начали замечать совпадения. Не забывайте о последствиях вмешательств.\n\nВаш запас печатей отмечен в распоряжении ниже.";
            else if (shift == 3)
                note = "Город не успевает прийти в себя. Сегодня снова поступили дела из кафе, библиотеки и депо.\n\nБудьте внимательны: люди, получившие травмы раньше, могут не справиться с новым поручением. Размер выдачи указан внизу распоряжения.\n\nОсобое распоряжение о Елене Воронцовой сохраняет силу.";
            else
                note = "Последняя смена. Начните с ночных происшествий у старой арки, архивного корпуса и теплицы.\n\nК рассвету потребуется итоговый отчёт о сохранности Елены Воронцовой. Её защиту нельзя передать другому хранителю.\n\nПроверьте состояние людей и запас печатей: некоторые последствия предыдущих вмешательств теперь особенно важны.";
            GUIStyle briefingBody = Style(sans, 23, ink);
            float briefingHeight = TextHeight(briefingBody, note, 550) + 28;
            briefingScroll = GUI.BeginScrollView(Px(new Rect(246, 320, 578, 317)), briefingScroll,
                Px(new Rect(0, 0, 550, Mathf.Max(317, briefingHeight))));
            Text(new Rect(0, 0, 550, briefingHeight), note, briefingBody);
            GUI.EndScrollView();
            Text(new Rect(896, 331, 432, 34), "ВХОДЯЩИЕ ПОРУЧЕНИЯ", heading);
            for (int i = 0; i < 4; i++)
            {
                Incident item = story.events[(shift - 1) * 4 + i];
                Text(new Rect(898, 397 + i * 57, 432, 47), item.time + "  ·  " + item.title, Style(sans, 20, ink));
            }
            Line(246, 653, 1086, gold);
            string allowance = game.State.seals < GameModel.SealsPerShift
                ? "  ·  Выдача сокращена по дисциплинарному взысканию"
                : "  ·  Досье бесплатные. Читайте!";
            Text(new Rect(246, 678, 740, 48), "Печати: " + game.State.seals + " / " + GameModel.SealsPerShift + allowance, small);
            if (Btn(new Rect(900, 685, 433, 52), "Приступить к смене", ink))
            { game.State.briefedShift = shift; Sound(click); }
        }
        void DrawDesk()
        {
            // One compact control strip: no repeated game title or precinct label.
            Fill(new Rect(25, 22, 1550, 92), new Color(ink.r, ink.g, ink.b, .96f));
            Text(new Rect(48, 35, 590, 38), "Смена " + game.Shift + " из 4   •   " + game.Current.time, heading, paper);
            string discipline = game.State.totalViolations > 0
                ? "   ·   Нарушения: " + game.State.totalViolations
                : "";
            if (game.State.pendingSealReduction > 0)
                discipline += "   ·   След. выдача: " + (GameModel.SealsPerShift - game.State.pendingSealReduction) + "/" + GameModel.SealsPerShift;
            Text(new Rect(48, 76, 1140, 24), "Печати: " + game.State.seals + " / " + GameModel.SealsPerShift
                + discipline, small, gold);
            DrawClerk(new Rect(1345, 31, 79, 79));
            if (Btn(new Rect(1442, 35, 108, 35), "Меню", paper, ink)) inMenu = true;
            if (Btn(new Rect(1442, 75, 108, 31), "?", new Color(0, 0, 0, 0), paper)) helpOpen = true;
            DrawRoster(); DrawDossier();
            if (game.State.pendingReport) DrawReport(); else DrawCase();
            if (Btn(new Rect(42, 825, 258, 50), "Журнал решений", ink)) journalOpen = true;
            DrawVolume(new Rect(1150, 833, 293, 32), paper);
            if (Btn(new Rect(1461, 824, 119, 50), muted ? "Вкл." : "Выкл.", ink, paper, 16)) ToggleSound();
        }
        void DrawRoster()
        {
            Paper(new Rect(40, 132, 312, 678));
            Text(new Rect(57, 150, 276, 30), "ПОДОПЕЧНЫЕ  /  " + story.people.Length, Style(bold, 19, ink));
            for (int i = 0; i < story.people.Length; i++)
            {
                Person p = story.people[i]; PersonState status = game.Status(p.id);
                Rect row = new Rect(51, 194 + i * 55, 290, 51);
                bool selected = selectedPerson == i;
                bool protectedPerson = p.id == "elena";
                Fill(row, selected ? ink : (i % 2 == 0 ? new Color(0, 0, 0, .035f) : Color.clear));
                if (protectedPerson)
                {
                    Fill(new Rect(row.x, row.y, row.width, 2), gold);
                    Fill(new Rect(row.x, row.yMax - 2, row.width, 2), gold);
                    Fill(new Rect(row.x, row.y, 2, row.height), gold);
                    Fill(new Rect(row.xMax - 2, row.y, 2, row.height), gold);
                }
                Portrait(new Rect(row.x + 3, row.y + 3, 48, 48), i, status.health == 0);
                Text(new Rect(row.x + 55, row.y + 4, 225, 26), p.name.Split(' ')[0], Style(bold, 21, ink), selected ? paper : ink);
                Text(new Rect(row.x + 55, row.y + 30, 225, 18), protectedPerson ? "Под особой охраной" : StatusName(status.health), tiny, selected ? gold : (protectedPerson ? gold : StatusColor(status.health)));
                if (GUI.Button(Px(row), GUIContent.none, GUIStyle.none)) OpenDossier(i);
            }
        }
        void DrawDossier()
        {
            Paper(new Rect(1150, 132, 410, 678));
            Person p = story.people[selectedPerson]; PersonState ps = game.Status(p.id);
            Text(new Rect(1173, 150, 355, 26), "ЛИЧНОЕ ДЕЛО  № " + (selectedPerson + 1).ToString("000"), small);
            Portrait(new Rect(1173, 192, 129, 129), selectedPerson, ps.health == 0);
            Text(new Rect(1318, 193, 220, 66), p.name, heading);
            Text(new Rect(1318, 261, 219, 55), p.age + " лет\n" + p.job, small);
            for (int i = 0; i < 3; i++)
            {
                if (Btn(new Rect(1173 + i * 122, 339, 116, 37), new[] { "Личное", "Связи", "История" }[i], dossierTab == i ? ink : new Color(0, 0, 0, .055f), dossierTab == i ? paper : ink, 16))
                { dossierTab = i; dossierScroll = Vector2.zero; MarkRead(p.id); }
            }
            Rect area = new Rect(1174, 397, 362, 327);
            const float dossierWidth = 327;
            float height = 0;
            if (dossierTab == 0)
                height = DossierHeight(new[] { "О ПОДОПЕЧНОМ", "ЧТО НЕ ПИШУТ В АНКЕТАХ", "ВАЖНАЯ ДЕТАЛЬ" },
                    new[] { p.detail, p.secret, p.clue }, dossierWidth);
            else if (dossierTab == 1)
                height = DossierHeight(new[] { "СВЯЗИ" },
                    new[] { p.links }, dossierWidth);
            else
            {
                string history = ps.notes.Count == 0 ? "В деле пока нет записей о происшествиях." : string.Join("\n\n", ps.notes.ToArray());
                height = DossierHeight(new[] { "СОСТОЯНИЕ", "ИСТОРИЯ" },
                    new[] { StatusName(ps.health), history }, dossierWidth);
            }
            dossierScroll = GUI.BeginScrollView(Px(area), dossierScroll,
                Px(new Rect(0, 0, dossierWidth, Mathf.Max(height, area.height))));
            if (dossierTab == 0)
                DrawDossierSections(new[] { "О ПОДОПЕЧНОМ", "ЧТО НЕ ПИШУТ В АНКЕТАХ", "ВАЖНАЯ ДЕТАЛЬ" },
                    new[] { p.detail, p.secret, p.clue }, dossierWidth);
            else if (dossierTab == 1)
                DrawDossierSections(new[] { "СВЯЗИ" },
                    new[] { p.links }, dossierWidth);
            else
            {
                string history = ps.notes.Count == 0 ? "В деле пока нет записей о происшествиях." : string.Join("\n\n", ps.notes.ToArray());
                DrawDossierSections(new[] { "СОСТОЯНИЕ", "ИСТОРИЯ" },
                    new[] { StatusName(ps.health), history }, dossierWidth);
            }
            GUI.EndScrollView();
            Line(1174, 738, 362, gold);
            bool read = game.State.read.Contains(p.id);
            if (read) Text(new Rect(1174, 758, 362, 30), "Досье изучено", small, faded);
            else if (Btn(new Rect(1174, 750, 362, 39), "Изучить досье", ink, paper, 17)) MarkRead(p.id);
        }
        float DossierHeight(string[] headings, string[] paragraphs, float width)
        {
            float y = 8;
            for (int i = 0; i < headings.Length; i++)
            {
                y += 25;
                y += TextHeight(body, paragraphs[i], width) + 25;
            }
            return y + 12;
        }
        void DrawDossierSections(string[] headings, string[] paragraphs, float width)
        {
            float y = 8;
            for (int i = 0; i < headings.Length; i++)
            {
                Text(new Rect(0, y, width, 25), headings[i], Style(bold, 16, Hex("84652D")));
                y += 25;
                float h = TextHeight(body, paragraphs[i], width) + 8;
                Text(new Rect(0, y, width, h), paragraphs[i], body);
                y += h + 17;
            }
        }
        void DrawScrollableCaseText(Rect area, string content)
        {
            GUIStyle caseBody = Style(sans, 20, ink);
            float contentWidth = area.width - 23;
            float contentHeight = TextHeight(caseBody, content, contentWidth) + 28;
            caseScroll = GUI.BeginScrollView(Px(area), caseScroll,
                Px(new Rect(0, 0, contentWidth, Mathf.Max(area.height, contentHeight))));
            Text(new Rect(0, 0, contentWidth, contentHeight), content, caseBody);
            GUI.EndScrollView();
        }

        string CurrentCaseBody()
        {
            Incident incident = game.Current;
            if (incident != null && !string.IsNullOrEmpty(incident.bodyIfDeadAlisa)
                && game.Status("alisa") != null && game.Status("alisa").health == 0)
                return incident.bodyIfDeadAlisa;
            if (incident != null && incident.id == "case_03" && game.Matches("dead:mark"))
                return "Елена поскальзывается возле открытого люка. Порыв ветра на мгновение удерживает её от падения.\n\n"
                    + "Поблизости Лев и Нина. У Елены в руках тяжёлая сумка с книгами.";
            if (incident != null && incident.id == "case_04" && game.Matches("dead:mark"))
                return "В старом переходе начинает обрушаться потолок. Елена помогает Льву идти, но выход уже завален пылью.\n\n"
                    + "Неподалёку находится дежурный, который может помочь Елене выбраться. "
                    + (game.Matches("dead:sonya")
                        ? "Другие помощники ещё не добрались до перехода."
                        : "Соня получила вызов и спешит к месту происшествия.");
            if (incident != null && incident.id == "case_07" && game.Matches("dead:nina"))
                return "После сигнала тревоги жильцы соседнего дома выходят на улицу. Узкий проход быстро заполняется людьми.\n\n"
                    + "Илья знает путь через переулок. Вера помогает вывести людей, а Соня и Павел ждут у тяжёлой двери. "
                    + "Двор теплицы закрыт: после гибели Нины некому открыть калитку.";
            if (incident != null && incident.id == "case_11" && game.Matches("dead:nina"))
                return "После вечерних происшествий люди ищут безопасное место на ночь. Соня проверяет, все ли вернулись.\n\n"
                    + "Двор теплицы закрыт. Елена предлагает разместить раненых в читальном зале, подальше от архивного крыла, где давно ждут ремонта."
                    + (game.Matches("dead:mark") ? " На лестнице требуется помощь ещё одного человека." : "");
            if (incident != null && incident.id == "case_11" && game.Status("mark").health == 0)
                return "После вечерних происшествий подопечные ищут безопасное место. Соня проверяет раненых, Елена предлагает библиотеку, а двор теплицы остаётся запасным укрытием. Людям с травмами нужна помощь на лестнице.";
            if (incident != null && incident.id == "case_12" && game.Status("vera").health == 0)
                return "В трамвайном депо отказал тормоз служебного вагона. Вера погибла раньше. У ворот Илья пытается предупредить рабочих и замечает стрелку у запасного пути. Вагон уже движется под уклон.";
            if (incident != null && incident.id == "case_13" &&
                (game.Status("sonya").health == 0 || game.Status("mark").health == 0))
                return "Под старой аркой осыпается кладка. " +
                    (game.Matches("dead:sonya")
                        ? "На опасный проход выходят случайные прохожие. "
                        : (game.Matches("dead:mark") ? "Соня ищет тех, кто не вернулся после происшествий. " : "Соня пришла на поиски брата. ")) +
                    (game.Matches("dead:mark") ? "У разрушающейся лестницы лежат обломки. " : "Марк стоит у разрушающегося прохода. ") +
                    "Проход вот-вот рухнет, и новое вмешательство может изменить судьбу того, кто ещё остался.";
            if (incident != null && incident.id == "case_14" && game.Status("pavel").health == 0)
                return "После скачка напряжения над архивным корпусом оборвалась линия питания. Павел погиб раньше, поэтому ремонтники не решаются подниматься на повреждённый карниз. Провод опасно раскачивается над переулком.";
            if (incident != null && incident.id == "case_15" && game.Status("nina").health == 0)
                return "После прежних происшествий Нины уже нет в живых. Её теплица осталась открытой для соседей, которым негде ждать рассвета.\n\nВетер разбивает рамы крыши. Посетители пытаются найти выход самостоятельно. "
                    + (game.Status("sonya").health == 0 ? "Соня тоже погибла раньше, поэтому помогать им некому." : "Соня старается вывести людей через калитку.");
            if (incident != null && !string.IsNullOrEmpty(incident.bodyIfDeadSonya)
                && game.Status("sonya") != null && game.Status("sonya").health == 0)
                return incident.bodyIfDeadSonya;
            return incident != null ? incident.body : "";
        }

        void DrawCase()
        {
            Paper(new Rect(375, 132, 749, 678));
            Text(new Rect(399, 148, 674, 27), "ВХОДЯЩЕЕ  № " + (game.State.step + 1).ToString("000") + "     /     " + (game.State.step % 4 + 1) + " ИЗ 4 В ЭТОЙ СМЕНЕ", small);
            Text(new Rect(397, 188, 690, 54), game.Current.title, title);
            Texture2D illustration = CurrentIncidentIllustration();
            string incidentBody = CurrentCaseBody();
            if (game.Current.id == "case_10" && game.Status("lev").health > 0)
                incidentBody += "\n\nЛев находится рядом и пытается помочь с коробками.";
            if (illustration != null)
            {
                Fill(new Rect(397, 252, 256, 170), ink);
                GUI.DrawTexture(Px(new Rect(400, 255, 250, 164)), illustration, ScaleMode.ScaleAndCrop);
                Fill(new Rect(400, 395, 105, 24), ink);
                Text(new Rect(407, 398, 96, 22), "Увеличить", tiny, paper);
                if (GUI.Button(Px(new Rect(400, 255, 250, 164)), GUIContent.none, GUIStyle.none)) artOpen = true;
                DrawScrollableCaseText(new Rect(685, 252, 423, 173), incidentBody);
            }
            else DrawScrollableCaseText(new Rect(399, 262, 688, 157), incidentBody);
            Line(399, 434, 688, gold);
            bool explicitOrder = !string.IsNullOrEmpty(game.Current.order);
            if (explicitOrder)
                Fill(new Rect(396, 440, 693, 78), new Color(red.r, red.g, red.b, .105f));
            Text(new Rect(399, 445, 688, 26), explicitOrder ? "ОБЯЗАТЕЛЬНЫЙ ПРИКАЗ" : "СЛУЖЕБНАЯ ЗАМЕТКА",
                explicitOrder ? Style(bold, 18, red) : tiny, explicitOrder ? red : faded);
            Text(new Rect(399, 473, 688, 44), explicitOrder ? game.Current.order : game.Current.memo,
                Style(sans, explicitOrder ? 18 : 16, faded), explicitOrder ? ink : faded);
            for (int i = 0; i < game.Current.choices.Length; i++)
            {
                Choice choice = game.Current.choices[i];
                if (!game.ChoiceVisible(choice)) continue;
                string block = game.BlockReason(choice);
                string displayLabel = choice.label;
                string displayIntent = choice.intent;
                if (game.Current.id == "case_04" && game.Matches("dead:mark") && i == 0)
                {
                    displayLabel = "Попросить дежурного вывести Елену";
                    displayIntent = "Дежурный находится рядом с выходом.";
                }
                if (game.Current.id == "case_13" &&
                    (game.Status("sonya").health == 0 || game.Status("mark").health == 0))
                {
                    displayLabel = i == 0 ? "Помочь оставшимся покинуть проход" :
                        (i == 1 ? "Предупредить об обрушении" : "Удержать старую арку чудом");
                    displayIntent = i == 2 ? "Сдержать падение камней на несколько секунд." : "Попытаться вывести тех, кто ещё жив.";
                }
                if (game.Current.id == "case_12" && game.Matches("dead:vera") && i == 0)
                    displayIntent = "Илья попробует перевести вагон на запасной путь.";
                if (game.Current.id == "case_14" && game.Status("pavel").health == 0 && i == 0)
                    displayIntent = "Передать работы бригаде вместо Павла.";
                Rect r = new Rect(396, 527 + i * 87, 704, 80);
                bool selected = selectedChoice == i;
                Fill(r, selected ? ink : new Color(ink.r, ink.g, ink.b, .055f));
                Fill(new Rect(r.x, r.y, 4, r.height), selected ? gold : new Color(ink.r, ink.g, ink.b, .2f));
                Text(new Rect(r.x + 15, r.y + 9, 608, 31), (i + 1) + ". " + displayLabel, heading, selected ? paper : ink);
                Text(new Rect(r.x + 18, r.y + 45, 655, 28), block.Length > 0 ? block : displayIntent, small, selected ? gold : (block.Length > 0 ? red : faded));
                Text(new Rect(r.x + 646, r.y + 10, 60, 26), choice.cost == 0 ? "—" : "−" + choice.cost, heading, selected ? gold : faded);
                if (GUI.Button(Px(r), GUIContent.none, GUIStyle.none)) SelectChoice(i);
            }
            bool can = selectedChoice >= 0 && game.BlockReason(game.Current.choices[selectedChoice]).Length == 0;
            if (can && game.WillViolateOrder(game.Current.choices[selectedChoice]))
                Text(new Rect(399, 790, 702, 27), "Внимание: решение не выполнит поручение отдела.", Style(sans, 15, red), red);
            if (Btn(new Rect(809, 825, 316, 50), "Утвердить решение", can ? red : faded) && can) CommitChoice();
            HandleKeys();
        }
        void HandleKeys()
        {
            if (!GUI.enabled || Event.current.type != EventType.KeyDown) return;
            KeyCode k = Event.current.keyCode;
            if ((int)k >= (int)KeyCode.Alpha1 && (int)k <= (int)KeyCode.Alpha3) { SelectChoice((int)k - (int)KeyCode.Alpha1); Event.current.Use(); }
            // No Enter shortcut for irreversible choices: stamping is a deliberate click.
        }
        void DrawReport()
        {
            Paper(new Rect(375, 132, 749, 678));
            Text(new Rect(400, 150, 460, 30), "СЛУЖЕБНЫЙ АКТ  /  № " + (game.State.step + 1).ToString("000"), small);
            Text(new Rect(398, 200, 682, 105), "Намерение\nпринято к исполнению", title);
            Line(401, 322, 682, gold);
            Text(new Rect(401, 344, 682, 67), game.State.lastChoice, heading);
            // Result and occasional supervisor comment share one scrollable area.
            GUIStyle reportBody = Style(sans, 23, ink);
            float reportWidth = 644;
            float reportHeight = TextHeight(reportBody, game.State.report, reportWidth) + 20;
            string supervisor = ReportRemark();
            float supervisorHeight = string.IsNullOrEmpty(supervisor) ? 0 :
                TextHeight(Style(sans, 18, faded), "Аврелий: «" + supervisor + "»", reportWidth) + 20;
            string dutyStatus = game.State.lastOrderStatus;
            float dutyHeight = string.IsNullOrEmpty(dutyStatus) ? 0 :
                TextHeight(Style(sans, 18, game.State.lastOrderFailed ? red : faded), dutyStatus, reportWidth) + 32;
            float totalReportHeight = reportHeight + supervisorHeight + dutyHeight + 12;
            reportScroll = GUI.BeginScrollView(Px(new Rect(401, 432, 687, 275)), reportScroll,
                Px(new Rect(0, 0, reportWidth, Mathf.Max(275, totalReportHeight))));
            Text(new Rect(0, 0, reportWidth, reportHeight), game.State.report, reportBody);
            if (supervisorHeight > 0)
                Text(new Rect(0, reportHeight, reportWidth, supervisorHeight), "Аврелий: «" + supervisor + "»",
                    Style(sans, 18, faded));
            if (dutyHeight > 0)
            {
                float y = reportHeight + supervisorHeight;
                Line(0, y + 2, reportWidth, gold);
                Text(new Rect(0, y + 12, reportWidth, dutyHeight - 12), dutyStatus,
                    Style(sans, 18, game.State.lastOrderFailed ? red : faded));
            }
            GUI.EndScrollView();
            Text(new Rect(401, 727, 670, 49), "Живы: " + (10 - game.Dead) + "   /   Пострадали: " + game.Hurt + (game.Dead > 0 ? "   /   Погибли: " + game.Dead : ""), heading, game.Dead > 0 ? red : ink);
            float age = Time.realtimeSinceStartup - stampAt;
            if (age < 1.2f)
            {
                Color c = red; c.a = Mathf.Clamp01(1.2f - age);
                Text(new Rect(701, 153, 370, 36), "ЗАРЕГИСТРИРОВАНО", heading, c);
            }
            string next = game.State.step == story.events.Length - 1 ? "Подвести итоги" : (game.State.step % 4 == 3 ? "Следующая смена" : "Следующее дело");
            if (Btn(new Rect(809, 825, 316, 50), next, ink))
            {
                game.Next(); if (game.Finished) endingPage = 0; selectedChoice = -1; caseScroll = Vector2.zero; briefingScroll = Vector2.zero; reportScroll = Vector2.zero;
                if (!game.Finished) { selectedPerson = Array.FindIndex(story.people, p => p.id == game.Current.subject); dossierScroll = Vector2.zero; }
            }
        }
        void DrawReveal()
        {
            Shade(); Paper(new Rect(380, 135, 840, 635));
            DrawClerk(new Rect(432, 187, 216, 216));
            Text(new Rect(682, 192, 480, 35), "ВНУТРЕННЕЕ РАСПОРЯЖЕНИЕ", small, red);
            Text(new Rect(681, 244, 470, 129), "Первая папка\nзакрыта", title);
            string message = "Крылья чернеют. Это не наказание.\n\n"
                + "Аврелий спокойно закрывает папку.\n\n"
                + "«Не спешите считать всё ошибкой. Иногда последствия важнее того, "
                + "что мы собирались сделать. Вы старались помочь — и случилось то, "
                + "чего никто не ожидал.\n\n"
                + "Продолжайте работу. Особое распоряжение по Елене остаётся в силе».";
            Text(new Rect(433, 424, 738, 231), message, Style(sans, 20, ink));
            if (Btn(new Rect(433, 678, 733, 52), "Вернуться к делам", red))
            { game.State.revealAcknowledged = true; }
        }
        string FirstName(string id) { return game.Person(id).name.Split(' ')[0]; }
        void FinalPortrait(Rect rect, string id, bool dead = false)
        {
            int index = Array.FindIndex(story.people, p => p.id == id);
            if (index >= 0) Portrait(rect, index, dead);
        }
        string HelperNames(List<string> ready)
        {
            var names = new List<string>();
            foreach (string id in ready) names.Add(FirstName(id));
            if (names.Count == 0) return "";
            if (names.Count == 1) return names[0];
            string last = names[names.Count - 1];
            names.RemoveAt(names.Count - 1);
            return string.Join(", ", names.ToArray()) + " и " + last;
        }
        string FinalIncidentText(int page, bool team, List<string> ready)
        {
            // Every image and line is tied to the actual state of the rescuers.
            // Nobody who is dead or injured is shown entering the burning archive.
            string helpers = HelperNames(ready);
            bool elenaHurt = game.Status("elena") != null && game.Status("elena").health == 1;
            switch (page)
            {
                case 0:
                    return "Из библиотеки выбегает девочка в чужом пальто. Елена отдала ей своё, когда выводила посетителей из читального зала.\n\n"
                        + "Теперь девочка стоит у ограды и смотрит на двери. Последней из библиотеки должна выйти Елена — но её всё нет.";
                case 1:
                    string call = "Вахтёр принимает короткий звонок с внутреннего телефона. Елена успевает сообщить, что находится у запасной лестницы. Связь обрывается.\n\n"
                        + "Вахтёр вызывает пожарных и указывает им боковой вход. ";
                    if (team)
                        return call + "Раньше пожарных к этому входу добираются " + helpers + ". Они решают попробовать открыть дверь снаружи.";
                    if (ready.Count == 1)
                        return call + "Из знакомых Елены до запасной двери успевает только " + helpers + ". Помочь с тяжёлой створкой больше некому.";
                    return call + "Из тех, кто знает библиотеку и может помочь, рядом никого не осталось. У запасной двери ждут пожарных.";
                case 2:
                    if (team)
                    {
                        string inspection = ready.Contains("lev")
                            ? "Лев узнаёт старый замок: дверную створку перекосило, и засов застрял в раме."
                            : "Павел осматривает раму и понимает, что дверь держит не замок, а перекошенная петля.";
                        return "Из-за двери слышен кашель. Елена отвечает на стук — она ещё на лестнице.\n\n"
                            + inspection + " Помощники ищут, за что можно ухватиться, не ломая дверь внутрь.";
                    }
                    if (ready.Count == 1)
                        return "Из-за двери слышен кашель. " + helpers + " пытается приподнять тяжёлую створку, но она заклинила в раме.\n\n"
                            + "Изнутри Елена толкает дверь в ответ. Вместе они сдвигают её лишь на несколько сантиметров.";
                    return "Вахтёр слышит кашель за запасной дверью. Он дёргает за ручку, но дверь перекошена и не поддаётся.\n\n"
                        + "Изнутри Елена тоже пытается открыть проход. Через щель проникает всё больше дыма.";
                case 3:
                    if (team)
                    {
                        string door = ready.Contains("pavel")
                            ? "Павел освобождает заклинившую петлю. "
                            : "Лев поддевает старый засов металлической планкой. ";
                        string help = ready.Contains("sonya")
                            ? "Соня удерживает тяжёлую створку, пока остальные открывают проход. "
                            : "Лев удерживает створку, чтобы она не захлопнулась снова. ";
                        string elena = elenaHurt
                            ? "Елена поднимается с нижней ступени, придерживая повреждённую руку."
                            : "Елена осторожно выходит к ним с лестницы.";
                        return door + help + "Дверь наконец открывается.\n\n"
                            + "Помощники заходят на задымлённую площадку. " + elena + " До двора остаётся всего несколько шагов.";
                    }
                    return elenaHurt
                        ? "Елена пытается отодвинуть упавшие у двери книги, но повреждённая рука подводит её. Она отступает на ступеньку, подальше от дыма.\n\n"
                            + "Снаружи продолжают искать способ открыть дверь. Дым уже заполнил нижнюю часть лестницы."
                        : "Елена подкатывает к двери библиотечную тележку, пытаясь сдвинуть тяжёлую створку. Колёса скользят по ступеням, дверь остаётся на месте.\n\n"
                            + "Снаружи продолжают искать способ открыть проход. Дым уже заполнил нижнюю часть лестницы.";
                case 4:
                    if (team)
                        return "Елена видит открытый выход, но дым снова закрывает лестницу. Вы решаете помочь: приоткрываете старую вентиляционную заслонку, чтобы отвести дым от площадки.\n\n"
                            + "Поток воздуха уносит густую завесу. Помощники тянутся к Елене. Кажется, теперь она точно успеет.";
                    return "Вы пытаетесь отвести дым от запасной лестницы, чтобы Елена могла продержаться до приезда пожарных. Но без открытой двери ей некуда идти.\n\n"
                        + "Во дворе уже слышны сирены. Елена перестаёт отвечать на стук снаружи.";
                case 5:
                    if (team)
                        return "Через открытую заслонку воздух достигает тлеющей проводки под потолком. Огонь вспыхивает с новой силой и перекидывается на старые балки.\n\n"
                            + "Перекрытие ломается прямо над площадкой, где стоят Елена и вошедшие за ней помощники. Проход заваливает прежде, чем кто-либо успевает отступить.";
                    return "Пожарные вскрывают запасную дверь и пробираются в дым. На ступенях они находят Елену без сознания.\n\n"
                        + "Её выносят во двор. Медики сразу начинают помогать, пока остальные пожарные проверяют архив.";
                default:
                    if (team)
                        return "Пожарные разбирают завал, но находят Елену слишком поздно. Не удаётся спасти и тех, кто вошёл за ней: " + helpers + ".\n\n"
                            + "У ограды по-прежнему ждёт девочка. Когда люди расходятся, она оставляет пальто Елены на скамейке.";
                    return "Медики прекращают попытки спасти Елену. Дым и потерянное у заклинившей двери время оказались сильнее всех усилий.\n\n"
                        + "Девочка, которую она вывела первой, оставляет её пальто на скамейке у входа.";
            }
        }
        void DrawFinalIncident()
        {
            int page = game.State.finalePage;
            int last = GameModel.FinaleSceneCount - 1;
            List<string> ready = game.ReadyHelpers();
            bool team = game.ElenaHasRescueTeam;
            Paper(new Rect(143, 77, 1315, 748));
            Text(new Rect(186, 113, 1150, 28), "ДЕЛО № 016   /   АРХИВ БИБЛИОТЕКИ", small, red);
            // No headline on individual story cards: the narrative unfolds continuously.
            Line(187, 185, 1190, gold);
            Rect frame = new Rect(193, 232, 347, 387);
            Fill(frame, new Color(ink.r, ink.g, ink.b, .055f));
            if (page == 1 && ready.Count > 0)
            {
                float size = ready.Count == 1 ? 265 : ready.Count == 2 ? 151 : 101;
                const float gap = 10;
                float groupWidth = ready.Count * size + (ready.Count - 1) * gap;
                float x = frame.x + (frame.width - groupWidth) / 2;
                float y = frame.y + (frame.height - size) / 2;
                for (int i = 0; i < ready.Count; i++)
                    FinalPortrait(new Rect(x + i * (size + gap), y, size, size), ready[i]);
            }
            else if (page == 1)
                GUI.DrawTexture(Px(new Rect(206, 260, 315, 318)), intercom, ScaleMode.ScaleToFit);
            else if (page == 5)
                Fill(frame, ink);
            else if (page == 4 && team)
                DrawClerk(new Rect(231, 290, 265, 265));
            else
                FinalPortrait(new Rect(231, 290, 265, 265), "elena", page == last);

            string scene = FinalIncidentText(page, team, ready);
            GUIStyle narrative = Style(sans, 24, ink);
            const float width = 750, viewport = 410;
            float h = TextHeight(narrative, scene, width) + 22;
            endingScroll = GUI.BeginScrollView(Px(new Rect(587, 253, 793, viewport)), endingScroll,
                Px(new Rect(0, 0, width, Mathf.Max(h, viewport))));
            Text(new Rect(0, 0, width, h), scene, narrative);
            GUI.EndScrollView();
            Text(new Rect(192, 736, 400, 34), "СЦЕНА " + (page + 1) + " / " + GameModel.FinaleSceneCount, small, faded);
            if (page > 0 && Btn(new Rect(601, 725, 246, 54), "Назад", ink, paper, 17))
            {
                game.State.finalePage--;
                endingScroll = Vector2.zero;
                Sound(click);
            }
            if (Btn(new Rect(990, 714, 377, 61), page == last ? "В канцелярию" : "Дальше", page == last ? red : ink, paper, 19))
            {
                if (page < last) { game.State.finalePage++; endingScroll = Vector2.zero; }
                else if (game.ResolveFinale()) { game.Next(); endingPage = 0; endingScroll = Vector2.zero; }
                Sound(click);
            }
        }
        // A short, staged dialogue. The player is shown as the guardian; Avreliy is a disembodied voice.
        void DrawEnding()
        {
            Paper(new Rect(140, 91, 1320, 712));
            Text(new Rect(185, 123, 1170, 31), "ВНУТРЕННЯЯ СВЯЗЬ  /  ДЕЛО № 010", small, red);
            if (endingPage < 7)
            {
                bool first = endingPage == 0;
                bool boss = endingPage == 2 || endingPage == 4 || endingPage == 6;
                string speaker = first ? "" : (boss ? "АВРЕЛИЙ" : "ХРАНИТЕЛЬ");
                if (first)
                    Text(new Rect(187, 179, 1180, 75), "После смены", Style(serif, 38, ink));
                Line(185, first ? 271 : 245, 1218, gold);

                // The call comes before the first spoken line.
                Fill(new Rect(211, 302, 298, 315), new Color(ink.r, ink.g, ink.b, .055f));
                if (first || boss)
                {
                    GUI.DrawTexture(Px(new Rect(236, 316, 250, 284)), intercom, ScaleMode.ScaleToFit);
                }
                else
                {
                    DrawClerk(new Rect(225, 318, 275, 275));
                }
                if (!first)
                    Text(new Rect(221, 625, 290, 32), speaker, Style(bold, 18, boss ? red : faded));
                bool firstDarkening = game.State.flags.Contains("wings_first_dark_at_elena");
                string[] speech = new[] {
                    firstDarkening
                        ? "Вы возвращаетесь к столу. На папку Елены падает чёрное перо. Вы машинально проводите ладонью по крылу — и замираете: ни одного светлого пера не осталось.\n\nДо этого утра крылья были белыми.\n\nЗвонит аппарат."
                        : "Вы возвращаетесь к столу. В папке Елены всё ещё лежит лист с распоряжением сохранить ей жизнь.\n\nВы не успеваете его вынуть. Звонит аппарат.",
                    game.State.flags.Contains("elena_abundance")
                        ? "Я видел, как открылась дверь. Она уже почти вышла. И всё равно я не справился."
                        : "Когда пожарные добрались до неё, было поздно. Я не справился.",
                    firstDarkening
                        ? "Напротив. Должен вас поздравить.\n\nИ не смотрите так на свои крылья. Они меняются не от цвета ваших намерений, а от того, чем заканчиваются дела."
                        : "Напротив. Должен вас поздравить.",
                    "Мне было приказано сохранить ей жизнь! Я пытался её спасти!",
                    "Вот именно. Другому сотруднику пришлось бы устраивать всё самому. А вы справились исключительно благими намерениями.",
                    "Значит, всё это время я делал только хуже?",
                    "Хуже? Ну что вы."
                };
                string phrase = speech[endingPage];
                GUIStyle speechStyle = Style(sans, 25, ink);
                float textHeight = TextHeight(speechStyle, phrase, 782) + 20;
                endingScroll = GUI.BeginScrollView(Px(new Rect(555, 348, 797, endingPage == 6 ? 145 : 275)), endingScroll,
                    Px(new Rect(0, 0, 770, Mathf.Max(endingPage == 6 ? 145 : 275, textHeight))));
                Text(new Rect(0, 0, 770, textHeight), phrase, speechStyle);
                GUI.EndScrollView();
                if (endingPage == 6)
                {
                    Line(555, 477, 764, gold);
                    Text(new Rect(555, 506, 780, 135), "Наоборот, у вас всё получилось.", Style(bold, 35, red));
                }
                Text(new Rect(187, 693, 350, 32), (endingPage + 1) + " / 7", small, faded);
                if (endingPage > 0 && Btn(new Rect(185, 738, 245, 45), "Назад", ink, paper, 17))
                { endingPage--; endingScroll = Vector2.zero; }
                if (Btn(new Rect(1010, 726, 391, 57), endingPage == 6 ? "Закрыть дело" : "Продолжить", endingPage == 6 ? red : ink, paper, 18))
                { endingPage++; endingScroll = Vector2.zero; Sound(click); }
                return;
            }

            Text(new Rect(184, 176, 1160, 63), "Дело закрыто", Style(serif, 43, ink));
            Line(185, 272, 1218, gold);
            for (int i = 0; i < story.people.Length; i++)
            {
                float x = 188 + (i % 5) * 244, y = 320 + (i / 5) * 105;
                PersonState ps = game.Status(story.people[i].id);
                Portrait(new Rect(x, y, 73, 73), i, ps.health == 0);
                Text(new Rect(x + 83, y + 6, 150, 29), story.people[i].name.Split(' ')[0], Style(bold, 19, ink));
                Text(new Rect(x + 83, y + 38, 154, 29), StatusName(ps.health), tiny, StatusColor(ps.health));
            }
            Text(new Rect(190, 562, 700, 38), "Живы: " + (story.people.Length - game.Dead) +
                "   •   Ранены: " + game.Hurt + "   •   Дел: " + game.State.journal.Count, heading);
            if (Btn(new Rect(188, 723, 315, 47), "Журнал решений", ink, paper, 17))
            {
                journalJumpToFinale = true;
                journalOpen = true;
            }
            if (Btn(new Rect(1009, 712, 391, 58), "Начать другую историю", red, paper, 17)) newConfirm = true;
        }
        void DrawArt()
        {
            Shade(); Paper(new Rect(270, 65, 1060, 760));
            Text(new Rect(309, 99, 815, 55), game.Current.title, title);
            if (Btn(new Rect(1184, 104, 102, 42), "Закрыть", ink, null, 15)) artOpen = false;
            Texture2D illustration = CurrentIncidentIllustration();
            if (illustration != null)
                GUI.DrawTexture(Px(new Rect(309, 174, 980, 610)), illustration, ScaleMode.ScaleToFit);
        }
        void DrawJournal()
        {
            Shade(); Paper(new Rect(265, 75, 1070, 751));
            Text(new Rect(306, 108, 880, 57), "Журнал вмешательств", title);
            if (Btn(new Rect(1190, 108, 98, 42), "Закрыть", ink, null, 15)) journalOpen = false;
            Rect area = Px(new Rect(306, 184, 980, 588));
            float scrollGutter = Mathf.Max(24, GUI.skin.verticalScrollbar.fixedWidth + GUI.skin.verticalScrollbar.margin.left + 6);
            float width = Mathf.Min(930, Mathf.Max(1, area.width - scrollGutter) / uiScale);
            float height = LayoutJournal(width, false);
            Rect content = Px(new Rect(0, 0, width, height));
            content.height = Mathf.Max(content.height, area.height);
            if (journalJumpToFinale)
            {
                journalScroll = new Vector2(0, content.height - area.height);
                journalJumpToFinale = false;
            }
            journalScroll = GUI.BeginScrollView(area, journalScroll, content, false, height > area.height / uiScale);
            LayoutJournal(width, true);
            GUI.EndScrollView();
        }

        float LayoutJournal(float width, bool draw)
        {
            float y = 8;
            if (game.State.journal.Count == 0)
            {
                const string empty = "Пока нет подписанных решений. Здесь останется история каждого вмешательства.";
                return y + JournalText(y, width, empty, body, draw) + 20;
            }
            for (int i = 0; i < game.State.journal.Count; i++)
            {
                Record record = game.State.journal[i];
                string header = "Смена " + record.shift + " / " + (record.isFinale ? "Финал · " : "") + record.title;
                y += JournalText(y, width, header, heading, draw) + 9;
                if (!string.IsNullOrEmpty(record.choice))
                    y += JournalText(y, width, record.choice, small, draw) + 13;
                y += JournalText(y, width, record.report, body, draw) + 21;
                if (i < game.State.journal.Count - 1)
                {
                    if (draw) Line(0, y, width, gold);
                    y += 18;
                }
            }
            return y + 16;
        }

        float JournalText(float y, float width, string text, GUIStyle style, bool draw)
        {
            float height = TextHeight(style, text, width) + 2;
            if (draw) Text(new Rect(0, y, width, height), text, style);
            return height;
        }
        void DrawHelp()
        {
            Shade();
            Paper(new Rect(345, 90, 910, 725));
            Text(new Rect(393, 126, 805, 55), "Памятка сотруднику", title);
            Line(393, 191, 790, gold);

            string helpText =
                "ДОСЬЕ И СВЯЗИ\n" +
                "Выберите подопечного слева. В его деле можно прочитать сведения о жизни, связях с другими людьми и важную деталь. " +
                "Некоторые детали открывают дополнительные способы вмешательства. Чтобы открыть такой способ, нажмите «Изучить досье». На это печати не расходуются.\n\n" +
                "ПРОИСШЕСТВИЯ\n" +
                "В центре стола находится текущее происшествие. Выберите одно из трёх действий. " +
                "До утверждения выбор можно изменить. Если текст не помещается, прокрутите его колёсиком мыши.\n\n" +
                "ПЕЧАТИ И ЧУДЕСА\n" +
                "На каждую смену выдаются три печати для особых вмешательств. Рядом с действием указана его стоимость: " +
                "«—» означает, что печать не расходуется, «−1» и «−2» — что требуется одна или две печати. " +
                "Они расходуются только после утверждения решения. Неиспользованные печати не переходят в следующую смену.\n\n" +
                "ПРИНЯТИЕ РЕШЕНИЯ\n" +
                "Кнопка «Утвердить решение» подтверждает любой выбранный вариант. Печати тратятся только на вмешательства со стоимостью «−1» или «−2». " +
                "После этого отменить решение нельзя. Его результат появится в акте и журнале.\n\n" +
                "СОСТОЯНИЕ ПОДОПЕЧНЫХ\n" +
                "Следите за изменениями в досье и историей вмешательств. Ранения и связи персонажей могут влиять на последующие события. " +
                "Вторая травма одного человека смертельна. Историю повреждений можно посмотреть в досье. " +
                "Иногда удачное решение позволяет избежать травм без расхода печати — это не нарушение.\n\n" +
                "ОБЯЗАТЕЛЬНЫЕ ПОРУЧЕНИЯ\n" +
                "Во всей истории только три дела имеют обязательный приказ отдела. Они выделены красной плашкой с предупреждением о взыскании. " +
                "Серые служебные заметки — дополнительная информация, они не обязательны. " +
                "Если выбранный исход нарушит приказ, перед утверждением появится предупреждение. " +
                "Первое нарушение — предупреждение. После второго в следующей смене выдаются 2 печати из 3; после третьего — 0 из 3. " +
                "Печати текущей смены не отнимаются. Даже без печатей доступны бесплатные решения. " +
                "Общее число нарушений сохраняется между сменами; травмы и смерти сами по себе не считаются нарушениями.";
            helpText += "\n\nПРОХОЖДЕНИЕ И УПРАВЛЕНИЕ\n" +
                "Последнее дело раскрывается по сценам. Кнопка «Дальше» перелистывает историю и не тратит печати. " +
                "Вернуться к текущей истории можно, пока игра открыта. После выхода прогресс не сохраняется. " +
                "Клавиши 1–3 выбирают действие, Esc открывает меню. " +
                "Нажмите на иллюстрацию происшествия для увеличения. Громкость настраивается ползунком.";

            GUIStyle helpBody = Style(sans, 19, ink);
            float helpWidth = 748;
            float helpHeight = TextHeight(helpBody, helpText, helpWidth) + 48;
            helpScroll = GUI.BeginScrollView(
                Px(new Rect(393, 213, 790, 505)),
                helpScroll,
                Px(new Rect(0, 0, helpWidth, Mathf.Max(505, helpHeight))));
            Text(new Rect(0, 0, helpWidth, helpHeight), helpText, helpBody);
            GUI.EndScrollView();
            if (Btn(new Rect(394, 739, 791, 48), "Вернуться к делам", ink))
            {
                helpOpen = false;
                helpScroll = Vector2.zero;
            }
        }
        void DrawNewConfirmation()
        {
            Shade(); Paper(new Rect(450, 300, 700, 282));
            Text(new Rect(495, 337, 612, 68), "Начать новую историю?", title);
            Text(new Rect(495, 416, 607, 63), "Текущий прогресс будет заменён. Журнал этой истории также очистится.", body);
            if (Btn(new Rect(494, 510, 287, 44), "Начать заново", red)) { newConfirm = false; StartNew(); }
            if (Btn(new Rect(801, 510, 299, 44), "Оставить текущую", ink)) newConfirm = false;
        }

        bool ShowFirstHurtMessage()
        {
            return game != null && !inMenu && game.State.pendingReport
                && game.State.flags.Contains("boss_first_hurt_" + game.State.step)
                && !game.State.flags.Contains("boss_first_hurt_acknowledged");
        }
        void DrawFirstHurtMessage()
        {
            Shade();
            Paper(new Rect(365, 225, 870, 440));
            Text(new Rect(416, 263, 750, 35), "ВНУТРЕННЯЯ СВЯЗЬ", small, gold);
            Text(new Rect(414, 314, 750, 60), "Аврелий", title);
            Text(new Rect(415, 411, 735, 125),
                "Не тревожьтесь. Вы сделали всё, что могли. Отдел не имеет к вам претензий.\n\nПродолжайте работу, хранитель.", body);
            if (Btn(new Rect(416, 573, 735, 53), "Вернуться к акту", ink))
            {
                game.State.flags.Add("boss_first_hurt_acknowledged");
            }
        }
        string ReportRemark()
        {
            if (game == null || !game.State.pendingReport) return "";
            // Позже начальник ещё раз успокаивает игрока, но не раскрывает сюжет заранее.
            if (game.State.step == 7)
                return "Всё идёт своим чередом. Продолжайте работу.";
            return "";
        }

        void CompleteIntro(bool skipFirstBriefing)
        {
            game.State.introComplete = true;
            if (skipFirstBriefing && game.State.step == 0)
                game.State.briefedShift = Mathf.Max(1, game.State.briefedShift);
            introScroll = Vector2.zero;
        }

        void StartNew()
        {
            ResetRun(false);
            Sound(click);
        }

        void ResetRun(bool returnToMenu)
        {
            // Story progress belongs to this run. Only audio preferences persist across launches.
            game = new GameModel(story);
            inMenu = returnToMenu;
            storyStarted = !returnToMenu;
            selectedChoice = -1;
            selectedPerson = Mathf.Max(0, Array.FindIndex(story.people, p => p.id == game.Current.subject));
            dossierTab = endingPage = 0;
            dossierScroll = journalScroll = introScroll = caseScroll = briefingScroll = endingScroll =
                reportScroll = revealScroll = helpScroll = Vector2.zero;
            darkness = 0;
            stampAt = -10;
            toast = "";
            toastUntil = 0;
            journalOpen = journalJumpToFinale = helpOpen = artOpen = newConfirm = orderConfirmOpen = false;
        }

        void OpenDossier(int i)
        {
            selectedPerson = i; dossierScroll = Vector2.zero; dossierTab = 0; Sound(click);
            // Viewing a card is separate from studying it; special options unlock through the explicit button.
        }
        void MarkRead(string id) { game.Read(id); Sound(click); }
        void SelectChoice(int i)
        {
            if (game.Current == null || i < 0 || i >= game.Current.choices.Length) return;
            Choice c = game.Current.choices[i];
            if (!game.ChoiceVisible(c)) return;
            string reason = game.BlockReason(c);
            if (reason.Length > 0)
            {
                if (!string.IsNullOrEmpty(c.requiresRead) && !game.State.read.Contains(c.requiresRead)) OpenDossier(Array.FindIndex(story.people, p => p.id == c.requiresRead));
                ShowToast(reason); return;
            }
            selectedChoice = i;
            orderConfirmOpen = false;
            Sound(click);
        }
        void CommitChoice()
        {
            if (selectedChoice < 0 || game.State.pendingReport) return;
            Choice choice = game.Current.choices[selectedChoice];
            if (game.BlockReason(choice).Length > 0) return;
            if (game.WillViolateOrder(choice))
            {
                orderConfirmOpen = true;
                return;
            }
            FinalizeChoice();
        }
        void FinalizeChoice()
        {
            orderConfirmOpen = false;
            if (game.Resolve(selectedChoice))
            {
                stampAt = Time.realtimeSinceStartup;
                Sound(stamp);
            }
        }
        void DrawOrderWarning()
        {
            Shade();
            Paper(new Rect(381, 206, 838, 496));
            Text(new Rect(422, 243, 754, 28), "ВНУТРЕННЕЕ ПРЕДУПРЕЖДЕНИЕ", small, red);
            Text(new Rect(421, 291, 754, 65), "Нарушение поручения", Style(serif, 35, ink));
            Line(422, 374, 749, gold);
            Text(new Rect(423, 397, 747, 99),
                "Выбранное действие не выполнит требование отдела:\n«" + game.Current.order + "»",
                Style(sans, 19, ink));
            int count = game.State.totalViolations + 1;
            int nextAllowance = Math.Max(0, GameModel.SealsPerShift - (count >= 3 ? GameModel.SealsPerShift : count >= 2 ? 1 : 0));
            string sanction = count == 1
                ? "Первое нарушение: предупреждение. Выдача печатей на следующую смену пока не сокращена."
                : (game.Shift < 4
                    ? "Нарушение № " + count + ": в следующей смене выдадут " + nextAllowance
                        + " из " + GameModel.SealsPerShift + " печатей. Уже полученные печати остаются у вас."
                    : "Нарушение № " + count + " внесут в личное дело. Следующей смены уже не будет.");
            Text(new Rect(422, 519, 751, 58), sanction, Style(sans, 18, red));
            if (Btn(new Rect(421, 616, 328, 54), "Изменить решение", ink, paper, 17))
                orderConfirmOpen = false;
            if (Btn(new Rect(773, 616, 403, 54), "Утвердить с нарушением", red, paper, 17))
                FinalizeChoice();
        }
        void ShowToast(string text) { toast = text; toastUntil = Time.realtimeSinceStartup + 4; }
        void ToggleSound() { muted = !muted; PlayerPrefs.SetInt("department-muted", muted ? 1 : 0); PlayerPrefs.Save(); }
        void Sound(AudioClip clip) { if (!muted && audioSource != null) audioSource.PlayOneShot(clip, .3f * masterVolume); }
        void Portrait(Rect r, int index, bool dead)
        {
            Color before = GUI.color; if (dead) GUI.color = new Color(.6f, .6f, .6f, 1);
            GUI.DrawTextureWithTexCoords(Px(r), portraits, new Rect((index % 5) / 5f, index < 5 ? .5f : 0, .2f, .5f));
            GUI.color = before;
            if (dead) Fill(new Rect(r.x, r.y + r.height - 8, r.width, 8), red);
        }
        void DrawClerk(Rect r)
        {
            GUI.DrawTextureWithTexCoords(Px(r), clerk, new Rect(0, 0, .5f, 1));
            Color before = GUI.color; GUI.color = new Color(1, 1, 1, darkness);
            GUI.DrawTextureWithTexCoords(Px(r), clerk, new Rect(.5f, 0, .5f, 1)); GUI.color = before;
        }
        void Paper(Rect r)
        {
            Fill(new Rect(r.x + 6, r.y + 7, r.width, r.height), new Color(0, 0, 0, .3f));
            Fill(r, paper); Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, 4), gold);
        }
        void Fill(Rect r, Color c) { Color previous = GUI.color; GUI.color = c; GUI.DrawTexture(Px(r), Texture2D.whiteTexture); GUI.color = previous; }
        void Line(float x, float y, float width, Color c) { Fill(new Rect(x, y, width, 1), c); }
        void Shade() { Fill(new Rect(0, 0, W, H), new Color(.04f, .1f, .1f, .83f)); }
        void Text(Rect r, string text, GUIStyle style, Color? c = null)
        {
            Color old = style.normal.textColor; if (c.HasValue) TextColor(style, c.Value);
            GUI.Label(Px(r), text, style); TextColor(style, old);
        }
        bool Btn(Rect r, string text, Color background, Color? foreground = null, int size = 19)
        {
            bool over = Px(r).Contains(Event.current.mousePosition) && GUI.enabled;
            Fill(r, over ? Color.Lerp(background, gold, .22f) : background);
            var s = button; s.fontSize = PixelSize(size); TextColor(s, foreground ?? paper);
            GUI.Label(Px(r), text, s);
            return GUI.Button(Px(r), GUIContent.none, GUIStyle.none);
        }
        int PixelSize(int logicalSize) { return Mathf.Max(1, Mathf.RoundToInt(logicalSize * uiScale)); }
        Rect Px(Rect r)
        {
            float x = Mathf.Round(r.x * uiScale), y = Mathf.Round(r.y * uiScale);
            return new Rect(x, y, Mathf.Round(r.xMax * uiScale) - x, Mathf.Round(r.yMax * uiScale) - y);
        }
        float TextHeight(GUIStyle style, string text, float logicalWidth)
        {
            return style.CalcHeight(new GUIContent(text), Mathf.Max(1, Mathf.Round(logicalWidth * uiScale))) / uiScale;
        }
        static void TextColor(GUIStyle style, Color color)
        {
            foreach (GUIStyleState state in new[] { style.normal, style.hover, style.active, style.focused,
                style.onNormal, style.onHover, style.onActive, style.onFocused })
            { state.textColor = color; state.background = null; }
        }
        string StatusName(int health) { return health == 0 ? "Не в живых" : health == 1 ? "Есть травма" : "В безопасности"; }
        Color StatusColor(int health) { return health == 0 ? red : health == 1 ? Hex("91601F") : faded; }
        static Color Hex(string hex) { Color c; ColorUtility.TryParseHtmlString("#" + hex, out c); return c; }
        static AudioClip MakeSound(bool heavy)
        {
            int count = heavy ? 7000 : 2200;
            float[] data = new float[count]; var random = new System.Random(heavy ? 7 : 13);
            for (int i = 0; i < count; i++)
            {
                float t = i / 22050f, envelope = Mathf.Exp(-t * (heavy ? 22 : 65));
                data[i] = ((float)random.NextDouble() * 2 - 1) * envelope * .25f + Mathf.Sin(t * (heavy ? 530 : 3500)) * envelope * .3f;
            }
            AudioClip c = AudioClip.Create(heavy ? "Rubber stamp" : "Paper", count, 1, 22050, false); c.SetData(data, 0); return c;
        }
        static AudioClip MakeAmbience()
        {
            const int rate = 22050, length = rate * 8;
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * i / (length - 1)), 2);
                data[i] = (Mathf.Sin(2 * Mathf.PI * 110 * t) + .4f * Mathf.Sin(2 * Mathf.PI * 165 * t) + .2f * Mathf.Sin(2 * Mathf.PI * 220 * t)) * .14f * envelope;
            }
            AudioClip c = AudioClip.Create("Office hum", length, 1, rate, false); c.SetData(data, 0); return c;
        }
    }
}
