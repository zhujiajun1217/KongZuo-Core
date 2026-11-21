using System;
using UnityEngine;

namespace KongZuo
{
    // 旧版主程序 P0 HUD：无外部字体/贴图依赖，保留 KZHud 公共 API 与剧情状态规则。
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
        private bool relicOpen;
        private int relicChoice;
        private Action relicSolved;
        private string relicFeedback = "";
        private int relicOpenedFrame = -1;
        private Vector2 scroll;
        private float toastUntil;
        private int pageOpenedFrame = -1;

        private GUIStyle labelStyle;
        private GUIStyle headStyle;
        private GUIStyle centeredStyle;
        private GUIStyle footerStyle;

        private static readonly Color Background = new Color(0.075f, 0.09f, 0.11f, 0.94f);
        private static readonly Color Border = new Color(0.66f, 0.47f, 0.29f, 0.88f);
        private static readonly Color Ivory = new Color(0.94f, 0.89f, 0.78f);
        private static readonly Color Gold = new Color(0.89f, 0.74f, 0.48f);

        public static bool IsModal
        {
            get { return KZTitleTransition.IsPlaying || (current != null &&
                (current.pageTitle != null || current.journalOpen || current.relicOpen)); }
        }

        private void Awake() { current = this; }
        private void OnDestroy()
        {
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
            current.relicOpen = false;
            current.pageTitle = title ?? "资料检视";
            current.pageBody = body ?? "";
            current.scroll = Vector2.zero;
            current.pageClosed = onClose;
            current.pageOpenedFrame = Time.frameCount;
        }

        public static void ShowRelicChoice(Action onSolved)
        {
            if (current == null) { Debug.LogWarning("缺少 KZHud。"); return; }
            current.pageTitle = null;
            current.journalOpen = false;
            current.relicOpen = true;
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

        private void Update()
        {
            if (KZTitleTransition.IsPlaying) return;
            if (pageTitle != null)
            {
                if (Time.frameCount != pageOpenedFrame &&
                    (KZInput.ConsumeInteract() || KZInput.ConsumeEscape()))
                    ClosePage();
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
                if (KZInput.Confirm())
                {
                    if (relicChoice == 0) relicFeedback = "先选择一件遗物。";
                    else if (relicChoice != 1)
                    {
                        relicFeedback = "与场务物品记录不符。还可以重新选择。";
                        KZAudioFeedback.PlayCue(KZSoundCue.PuzzleFailure);
                    }
                    else
                    {
                        relicOpen = false;
                        KZAudioFeedback.PlayCue(KZSoundCue.PuzzleSuccess);
                        Action callback = relicSolved;
                        relicSolved = null;
                        if (callback != null) callback();
                    }
                }
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
                KZAudioFeedback.PlayCue(KZSoundCue.Paper);
            }
        }

        private void PrepareStyles()
        {
            if (labelStyle != null) return;
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                richText = false
            };
            labelStyle.normal.textColor = Ivory;
            headStyle = new GUIStyle(labelStyle)
            {
                fontSize = 23,
                fontStyle = FontStyle.Bold
            };
            headStyle.normal.textColor = Gold;
            centeredStyle = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };
            footerStyle = new GUIStyle(labelStyle)
            {
                fontSize = 13
            };
        }

        private static void Panel(Rect rectangle)
        {
            Color previous = GUI.color;
            GUI.color = Background;
            GUI.DrawTexture(rectangle, Texture2D.whiteTexture);
            GUI.color = Border;
            GUI.DrawTexture(new Rect(rectangle.x, rectangle.y, rectangle.width, 1.5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rectangle.x, rectangle.yMax - 1.5f, rectangle.width, 1.5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rectangle.x, rectangle.y, 1.5f, rectangle.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rectangle.xMax - 1.5f, rectangle.y, 1.5f, rectangle.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void OnGUI()
        {
            if (current != this || KZTitleTransition.IsPlaying) return;
            PrepareStyles();

            float topWidth = Mathf.Min(650f, Screen.width - 28f);
            Panel(new Rect(14f, 14f, topWidth, 110f));
            GUI.Label(new Rect(28f, 22f, topWidth - 28f, 34f), chapterName, headStyle);
            string objective = KZStoryDirector.Current == null
                ? "等待关卡逻辑" : KZStoryDirector.Current.Objective;
            GUI.Label(new Rect(28f, 60f, topWidth - 28f, 60f), "当前目标：" + objective, labelStyle);

            if (pageTitle != null)
            {
                float w = Mathf.Min(760f, Screen.width - 40f);
                float h = Mathf.Max(160f, Mathf.Min(460f, Screen.height - 140f));
                Rect area = new Rect((Screen.width - w) * .5f, (Screen.height - h) * .5f, w, h);
                Panel(area);
                GUILayout.BeginArea(new Rect(area.x + 24f, area.y + 20f, area.width - 48f, area.height - 40f));
                GUILayout.Label(pageTitle, headStyle);
                scroll = GUILayout.BeginScrollView(scroll);
                GUILayout.Label(pageBody, labelStyle);
                GUILayout.EndScrollView();
                GUILayout.FlexibleSpace();
                GUILayout.Label("按 E / Esc 收起文档 · 继续调查", footerStyle);
                GUILayout.EndArea();
            }
            else if (relicOpen)
            {
                float w = Mathf.Min(700f, Screen.width - 40f);
                float h = Mathf.Max(180f, Mathf.Min(380f, Screen.height - 140f));
                Rect area = new Rect((Screen.width - w) * .5f, (Screen.height - h) * .5f, w, h);
                Panel(area);
                GUILayout.BeginArea(new Rect(area.x + 24f, area.y + 20f, w - 48f, h - 40f));
                GUILayout.Label("七号座的遗物", headStyle);
                GUILayout.Space(10f);
                GUILayout.Label("1. 钥匙串" + (relicChoice == 1 ? "  [已选]" : ""), labelStyle);
                GUILayout.Label("2. 旧工牌" + (relicChoice == 2 ? "  [已选]" : ""), labelStyle);
                GUILayout.Label("3. 铜哨" + (relicChoice == 3 ? "  [已选]" : ""), labelStyle);
                GUILayout.Space(12f);
                GUILayout.Label(relicFeedback, labelStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label("数字 1 / 2 / 3 选择 · Enter 确认 · E / Esc 返回", footerStyle);
                GUILayout.EndArea();
            }
            else if (journalOpen)
            {
                float w = Mathf.Min(620f, Screen.width - 36f);
                float h = Mathf.Max(160f, Mathf.Min(440f, Screen.height - 140f));
                Rect area = new Rect(Screen.width - w - 16f, 132f, w, h);
                Panel(area);
                GUILayout.BeginArea(new Rect(area.x + 20f, area.y + 18f, area.width - 40f, area.height - 36f));
                GUILayout.Label("林砚的调查笔记", headStyle);
                scroll = GUILayout.BeginScrollView(scroll);
                GUILayout.Label(CollectedClues(), labelStyle);
                GUILayout.EndScrollView();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Tab / E / Esc 收起", footerStyle);
                GUILayout.EndArea();
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(hint))
                {
                    float w = Mathf.Min(450f, Screen.width - 24f);
                    Rect hintArea = new Rect((Screen.width - w) * .5f, Screen.height - 128f, w, 46f);
                    Panel(hintArea);
                    GUI.Label(new Rect(hintArea.x + 10f, hintArea.y + 2f,
                        hintArea.width - 20f, hintArea.height - 4f), hint, centeredStyle);
                }
                GUI.Label(new Rect(Screen.width * .5f - 9f, Screen.height * .5f - 14f, 18f, 24f), "+", headStyle);
            }

            if (Time.unscaledTime < toastUntil && !string.IsNullOrEmpty(toast))
            {
                float w = Mathf.Min(950f, Screen.width - 30f);
                Rect toastArea = new Rect((Screen.width - w) * .5f, Screen.height - 74f, w, 57f);
                Panel(toastArea);
                GUI.Label(new Rect(toastArea.x + 15f, toastArea.y + 6f,
                    toastArea.width - 30f, toastArea.height - 12f), toast, centeredStyle);
            }
            GUI.Label(new Rect(18f, Screen.height - 25f, Screen.width - 30f, 23f),
                "WASD 移动 · Shift 加速 · 鼠标观察 · E 调查 · F 照片对位 · Tab 笔记 · Esc 释放/捕获", footerStyle);
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
            if (s.HasFlag(KZFlags.Ticket)) result += "• 票根：残缺记录显示七号座未售。\n\n";
            if (s.HasFlag(KZFlags.PhotoAligned)) result += "• 照片对位：立柱与侧梯吻合，中央留出了七号座。\n\n";
            if (s.HasFlag(KZFlags.WorkRecord)) result += "• 场务物品记录：周明生保管过戏楼钥匙。\n\n";
            if (s.HasFlag(KZFlags.RelicIdentified)) result += "• 遗物推理：七号座留下的是钥匙串。\n\n";
            if (s.HasFlag(KZFlags.EchoFinished)) result += "• 残响：女子在空座坐下，拿走钥匙。\n\n";
            return result.Length == 0 ? "暂时没有记录。" : result;
        }
    }
}
