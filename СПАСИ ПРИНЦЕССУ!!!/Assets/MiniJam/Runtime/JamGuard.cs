using UnityEngine;

namespace MiniJam
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class JamGuard : MonoBehaviour
    {
        public Transform pointA, pointB, visual;
        public Animator animator;
        public JamConfig config;
        public string warningLine;
        bool warned;
        public float detectionRange = 5.5f;
        public bool IsDown { get; private set; }
        CharacterController controller;
        Vector3 home, visualPosition;
        Quaternion visualRotation;
        bool towardB = true;
        float attackAt = -1, recoveryUntil, fall, gravity, phase;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            home = transform.position;
            if (visual != null) { visualRotation = visual.localRotation; visualPosition = visual.localPosition; }
        }
        void Update()
        {
            var game = JamGame.Instance;
            if (game == null || game.Paused) return;
            if (IsDown)
            {
                fall = Mathf.MoveTowards(fall, 1, Time.deltaTime * 4);
                if (visual != null)
                {
                    visual.localRotation = visualRotation * Quaternion.Euler(0, 0, fall * 85);
                    visual.localPosition = visualPosition + Vector3.up * (fall * 0.25f);
                }
                return;
            }
            if (!game.CanPlay) { if (animator != null) animator.SetFloat("Speed", 0); return; }
            Vector3 toPlayer = game.Player.transform.position - transform.position;
            float distance = toPlayer.magnitude;
            bool visible = distance < detectionRange && !Physics.Linecast(transform.position + Vector3.up,
                game.Player.transform.position + Vector3.up, 1, QueryTriggerInteraction.Ignore);
            if (visible && !warned && !string.IsNullOrEmpty(warningLine) && game.TryGuardShout(warningLine)) warned = true;
            Vector3 target = visible ? game.Player.transform.position : (towardB ? pointB.position : pointA.position);
            if (!visible && Vector3.Distance(transform.position, target) < 0.4f) towardB = !towardB;
            // Keep each guard within its own room and away from pit/checkpoint entrances.
            target.z = Mathf.Clamp(target.z, Mathf.Min(pointA.position.z, pointB.position.z) - 1,
                Mathf.Max(pointA.position.z, pointB.position.z) + 1);
            Vector3 direction = target - transform.position; direction.y = 0;
            float speed = config != null ? config.guardSpeed : 2.4f;
            if (attackAt >= 0)
            {
                speed = 0;
                if (Time.time >= attackAt)
                {
                    if (visible && distance < 1.65f) game.Player.Hurt();
                    attackAt = -1; recoveryUntil = Time.time + 0.8f;
                }
            }
            else if (visible && distance < 1.6f && Time.time >= recoveryUntil)
            {
                attackAt = Time.time + 0.7f; speed = 0;
                if (animator != null) animator.SetTrigger("Attack");
            }
            if (Time.time < recoveryUntil || direction.magnitude < 0.2f) speed = 0;
            if (direction.sqrMagnitude > 0.03f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 8);
            gravity = controller.isGrounded ? -2 : gravity - 22 * Time.deltaTime;
            controller.Move((direction.normalized * speed + Vector3.up * gravity) * Time.deltaTime);
            if (animator != null) animator.SetFloat("Speed", speed > 0 ? 1 : 0);
            else if (visual != null)
            {
                phase += Time.deltaTime * speed * 5;
                visual.localPosition = visualPosition + Vector3.up * (speed > 0 ? Mathf.Abs(Mathf.Sin(phase)) * 0.05f : 0);
            }
        }
        void OnGUI()
        {
            var game = JamGame.Instance;
            if (game == null || !game.CanPlay || game.Camera == null) return;
            if (!IsDown && attackAt < 0) return;
            Vector3 p = game.Camera.WorldToScreenPoint(transform.position + Vector3.up * 2.3f);
            if (p.z < 0) return;
            GUI.color = IsDown ? new Color(0.75f, 0.9f, 1) : new Color(1, 0.35f, 0.22f);
            var style = new GUIStyle(GUI.skin.label) { fontSize = 25, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(p.x - 70, Screen.height - p.y - 20, 140, 40), IsDown ? "Z z z" : "!", style);
            GUI.color = Color.white;
        }
        public void KnockOut()
        {
            if (IsDown) return;
            IsDown = true; attackAt = -1;
            controller.enabled = false;
            if (animator != null) animator.enabled = false;
            JamGame.Instance.Sound.Play(JamSound.Cue.Hit);
        }
        public void ResetGuard()
        {
            IsDown = false; attackAt = -1; fall = gravity = 0;
            recoveryUntil = Time.time + 1;
            controller.enabled = false;
            transform.SetPositionAndRotation(home, Quaternion.identity);
            controller.enabled = true;
            if (visual != null) { visual.localPosition = visualPosition; visual.localRotation = visualRotation; }
            if (animator != null) animator.enabled = true;
        }
    }
}
