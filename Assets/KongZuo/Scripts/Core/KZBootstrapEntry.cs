using UnityEngine;
using UnityEngine.SceneManagement;

namespace KongZuo
{
    public sealed class KZBootstrapEntry : MonoBehaviour
    {
        private void Start()
        {
            // 从总入口启动 = 新一局；直接 Play 单个关卡则不强制清空调试状态。
            KZGameSession.EnsureExists().ResetSession();
            if (!Application.CanStreamedLevelBeLoaded(KZSceneNames.Prologue))
            {
                Debug.LogError("[空座] 缺少序章场景；请先执行 空座/生成P0灰盒场景。");
                return;
            }
            SceneManager.LoadScene(KZSceneNames.Prologue);
        }
    }
}
