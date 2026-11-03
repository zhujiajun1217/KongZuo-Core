using UnityEngine;

namespace KongZuo
{
    public enum KZZone { Audience, ApproachSeat }

    // 给 BoxCollider 勾选 Is Trigger。只响应玩家 CharacterController。
    [RequireComponent(typeof(BoxCollider))]
    public sealed class KZStoryTrigger : MonoBehaviour
    {
        public KZZone zone;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<KZFirstPersonPlayer>() == null) return;
            if (KZStoryDirector.Current == null) return;
            if (zone == KZZone.Audience) KZStoryDirector.Current.AudienceEntered();
            else KZStoryDirector.Current.ApproachedSeat();
        }

        private void OnTriggerExit(Collider other)
        {
            if (zone != KZZone.Audience ||
                other.GetComponentInParent<KZFirstPersonPlayer>() == null ||
                KZStoryDirector.Current == null) return;
            BoxCollider bounds = GetComponent<BoxCollider>();
            if (bounds == null) return;
            Vector3 local = transform.InverseTransformPoint(other.transform.position) - bounds.center;
            if (local.z <= -bounds.size.z * .5f + 1f)
                KZStoryDirector.Current.AudienceExited();
        }
    }
}
