using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KongZuo
{
    /// <summary>跨场景保留的最小剧情状态容器。正式存档系统由后续版本接入。</summary>
    [DisallowMultipleComponent]
    public sealed class KZGameSession : MonoBehaviour
    {
        private const string SaveKey = "KongZuo.P0.Progress.v1";
        [Serializable]
        private sealed class SaveData { public string[] flags; }
        public static KZGameSession Instance { get; private set; }

        private readonly HashSet<string> flags = new HashSet<string>(StringComparer.Ordinal);
        private bool hasTransitionPose;
        private Vector3 transitionPosition;
        private Quaternion transitionRotation;
        public event Action<string> FlagAdded;
        public string CurrentSceneName { get; private set; }

        public static KZGameSession EnsureExists()
        {
            if (Instance != null) return Instance;

            KZGameSession existing = FindFirstObjectByType<KZGameSession>();
            if (existing != null) return existing;

            return new GameObject("KZ_GameSession").AddComponent<KZGameSession>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProgress();
            CurrentSceneName = SceneManager.GetActiveScene().name;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CurrentSceneName = scene.name;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        public bool HasFlag(string flag)
        {
            return !string.IsNullOrWhiteSpace(flag) && flags.Contains(flag);
        }

        public bool TryAddFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag)) return false;
            if (!flags.Add(flag)) return false;
            SaveProgress();
            FlagAdded?.Invoke(flag);
            return true;
        }


        // 用于恢复中断的演出，不清除其他已经收集的线索。
        public bool RemoveFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag) || !flags.Remove(flag)) return false;
            SaveProgress();
            return true;
        }

        public string[] GetFlagsSorted()
        {
            string[] result = new string[flags.Count];
            flags.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        public void ResetSession()
        {
            flags.Clear();
            hasTransitionPose = false;
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        public void SetTransitionPose(Vector3 position, Quaternion rotation)
        {
            transitionPosition = position;
            transitionRotation = rotation;
            hasTransitionPose = true;
        }

        public bool TryConsumeTransitionPose(out Vector3 position, out Quaternion rotation)
        {
            position = transitionPosition;
            rotation = transitionRotation;
            if (!hasTransitionPose) return false;
            hasTransitionPose = false;
            return true;
        }

        private void LoadProgress()
        {
            string json = PlayerPrefs.GetString(SaveKey, "");
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || data.flags == null) return;
                foreach (string flag in data.flags)
                    if (!string.IsNullOrWhiteSpace(flag)) flags.Add(flag);
            }
            catch (Exception error) { Debug.LogWarning("[空座] 进度读取失败：" + error.Message); }
        }

        private void SaveProgress()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new SaveData { flags = GetFlagsSorted() }));
            PlayerPrefs.Save();
        }
    }
}
