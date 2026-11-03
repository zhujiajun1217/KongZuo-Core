using UnityEngine;

namespace KongZuo
{
    [DisallowMultipleComponent]
    public sealed class KZInteractor : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0.5f)] private float reach = 3.3f;
        [SerializeField] private LayerMask raycastMask = ~0;
        private KZInteractable focused;

        public void SetCamera(Camera camera) { viewCamera = camera; }

        private void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        }

        private void OnDisable() { Focus(null); }

        private void Focus(KZInteractable item)
        {
            if (focused == item) return;
            focused = item;
            KZHud.SetHint(item == null ? "" : "[E] " + item.DisplayName);
        }

        private void Update()
        {
            if (KZHud.IsModal || Cursor.lockState != CursorLockMode.Locked ||
                KZPhotoAlignmentPuzzle.IsAnyActive ||
                (KZStoryDirector.Current != null && KZStoryDirector.Current.IsBusy))
            {
                Focus(null);
                return;
            }

            KZInteractable target = null;
            if (viewCamera != null &&
                Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward,
                    out RaycastHit hit, reach, raycastMask, QueryTriggerInteraction.Ignore))
            {
                target = hit.collider.GetComponentInParent<KZInteractable>();
            }

            Focus(target);
            if (target != null && KZInput.ConsumeInteract())
                target.Interact();
        }
    }
}
