using System.Collections.Generic;
using UnityEngine;

namespace MiniJam
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class JamPlayer : MonoBehaviour
    {
        public JamConfig config;
        public JamCamera rig;
        public Transform visual;
        public Transform sword;
        public Transform viewModel;
        public Animator animator;
        public JamInteractable Focus { get; private set; }
        public bool HasKey { get; set; }
        public Vector3 Checkpoint { get; private set; }
        CharacterController controller;
        float verticalSpeed, attackUntil, nextAttack, coyoteUntil, jumpUntil, invulnerableUntil;
        float walkPhase;
        Quaternion swordRest;
        Vector3 visualRest;
        Vector3 weaponRest;
        bool hitPending;
        readonly HashSet<JamGuard> hitGuards = new HashSet<JamGuard>();

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            Checkpoint = transform.position;
            if (sword != null) swordRest = sword.localRotation;
            if (visual != null) visualRest = visual.localPosition;
            if (viewModel != null) weaponRest = viewModel.localPosition;
        }
        void Start() { SetFirstPerson(true); }
        public void SetFirstPerson(bool enabled)
        {
            if (visual != null) visual.gameObject.SetActive(!enabled);
            if (sword != null) sword.gameObject.SetActive(!enabled);
            if (viewModel != null) viewModel.gameObject.SetActive(enabled);
        }
        void Update()
        {
            var game = JamGame.Instance;
            if (game == null || !game.CanPlay) { Focus = null; Animate(0); return; }
            if (transform.position.y < -5) { game.Respawn(); return; }
            float speed = config != null ? config.moveSpeed : 5.5f;
            Vector2 input = JamInput.Move;
            Vector3 motion = rig.FlatForward * input.y + rig.FlatRight * input.x;
            // Strafing never turns the body away from the direction of view.
            transform.rotation = Quaternion.Euler(0, rig.yaw, 0);
            if (controller.isGrounded)
            {
                if (verticalSpeed < 0) verticalSpeed = -2;
                coyoteUntil = Time.time + 0.1f;
            }
            if (JamInput.Jump) jumpUntil = Time.time + 0.12f;
            if (jumpUntil > Time.time && coyoteUntil > Time.time)
            {
                verticalSpeed = Mathf.Sqrt(44f * (config != null ? config.jumpHeight : 1.5f));
                jumpUntil = coyoteUntil = 0;
                game.Sound.Play(JamSound.Cue.Jump);
            }
            verticalSpeed -= 22f * Time.deltaTime;
            var flags = controller.Move((motion * speed + Vector3.up * verticalSpeed) * Time.deltaTime);
            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
            if (!game.CanPlay) return;
            FindInteraction();
            if (JamInput.Interact && Focus != null) Focus.Use(this);
            if (!game.CanPlay) return;
            if (JamInput.Attack && Time.time >= nextAttack)
            {
                nextAttack = Time.time + 0.5f;
                attackUntil = Time.time + 0.32f;
                hitPending = true;
                hitGuards.Clear();
                if (animator != null && animator.isActiveAndEnabled) animator.SetTrigger("Attack");
                game.Sound.Play(JamSound.Cue.Swing);
            }
            if (hitPending && Time.time >= attackUntil - 0.18f)
            {
                hitPending = false;
                Vector3 origin = transform.position + Vector3.up;
                foreach (var collider in Physics.OverlapSphere(origin + transform.forward * 0.8f, 1.3f))
                {
                    var crate = collider.GetComponentInParent<JamBreakable>();
                    if (crate != null)
                    {
                        Vector3 target = collider.ClosestPoint(origin);
                        Vector3 direction = target - origin;
                        if (Vector3.Dot(transform.forward, direction.normalized) > 0.05f &&
                            (!Physics.Linecast(origin, target, out RaycastHit obstacle, 1, QueryTriggerInteraction.Ignore) ||
                             obstacle.collider.GetComponentInParent<JamBreakable>() == crate)) crate.Hit();
                        continue;
                    }
                    var guard = collider.GetComponentInParent<JamGuard>();
                    if (guard == null || guard.IsDown || !hitGuards.Add(guard)) continue;
                    Vector3 to = guard.transform.position - transform.position;
                    if (Vector3.Dot(transform.forward, to.normalized) < 0.05f) continue;
                    if (Physics.Linecast(origin, guard.transform.position + Vector3.up, 1, QueryTriggerInteraction.Ignore)) continue;
                    guard.KnockOut();
                }
            }
            Animate(motion.magnitude);
        }
        void Animate(float speed)
        {
            if (animator != null && animator.isActiveAndEnabled) animator.SetFloat("Speed", speed);
            if (JamGame.Instance == null || JamGame.Instance.Paused) return;
            walkPhase += Time.deltaTime * speed * 13;
            if (visual != null && animator == null)
                visual.localPosition = visualRest + Vector3.up * (Mathf.Abs(Mathf.Sin(walkPhase)) * 0.055f * speed);
            if (sword != null)
            {
                float progress = Mathf.Clamp01(1f - (attackUntil - Time.time) / 0.32f);
                float swing = Time.time < attackUntil ? Mathf.Sin(progress * Mathf.PI) * -115f : 0;
                sword.localRotation = swordRest * Quaternion.Euler(swing, 0, -swing * 0.3f);
            }
            if (viewModel != null && viewModel.gameObject.activeSelf)
            {
                float progress = Mathf.Clamp01(1f - (attackUntil - Time.time) / 0.32f);
                float swing = Time.time < attackUntil ? Mathf.Sin(progress * Mathf.PI) : 0;
                Vector3 bob = new Vector3(Mathf.Sin(walkPhase * 0.5f), Mathf.Abs(Mathf.Sin(walkPhase)), 0) * (speed * 0.012f);
                Vector3 position = weaponRest + bob + new Vector3(-0.16f, 0.06f, 0.02f) * swing;
                // Pull the view model inward near a wall instead of letting it vanish into it.
                if (Physics.Raycast(rig.transform.position, rig.transform.forward, out RaycastHit hit, 0.75f, 1, QueryTriggerInteraction.Ignore))
                    position.z = Mathf.Min(position.z, Mathf.Max(0.16f, hit.distance - 0.15f));
                viewModel.localPosition = position;
                viewModel.localRotation = Quaternion.Euler(swing * 65f, swing * -20f, swing * 35f);
            }
        }
        void FindInteraction()
        {
            Focus = null;
            float best = 2.6f;
            foreach (var collider in Physics.OverlapSphere(transform.position + Vector3.up * 0.8f, 2.7f))
            {
                var item = collider.GetComponentInParent<JamInteractable>();
                if (item == null || !item.Available) continue;
                float d = Vector3.Distance(transform.position, item.transform.position);
                if (d >= best) continue;
                Vector3 from = transform.position + Vector3.up;
                Vector3 to = item.transform.position + Vector3.up * 0.6f;
                if (Physics.Linecast(from, to, out RaycastHit hit, 1, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<JamInteractable>() != item) continue;
                best = d; Focus = item;
            }
        }
        public void SetCheckpoint(Vector3 point) { Checkpoint = point; }
        public void StopActions()
        {
            hitPending = false; attackUntil = nextAttack = 0;
            jumpUntil = 0; Focus = null;
        }
        public void Teleport(Vector3 point, bool protect = true)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(point, Quaternion.identity);
            controller.enabled = true;
            verticalSpeed = 0;
            coyoteUntil = jumpUntil = 0;
            attackUntil = nextAttack = 0;
            hitPending = false;
            if (protect) invulnerableUntil = Time.time + 1.3f;
            if (rig != null) rig.Snap(true);
        }
        public void Hurt()
        {
            if (Time.time >= invulnerableUntil && JamGame.Instance != null && JamGame.Instance.CanPlay)
                JamGame.Instance.Respawn();
        }
    }
}
