using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KongZuo
{
    // 序章与第一章的P0剧情控制器。之后可替换成 Timeline/正式任务系统。
    [DisallowMultipleComponent]
    public sealed class KZStoryDirector : MonoBehaviour
    {
        public static KZStoryDirector Current { get; private set; }
        public bool isPrologue = true;

        [Header("公共")]
        public KZFirstPersonPlayer player;
        public Light stageLamp;

        [Header("序章")]
        public GameObject frontDoor;
        public GameObject anomalyShadow;
        public GameObject programProp;

        [Header("第一章／1992残响")]
        public GameObject echo1992Group;
        public Transform echoActress;
        public Transform[] echoRoute;
        public Transform[] crowdHeads;
        public GameObject echoKey;
        public Transform echoPlayerView;
        public KZPhotoAlignmentPuzzle photoAlignment;

        private KZGameSession session;
        private bool busy;
        private float originalLampIntensity;
        private Color originalAmbient;
        private Vector3 closedDoorPosition;
        private Quaternion closedDoorRotation;
        private bool doorPoseCaptured;

        public bool IsBusy { get { return busy; } }

        public string Objective
        {
            get
            {
                if (session == null) return "载入剧情中";
                if (isPrologue)
                {
                    if (!Has(KZFlags.Worksheet)) return "查看拆楼前资产清点表";
                    if (!Has(KZFlags.Door)) return "打开正门，进入长明戏楼";
                    if (!Has(KZFlags.Phone)) return "到办公室接听电话";
                    if (!Has(KZFlags.Seat)) return "调查观众厅的第一排七座";
                    if (!Has(KZFlags.Register)) return "前往售票口核对1992年的座位登记表";
                    if (!Has(KZFlags.AnomalyStarted)) return "离开观众厅后，从前厅重新进入";
                    if (!Has(KZFlags.AnomalyResolved)) return "靠近第一排，确认刚才的人影";
                    if (!Has(KZFlags.Program)) return "查看新出现的1992年节目单";
                    return "序章完成，准备进入第一章";
                }

                if (!Has(KZFlags.Ritual)) return "在前厅阅读留座戏宣传册";
                if (!Has(KZFlags.Photo)) return "查看旧合照及背面文字";
                if (!Has(KZFlags.Ticket)) return "前往售票口调查残缺票根";
                if (!Has(KZFlags.PhotoAligned)) return "回到观众厅，按 F 对照旧照片与舞台立柱";
                if (!Has(KZFlags.WorkRecord)) return "阅读周明生的场务物品记录";
                if (!Has(KZFlags.RelicIdentified)) return "结合照片和记录判断七号座留下的遗物";
                if (!Has(KZFlags.EchoFinished)) return "返回第一排七座，调查钥匙曾放置的位置";
                return "第一章完成：调查笔记已记录残响";
            }
        }

        private void Awake()
        {
            Current = this;
            session = KZGameSession.EnsureExists();
            originalLampIntensity = stageLamp == null ? 0f : stageLamp.intensity;
            originalAmbient = RenderSettings.ambientLight;
            if (frontDoor != null)
            {
                closedDoorPosition = frontDoor.transform.position;
                closedDoorRotation = frontDoor.transform.rotation;
                doorPoseCaptured = true;
            }
        }

        private void Start()
        {
            if (isPrologue)
            {
                SetFrontDoorState(Has(KZFlags.Door));
                if (anomalyShadow != null)
                    anomalyShadow.SetActive(Has(KZFlags.AnomalyStarted) &&
                                            !Has(KZFlags.AnomalyResolved));
                if (programProp != null) programProp.SetActive(Has(KZFlags.AnomalyResolved));

                if (!Has(KZFlags.Worksheet))
                    KZHud.ShowPage("临槐县文化馆 · 拆除前资产清点表",
                        "2004年10月21日 17:42\n\n长明戏楼拆除前资产清点表\n\n" +
                        "□ 戏服  □ 照片  □ 磁带\n□ 乐器  □ 演出资料  □ 历史档案\n\n" +
                        "手写备注：明早施工，今晚务必完成。\n\n" +
                        "你是县文化馆资料员林砚，负责最后的资料登记。",
                        () => Mark(KZFlags.Worksheet));
                else KZHud.ShowToast("继续进行拆楼前的清点工作。");
            }
            else
            {
                if (echo1992Group != null) echo1992Group.SetActive(false);
                if (Has(KZFlags.EchoStarted) && !Has(KZFlags.EchoFinished))
                    session.RemoveFlag(KZFlags.EchoStarted); // 中断恢复，不永久卡关
                if (session.TryAddFlag(KZFlags.ChapterOneStarted))
                    KZHud.ShowPage("第一章 · 座",
                    "节目单指向1992年7月17日的留座戏。\n\n" +
                    "从前厅的民俗资料、旧合照与售票处的票根入手，" +
                    "再核对第一排中央那把椅子。");
                if (player != null && session.TryConsumeTransitionPose(out Vector3 position, out Quaternion rotation))
                    player.Teleport(position, rotation);
            }
        }

        private bool Has(string flag) { return session.HasFlag(flag); }
        private void Mark(string flag) { session.TryAddFlag(flag); }

        private bool ReadyForAnomaly()
        {
            return Has(KZFlags.Phone) && Has(KZFlags.Seat) && Has(KZFlags.Register);
        }

        public void Handle(KZInteractable item)
        {
            if (item == null || busy) return;
            switch (item.action)
            {
                case KZActionKind.Worksheet:
                    KZAudioFeedback.PlayCue(KZSoundCue.Paper);
                    KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.Worksheet));
                    break;
                case KZActionKind.FrontDoor:
                    if (!Has(KZFlags.Worksheet))
                        KZHud.ShowToast("先查看手中的清点表。");
                    else
                    {
                        if (session.TryAddFlag(KZFlags.Door))
                            StartCoroutine(OpenFrontDoor());
                        KZAudioFeedback.PlayCue(KZSoundCue.Door);
                        KZHud.ShowToast("门轴发出一声沉闷的摩擦声。你进入了戏楼。");
                    }
                    break;
                case KZActionKind.Phone:
                    KZAudioFeedback.PlayCue(KZSoundCue.Phone);
                    KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.Phone));
                    break;
                case KZActionKind.Seat07:
                    if (!isPrologue)
                    {
                        if (Has(KZFlags.RelicIdentified) && !Has(KZFlags.EchoFinished) && !busy)
                            StartCoroutine(PlayEcho());
                        else if (Has(KZFlags.EchoFinished))
                            KZHud.ShowToast("一排七座已经空了，戏楼又回到2004年。");
                        else KZHud.ShowPage(item.DisplayName, item.body);
                    }
                    else if (Has(KZFlags.AnomalyResolved) && !Has(KZFlags.Program))
                    {
                        ShowFoundProgram();
                    }
                    else
                    {
                        KZAudioFeedback.PlayCue(KZSoundCue.Paper);
                        KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.Seat));
                    }
                    break;
                case KZActionKind.Register:
                    KZAudioFeedback.PlayCue(KZSoundCue.Paper);
                    KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.Register));
                    break;
                case KZActionKind.Program:
                    if (!Has(KZFlags.AnomalyResolved))
                        KZHud.ShowToast("这里暂时还没有节目单。");
                    else ShowFoundProgram();
                    break;
                case KZActionKind.Ritual:
                    KZAudioFeedback.PlayCue(KZSoundCue.Paper);
                    KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.Ritual));
                    break;
                case KZActionKind.OldPhoto:
                    KZAudioFeedback.PlayCue(KZSoundCue.Paper);
                    KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.Photo));
                    break;
                case KZActionKind.Ticket:
                    KZAudioFeedback.PlayCue(KZSoundCue.Paper);
                    KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.Ticket));
                    break;
                case KZActionKind.SeatRecord:
                    KZAudioFeedback.PlayCue(KZSoundCue.Paper);
                    KZHud.ShowPage(item.DisplayName, item.body);
                    break;
                case KZActionKind.WorkRecord:
                    KZAudioFeedback.PlayCue(KZSoundCue.Paper);
                    KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.WorkRecord));
                    break;
                case KZActionKind.RelicDecision:
                    if (!Has(KZFlags.PhotoAligned) || !Has(KZFlags.WorkRecord))
                    {
                        KZHud.ShowToast("旧照片和场务记录还没有全部核对。");
                        return;
                    }
                    if (Has(KZFlags.RelicIdentified))
                    {
                        KZHud.ShowToast("记录已经指向周明生的钥匙。回到七号座查看。");
                        return;
                    }
                    KZHud.ShowRelicChoice(() =>
                    {
                        if (session.TryAddFlag(KZFlags.RelicIdentified))
                            KZHud.ShowToast("线索吻合：周明生留下的是戏楼钥匙。回到七号座。", 6f);
                    });
                    break;
                case KZActionKind.BackstageDoor:
                case KZActionKind.Poster:
                    KZHud.ShowPage(item.DisplayName, item.body);
                    break;
            }
        }

        private void ShowFoundProgram()
        {
            const string contents = "一九九二年七月十七日\n长明戏楼\n周明生留座戏\n\n" +
                "纸张像是刚刚被谁放在椅子上的。";
            KZAudioFeedback.PlayCue(KZSoundCue.Paper);
            if (Has(KZFlags.PrologueDone))
            {
                KZHud.ShowPage("新出现的旧节目单", contents);
                return;
            }
            KZHud.ShowPage("新出现的旧节目单", contents, () =>
            {
                Mark(KZFlags.Program);
                StartCoroutine(EnterFirstChapter());
            });
        }

        public void AudienceEntered()
        {
            if (!isPrologue || Has(KZFlags.AnomalyStarted)) return;
            if (Has(KZFlags.AudienceVisited) && Has(KZFlags.AudienceLeft) && ReadyForAnomaly())
            {
                if (session.TryAddFlag(KZFlags.AnomalyStarted))
                {
                    if (anomalyShadow != null) anomalyShadow.SetActive(true);
                    KZHud.ShowToast("观众厅暗了一瞬。第一排七座上，似乎坐着一个人……", 6f);
                }
            }
            Mark(KZFlags.AudienceVisited);
        }

        public void AudienceExited()
        {
            if (!isPrologue) return;
            if (Has(KZFlags.AudienceVisited)) Mark(KZFlags.AudienceLeft);
        }

        public void ApproachedSeat()
        {
            if (!isPrologue || busy || !Has(KZFlags.AnomalyStarted)
                || Has(KZFlags.AnomalyResolved)) return;
            StartCoroutine(ResolveAnomaly());
        }

        private IEnumerator ResolveAnomaly()
        {
            busy = true;
            KZHud.ShowToast("舞台上的灯突然闪亮，又迅速熄灭……", 2f);
            if (stageLamp != null) stageLamp.intensity = originalLampIntensity * 3f;
            yield return new WaitForSeconds(0.30f);
            if (stageLamp != null) stageLamp.intensity = 0f;
            yield return new WaitForSeconds(0.45f);
            if (anomalyShadow != null) anomalyShadow.SetActive(false);
            if (programProp != null) programProp.SetActive(true);
            if (stageLamp != null) stageLamp.intensity = originalLampIntensity;
            Mark(KZFlags.AnomalyResolved);
            busy = false;
            KZHud.ShowToast("人影消失了。座椅上多出一张节目单。", 6f);
        }

        private IEnumerator EnterFirstChapter()
        {
            busy = true;
            if (player != null) player.SetFreezeWalking(true);
            if (Application.CanStreamedLevelBeLoaded(KZSceneNames.ChapterOne))
            {
                yield return KZTitleTransition.Play(KZSceneNames.ChapterOne, () =>
                {
                    if (player != null)
                        session.SetTransitionPose(player.transform.position, player.transform.rotation);
                    Mark(KZFlags.PrologueDone);
                });
            }
            else
            {
                busy = false;
                if (player != null) player.SetFreezeWalking(false);
                KZHud.ShowToast("第一章未加入 Build Profiles 的场景列表。");
            }
        }

        private IEnumerator PlayEcho()
        {
            if (!session.TryAddFlag(KZFlags.EchoStarted)) yield break;
            busy = true;
            if (player != null)
            {
                player.SetFreezeWalking(true);
                if (echoPlayerView != null)
                    player.Teleport(echoPlayerView.position, echoPlayerView.rotation);
            }
            KZAudioFeedback.PlayCue(KZSoundCue.Gong);
            KZHud.ShowToast("咚—— 一声锣响。\n戏楼的光线慢慢消失。", 3f);
            if (stageLamp != null) stageLamp.intensity = 0f;
            yield return new WaitForSeconds(1.1f);

            // 这里是双时代模型组开关，之后可接 Unity Timeline 与 URP Volume。
            if (echo1992Group != null) echo1992Group.SetActive(true);
            if (echoKey != null) echoKey.SetActive(true);
            RenderSettings.ambientLight = new Color(.60f, .40f, .26f);
            if (stageLamp != null) stageLamp.intensity = originalLampIntensity * 2f;
            KZHud.ShowToast("【1992 · 残响】观众坐满戏楼，台上又传来唱腔。", 5f);
            yield return new WaitForSeconds(1.3f);

            if (echoActress != null && echoRoute != null && echoRoute.Length >= 2)
            {
                echoActress.position = echoRoute[0].position;
                for (int i = 1; i < echoRoute.Length; ++i)
                {
                    Vector3 from = echoRoute[i - 1].position;
                    Vector3 to = echoRoute[i].position;
                    Vector3 direction = to - from;
                    if (direction.sqrMagnitude > 0.001f)
                        echoActress.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                    float duration = Mathf.Max(0.3f, direction.magnitude / 2.1f);
                    float elapsed = 0f;
                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        echoActress.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                        yield return null;
                    }
                    echoActress.position = to;
                }
            }

            KZHud.ShowToast("穿戏服的女子在一排七座前停下，缓缓坐了下去……", 5f);
            if (echoActress != null)
            {
                Vector3 start = echoActress.position;
                Vector3 sit = start + Vector3.down * .42f;
                float elapsed = 0f;
                while (elapsed < 1.2f)
                {
                    elapsed += Time.deltaTime;
                    echoActress.position = Vector3.Lerp(start, sit, Mathf.Clamp01(elapsed / 1.2f));
                    yield return null;
                }
            }

            if (crowdHeads != null && echoActress != null)
                foreach (Transform head in crowdHeads)
                    if (head != null)
                    {
                        Vector3 towards = echoActress.position + Vector3.up * 0.9f - head.position;
                        if (towards.sqrMagnitude > 0.001f)
                            head.rotation = Quaternion.LookRotation(towards, Vector3.up);
                    }

            KZHud.ShowToast("所有观众同时转头看向她。她伸手，取走了空座上的钥匙。", 4f);
            yield return new WaitForSeconds(1.8f);
            if (echoKey != null) echoKey.SetActive(false);
            KZAudioFeedback.PlayCue(KZSoundCue.Key);
            yield return new WaitForSeconds(1.1f);

            // 无论正式 Timeline 后续怎样改，必须恢复原始时空与玩家输入。
            Restore2004();
            Mark(KZFlags.EchoFinished);
            Mark(KZFlags.ChapterOneDone);
            KZHud.ShowPage("第一章结束 · 座",
                "灯光重新亮起，戏楼又是2004年的破败模样。\n\n" +
                "你目睹一个穿戏服的女子坐上一排七座，从椅上取走钥匙。\n" +
                "这段记忆并不说明她为何这么做。\n\n" +
                "【第四周原型到此结束】第二章由后续团队任务接入。");
        }

        private void Restore2004()
        {
            if (echo1992Group != null) echo1992Group.SetActive(false);
            if (stageLamp != null) stageLamp.intensity = originalLampIntensity;
            RenderSettings.ambientLight = originalAmbient;
            if (player != null) player.SetFreezeWalking(false);
            busy = false;
        }

        private void SetFrontDoorState(bool open)
        {
            if (frontDoor == null || !doorPoseCaptured) return;
            frontDoor.SetActive(true);
            frontDoor.transform.SetPositionAndRotation(
                open ? closedDoorPosition + Vector3.right * 1.5f : closedDoorPosition,
                open ? closedDoorRotation * Quaternion.Euler(0f, 90f, 0f) : closedDoorRotation);
            foreach (Collider collider in frontDoor.GetComponentsInChildren<Collider>(true))
                collider.enabled = !open;
        }

        private IEnumerator OpenFrontDoor()
        {
            if (frontDoor == null || !doorPoseCaptured) yield break;
            Vector3 from = frontDoor.transform.position;
            Quaternion rotation = frontDoor.transform.rotation;
            float elapsed = 0f;
            while (elapsed < 0.45f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.45f);
                frontDoor.transform.SetPositionAndRotation(
                    Vector3.Lerp(from, closedDoorPosition + Vector3.right * 1.5f, t),
                    Quaternion.Slerp(rotation, closedDoorRotation * Quaternion.Euler(0f, 90f, 0f), t));
                if (t > 0.65f)
                    foreach (Collider collider in frontDoor.GetComponentsInChildren<Collider>(true))
                        collider.enabled = false;
                yield return null;
            }
            SetFrontDoorState(true);
        }

        private void OnDisable()
        {
            // 场景切换、禁用或意外中断时不要遗留锁定的玩家控制。
            Restore2004();
            if (Current == this) Current = null;
        }
    }
}
