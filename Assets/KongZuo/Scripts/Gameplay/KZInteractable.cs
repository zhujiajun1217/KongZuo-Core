using UnityEngine;

namespace KongZuo
{
    public enum KZActionKind
    {
        Worksheet, FrontDoor, Phone, Seat07, Register, Program,
        Ritual, OldPhoto, Ticket, SeatPuzzle, BackstageDoor, Poster,
        WorkRecord, RelicDecision
    }

    [DisallowMultipleComponent]
    public sealed class KZInteractable : MonoBehaviour
    {
        public KZActionKind action;
        public string displayName = "调查";
        [TextArea(2, 7)] public string body = "";
        public string DisplayName { get { return string.IsNullOrWhiteSpace(displayName) ? name : displayName; } }

        public void Interact()
        {
            if (KZStoryDirector.Current == null)
            {
                KZHud.ShowToast("本场景未安装 KZStoryDirector。");
                return;
            }
            KZStoryDirector.Current.Handle(this);
        }
    }
}
