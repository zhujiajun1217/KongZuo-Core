using UnityEngine;

namespace KongZuo.LMLWeek4
{
    [CreateAssetMenu(fileName = "KZW4_RelicPuzzle", menuName = "空座/LML第四周/遗物谜题配置")]
    public sealed class KZW4PuzzleDefinition : ScriptableObject
    {
        [Header("可由策划调整，示例答案为钥匙串")]
        public string title = "第一排七座上放过什么遗物？";
        [TextArea(2, 6)] public string explanation =
            "留座戏宣传册提到了亡者遗物。旧照片对位后，确认物品位于七号座；结合周明生的场务记录作答。";
        public string[] options = { "一串戏楼钥匙", "旧工牌", "铜哨" };
        [Range(1, 3)] public int correctChoice = 1;
        [TextArea] public string wrongFeedback = "物件形状和场务记录不符，请再核对照片。";
        [TextArea] public string correctFeedback = "确定了：周明生的钥匙串曾被摆在第一排七座。";

        public bool IsValid()
        {
            return options != null && options.Length == 3 && correctChoice >= 1 && correctChoice <= 3;
        }
    }
}
