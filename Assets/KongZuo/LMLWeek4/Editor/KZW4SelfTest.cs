#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace KongZuo.LMLWeek4.EditorTools
{
    /// <summary>Editor-run tests (no Unity Test Framework package required).</summary>
    public static class KZW4SelfTest
    {
        private static int passed;
        private static int failed;

        private static void Check(bool condition, string name)
        {
            if (condition) passed++;
            else { failed++; Debug.LogError("[LML第四周自测 FAILED] " + name); }
        }

        [MenuItem("空座/LML 第四周/02 执行系统自测")]
        public static void Run()
        {
            passed = 0;
            failed = 0;
            for (int answer = 1; answer <= 3; answer++)
            {
                for (int mask = 0; mask < 8; mask++)
                {
                    for (int choice = 0; choice <= 4; choice++)
                    {
                        KZW4PuzzleState s = new KZW4PuzzleState(answer);
                        if ((mask & 1) != 0) s.Collect(KZW4Evidence.Ritual);
                        if ((mask & 2) != 0) s.Collect(KZW4Evidence.PlacementPhoto);
                        if ((mask & 4) != 0) s.Collect(KZW4Evidence.WorkRecord);
                        Check(s.HasAllEvidence == (mask == 7), "线索真值组合 " + mask);
                        Check(s.MarkPhotoAligned() == ((mask & 2) != 0), "照片条件 " + mask);
                        KZW4AnswerResult result = s.Submit(choice);
                        KZW4AnswerResult expected = mask != 7 ? KZW4AnswerResult.MissingEvidence :
                            (choice < 1 || choice > 3 ? KZW4AnswerResult.InvalidChoice :
                             (choice == answer ? KZW4AnswerResult.Correct : KZW4AnswerResult.Wrong));
                        Check(result == expected, "结果判定 answer=" + answer + " evidence=" + mask + " choice=" + choice);
                        Check(s.Solved == (result == KZW4AnswerResult.Correct), "仅正确答案解锁");
                        Check(!s.TryBeginEcho() == !s.Solved, "错误情况不触发残响");
                        if (s.Solved)
                        {
                            Check(!s.TryBeginEcho(), "残响运行中不可重复触发");
                            s.EndEcho(false);
                            Check(s.TryBeginEcho(), "演出中断可重新进入");
                            s.EndEcho(true);
                            Check(s.EchoCompleted && !s.TryBeginEcho(), "成功演出后一次性标记");
                        }
                    }
                }
            }

            KZW4PuzzleState gate = new KZW4PuzzleState(1);
            gate.Collect(KZW4Evidence.Ritual | KZW4Evidence.PlacementPhoto | KZW4Evidence.WorkRecord);
            Check(gate.Submit(1) == KZW4AnswerResult.PhotoNotAligned, "未对位不能猜答案");
            gate.MarkPhotoAligned();
            Check(gate.Submit(1) == KZW4AnswerResult.Correct, "对位完成后可答对");
            Check(gate.Submit(1) == KZW4AnswerResult.AlreadySolved, "解过谜不能重复奖励");

            bool badAnswerRejected = false;
            try { new KZW4PuzzleState(4); }
            catch (ArgumentOutOfRangeException) { badAnswerRejected = true; }
            Check(badAnswerRejected, "非法正确答案配置拒绝创建");

            GameObject reference = new GameObject("KZW4_TestReference");
            GameObject cameraGo = new GameObject("KZW4_TestCamera");
            GameObject[] marks = new GameObject[3];
            try
            {
                reference.transform.position = new Vector3(0f, 1.62f, -4.8f);
                reference.transform.rotation = Quaternion.identity;
                cameraGo.transform.SetPositionAndRotation(reference.transform.position, reference.transform.rotation);
                Camera camera = cameraGo.AddComponent<Camera>();
                camera.fieldOfView = 72f;
                camera.aspect = 16f / 9f;
                Vector3[] coords = { new Vector3(-5.2f, 2.9f, 10.4f),
                    new Vector3(5.2f, 2.9f, 10.4f), new Vector3(7.4f, .52f, 7.65f) };
                Transform[] transforms = new Transform[3];
                for (int i = 0; i < 3; i++)
                {
                    marks[i] = new GameObject("KZW4_TestLandmark_" + i);
                    marks[i].transform.position = coords[i];
                    transforms[i] = marks[i].transform;
                }
                float error;
                Check(KZW4PhotoAlignment.Matches(camera, reference.transform, transforms,
                    1.35f, .055f, out error), "准确透视对位成功");
                cameraGo.transform.position += new Vector3(5f, 0f, 0f);
                Check(!KZW4PhotoAlignment.Matches(camera, reference.transform, transforms,
                    1.35f, .055f, out error), "远处拍摄不能误判");
                cameraGo.transform.position = reference.transform.position;
                cameraGo.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
                Check(!KZW4PhotoAlignment.Matches(camera, reference.transform, transforms,
                    1.35f, .055f, out error), "大角度偏差不能过关");
                transforms[0] = null;
                Check(!KZW4PhotoAlignment.Matches(camera, reference.transform, transforms,
                    1.35f, .055f, out error), "缺少地标不能过关");
            }
            finally
            {
                foreach (GameObject g in marks) if (g != null) UnityEngine.Object.DestroyImmediate(g);
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(reference);
            }

            string resultText = "谜题、残响、照片对位 Editor 自测：通过 " + passed + "，失败 " + failed +
                "。" + (failed > 0 ? "请打开Console查看 FAILED。" : " 可进行 Play 真机验收。");
            if (failed == 0) Debug.Log("[LML第四周] " + resultText);
            else Debug.LogError("[LML第四周] " + resultText);
            EditorUtility.DisplayDialog("LML 第四周自动自测", resultText, "关闭");
        }
    }
}
#endif
