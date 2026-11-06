using UnityEngine;

namespace KongZuo
{
    // Projects the authored stage landmarks into the same viewport as the 1992 photograph.
    public sealed class KZPhotoAlignmentPuzzle : MonoBehaviour
    {
        public Camera viewCamera;
        public Transform referenceView;
        public Transform[] landmarks;
        [Min(0.5f)] public float maxViewDistance = 3.5f;
        [Range(.01f, .2f)] public float maxViewportError = .12f;
        [Min(.05f)] public float requiredHoldSeconds = .35f;
        public LayerMask occlusionMask = ~0;

        private KZGameSession session;
        private bool active;
        private float hold;
        private float worstError;
        private string feedback = "";
        public static bool IsAnyActive { get; private set; }

        private void Awake()
        {
            session = KZGameSession.EnsureExists();
            if (viewCamera == null) viewCamera = FindFirstObjectByType<Camera>();
        }

        private void OnDisable()
        {
            active = false;
            IsAnyActive = false;
        }

        private void Update()
        {
            if (session.HasFlag(KZFlags.PhotoAligned)) return;
            if (!session.HasFlag(KZFlags.Photo) || !session.HasFlag(KZFlags.Ticket)) return;
            if (KZStoryDirector.Current == null || KZStoryDirector.Current.isPrologue ||
                KZStoryDirector.Current.IsBusy || KZHud.IsModal) return;

            if (KZInput.Photo())
            {
                active = !active;
                IsAnyActive = active;
                hold = 0f;
                if (active) SnapToReferenceView();
                feedback = active ? "已回到旧照片拍摄点，保持视线片刻即可完成对位。" : "";
            }
            if (!active) return;
            if (KZInput.ConsumeEscape())
            {
                active = false;
                IsAnyActive = false;
                hold = 0f;
                return;
            }

            bool match = Matches(out worstError);
            if (match) hold += Time.deltaTime;
            else hold = 0f;
            feedback = match ? "三处地标接近吻合，保持视线。" :
                (worstError < float.PositiveInfinity ?
                    "最大偏差 " + Mathf.RoundToInt(worstError * 100f) + "%，继续微调。" :
                    "照片中的地标没有全部进入视野，或被建筑遮挡。");
            if (hold < requiredHoldSeconds) return;
            active = false;
            IsAnyActive = false;
            session.TryAddFlag(KZFlags.PhotoAligned);
            KZHud.ShowPage("旧照片对位完成",
                "舞台两侧立柱与侧梯的位置吻合。照片里被留出的中央座位，正是第一排七座。");
            KZAudioFeedback.PlayCue(KZSoundCue.PuzzleSuccess);
        }

        private void SnapToReferenceView()
        {
            if (viewCamera == null || referenceView == null) return;
            KZFirstPersonPlayer player = viewCamera.GetComponentInParent<KZFirstPersonPlayer>();
            if (player == null) return;
            player.TeleportView(referenceView.position, referenceView.rotation);
        }

        public bool Matches(out float maxError)
        {
            maxError = float.PositiveInfinity;
            if (viewCamera == null || referenceView == null || landmarks == null || landmarks.Length < 3)
                return false;
            if (Vector3.Distance(viewCamera.transform.position, referenceView.position) > maxViewDistance)
                return false;
            float tanHalf = Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * .5f);
            maxError = 0f;
            foreach (Transform landmark in landmarks)
            {
                if (landmark == null) return false;
                Vector3 local = Quaternion.Inverse(referenceView.rotation) *
                    (landmark.position - referenceView.position);
                Vector3 actual = viewCamera.WorldToViewportPoint(landmark.position);
                if (local.z <= 0f || actual.z <= 0f) return false;
                Vector2 target = new Vector2(
                    .5f + local.x / (2f * local.z * tanHalf * viewCamera.aspect),
                    .5f + local.y / (2f * local.z * tanHalf));
                if (target.x <= .02f || target.x >= .98f ||
                    target.y <= .02f || target.y >= .98f ||
                    actual.x <= .02f || actual.x >= .98f ||
                    actual.y <= .02f || actual.y >= .98f) return false;
                // The imported opera-house model uses broad collision meshes around seats and
                // stage trim. Ray tests therefore reported a false occlusion even while the
                // on-screen landmark dot was visibly aligned with its target. Viewport matching
                // is the actual puzzle action and is sufficient for this authored camera pose.
                maxError = Mathf.Max(maxError, Vector2.Distance(target, actual));
            }
            return maxError <= maxViewportError;
        }

        private void OnGUI()
        {
            if (!active || viewCamera == null || referenceView == null || landmarks == null) return;
            GUI.Box(new Rect(18f, 138f, 620f, 82f),
                "1992年旧照片对位  ·  F / Esc 收起\n" + feedback);
            foreach (Transform landmark in landmarks)
            {
                if (landmark == null) continue;
                Vector3 local = Quaternion.Inverse(referenceView.rotation) *
                    (landmark.position - referenceView.position);
                if (local.z <= 0f) continue;
                float tanHalf = Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * .5f);
                float x = .5f + local.x / (2f * local.z * tanHalf * viewCamera.aspect);
                float y = .5f + local.y / (2f * local.z * tanHalf);
                if (x > 0f && x < 1f && y > 0f && y < 1f)
                    GUI.Box(new Rect(x * Screen.width - 12f,
                        (1f - y) * Screen.height - 12f, 24f, 24f), "+");
                Vector3 actual = viewCamera.WorldToViewportPoint(landmark.position);
                if (actual.z > 0f && actual.x > 0f && actual.x < 1f &&
                    actual.y > 0f && actual.y < 1f)
                    GUI.Label(new Rect(actual.x * Screen.width - 9f,
                        (1f - actual.y) * Screen.height - 10f, 24f, 24f), "●");
            }
        }
    }
}
