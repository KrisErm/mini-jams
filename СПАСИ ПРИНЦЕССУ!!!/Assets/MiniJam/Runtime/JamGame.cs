using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniJam
{
    public sealed class JamGame : MonoBehaviour
    {
        public enum Phase { Menu, Intro, Playing, Dialogue, Transition, Ending, Video }
        [Serializable] public struct Line
        {
            public string speaker;
            [TextArea] public string text;
            public Line(string who, string words) { speaker = who; text = words; }
        }
        public static JamGame Instance { get; private set; }
        public JamConfig config;
        public bool introScene;
        public JamPlayer Player;
        public JamCamera Rig;
        public Camera Camera;
        public JamSound Sound;
        public JamVideo Video;
        public Transform introCameraStart, introCameraEnd;
        public Transform finalCamera, finalPlayerSpot;
        public Transform finalBars, broom;
        public JamGuard[] guards;
        public Phase State { get; private set; }
        public bool Paused { get; private set; }
        public bool CanPlay => State == Phase.Playing && !Paused && Time.frameCount > resumeFrame;
        public float Fade { get; private set; }
        public bool TwistCard { get; private set; }
        public int Chapter = 1;
        public string ChapterTitle = "Караульный зал";
        public string Objective = "Найди ключ от решётки. Стражу можно вырубить или обойти";
        public string ToastText { get; private set; }
        public float ToastUntil { get; private set; }
        public float PlayTime { get; private set; }
        float nextGuardShout;
        public bool TryGuardShout(string text)
        {
            if (!CanPlay || Time.time < nextGuardShout) return false;
            nextGuardShout = Time.time + 4;
            Toast("СТРАЖНИК: «" + text + "»", 3);
            return true;
        }
        public Line CurrentLine => lines != null && lineIndex < lines.Length ? lines[lineIndex] : new Line("", "");
        public int VisibleCharacters { get; private set; }
        Line[] lines;
        int lineIndex;
        float lineStarted;
        Action dialogueDone;
        bool reveal;
        int resumeFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Time.timeScale = 1; }
        void Awake()
        {
            Instance = this;
            Time.timeScale = 1;
            State = introScene ? Phase.Menu : Phase.Playing;
            SetCursor(!introScene);
        }
        void Start()
        {
            if (introScene && introCameraStart != null)
                Camera.transform.SetPositionAndRotation(introCameraStart.position, introCameraStart.rotation);
            else
            {
                Fade = 1;
                StartCoroutine(FadeTo(0, 0.45f));
            }
        }
        void OnDestroy()
        {
            if (Instance == this) { Instance = null; Time.timeScale = 1; SetCursor(false); }
        }
        void Update()
        {
            if (JamInput.Pause && (State == Phase.Playing || Paused)) TogglePause();
            if (CanPlay) PlayTime += Time.deltaTime;
            if (State != Phase.Dialogue || Paused) return;
            VisibleCharacters = reveal ? CurrentLine.text.Length : Mathf.Min(CurrentLine.text.Length, (int)((Time.unscaledTime - lineStarted) * 40));
            if (Time.unscaledTime - lineStarted > 0.18f && JamInput.Advance) AdvanceDialogue();
        }
        void OnApplicationFocus(bool focus)
        {
            if (!focus && CanPlay) TogglePause();
        }
        public void TogglePause()
        {
            if (State != Phase.Playing && !Paused) return;
            Paused = !Paused; Time.timeScale = Paused ? 0 : 1; SetCursor(!Paused);
        }
        static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
        public void StartAdventure() { if (State == Phase.Menu) StartCoroutine(Intro()); }
        IEnumerator Intro()
        {
            State = Phase.Intro;
            float t = 0;
            while (t < 2.2f)
            {
                t += Time.deltaTime; float u = Mathf.SmoothStep(0, 1, t / 2.2f);
                Camera.transform.position = Vector3.Lerp(introCameraStart.position, introCameraEnd.position, u);
                Camera.transform.rotation = Quaternion.Slerp(introCameraStart.rotation, introCameraEnd.rotation, u);
                yield return null;
            }
            Say(new[] {
                new Line("ГЕРОЙ", "Вот он. Мрачный замок, высокая башня… Всё как в объявлении!"),
                new Line("ГЕРОЙ", config.princessToRescue + ", держись! Я пришёл тебя спасти."),
                new Line("ГЕРОЙ", "Так… У развилки надо было повернуть налево. Или направо? Ладно, замок-то вот он.")
            }, () => StartCoroutine(Load("MJ_Castle")));
        }
        public void SkipIntro() { if (State == Phase.Intro || State == Phase.Dialogue) { StopAllCoroutines(); StartCoroutine(Load("MJ_Castle")); } }
        public void Say(Line[] dialogue, Action onComplete)
        {
            if (dialogue == null || dialogue.Length == 0) { onComplete?.Invoke(); return; }
            lines = dialogue; lineIndex = 0; dialogueDone = onComplete;
            Time.timeScale = 0;
            State = Phase.Dialogue; SetCursor(false); StartLine();
        }
        public void ShowNotice(string title, string text)
        {
            if (!CanPlay) return;
            if (Player != null) Player.StopActions();
            Say(new[] { new Line(title, text) }, ResumePlay);
        }
        void ResumePlay() { resumeFrame = Time.frameCount; State = Phase.Playing; SetCursor(true); }
        void StartLine() { lineStarted = Time.unscaledTime; reveal = false; VisibleCharacters = 0; }
        public void AdvanceDialogue()
        {
            if (State != Phase.Dialogue || Time.unscaledTime - lineStarted < 0.18f) return;
            if (VisibleCharacters < CurrentLine.text.Length) { reveal = true; VisibleCharacters = CurrentLine.text.Length; return; }
            lineIndex++;
            if (lineIndex < lines.Length) { StartLine(); return; }
            Time.timeScale = 1;
            State = Phase.Transition;
            var next = dialogueDone; dialogueDone = null; next?.Invoke();
        }
        public void Toast(string text, float seconds = 4) { ToastText = text; ToastUntil = Time.unscaledTime + seconds; }
        public void Respawn() { if (CanPlay) StartCoroutine(RespawnRoutine()); }
        IEnumerator RespawnRoutine()
        {
            State = Phase.Transition;
            Sound.Play(JamSound.Cue.Respawn);
            yield return FadeTo(1, 0.16f);
            Player.Teleport(Player.Checkpoint);
            if (guards != null) foreach (var guard in guards) if (guard != null) guard.ResetGuard();
            yield return FadeTo(0, 0.22f);
            State = Phase.Playing; SetCursor(true);
            Toast("Ещё попытка. Ты у последней контрольной точки.");
        }
        public void BeginFinale()
        {
            if (!CanPlay) return;
            State = Phase.Transition; SetCursor(false); StartCoroutine(Finale());
        }
        IEnumerator Finale()
        {
            yield return FadeTo(1, 0.25f);
            Player.Teleport(finalPlayerSpot.position, false);
            Player.SetFirstPerson(false);
            Rig.cinematic = true;
            Camera.transform.SetPositionAndRotation(finalCamera.position, finalCamera.rotation);
            yield return FadeTo(0, 0.35f);
            Say(new[] {
                new Line("ГЕРОЙ", "Принцесса " + config.princessToRescue + "! Вы свободны!"),
                new Line("ПРИНЦЕССА", "Меня зовут " + config.actualPrincess + "."),
                new Line("ГЕРОЙ", "Это… ваше имя под прикрытием?"),
                new Line("ПРИНЦЕССА", "Это моё имя. А ты кто такой и почему моя стража спит на полу?"),
                new Line("ГЕРОЙ", "Постойте. Это замок Чёрного Ворона?"),
                new Line("ПРИНЦЕССА", "Белой Цапли. Ворон — за развилкой. Налево."),
                new Line("ГЕРОЙ", "А я повернул направо… Ладно, неловко вышло. Я пойду?"),
                new Line("ПРИНЦЕССА", "Конечно. Сразу после того, как здесь уберёшь.")
            }, () => StartCoroutine(TrapEnding()));
        }
        IEnumerator TrapEnding()
        {
            State = Phase.Transition;
            Sound.Play(JamSound.Cue.Gate);
            Vector3 start = finalBars.localPosition;
            Vector3 end = start + Vector3.down * 5;
            float t = 0;
            while (t < 0.65f)
            {
                t += Time.deltaTime;
                finalBars.localPosition = Vector3.Lerp(start, end, Mathf.Clamp01(t * t / (0.65f * 0.65f)));
                yield return null;
            }
            if (Player.sword != null) Player.sword.gameObject.SetActive(false);
            broom.gameObject.SetActive(true);
            Say(new[] {
                new Line("ПРИНЦЕССА", config.mistakenPrincessLine),
                new Line("ГЕРОЙ", "А награда за спасение?.."),
                new Line("ПРИНЦЕССА", config.cleaningBillLine)
            }, PlayEndingVideo);
        }
        void PlayEndingVideo()
        {
            State = Phase.Video; SetCursor(false);
            if (Video == null) Video = gameObject.AddComponent<JamVideo>();
            Video.Play(Sound, () => { State = Phase.Ending; SetCursor(false); });
        }
        IEnumerator FadeTo(float value, float seconds)
        {
            float from = Fade, t = 0;
            while (t < seconds) { t += Time.unscaledDeltaTime; Fade = Mathf.Lerp(from, value, t / seconds); yield return null; }
            Fade = value;
        }
        public void Restart()
        {
            Time.timeScale = 1; Paused = false; StopAllCoroutines(); StartCoroutine(Load("MJ_Intro"));
        }
        IEnumerator Load(string scene)
        {
            Time.timeScale = 1;
            State = Phase.Transition; SetCursor(false);
            yield return FadeTo(1, 0.35f);
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError("Mini Jam: scene missing from Build Settings: " + scene);
                Fade = 0; State = introScene ? Phase.Menu : Phase.Playing;
                SetCursor(!introScene); Toast("Добавь MJ_Intro и MJ_Castle в Build Settings через Mini Jam → Проверить сцены.", 15);
                yield break;
            }
            yield return SceneManager.LoadSceneAsync(scene);
        }
        public void Quit()
        {
            Time.timeScale = 1; SetCursor(false);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
