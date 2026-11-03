using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KongZuo.LMLWeek4
{
    /// <summary>
    /// Scene-local, reusable audio service; does not replace or persist over team audio.
    /// Missing optional audio clips do not prevent gameplay or subtitle output.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KZW4AudioManager : MonoBehaviour
    {
        public KZW4AudioCatalog catalog;
        [Header("Mixer-free P0 channel volumes")]
        [Range(0f, 1f)] public float masterVolume = .85f;
        [Range(0f, 1f)] public float ambienceVolume = .60f;
        [Range(0f, 1f)] public float sfxVolume = .85f;
        [Range(0f, 1f)] public float voiceVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = .55f;

        public event Action<string, float> SubtitleRequested;

        private AudioSource ambience;
        private AudioSource sfx;
        private AudioSource voice;
        private AudioSource music;
        private Coroutine ambienceFade;
        private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        private readonly HashSet<string> warnings = new HashSet<string>();
        private string ambienceId = "";
        private string requestedAmbienceId = "";

        private void Awake()
        {
            ambience = CreateChannel("Ambience", true);
            sfx = CreateChannel("SFX", false);
            voice = CreateChannel("Voice", false);
            music = CreateChannel("Music", true);
            ApplyVolumes();
        }

        private AudioSource CreateChannel(string label, bool loop)
        {
            Transform existing = transform.Find("KZW4_" + label);
            GameObject channel = existing == null ? new GameObject("KZW4_" + label) : existing.gameObject;
            channel.transform.SetParent(transform, false);
            AudioSource source = channel.GetComponent<AudioSource>();
            if (source == null) source = channel.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private KZW4AudioCatalog.Entry Lookup(string id, KZW4AudioCategory expected)
        {
            KZW4AudioCatalog.Entry entry = catalog == null ? null : catalog.Find(id);
            if (entry != null && entry.category == expected) return entry;
            if (warnings.Add("missing:" + id))
                Debug.LogWarning("[空座/LML音频] 未配置音频事件 " + id + "（" + expected + "）", this);
            return null;
        }

        private bool PassCooldown(KZW4AudioCatalog.Entry entry)
        {
            float last;
            if (lastPlayed.TryGetValue(entry.id, out last) &&
                Time.unscaledTime - last < entry.cooldownSeconds) return false;
            lastPlayed[entry.id] = Time.unscaledTime;
            return true;
        }

        public void ApplyVolumes()
        {
            if (ambience != null) ambience.volume = masterVolume * ambienceVolume *
                (catalog == null || catalog.Find(ambienceId) == null ? 1f : catalog.Find(ambienceId).volume);
            if (sfx != null) sfx.volume = 1f;
            if (voice != null) voice.volume = masterVolume * voiceVolume;
            if (music != null)
            {
                KZW4AudioCatalog.Entry entry = catalog == null ? null : catalog.Find("music.default");
                music.volume = masterVolume * musicVolume * (entry == null ? 1f : entry.volume);
            }
        }

        public void PlaySfx(string id)
        {
            KZW4AudioCatalog.Entry entry = Lookup(id, KZW4AudioCategory.Sfx);
            if (entry == null || !PassCooldown(entry)) return;
            if (entry.clip != null && sfx != null)
                sfx.PlayOneShot(entry.clip, Mathf.Clamp01(entry.volume * masterVolume * sfxVolume));
        }

        public void PlaySfx3D(string id, Vector3 position)
        {
            KZW4AudioCatalog.Entry entry = Lookup(id, KZW4AudioCategory.Sfx);
            if (entry == null || !PassCooldown(entry) || entry.clip == null) return;
            GameObject oneShot = new GameObject("KZW4_OneShot_" + id);
            oneShot.transform.position = position;
            AudioSource source = oneShot.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.maxDistance = 18f;
            source.volume = Mathf.Clamp01(entry.volume * masterVolume * sfxVolume);
            source.clip = entry.clip;
            source.Play();
            Destroy(oneShot, entry.clip.length + .25f);
        }

        public void PlayVoice(string id, string subtitle, float subtitleSeconds = 5f)
        {
            // Always produce captions even when a voice actor has not supplied a clip.
            if (SubtitleRequested != null && !string.IsNullOrEmpty(subtitle))
                SubtitleRequested(subtitle, Mathf.Max(1f, subtitleSeconds));
            KZW4AudioCatalog.Entry entry = Lookup(id, KZW4AudioCategory.Voice);
            if (entry == null || !PassCooldown(entry) || voice == null) return;
            if (entry.clip != null)
            {
                voice.Stop();
                voice.clip = entry.clip;
                voice.volume = Mathf.Clamp01(entry.volume * masterVolume * voiceVolume);
                voice.Play();
            }
        }

        public void PlayMusic(string id)
        {
            KZW4AudioCatalog.Entry entry = Lookup(id, KZW4AudioCategory.Music);
            if (music == null) return;
            music.Stop();
            music.clip = entry == null ? null : entry.clip;
            music.volume = entry == null ? 0f : Mathf.Clamp01(entry.volume * masterVolume * musicVolume);
            if (music.clip != null) music.Play();
        }

        public void SwitchAmbience(string id, float fadeSeconds = .65f)
        {
            if (ambience == null || string.IsNullOrEmpty(id) || id == requestedAmbienceId) return;
            KZW4AudioCatalog.Entry entry = Lookup(id, KZW4AudioCategory.Ambience);
            requestedAmbienceId = id;
            if (ambienceFade != null) StopCoroutine(ambienceFade);
            ambienceFade = StartCoroutine(ChangeAmbience(entry, id, Mathf.Max(.02f, fadeSeconds)));
        }

        private IEnumerator ChangeAmbience(KZW4AudioCatalog.Entry next, string id, float seconds)
        {
            float start = ambience.volume;
            float elapsed = 0f;
            float half = seconds * .5f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                ambience.volume = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / half));
                yield return null;
            }

            ambience.Stop();
            ambienceId = id;
            ambience.clip = next == null ? null : next.clip;
            ambience.loop = true;
            if (ambience.clip != null) ambience.Play();

            float destination = masterVolume * ambienceVolume * (next == null ? 1f : next.volume);
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                ambience.volume = Mathf.Lerp(0f, destination, Mathf.Clamp01(elapsed / half));
                yield return null;
            }
            ambience.volume = destination;
            ambienceFade = null;
        }

        private void OnDisable()
        {
            if (ambienceFade != null) { StopCoroutine(ambienceFade); ambienceFade = null; }
            if (ambience != null) ambience.Stop();
            if (voice != null) voice.Stop();
            if (music != null) music.Stop();
        }
    }
}
