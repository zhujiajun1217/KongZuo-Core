using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KongZuo.V03
{
    /// <summary>
    /// V0.3 narrative adapter (prologue + chapters 1–2 only).
    /// No future-plot spoilers; echoes are fragments, not omniscient truth.
    /// Does not change the team's original KZStoryDirector or LMLWeek4 sources.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KZV03Director : MonoBehaviour
    {
        public enum Part { Prologue, Seat, Person }
        public static KZV03Director Current { get; private set; }
        public Part part;
        public KZV03Player player;
        public Light stageLamp;
        public GameObject frontDoor, anomalyFigure, programProp, echoFigure, echoKey, argumentFigures, doorFigure;
        public GameObject recordPoint;
        public GameObject[] workroomDoors;
        public AudioSource ambienceSource, effectsSource;
        public AudioClip ambience2004, ambience1992, gong, telephone, paper, keySfx;
        public Transform seatView;

        private KZV03State s;
        private bool seenHall, leftHallAfterSeen, busy, sceneEnding;
        private int alignmentSteps;
        private float lampDefault, noProgressSince;
        private Color ambientDefault;
        private bool firstHintGiven, secondHintGiven;
        public bool Busy { get { return busy; } }
        public bool Finale { get; private set; }
        public string ChapterLabel { get { return part == Part.Prologue ? "序章 · 拆楼前夜" : part == Part.Seat ? "第一章 · 座" : "第二章 · 人"; } }

        public string Objective
        {
            get
            {
                if (s == null) return "剧情载入中";
                if (part == Part.Prologue)
                {
                    if (!Has(KZV03Flag.Worksheet)) return "检视拆除前资产清点表";
                    if (!Has(KZV03Flag.Door)) return "推开长明戏楼正门";
                    if (!Has(KZV03Flag.Phone)) return "去前厅西侧办公室接听座机";
                    if (!Has(KZV03Flag.Seat)) return "核查观众厅第一排七号座";
                    if (!Has(KZV03Flag.Register)) return "前厅东侧售票口核对登记表";
                    if (!Has(KZV03Flag.Anomaly)) return "从前厅再次进入观众厅，确认异样";
                    return "返回第一排七座，检视突然出现的节目单";
                }
                if (part == Part.Seat)
                {
                    if (!Has(KZV03Flag.Photo)) return "前往售票口，找到1992年的演出布置照";
                    if (!Has(KZV03Flag.Alignment)) return "去观众厅后方偏西的拍摄点完成旧照对景";
                    if (!Has(KZV03Flag.Handover)) return "在前厅找到周明生遗物点交单";
                    if (!Has(KZV03Flag.EvidenceLinked)) return "按 Tab，在笔记中关联对景照片与遗物点交单";
                    if (!Has(KZV03Flag.EchoKey)) return "重新检视七号座，确认是谁取走了铜钥匙";
                    return "第一章完成，正前往后台";
                }
                if (!Has(KZV03Flag.MaintenanceKey)) return "取左侧台工具箱中的2004年维修钥匙";
                if (!Has(KZV03Flag.WorkroomOpen)) return "用维修钥匙打开化妆间与服装间工作区";
                if (!Has(KZV03Flag.Identity)) return "进入化妆间，查看带靛蓝滚边的沈芸剧照";
                if (!Has(KZV03Flag.LifePhoto)) return "检查化妆间里沈芸与顾兰的小桥生活照";
                if (!Has(KZV03Flag.Travel)) return "找到旧节目册中的1992年购票凭证";
                if (!Has(KZV03Flag.Letter)) return "检查化妆间未寄出的信";
                if (!Has(KZV03Flag.Argument)) return "查看旧红线，听完不完整的争执残响";
                if (!Has(KZV03Flag.XuNote)) return "服装间寻找许承山工作服中的纸条";
                if (!Has(KZV03Flag.ChestLabel)) return "服装间核对旧戏箱的钥匙登记标签";
                if (!Has(KZV03Flag.EastDoorEcho)) return "沿后台通廊向东，检查旧工作门（非B1楼梯）";
                if (!Has(KZV03Flag.DoorRecorded)) return "再看门内侧，拍下门闩和旧划痕";
                return "调查记录完成 · 未完待续";
            }
        }

        private void Awake()
        {
            Current = this;
            s = KZV03State.Ensure();
            lampDefault = stageLamp == null ? 0f : stageLamp.intensity;
            ambientDefault = RenderSettings.ambientLight;
            s.Changed += OnProgress;
            noProgressSince = Time.unscaledTime;
        }

        private void Start()
        {
            if (part == Part.Prologue)
            {
                s.ResetProgress();
                if (anomalyFigure != null) anomalyFigure.SetActive(false);
                if (programProp != null) programProp.SetActive(false);
                Page("临槐县文化馆 · 长明戏楼清点单",
                    "2004年10月21日 17:42，细雨。\n\n戏服／旧照片／磁带／乐器／档案。\n\n明早施工，今晚务必完成。\n\n林砚：‘先把能留下的东西记清楚。’\n\nWASD移动、鼠标观察、E检视、Tab打开调查笔记。",
                    KZV03Flag.Worksheet);
            }
            else if (part == Part.Seat)
            {
                if (echoFigure != null) echoFigure.SetActive(false);
                if (echoKey != null) echoKey.SetActive(false);
                Page("第一章 · 座",
                    "节目单背面有手写数字‘17’。七号座曾放过什么？\n\n这不是要猜一个座位谜题：林砚需要核对旧照片和当年的遗物清点记录。\n\n旧照片可在售票口寻找。", null);
            }
            else
            {
                if (argumentFigures != null) argumentFigures.SetActive(false);
                if (doorFigure != null) doorFigure.SetActive(false);
                if (recordPoint != null) recordPoint.SetActive(false);
                Page("第二章 · 人",
                    "舞台侧幕后传来木头轻碰声。林砚沿左侧台进入后台通廊。\n\n后台北墙依次为化妆间、服装间、档案室和配电室；本章只开放前两间。\n\n临时维修钥匙是2004年施工人员留下的，绝非1992年周明生的铜钥匙。", null);
            }
            if (ambienceSource != null && ambience2004 != null)
            {
                ambienceSource.clip = ambience2004;
                ambienceSource.loop = true;
                ambienceSource.Play();
            }
        }

        private void OnDestroy()
        {
            if (s != null) s.Changed -= OnProgress;
            RestoreNormal();
            if (Current == this) Current = null;
        }

        private void OnProgress(string _) { noProgressSince = Time.unscaledTime; firstHintGiven = secondHintGiven = false; }
        private bool Has(string key) { return s != null && s.Has(key); }
        private void Add(string key) { if (s != null) s.Add(key); }
        private void Page(string title, string contents, string flag)
        {
            KZV03Hud.ShowPage(title, contents, flag == null ? (System.Action)null : () => Add(flag));
            if (effectsSource != null && paper != null) effectsSource.PlayOneShot(paper, .38f);
        }

        private void Update()
        {
            if (part == Part.Prologue && player != null && !Has(KZV03Flag.Anomaly))
            {
                float z = player.transform.position.z;
                if (z > -7f) seenHall = true;
                if (seenHall && z < -11f) leftHallAfterSeen = true;
                if (leftHallAfterSeen && z > -7f && s.HasAll(KZV03Flag.Phone, KZV03Flag.Seat, KZV03Flag.Register))
                    StartCoroutine(PrologueAnomaly());
            }
            if (busy || KZV03Hud.IsModal || sceneEnding) return;
            float elapsed = Time.unscaledTime - noProgressSince;
            if (elapsed > 120f && !secondHintGiven)
            {
                secondHintGiven = true;
                KZV03Hud.Toast("笔记线索提示：" + Objective, 7f);
            }
            else if (elapsed > 70f && !firstHintGiven)
            {
                firstHintGiven = true;
                KZV03Hud.Toast("可按 Tab 查看已知事实。当前目标：" + Objective, 7f);
            }
        }

        public void EvidenceLinked()
        {
            if (part == Part.Seat && Has(KZV03Flag.EvidenceLinked))
                KZV03Hud.Toast("已核对：一排七座放过周明生的铜钥匙。\n下一步：再看七号座。", 6f);
        }

        public void Interact(string id)
        {
            if (string.IsNullOrEmpty(id) || busy || KZV03Hud.IsModal || sceneEnding) return;
            switch (id)
            {
                // 序章：民俗是传言，火灾失踪与‘离镇’是报道，不是确定的离去。
                case "p_worksheet":
                    Page("拆除前资产清点表", "临槐县文化馆：明早施工，今晚登记戏服、旧照片、磁带、乐器和历史资料。", KZV03Flag.Worksheet); break;
                case "p_door":
                    if (!Has(KZV03Flag.Worksheet)) { KZV03Hud.Toast("先确认今晚的清点任务。"); return; }
                    Add(KZV03Flag.Door);
                    if (frontDoor != null) frontDoor.SetActive(false);
                    KZV03Hud.Toast("沉重的木门吱呀打开，潮湿的前厅在面前展开。"); break;
                case "p_phone":
                    if (!Has(KZV03Flag.Door)) { KZV03Hud.Toast("先进入戏楼。"); return; }
                    Sfx(telephone);
                    Page("办公室座机 · 何文斌来电",
                        "何文斌：‘到了？’\n林砚：‘刚到。档案柜在哪边？’\n何文斌：‘办公室有一部分，后台也堆着些。照表登记就行。’\n林砚：‘1992年的资料也要登记吗？’\n何文斌：‘题名、数量，能看清就记。后台木头朽得厉害，别乱钻。’\n林砚：‘明白。’\n何文斌：‘林砚……别把自己弄得太晚。’\n\n剪报只是推测沈芸可能已离镇，绝非确证。", KZV03Flag.Phone); break;
                case "p_seat":
                    Page("第一排七座", "中央座席坐面意外干净。它正对戏台，但通往它的路在左右两侧，没有贯穿座席中央的纵向通道。\n\n林砚：‘那张椅子为什么连灰都没有？’", KZV03Flag.Seat); break;
                case "p_register":
                    Page("1992年座位登记表", "一排七座——空。\n\n‘空’是一条历史登记记录，尚不能解释为什么保留这张椅子。", KZV03Flag.Register); break;
                case "p_folklore":
                    Page("《留座戏》宣传册 · 可选", "旧戏班为逝者唱戏，第一排中央留出一席，遗物暂置其上，散场三声锣之前不得占座或取物。\n\n手写批注：‘活人占座，死人带走。’\n\n【注意：当地传言，不等于世界规则已证实。】", KZV03Flag.Folklore); break;
                case "p_archive":
                    Page("1992年火灾档案移交目录 · 可选",
                        "封面有何文斌的移交签字；题名与数量可辨，未写明起火原因或调查结论。", null); break;
                case "p_poster":
                    Page("戏楼旧剪报 · 可选", "1992年火灾，演员沈芸失踪。旧报纸写‘据其离镇意愿推测，或已离开’。\n\n这不等于已有登车或离镇记录。", null); break;
                case "p_program":
                    if (!Has(KZV03Flag.Anomaly)) { KZV03Hud.Toast("七号座上还没有这张纸。"); return; }
                    KZV03Hud.ShowPage("忽然出现的节目单", "1992年7月17日｜长明戏楼｜周明生留座戏\n\n纸背：手写‘17’和一片墨渍。\n\n林砚：‘刚才……明明有人。’", () =>
                    {
                        Add(KZV03Flag.Program);
                        StartCoroutine(NextPart(KZV03Scenes.Seat, "《空座》\n第一章 · 座"));
                    }); break;

                // 第一章：照片 + 点交单，而不是已过时的‘排座’谜题。
                case "c1_program":
                    Page("节目单背面的字", "手写‘17’，墨渍遮住了一角。林砚需要核对当年的物件登记。", null); break;
                case "c1_photo":
                    Page("演出布置核对照 · 1992年7月17日",
                        "相片从观众厅后方偏西拍摄：台口立柱、右侧梯、第一排椅背清晰。\n七号座上有一个被布角挡住的金属轮廓。\n背面：‘开戏前核对，前排中座勿动。’", KZV03Flag.Photo); break;
                case "c1_align": AlignPhoto(); break;
                case "c1_handover":
                    Page("《遗物陈设点交单》", "旧铜钥匙一串／演前暂陈第一排七座／散场三声锣后归还库房。\n\n领回人签字处被水渍浸坏。\n\n单据内容本身不能说明谁后来领取了钥匙。", KZV03Flag.Handover); break;
                case "c1_zhou_photo":
                    Page("场务合照 · 可选", "周明生（1972—1992），年轻场务。\n背面：‘明生管后台戏箱，钥匙总挂腰上。’\n\n这并不告诉玩家戏箱里是什么。", null); break;
                case "c1_seat":
                    if (Has(KZV03Flag.EchoKey)) { KZV03Hud.Toast("残响已结束：女子取走了铜钥匙；身份仍需确认。"); break; }
                    if (!s.HasAll(KZV03Flag.Alignment,KZV03Flag.EvidenceLinked))
                    { KZV03Hud.Toast("先对齐旧照片，并在 Tab 笔记中关联点交单。", 6f); return; }
                    StartCoroutine(FirstEcho()); break;

                // 第二章：人物信息优先，现实物证核对残响。
                case "c2_maintenance":
                    Page("2004年临时维修钥匙", "侧台工具箱里有一把施工维修钥匙，用于开启普通工作区。\n\n它与1992年周明生放在七号座上的铜钥匙不是同一件。", KZV03Flag.MaintenanceKey); break;
                case "c2_workroom_door":
                    if (!Has(KZV03Flag.MaintenanceKey)) { KZV03Hud.Toast("先取左侧台工具箱里的普通维修钥匙。"); return; }
                    Add(KZV03Flag.WorkroomOpen);
                    if (workroomDoors != null)
                        foreach (GameObject workroomDoor in workroomDoors)
                            if (workroomDoor != null) workroomDoor.SetActive(false);
                    KZV03Hud.Toast("普通工作区的两扇门打开了。档案室、配电室和东门仍保持封闭。", 6f);
                    break;
                case "c2_roster":
                    Page("1992年演员排班表 · 可选",
                        "化妆间门边的排班表依稀可见‘沈芸’和‘顾兰’。\n\n这些名字属于曾在这里生活、排练和演出的真实人。", null); break;
                case "c2_portrait":
                    Page("沈芸 · 化妆镜里的旧剧照", "背面：‘沈芸／长明戏班／1992’。\n戏服袖缘的靛蓝滚边，与七号座残响中取钥匙女子完全一致。\n\n林砚：‘沈芸……原来是她。’", KZV03Flag.Identity); break;
                case "c2_life_photo":
                    Page("沈芸与顾兰 · 小桥生活照", "两人站在镇外小桥上，穿常服，头发被风吹乱。\n背面，沈芸写：‘阿兰，明年一起去看看真正的江。’\n顾兰答：‘说话算话。’", KZV03Flag.LifePhoto); break;
                case "c2_everyday":
                    Page("化妆间的日常 · 可选", "眉笔盒：‘借笔先问我。’\n节目单背面记录零散伙食账。旧杂志里折了几页江州地图。\n\n戏班是这些人曾经生活的地方。", null); break;
                case "c2_booklet_hint":
                    Page("抽屉夹着的便签", "‘那本旧节目册别扔，凭证在里面。’\n\n化妆台底层，放大镜旁的布袋里可能还有东西。", null); break;
                case "c2_travel":
                    Page("旧节目册内 · 购票凭证", "1992年7月17日｜临槐—江州｜23:40。\n\n沈芸旅费账：‘票买了。路费还差三块。那也得去。’\n\n林砚：‘她连票都买好了。’\n\n【已证：计划离镇。未证：是否真正登车。凭证不等于车票或乘车记录。】", KZV03Flag.Travel); break;
                case "c2_letter":
                    Page("沈芸未寄出的信", "‘承山说外面不见得好，我知道。可是好不好，总该我自己看看。’\n\n她打算离开不是一时冲动。", KZV03Flag.Letter); break;
                case "c2_red_thread":
                    if (!Has(KZV03Flag.Letter)) { KZV03Hud.Toast("先看看桌上的那封未寄出的信。"); return; }
                    if (Has(KZV03Flag.Argument)) { KZV03Hud.Toast("残响只截取争执片段，后来发生了什么仍然未知。"); return; }
                    StartCoroutine(ArgumentEcho()); break;
                case "c2_xu_note":
                    Page("许承山工作服内袋的折纸", "‘芸：换幕的时候你从化妆间把包拿过来，我先去把东西放好。十一点，后门碰头，我们一道去车站。——承山。’\n\n这是共同离开的计划，并不能抹去他此前抓腕的越界行为。", KZV03Flag.XuNote); break;
                case "c2_chest_label":
                    Page("1992年旧戏箱标签", "‘地下道具仓／旧铁锁戏箱／保管钥匙：周明生。’\n\n遗物钥匙可能有实际用途；但戏箱里有什么，在本阶段仍未知。\n\nB1封闭，不进入。", KZV03Flag.ChestLabel); break;
                case "c2_gu_sleeve":
                    Page("顾兰的戏衣 · 可选", "空纸套封面写：‘先放我这儿，别生气。’\n旁边唱段草稿：‘人走，戏可以接着唱。’\n顾兰圈了两遍：‘那我呢？’\n\n不能因此断言顾兰拿过车票。", KZV03Flag.GuSleeve); break;
                case "c2_missing_ticket":
                    Page("化妆台边的潦草便签 · 可选", "‘车票怎么不见了？’\n\n无署名，纸张与沈芸记账用的一样。\n\n尚不能据此判断是谁拿走了票。", KZV03Flag.MissingTicket); break;
                case "c2_east_route":
                    Page("后台工作路线图", "通廊东侧门通往戏楼外窄巷，供搬运道具及临时疏散使用。\n\n图中没有记录谁曾锁门。\n\n西端通向B1的楼梯已封闭，和东侧门不是同一条路。", null); break;
                case "c2_locked_room":
                    Page("暂未开放的区域", "档案室、配电室和通往B1的楼梯只开放外观检视，现阶段不进入。", null); break;
                case "c2_east_door":
                    if (Has(KZV03Flag.EastDoorEcho)) { KZV03Hud.Toast("门内侧还留着划痕。请用清点相机记录。",5f); return; }
                    if (!s.HasAll(KZV03Flag.Identity, KZV03Flag.LifePhoto, KZV03Flag.Travel, KZV03Flag.XuNote, KZV03Flag.ChestLabel))
                    { KZV03Hud.Toast("先核对沈芸剧照与生活照、购票凭证、许承山纸条和旧戏箱钥匙登记。", 7f); return; }
                    StartCoroutine(EastDoorEcho()); break;
                case "c2_record":
                    if (!Has(KZV03Flag.EastDoorEcho)) { KZV03Hud.Toast("现在还没有需要记录的异常。"); return; }
                    KZV03Hud.ShowPage("东侧工作门 · 清点相机记录",
                        "取景：门内侧锁座、烧黑的门框、旧摩擦痕。\n\n快门瞬间似有模糊手影；照片预览却只有真实的门锁与划痕。\n\n【新事实：起火后沈芸疑似曾在门内侧尝试开门，却没能立刻打开。】\n\n不能确定锁门者、火源、最终去向或沈芸是否遇难。", () =>
                    {
                        Add(KZV03Flag.DoorRecorded);
                        EndDemo();
                    }); break;
                default:
                    Debug.LogWarning("[空座V0.3] 未注册交互ID: " + id);
                    KZV03Hud.Toast("待接入的调查物件：" + id); break;
            }
        }

        private void AlignPhoto()
        {
            if (!Has(KZV03Flag.Photo)) { KZV03Hud.Toast("先在售票口拿到旧照片。"); return; }
            if (Has(KZV03Flag.Alignment)) { KZV03Hud.Toast("对景已完成：椅面确实放过金属物件。"); return; }
            alignmentSteps++;
            if (alignmentSteps == 1) { KZV03Hud.Toast("舞台立柱对上了。再观察右侧梯与椅背。", 5f); return; }
            if (alignmentSteps == 2) { KZV03Hud.Toast("椅背位置快对上了。再微调角度，按 E 再核对一次。", 5f); return; }
            Page("旧照片对景完成", "旧照片与2004年现场大致吻合。\n布角下的金属影子落在七号座椅面。\n\n林砚：‘那张空座上，原来确实放过东西。’\n\n物品种类还须核对原始点交记录。", KZV03Flag.Alignment);
        }

        private IEnumerator PrologueAnomaly()
        {
            busy = true;
            Add(KZV03Flag.Anomaly);
            if (anomalyFigure != null) anomalyFigure.SetActive(true);
            KZV03Hud.Toast("大厅中央……有个穿旧戏服的人影坐在七号座上。", 4f);
            yield return new WaitForSeconds(.9f);
            if (stageLamp != null) stageLamp.intensity = lampDefault * 2.5f;
            yield return new WaitForSeconds(.3f);
            if (stageLamp != null) stageLamp.intensity = 0f;
            if (anomalyFigure != null) anomalyFigure.SetActive(false);
            if (programProp != null) programProp.SetActive(true);
            yield return new WaitForSeconds(.45f);
            if (stageLamp != null) stageLamp.intensity = lampDefault;
            busy = false;
            KZV03Hud.Toast("人影消失了；座位上多出一张泛黄节目单。", 6f);
        }

        private IEnumerator FirstEcho()
        {
            busy = true;
            if (player != null) player.FreezeMovement(true);
            if (seatView != null && player != null) player.Teleport(seatView.position, seatView.rotation);
            Sfx(gong);
            yield return Moment("1992 · 留座戏残响", "一声锣响。近处椅背恢复旧时光泽，台上唱腔隐约传来。\n\n七号座空着，上面放着一串铜钥匙。\n\n残响只能显示那一晚发生过的局部动作，不能解释动机。");
            if (echoFigure != null) echoFigure.SetActive(true);
            if (echoKey != null) echoKey.SetActive(true);
            if (ambienceSource != null && ambience1992 != null) { ambienceSource.clip=ambience1992; ambienceSource.Play(); }
            RenderSettings.ambientLight = new Color(.62f, .47f, .30f);
            if (stageLamp != null) stageLamp.intensity = lampDefault * 1.7f;
            if (echoFigure != null)
            {
                Vector3 from = echoFigure.transform.position;
                Vector3 to = new Vector3(0f, .9f, 3.5f);
                float t = 0f;
                while (t < 2.4f)
                {
                    t += Time.deltaTime;
                    echoFigure.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t/2.4f));
                    yield return null;
                }
            }
            yield return Moment("一排七座", "穿旧戏服、袖缘带靛蓝滚边的年轻女子沿左侧台走来。\n她坐上空座，伸向铜钥匙。\n\n邻座老人：‘姑娘，散场锣还没敲，不能碰那个。’\n女人：‘我知道。’");
            if (echoKey != null) echoKey.SetActive(false);
            Sfx(keySfx);
            yield return Moment("残响消散", "她把钥匙收进袖中，起身从侧台离开。\n\n林砚：‘那不是有人等她来……是她自己拿走了钥匙。’\n\n是谁？为什么需要它？都还不能断言。");
            Add(KZV03Flag.EchoKey);
            RestoreNormal();
            StartCoroutine(NextPart(KZV03Scenes.Person,"第二章 · 人"));
        }

        private IEnumerator ArgumentEcho()
        {
            busy = true;
            if (player != null) player.FreezeMovement(true);
            if (argumentFigures != null) argumentFigures.SetActive(true);
            yield return Moment("1992 · 化妆间残响", "沈芸正收拾布包，许承山闯入。\n\n许承山：‘你不能现在走。’\n沈芸：‘你不是说会和我一起吗？’\n许承山：‘我知道。可外面还有人，再等一会儿。’");
            yield return Moment("被截断的争执", "沈芸：‘你是不是也觉得，我不该走？’\n许承山抓住她的手腕：‘我不是那个意思。’\n沈芸挣开：‘那就放开。你不能替我决定。’\n\n残响在此截断。我们不知道他的下一步动作或剩余谈话。");
            Add(KZV03Flag.Argument);
            if (argumentFigures != null) argumentFigures.SetActive(false);
            RestoreNormal();
            KZV03Hud.Toast("可以按 Tab 记录暂时假设；没有唯一‘正确’人物动机答案。", 7f);
        }

        private IEnumerator EastDoorEcho()
        {
            busy = true;
            if (player != null) player.FreezeMovement(true);
            Sfx(gong);
            yield return Moment("后台东侧工作门", "门闩‘咔’地响了一声。\n\n这扇门通向外面的窄巷，与西侧已封闭的B1楼梯没有关系。\n\n门内侧传出急促的拍击声。林砚：‘谁？’");
            if (doorFigure != null) doorFigure.SetActive(true);
            if (stageLamp != null) stageLamp.intensity = lampDefault * .45f;
            yield return Moment("1992 · 起火后 · 短残响", "烟气、脚步、远处惊乱的人声。\n\n一个靛蓝滚边袖缘的女子冲到东侧门前，从内侧拧门闩。\n\n女人：‘开门！’\n女人：‘有人吗？开门啊！’\n\n门没有动。她向舞台方向看去，后续画面戛然而止。");
            if (doorFigure != null) doorFigure.SetActive(false);
            Add(KZV03Flag.EastDoorEcho);
            if (recordPoint != null) recordPoint.SetActive(true);
            RestoreNormal();
            KZV03Hud.Toast("回到2004年。请用清点相机拍下门内侧锁座与旧划痕。", 7f);
        }

        private IEnumerator Moment(string title, string body)
        {
            KZV03Hud.ShowPage(title, body);
            while (KZV03Hud.IsModal) yield return null;
        }

        private IEnumerator NextPart(string sceneName, string chapterTitle)
        {
            if (sceneEnding) yield break;
            sceneEnding = true;
            busy = true;
            if (player != null) player.FreezeMovement(true);
            KZV03Hud.Toast(chapterTitle, 2.5f);
            yield return new WaitForSeconds(1f);
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                busy = sceneEnding = false;
                if (player != null) player.FreezeMovement(false);
                KZV03Hud.Toast("找不到下一章：请先用 空座/V0.3 菜单生成场景并添加至 Build Profiles。", 9f);
                yield break;
            }
            SceneManager.LoadScene(sceneName);
        }

        private void EndDemo()
        {
            if (sceneEnding) return;
            sceneEnding = true;
            KZV03Hud.ShowPage("那一晚，门没有为她打开。",
                "【已确认】沈芸当晚计划离镇，曾取走周明生的钥匙；火灾发生后，\n她疑似曾在后台东侧工作门内侧试图开门，却没能立刻打开。\n\n【待查】门为什么打不开？她后来去了哪里？\n\n《空座》· 未完待续\n\n—— 本阶段不开放第三章、B1返场道与最终真相。", null);
        }

        private void Sfx(AudioClip clip) { if (effectsSource != null && clip != null) effectsSource.PlayOneShot(clip); }
        private void RestoreNormal()
        {
            if (stageLamp != null) stageLamp.intensity = lampDefault;
            RenderSettings.ambientLight = ambientDefault;
            if (echoFigure != null) echoFigure.SetActive(false);
            if (echoKey != null) echoKey.SetActive(false);
            if (argumentFigures != null) argumentFigures.SetActive(false);
            if (doorFigure != null) doorFigure.SetActive(false);
            if (ambienceSource != null && ambience2004 != null && ambienceSource.clip != ambience2004)
            { ambienceSource.clip = ambience2004; ambienceSource.Play(); }
            if (player != null) player.FreezeMovement(false);
            busy = false;
        }
    }

    public static class KZV03Scenes
    {
        public const string Prologue = "V03_00_Prologue";
        public const string Seat = "V03_01_Seat";
        public const string Person = "V03_02_Person";
        public const string Folder = "Assets/KongZuo/V03/Scenes";
    }
}
