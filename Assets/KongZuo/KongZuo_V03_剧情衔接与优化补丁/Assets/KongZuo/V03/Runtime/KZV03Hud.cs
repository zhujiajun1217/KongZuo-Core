using System;
using System.Text;
using UnityEngine;

namespace KongZuo.V03
{
    /// <summary>Dependency-free prototype UI. Replace with TMP/Canvas once artwork is available.</summary>
    [DisallowMultipleComponent]
    public sealed class KZV03Hud : MonoBehaviour
    {
        private static KZV03Hud active;
        public static string Hint = "";
        public static bool IsModal { get { return active != null && (active.pageTitle != null || active.journalOpen); } }

        private string pageTitle, pageBody, toast = "", hypothesis = "无法判断";
        private Action onPageClosed;
        private bool journalOpen;
        private float toastEnd;
        private Vector2 scroll;
        private int pageOpenedFrame = -1;
        private GUIStyle copy, header, small, button;

        private void Awake() { active = this; }
        private void OnDestroy() { if (active == this) { active = null; Hint = ""; } }

        public static void Toast(string value, float seconds = 4f)
        {
            if (active == null) { Debug.Log("[空座V0.3] " + value); return; }
            active.toast = value ?? "";
            active.toastEnd = Time.unscaledTime + seconds;
        }

        public static void ShowPage(string title, string body, Action onClose = null)
        {
            if (active == null)
            {
                Debug.LogWarning("[空座V0.3] 缺少 KZV03Hud: " + title);
                return;
            }
            active.journalOpen = false;
            active.pageTitle = title;
            active.pageBody = body ?? "";
            active.onPageClosed = onClose;
            active.pageOpenedFrame = Time.frameCount;
            active.scroll = Vector2.zero;
        }

        private void Update()
        {
            if (KZV03Director.Current != null && KZV03Director.Current.Finale) return;
            if (pageTitle != null && Time.frameCount != pageOpenedFrame && (KZV03Input.Interact() || KZV03Input.Escape())) { ClosePage(); return; }
            if (journalOpen && (KZV03Input.Interact() || KZV03Input.Escape() || KZV03Input.Journal()))
            {
                journalOpen = false;
                return;
            }
            if (pageTitle == null && KZV03Input.Journal()) { journalOpen = !journalOpen; scroll = Vector2.zero; }
        }

        private void ClosePage()
        {
            Action callback = onPageClosed;
            pageTitle = null;
            pageBody = null;
            onPageClosed = null;
            if (callback != null) callback();
        }

        private void PrepareStyles()
        {
            if (copy != null) return;
            copy = new GUIStyle(GUI.skin.label) { fontSize=17, wordWrap=true, richText=false };
            header = new GUIStyle(GUI.skin.label) { fontSize=23, fontStyle=FontStyle.Bold, wordWrap=true };
            small = new GUIStyle(GUI.skin.label) { fontSize=14, wordWrap=true };
            button = new GUIStyle(GUI.skin.button) { fontSize=16, wordWrap=true };
        }

        private void OnGUI()
        {
            PrepareStyles();
            if (KZV03Director.Current != null && KZV03Director.Current.Finale)
            {
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.blackTexture);
                GUIStyle centered = new GUIStyle(GUI.skin.label)
                { fontSize=29, fontStyle=FontStyle.Bold, alignment=TextAnchor.MiddleCenter, wordWrap=true };
                GUI.Label(new Rect(Screen.width*.12f, Screen.height*.32f,
                    Screen.width*.76f, Screen.height*.32f),
                    "那一晚，门没有为她打开。\n\n《空座》 · 未完待续", centered);
                return;
            }
            float width = Mathf.Min(790f, Screen.width - 32f);
            GUI.Box(new Rect(12f, 12f, Mathf.Min(700f, Screen.width - 24f), 102f), "");
            string heading = KZV03Director.Current == null ? "《空座》V0.3" : KZV03Director.Current.ChapterLabel;
            string objective = KZV03Director.Current == null ? "载入剧情" : KZV03Director.Current.Objective;
            GUI.Label(new Rect(28f, 18f, Screen.width-56f, 32f), heading, header);
            GUI.Label(new Rect(28f, 54f, Mathf.Min(665f, Screen.width-56f), 54f), "当前目标：" + objective, small);
            if (Time.unscaledTime < toastEnd && !string.IsNullOrEmpty(toast) && pageTitle == null && !journalOpen)
            {
                Rect t = new Rect((Screen.width-width)*.5f, Screen.height-125f, width, 76f);
                GUI.Box(t, "");
                GUI.Label(new Rect(t.x+18f,t.y+13f,t.width-36f,t.height-20f),toast,copy);
            }
            if (!IsModal)
            {
                GUI.Label(new Rect(18f,Screen.height-39f,Screen.width-36f,30f),Hint + "  |  Esc 鼠标锁定", small);
                return;
            }

            float h = Mathf.Min(590f, Screen.height-105f);
            Rect area = new Rect((Screen.width-width)*.5f,(Screen.height-h)*.5f,width,h);
            GUI.Box(area," ");
            GUILayout.BeginArea(new Rect(area.x+26f,area.y+24f,area.width-52f,area.height-42f));
            if (pageTitle != null)
            {
                GUILayout.Label(pageTitle, header);
                GUILayout.Space(8);
                scroll = GUILayout.BeginScrollView(scroll);
                GUILayout.Label(pageBody, copy);
                GUILayout.EndScrollView();
                GUILayout.Space(12);
                if (GUILayout.Button("收起资料 / 继续（E）", button, GUILayout.Height(42))) ClosePage();
            }
            else if (journalOpen)
            {
                GUILayout.Label("林砚的调查笔记", header);
                GUILayout.Space(8);
                scroll = GUILayout.BeginScrollView(scroll);
                GUILayout.Label(BuildJournal(), copy);
                GUILayout.Space(12);
                KZV03State state = KZV03State.Ensure();
                if (state.HasAll(KZV03Flag.Alignment, KZV03Flag.Handover) && !state.Has(KZV03Flag.EvidenceLinked))
                {
                    GUILayout.Label("证据关联：照片对景与遗物点交单", header);
                    if (GUILayout.Button("将两项证据关联：确认七号座放过周明生铜钥匙", button, GUILayout.Height(48)))
                    {
                        state.Add(KZV03Flag.EvidenceLinked);
                        KZV03Director.Current?.EvidenceLinked();
                    }
                }
                if (state.Has(KZV03Flag.Argument))
                {
                    GUILayout.Space(12);
                    GUILayout.Label("暂时的判断（不是唯一正确答案）", header);
                    foreach (string option in new[] {"阻拦离开", "时间另有安排", "无法判断"})
                        if (GUILayout.Button((hypothesis==option?"● ":"○ ")+option, button, GUILayout.Height(34))) hypothesis=option;
                    GUILayout.Label("这只是林砚当前的猜测，不视为已证实事实。",small);
                }
                GUILayout.EndScrollView();
                GUILayout.Space(12);
                if (GUILayout.Button("合上笔记（Tab/E）", button,GUILayout.Height(42))) journalOpen=false;
            }
            GUILayout.EndArea();
        }

        private static string BuildJournal()
        {
            KZV03State s = KZV03State.Ensure();
            StringBuilder b = new StringBuilder();
            b.AppendLine("【确定事实 / 由证据支持】");
            if (s.Has(KZV03Flag.Phone)) b.AppendLine("• 1992年戏楼曾失火，沈芸失踪；‘已经离镇’只是传闻。");
            if (s.Has(KZV03Flag.Seat) && s.Has(KZV03Flag.Register)) b.AppendLine("• 一排七座是特意留出的空座，登记表也记为‘空’。");
            if (s.Has(KZV03Flag.Alignment)) b.AppendLine("• 1992年的现场照片显示七号座上放过物件，暂不能认出是什么。");
            if (s.Has(KZV03Flag.EvidenceLinked)) b.AppendLine("• 对景照片＋点交单：七号座曾放置周明生的铜钥匙。");
            if (s.Has(KZV03Flag.EchoKey)) b.AppendLine("• 一名穿靛蓝滚边戏服的女子曾坐上空座并取走钥匙。");
            if (s.Has(KZV03Flag.Identity)) b.AppendLine("• 剧照与衣袖特征吻合：取钥匙的女子是沈芸。");
            if (s.Has(KZV03Flag.LifePhoto)) b.AppendLine("• 沈芸与顾兰曾关系亲密，也憧憬外面的生活。");
            if (s.Has(KZV03Flag.Travel)) b.AppendLine("• 购票凭证：沈芸打算乘1992年7月17日23:40的列车离镇；是否上车未知。");
            if (s.Has(KZV03Flag.Argument)) b.AppendLine("• 残响片段中许承山抓住沈芸并说‘不能现在走’；上下文不完整。");
            if (s.Has(KZV03Flag.XuNote)) b.AppendLine("• 许承山的纸条写着：十一点后门会合，一起去车站。");
            if (s.Has(KZV03Flag.ChestLabel)) b.AppendLine("• 地下道具仓旧戏箱登记的保管钥匙是周明生的铜钥匙，箱中物品未知。");
            if (s.Has(KZV03Flag.GuSleeve)) b.AppendLine("• 顾兰留下含糊的‘先放我这儿’纸套；不能据此判定她藏过车票。");
            if (s.Has(KZV03Flag.EastDoorEcho)) b.AppendLine("• 起火后的短残响显示沈芸疑似曾在东侧工作门内侧尝试开门，门未立刻打开。");
            b.AppendLine();
            b.AppendLine("【仍待调查】");
            if (s.Has(KZV03Flag.EchoKey)) b.AppendLine("• 她为什么需要遗物钥匙？");
            if (s.Has(KZV03Flag.Travel)) b.AppendLine("• 她有没有赶上火车？车票后来去了哪里？");
            if (s.Has(KZV03Flag.EastDoorEcho)) b.AppendLine("• 门为什么打不开？她后来去了哪里？");
            b.AppendLine();
            b.AppendLine("残响只呈现片段；猜测不是结论。按 Tab 关闭笔记。");
            return b.ToString();
        }
    }
}
