using UnityEngine;

namespace MiniJam
{
    public sealed class JamZone : MonoBehaviour
    {
        public enum Kind { Checkpoint, Fall }
        public Kind kind;
        public Transform spawn;
        public int chapter = 1;
        public string title;
        [TextArea] public string objective;
        [TextArea] public string notice;
        bool activated;
        void OnTriggerEnter(Collider other) { Enter(other); }
        void OnTriggerStay(Collider other) { Enter(other); }
        void Enter(Collider other)
        {
            var player = other.GetComponentInParent<JamPlayer>();
            var game = JamGame.Instance;
            if (player == null || game == null || !game.CanPlay) return;
            if (kind == Kind.Fall) { player.Hurt(); return; }
            if (activated) return;
            activated = true;
            player.SetCheckpoint(spawn.position);
            game.Chapter = chapter;
            game.ChapterTitle = title;
            game.Objective = objective;
            game.Sound.Play(JamSound.Cue.Checkpoint);
        }
    }
}
