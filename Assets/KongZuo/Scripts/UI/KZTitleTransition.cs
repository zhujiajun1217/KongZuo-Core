using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace KongZuo
{
    // Owns the coroutine across scene destruction, keeping the loading cut covered.
    public sealed class KZTitleTransition : MonoBehaviour
    {
        private static KZTitleTransition current;
        public static bool IsPlaying { get { return current != null; } }
        private VideoPlayer video;
        private RenderTexture target;
        private Texture2D fallback;
        private float black;
        private float stillAlpha;
        private bool showVideo;
        private bool failed;

        public static IEnumerator Play(string nextScene, Action beforeLoad)
        {
            if (IsPlaying) yield break;
            var host = new GameObject("空座 · 序章标题过渡");
            var overlay = host.AddComponent<KZTitleTransition>();
            current = overlay;
            DontDestroyOnLoad(host);
            overlay.StartCoroutine(overlay.Run(nextScene, beforeLoad));
            while (overlay != null) yield return null;
        }

        private IEnumerator Run(string nextScene, Action beforeLoad)
        {
            fallback = Resources.Load<Texture2D>("KongZuoTitle/TitleStill");
            VideoClip clip = Resources.Load<VideoClip>("KongZuoTitle/TitleReveal");
            if (clip != null)
            {
                video = gameObject.AddComponent<VideoPlayer>();
                video.playOnAwake = false;
                video.isLooping = false;
                video.audioOutputMode = VideoAudioOutputMode.None;
                video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                video.renderMode = VideoRenderMode.RenderTexture;
                target = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
                target.Create();
                video.targetTexture = target;
                video.clip = clip;
                video.waitForFirstFrame = true;
                video.errorReceived += OnVideoError;
                video.Prepare();
            }

            // Suppress the old toast/HUD through KZHud while the world gently darkens.
            yield return Fade(0f, 1f, .9f);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (video != null && !video.isPrepared && !failed && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (video != null && video.isPrepared && !failed)
            {
                video.Play();
                float start = Time.realtimeSinceStartup;
                // Give the decoder a bounded first-frame wait instead of displaying an uninitialised RT.
                while (video.frame < 0 && !failed && Time.realtimeSinceStartup - start < 1f)
                    yield return null;
                showVideo = video.frame >= 0 && !failed;
                if (showVideo)
                {
                    bool finished = false;
                    VideoPlayer.EventHandler onEnd = v => finished = true;
                    video.loopPointReached += onEnd;
                    while (!finished && !failed && Time.realtimeSinceStartup - start < 8f)
                        yield return null;
                    video.loopPointReached -= onEnd;
                }
            }
            if (!showVideo || failed)
            {
                showVideo = false;
                float elapsed = 0f;
                while (elapsed < 6f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    stillAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - .5f) / 1.7f))
                        * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - 4.5f) / 1.3f)));
                    yield return null;
                }
            }
            showVideo = false;
            stillAlpha = 0f;
            if (video != null) video.Stop();
            beforeLoad?.Invoke();
            SceneManager.LoadScene(nextScene);
            yield return null;
            yield return Fade(1f, 0f, .9f);
            Destroy(gameObject);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                black = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            black = to;
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            failed = true;
            Debug.LogWarning("[空座] 标题视频解码失败，使用定帧淡入淡出：" + message);
        }

        private void OnGUI()
        {
            GUI.depth = -10000;
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, black);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            // ScaleToFit preserves the title on both 16:9 and the recording's 16:10 viewport.
            if (showVideo && target != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), target, ScaleMode.ScaleToFit, false);
            }
            else if (fallback != null && stillAlpha > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, stillAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), fallback, ScaleMode.ScaleToFit, true);
            }
            GUI.color = old;
        }

        private void OnDestroy()
        {
            if (video != null)
            {
                video.errorReceived -= OnVideoError;
                video.Stop();
                video.targetTexture = null;
            }
            if (target != null) { target.Release(); Destroy(target); }
            if (current == this) current = null;
        }
    }
}
