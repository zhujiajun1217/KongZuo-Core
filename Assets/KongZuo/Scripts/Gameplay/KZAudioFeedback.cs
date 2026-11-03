using UnityEngine;

namespace KongZuo
{
    public enum KZSoundCue
    {
        Paper,
        Door,
        Phone,
        Key,
        Gong,
        PuzzleSuccess,
        PuzzleFailure
    }

    /// <summary>
    /// Optional scene-local sound layer for the original main-program P0 scenes.
    /// Nothing plays unless clips have been assigned; it does not touch KZW4AudioManager.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KZAudioFeedback : MonoBehaviour
    {
        public static KZAudioFeedback Current { get; private set; }

        [Header("声音素材（可用第四周占位音频，后续替换正式音效）")]
        [SerializeField] private AudioClip ambienceLoop;
        [SerializeField] private AudioClip paper;
        [SerializeField] private AudioClip door;
        [SerializeField] private AudioClip phone;
        [SerializeField] private AudioClip key;
        [SerializeField] private AudioClip gong;
        [SerializeField] private AudioClip puzzleSuccess;
        [SerializeField] private AudioClip puzzleFailure;

        [Header("音量")]
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.28f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.72f;

        private AudioSource musicSource;
        private AudioSource sfxSource;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("[空座] 当前场景有重复的 KZAudioFeedback；已忽略后加载的组件。", this);
                enabled = false;
                return;
            }
            Current = this;

            AudioSource[] existing = GetComponents<AudioSource>();
            musicSource = existing.Length > 0 ? existing[0] : gameObject.AddComponent<AudioSource>();
            sfxSource = existing.Length > 1 ? existing[1] : gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
        }

        private void Start()
        {
            if (Current != this || musicSource == null || ambienceLoop == null) return;
            musicSource.clip = ambienceLoop;
            musicSource.volume = ambienceVolume;
            musicSource.Play();
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public static void PlayCue(KZSoundCue cue)
        {
            if (Current != null) Current.Play(cue);
        }

        public void Play(KZSoundCue cue)
        {
            if (sfxSource == null) return;
            AudioClip clip = null;
            switch (cue)
            {
                case KZSoundCue.Paper: clip = paper; break;
                case KZSoundCue.Door: clip = door; break;
                case KZSoundCue.Phone: clip = phone; break;
                case KZSoundCue.Key: clip = key; break;
                case KZSoundCue.Gong: clip = gong; break;
                case KZSoundCue.PuzzleSuccess: clip = puzzleSuccess; break;
                case KZSoundCue.PuzzleFailure: clip = puzzleFailure; break;
            }
            if (clip != null && sfxVolume > 0f) sfxSource.PlayOneShot(clip, sfxVolume);
        }
    }
}
