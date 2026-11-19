using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace MiniJam
{
    // The original MP4 is copied to StreamingAssets by the editor installer.
    // APIOnly lets IMGUI preserve the video's aspect ratio on any display.
    public sealed class JamVideo : MonoBehaviour
    {
        VideoPlayer player;
        AudioSource audioTrack;
        JamSound sound;
        bool finished, active;
        string error;
        float started;
        void Start() { Prepare(); }
        void Prepare()
        {
            if (player != null) return;
            audioTrack = gameObject.AddComponent<AudioSource>();
            audioTrack.playOnAwake = false; audioTrack.spatialBlend = 0;
            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false; player.isLooping = false;
            player.waitForFirstFrame = true; player.skipOnDrop = false;
            player.renderMode = VideoRenderMode.APIOnly;
            player.source = VideoSource.Url;
            string path = Path.Combine(Application.streamingAssetsPath, "MiniJam/twist.mp4");
            player.url = path.Contains("://") ? path : new Uri(Path.GetFullPath(path)).AbsoluteUri;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.controlledAudioTrackCount = 1;
            player.EnableAudioTrack(0, true); player.SetTargetAudioSource(0, audioTrack);
            player.loopPointReached += OnEnd;
            player.errorReceived += OnError;
            player.Prepare();
        }
        void OnEnd(VideoPlayer source) { finished = true; }
        void OnError(VideoPlayer source, string message)
        {
            error = message;
            Debug.LogWarning("Mini Jam: видео не воспроизведено: " + message);
        }
        public void Play(JamSound gameSound, Action onComplete)
        {
            if (active) return;
            Prepare(); sound = gameSound;
            active = true; finished = false; started = Time.realtimeSinceStartup;
            if (sound != null) sound.DuckForVideo(true);
            audioTrack.volume = sound != null ? sound.Volume : 0.7f;
            StartCoroutine(Playback(onComplete));
        }
        IEnumerator Playback(Action onComplete)
        {
            while (!player.isPrepared && error == null && Time.realtimeSinceStartup - started < 8)
                yield return null;
            if (!player.isPrepared && error == null) error = "Время подготовки видео истекло.";
            if (error == null)
            {
                player.Play();
                float deadline = Time.realtimeSinceStartup + Mathf.Max(10, (float)player.length + 5);
                while (!finished && error == null && Time.realtimeSinceStartup < deadline)
                {
                    if (Time.realtimeSinceStartup - started > 0.35f && JamInput.Pause) break;
                    yield return null;
                }
                if (!finished && error == null && Time.realtimeSinceStartup >= deadline)
                    error = "Время воспроизведения видео истекло.";
            }
            if (error != null) yield return new WaitForSecondsRealtime(2);
            Cleanup();
            onComplete?.Invoke();
        }
        void OnGUI()
        {
            if (!active) return;
            GUI.depth = -100;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (error == null && player.texture != null)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), player.texture, ScaleMode.ScaleToFit, false);
            else
            {
                var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, wordWrap = true };
                GUI.Label(new Rect(40, Screen.height / 2f - 70, Screen.width - 80, 140),
                    error == null ? "" : "Видео не удалось открыть. Переходим к финалу.\nПодробности — в Console.", style);
            }
            GUI.depth = 0;
        }
        void Cleanup()
        {
            active = false;
            if (player != null) player.Stop();
            if (audioTrack != null) audioTrack.Stop();
            if (sound != null) sound.DuckForVideo(false);
        }
        void OnDestroy()
        {
            Cleanup();
            if (player != null) { player.loopPointReached -= OnEnd; player.errorReceived -= OnError; }
        }
    }
}
