using System;
using System.Collections.Generic;
using UnityEngine;

namespace KongZuo.V03
{
    /// <summary>V0.3 demo-only story facts. Flags reset on entering the prologue; no destructive save-file writes.</summary>
    [DisallowMultipleComponent]
    public sealed class KZV03State : MonoBehaviour
    {
        public static KZV03State Instance { get; private set; }
        private readonly HashSet<string> flags = new HashSet<string>(StringComparer.Ordinal);
        public event Action<string> Changed;

        public static KZV03State Ensure()
        {
            if (Instance != null) return Instance;
            KZV03State existing = FindFirstObjectByType<KZV03State>();
            if (existing != null) return existing;
            return new GameObject("KZ_V03_StoryState").AddComponent<KZV03State>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool Has(string id) { return !string.IsNullOrEmpty(id) && flags.Contains(id); }
        public bool HasAll(params string[] ids)
        {
            for (int i = 0; i < ids.Length; i++) if (!Has(ids[i])) return false;
            return true;
        }
        public void Add(string id)
        {
            if (!string.IsNullOrEmpty(id) && flags.Add(id)) Changed?.Invoke(id);
        }
        public void ResetProgress() { flags.Clear(); Changed?.Invoke(""); }
    }

    public static class KZV03Flag
    {
        public const string Worksheet="p_worksheet", Door="p_door", Phone="p_phone";
        public const string Seat="p_seat", Register="p_register", Folklore="p_folklore";
        public const string Anomaly="p_anomaly", Program="p_program";
        public const string Photo="c1_photo", Alignment="c1_alignment", Handover="c1_handover";
        public const string EvidenceLinked="c1_evidence_linked", EchoKey="c1_echo_key";
        public const string MaintenanceKey="c2_maintenance_key", WorkroomOpen="c2_workroom_open", Identity="c2_identity";
        public const string LifePhoto="c2_life_photo", Travel="c2_ticket_receipt";
        public const string Letter="c2_letter", Argument="c2_argument";
        public const string XuNote="c2_xu_note", ChestLabel="c2_chest_label";
        public const string GuSleeve="c2_gu_sleeve", MissingTicket="c2_missing_ticket";
        public const string EastDoorEcho="c2_east_door_echo", DoorRecorded="c2_door_recorded";
    }
}
