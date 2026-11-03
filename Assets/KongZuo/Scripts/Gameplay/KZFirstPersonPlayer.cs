using UnityEngine;

namespace KongZuo
{
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class KZFirstPersonPlayer : MonoBehaviour
    {
        [SerializeField] private Transform eye;
        [SerializeField, Min(0f)] private float walkSpeed = 3.5f;
        [SerializeField, Min(1f)] private float sprintMultiplier = 1.35f;
        [SerializeField, Min(0f)] private float sensitivity = 1f;
        // Zero means the previous instantaneous movement/aiming behaviour.
        [SerializeField, Min(0f)] private float moveAcceleration = 0f;
        [SerializeField, Range(0f, 0.2f)] private float lookSmoothTime = 0f;
        [SerializeField] private float gravity = -19.6f;

        private CharacterController controller;
        private float pitch;
        private float verticalSpeed;
        private bool freezeWalking;
        private Vector3 horizontalVelocity;
        private Vector2 smoothLook;
        private Vector2 lookVelocity;

        public void SetCameraPivot(Transform pivot) { eye = pivot; }
        public void SetFreezeWalking(bool frozen)
        {
            freezeWalking = frozen;
            if (frozen) horizontalVelocity = Vector3.zero;
        }

        public void Teleport(Vector3 position, Quaternion facing)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            bool wasEnabled = controller.enabled;
            if (wasEnabled) controller.enabled = false;
            transform.SetPositionAndRotation(position, facing);
            if (wasEnabled) controller.enabled = true;
            pitch = 0f;
            verticalSpeed = 0f;
            horizontalVelocity = Vector3.zero;
            smoothLook = Vector2.zero;
            lookVelocity = Vector2.zero;
            if (eye != null) eye.localRotation = Quaternion.identity;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (eye == null)
            {
                Camera cameraChild = GetComponentInChildren<Camera>();
                if (cameraChild != null) eye = cameraChild.transform;
            }
        }

        private void Start()
        {
            KZGameSession.EnsureExists();
            SetCursor(true);
        }

        private void OnDisable() { SetCursor(false); }

        private void OnApplicationFocus(bool focused)
        {
            // Losing focus should never leave a locked, invisible cursor behind.
            if (!focused) SetCursor(false);
        }

        private void Update()
        {
            // Modal windows consume E/Esc. The player must not also unlock the cursor
            // on the same frame a journal or page closes.
            if (KZHud.IsModal) return;

            if (!KZPhotoAlignmentPuzzle.IsAnyActive && KZInput.ConsumeEscape())
                SetCursor(Cursor.lockState != CursorLockMode.Locked);
            else if (Cursor.lockState != CursorLockMode.Locked && KZInput.Click()) SetCursor(true);

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                horizontalVelocity = Vector3.zero;
                smoothLook = Vector2.zero;
                lookVelocity = Vector2.zero;
                return;
            }

            Vector2 look = KZInput.Look() * sensitivity;
            if (lookSmoothTime > 0f)
                look = Vector2.SmoothDamp(smoothLook, look, ref lookVelocity,
                    lookSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            smoothLook = look;

            transform.Rotate(Vector3.up, look.x, Space.Self);
            pitch = Mathf.Clamp(pitch - look.y, -82f, 82f);
            if (eye != null) eye.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            if (controller == null || !controller.enabled) return;

            Vector3 desired = Vector3.zero;
            if (!freezeWalking)
            {
                Vector2 axis = KZInput.Move();
                Vector3 direction = transform.right * axis.x + transform.forward * axis.y;
                float speed = walkSpeed * (KZInput.SprintHeld() ? sprintMultiplier : 1f);
                desired = direction * speed;
            }

            horizontalVelocity = moveAcceleration <= 0f
                ? desired
                : Vector3.MoveTowards(horizontalVelocity, desired, moveAcceleration * Time.deltaTime);
            if (freezeWalking) horizontalVelocity = Vector3.zero;

            // Still apply gravity while walking is locked so actors cannot float.
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -1.5f;
            verticalSpeed += gravity * Time.deltaTime;
            controller.Move((horizontalVelocity + Vector3.up * verticalSpeed) * Time.deltaTime);
        }

        private static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
