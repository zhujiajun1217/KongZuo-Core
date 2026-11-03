using System;
using System.Collections;
using UnityEngine;

namespace KongZuo.LMLWeek4
{
    /// <summary>
    /// Self-contained P0 demonstration of the V0.4 evidence -> photograph -> relic -> seat -> echo chain.
    /// Does not change KZStoryDirector, KZHud, KZFlags, or official scenes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KZW4DemoDirector : MonoBehaviour
    {
        private enum Overlay { None, Document, Photo, Relic, Volumes }

        [Header("Puzzle")]
        public KZW4PuzzleDefinition definition;
        public KZW4DemoPlayer player;
        public Camera viewCamera;
        public Transform photoVantage;
        public Transform[] photoLandmarks;
        [Min(.1f)] public float photoPositionTolerance = 1.35f;
        [Range(.005f, .25f)] public float photoViewportTolerance = .055f;
        [Range(.1f, 3f)] public float photoHoldSeconds = .65f;

        [Header("Echo")]
        public KZW4AudioManager audioManager;
        public GameObject echo1992Group;
        public Transform actress;
        public Transform[] actressRoute;
        public Transform[] crowdHeads;
        public GameObject keyProp;
        public Light stageLight;

        private KZW4PuzzleState state;
        private Overlay overlay;
        private KZW4Evidence pendingClue;
        private string pageTitle = "";
        private string pageContents = "";
        private int selectedChoice = 0;
        private string feedback = "";
        private string worldHint = "";
        private string toast = "";
        private float toastExpires;
        private float stablePhotoTime;
        private float photoError;
        private bool photoMatched;
        private bool preview1992;
        private float fade;
        private string subtitle = "";
        private float subtitleExpires;
        private bool echoBusy;
        private float originalStageIntensity;
        private Color originalAmbientColor;
        private Vector3 actressInitialPosition;
        private Quaternion actressInitialRotation;
        private Quaternion[] crowdInitialRotations;

        public bool EchoBusy { get { return echoBusy; } }
        public bool BlocksCamera { get { return echoBusy || overlay == Overlay.Document ||
            overlay == Overlay.Relic || overlay == Overlay.Volumes; } }
        public bool IsOverlayActive { get { return overlay != Overlay.None; } }
        public bool IsCompleted { get { return state != null && state.EchoCompleted; } }

        private void Start()
        {
            state = new KZW4PuzzleState(definition != null && definition.IsValid()
                ? definition.correctChoice : 1);
            if (definition != null && !definition.IsValid())
                Debug.LogError("[空座/LML] 遗物配置必须提供三个选项且正确选项为1..3。", this);
            originalAmbientColor = RenderSettings.ambientLight;
            originalStageIntensity = stageLight == null ? 0f : stageLight.intensity;
            actressInitialPosition = actress == null ? Vector3.zero : actress.position;
            actressInitialRotation = actress == null ? Quaternion.identity : actress.rotation;
            crowdInitialRotations = new Quaternion[crowdHeads == null ? 0 : crowdHeads.Length];
            for (int i = 0; i < crowdInitialRotations.Length; i++)
                crowdInitialRotations[i] = crowdHeads[i] == null ? Quaternion.identity : crowdHeads[i].rotation;
            if (viewCamera == null && player != null) viewCamera = player.eyeCamera;
            if (echo1992Group != null) echo1992Group.SetActive(false);
            if (audioManager != null)
            {
                audioManager.SubtitleRequested += ShowSubtitle;
                audioManager.SwitchAmbience("ambience.2004");
            }
            Toast("先阅读留座戏资料、旧照片、场务记录，再去观众厅找照片机位。", 7f);
        }

        private void OnDestroy()
        {
            if (audioManager != null) audioManager.SubtitleRequested -= ShowSubtitle;
        }

        private void OnDisable()
        {
            // Domain reloads can call OnDisable in Edit Mode: never mutate a saved scene then.
            if (!Application.isPlaying || state == null) return;
            Restore2004();
            state.EndEcho(false);
        }

        private void Update()
        {
            if (state == null || viewCamera == null) return;
            if (echoBusy) return;

            if ((overlay == Overlay.None || overlay == Overlay.Volumes) && KZW4KeyInput.F10())
            {
                SetOverlay(overlay == Overlay.Volumes ? Overlay.None : Overlay.Volumes);
                return;
            }
            if (overlay == Overlay.Volumes)
            {
                if (KZInput.Escape()) { SetOverlay(Overlay.None); return; }
                if (KZW4KeyInput.M())
                {
                    preview1992 = !preview1992;
                    if (audioManager != null)
                        audioManager.SwitchAmbience(preview1992 ? "ambience.1992" : "ambience.2004");
                }
                return;
            }
            if (overlay == Overlay.Document)
            {
                if (KZInput.ConsumeInteract() || KZInput.Escape())
                {
                    if (pendingClue != KZW4Evidence.None) state.Collect(pendingClue);
                    pendingClue = KZW4Evidence.None;
                    SetOverlay(Overlay.None);
                }
                return;
            }
            if (overlay == Overlay.Relic)
            {
                if (KZInput.Escape() || KZInput.ConsumeInteract()) { SetOverlay(Overlay.None); return; }
                for (int i = 0; i < 3; i++)
                    if (KZInput.Number(i)) selectedChoice = i + 1;
                if (KZInput.Confirm()) SubmitRelic();
                return;
            }
            if (overlay == Overlay.Photo)
            {
                if (KZInput.Escape() || KZW4KeyInput.F()) { SetOverlay(Overlay.None); return; }
                photoMatched = KZW4PhotoAlignment.Matches(viewCamera, photoVantage, photoLandmarks,
                    photoPositionTolerance, photoViewportTolerance, out photoError);
                stablePhotoTime = photoMatched ? stablePhotoTime + Time.deltaTime : 0f;
                if (stablePhotoTime >= photoHoldSeconds && state.MarkPhotoAligned())
                {
                    if (audioManager != null) audioManager.PlaySfx("puzzle.ok");
                    SetOverlay(Overlay.None);
                    Toast("照片对位完成：遗物确实摆在第一排七座。按 G 结合记录推理。", 6f);
                }
                return;
            }

            if (KZInput.Escape())
            {
                KZW4DemoPlayer.LockCursor(false);
                return;
            }
            if (KZW4KeyInput.R()) { ResetDemo(); return; }
            if (KZW4KeyInput.M())
            {
                preview1992 = !preview1992;
                if (audioManager != null)
                    audioManager.SwitchAmbience(preview1992 ? "ambience.1992" : "ambience.2004");
                Toast(preview1992 ? "仅试听1992年的环境音（未触发剧情）" : "恢复2004年的环境音", 3f);
            }
            if (KZW4KeyInput.F())
            {
                if (!state.HasPhoto) { Toast("先阅读售票处的1992年布置照片。", 3f); return; }
                if (state.PhotoAligned) { Toast("照片已经对位完成。", 3f); return; }
                stablePhotoTime = 0f;
                SetOverlay(Overlay.Photo);
                return;
            }
            if (KZW4KeyInput.G()) { OpenRelic(); return; }

            worldHint = "";
            if (Cursor.lockState != CursorLockMode.Locked) return;
            RaycastHit hit;
            if (Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward,
                out hit, 3.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                KZW4DemoInteractable item = hit.collider.GetComponentInParent<KZW4DemoInteractable>();
                if (item != null)
                {
                    worldHint = "[E] " + item.displayName;
                    if (KZInput.ConsumeInteract()) Interact(item);
                }
            }
        }

        private void Interact(KZW4DemoInteractable item)
        {
            if (audioManager != null && item.kind != KZW4DemoInteraction.SeatSeven)
                audioManager.PlaySfx("paper");
            switch (item.kind)
            {
                case KZW4DemoInteraction.Ritual:
                    OpenPage(item.displayName, item.contents, KZW4Evidence.Ritual);
                    break;
                case KZW4DemoInteraction.Photo:
                    OpenPage(item.displayName, item.contents, KZW4Evidence.PlacementPhoto);
                    break;
                case KZW4DemoInteraction.WorkRecord:
                    OpenPage(item.displayName, item.contents, KZW4Evidence.WorkRecord);
                    break;
                case KZW4DemoInteraction.Phone:
                    OpenPage(item.displayName, item.contents, KZW4Evidence.None);
                    if (audioManager != null)
                    {
                        audioManager.PlaySfx("phone");
                        audioManager.PlayVoice("voice.phone", "何文斌：今晚只登记数量，后台不要乱进。", 5f);
                    }
                    break;
                case KZW4DemoInteraction.SeatSeven:
                    if (state.EchoCompleted)
                        OpenPage("第一排七座", "残响结束后，座椅又回到2004年的样子。你只知道沈芸取走了周明生的钥匙，并不知道她的动机。", KZW4Evidence.None);
                    else if (!state.Solved)
                        OpenPage("第一排七座", "椅面异常干净，却没有钥匙。查看留座戏资料和旧照片，确认1992年这里原来放着什么。", KZW4Evidence.None);
                    else if (state.TryBeginEcho())
                        StartCoroutine(PlayEcho());
                    break;
            }
        }

        private void OpenPage(string title, string contents, KZW4Evidence clue)
        {
            pendingClue = clue;
            pageTitle = title;
            pageContents = contents;
            SetOverlay(Overlay.Document);
        }

        private void OpenRelic()
        {
            if (!state.HasAllEvidence)
            {
                Toast("线索不全：请先阅读宣传册、布置照和场务记录。", 4f);
                return;
            }
            if (!state.PhotoAligned)
            {
                Toast("还不能确定照片中的物件位置。按 F 举起旧照片到观众厅对位。", 4f);
                return;
            }
            if (state.Solved) { Toast("已确认周明生的遗物。回第一排七座按 E。", 4f); return; }
            selectedChoice = 0;
            feedback = "选择证据支持的遗物；错误可继续作答。";
            SetOverlay(Overlay.Relic);
        }

        private void SubmitRelic()
        {
            KZW4AnswerResult result = state.Submit(selectedChoice);
            switch (result)
            {
                case KZW4AnswerResult.InvalidChoice:
                    feedback = "先按 1 / 2 / 3 选择一件物品。";
                    break;
                case KZW4AnswerResult.Wrong:
                    feedback = definition == null ? "请再核对证据。" : definition.wrongFeedback;
                    if (audioManager != null) audioManager.PlaySfx("puzzle.fail");
                    break;
                case KZW4AnswerResult.Correct:
                    if (audioManager != null) audioManager.PlaySfx("puzzle.ok");
                    SetOverlay(Overlay.None);
                    Toast(definition == null ? "正确。请返回一排七座。" : definition.correctFeedback + " 返回七号座按 E。", 7f);
                    break;
                default:
                    feedback = "还没有满足判定条件。";
                    break;
            }
        }

        private void SetOverlay(Overlay next)
        {
            overlay = next;
            stablePhotoTime = 0f;
            worldHint = "";
            KZW4DemoPlayer.LockCursor(next != Overlay.Volumes);
        }

        private void ShowSubtitle(string text, float seconds)
        {
            subtitle = text;
            subtitleExpires = Time.unscaledTime + seconds;
        }

        private void Toast(string text, float seconds)
        {
            toast = text;
            toastExpires = Time.unscaledTime + seconds;
        }

        private string Objective()
        {
            if (!state.HasAllEvidence) return "阅读：留座戏宣传册、1992布置照、周明生场务记录";
            if (!state.PhotoAligned) return "进入观众厅按 F 举照，找出旧照拍摄机位（匹配三处建筑标记）";
            if (!state.Solved) return "按 G 结合旧照片和场务记录判断遗物";
            if (!state.EchoCompleted) return "回到第一排七座按 E，观察1992年残响";
            return "第四周演示完成：残响已结束，2004年恢复，可按 R 重测";
        }

        private IEnumerator PlayEcho()
        {
            echoBusy = true;
            bool finished = false;
            SetOverlay(Overlay.None);
            try
            {
                if (audioManager != null) audioManager.PlaySfx3D("gong", keyProp == null ? transform.position : keyProp.transform.position);
                Toast("咚——灯灭，十二年前的声音渐渐回来了……", 4f);
                yield return FadeTo(1f, .70f);
                if (echo1992Group != null) echo1992Group.SetActive(true);
                if (keyProp != null) keyProp.SetActive(true);
                RenderSettings.ambientLight = new Color(.58f, .36f, .24f);
                if (stageLight != null) stageLight.intensity = originalStageIntensity * 1.7f;
                if (audioManager != null) audioManager.SwitchAmbience("ambience.1992", .6f);
                yield return FadeTo(0f, .60f);
                Toast("【1992】观众面向舞台，沈芸从侧台缓缓走下。", 5f);
                if (actress != null && actressRoute != null)
                {
                    for (int i = 0; i < actressRoute.Length; i++)
                    {
                        Transform target = actressRoute[i];
                        if (target == null) continue;
                        yield return MoveActress(target.position, 1.6f);
                    }
                    yield return MoveActress(actress.position + Vector3.down * .38f, .6f);
                }
                if (crowdHeads != null && actress != null)
                    foreach (Transform head in crowdHeads)
                        if (head != null)
                        {
                            Vector3 direction = (actress.position + Vector3.up * .75f) - head.position;
                            direction.y = 0f;
                            if (direction.sqrMagnitude > .01f)
                                head.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                        }
                Toast("观众齐齐回头；沈芸伸手取走了椅子上的钥匙。", 4f);
                yield return new WaitForSeconds(1.1f);
                if (keyProp != null) keyProp.SetActive(false);
                if (audioManager != null) audioManager.PlaySfx("key");
                yield return new WaitForSeconds(.8f);
                yield return FadeTo(1f, .7f);
                finished = true;
            }
            finally
            {
                Restore2004();
                state.EndEcho(finished);
                if (finished)
                {
                    fade = 0f;
                    OpenPage("第一章 · 座｜第四周演示完成",
                        "回到了2004年。你看到沈芸坐在一排七座并拿走周明生的钥匙。\n\n" +
                        "这段残响不能证明她为什么拿走钥匙，也不说明之后发生了什么。\n\n" +
                        "本场景只用于验证独立功能；没有修改主工程第一章。", KZW4Evidence.None);
                }
            }
        }

        private IEnumerator MoveActress(Vector3 target, float duration)
        {
            if (actress == null) yield break;
            Vector3 start = actress.position;
            Vector3 direction = target - start;
            Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
            if (horizontal.sqrMagnitude > .001f)
                actress.rotation = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                actress.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            actress.position = target;
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            float from = fade;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fade = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            fade = target;
        }

        private void Restore2004()
        {
            echoBusy = false;
            fade = 0f;
            if (echo1992Group != null) echo1992Group.SetActive(false);
            if (stageLight != null) stageLight.intensity = originalStageIntensity;
            RenderSettings.ambientLight = originalAmbientColor;
            if (actress != null) actress.SetPositionAndRotation(actressInitialPosition, actressInitialRotation);
            if (crowdHeads != null && crowdInitialRotations != null)
                for (int i = 0; i < crowdHeads.Length && i < crowdInitialRotations.Length; i++)
                    if (crowdHeads[i] != null) crowdHeads[i].rotation = crowdInitialRotations[i];
            preview1992 = false;
            if (audioManager != null && audioManager.isActiveAndEnabled)
                audioManager.SwitchAmbience("ambience.2004");
        }

        private void ResetDemo()
        {
            Restore2004();
            state = new KZW4PuzzleState(definition != null && definition.IsValid() ? definition.correctChoice : 1);
            pendingClue = KZW4Evidence.None;
            SetOverlay(Overlay.None);
            if (player != null) player.ResetToStart();
            Toast("本次练习已重置：所有线索和谜题状态重新开始。", 5f);
        }

        private void OnGUI()
        {
            if (state == null) return;
            GUIStyle header = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, wordWrap = true };
            GUIStyle body = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            GUIStyle smaller = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            float boxWidth = Mathf.Min(Screen.width - 30f, 760f);

            GUI.Box(new Rect(12f, 12f, boxWidth, 112f), "");
            GUI.Label(new Rect(25f, 19f, boxWidth - 30f, 33f), "《空座》 · 第四周 LML 独立集成演示", header);
            GUI.Label(new Rect(25f, 56f, boxWidth - 30f, 65f), "当前目标：" + Objective(), body);

            if (overlay == Overlay.Document)
                DrawModal(pageTitle, pageContents, "E / Esc 关闭文档并记录线索", header, body);
            else if (overlay == Overlay.Relic)
                DrawRelic(header, body);
            else if (overlay == Overlay.Volumes)
                DrawVolume(header, body);
            else if (overlay == Overlay.Photo)
                DrawPhotoOverlay(header, smaller);
            else
            {
                if (!string.IsNullOrEmpty(worldHint))
                    GUI.Box(new Rect(Screen.width * .5f - 230f, Screen.height - 130f, 460f, 48f), worldHint);
                if (!echoBusy)
                    GUI.Label(new Rect(Screen.width * .5f - 8f, Screen.height * .5f - 10f, 25f, 25f), "+", body);
            }

            if (Time.unscaledTime < toastExpires && !string.IsNullOrEmpty(toast))
                GUI.Box(new Rect(20f, Screen.height - 83f, Screen.width - 40f, 53f), toast);
            if (Time.unscaledTime < subtitleExpires && !string.IsNullOrEmpty(subtitle))
                GUI.Box(new Rect(100f, Screen.height - 164f, Screen.width - 200f, 53f), subtitle);
            GUI.Label(new Rect(15f, Screen.height - 24f, Screen.width - 20f, 24f),
                "WASD 移动  |  E 调查  |  F 照片对位  |  G 遗物判断  |  F10 音量  |  M 试听年代环境  |  R 重置", smaller);
            if (fade > 0f)
            {
                Color saved = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(fade));
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = saved;
            }
        }

        private void DrawModal(string title, string content, string footer, GUIStyle head, GUIStyle text)
        {
            float w = Mathf.Min(Screen.width - 40f, 780f);
            float h = Mathf.Min(Screen.height - 140f, 440f);
            Rect window = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(window, "");
            GUILayout.BeginArea(new Rect(window.x + 25f, window.y + 19f, w - 50f, h - 35f));
            GUILayout.Label(title, head);
            GUILayout.Space(10f);
            GUILayout.Label(content, text);
            GUILayout.FlexibleSpace();
            GUILayout.Label(footer, text);
            GUILayout.EndArea();
        }

        private void DrawRelic(GUIStyle head, GUIStyle text)
        {
            string title = definition == null ? "遗物判断" : definition.title;
            float w = Mathf.Min(Screen.width - 40f, 790f);
            float h = Mathf.Min(Screen.height - 120f, 460f);
            Rect window = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(window, "");
            GUILayout.BeginArea(new Rect(window.x + 25f, window.y + 22f, w - 50f, h - 40f));
            GUILayout.Label(title, head);
            GUILayout.Label(definition == null ? "根据线索判断七号座的遗物。" : definition.explanation, text);
            GUILayout.Space(12f);
            for (int i = 0; i < 3; i++)
            {
                string option = definition != null && definition.options != null && definition.options.Length > i
                    ? definition.options[i] : "选项 " + (i + 1);
                GUILayout.Label((i + 1) + ". " + option + (selectedChoice == i + 1 ? "    [已选]" : ""), text);
            }
            GUILayout.Space(8f);
            GUILayout.Label(feedback, text);
            GUILayout.FlexibleSpace();
            GUILayout.Label("数字 1 / 2 / 3 选择  ·  Enter 确认  ·  E / Esc 返回", text);
            GUILayout.EndArea();
        }

        private void DrawPhotoOverlay(GUIStyle head, GUIStyle text)
        {
            GUI.Box(new Rect(15f, 135f, Mathf.Min(720f, Screen.width - 30f), 110f), "");
            GUI.Label(new Rect(28f, 142f, 660f, 32f), "1992 开戏前布置照片 · 对位模式", head);
            GUI.Label(new Rect(28f, 177f, 660f, 60f),
                "寻找正确站位与朝向，让舞台两根立柱及右侧台阶的三组十字线重叠。" +
                "  匹配：" + (photoMatched ? "保持稳定…" : "未对齐") +
                "  " + Mathf.RoundToInt(100f * Mathf.Clamp01(stablePhotoTime / photoHoldSeconds)) + "%  |  F / Esc 收起", text);

            if (viewCamera == null || photoVantage == null || photoLandmarks == null) return;
            for (int i = 0; i < photoLandmarks.Length; i++)
            {
                if (photoLandmarks[i] == null) continue;
                Vector3 target = KZW4PhotoAlignment.ProjectReference(viewCamera,
                    photoVantage, photoLandmarks[i].position);
                Vector3 actual = viewCamera.WorldToViewportPoint(photoLandmarks[i].position);
                if (target.z > 0f && target.x >= 0f && target.x <= 1f && target.y >= 0f && target.y <= 1f)
                {
                    float x = target.x * Screen.width;
                    float y = (1f - target.y) * Screen.height;
                    GUI.Box(new Rect(x - 18f, y - 18f, 36f, 36f), "╋");
                }
                if (actual.z > 0f && actual.x >= 0f && actual.x <= 1f && actual.y >= 0f && actual.y <= 1f)
                {
                    float x = actual.x * Screen.width;
                    float y = (1f - actual.y) * Screen.height;
                    GUI.Label(new Rect(x - 14f, y - 13f, 35f, 35f), "●", head);
                }
            }
        }

        private void DrawVolume(GUIStyle head, GUIStyle text)
        {
            float w = Mathf.Min(Screen.width - 50f, 550f);
            float h = 395f;
            Rect window = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(window, "");
            GUILayout.BeginArea(new Rect(window.x + 24f, window.y + 20f, w - 48f, h - 39f));
            GUILayout.Label("音频管理 · 五路音量", head);
            if (audioManager != null)
            {
                DrawSlider("总音量", ref audioManager.masterVolume, text);
                DrawSlider("环境音", ref audioManager.ambienceVolume, text);
                DrawSlider("音效", ref audioManager.sfxVolume, text);
                DrawSlider("语音", ref audioManager.voiceVolume, text);
                DrawSlider("音乐", ref audioManager.musicVolume, text);
                audioManager.ApplyVolumes();
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("可试听 M 切换 2004 / 1992 环境声。F10 / Esc 返回。", text);
            GUILayout.EndArea();
        }

        private static void DrawSlider(string title, ref float value, GUIStyle style)
        {
            GUILayout.Label(title + "  " + Mathf.RoundToInt(value * 100f) + "%", style);
            value = GUILayout.HorizontalSlider(value, 0f, 1f);
        }
    }
}
