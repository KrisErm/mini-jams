using UnityEngine;

namespace MiniJam
{
    public sealed class JamSound : MonoBehaviour
    {
        public enum Cue { Jump, Swing, Hit, Key, Gate, Checkpoint, Respawn, Twist }
        public AudioClip[] cues;
        public AudioClip ambience;
        AudioSource effects, music;
        public float Volume { get; private set; } = 0.7f;
        void Awake()
        {
            effects = gameObject.AddComponent<AudioSource>();
            effects.spatialBlend = 0; effects.volume = 0.5f;
            music = gameObject.AddComponent<AudioSource>();
            music.spatialBlend = 0; music.volume = 0.14f; music.loop = true;
            music.clip = ambience;
            if (ambience != null) music.Play();
        }
        public void Play(Cue cue)
        {
            int index = (int)cue;
            if (cues != null && index < cues.Length && cues[index] != null) effects.PlayOneShot(cues[index]);
        }
        public void SetVolume(float value) { Volume = value; effects.volume = value * 0.65f; music.volume = value * 0.18f; }
        public void DuckForVideo(bool mute) { music.mute = mute; effects.mute = mute; }
    }
}
