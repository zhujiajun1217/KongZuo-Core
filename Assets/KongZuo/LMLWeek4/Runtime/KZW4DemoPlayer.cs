using UnityEngine;

namespace KongZuo.LMLWeek4
{
    /// <summary>Test-scene-only movement. The original KZFirstPersonPlayer is untouched.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class KZW4DemoPlayer : MonoBehaviour
    {
        public Camera eyeCamera;
        public KZW4DemoDirector director;
        [Min(.1f)] public float walkSpeed = 3.4f;
        [Min(.1f)] public float lookSensitivity = 1f;
        private CharacterController body;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private float pitch;
        private float fallSpeed;

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            if (eyeCamera == null) eyeCamera = GetComponentInChildren<Camera>();
        }

        private void Start() { LockCursor(true); }
        private void OnDisable() { if (Application.isPlaying) LockCursor(false); }

        private void Update()
        {
            if (director != null && director.BlocksCamera) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (director == null || !director.IsOverlayActive)
                    if (KZInput.Click()) LockCursor(true);
                return;
            }

            Vector2 look = KZInput.Look() * lookSensitivity;
            transform.Rotate(Vector3.up, look.x);
            pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
            if (eyeCamera != null) eyeCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            if (director != null && director.EchoBusy) return;

            Vector2 move = KZInput.Move();
            if (body.isGrounded && fallSpeed < 0f) fallSpeed = -1f;
            fallSpeed += -19.6f * Time.deltaTime;
            Vector3 direction = transform.right * move.x + transform.forward * move.y;
            body.Move((direction * walkSpeed + Vector3.up * fallSpeed) * Time.deltaTime);
        }

        public void ResetToStart()
        {
            body.enabled = false;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            body.enabled = true;
            pitch = 0f;
            fallSpeed = 0f;
            if (eyeCamera != null) eyeCamera.transform.localRotation = Quaternion.identity;
            LockCursor(true);
        }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
