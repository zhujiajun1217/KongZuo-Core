using System;
using System.Collections.Generic;
using UnityEngine;

namespace KongZuo.LMLWeek4
{
    public enum KZW4AudioCategory { Ambience, Sfx, Voice, Music }

    [CreateAssetMenu(fileName = "KZW4_AudioCatalog", menuName = "空座/LML第四周/音频资源表")]
    public sealed class KZW4AudioCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id = "";
            public KZW4AudioCategory category = KZW4AudioCategory.Sfx;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Min(0f)] public float cooldownSeconds = 0.12f;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (Entry entry in entries)
                if (entry != null && string.Equals(entry.id, id, StringComparison.Ordinal))
                    return entry;
            return null;
        }

        public bool HasDuplicateIds()
        {
            HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);
            foreach (Entry entry in entries)
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || !found.Add(entry.id))
                    return true;
            return false;
        }
    }
}
