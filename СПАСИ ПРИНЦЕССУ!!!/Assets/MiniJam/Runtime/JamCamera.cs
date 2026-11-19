using UnityEngine;

namespace MiniJam
{
    [DefaultExecutionOrder(100)]
    public sealed class JamCamera : MonoBehaviour
    {
        public Transform target;
        public JamConfig config;
        public float yaw;
        public float pitch;
        public float eyeHeight = 1.62f;
        public bool cinematic;
        public Vector3 FlatForward => Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        public Vector3 FlatRight => Quaternion.Euler(0, yaw, 0) * Vector3.right;

        void LateUpdate()
        {
            if (cinematic || target == null) return;
            var game = JamGame.Instance;
            if (game != null && game.CanPlay && Cursor.lockState == CursorLockMode.Locked)
            {
                var look = JamInput.Look * (config != null ? config.mouseSensitivity : 1f);
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, -78, 78);
            }
            Snap(false);
        }
        public void Snap(bool resetYaw)
        {
            if (target == null) return;
            if (resetYaw) { yaw = target.eulerAngles.y; pitch = 0; }
            // Eye is inside the controller capsule. Camera has no third-person orbit.
            transform.SetPositionAndRotation(target.position + Vector3.up * eyeHeight, Quaternion.Euler(pitch, yaw, 0));
        }
    }
}
