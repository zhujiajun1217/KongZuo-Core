using UnityEngine;

namespace KongZuo.V03
{
    /// <summary>Minimal inspected prop. Its identifier maps to authored dialogue and prerequisite checks.</summary>
    [DisallowMultipleComponent]
    public sealed class KZV03Item : MonoBehaviour
    {
        public string id;
        public string displayName = "调查";
        public bool showAfterEastEcho;

        public string Label { get { return string.IsNullOrEmpty(displayName) ? gameObject.name : displayName; } }

        public void Use()
        {
            if (KZV03Director.Current != null) KZV03Director.Current.Interact(id);
        }
    }
}
