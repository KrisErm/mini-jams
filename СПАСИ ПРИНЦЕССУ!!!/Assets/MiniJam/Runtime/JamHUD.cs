using UnityEngine;

namespace MiniJam
{
    // IMGUI keeps the drop-in package independent of TMP, UGUI and input UI modules.
    public sealed class JamHUD : MonoBehaviour
    {
        public Font font;
        GUIStyle small, body, heading, title, button, speaker;
        readonly Color ink = new Color(0.055f, 0.072f, 0.105f, 0.96f);
        readonly Color gold = new Color(0.93f, 0.74f, 0.40f);
        readonly Color muted = new Color(0.65f, 0.71f, 0.78f);
        float volume = 0.7f;
        void Styles()
        {
            if (body != null) return;
            small = Style(19, muted); body = Style(28, Color.white);
            heading = Style(37, Color.white, true); title = Style(76, Color.white, true);
            speaker = Style(22, gold, true);
            button = Style(25, Color.white, true); button.alignment = TextAnchor.MiddleCenter;
        }
        GUIStyle Style(int size, Color color, bool bold = false)
        {
            return new GUIStyle(GUI.skin.label) { font = font, fontSize = size, wordWrap = true,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = color }, padding = new RectOffset(0, 0, 0, 0) };
        }
        void Fill(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white; }
        void Panel(Rect r) { Fill(r, ink); Fill(new Rect(r.x, r.y, 4, r.height), gold); }
        bool Button(Rect r, string text, bool primary = false)
        {
            Color c = primary ? new Color(0.49f, 0.19f, 0.24f) : new Color(0.15f, 0.19f, 0.25f);
            if (r.Contains(Event.current.mousePosition)) c *= 1.3f;
            c.a = 1; Fill(r, c);
            return GUI.Button(r, text, button);
        }
        void OnGUI()
        {
            var game = JamGame.Instance;
            if (game == null) return;
            if (game.State == JamGame.Phase.Video) return;
            Styles();
            Matrix4x4 previous = GUI.matrix;
            // Letterbox coordinates: UI keeps its proportions on 4:3 and ultrawide monitors.
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            Vector3 offset = new Vector3((Screen.width - 1600 * scale) / 2, (Screen.height - 900 * scale) / 2, 0);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one * scale);
            if (game.State == JamGame.Phase.Menu) Menu(game);
            if (game.State == JamGame.Phase.Playing && !game.Paused) Gameplay(game);
            if (game.State == JamGame.Phase.Dialogue) Dialogue(game);
            if (game.introScene && (game.State == JamGame.Phase.Intro || game.State == JamGame.Phase.Dialogue))
                if (Button(new Rect(1260, 36, 290, 48), "Пропустить вступление")) game.SkipIntro();
            if (game.Paused) Pause(game);
            if (game.State == JamGame.Phase.Ending) Ending(game);
            if (!string.IsNullOrEmpty(game.ToastText) && Time.unscaledTime < game.ToastUntil && game.State == JamGame.Phase.Playing && !game.Paused)
            {
                Panel(new Rect(280, 708, 1040, 76));
                GUI.Label(new Rect(308, 730, 984, 46), game.ToastText, small);
            }
            if (game.TwistCard)
            {
                Fill(new Rect(0, 280, 1600, 330), new Color(0.035f, 0.045f, 0.07f, 0.95f));
                Fill(new Rect(0, 280, 1600, 5), gold); Fill(new Rect(0, 605, 1600, 5), gold);
                var centered = new GUIStyle(title) { alignment = TextAnchor.MiddleCenter, fontSize = 100 };
                GUI.Label(new Rect(50, 340, 1500, 180), "ВОТ ЭТО ПОВОРОТ", centered);
            }
            GUI.matrix = previous;
            if (game.Fade > 0) Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.035f, 0.045f, 0.065f, game.Fade));
        }
        void Menu(JamGame game)
        {
            Panel(new Rect(72, 92, 660, 722));
            GUI.Label(new Rect(112, 136, 580, 32), "МИНИ-ДЖЕМ  /  КОРОТКОЕ ПРИКЛЮЧЕНИЕ", speaker);
            GUI.Label(new Rect(106, 211, 590, 190), "СПАСИ\nПРИНЦЕССУ", title);
            GUI.Label(new Rect(112, 433, 540, 95), "Замок. Стража. Принцесса.\nЧто может пойти не так?", body);
            if (Button(new Rect(112, 572, 540, 68), "Отправиться на подвиг", true)) game.StartAdventure();
            if (Button(new Rect(112, 656, 540, 54), "Выход")) game.Quit();
            GUI.Label(new Rect(112, 745, 550, 40), "WASD  •  Мышь  •  Space  •  ЛКМ  •  E", small);
        }
        void Gameplay(JamGame game)
        {
            // A small centre reticle remains readable against both stone and torch light.
            Fill(new Rect(796, 446, 8, 8), new Color(0, 0, 0, 0.6f));
            Fill(new Rect(798, 448, 4, 4), gold);
            if (game.Player != null && game.Player.Focus != null)
            {
                Panel(new Rect(540, 615, 520, 68));
                GUI.Label(new Rect(568, 632, 464, 44), "[ E ]  " + game.Player.Focus.Prompt, body);
            }
        }
        void Dialogue(JamGame game)
        {
            Fill(new Rect(0, 0, 1600, 50), new Color(0, 0, 0, 0.75f));
            Panel(new Rect(110, 604, 1380, 248));
            GUI.Label(new Rect(150, 631, 1260, 32), game.CurrentLine.speaker, speaker);
            string content = game.CurrentLine.text ?? "";
            string visible = content.Substring(0, Mathf.Min(content.Length, game.VisibleCharacters));
            GUI.Label(new Rect(150, 683, 1250, 108), visible, body);
            GUI.Label(new Rect(150, 805, 880, 32), "ЛКМ / E / Space — продолжить", small);
            GUI.Label(new Rect(1240, 800, 205, 40), "ДАЛЕЕ  →", speaker);
        }
        void Pause(JamGame game)
        {
            Fill(new Rect(0, 0, 1600, 900), new Color(0, 0, 0, 0.7f));
            Panel(new Rect(490, 154, 620, 594));
            GUI.Label(new Rect(534, 190, 540, 64), "Передышка", heading);
            if (Button(new Rect(534, 279, 532, 64), "Продолжить", true)) game.TogglePause();
            GUI.Label(new Rect(534, 377, 525, 38), "Громкость", small);
            float next = GUI.HorizontalSlider(new Rect(534, 425, 532, 24), volume, 0, 1);
            if (!Mathf.Approximately(next, volume)) { volume = next; game.Sound.SetVolume(volume); }
            if (Button(new Rect(534, 492, 532, 58), "Начать заново")) game.Restart();
            if (Button(new Rect(534, 571, 532, 58), "Выход")) game.Quit();
            GUI.Label(new Rect(534, 670, 530, 35), "Прогресс хранится до выхода из игры.", small);
        }
        void Ending(JamGame game)
        {
            Panel(new Rect(720, 100, 810, 700));
            GUI.Label(new Rect(770, 140, 700, 38), "НОВОЕ ЗАДАНИЕ ПОЛУЧЕНО", speaker);
            GUI.Label(new Rect(766, 208, 710, 190), "СПАСИ\nПАРКЕТ", title);
            GUI.Label(new Rect(770, 416, 685, 94), "Принцесса не спасена.\nЗато у замка появился уборщик.", body);
            int seconds = Mathf.FloorToInt(game.PlayTime);
            GUI.Label(new Rect(770, 536, 680, 40), "Время приключения: " + (seconds / 60) + ":" + (seconds % 60).ToString("00"), small);
            if (Button(new Rect(770, 612, 680, 64), "Ещё один поворот", true)) game.Restart();
            if (Button(new Rect(770, 696, 680, 54), "Выход")) game.Quit();
        }
    }
}
