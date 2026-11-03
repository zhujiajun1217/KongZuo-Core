using UnityEngine;

namespace KongZuo.LMLWeek4
{
    public enum KZW4DemoInteraction { Ritual, Photo, WorkRecord, SeatSeven, Phone }

    [DisallowMultipleComponent]
    public sealed class KZW4DemoInteractable : MonoBehaviour
    {
        public KZW4DemoInteraction kind;
        public string displayName = "调查";
        [TextArea(3, 10)] public string contents = "";
    }
}
