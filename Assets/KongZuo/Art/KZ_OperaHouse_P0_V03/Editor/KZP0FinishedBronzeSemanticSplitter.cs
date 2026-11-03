#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Splits only 09_Finished_Art/Structure_BRONZE from the existing one-mesh
/// source model into semantic groups. No menu, no runtime behaviour.
/// This is a one-time Editor transformation of the FULL prefab asset.
/// </summary>
[InitializeOnLoad]
internal static class KZP0FinishedBronzeSemanticSplitter
{
    private const string Root = "Assets/KongZuo/Art/KZ_OperaHouse_P0_V03";
    private const string PrefabPath = Root + "/Generated/Prefabs/KZ_OperaHouse_P0_V03_FULL.prefab";
    private const string SourceObj = Root + "/Models/09_Finish__BRONZE.obj";
    private const string GroupPath = Root + "/Models/09_Finish_BRONZE_Grouped/";
    private const string OriginalSHA256 = "50886b8ccfbaa318fa77b6307a5955cf0c3ef5a8a4d995c9d8fe5e7dd7882e88";
    private const string ModularSHA256 = "28a82f6674b6e0c7022b89f5a770f772d6d921eb352b222520f959de88a6896a";
    private const string Marker = "01_舞台前沿_底部金属线_X";

    private struct Entry
    {
        internal string asset, label;
        internal Vector3 pivot;
        internal Entry(string a, string l, float x, float y, float z)
        {
            asset = a; label = l; pivot = new Vector3(x, y, z);
        }
    }
    private static readonly Entry[] Groups = new Entry[]
    {
        new Entry("01_StageFront_LowerX", "01_舞台前沿_底部金属线_X", 0.000000f, 0.035000f, 3.447500f),
        new Entry("02_StageFront_Ornament_01", "02_舞台前沿装饰牌_01_整体", -6.700000f, 0.556000f, 3.711000f),
        new Entry("03_StageFront_Ornament_02", "03_舞台前沿装饰牌_02_整体", -4.466665f, 0.556000f, 3.711000f),
        new Entry("04_StageFront_Ornament_03", "04_舞台前沿装饰牌_03_整体", -2.233335f, 0.556000f, 3.711000f),
        new Entry("05_StageFront_Ornament_04", "05_舞台前沿装饰牌_04_整体", 0.000000f, 0.556000f, 3.711000f),
        new Entry("06_StageFront_Ornament_05", "06_舞台前沿装饰牌_05_整体", 2.233335f, 0.556000f, 3.711000f),
        new Entry("07_StageFront_Ornament_06", "07_舞台前沿装饰牌_06_整体", 4.466665f, 0.556000f, 3.711000f),
        new Entry("08_StageFront_Ornament_07", "08_舞台前沿装饰牌_07_整体", 6.700000f, 0.556000f, 3.711000f),
        new Entry("09_StageFront_UpperX", "09_舞台前沿_上沿金属线_X", 0.000000f, 1.042000f, 3.746500f),
        new Entry("10_LeftWall_Z_Strips", "10_左侧墙_平行金属线_Z", -9.500000f, 1.983500f, -3.580000f),
        new Entry("11_LeftWall_UpperZ", "11_左侧墙_顶部金属线_Z", -9.420000f, 6.105000f, -3.600000f),
        new Entry("12_LeftWall_Mounts", "12_左侧墙_金属固定件_整体", -9.515500f, 4.067500f, -3.645000f),
        new Entry("13_RightWall_Z_Strips", "13_右侧墙_平行金属线_Z", 9.500000f, 1.983500f, -3.580000f),
        new Entry("14_RightWall_UpperZ", "14_右侧墙_顶部金属线_Z", 9.420000f, 6.105000f, -3.600000f),
        new Entry("15_RightWall_Mounts", "15_右侧墙_金属固定件_整体", 9.515500f, 4.067500f, -3.645000f),
        new Entry("16_Ceiling_X_Strips", "16_顶棚_平行金属线_X", 0.000000f, 6.302500f, -3.735000f),
        new Entry("17_BackWall_Frame_左", "17_舞台背景装饰框_左_整体", -4.780000f, 3.300000f, 10.339500f),
        new Entry("18_BackWall_Frame_中", "18_舞台背景装饰框_中_整体", 0.000000f, 3.300000f, 10.339500f),
        new Entry("19_BackWall_Frame_右", "19_舞台背景装饰框_右_整体", 4.780000f, 3.300000f, 10.339500f),
        new Entry("20_StageLeft_ShortX", "20_舞台左侧_短金属线_X", -6.150000f, 1.135000f, 3.918500f),
        new Entry("21_StageRight_ShortX", "21_舞台右侧_短金属线_X", 6.150000f, 1.135000f, 3.918500f),
        new Entry("22_StageLeft_Corner", "22_舞台左侧_金属转角_整体", -7.270000f, 3.815000f, 4.085000f),
        new Entry("23_StageRight_Corner", "23_舞台右侧_金属转角_整体", 7.270000f, 3.815000f, 4.085000f),
        new Entry("24_Stage_Header_X", "24_戏台顶部_金属横线_X", 0.000000f, 6.385000f, 3.800000f),
        new Entry("25_WallSconce_L_01", "25_壁灯金属托_左_01_整体", -9.189000f, 3.465000f, -8.350000f),
        new Entry("26_WallSconce_L_02", "26_壁灯金属托_左_02_整体", -9.189000f, 3.465000f, -4.450000f),
        new Entry("27_WallSconce_L_03", "27_壁灯金属托_左_03_整体", -9.189000f, 3.465000f, -0.550000f),
        new Entry("28_WallSconce_L_04", "28_壁灯金属托_左_04_整体", -9.189000f, 3.465000f, 3.100000f),
        new Entry("29_WallSconce_R_01", "29_壁灯金属托_右_01_整体", 9.189000f, 3.465000f, -8.350000f),
        new Entry("30_WallSconce_R_02", "30_壁灯金属托_右_02_整体", 9.189000f, 3.465000f, -4.450000f),
        new Entry("31_WallSconce_R_03", "31_壁灯金属托_右_03_整体", 9.189000f, 3.465000f, -0.550000f),
        new Entry("32_WallSconce_R_04", "32_壁灯金属托_右_04_整体", 9.189000f, 3.465000f, 3.100000f),
    };

    private static bool queued;
    private static bool working;
    private static bool waiting;
    private static bool mismatchWarned;

    static KZP0FinishedBronzeSemanticSplitter()
    {
        EditorApplication.projectChanged += Queue;
        Queue();
    }

    internal static void Queue()
    {
        if (queued || working) return;
        queued = true;
        EditorApplication.delayCall += Apply;
    }

    private static void WaitForEditToFinish()
    {
        if (waiting) return;
        waiting = true;
        EditorApplication.update += Retry;
    }

    private static void Retry()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == PrefabPath) return;
        EditorApplication.update -= Retry;
        waiting = false;
        Queue();
    }

    private static string AbsolutePath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            assetPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static bool SourceMatches()
    {
        string p = AbsolutePath(SourceObj);
        if (!File.Exists(p)) return false;
        using (SHA256 sha = SHA256.Create())
        {
            string digest = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p)))
                .Replace("-", "").ToLowerInvariant();
            if (digest == OriginalSHA256 || digest == ModularSHA256) return true;
        }
        if (!mismatchWarned)
        {
            mismatchWarned = true;
            Debug.LogWarning("[空座 / BRONZE] 本地源 OBJ 已与补丁基准不同。为保护新模型，自动拆分已跳过。");
        }
        return false;
    }

    private static bool AssetsReady()
    {
        foreach (Entry g in Groups)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GroupPath + g.asset + ".obj") == null)
                return false;
        return true;
    }

    private static bool IsOldJoinedMesh(Transform candidate)
    {
        if (candidate == null || candidate.Find(Marker) != null) return false;
        MeshFilter[] filters = candidate.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length != 1 || filters[0].sharedMesh == null) return false;
        string meshSource = AssetDatabase.GetAssetPath(filters[0].sharedMesh);
        if (meshSource != SourceObj) return false;
        if (candidate.GetComponentsInChildren<Collider>(true).Length > 0) return false;
        if (candidate.GetComponentsInChildren<MonoBehaviour>(true).Length > 0) return false;
        return true;
    }

    private static void Apply()
    {
        queued = false;
        if (working) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            WaitForEditToFinish();
            return;
        }
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == PrefabPath)
        {
            // Changing prefab contents on disk while it is open would overwrite
            // the user's unsaved work. Wait until Prefab Mode is closed.
            WaitForEditToFinish();
            return;
        }
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset == null) return;
        Transform art = prefabAsset.transform.Find("09_Finished_Art");
        if (art == null) return;
        Transform old = art.Find("Structure_BRONZE");
        if (old == null || old.Find(Marker) != null) return;
        if (!IsOldJoinedMesh(old))
        {
            // A locally edited structure must not be replaced or duplicated.
            return;
        }
        if (!AssetsReady() || !SourceMatches()) return;

        working = true;
        GameObject contents = null;
        try
        {
            contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            Transform layer = contents.transform.Find("09_Finished_Art");
            if (layer == null) return;
            Transform original = layer.Find("Structure_BRONZE");
            if (!IsOldJoinedMesh(original)) return;
            Renderer oldRenderer = original.GetComponentInChildren<Renderer>(true);
            if (oldRenderer == null || oldRenderer.sharedMaterial == null)
                throw new Exception("原始 BRONZE 材质缺失，停止替换");
            Material originalMaterial = oldRenderer.sharedMaterial;

            // Backup the EXACT current prefab file before editing. It stays in
            // Library (Git-ignored), outside the project's Assets folder.
            string backupDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Library", "KZ_P0_BRONZE_Backups");
            Directory.CreateDirectory(backupDirectory);
            string backupPath = Path.Combine(backupDirectory,
                "KZ_OperaHouse_P0_V03_FULL_before_BRONZE_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".prefab");
            File.Copy(AbsolutePath(PrefabPath), backupPath, false);

            GameObject replacement = new GameObject("Structure_BRONZE");
            replacement.transform.SetParent(layer, false);
            replacement.transform.SetSiblingIndex(original.GetSiblingIndex());
            replacement.transform.localPosition = original.localPosition;
            replacement.transform.localRotation = original.localRotation;
            replacement.transform.localScale = original.localScale;
            replacement.layer = original.gameObject.layer;
            replacement.tag = original.gameObject.tag;
            replacement.SetActive(original.gameObject.activeSelf);

            foreach (Entry g in Groups)
            {
                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(GroupPath + g.asset + ".obj");
                if (modelAsset == null) throw new Exception("拆分文件丢失：" + g.asset);
                GameObject group = new GameObject(g.label);
                group.layer = replacement.layer;
                group.transform.SetParent(replacement.transform, false);
                group.transform.localPosition = g.pivot;
                group.transform.localRotation = Quaternion.identity;
                group.transform.localScale = Vector3.one;

                GameObject inst = PrefabUtility.InstantiatePrefab(modelAsset, contents.scene) as GameObject;
                if (inst == null) throw new Exception("无法加载分组网格：" + g.asset);
                inst.name = "mesh";
                inst.transform.SetParent(group.transform, false);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                Renderer[] renderers = inst.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new Exception("分组网格没有 Renderer：" + g.asset);
                foreach (Renderer r in renderers)
                {
                    Material[] mats = r.sharedMaterials;
                    if (mats == null || mats.Length == 0)
                        r.sharedMaterial = originalMaterial;
                    else
                    {
                        for (int i = 0; i < mats.Length; i++) mats[i] = originalMaterial;
                        r.sharedMaterials = mats;
                    }
                    r.shadowCastingMode = oldRenderer.shadowCastingMode;
                    r.receiveShadows = oldRenderer.receiveShadows;
                    r.lightProbeUsage = oldRenderer.lightProbeUsage;
                    r.reflectionProbeUsage = oldRenderer.reflectionProbeUsage;
                }
                foreach (Collider col in inst.GetComponentsInChildren<Collider>(true))
                    UnityEngine.Object.DestroyImmediate(col);
            }

            // This actually removes the old combined geometry, rather than
            // keeping a second hidden copy or turning off selection outlines.
            UnityEngine.Object.DestroyImmediate(original.gameObject);
            if (PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath) == null)
                throw new Exception("无法保存目标 FULL.prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("[空座 / BRONZE] 精修金属装饰已经拆成 32 个可独立选中的组；原 geometry 已移除。Prefab 备份保存在 Library/KZ_P0_BRONZE_Backups。");
        }
        catch (Exception ex)
        {
            Debug.LogError("[空座 / BRONZE] 自动拆分失败；未确认成功：" + ex);
        }
        finally
        {
            if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            working = false;
        }
    }
}

internal sealed class KZP0FinishedBronzeGroupAssetWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (path == "Assets/KongZuo/Art/KZ_OperaHouse_P0_V03/Generated/Prefabs/KZ_OperaHouse_P0_V03_FULL.prefab"
                || path.Contains("/09_Finish_BRONZE_Grouped/"))
            {
                KZP0FinishedBronzeSemanticSplitter.Queue();
                break;
            }
        }
    }
}
#endif
