#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KongZuo.EditorTools
{
    /// <summary>Only acts when the user selects the menu command. Never rewrites a scene automatically.</summary>
    public static class KZMainPolishAudioSetup
    {
        private const string AudioPath = "Assets/KongZuo/LMLWeek4/Audio/Placeholder/";

        [MenuItem("空座/主程序优化/为当前旧版场景配置音效（手动保存）")]
        private static void ConfigureCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("请先停止播放", "退出 Play 模式后再配置场景。", "知道了");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.name.StartsWith("V03_"))
            {
                EditorUtility.DisplayDialog("不修改场景", "此工具只用于旧版主程序场景，不应添加到 V0.3 场景。", "知道了");
                return;
            }
            if (Object.FindFirstObjectByType<KZStoryDirector>() == null)
            {
                EditorUtility.DisplayDialog("不修改场景", "当前场景没有旧版 KZStoryDirector。请先打开 01_Prologue 或 02_Chapter01。", "知道了");
                return;
            }
            if (!EditorUtility.DisplayDialog("配置音效（可撤销）",
                "将在当前打开的旧版场景中添加一个 KZ_AudioFeedback 物体，并尝试绑定第四周占位 WAV。\n" +
                "不会自动保存，也不会更改场景中的原有物体。请先确保已有备份。", "添加", "取消"))
                return;

            KZAudioFeedback feedback = Object.FindFirstObjectByType<KZAudioFeedback>();
            if (feedback == null)
            {
                GameObject go = new GameObject("KZ_AudioFeedback");
                Undo.RegisterCreatedObjectUndo(go, "创建空座音效反馈物体");
                feedback = Undo.AddComponent<KZAudioFeedback>(go);
            }
            else Undo.RecordObject(feedback, "绑定空座音效素材");

            SerializedObject settings = new SerializedObject(feedback);
            int bound = 0;
            bound += FillIfEmpty(settings, "ambienceLoop", "ambience_2004.wav");
            bound += FillIfEmpty(settings, "paper", "paper.wav");
            bound += FillIfEmpty(settings, "phone", "phone.wav");
            bound += FillIfEmpty(settings, "key", "key.wav");
            bound += FillIfEmpty(settings, "gong", "gong.wav");
            bound += FillIfEmpty(settings, "puzzleSuccess", "puzzle_ok.wav");
            bound += FillIfEmpty(settings, "puzzleFailure", "puzzle_fail.wav");
            // There was no distinct door sample in Week 4, so this remains intentionally empty.
            settings.ApplyModifiedProperties();
            EditorUtility.SetDirty(feedback);
            EditorSceneManager.MarkSceneDirty(scene);

            Selection.activeGameObject = feedback.gameObject;
            EditorUtility.DisplayDialog("已配置，尚未保存", "绑定了 " + bound +
                " 个之前为空的音频字段。\n请在 Inspector 检查结果；如果合适，再用 Ctrl+S 保存场景。\n" +
                "后期可以将 Door 等字段替换成正式音效。", "知道了");
        }

        private static int FillIfEmpty(SerializedObject settings, string property, string assetName)
        {
            SerializedProperty slot = settings.FindProperty(property);
            if (slot == null || slot.objectReferenceValue != null) return 0;
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + assetName);
            if (clip == null) return 0;
            slot.objectReferenceValue = clip;
            return 1;
        }
    }
}
#endif
