#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 使用 Renderer 的世界空间包围盒，将一个物体的底部贴合到另一个物体的顶部。
///
/// 使用方法：
/// 1. 先选中要移动的物体（例如房檐）。
/// 2. 按住 Ctrl 再选中目标物体（例如墙体），让目标物体成为最后选中的活动对象。
/// 3. 点击 Tools/空座/构建工具/自动底部对齐顶部。
/// </summary>
public static class AlignBottomToTop
{
    [MenuItem("Tools/空座/构建工具/自动底部对齐顶部", false, 200)]
    private static void AlignSelectedObjects()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length != 2)
        {
            ShowError("请选择且只选择两个物体：先选择要移动的房檐，再选择目标墙体。");
            return;
        }

        // Unity 将最后点击的对象记录为 activeGameObject。
        // 因此活动对象是目标墙体，另一个对象是需要移动的房檐。
        GameObject targetWall = Selection.activeGameObject;
        if (targetWall == null)
        {
            ShowError("无法取得最后选中的目标墙体，请重新选择两个物体。");
            return;
        }

        GameObject movingEave = selected[0] == targetWall ? selected[1] : selected[0];

        if (!TryGetCombinedRendererBounds(movingEave, out Bounds movingBounds))
        {
            ShowError($"要移动的物体“{movingEave.name}”及其子物体中没有 Renderer 组件，无法计算真实底部。");
            return;
        }

        if (!TryGetCombinedRendererBounds(targetWall, out Bounds targetBounds))
        {
            ShowError($"目标物体“{targetWall.name}”及其子物体中没有 Renderer 组件，无法计算真实顶部。");
            return;
        }

        // Renderer.bounds 是世界空间包围盒，不依赖两个模型各自的 Pivot。
        float heightDifference = targetBounds.max.y - movingBounds.min.y;

        Undo.RecordObject(movingEave.transform, "房檐底部对齐墙体顶部");
        movingEave.transform.position += Vector3.up * heightDifference;
        EditorUtility.SetDirty(movingEave.transform);

        Debug.Log(
            $"[自动对齐] 已将“{movingEave.name}”沿 Y 轴移动 {heightDifference:F4}，" +
            $"其底部现已对齐“{targetWall.name}”的顶部。",
            movingEave);
    }

    /// <summary>
    /// 合并指定物体及其所有子物体的 Renderer.bounds。
    /// 如果根物体只是一个空父节点，也能正确计算整个模型的世界空间边界。
    /// </summary>
    private static bool TryGetCombinedRendererBounds(GameObject root, out Bounds combinedBounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            combinedBounds = default;
            return false;
        }

        combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combinedBounds.Encapsulate(renderers[i].bounds);

        return true;
    }

    /// <summary>
    /// Renderer 缺失或选择数量错误时，同时在 Console 和弹窗中明确报错。
    /// </summary>
    private static void ShowError(string message)
    {
        Debug.LogError("[自动底部对齐顶部] " + message);
        EditorUtility.DisplayDialog("自动底部对齐顶部", message, "确定");
    }

    // 只有恰好选中两个对象时才启用菜单，减少误操作。
    [MenuItem("Tools/空座/构建工具/自动底部对齐顶部", true)]
    private static bool ValidateAlignSelectedObjects()
    {
        return Selection.gameObjects.Length == 2;
    }
}
#endif
