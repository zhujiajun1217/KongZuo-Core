using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KongZuo
{
    // UI V7: real-time Unity HUD / subtitle polish. Story and interactions remain live.
    [DisallowMultipleComponent]
    public sealed class KZHud : MonoBehaviour
    {
        public string chapterName = "《空座》";
        private static KZHud current;
        private static string hint = "";

        private string toast = "";
        private string pageTitle;
        private string pageBody;
        private Action pageClosed;
        private bool journalOpen;
        // Preserve the shipped chapter-one seating puzzle. Older V10/V11 HUD builds
        // accidentally omitted this public runtime API and could not compile.
        private bool puzzleOpen;
        private readonly KZSeatPuzzle puzzle = new KZSeatPuzzle();
        private Func<bool> cluesKnown;
        private Action puzzleSolved;
        private string puzzleFeedback = "";
        private int puzzleOpenedFrame = -1;
        private bool relicOpen;
        private int relicChoice;
        private Action relicSolved;
        private string relicFeedback = "";
        private int relicOpenedFrame = -1;
        private Vector2 scroll;
        private float toastUntil;
        private float chapterToastStarted;
        private int pageOpenedFrame = -1;

        // Camera-driven transition is a modal cinematic. The team-made camera,
        // video and audio scripts remain untouched.
        public static bool IsModal
        {
            get { return KZTitleTransition.IsPlaying || (current != null &&
                (current.pageTitle != null || current.journalOpen ||
                 current.puzzleOpen || current.relicOpen || current.showcaseOpen)); }
        }

        private float hudBornAt;
        private float modalOpenedAt;
        private void Awake() { current = this; hudBornAt = Time.unscaledTime; }
        private void OnDisable() { RestoreCursor(); }
        private void OnDestroy()
        {
            RestoreCursor();
            if (current == this) { current = null; hint = ""; }
        }

        public static void SetHint(string value)
        {
            string next = value ?? "";
            if (hint != next) hint = next;
        }

        public static void ShowToast(string text, float seconds = 5f)
        {
            if (current == null) { Debug.Log("[空座] " + text); return; }
            current.toast = text ?? "";
            current.chapterToastStarted = Time.unscaledTime;
            current.toastUntil = Time.unscaledTime + Mathf.Max(0f, seconds);
        }

        public static void ShowPage(string title, string body, Action onClose = null)
        {
            if (current == null)
            {
                Debug.Log("[空座] " + title + ": " + body);
                if (onClose != null) onClose();
                return;
            }
            current.journalOpen = false;
            current.puzzleOpen = false;
            current.relicOpen = false;
            current.pageTitle = title ?? "资料检视";
            current.pageBody = body ?? "";
            current.scroll = Vector2.zero;
            current.evidenceScroll = Vector2.zero;
            current.evidenceTab = 0;
            current.documentExpanded = false;
            current.pageClosed = onClose;
            current.pageOpenedFrame = Time.frameCount;
            current.modalOpenedAt = Time.unscaledTime;
        }

        // Public API called by KZStoryDirector in the original project.
        public static void ShowPuzzle(Func<bool> isReady, Action onSolved)
        {
            if (current == null) { Debug.LogWarning("缺少 KZHud。"); return; }
            current.journalOpen = false;
            current.pageTitle = null;
            current.pageBody = null;
            current.pageClosed = null;
            current.puzzleOpen = true;
            current.relicOpen = false;
            current.puzzleOpenedFrame = Time.frameCount;
            current.modalOpenedAt = Time.unscaledTime;
            current.cluesKnown = isReady;
            current.puzzleSolved = onSolved;
            current.puzzleFeedback = "根据宣传册、合照与票根填写座次。";
        }

        public static void ShowRelicChoice(Action onSolved)
        {
            if (current == null) { Debug.LogWarning("缺少 KZHud。"); return; }
            current.pageTitle = null;
            current.journalOpen = false;
            current.puzzleOpen = false;
            current.relicOpen = true;
            current.modalOpenedAt = Time.unscaledTime;
            current.relicChoice = 0;
            current.relicSolved = onSolved;
            current.relicFeedback = "根据场务记录与旧照片，判断七号座留下的东西。";
            current.relicOpenedFrame = Time.frameCount;
        }

        private void ClosePage()
        {
            Action callback = pageClosed;
            pageTitle = null;
            pageBody = null;
            pageClosed = null;
            if (callback != null) callback();
        }

        // F9 is deliberately a runtime key: it works in the actual Game window,
        // including builds, without any Unity Editor menu or scene modification.
        private static bool IsShowcaseKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.f9Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F9);
#else
            return false;
#endif
        }

        private void Update()
        {
            // Never steal controls or draw the old HUD on top of the team's title reel.
            if (KZTitleTransition.IsPlaying || KZPhotoAlignmentPuzzle.IsAnyActive) return;
            if (enableShowcaseHotkey && IsShowcaseKeyPressed())
            {
                if (showcaseOpen) showcaseOpen = false;
                else if (pageTitle == null && !journalOpen && !relicOpen)
                {
                    showcaseTab = 0;
                    showcaseOpen = true;
                }
                return;
            }
            if (showcaseOpen)
            {
                if (KZInput.ConsumeEscape()) showcaseOpen = false;
                return;
            }
            if (pageTitle != null)
            {
                if (Time.frameCount != pageOpenedFrame &&
                    (KZInput.ConsumeInteract() || KZInput.ConsumeEscape()))
                    ClosePage();
                return;
            }

            if (puzzleOpen)
            {
                if (Time.frameCount == puzzleOpenedFrame) return;
                if (KZInput.ConsumeInteract() || KZInput.ConsumeEscape())
                {
                    puzzleOpen = false;
                    return;
                }
                if (KZInput.Number(1))
                    puzzleFeedback = "一排七座必须留空，不能安排观众。";
                if (KZInput.Number(0)) puzzle.Cycle(0);
                if (KZInput.Number(2)) puzzle.Cycle(2);
                if (KZInput.Confirm()) SubmitSeatPuzzle();
                return;
            }

            if (relicOpen)
            {
                if (Time.frameCount == relicOpenedFrame) return;
                if (KZInput.ConsumeInteract() || KZInput.ConsumeEscape())
                {
                    relicOpen = false;
                    return;
                }
                for (int choice = 0; choice < 3; choice++)
                    if (KZInput.Number(choice)) relicChoice = choice + 1;
                if (KZInput.Confirm()) SubmitRelic();
                return;
            }

            if (journalOpen)
            {
                // Handle closing first; never let Escape also unlock player cursor.
                if (KZInput.Journal() || KZInput.ConsumeInteract() || KZInput.ConsumeEscape())
                    journalOpen = false;
                return;
            }

            if (KZInput.Journal())
            {
                journalOpen = true;
                modalOpenedAt = Time.unscaledTime;
                KZAudioFeedback.PlayCue(KZSoundCue.Paper);
            }
        }

        // -------------------------------------------------------------------------
        // Presentation V6: context-sensitive Game-view UI, NOT a screenshot layer.
        // The story/input/public API above stays exactly compatible with the prototype.
        // PNG resources are editable: PresentationV6/Resources/KZUIV6.
        // -------------------------------------------------------------------------
        private static readonly Color Brass = new Color(0.83f, .68f, .46f, 1f);
        private static readonly Color PaleGold = new Color(.97f, .85f, .65f, 1f);
        private static readonly Color OldPaperInk = new Color(.26f, .16f, .12f, 1f);
        private static readonly Color GreyGold = new Color(.78f, .69f, .58f, 1f);
        private static readonly Color DimBackdrop = new Color(.025f, .025f, .025f, .70f);
        private const float ReferenceWidth = 1648f;
        private const float ReferenceHeight = 928f;

        [Header("《空座》游戏内 UI 视觉框架 V6 · 旧纸资料与笔记")]
        public bool showObjectiveHud = true;
        public bool showControlsHint = false;

        private Texture2D uiDark, uiHud, uiSubtitle, uiPrompt, uiRibbon;
        private Texture2D uiBadge, uiBadgeActive, uiPaper, uiProgram, uiChapter;
        private Texture2D uiTabIdle, uiTabOn, uiVelvet, uiFlower, uiRose;
        // V5 independent textured widgets: paper is completely free of ruled lines.
        private Texture2D uiReferenceCover, uiNotePaper, uiArchiveBook;
        private Texture2D uiMenuActive, uiMenuIdle, uiReferenceTabOn, uiReferenceTabIdle, uiNib;
        private Texture2D uiArchiveNotebook;
        // V11: Two plain, unbound sheets; no binder, clip, rope or floral decorations.
        private Texture2D uiChapterEndPlainV11, uiSpeakerPlainV11;
        private Texture2D uiObjectiveV7, uiSpeakerPaperV7;
        private GUIStyle uiTitle, uiHeading, uiBody, uiMuted, uiSmall, uiCenter, uiSmallCenter;
        private GUIStyle uiPaperTitle, uiPaperText, uiPaperSmall, uiOption;
        private GUIStyle uiKey, uiChapterSub, uiChapterMain;
        private GUIStyle uiArchiveLabel, uiMenuSelected, uiMenuNormal, uiArchiveHeading, uiArchiveBody, uiArchiveFooter;
        private GUIStyle uiJournalTabSelected, uiJournalTabNormal;
        private GUIStyle uiHudTitleV7, uiHudYearV7, uiHudTaskV7, uiHudChapterV7;
        private GUIStyle uiSpeakerV7, uiSubtitleV7, uiContinueV7;
        private Font uiFont;
        private bool uiLoaded;
        [Tooltip("在实际 Game 视图按 F9 打开 UI 验收模式；正常游玩时不会出现展示工具条")]
        public bool enableShowcaseHotkey = false;
        private bool showcaseOpen;
        private int showcaseTab;
        private bool cursorOwned;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private int journalTab;
        private int evidenceTab;
        private bool documentExpanded;
        private Vector2 evidenceScroll;
        private Vector2 journalScroll;
        private float previewHintUntil;
        private string previewHint;

        private void PrepareStyles()
        {
            if (uiTitle != null) return;
            if (!uiLoaded)
            {
                uiLoaded = true;
                uiDark = Resources.Load<Texture2D>("KZUIV6/dark_frame");
                uiHud = Resources.Load<Texture2D>("KZUIV6/hud_frame");
                uiSubtitle = Resources.Load<Texture2D>("KZUIV6/subtitle_frame");
                uiPrompt = Resources.Load<Texture2D>("KZUIV6/prompt_frame");
                uiRibbon = Resources.Load<Texture2D>("KZUIV6/ribbon");
                uiBadge = Resources.Load<Texture2D>("KZUIV6/badge");
                uiBadgeActive = Resources.Load<Texture2D>("KZUIV6/badge_active");
                uiPaper = Resources.Load<Texture2D>("KZUIV6/paper_sheet");
                uiProgram = Resources.Load<Texture2D>("KZUIV6/program_cover");
                uiChapter = Resources.Load<Texture2D>("KZUIV6/chapter_plate");
                uiTabIdle = Resources.Load<Texture2D>("KZUIV6/tab_idle");
                uiTabOn = Resources.Load<Texture2D>("KZUIV6/tab_selected");
                uiVelvet = Resources.Load<Texture2D>("KZUIV6/velvet");
                uiFlower = Resources.Load<Texture2D>("KZUIV6/floral_corner");
                uiRose = Resources.Load<Texture2D>("KZUIV6/rose_cameo");
                uiReferenceCover = Resources.Load<Texture2D>("KZUIV6/reference_program_cover");
                uiNotePaper = Resources.Load<Texture2D>("KZUIV6/parchment_unruled");
                uiArchiveBook = Resources.Load<Texture2D>("KZUIV6/archive_book_icon");
                uiMenuActive = Resources.Load<Texture2D>("KZUIV6/menu_active");
                uiMenuIdle = Resources.Load<Texture2D>("KZUIV6/menu_idle");
                uiReferenceTabOn = Resources.Load<Texture2D>("KZUIV6/tab_reference_selected");
                uiReferenceTabIdle = Resources.Load<Texture2D>("KZUIV6/tab_reference_idle");
                uiNib = Resources.Load<Texture2D>("KZUIV6/pen_nib");
                uiArchiveNotebook = Resources.Load<Texture2D>("KZUIV6/dossier_notebook_blank");
                uiObjectiveV7 = Resources.Load<Texture2D>("KZUIV7/objective_frame");
                uiSpeakerPaperV7 = Resources.Load<Texture2D>("KZUIV7/speaker_torn_paper");
                uiChapterEndPlainV11 = Resources.Load<Texture2D>("KZUIV11/chapter_end_plain_burnt");
                uiSpeakerPlainV11 = Resources.Load<Texture2D>("KZUIV11/speaker_plain_burnt");
                if (uiChapterEndPlainV11 == null || uiSpeakerPlainV11 == null)
                    Debug.LogWarning("[空座 UI V11] 缺少素纸资源；请完整导入 V11 增量包。");
                if (uiObjectiveV7 == null || uiSpeakerPaperV7 == null)
                    Debug.LogWarning("[KZ UI V7] Missing PresentationV7 textures; legacy decorations will be used.");
                if (uiDark == null || uiPaper == null || uiArchiveNotebook == null)
                    Debug.LogWarning("[空座/UI V6] 请安装 PresentationV6/Resources/KZUIV6 下的所有纹理。");
            }
            try
            {
                uiFont = Font.CreateDynamicFontFromOSFont(new string[]
                {
                    "KaiTi", "STKaiti", "FangSong", "SimSun", "STSong",
                    "Noto Serif CJK SC", "Microsoft YaHei", "PingFang SC", "Arial Unicode MS"
                }, 26);
            }
            catch (Exception) { uiFont = null; }
            if (uiFont == null) uiFont = GUI.skin.font;

            uiTitle = MakeStyle(30, PaleGold, FontStyle.Bold);
            uiHeading = MakeStyle(25, Brass, FontStyle.Bold);
            uiBody = MakeStyle(24, new Color(.96f, .87f, .71f), FontStyle.Normal);
            uiMuted = MakeStyle(21, GreyGold, FontStyle.Normal);
            uiSmall = MakeStyle(18, Brass, FontStyle.Normal);
            uiCenter = MakeStyle(24, PaleGold, FontStyle.Normal);
            uiCenter.alignment = TextAnchor.MiddleCenter;
            uiSmallCenter = MakeStyle(18, PaleGold, FontStyle.Normal);
            uiSmallCenter.alignment = TextAnchor.MiddleCenter;
            uiPaperTitle = MakeStyle(34, OldPaperInk, FontStyle.Bold);
            uiPaperTitle.alignment = TextAnchor.MiddleCenter;
            uiPaperText = MakeStyle(31, new Color(.27f,.16f,.12f), FontStyle.Normal);
            uiPaperText.wordWrap = true;
            uiPaperSmall = MakeStyle(22, new Color(.34f, .22f, .15f), FontStyle.Normal);
            uiOption = MakeStyle(26, PaleGold, FontStyle.Normal);
            uiOption.alignment = TextAnchor.MiddleCenter;
            uiKey = MakeStyle(30, PaleGold, FontStyle.Bold);
            uiKey.alignment = TextAnchor.MiddleCenter;
            uiChapterSub = MakeStyle(28, Brass, FontStyle.Normal);
            uiChapterSub.alignment = TextAnchor.MiddleCenter;
            uiChapterMain = MakeStyle(105, PaleGold, FontStyle.Bold);
            uiChapterMain.alignment = TextAnchor.MiddleCenter;

            // V6: readable on-screen type at 16:9, independent from global HUD styles.
            uiArchiveLabel = MakeStyle(42, PaleGold, FontStyle.Bold);
            uiArchiveLabel.alignment = TextAnchor.MiddleLeft;
            uiMenuSelected = MakeStyle(35, OldPaperInk, FontStyle.Bold);
            uiMenuNormal = MakeStyle(34, new Color(.95f,.81f,.59f,1f), FontStyle.Bold);
            uiMenuSelected.alignment = uiMenuNormal.alignment = TextAnchor.MiddleLeft;
            uiMenuSelected.wordWrap = uiMenuNormal.wordWrap = false;
            uiMenuSelected.clipping = uiMenuNormal.clipping = TextClipping.Clip;
            uiArchiveHeading = MakeStyle(40, OldPaperInk, FontStyle.Bold);
            uiArchiveBody = MakeStyle(32, new Color(.24f,.14f,.105f,1f), FontStyle.Normal);
            uiArchiveBody.wordWrap = true;
            uiArchiveHeading.alignment = TextAnchor.MiddleCenter;
            uiArchiveHeading.wordWrap = true;
            uiArchiveFooter = MakeStyle(24, new Color(.32f,.17f,.11f), FontStyle.Normal);
            uiArchiveFooter.alignment = TextAnchor.MiddleCenter;
            uiJournalTabSelected = MakeStyle(35, OldPaperInk, FontStyle.Bold);
            uiJournalTabNormal = MakeStyle(33, PaleGold, FontStyle.Bold);
            uiJournalTabSelected.alignment = uiJournalTabNormal.alignment = TextAnchor.MiddleCenter;
            uiPaperText.fontSize = 30;

            // V7 dedicated styles. Styles use middle alignment, so text centers on its
            // actual label rect instead of relying on pixel-specific top baselines.
            uiHudTitleV7 = MakeStyle(32, PaleGold, FontStyle.Bold);
            uiHudTitleV7.alignment = TextAnchor.MiddleCenter;
            uiHudTitleV7.wordWrap = false;
            uiHudYearV7 = MakeStyle(30, PaleGold, FontStyle.Bold);
            uiHudYearV7.alignment = TextAnchor.MiddleCenter;
            uiHudTaskV7 = MakeStyle(29, new Color(.99f,.89f,.74f,1f), FontStyle.Bold);
            uiHudTaskV7.alignment = TextAnchor.MiddleLeft;
            uiHudTaskV7.wordWrap = false;
            uiHudChapterV7 = MakeStyle(22, new Color(.93f,.76f,.52f,1f), FontStyle.Bold);
            uiHudChapterV7.alignment = TextAnchor.MiddleLeft;
            uiSpeakerV7 = MakeStyle(50, new Color(.205f,.105f,.076f,1f), FontStyle.Bold);
            uiSpeakerV7.alignment = TextAnchor.MiddleCenter;
            uiSpeakerV7.wordWrap = false;
            uiSubtitleV7 = MakeStyle(41, new Color(.99f,.89f,.73f,1f), FontStyle.Bold);
            uiSubtitleV7.alignment = TextAnchor.MiddleLeft;
            uiSubtitleV7.wordWrap = true;
            uiContinueV7 = MakeStyle(30, PaleGold, FontStyle.Bold);
            uiContinueV7.alignment = TextAnchor.MiddleCenter;
        }

        private GUIStyle MakeStyle(int fontSize, Color color, FontStyle face)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.font = uiFont;
            style.fontSize = fontSize;
            style.fontStyle = face;
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.alignment = TextAnchor.UpperLeft;
            style.wordWrap = true;
            style.richText = false;
            style.padding = new RectOffset(0, 0, 0, 0);
            return style;
        }

        // Source textures have a 45–55 pixel corner region; preserve the ornaments.
        private static void NineSlice(Texture2D texture, Rect r, float sourceBorder, float targetBorder)
        {
            if (texture == null)
            {
                FillRect(r, new Color(.07f,.066f,.06f,.95f));
                FrameLine(r, Brass, 1.2f);
                return;
            }
            targetBorder = Mathf.Min(targetBorder, Mathf.Min(r.width, r.height) * .42f);
            float u = sourceBorder / texture.width;
            float v = sourceBorder / texture.height;
            float[] xs = {r.x, r.x + targetBorder, r.xMax - targetBorder, r.xMax};
            float[] ys = {r.y, r.y + targetBorder, r.yMax - targetBorder, r.yMax};
            float[] us = {0f, u, 1f - u, 1f};
            float[] vs = {0f, v, 1f - v, 1f};
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    Rect dst = new Rect(xs[col], ys[row], xs[col+1]-xs[col], ys[row+1]-ys[row]);
                    if (dst.width <= 0 || dst.height <= 0) continue;
                    // Source V axis is bottom-origin; row zero is the TOP of the source.
                    Rect uv = new Rect(us[col], vs[2-row], us[col+1]-us[col], vs[3-row]-vs[2-row]);
                    GUI.DrawTextureWithTexCoords(dst, texture, uv, true);
                }
        }

        private static void Image(Rect r, Texture2D t, Color? tint = null)
        {
            if (t == null) { FillRect(r, new Color(.10f,.08f,.065f,.82f)); return; }
            Color old = GUI.color;
            GUI.color = tint ?? Color.white;
            GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        private static void FillRect(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void FrameLine(Rect r, Color color, float thickness)
        {
            FillRect(new Rect(r.x,r.y,r.width,thickness),color);
            FillRect(new Rect(r.x,r.yMax-thickness,r.width,thickness),color);
            FillRect(new Rect(r.x,r.y,thickness,r.height),color);
            FillRect(new Rect(r.xMax-thickness,r.y,thickness,r.height),color);
        }

        private static void GoldRule(float x, float y, float w)
        {
            FillRect(new Rect(x,y,w,1.3f),new Color(.66f,.45f,.24f,.72f));
            FillRect(new Rect(x+w*.50f-3.5f,y-3.5f,7,7),new Color(.82f,.64f,.39f,.86f));
        }

        private static bool InvisibleButton(Rect rect)
        {
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private static string CleanHint(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            return raw.Replace("[E]", "").Replace("按 E", "").Trim(' ', '·', '：', ':');
        }

        private string YearLabel()
        {
            KZGameSession s = KZGameSession.Instance;
            return s != null && s.HasFlag(KZFlags.EchoStarted) && !s.HasFlag(KZFlags.EchoFinished)
                ? "1992" : "2004";
        }

        private string ChapterLabel()
        {
            if (KZStoryDirector.Current == null) return chapterName;
            return KZStoryDirector.Current.isPrologue ? "序章 · 拆楼前夜" : "第一章 · 座";
        }

        private void DrawObjective()
        {
            // Balanced, live mission panel: ornamental art is a BLANK texture.
            // Year, mission and chapter are always read from gameplay state.
            float entrance = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((Time.unscaledTime - hudBornAt) / .58f));
            Rect r = new Rect(34f, 30f - (1f - entrance) * 18f, 590f, 248f);
            if (uiObjectiveV7 != null)
                Image(r, uiObjectiveV7);
            else
                NineSlice(uiHud, r, 44f, 36f);

            // Header title stays centered in the space between rose and year badge.
            GUI.Label(new Rect(r.x + 182f, r.y + 17f, 203f, 65f),
                "\u5F53\u524D\u76EE\u6807", uiHudTitleV7);

            Rect yearRect = new Rect(r.x + 435f, r.y + 13f, 137f, 52f);
            Image(yearRect, uiBadge);
            GUI.Label(new Rect(yearRect.x + 5f, yearRect.y + 2f,
                yearRect.width - 10f, yearRect.height - 4f), YearLabel(), uiHudYearV7);

            string objective = KZStoryDirector.Current == null
                ? "\u8C03\u67E5\u7B2C\u4E00\u6392\u4E03\u53F7\u5EA7"
                : KZStoryDirector.Current.Objective;
            if (string.IsNullOrWhiteSpace(objective))
                objective = "\u8C03\u67E5\u7B2C\u4E00\u6392\u4E03\u53F7\u5EA7";

            // Checkbox shares a single content center line with the objective text.
            Rect check = new Rect(r.x + 70f, r.y + 122f, 33f, 33f);
            FillRect(check, new Color(.04f,.037f,.032f,.85f));
            FrameLine(check, PaleGold, 2.4f);
            Rect textRect = new Rect(r.x + 124f, r.y + 103f, r.width - 152f, 73f);
            GUIContent content = new GUIContent(objective);
            // Single-line where possible; adapt to unusually long in-game missions.
            int oldFont = uiHudTaskV7.fontSize;
            bool oldWrap = uiHudTaskV7.wordWrap;
            for (int size = 29; size >= 20; size--)
            {
                uiHudTaskV7.fontSize = size;
                if (uiHudTaskV7.CalcSize(content).x <= textRect.width - 4f) break;
            }
            if (uiHudTaskV7.CalcSize(content).x > textRect.width - 4f)
                uiHudTaskV7.wordWrap = true;
            GUI.Label(textRect, content, uiHudTaskV7);
            uiHudTaskV7.fontSize = oldFont;
            uiHudTaskV7.wordWrap = oldWrap;

            // Chapter line sits in its own footer strip, clear of the bottom frame.
            GUI.Label(new Rect(r.x + 78f, r.y + 199f, r.width - 130f, 36f),
                ChapterLabel(), uiHudChapterV7);
        }

        private void DrawInteraction()
        {
            string visibleHint = Time.unscaledTime < previewHintUntil ? previewHint : hint;
            DrawInteractionText(visibleHint);
        }

        private void DrawInteractionText(string visibleHint)
        {
            if (string.IsNullOrWhiteSpace(visibleHint)) return;
            string text = CleanHint(visibleHint);
            // KZInteractor already passes '[E] DisplayName'. Do not double-prefix '调查'.
            string label = text.StartsWith("调查", StringComparison.Ordinal) ? text : "调查  " + text;
            float width = Mathf.Clamp(285f + text.Length * 12f, 367f, 630f);
            float x = (ReferenceWidth - width) * .5f;
            Rect r = new Rect(x, 574f, width, 86f);
            NineSlice(uiPrompt, r, 39f, 29f);
            Rect keyRect = new Rect(x + 23f, r.y + 16f, 55f, 51f);
            FillRect(keyRect, new Color(.12f, .078f, .067f, .95f));
            FrameLine(keyRect, PaleGold, 1.8f);
            GUI.Label(new Rect(keyRect.x, keyRect.y, keyRect.width, keyRect.height), "E", uiKey);
            GUI.Label(new Rect(keyRect.xMax + 22f, r.y + 24f, r.width - 114f, 40f), label, uiBody);
            FillRect(new Rect(r.x + 11, r.y + 11, 1f, r.height - 22),
                     new Color(.81f, .57f, .31f, .58f));
        }

        private void DrawReticle()
        {
            float x=ReferenceWidth*.5f,y=ReferenceHeight*.5f;
            FillRect(new Rect(x-7,y,5,1),new Color(.9f,.75f,.51f,.65f));
            FillRect(new Rect(x+3,y,5,1),new Color(.9f,.75f,.51f,.65f));
            FillRect(new Rect(x,y-7,1,5),new Color(.9f,.75f,.51f,.65f));
            FillRect(new Rect(x,y+3,1,5),new Color(.9f,.75f,.51f,.65f));
        }

        private bool IsChapterToast()
        {
            return Time.unscaledTime < toastUntil && !string.IsNullOrEmpty(toast) &&
                (toast.Contains("第一章 · 座") || toast.Contains("第一章·座") || toast.Contains("第一章 ·座"));
        }

        private void DrawChapterCard()
        {
            // Art stays fixed; lettering settles and fades in, like a silent title card.
            // No game state, timeScale or sound source is touched here.
            Image(new Rect(0,0,ReferenceWidth,ReferenceHeight),uiChapter);
            float time = Mathf.Max(0f, Time.unscaledTime - chapterToastStarted);
            float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / .65f));
            float rise = (1f - reveal) * 21f;
            Color previous = GUI.color;
            GUI.color = new Color(1f,1f,1f,reveal);
            GUI.Label(new Rect(440f,296f+rise,768f,80f),"—  第 一 章  —",uiChapterSub);
            GUI.Label(new Rect(540f,372f+rise,568f,163f),"座",uiChapterMain);
            GoldRule(585f,570f,478f);
            GUI.Label(new Rect(516f,610f+rise*.35f,616f,62f),"长明戏楼 · 一段尚未散场的往事",uiChapterSub);
            GUI.color = previous;
            Image(new Rect(1170,636,328,254),uiFlower,new Color(1f,1f,1f,reveal*.87f));
        }

        private void DrawToast()
        {
            if (Time.unscaledTime >= toastUntil || string.IsNullOrWhiteSpace(toast)) return;
            if (IsChapterToast()) { DrawChapterCard(); return; }
            DrawSubtitle("林砚", toast.Replace("【1992 · 残响】", ""));
        }

        private void DrawSubtitle(string speaker, string line)
        {
            // V11: This is the real Unity runtime subtitle renderer, not a pasted screenshot.
            // It preserves the original dialogue content and toast timing.
            float reveal = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((Time.unscaledTime - chapterToastStarted) / .35f));
            Rect r = new Rect(19f, 666f + (1f - reveal) * 19f, 1610f, 226f);
            NineSlice(uiSubtitle, r, 46f, 42f);
            // Slightly stronger hairline borders to match the supplied black/gold reference.
            FrameLine(new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f),
                new Color(.75f,.54f,.30f,.48f), 1.3f);

            // Character label is a SINGLE burnt, frayed plain sheet.
            // No notebook rings, metal clip, ropes, flowers, printing, or baked-in text.
            Rect tag = new Rect(r.x + 25f, r.y - 51f, 365f, 111f);
            Image(tag, uiSpeakerPlainV11 != null ? uiSpeakerPlainV11 : uiSpeakerPaperV7);
            Rect speakerArea = new Rect(tag.x + 29f, tag.y + 18f,
                tag.width - 61f, tag.height - 31f);
            string shownSpeaker = string.IsNullOrWhiteSpace(speaker) ? " " : speaker;
            int oldSpeakerFont = uiSpeakerV7.fontSize;
            for (int size = 50; size >= 26; size--)
            {
                uiSpeakerV7.fontSize = size;
                if (uiSpeakerV7.CalcSize(new GUIContent(shownSpeaker)).x <= speakerArea.width - 6f)
                    break;
            }
            GUI.Label(speakerArea, shownSpeaker, uiSpeakerV7);
            uiSpeakerV7.fontSize = oldSpeakerFont;

            // Live story text, wrapped and resized only when necessary.
            Rect contentRect = new Rect(r.x + 111f, r.y + 73f,
                r.width - 238f, r.height - 99f);
            string shownLine = line ?? "";
            int oldBodyFont = uiSubtitleV7.fontSize;
            GUIContent content = new GUIContent(shownLine);
            for (int size = 41; size >= 26; size--)
            {
                uiSubtitleV7.fontSize = size;
                if (uiSubtitleV7.CalcHeight(content, contentRect.width) <= contentRect.height)
                    break;
            }
            GUI.Label(contentRect, content, uiSubtitleV7);
            uiSubtitleV7.fontSize = oldBodyFont;
            GUI.Label(new Rect(r.xMax - 80f, r.yMax - 64f, 36f, 35f),
                "\u25BE", uiContinueV7);
        }

        private void DrawModalBackdrop()
        {
            float alpha = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((Time.unscaledTime - modalOpenedAt) / .22f));
            Color veil = DimBackdrop;
            veil.a *= alpha;
            FillRect(new Rect(0,0,ReferenceWidth,ReferenceHeight),veil);
        }

        private void RibbonTitle(Rect rect, string title)
        {
            Image(rect,uiRibbon);
            GUI.Label(new Rect(rect.x+78,rect.y+21,rect.width-110,55),title,uiTitle);
        }

        private bool CloseButton(Rect r)
        {
            GUI.Label(r,"×",uiHeading);
            return InvisibleButton(r);
        }

        private void DrawScrollText(Rect region,string text,GUIStyle style,ref Vector2 scrollPosition)
        {
            if (string.IsNullOrEmpty(text)) text="暂时没有记录。";
            float textWidth = region.width-40f;
            float height = Mathf.Max(region.height+2,style.CalcHeight(new GUIContent(text),textWidth)+45f);
            Rect content = new Rect(0,0,region.width-22f,height);
            scrollPosition = GUI.BeginScrollView(region,scrollPosition,content,false,true);
            GUI.Label(new Rect(3,4,textWidth,height-6f),text,style);
            GUI.EndScrollView();
        }

        // V6: True Game-view archive interface. Notebook art contains NO text.
        // The active story page is still rendered from ShowPage(title, body).
        // Chapter-end document replaces the notebook sprite with a plain scorched sheet.
        // Other investigation documents retain V10 visuals; story data stays live.
        private void DrawInvestigation()
        {
            DrawModalBackdrop();
            NineSlice(uiDark, new Rect(10f, 10f, 1628f, 908f), 50f, 42f);

            // One transparent, charred archival notebook texture.
            // Its aspect is 828x895; we keep almost 1:1 proportions to avoid
            // stretching edge tears or making the binder rings elliptical.

            bool usePlainChapterEnd = !string.IsNullOrEmpty(pageTitle) &&
                pageTitle.Contains("章结束");
            if (usePlainChapterEnd && uiChapterEndPlainV11 != null)
                Image(new Rect(804f, 8f, 817f, 888f), uiChapterEndPlainV11);
            else if (uiArchiveNotebook != null)
                Image(new Rect(798f, 8f, 826f, 893f), uiArchiveNotebook);
            else
                Image(new Rect(851f, 61f, 721f, 806f), uiNotePaper);

            Image(new Rect(57f, 37f, 101f, 80f), uiArchiveBook);
            GUI.Label(new Rect(169f, 49f, 470f, 74f), "调查资料", uiArchiveLabel);
            Image(new Rect(637f, 46f, 152f, 73f), uiFlower,
                  new Color(.85f, .65f, .42f, .27f));

            string[] items = { "新出现的节目单", "戏楼火灾剪报", "演出照片（1992）" };
            if (!string.IsNullOrEmpty(pageTitle) && !pageTitle.Contains("节目单"))
                items[0] = pageTitle.Length > 16 ? pageTitle.Substring(0, 16) + "…" : pageTitle;

            for (int i = 0; i < items.Length; i++)
            {
                Rect item = new Rect(68f, 180f + i * 141f, 710f, 115f);
                Image(item, i == evidenceTab ? uiMenuActive : uiMenuIdle);
                GUI.Label(new Rect(item.x + 94f, item.y + 11f,
                       item.width - 124f, item.height - 22f), items[i],
                       i == evidenceTab ? uiMenuSelected : uiMenuNormal);
                if (InvisibleButton(item))
                {
                    evidenceTab = i;
                    evidenceScroll = Vector2.zero;
                    documentExpanded = (i != 0) ||
                        (pageTitle != null && !pageTitle.Contains("节目单"));
                }
            }

            bool cover = evidenceTab == 0 && !documentExpanded &&
                (pageTitle == null || pageTitle.Contains("节目单"));
            if (cover)
            {
                // The booklet sits on the damaged papers as an actual GUI item,
                // not as a baked full-screen image. Clicking opens the live text.
                Matrix4x4 old = GUI.matrix;
                GUIUtility.RotateAroundPivot(6f, new Vector2(1209f, 487f));
                Rect booklet = new Rect(1045f, 250f, 380f, 523f);
                Image(booklet, uiReferenceCover != null ? uiReferenceCover : uiProgram);
                bool clicked = InvisibleButton(booklet);
                GUI.matrix = old;
                if (clicked) { documentExpanded = true; evidenceScroll = Vector2.zero; }
            }
            else
            {
                string heading, body;
                if (evidenceTab == 0)
                {
                    heading = string.IsNullOrWhiteSpace(pageTitle) ? "调查记录" : pageTitle;
                    body = string.IsNullOrWhiteSpace(pageBody) ? "尚无记录。" : pageBody;
                }
                else if (evidenceTab == 1)
                {
                    heading = "戏楼火灾剪报";
                    body = "该剪报尚未收入当前档案。";
                }
                else
                {
                    heading = "演出照片（1992）";
                    KZGameSession session = KZGameSession.Instance;
                    body = session != null && session.HasFlag(KZFlags.Photo)
                        ? "旧照片中记录了长明戏楼的演出。\n\n继续结合照片背面的文字调查。"
                        : "该照片尚未收入当前档案。";
                }

                // No printed text, ruled lines or answer boxes in the paper sprite.
                // All words come from the current story page and wrap naturally.
                GUI.Label(new Rect(936f, 154f, 576f, 128f), heading, uiArchiveHeading);
                DrawCleanPaperText(new Rect(947f, 302f, 547f, 421f), body,
                    ref evidenceScroll, uiArchiveBody);

                // The requested chapter-ending sheet is blank plain paper, with
                // neither ornamental footer nor ruled lines.
                if (!usePlainChapterEnd)
                    GUI.Label(new Rect(953f, 755f, 425f, 41f),
                        "长明戏楼 · 档案抄录", uiArchiveFooter);
                // Clicking the lower return area returns to the booklet cover.
                if (evidenceTab == 0 && documentExpanded &&
                    pageTitle != null && pageTitle.Contains("节目单"))
                {
                    if (InvisibleButton(new Rect(967f, 768f, 420f, 42f)))
                        documentExpanded = false;
                }
            }
            GUI.Label(new Rect(50f, 841f, 680f, 38f),
                "E / Esc 关闭     ·     点击封面阅读资料", uiSmall);
            if (CloseButton(new Rect(1573f, 16f, 58f, 57f)))
            {
                if (showcaseOpen) showcaseOpen = false;
                else ClosePage();
            }
        }

        // No visible scrollbar or ruled paper. Wheel scrolling remains functional
        // for long story documents and notebook entries.
        private void DrawCleanPaperText(Rect area,string content,ref Vector2 scrollPosition,
            GUIStyle textStyle = null)
        {
            if(string.IsNullOrWhiteSpace(content)) content="暂无记录。";
            GUIStyle activeStyle = textStyle ?? uiPaperText;
            float width=area.width-15f;
            float height=Mathf.Max(area.height+3f,
                activeStyle.CalcHeight(new GUIContent(content),width)+33f);
            scrollPosition=GUI.BeginScrollView(area,scrollPosition,
                new Rect(0,0,area.width-7f,height),false,false,
                GUIStyle.none,GUIStyle.none);
            GUI.Label(new Rect(1f,0f,width,height),content,activeStyle);
            GUI.EndScrollView();
        }

        private static string JournalPeople()
        {
            KZGameSession s=KZGameSession.EnsureExists();
            string t="";
            if (s.HasFlag(KZFlags.Phone)) t+="何文斌\n电话里提醒林砚：后台有危险，今晚只登记物品数量。\n\n";
            if (s.HasFlag(KZFlags.Photo) || s.HasFlag(KZFlags.Program)) t+="周明生\n名字出现在旧合照与留座戏的资料中，仍有疑点。\n\n";
            return t.Length==0?"人物信息尚未解锁。":t;
        }
        private static string JournalEvents()
        {
            KZGameSession s=KZGameSession.EnsureExists();
            string t="序章 · 拆楼前夜\n2004年，林砚来到即将拆除的长明戏楼。\n\n";
            if (s.HasFlag(KZFlags.Seat)) t+="第一排七座\n椅面异常干净，需要继续核对资料。\n\n";
            if (s.HasFlag(KZFlags.Program)) t+="旧节目单\n日期：1992年7月17日。\n\n";
            if (s.HasFlag(KZFlags.PhotoAligned)) t+="照片对位\n立柱与侧梯的位置与现场吻合。\n\n";
            return t;
        }
        private string JournalContent()
        {
            if (showcaseOpen && journalTab == 0)
                return "第一排七号座：\n座面在一片灰尘中，意外地干净。\n——似乎并非偶然。";
            switch(journalTab)
            {
                case 1: return CollectedClues();
                case 2: return JournalPeople();
                case 3: return JournalEvents();
                default:
                    return "第一排七号座：\n"+
                    (KZGameSession.EnsureExists().HasFlag(KZFlags.Seat) ?
                        "座面在一片灰尘中，意外地干净。\n——似乎并非偶然。\n\n" : "尚未调查。\n\n")+
                    "调查随记\n\n"+CollectedClues();
            }
        }

        // Notebook matches the supplied reference: four copper tabs perched on ONE
        // warm, distressed parchment sheet. The paper contains NO guide lines.
        private void DrawJournal()
        {
            DrawModalBackdrop();
            Rect frame = new Rect(221f,145f,1205f,618f);
            NineSlice(uiDark,frame,50f,49f);
            // A subtle second sheet adds convincing layered-paper depth.
            Image(new Rect(290f,240f,1054f,469f),uiNotePaper,
                new Color(.69f,.61f,.51f,.55f));
            Rect sheet=new Rect(289f,237f,1076f,465f);
            Image(sheet,uiNotePaper!=null?uiNotePaper:uiPaper);
            string[] tabs={"笔记","线索","人物","事件"};
            for(int i=0;i<tabs.Length;i++)
            {
                Rect tab = new Rect(299f+i*265f,162f,267f,95f);
                Image(tab,i==journalTab?uiReferenceTabOn:uiReferenceTabIdle);
                GUIStyle st = i==journalTab?uiJournalTabSelected:uiJournalTabNormal;
                GUI.Label(new Rect(tab.x+39f,tab.y+21f,189f,62f),tabs[i],st);
                if(InvisibleButton(tab)) {journalTab=i;journalScroll=Vector2.zero;}
            }
            if(CloseButton(new Rect(1370f,167f,47f,49f)))
            {
                if(showcaseOpen)showcaseOpen=false;else journalOpen=false;
                return;
            }
            // The first line is the actual game-record heading; there is no huge
            // '调查记录' card or decorative grid covering up the page.
            string content=JournalContent();
            DrawCleanPaperText(new Rect(371f,307f,902f,328f),content,ref journalScroll);
            Image(new Rect(1290f,638f,108f,105f),uiNib);
            GUI.Label(new Rect(1072f,704f,260f,33f),"Tab / E / Esc 收起",uiSmall);
        }

        private bool Option(Rect r,string text,bool active)
        {
            Image(r,active ? uiBadgeActive : uiBadge);
            GUI.Label(new Rect(r.x+20,r.y+13,r.width-40,r.height-13),text,active?uiPaperSmall:uiOption);
            return InvisibleButton(r);
        }

        // Real chapter-one puzzle: scene-driven and playable without editor menus.
        private void DrawSeatPuzzle()
        {
            DrawModalBackdrop();
            Rect frame = new Rect(260f, 145f, 1128f, 655f);
            NineSlice(uiDark, frame, 50f, 48f);
            RibbonTitle(new Rect(276f, 121f, 580f, 98f), "第一排 · 座次复原");
            GUI.Label(new Rect(331f, 232f, 970f, 57f),
                "根据宣传册、旧合照与票根，复原第一排座次。", uiBody);
            string[] labels = {
                "第一排六座 · " + puzzle.Seat6 + "   [1 切换]",
                "第一排七座 · " + puzzle.Seat7 + "   [固定]",
                "第一排八座 · " + puzzle.Seat8 + "   [3 切换]"
            };
            for (int i = 0; i < 3; i++)
            {
                Rect r = new Rect(332f, 322f + i * 92f, 825f, 80f);
                Image(r, i == 1 ? uiBadgeActive : uiBadge);
                GUI.Label(new Rect(r.x + 28f, r.y + 12f,
                    r.width - 55f, r.height - 22f), labels[i], uiPaperSmall);
                if (i != 1 && InvisibleButton(r)) puzzle.Cycle(i == 0 ? 0 : 2);
            }
            GUI.Label(new Rect(344f, 612f, 933f, 70f), puzzleFeedback, uiBody);
            if (Option(new Rect(1040f, 700f, 257f, 69f), "提交判定", true))
                SubmitSeatPuzzle();
            GUI.Label(new Rect(341f, 720f, 665f, 40f),
                "1 / 3 切换 · Enter 提交 · E / Esc 返回", uiSmall);
            if (CloseButton(new Rect(1324f, 170f, 47f, 48f))) puzzleOpen = false;
        }

        private void SubmitSeatPuzzle()
        {
            KZSeatPuzzle.Result result = puzzle.Check(cluesKnown != null && cluesKnown());
            switch (result)
            {
                case KZSeatPuzzle.Result.MissingClues:
                    puzzleFeedback = "还缺少关键证据：请查看宣传册、旧合照、票根。";
                    KZAudioFeedback.PlayCue(KZSoundCue.PuzzleFailure);
                    break;
                case KZSeatPuzzle.Result.Incomplete:
                    puzzleFeedback = "还有位置未填写；按数字1和3切换人物。";
                    KZAudioFeedback.PlayCue(KZSoundCue.PuzzleFailure);
                    break;
                case KZSeatPuzzle.Result.Incorrect:
                    puzzleFeedback = "座次与已有线索不符，可以继续修改。";
                    KZAudioFeedback.PlayCue(KZSoundCue.PuzzleFailure);
                    break;
                case KZSeatPuzzle.Result.Correct:
                    puzzleOpen = false;
                    KZAudioFeedback.PlayCue(KZSoundCue.PuzzleSuccess);
                    Action callback = puzzleSolved;
                    puzzleSolved = null;
                    if (callback != null) callback();
                    break;
            }
        }

        private void DrawRelic()
        {
            DrawModalBackdrop();
            Rect frame=new Rect(312,142,1024,634);
            NineSlice(uiDark,frame,49,49);
            RibbonTitle(new Rect(330,107,550,98),"七号座 · 遗物推理");
            Image(new Rect(329f, 102f, 75f, 74f), uiRose);
            GUI.Label(new Rect(372,221,889,67),"选择真正留在七号座上的遗物。",uiBody);
            string[] choices={"钥匙串","旧工牌","铜哨"};
            for(int i=0;i<3;i++)
            {
                Rect item=new Rect(373,327+i*91,680,79);
                if(Option(item,(i+1)+"  ·  "+choices[i],relicChoice==i+1))relicChoice=i+1;
            }
            GUI.Label(new Rect(378,612,864,53),relicFeedback,uiBody);
            if(Option(new Rect(1006,681,263,74),"确认  Enter",true) && !showcaseOpen)SubmitRelic();
            GUI.Label(new Rect(382,713,600,42),"数字 1 / 2 / 3 选择 · E / Esc 返回",uiSmall);
            if(CloseButton(new Rect(1274,172,50,48))) { if (showcaseOpen) showcaseOpen = false; else relicOpen = false; }
        }

        private void SubmitRelic()
        {
            if(relicChoice==0) relicFeedback="先选择一件遗物。";
            else if(relicChoice!=1)
            {
                relicFeedback="与场务物品记录不符。还可以重新选择。";
                KZAudioFeedback.PlayCue(KZSoundCue.PuzzleFailure);
            }
            else
            {
                relicOpen=false;
                KZAudioFeedback.PlayCue(KZSoundCue.PuzzleSuccess);
                Action callback=relicSolved;relicSolved=null;
                if(callback!=null)callback();
            }
        }

        // Cursor is deliberately visible only for a modal page with actual buttons.
        // Restore the player's PREVIOUS state; never permanently unlock their FPS view.
        private void LateUpdate()
        {
            bool modal=current == this &&
                (pageTitle != null || journalOpen || puzzleOpen || relicOpen || showcaseOpen);
            if(modal && !cursorOwned)
            {
                previousCursorLock=Cursor.lockState;
                previousCursorVisible=Cursor.visible;
                cursorOwned=true;
                Cursor.lockState=CursorLockMode.None;
                Cursor.visible=true;
            }
            else if(!modal && cursorOwned) RestoreCursor();
        }
        private void RestoreCursor()
        {
            if(!cursorOwned)return;
            cursorOwned=false;
            Cursor.lockState=previousCursorLock;
            Cursor.visible=previousCursorVisible;
        }

        // Editor-menu alternative to F9. Does not write story progress.
        public static void ToggleVisualShowcase()
        {
            if (current == null)
            {
                Debug.LogWarning("[空座/UI V6] 当前场景没有 KZHud；请运行 01_Prologue。");
                return;
            }
            if (KZTitleTransition.IsPlaying || KZPhotoAlignmentPuzzle.IsAnyActive) return;
            if (!current.showcaseOpen && (current.pageTitle != null || current.journalOpen ||
                current.relicOpen || current.puzzleOpen))
            {
                Debug.LogWarning("[空座/UI V6] 请先按 E / Esc 退出当前调查窗口，再打开验收模式。");
                return;
            }
            current.showcaseOpen = !current.showcaseOpen;
            if (current.showcaseOpen)
            {
                current.showcaseTab = 0;
                current.modalOpenedAt = Time.unscaledTime;
            }
        }

        // Optional Editor menu previews; F9 is the editor-independent Game-view verifier.
        public static void PreviewJournal()
        {
            if(current==null)return;
            current.pageTitle=null;
            current.journalOpen=true;
            current.puzzleOpen=false;
            current.relicOpen=false;
            current.journalTab=0;
        }
        public static void PreviewPrompt()
        {
            if(current!=null)current.ShowToastLocal("七号座比周围干净，像有人刚刚离开。",8f);
            if (current != null)
            {
                current.previewHint = "[E] 第一排七号座";
                current.previewHintUntil = Time.unscaledTime + 8f;
            }
        }
        private void ShowToastLocal(string content,float seconds)
        {
            toast=content;
            toastUntil=Time.unscaledTime+seconds;
        }

        private void OnGUI()
        {
            if(current != this || KZTitleTransition.IsPlaying ||
                KZPhotoAlignmentPuzzle.IsAnyActive) return;
            PrepareStyles();
            if(Event.current.type==EventType.Layout && Screen.width==0)return;
            Matrix4x4 oldMatrix=GUI.matrix;
            Color oldColor=GUI.color;
            float scale=Mathf.Max(.25f,Mathf.Min(Screen.width/ReferenceWidth,Screen.height/ReferenceHeight));
            float ox=(Screen.width-ReferenceWidth*scale)*.5f;
            float oy=(Screen.height-ReferenceHeight*scale)*.5f;
            GUI.matrix=Matrix4x4.TRS(new Vector3(ox,oy,0f),Quaternion.identity,new Vector3(scale,scale,1f));
            try
            {
                if (showcaseOpen)
                {
                    DrawGameViewShowcase();
                    return;
                }
                // Floating game HUD is always real and uses KZStoryDirector.Objective.
                if(showObjectiveHud && !IsChapterToast() && !IsModal)DrawObjective();
                if(pageTitle!=null)DrawInvestigation();
                else if(puzzleOpen)DrawSeatPuzzle();
                else if(relicOpen)DrawRelic();
                else if(journalOpen)DrawJournal();
                else
                {
                    DrawReticle();
                    DrawInteraction();
                }
                if(!IsModal && Time.unscaledTime<toastUntil && !string.IsNullOrEmpty(toast)) DrawToast();
                if(showControlsHint && !IsModal)
                    GUI.Label(new Rect(42,890,1490,30),
                       "WASD 移动  ·  Shift 加速  ·  鼠标观察  ·  E 调查  ·  F 照片对位  ·  Tab 笔记  ·  Esc 鼠标",uiSmall);
            }
            finally
            {
                GUI.matrix=oldMatrix;
                GUI.color=oldColor;
            }
        }

        // Built-in Play-mode verification: actual Unity Game view, not a mock PNG.
        // It reuses the exact same live renderers as the gameplay HUD / pages.
        // Only text data in this preview is temporary; it writes no story flags.
        private void DrawGameViewShowcase()
        {
            switch (showcaseTab)
            {
                case 0:
                    DrawObjective();
                    DrawReticle();
                    DrawInteractionText("[E] 第一排七号座");
                    DrawSubtitle("林砚", "……这张节目单……怎么会在这里？");
                    break;
                case 1:
                    // Safeguard: F9 cannot open while a real modal is active,
                    // so replacing the text for one GUI event cannot call story callbacks.
                    string oldTitle = pageTitle;
                    string oldBody = pageBody;
                    try
                    {
                        pageTitle = "临槐县文化馆 · 拆除前资产清点表";
                        pageBody = "2004年10月21日 17:42\n\n" +
                                   "长明戏楼拆除前资产清点表\n\n" +
                                   "□ 戏服  □ 照片  □ 磁带\n" +
                                   "□ 乐器  □ 演出资料  □ 历史档案\n\n" +
                                   "手写备注：明早施工，今晚务必完成。\n\n" +
                                   "你是县文化馆资料员林砚，负责最后的资料登记。";
                        DrawInvestigation();
                    }
                    finally
                    {
                        pageTitle = oldTitle;
                        pageBody = oldBody;
                    }
                    break;
                case 2: DrawJournal(); break;
                case 3: DrawRelic(); break;
                case 4: DrawChapterCard(); break;
            }
            DrawShowcaseToolbar();
        }

        private void DrawShowcaseToolbar()
        {
            // F9 visual QA only. The toolbar must never cover the subtitle being
            // inspected on the exploration tab; ordinary gameplay has no toolbar.
            Rect bar = showcaseTab == 0
                ? new Rect(841f, 38f, 728f, 137f)
                : new Rect(49f, 693f, 726f, 137f);
            NineSlice(uiDark, bar, 47f, 29f);
            GUI.Label(new Rect(bar.x + 23f, bar.y + 14f, 470f, 35f),
                "\u300A\u7A7A\u5EA7\u300B\u00B7 \u6E38\u620F\u5185 UI \u9A8C\u6536", uiSmall);
            GUI.Label(new Rect(bar.x + 506f, bar.y + 14f, 216f, 36f), "F9 / Esc", uiSmall);
            string[] labels = { "\u63A2\u7D22", "\u8D44\u6599", "\u7B14\u8BB0", "\u9057\u7269", "\u7AE0\u8282" };
            for (int i = 0; i < labels.Length; i++)
            {
                Rect tab = new Rect(bar.x + 15f + i * 139f, bar.y + 65f, 130f, 48f);
                FillRect(tab, i == showcaseTab
                    ? new Color(.46f,.28f,.13f,.96f) : new Color(.12f,.10f,.078f,.91f));
                FrameLine(tab, i == showcaseTab ? PaleGold : Brass, 1.2f);
                GUI.Label(new Rect(tab.x + 2f, tab.y + 10f, tab.width - 4f, 27f),
                    labels[i], uiSmallCenter);
                if (InvisibleButton(tab))
                {
                    showcaseTab = i;
                    evidenceTab = 0;
                    evidenceScroll = Vector2.zero;
                    documentExpanded = false;
                }
            }
        }

        private static string CollectedClues()
        {
            KZGameSession s = KZGameSession.EnsureExists();
            string result = "";
            if (s.HasFlag(KZFlags.Worksheet)) result += "• 资产清点表：明早施工，今晚务必完成。\n\n";
            if (s.HasFlag(KZFlags.Phone)) result += "• 电话：何文斌提醒后台危险，只登记数量。\n\n";
            if (s.HasFlag(KZFlags.Seat)) result += "• 第一排七座：椅面比其他位置干净。\n\n";
            if (s.HasFlag(KZFlags.Register)) result += "• 1992座位登记表：一排七座——空。\n\n";
            if (s.HasFlag(KZFlags.Program)) result += "• 节目单：1992年7月17日，周明生留座戏。\n\n";
            if (s.HasFlag(KZFlags.Ritual)) result += "• 留座戏：亡者留座，戏散前不得取遗物。\n\n";
            if (s.HasFlag(KZFlags.Photo)) result += "• 旧合照背面：周明生，1972—1992。\n\n";
            if (s.HasFlag(KZFlags.Ticket)) result += "• 票根：可与座次表核对。\n\n";
            if (s.HasFlag(KZFlags.PhotoAligned)) result += "• 照片对位：立柱与侧梯吻合，中央留出了七号座。\n\n";
            if (s.HasFlag(KZFlags.WorkRecord)) result += "• 场务物品记录：周明生保管过戏楼钥匙。\n\n";
            if (s.HasFlag(KZFlags.RelicIdentified)) result += "• 遗物推理：七号座留下的是钥匙串。\n\n";
            if (s.HasFlag(KZFlags.EchoFinished)) result += "• 残响：女子在空座坐下，拿走钥匙。\n\n";
            return result.Length == 0 ? "暂时没有记录。" : result;
        }
    }
}
