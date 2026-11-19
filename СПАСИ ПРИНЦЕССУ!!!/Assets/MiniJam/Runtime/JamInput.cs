using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MiniJam
{
    // Compile only the enabled backend. No Input Actions asset or axis setup is needed.
    public static class JamInput
    {
        public static Vector2 Move
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k == null) return Vector2.zero;
                return Vector2.ClampMagnitude(new Vector2(
                    ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1 : 0) - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1 : 0),
                    ((k.wKey.isPressed || k.upArrowKey.isPressed) ? 1 : 0) - ((k.sKey.isPressed || k.downArrowKey.isPressed) ? 1 : 0)), 1);
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Vector2.ClampMagnitude(new Vector2(
                    ((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) ? 1 : 0) - ((Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) ? 1 : 0),
                    ((Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? 1 : 0) - ((Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ? 1 : 0)), 1);
#else
                return Vector2.zero;
#endif
            }
        }
        public static Vector2 Look
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue() * 0.11f;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 2f;
#else
                return Vector2.zero;
#endif
            }
        }
        public static bool Jump => Down(0);
        public static bool Interact => Down(1);
        public static bool Pause => Down(2);
        public static bool Attack => Down(3);
        public static bool Advance => Down(4) || Interact || Attack || Jump;
        static bool Down(int key)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (key == 3) return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (k == null) return false;
            switch (key)
            {
                case 0: return k.spaceKey.wasPressedThisFrame;
                case 1: return k.eKey.wasPressedThisFrame;
                case 2: return k.escapeKey.wasPressedThisFrame;
                case 4: return k.enterKey.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            switch (key)
            {
                case 0: return Input.GetKeyDown(KeyCode.Space);
                case 1: return Input.GetKeyDown(KeyCode.E);
                case 2: return Input.GetKeyDown(KeyCode.Escape);
                case 3: return Input.GetMouseButtonDown(0);
                case 4: return Input.GetKeyDown(KeyCode.Return);
            }
#endif
            return false;
        }
    }
}
