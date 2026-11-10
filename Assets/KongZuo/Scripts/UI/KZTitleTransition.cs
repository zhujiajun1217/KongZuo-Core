using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace KongZuo
{
    // Blender and the live camera share a baked path. There is no full-screen black cut.
    public sealed class KZTitleTransition : MonoBehaviour
    {
        [Serializable] private sealed class CameraFrame
        {
            public int frame;
            public float[] position;
            public float[] rotation;
            public float fov = 60f;
            public float titleOpacity;
            public Vector3 Position => new Vector3(position[0], position[1], position[2]);
            public Quaternion Rotation => new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
        }
        [Serializable] private sealed class CameraPath
        {
            public int fps = 30;
            public int frames = 240;
            public bool useRenderedMovie = false;
            public CameraFrame[] poses;
        }

        private static KZTitleTransition current;
        public static bool IsPlaying => current != null;
        private Camera view;
        private KZFirstPersonPlayer player;
        private VideoPlayer video;
        private RenderTexture target;
        private Texture2D titleOverlay;
        private CameraPath path;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private float originalFov;
        private float originalAspect;
        private Rect originalRect;
        private Rect gate;
        private float movieAlpha;
        private float titleAlpha;
        private float sampledTitleAlpha;
        private float handoffTime;
        private bool failed;
        private bool showVideo;
        private bool finished;

        public static IEnumerator Play(string nextScene, Action beforeLoad)
        {
            if (IsPlaying) yield break;
            var host = new GameObject("空座 · 戏楼连续标题镜头");
            var overlay = host.AddComponent<KZTitleTransition>();
            current = overlay;
            DontDestroyOnLoad(host);
            overlay.StartCoroutine(overlay.Run(nextScene, beforeLoad));
            while (overlay != null) yield return null;
        }

        private void FindView()
        {
            player = KZStoryDirector.Current == null ? null : KZStoryDirector.Current.player;
            if (player == null) player = FindFirstObjectByType<KZFirstPersonPlayer>();
            view = player == null ? Camera.main : player.GetComponentInChildren<Camera>(true);
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static Rect FilmGate()
        {
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            const float movieAspect = 16f / 9f;
            if (aspect > movieAspect)
            {
                float width = movieAspect / aspect;
                return new Rect((1f - width) * .5f, 0f, width, 1f);
            }
            float height = aspect / movieAspect;
            return new Rect(0f, (1f - height) * .5f, 1f, height);
        }

        private static Rect BlendRect(Rect a, Rect b, float t)
        {
            return new Rect(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t),
                Mathf.Lerp(a.width, b.width, t), Mathf.Lerp(a.height, b.height, t));
        }

        private void SetGate(Rect rectangle)
        {
            gate = rectangle;
            if (view == null) return;
            view.rect = gate;
            view.aspect = Screen.width * gate.width / Mathf.Max(1f, Screen.height * gate.height);
        }

        private void Pose(float seconds)
        {
            if (view == null || path == null || path.poses == null || path.poses.Length == 0) return;
            float sample = Mathf.Clamp(seconds * path.fps, 0f, path.poses.Length - 1);
            int index = Mathf.FloorToInt(sample);
            CameraFrame a = path.poses[index];
            CameraFrame b = path.poses[Mathf.Min(index + 1, path.poses.Length - 1)];
            float t = sample - index;
            view.transform.SetPositionAndRotation(Vector3.Lerp(a.Position, b.Position, t),
                Quaternion.Slerp(a.Rotation, b.Rotation, t));
            view.fieldOfView = Mathf.Lerp(a.fov, b.fov, t);
            sampledTitleAlpha = Mathf.Lerp(a.titleOpacity, b.titleOpacity, t);
            SetGate(FilmGate());
        }

        private IEnumerator Run(string nextScene, Action beforeLoad)
        {
            FindView();
            if (view == null)
            {
                beforeLoad?.Invoke();
                SceneManager.LoadScene(nextScene);
                Destroy(gameObject);
                yield break;
            }
            originalPosition = view.transform.position;
            originalRotation = view.transform.rotation;
            originalFov = view.fieldOfView;
            originalAspect = view.aspect;
            originalRect = view.rect;
            gate = originalRect;
            TextAsset cameraData = Resources.Load<TextAsset>("KongZuoTitle/TitleCamera");
            if (cameraData != null) path = JsonUtility.FromJson<CameraPath>(cameraData.text);
            titleOverlay = Resources.Load<Texture2D>("KongZuoTitle/TitleOverlay");
            // Default: replay Blender's authored animation in the actual live theatre.
            // The rendered movie remains available as a preview and an optional cinematic mode.
            VideoClip clip = path != null && path.useRenderedMovie
                ? Resources.Load<VideoClip>("KongZuoTitle/TitleReveal") : null;
            if (clip != null && path != null && path.poses != null && path.poses.Length > 0)
            {
                video = gameObject.AddComponent<VideoPlayer>();
                video.playOnAwake = false;
                video.isLooping = false;
                video.audioOutputMode = VideoAudioOutputMode.None;
                video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                video.renderMode = VideoRenderMode.RenderTexture;
                video.waitForFirstFrame = true;
                video.skipOnDrop = true;
                target = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
                target.Create();
                video.targetTexture = target;
                video.clip = clip;
                video.errorReceived += OnVideoError;
                video.Prepare();
            }

            // Keep the live theatre on screen while aligning with Blender's first frame.
            if (path != null && path.poses != null && path.poses.Length > 0)
            {
                CameraFrame first = path.poses[0];
                float elapsed = 0f;
                while (elapsed < 1.8f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Ease(elapsed / 1.8f);
                    view.transform.SetPositionAndRotation(Vector3.Lerp(originalPosition, first.Position, t),
                        Quaternion.Slerp(originalRotation, first.Rotation, t));
                    view.fieldOfView = Mathf.Lerp(originalFov, first.fov, t);
                    SetGate(BlendRect(originalRect, FilmGate(), t));
                    yield return null;
                }
                Pose(0f);
            }

            float deadline = Time.realtimeSinceStartup + 1f;
            while (video != null && !video.isPrepared && !failed && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (video != null && video.isPrepared && !failed)
            {
                video.Play();
                deadline = Time.realtimeSinceStartup + 1f;
                while (video.frame < 0 && !failed && Time.realtimeSinceStartup < deadline) yield return null;
                showVideo = video.frame >= 0 && !failed;
            }
            float duration = path == null ? 8f : path.frames / (float)Mathf.Max(1, path.fps);
            float started = Time.realtimeSinceStartup;
            bool loaded = false;
            while (Time.realtimeSinceStartup - started < duration)
            {
                float elapsed = Time.realtimeSinceStartup - started;
                if (failed) showVideo = false;
                // Live geometry follows the same path underneath the dissolves.
                float poseTime = showVideo && video != null ? Mathf.Clamp((float)video.time, 0f, duration) : elapsed;
                Pose(poseTime);
                movieAlpha = Ease(elapsed / .85f) * (1f - Ease((elapsed - (duration - .85f)) / .85f));
                titleAlpha = showVideo ? 0f : sampledTitleAlpha;

                // Change chapters behind the fully visible theatre render, before dissolving back.
                if (!loaded && elapsed >= duration - 1.7f)
                {
                    loaded = true;
                    beforeLoad?.Invoke();
                    handoffTime = poseTime;
                    SceneManager.sceneLoaded += OnChapterLoaded;
                    SceneManager.LoadScene(nextScene);
                    yield return null; // Allow the chapter director to consume its saved player pose.
                    SceneManager.sceneLoaded -= OnChapterLoaded;
                    FindView();
                    if (player != null) player.TeleportView(originalPosition, originalRotation);
                    Pose(poseTime);
                }
                yield return null;
            }
            movieAlpha = 0f;
            titleAlpha = 0f;
            showVideo = false;
            if (video != null) video.Stop();
            if (!loaded)
            {
                beforeLoad?.Invoke();
                SceneManager.LoadScene(nextScene);
                yield return null;
                FindView();
                if (player != null) player.TeleportView(originalPosition, originalRotation);
            }

            // Return from the cinematic to the player's original view without a cut or pitch snap.
            if (view != null)
            {
                Vector3 from = view.transform.position;
                Quaternion rotation = view.transform.rotation;
                float fov = view.fieldOfView;
                float elapsed = 0f;
                while (elapsed < 1.4f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Ease(elapsed / 1.4f);
                    view.transform.SetPositionAndRotation(Vector3.Lerp(from, originalPosition, t),
                        Quaternion.Slerp(rotation, originalRotation, t));
                    view.fieldOfView = Mathf.Lerp(fov, originalFov, t);
                    SetGate(BlendRect(FilmGate(), originalRect, t));
                    yield return null;
                }
                view.transform.SetPositionAndRotation(originalPosition, originalRotation);
                view.fieldOfView = originalFov;
                view.rect = originalRect;
                view.aspect = originalAspect;
            }
            // Restore the body and its camera pivot before player input/gravity resumes.
            // Moving the camera alone leaves the cinematic's local offset on the player.
            if (player != null) player.TeleportView(originalPosition, originalRotation);
            finished = true;
            Destroy(gameObject);
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            failed = true;
            Debug.LogWarning("[空座] 标题视频不可用，改为实时戏楼镜头与透明标题：" + message);
        }

        private void OnChapterLoaded(Scene scene, LoadSceneMode mode)
        {
            // Apply the cinematic pose before the new scene can render its first frame.
            FindView();
            Pose(handoffTime);
        }

        private void OnGUI()
        {
            GUI.depth = -10000;
            Color old = GUI.color;
            // Only mask the aspect-ratio margins. The theatre image is never covered by black.
            Rect image = new Rect(gate.x * Screen.width, (1f - gate.yMax) * Screen.height,
                gate.width * Screen.width, gate.height * Screen.height);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, image.y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, image.yMax, Screen.width, Screen.height - image.yMax), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, image.y, image.x, image.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(image.xMax, image.y, Screen.width - image.xMax, image.height), Texture2D.whiteTexture);
            if (showVideo && target != null && movieAlpha > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, movieAlpha);
                GUI.DrawTexture(image, target, ScaleMode.StretchToFill, true);
            }
            else if (titleOverlay != null && titleAlpha > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, titleAlpha);
                GUI.DrawTexture(image, titleOverlay, ScaleMode.StretchToFill, true);
            }
            GUI.color = old;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnChapterLoaded;
            if (view != null)
            {
                if (!finished)
                {
                    if (player != null) player.TeleportView(originalPosition, originalRotation);
                    else view.transform.SetPositionAndRotation(originalPosition, originalRotation);
                }
                view.rect = originalRect;
                view.aspect = originalAspect;
                view.fieldOfView = originalFov;
            }
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
