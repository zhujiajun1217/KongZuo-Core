using UnityEngine;

namespace KongZuo.V03
{
    /// <summary>Self-contained controller for independent V0.3 prototype scenes.</summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class KZV03Player : MonoBehaviour
    {
        public Camera viewCamera;
        [Range(.5f, 8f)] public float walkSpeed = 3.7f;
        [Range(.25f, 4f)] public float lookSensitivity = 1.0f;
        public float interactionRange = 3.2f;
        private CharacterController motor;
        private float pitch, verticalSpeed;
        private KZV03Item lookedAt;
        private bool freezeMove;

        public void FreezeMovement(bool value) { freezeMove = value; }
        public void Teleport(Vector3 pos, Quaternion orientation)
        {
            if (motor == null) motor = GetComponent<CharacterController>();
            motor.enabled = false;
            transform.SetPositionAndRotation(pos, orientation);
            motor.enabled = true;
            pitch = 0f;
            verticalSpeed = 0f;
            if (viewCamera != null) viewCamera.transform.localRotation = Quaternion.identity;
        }

        private void Awake()
        {
            motor = GetComponent<CharacterController>();
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        }
        private void OnEnable() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        private void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        private void Update()
        {
            bool modal = KZV03Hud.IsModal;
            if (modal)
            {
                SetCursor(false);
                KZV03Hud.Hint = "";
                return;
            }
            if (KZV03Input.Escape()) SetCursor(Cursor.lockState != CursorLockMode.Locked);
            if (Cursor.lockState != CursorLockMode.Locked && KZV03Input.Click()) SetCursor(true);
            if (Cursor.lockState != CursorLockMode.Locked) { KZV03Hud.Hint = "点击画面继续操控"; return; }

            Vector2 look = KZV03Input.Look() * lookSensitivity;
            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -83f, 83f);
            if (viewCamera != null) viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            lookedAt = null;
            if (viewCamera != null && !IsBusy() &&
                Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out RaycastHit hit,
                    interactionRange, ~0, QueryTriggerInteraction.Ignore))
                lookedAt = hit.collider.GetComponentInParent<KZV03Item>();

            if (lookedAt != null && lookedAt.showAfterEastEcho && !KZV03State.Ensure().Has(KZV03Flag.EastDoorEcho))
                lookedAt = null;
            KZV03Hud.Hint = lookedAt == null ? "WASD 移动 · Tab 笔记 · E 检视" : "[E] " + lookedAt.Label;
            if (lookedAt != null && KZV03Input.Interact()) lookedAt.Use();

            if (freezeMove || IsBusy()) return;
            Vector2 movement = KZV03Input.Move();
            Vector3 forward = transform.forward * movement.y + transform.right * movement.x;
            if (motor.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += -19.6f * Time.deltaTime;
            motor.Move((forward * walkSpeed + Vector3.up * verticalSpeed) * Time.deltaTime);
        }

        private static bool IsBusy() { return KZV03Director.Current != null && KZV03Director.Current.Busy; }
        private static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
