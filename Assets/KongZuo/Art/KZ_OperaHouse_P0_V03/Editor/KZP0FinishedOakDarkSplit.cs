#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Only repairs 09_Finished_Art / Structure_OAK_DARK in the existing P0 FULL prefab.
/// No menu, no game-time behaviour. Does not touch the team scenes or source OBJ.
/// </summary>
[InitializeOnLoad]
internal static class KZP0FinishedOakDarkSplit
{
    private const string Root = "Assets/KongZuo/Art/KZ_OperaHouse_P0_V03";
    private const string PrefabPath = Root + "/Generated/Prefabs/KZ_OperaHouse_P0_V03_FULL.prefab";
    private const string SourcePath = Root + "/Models/09_Finish__OAK_DARK.obj";
    private const string GroupFolder = Root + "/Models/09_Finish_OAK_DARK_Grouped/";
    private const string ExpectedSourceSha256 = "1f7af00e3a97903f98a7839d756fd5ae67abb52e1bae7d9a5a5a856956ac8960";
    private const string Marker = "01_顶棚横梁_X_独立";

    // Names are listed in the same semantic order as the packaged OBJ meshes.
    private static readonly string[,] Groups = new string[,]
    {
        {"01_Ceiling_X_Beams", "01_顶棚横梁_X_独立"},
        {"02_Side_Z_Beams", "02_侧边纵梁_Z_独立"},
        {"03_Side_Y_Posts", "03_左右立柱_Y_独立"},
        {"04_Stage_Y_Posts", "04_戏台立柱_Y_独立"},
        {"05_Stage_X_Header", "05_戏台顶部横梁_X_独立"},
        {"06_StageFront_Panels", "06_舞台前沿木饰_整体"},
        {"07_StageFront_Rail", "07_舞台前缘横木_X_独立"},
        {"08_LeftWall_Panels", "08_左侧护墙木板_整体"},
        {"09_RightWall_Panels", "09_右侧护墙木板_整体"},
        {"10_Left_Decor_Frame", "10_装饰框_左_整体"},
        {"11_Middle_Decor_Frame", "11_装饰框_中_整体"},
        {"12_Right_Decor_Frame", "12_装饰框_右_整体"},
        {"13_Left_WallSconce_01", "13_壁灯木托_左_01_整体"},
        {"14_Left_WallSconce_02", "14_壁灯木托_左_02_整体"},
        {"15_Left_WallSconce_03", "15_壁灯木托_左_03_整体"},
        {"16_Left_WallSconce_04", "16_壁灯木托_左_04_整体"},
        {"17_Right_WallSconce_01", "17_壁灯木托_右_01_整体"},
        {"18_Right_WallSconce_02", "18_壁灯木托_右_02_整体"},
        {"19_Right_WallSconce_03", "19_壁灯木托_右_03_整体"},
        {"20_Right_WallSconce_04", "20_壁灯木托_右_04_整体"},
    };

    private static bool queued;
    private static bool waiting;
    private static bool working;
    private static bool hasWarnedMismatch;

    static KZP0FinishedOakDarkSplit()
    {
        EditorApplication.projectChanged += Queue;
        Queue();
    }

    internal static void Queue()
    {
        if (working || queued) return;
        queued = true;
        EditorApplication.delayCall += ApplyIfReady;
    }

    private static void WaitUntilReady()
    {
        if (waiting) return;
        waiting = true;
        EditorApplication.update += OnWait;
    }

    private static void OnWait()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == PrefabPath) return;
        EditorApplication.update -= OnWait;
        waiting = false;
        Queue();
    }

    private static string InProject(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            assetPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static bool MatchesExpectedModel()
    {
        string path = InProject(SourcePath);
        if (!File.Exists(path)) return false;
        using (SHA256 sha = SHA256.Create())
        {
            string actual = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                .Replace("-", "").ToLowerInvariant();
            if (actual == ExpectedSourceSha256) return true;
        }
        if (!hasWarnedMismatch)
        {
            hasWarnedMismatch = true;
            Debug.LogWarning("[空座 / OAK_DARK] 原始 OBJ 与补丁基准版本不同，已停止自动替换。请不要强制覆盖团队模型。");
        }
        return false;
    }

    private static bool AllGroupModelsLoaded()
    {
        for (int i = 0; i < Groups.GetLength(0); i++)
        {
            string path = GroupFolder + Groups[i, 0] + ".obj";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return false;
        }
        return true;
    }

    private static void ApplyIfReady()
    {
        queued = false;
        if (working) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            WaitUntilReady();
            return;
        }
        PrefabStage currentStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (currentStage != null && currentStage.assetPath == PrefabPath)
        {
            // Don't modify the prefab on disk while the user has it open for editing.
            WaitUntilReady();
            return;
        }
        if (!AllGroupModelsLoaded() || !MatchesExpectedModel()) return;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (asset == null) return;
        Transform art = asset.transform.Find("09_Finished_Art");
        if (art == null) return; // Fine-art auto bake may still be rebuilding.
        Transform old = art.Find("Structure_OAK_DARK");
        if (old == null || old.Find(Marker) != null) return; // Already split or absent.
        MeshFilter[] oldMeshes = old.GetComponentsInChildren<MeshFilter>(true);
        if (oldMeshes.Length != 1) // Never delete unknown manual edits.
        {
            Debug.LogWarning("[空座 / OAK_DARK] 精修层不是预期的单网格结构，跳过自动拆分。");
            return;
        }

        working = true;
        GameObject contents = null;
        try
        {
            contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (contents == null) throw new Exception("无法载入原完整戏楼 Prefab");
            Transform layer = contents.transform.Find("09_Finished_Art");
            if (layer == null) return;
            Transform original = layer.Find("Structure_OAK_DARK");
            if (original == null || original.Find(Marker) != null) return;
            MeshFilter[] meshFilters = original.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters.Length != 1)
                throw new Exception("原精修层不是单一 geometry，禁止删除未知模型");
            Material originalMat = original.GetComponentInChildren<Renderer>(true) != null
                ? original.GetComponentInChildren<Renderer>(true).sharedMaterial : null;
            if (originalMat == null)
                originalMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Generated/Materials/KZM_FINISH_OAK_DARK.mat");
            if (originalMat == null) throw new Exception("找不到深色木材材质，停止以避免丢失材质");

            // A local safety copy; outside Assets so it will not be committed to Git.
            string backupDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Library", "KZ_P0_OAK_DARK_Backups");
            Directory.CreateDirectory(backupDir);
            string backup = Path.Combine(backupDir,
                "KZ_OperaHouse_P0_V03_FULL_before_OAK_DARK_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".prefab");
            File.Copy(InProject(PrefabPath), backup, false);

            int sibling = original.GetSiblingIndex();
            Vector3 oldLocalPosition = original.localPosition;
            Quaternion oldLocalRotation = original.localRotation;
            Vector3 oldLocalScale = original.localScale;
            bool wasActive = original.gameObject.activeSelf;
            int oldLayer = original.gameObject.layer;
            // Each imported OBJ has zero-centered imported root transforms; its vertices
            // stay in the SAME coordinates as the original one-piece OBJ.
            GameObject replacement = new GameObject("Structure_OAK_DARK");
            replacement.transform.SetParent(layer, false);
            replacement.transform.SetSiblingIndex(sibling);
            replacement.transform.localPosition = oldLocalPosition;
            replacement.transform.localRotation = oldLocalRotation;
            replacement.transform.localScale = oldLocalScale;
            replacement.layer = oldLayer;
            replacement.SetActive(wasActive);

            for (int i = 0; i < Groups.GetLength(0); i++)
            {
                string name = Groups[i, 1];
                string source = GroupFolder + Groups[i, 0] + ".obj";
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(source);
                if (model == null) throw new Exception("缺少拆分资源：" + source);
                GameObject selectedGroup = new GameObject(name);
                selectedGroup.transform.SetParent(replacement.transform, false);
                selectedGroup.layer = oldLayer;
                GameObject inst = PrefabUtility.InstantiatePrefab(model, contents.scene) as GameObject;
                if (inst == null) throw new Exception("无法导入拆分网格：" + source);
                inst.name = "mesh";
                inst.transform.SetParent(selectedGroup.transform, false);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = r.sharedMaterials;
                    if (mats == null || mats.Length == 0) r.sharedMaterial = originalMat;
                    else
                    {
                        for (int j = 0; j < mats.Length; j++) mats[j] = originalMat;
                        r.sharedMaterials = mats;
                    }
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
                foreach (Collider col in inst.GetComponentsInChildren<Collider>(true))
                    UnityEngine.Object.DestroyImmediate(col);
            }

            // Remove the legacy joined mesh, rather than just hiding its renderer.
            UnityEngine.Object.DestroyImmediate(original.gameObject);
            GameObject result = PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            if (result == null) throw new Exception("无法保存分组后的戏楼 Prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("[空座 / OAK_DARK] 精修层原 geometry 已拆成 20 个可独立移动的逻辑组。已保存原 Prefab 备份到 Library/KZ_P0_OAK_DARK_Backups。");
        }
        catch (Exception e)
        {
            Debug.LogError("[空座 / OAK_DARK] 自动拆分失败，未确认完成：" + e);
        }
        finally
        {
            if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            working = false;
        }
    }
}

/// <summary>Reapply the grouping if another existing tool regenerates the original prefab.</summary>
internal sealed class KZP0FinishedOakDarkSplitWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
        string[] moved, string[] movedFrom)
    {
        const string target = "Assets/KongZuo/Art/KZ_OperaHouse_P0_V03/Generated/Prefabs/KZ_OperaHouse_P0_V03_FULL.prefab";
        foreach (string path in imported)
        {
            if (path == target || path.Contains("/09_Finish_OAK_DARK_Grouped/"))
            {
                KZP0FinishedOakDarkSplit.Queue();
                return;
            }
        }
    }
}
#endif
