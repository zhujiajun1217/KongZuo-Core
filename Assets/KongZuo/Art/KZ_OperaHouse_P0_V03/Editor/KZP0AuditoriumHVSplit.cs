#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// P0 INTERIOR WOOD ONLY. One-time, editor-side change to the original FULL prefab.
/// 02_FrontRooms + 03_Auditorium, NOT 01_Exterior / 07_Roofs / story scenes.
/// Keeps whole window/door frames as individual logical objects, and separates
/// architectural beams by axis. No menu, no modal prompt, no auto-reset on reload.
/// </summary>
[InitializeOnLoad]
public static class KZP0AuditoriumHVSplit
{
    private const string Root = "Assets/KongZuo/Art/KZ_OperaHouse_P0_V03/";
    private const string Full = Root + "Generated/Prefabs/KZ_OperaHouse_P0_V03_FULL.prefab";
    private const string Models = Root + "Models/";
    private const string FrontPieces = Root + "InteriorFrontGroups/";
    private const string HallPieces = Root + "AuditoriumParallel/";
    private const string FrontRootName = "KZ_InteriorFrontGroups";
    private const string HallRootName = "KZ_ParallelDirectionGroups";

    private static bool attempted;
    private static readonly Dictionary<string, string> SourceSha = new Dictionary<string, string>
    {
        {"02_FrontRooms__TIMBER", "576dacfedcf1e61903602f0b8f8c2264003ac635cc649d6cd2fb32c71a6726d5"},
        {"02_FrontRooms__TIMBER_DARK", "baed39f5e9eda129aed8675bb23bb56ac78723f8be3ec2c069f1e823bfb89b4d"},
        {"02_FrontRooms__TIMBER_LIGHT", "d55845eb801642166bb0a9dbe4fdd68018999c57d6b640a8dba07a532513d9c7"},
        {"03_Auditorium__TIMBER", "8d37ee6d367a2c6eb2fd853b32821b64c655b5d9d93070e54ec277a6f62bb4c6"},
        {"03_Auditorium__TIMBER_DARK", "711dd22d446d599e3ccbab607500ae0d1d12d1254bcefc62d298eaef478fec18"},
        {"03_Auditorium__TIMBER_LIGHT", "677794c097bb0864371bec0e0122b4efa1a6effb8a6e7488a9e1833d5e3224a2"}
    };

    private static readonly Dictionary<string, string[]> FrontMap = new Dictionary<string, string[]>
    {
        {"02_FrontRooms__TIMBER", new [] { "Furniture_CenterLeft", "Furniture_CenterRight", "Furniture_EastDesk", "Furniture_WestDesk", "Ornament_Caps" }},
        {"02_FrontRooms__TIMBER_DARK", new [] { "Frame_CentralDoor", "Frame_Window_A", "Frame_Window_B", "Frame_Window_C", "Frame_Window_D", "Frame_Window_E", "Furniture_CenterRight", "Furniture_EastDesk", "Furniture_WestCabinet", "Furniture_WestDesk", "Ornament_Caps", "Trim_East", "Trim_West", "X_Beams", "Y_Posts" }},
        {"02_FrontRooms__TIMBER_LIGHT", new [] { "Furniture_CenterRight", "Furniture_EastDesk", "Furniture_WestCabinet", "Ornament_Caps" }}
    };

    private static readonly Dictionary<string, string[]> HallMap = new Dictionary<string, string[]>
    {
        {"03_Auditorium__TIMBER", new [] { "Ornament_Left", "Ornament_Right", "Slope_DownRight", "Slope_UpRight", "Y_Posts" }},
        {"03_Auditorium__TIMBER_DARK", new [] { "Ornament_Left", "Ornament_Right", "Panels_Left", "Panels_Right", "PictureFrame", "X_Beams", "Z_Beams" }},
        {"03_Auditorium__TIMBER_LIGHT", new [] { "Ornament_Left", "Ornament_Right", "Z_Beams" }}
    };

    private static readonly Dictionary<string, string> HallGroups = new Dictionary<string, string>
    {
        {"X_Beams", "01_X_Beams"},
        {"Z_Beams", "02_Z_Beams"},
        {"Y_Posts", "03_Y_Posts"},
        {"Slope_UpRight", "04_Slope_A"},
        {"Slope_DownRight", "05_Slope_B"},
        {"Panels_Left", "06_WallPanels_Left"},
        {"Panels_Right", "07_WallPanels_Right"},
        {"PictureFrame", "08_PictureFrame_Whole"},
        {"Ornament_Left", "09_Ornament_Left"},
        {"Ornament_Right", "10_Ornament_Right"}
    };

    static KZP0AuditoriumHVSplit() { EditorApplication.delayCall += ApplyOnce; }

    private static void ApplyOnce()
    {
        if (attempted || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ApplyOnce;
            return;
        }
        attempted = true;
        if (!AssetDatabase.IsValidFolder(FrontPieces.TrimEnd('/')) ||
            !AssetDatabase.IsValidFolder(HallPieces.TrimEnd('/'))) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Full) == null) return;
        InstallInteriorOnly();
    }

    private static string HashFile(string assetPath)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(File.ReadAllBytes(assetPath));
            StringBuilder result = new StringBuilder();
            foreach (byte b in hash) result.Append(b.ToString("x2"));
            return result.ToString();
        }
    }

    private static Mesh GetImportedMesh(string assetPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<Mesh>()
            .Where(m => m != null && m.vertexCount > 0)
            .OrderByDescending(m => m.vertexCount)
            .FirstOrDefault();
    }

    private static void ValidateOriginalMeshes()
    {
        foreach (var source in SourceSha)
        {
            string path = Models + source.Key + ".obj";
            if (!File.Exists(path)) throw new FileNotFoundException("Interior source OBJ not found", path);
            if (HashFile(path) != source.Value)
            {
                throw new InvalidOperationException("Interior source OBJ differs from the team and refined backups: " +
                    path + ". Stopped rather than overwrite newer local modeling.");
            }
        }
    }

    private static Dictionary<string, Mesh> LoadGroupMeshes(
        Dictionary<string, string[]> map, string folder)
    {
        Dictionary<string, Mesh> result = new Dictionary<string, Mesh>();
        foreach (var source in map)
        {
            foreach (string kind in source.Value)
            {
                string splitPath = folder + source.Key + "__" + kind + ".obj";
                Mesh m = GetImportedMesh(splitPath);
                if (m == null) throw new InvalidOperationException("Missing interior split mesh: " + splitPath);
                result[source.Key + "/" + kind] = m;
            }
        }
        return result;
    }

    private static Transform FindNode(GameObject root, string nodeName)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == nodeName);
    }

    private static List<MeshFilter> OriginalFilters(Transform area,
        Dictionary<string, string[]> map)
    {
        var paths = new HashSet<string>(map.Keys.Select(k => Models + k + ".obj"));
        return area.GetComponentsInChildren<MeshFilter>(true)
            .Where(f => f != null && f.sharedMesh != null)
            .Where(f => paths.Contains(AssetDatabase.GetAssetPath(f.sharedMesh)))
            .ToList();
    }

    private static string DisplayName(bool front, string kind)
    {
        if (!front) return HallGroups[kind];
        if (kind == "X_Beams") return "01_X_Beams";
        if (kind == "Y_Posts") return "03_Y_Posts";
        if (kind.StartsWith("Frame_")) return "10_" + kind;
        if (kind.StartsWith("Trim_")) return "20_" + kind;
        if (kind.StartsWith("Furniture_")) return "30_" + kind;
        return "40_" + kind;
    }

    private static void CreatePiece(Transform source, MeshRenderer originalRenderer,
        Transform groupParent, string childName, Mesh targetMesh)
    {
        GameObject piece = new GameObject(childName);
        piece.layer = source.gameObject.layer;
        // Preserve model-import transforms before moving under the new group.
        piece.transform.SetParent(source.parent, false);
        piece.transform.localPosition = source.localPosition;
        piece.transform.localRotation = source.localRotation;
        piece.transform.localScale = source.localScale;
        piece.transform.SetParent(groupParent, true);
        piece.AddComponent<MeshFilter>().sharedMesh = targetMesh;
        MeshRenderer display = piece.AddComponent<MeshRenderer>();
        display.sharedMaterials = originalRenderer.sharedMaterials;
        display.shadowCastingMode = originalRenderer.shadowCastingMode;
        display.receiveShadows = originalRenderer.receiveShadows;
        display.lightProbeUsage = originalRenderer.lightProbeUsage;
        display.reflectionProbeUsage = originalRenderer.reflectionProbeUsage;
        display.probeAnchor = originalRenderer.probeAnchor;
        display.enabled = true;
    }

    private static bool AreDefaultTransforms(Transform group)
    {
        return group.localPosition.sqrMagnitude < 0.0000001f &&
            Quaternion.Angle(group.localRotation, Quaternion.identity) < 0.001f &&
            (group.localScale - Vector3.one).sqrMagnitude < 0.0000001f;
    }

    private static bool CreateRegionGroups(Transform area, bool front,
        Dictionary<string, string[]> map, Dictionary<string, Mesh> meshes,
        string groupRootName)
    {
        if (area.Find(groupRootName) != null) return false;

        // Upgrade the old two-group auditorium version only if no user group
        // movement will be lost. Otherwise abort the whole prefab update.
        if (!front)
        {
            string[] legacy = {
                "KZ_\u6a2a\u6881\u7ec4_\u53ef\u5355\u72ec\u4e0a\u4e0b\u79fb\u52a8",
                "KZ_\u7ad6\u67f1\u7ec4_\u4fdd\u6301\u539f\u4f4d"
            };
            foreach (string name in legacy)
            {
                Transform old = area.Find(name);
                if (old != null && !AreDefaultTransforms(old))
                {
                    throw new InvalidOperationException("Old auditorium group was moved manually: " +
                        name + ". Group positions were preserved; no overwrite performed.");
                }
            }
            foreach (string name in legacy)
            {
                Transform old = area.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
        }

        List<MeshFilter> sources = OriginalFilters(area, map);
        foreach (string key in map.Keys)
        {
            string expected = Models + key + ".obj";
            if (!sources.Any(m => AssetDatabase.GetAssetPath(m.sharedMesh) == expected))
                throw new InvalidOperationException("Missing expected source object in " + area.name + ": " + expected);
        }

        GameObject container = new GameObject(groupRootName);
        container.transform.SetParent(area, false);
        Dictionary<string, Transform> destinations = new Dictionary<string, Transform>();
        foreach (string kind in map.Values.SelectMany(a => a).Distinct().OrderBy(k => DisplayName(front, k)))
        {
            GameObject g = new GameObject(DisplayName(front, kind));
            g.transform.SetParent(container.transform, false);
            destinations[kind] = g.transform;
        }

        foreach (MeshFilter source in sources)
        {
            string stem = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(source.sharedMesh));
            MeshRenderer renderer = source.GetComponent<MeshRenderer>();
            if (renderer == null) throw new InvalidOperationException("Missing source MeshRenderer for " + stem);
            foreach (string kind in map[stem])
            {
                Mesh split = meshes[stem + "/" + kind];
                CreatePiece(source.transform, renderer, destinations[kind], stem + "__" + kind, split);
            }
        }
        return true;
    }

    // Original OBJ assets stay untouched. The now-obsolete renderer is hidden,
    // and its combined MeshFilter reference is emptied when safe, so clicking
    // an old 'default' mesh cannot outline all walls + beams as one mesh.
    private static bool RemoveLegacySelectionBox(Transform area,
        Dictionary<string, string[]> map, string rootName)
    {
        Transform groups = area.Find(rootName);
        if (groups == null) return false;
        int required = map.Values.SelectMany(x => x).Distinct().Count();
        if (groups.GetComponentsInChildren<MeshFilter>(true)
            .Count(f => f != null && f.sharedMesh != null) < required)
        {
            throw new InvalidOperationException("Interior groups incomplete under " + area.name +
                "; refusing to hide the original wood mesh.");
        }

        bool changed = false;
        foreach (MeshFilter source in OriginalFilters(area, map))
        {
            MeshRenderer r = source.GetComponent<MeshRenderer>();
            if (r == null) continue;
            if (r.enabled)
            {
                r.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                changed = true;
            }
            if (source.GetComponent<Collider>() != null ||
                source.GetComponents<MonoBehaviour>().Length > 0)
            {
                // Keep the source mesh for a collider/script-bearing object.
                // All replacement graphics remain independent either way.
                continue;
            }
            source.sharedMesh = null;
            PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            changed = true;
        }
        return changed;
    }

    private static void InstallInteriorOnly()
    {
        GameObject loaded = null;
        try
        {
            ValidateOriginalMeshes();
            // Verify all imports BEFORE opening or editing any prefab.
            var frontMeshes = LoadGroupMeshes(FrontMap, FrontPieces);
            var hallMeshes = LoadGroupMeshes(HallMap, HallPieces);
            loaded = PrefabUtility.LoadPrefabContents(Full);
            Transform front = FindNode(loaded, "02_FrontRooms");
            Transform hall = FindNode(loaded, "03_Auditorium");
            if (front == null || hall == null)
                throw new InvalidOperationException("Expected 02_FrontRooms and 03_Auditorium under FULL.prefab.");

            // Do not modify anything if the desired grouping is already present
            // and no legacy source mesh refs are left.
            bool needsFront = front.Find(FrontRootName) == null;
            bool needsHall = hall.Find(HallRootName) == null;
            if (!needsFront && !needsHall &&
                OriginalFilters(front, FrontMap).Count == 0 &&
                OriginalFilters(hall, HallMap).Count == 0) return;

            string folder = "Library/KongZuoP0_Backups";
            Directory.CreateDirectory(folder);
            string backup = folder + "/FULL_before_InteriorOnly_" +
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".prefab";
            File.Copy(Full, backup, false);

            bool changed = false;
            if (needsFront)
                changed |= CreateRegionGroups(front, true, FrontMap, frontMeshes, FrontRootName);
            if (needsHall)
                changed |= CreateRegionGroups(hall, false, HallMap, hallMeshes, HallRootName);
            changed |= RemoveLegacySelectionBox(front, FrontMap, FrontRootName);
            changed |= RemoveLegacySelectionBox(hall, HallMap, HallRootName);
            if (!changed) return;
            bool success;
            PrefabUtility.SaveAsPrefabAsset(loaded, Full, out success);
            if (!success) throw new IOException("Unity could not save the FULL prefab; backup: " + backup);
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
            Debug.Log("[KZ Interior Only] 02_FrontRooms and 03_Auditorium timber split into movable groups. " +
                "Exterior and roof are untouched. Saved FULL.prefab. Backup: " + backup);
        }
        catch (Exception ex)
        {
            Debug.LogError("[KZ Interior Only] Nothing committed after validation failure, or a save failed. " +
                "Inspect the log and your backup before proceeding. Details: " + ex);
        }
        finally
        {
            if (loaded != null) PrefabUtility.UnloadPrefabContents(loaded);
        }
    }
}
#endif
