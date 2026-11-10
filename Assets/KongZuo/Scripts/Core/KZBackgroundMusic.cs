using UnityEngine;

namespace KongZuo
{
    public sealed class KZBackgroundMusic : MonoBehaviour
    {
        private const string ClipResourcePath = "KZBackgroundMusic";
        private static KZBackgroundMusic instance;

        private AudioSource musicSource;

        public static void EnsurePlaying()
        {
            if (instance == null)
                instance = Object.FindFirstObjectByType<KZBackgroundMusic>();

            if (instance == null || instance.musicSource.clip == null)
            {
                AudioClip clip = Resources.Load<AudioClip>(ClipResourcePath);
                if (clip == null)
                {
                    Debug.LogError("[空座] 找不到主线背景音乐资源：Resources/" +
                                   ClipResourcePath + ".mp3");
                    return;
                }

                if (instance == null)
                {
                    GameObject musicObject = new GameObject("KZ_BackgroundMusic");
                    instance = musicObject.AddComponent<KZBackgroundMusic>();
                }
                instance.musicSource.clip = clip;
            }

            if (!instance.musicSource.isPlaying)
                instance.musicSource.Play();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            musicSource.volume = 0.28f;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
