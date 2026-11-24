// KZ Program Inspect V2.5 - wider paper and real game-view blur.
// Replaces ONLY the V2.3 overlay script; no scene, prefab, pipeline or model edits.
// On the inspection frame, the original HUD is temporarily hidden so the
// screenshot contains the player's current view, not the old black archive UI.
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KongZuo
{
    [DisallowMultipleComponent]
    public sealed class KZProgramInspectOverlayV2 : MonoBehaviour
    {
        private const float FlipSeconds = 0.42f;
        private const string FrontResource = "KZProgramInspectV2/ProgramFront";
        private const string BackResource = "KZProgramInspectV2/ProgramBack";
        private static readonly BindingFlags PrivateInstance =
            BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo PageTitleField =
            typeof(KZHud).GetField("pageTitle", PrivateInstance);
        private static readonly MethodInfo ClosePageMethod =
            typeof(KZHud).GetMethod("ClosePage", PrivateInstance);

        private static KZProgramInspectOverlayV2 instance;
        private KZHud hud;
        private Texture2D front;
        private Texture2D back;
        private Texture2D blurredCurrentView;
        private float nextHudLookup;
        private bool isVisible;
        private bool captureReady;
        private float captureStartedAt;
        private int captureGeneration;
        private Coroutine captureRoutine;
        private bool showingBack;
        private bool targetBack;
        private bool flipping;
        private float flipStartedAt;
        private int openedFrame;
        private bool pausedLegacyHud;
        private CursorLockMode cursorBeforeInspect;
        private bool cursorVisibleBeforeInspect;
        private bool warnedIncompatibleHud;
        private GUIStyle hintStyle;
        private GUIStyle closeStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAutomatically()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureInstance();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;
            GameObject gameObject = new GameObject("KZ Program Inspect V2 (runtime only)");
            DontDestroyOnLoad(gameObject);
            instance = gameObject.AddComponent<KZProgramInspectOverlayV2>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            front = Resources.Load<Texture2D>(FrontResource);
            back = Resources.Load<Texture2D>(BackResource);
            if (front == null || back == null)
                Debug.LogWarning("[KZProgramInspectV2.4] ProgramFront/ProgramBack not found. Original UI is left alone.");
        }

        private void OnDisable() { EndPresentation(); }
        private void OnDestroy()
        {
            EndPresentation();
            if (instance == this) instance = null;
        }

        private static KZHud FindLiveHud()
        {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<KZHud>();
#else
            return UnityEngine.Object.FindObjectOfType<KZHud>();
#endif
        }

        private void Update()
        {
            if (hud == null || (!pausedLegacyHud && !hud.isActiveAndEnabled))
            {
                if (Time.unscaledTime >= nextHudLookup)
                {
                    hud = FindLiveHud();
                    nextHudLookup = Time.unscaledTime + 0.25f;
                }
            }

            if (PageTitleField == null || ClosePageMethod == null)
            {
                if (!warnedIncompatibleHud)
                {
                    Debug.LogWarning("[KZProgramInspectV2.4] KZHud API changed. Original inspection UI remains available.");
                    warnedIncompatibleHud = true;
                }
                if (isVisible) EndPresentation();
                return;
            }

            string title = null;
            if (hud != null && hud.gameObject.activeInHierarchy)
                title = PageTitleField.GetValue(hud) as string;

            KZStoryDirector story = KZStoryDirector.Current;
            bool showNow = story != null && story.isPrologue &&
                title != null && title.Contains("节目单") && title.Contains("新出现") &&
                !KZPhotoAlignmentPuzzle.IsAnyActive &&
                front != null && back != null &&
                (pausedLegacyHud || (hud != null && hud.isActiveAndEnabled));

            if (!showNow)
            {
                if (isVisible) EndPresentation();
                return;
            }

            if (!isVisible)
                BeginPresentation();

            if (!isVisible) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // In some Editor layouts, WaitForEndOfFrame may not run when the
            // Game tab is hidden. Do not trap the player on a blank screen.
            if (!captureReady && Time.unscaledTime - captureStartedAt > 1.5f)
            {
                captureGeneration++;
                if (captureRoutine != null) StopCoroutine(captureRoutine);
                captureRoutine = null;
                captureReady = true;
                Debug.LogWarning("[KZProgramInspectV2.4] Could not capture Game view. Showing readable paper without blur.");
            }

            // Ignore the same E key that originally picked up this program.
            if (Time.frameCount > openedFrame &&
                (KZInput.ConsumeInteract() || KZInput.ConsumeEscape()))
            {
                CloseUsingOriginalStoryCallback();
                return;
            }

            if (flipping && Time.unscaledTime - flipStartedAt >= FlipSeconds)
            {
                showingBack = targetBack;
                flipping = false;
            }
            if (!flipping && FlipKeyPressed()) BeginFlip();
        }

        private void BeginPresentation()
        {
            isVisible = true;
            captureReady = false;
            captureStartedAt = Time.unscaledTime;
            openedFrame = Time.frameCount;
            showingBack = false;
            targetBack = false;
            flipping = false;
            ReleaseBlur();
            PauseLegacyHud();

            // OnGUI intentionally draws nothing before this screenshot is
            // taken. Therefore the captured background is the real player
            // camera view with the old evidence panel removed.
            captureGeneration++;
            captureRoutine = StartCoroutine(CaptureViewAfterRendering(captureGeneration));
        }

        private IEnumerator CaptureViewAfterRendering(int generation)
        {
            yield return new WaitForEndOfFrame();
            if (!isVisible || generation != captureGeneration) yield break;

            Texture2D screenshot = null;
            try
            {
                screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                if (screenshot != null)
                    blurredCurrentView = MakeBlurredTexture(screenshot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[KZProgramInspectV2.4] Current-view blur unavailable: " + ex.Message);
            }
            finally
            {
                if (screenshot != null) Destroy(screenshot);
            }

            if (isVisible && generation == captureGeneration)
            {
                captureReady = true;
                captureRoutine = null;
            }
        }

        // Pure C# pixel processing of a one-time Game screenshot. No custom
        // shader, URP Renderer Feature, Global Volume, or RenderTexture required.
        private static Texture2D MakeBlurredTexture(Texture2D screenshot)
        {
            int sourceWidth = screenshot.width;
            int sourceHeight = screenshot.height;
            if (sourceWidth <= 0 || sourceHeight <= 0) return null;
            int width = Mathf.Min(sourceWidth, Mathf.Clamp(sourceWidth / 7, 96, 320));
            int height = Mathf.Min(sourceHeight,
                Mathf.Max(1, Mathf.RoundToInt((float)width * sourceHeight / sourceWidth)));

            Color32[] source = screenshot.GetPixels32();
            Color32[] pixelsA = new Color32[width * height];
            Color32[] pixelsB = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int sy = Mathf.Min(sourceHeight - 1,
                    Mathf.FloorToInt((y + 0.5f) * sourceHeight / height));
                for (int x = 0; x < width; x++)
                {
                    int sx = Mathf.Min(sourceWidth - 1,
                        Mathf.FloorToInt((x + 0.5f) * sourceWidth / width));
                    pixelsA[y * width + x] = source[sy * sourceWidth + sx];
                }
            }

            // Two separable Gaussian-like rounds plus bilinear upscale.
            BlurPass(pixelsA, pixelsB, width, height, true);
            BlurPass(pixelsB, pixelsA, width, height, false);
            BlurPass(pixelsA, pixelsB, width, height, true);
            BlurPass(pixelsB, pixelsA, width, height, false);

            Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.name = "KZ_CurrentViewBlur_RuntimeOnly";
            result.filterMode = FilterMode.Bilinear;
            result.wrapMode = TextureWrapMode.Clamp;
            result.SetPixels32(pixelsA);
            result.Apply(false, true);
            return result;
        }

        private static void BlurPass(Color32[] source, Color32[] destination,
            int width, int height, bool horizontal)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int r = 0, g = 0, b = 0;
                    for (int step = -2; step <= 2; step++)
                    {
                        int sx = horizontal ? Mathf.Clamp(x + step, 0, width - 1) : x;
                        int sy = horizontal ? y : Mathf.Clamp(y + step, 0, height - 1);
                        int weight = step == 0 ? 6 : (step == -1 || step == 1 ? 4 : 1);
                        Color32 c = source[sy * width + sx];
                        r += c.r * weight;
                        g += c.g * weight;
                        b += c.b * weight;
                    }
                    destination[y * width + x] = new Color32(
                        (byte)(r / 16), (byte)(g / 16), (byte)(b / 16), 255);
                }
            }
        }

        private void PauseLegacyHud()
        {
            if (pausedLegacyHud || hud == null || !hud.isActiveAndEnabled) return;
            hud.enabled = false;
            // KZHud.OnDisable already restores its own previous cursor state.
            cursorBeforeInspect = Cursor.lockState;
            cursorVisibleBeforeInspect = Cursor.visible;
            pausedLegacyHud = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ResumeLegacyHud()
        {
            if (!pausedLegacyHud) return;
            pausedLegacyHud = false;
            if (hud != null && hud.gameObject.activeInHierarchy)
                hud.enabled = true;
            Cursor.lockState = cursorBeforeInspect;
            Cursor.visible = cursorVisibleBeforeInspect;
        }

        private void ReleaseBlur()
        {
            if (blurredCurrentView != null)
            {
                Destroy(blurredCurrentView);
                blurredCurrentView = null;
            }
        }

        private void EndPresentation()
        {
            isVisible = false;
            captureReady = false;
            flipping = false;
            captureGeneration++;
            if (captureRoutine != null)
            {
                StopCoroutine(captureRoutine);
                captureRoutine = null;
            }
            ReleaseBlur();
            ResumeLegacyHud();
        }

        private void CloseUsingOriginalStoryCallback()
        {
            if (!isVisible) return;
            KZHud original = hud;
            EndPresentation();
            if (original == null || ClosePageMethod == null) return;
            try
            {
                ClosePageMethod.Invoke(original, null);
            }
            catch (TargetInvocationException ex)
            {
                Debug.LogException(ex.InnerException ?? ex);
            }
        }

        private static bool FlipKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null &&
                (keyboard.qKey.wasPressedThisFrame ||
                 keyboard.leftArrowKey.wasPressedThisFrame ||
                 keyboard.rightArrowKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Q) ||
                   Input.GetKeyDown(KeyCode.LeftArrow) ||
                   Input.GetKeyDown(KeyCode.RightArrow);
#else
            return false;
#endif
        }

        private void BeginFlip()
        {
            if (flipping) return;
            targetBack = !showingBack;
            flipStartedAt = Time.unscaledTime;
            flipping = true;
        }

        // Retained for teams that previously inserted the optional V2.2 hook.
        public static bool ShouldHideLegacyPage(string title)
        {
            return instance != null && instance.isActiveAndEnabled &&
                instance.isVisible && instance.front != null && instance.back != null &&
                !string.IsNullOrEmpty(title) &&
                title.Contains("节目单") && title.Contains("新出现");
        }

        private void MakeStyles()
        {
            if (hintStyle != null) return;
            Font font = null;
            try
            {
                font = Font.CreateDynamicFontFromOSFont(new[] {
                    "Microsoft YaHei", "SimSun", "Noto Sans CJK SC", "PingFang SC",
                    "Arial Unicode MS"
                }, 24);
            }
            catch (Exception) { }
            if (font == null) font = GUI.skin.font;

            hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.font = font;
            hintStyle.fontSize = 24;
            hintStyle.normal.textColor = new Color(0.94f, 0.84f, 0.72f);
            hintStyle.alignment = TextAnchor.MiddleRight;
            hintStyle.wordWrap = false;

            closeStyle = new GUIStyle(GUI.skin.button);
            closeStyle.font = font;
            closeStyle.fontSize = 42;
            closeStyle.alignment = TextAnchor.MiddleCenter;
            closeStyle.normal.textColor = new Color(0.94f, 0.84f, 0.72f);
            closeStyle.hover.textColor = Color.white;
            closeStyle.active.textColor = Color.white;
            closeStyle.normal.background = null;
            closeStyle.hover.background = null;
            closeStyle.active.background = null;
            closeStyle.focused.background = null;
        }

        private static void Fill(Rect area, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = previous;
        }

        private void OnGUI()
        {
            // Capture the scene on a clean frame with the old full-screen HUD
            // hidden: drawing before this point would contaminate the blur.
            if (!isVisible || !captureReady || front == null || back == null) return;
            MakeStyles();
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            int oldDepth = GUI.depth;
            GUI.matrix = Matrix4x4.identity;
            GUI.depth = -300;
            try
            {
                Rect screenArea = new Rect(0f, 0f, Screen.width, Screen.height);
                GUI.color = Color.white;
                if (blurredCurrentView != null)
                {
                    GUI.DrawTexture(screenArea, blurredCurrentView,
                        ScaleMode.StretchToFill, false);
                }
                // Keep the current game scene visible, but subtly darkened.
                Fill(screenArea, new Color(0.02f, 0.016f, 0.012f, 0.34f));

                float scale = Mathf.Clamp(Screen.height / 900f, 0.65f, 1.60f);
                float top = 24f * scale;
                float right = 30f * scale;
                float closeWidth = 48f * scale;
                Rect closeArea = new Rect(Screen.width - right - closeWidth,
                    top, closeWidth, 54f * scale);
                Rect hintArea = new Rect(closeArea.x - 158f * scale,
                    top + 7f * scale, 140f * scale, 38f * scale);
                hintStyle.fontSize = Mathf.RoundToInt(24f * scale);
                closeStyle.fontSize = Mathf.RoundToInt(42f * scale);
                GUI.Label(hintArea, "【Q】翻页", hintStyle);
                Fill(new Rect(closeArea.x - 9f * scale, top + 11f * scale,
                    1.0f * scale, 26f * scale),
                    new Color(.76f, .65f, .54f, .55f));
                if (GUI.Button(closeArea, "×", closeStyle))
                {
                    CloseUsingOriginalStoryCallback();
                    return;
                }

                // V2.5: keep the real blurred Game view, but show the program
                // with a wider 0.70:1 paper silhouette instead of the narrow
                // old texture ratio. Both front and back share this same rect.
                float originalPaperRatio =
                    (float)front.width / Mathf.Max(1, front.height);
                float paperRatio = Mathf.Max(0.70f, originalPaperRatio);
                float paperHeight = Mathf.Min(Screen.height * .86f,
                    Screen.width * .72f / paperRatio);
                float paperWidth = paperHeight * paperRatio;
                float paperTop = (Screen.height - paperHeight) * .5f;
                float t = flipping
                    ? Mathf.Clamp01((Time.unscaledTime - flipStartedAt) / FlipSeconds)
                    : 0f;
                float squash = flipping
                    ? Mathf.Max(.018f, Mathf.Abs(Mathf.Cos(t * Mathf.PI)))
                    : 1f;
                bool displayBack = flipping && t >= .5f ? targetBack : showingBack;
                Texture2D paper = displayBack ? back : front;
                float apparentWidth = paperWidth * squash;
                float left = (Screen.width - apparentWidth) * .5f;
                Rect paperArea = new Rect(left, paperTop, apparentWidth, paperHeight);

                // A soft contact shadow only: NO black frame, archive panel,
                // explanatory captions, or background stage illustration.
                float shadowOffset = Mathf.Max(3f, 7f * scale);
                Fill(new Rect(left + shadowOffset, paperTop + shadowOffset,
                    apparentWidth, paperHeight), new Color(0f, 0f, 0f, .26f));
                GUI.color = Color.white;
                GUI.DrawTexture(paperArea, paper, ScaleMode.StretchToFill, false);

                if (Event.current.type == EventType.MouseDown)
                    Event.current.Use();
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.depth = oldDepth;
            }
        }
    }
}
