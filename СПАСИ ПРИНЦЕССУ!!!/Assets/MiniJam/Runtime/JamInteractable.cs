using UnityEngine;

namespace MiniJam
{
    public sealed class JamInteractable : MonoBehaviour
    {
        public enum Kind { Key, LockedGate, Lever, Princess }
        public Kind kind;
        public JamGate gate;
        public Transform movingPart;
        public bool used;
        public bool Available => !used && kind != Kind.Princess;
        public string Prompt
        {
            get
            {
                switch (kind)
                {
                    case Kind.Key: return "Забрать ключ";
                    case Kind.LockedGate: return "Открыть решётку";
                    case Kind.Lever: return "Потянуть рычаг";
                    default: return "Спасти принцессу";
                }
            }
        }
        void OnTriggerEnter(Collider other) { TryStartFinale(other); }
        void OnTriggerStay(Collider other) { TryStartFinale(other); }
        void TryStartFinale(Collider other)
        {
            if (kind != Kind.Princess || used) return;
            var player = other.GetComponentInParent<JamPlayer>();
            if (player != null && JamGame.Instance != null && JamGame.Instance.CanPlay) Use(player);
        }
        void Update()
        {
            if (kind == Kind.Key && movingPart != null && !used)
                movingPart.Rotate(0, 70 * Time.deltaTime, 0, Space.World);
        }
        public void Use(JamPlayer player)
        {
            if (used || JamGame.Instance == null || !JamGame.Instance.CanPlay) return;
            var game = JamGame.Instance;
            switch (kind)
            {
                case Kind.Key:
                    used = true; player.HasKey = true;
                    game.Sound.Play(JamSound.Cue.Key);
                    
                    game.Objective = "Открой решётку в конце караульного зала";
                    gameObject.SetActive(false);
                    break;
                case Kind.LockedGate:
                    if (!player.HasKey) { game.Toast("Заперто."); return; }
                    used = true; gate.Open();
                    game.Sound.Play(JamSound.Cue.Gate);
                    game.Objective = "Перейди разрушенный мост";
                    break;
                case Kind.Lever:
                    used = true; gate.Open();
                    if (movingPart != null) movingPart.localRotation *= Quaternion.Euler(55, 0, 0);
                    game.Sound.Play(JamSound.Cue.Gate);
                    game.Objective = "Принцесса уже близко. Войди в тронный зал";
                    break;
                case Kind.Princess:
                    used = true; game.BeginFinale();
                    break;
            }
        }
    }
}
