using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KongZuo
{
    // 无需手工创建 InputAction：灰盒阶段直接监听键盘/鼠标。
    // 同时支持仅启用新输入系统、仅旧系统及 Both（新系统优先）。
    public static class KZInput
    {
        private static int lastInteractFrame = -1;
        private static int lastEscapeFrame = -1;

        public static Vector2 Move()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return Vector2.zero;
            int x = ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1 : 0)
                - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1 : 0);
            int y = ((k.wKey.isPressed || k.upArrowKey.isPressed) ? 1 : 0)
                - ((k.sKey.isPressed || k.downArrowKey.isPressed) ? 1 : 0);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Vector2.ClampMagnitude(
                new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#else
            return Vector2.zero;
#endif
        }

        public static Vector2 Look()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue() * 0.09f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 2f;
#else
            return Vector2.zero;
#endif
        }

        private static bool RawInteract()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }

        // 避免：同一帧 E 关闭文档后又重新触发射线交互。
        public static bool ConsumeInteract()
        {
            if (!RawInteract() || lastInteractFrame == Time.frameCount) return false;
            lastInteractFrame = Time.frameCount;
            return true;
        }

        public static bool ConsumeEscape()
        {
            if (!Escape() || lastEscapeFrame == Time.frameCount) return false;
            lastEscapeFrame = Time.frameCount;
            return true;
        }

        public static bool SprintHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            return k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
            return false;
#endif
        }

        public static bool Escape()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        public static bool Journal()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Tab);
#else
            return false;
#endif
        }

        public static bool Photo()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F);
#else
            return false;
#endif
        }

        public static bool Click()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        // index: 0=数字1, 1=数字2, 2=数字3
        public static bool Number(int index)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return false;
            switch (index)
            {
                case 0: return k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame;
                case 1: return k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame;
                case 2: return k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame;
                default: return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            switch (index)
            {
                case 0: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
                case 1: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
                case 2: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
                default: return false;
            }
#else
            return false;
#endif
        }

        public static bool Confirm()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.numpadEnterKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
            return false;
#endif
        }
    }
}
