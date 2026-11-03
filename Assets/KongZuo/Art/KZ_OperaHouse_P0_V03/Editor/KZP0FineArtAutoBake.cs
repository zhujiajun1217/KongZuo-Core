#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// One-time editor-side integration of the P0 art finish into the EXISTING opera-house prefab.
/// No menu, no run-time installer, no change to original model source, collision or player code.
/// Art geometry and textures live directly in the original P0 Models / Textures folders.
/// </summary>
[InitializeOnLoad]
public static class KZP0FineArtAutoBake
{
    const string Root = "Assets/KongZuo/Art/KZ_OperaHouse_P0_V03";
    const string Models = Root + "/Models/";
    const string Textures = Root + "/Textures/";
    const string Materials = Root + "/Generated/Materials";
    const string WholePrefab = Root + "/Generated/Prefabs/KZ_OperaHouse_P0_V03_FULL.prefab";
    const string ArtChild = "09_Finished_Art";
    const string LegacyChild = "KZ_V06_Cinematic_ArtLayer__DELETE_TO_UNDO";

    static readonly string[] EnvironmentParts = {
        "FLOOR_OAK", "OAK_DARK", "OAK_GOLD", "AGED_PLASTER",
        "CINNABAR_VELVET", "BRONZE", "LANTERN_PAPER"
    };
    static readonly string[] ChairParts = {
        "OAK_DARK", "OAK_GOLD", "SEAT_BURGUNDY", "BRONZE"
    };
    static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();
    static bool queued;
    static bool working;
    static bool waitingForReady;

    static KZP0FineArtAutoBake()
    {
        EditorApplication.projectChanged += Queue;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        Queue();
    }

    public static void Queue()
    {
        if (working || queued) return;
        queued = true;
        EditorApplication.delayCall += TryIntegrate;
    }

    // Called synchronously by the full-rebuild menu after saving the base prefab.
    // Unlike editor delayCall, this finishes before the new scenes are generated.
    public static bool ApplyAfterRebuild()
    {
        TryIntegrate();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WholePrefab);
        return prefab != null && prefab.transform.Find(ArtChild) != null;
    }

    static void TryIntegrate()
    {
        queued = false;
        if (working) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            // Import/recompile may still be running. Apply automatically at first idle frame.
            if (!waitingForReady)
            {
                waitingForReady = true;
                EditorApplication.update += RetryWhenIdle;
            }
            return;
        }
        if (!AllModelsAvailable()) return;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(WholePrefab);
        if (asset == null) return; // The V0.3 generated prefab must exist first.
        if (asset.transform.Find(ArtChild) != null) return; // Already baked; never duplicate.

        working = true;
        GameObject prefabRoot = null;
        try
        {
            MatCache.Clear();
            foreach (string key in new string[] {
                "FLOOR_OAK", "OAK_DARK", "OAK_GOLD", "AGED_PLASTER",
                "CINNABAR_VELVET", "BRONZE", "LANTERN_PAPER", "SEAT_BURGUNDY"
            }) GetOrMakeMat(key);

            prefabRoot = PrefabUtility.LoadPrefabContents(WholePrefab);
            if (prefabRoot == null) throw new Exception("无法读取原有完整 Prefab");
            Transform seats = FindChildByPrefix(prefabRoot.transform, "08_AudienceSeats_");
            if (seats == null) throw new Exception("原 Prefab 缺少 08_AudienceSeats，已停止，不会覆盖原模型");
            if (prefabRoot.transform.Find(ArtChild) != null) return;

            // The old scene-only art layer was never part of the original prefab, but
            // tolerate a previously saved copy of it under the prefab itself.
            Transform legacy = prefabRoot.transform.Find(LegacyChild);
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy.gameObject);

            GameObject layer = new GameObject(ArtChild);
            layer.transform.SetParent(prefabRoot.transform, false);
            layer.transform.localPosition = Vector3.zero;
            layer.transform.localRotation = Quaternion.identity;
            layer.transform.localScale = Vector3.one;

            foreach (string key in EnvironmentParts)
                AddPart(layer.transform, ArtModelPath(key), GetOrMakeMat(key), "Structure_" + key);
            foreach (string key in ChairParts)
                AddPart(layer.transform, ChairModelPath(key), GetOrMakeMat(key), "Seats_" + key);

            // The finished chairs replace only the low-poly LOOK; box colliders,
            // seat hierarchy, gameplay anchors and special seat sign remain intact.
            foreach (Renderer oldRenderer in seats.GetComponentsInChildren<Renderer>(true))
            {
                if (oldRenderer.gameObject.name.Contains("SIGN_SEAT") ||
                    oldRenderer.gameObject.name.Contains("__SIGN_") ||
                    oldRenderer.gameObject.name.Contains("__GOLD")) continue;
                oldRenderer.enabled = false;
            }

            for (int side = -1; side <= 1; side += 2)
                foreach (float z in new float[] { -8.35f, -4.45f, -.55f, 3.10f })
                    AddPointLight(layer.transform, "Wall_Lantern", new Vector3(side * 9.10f, 3.45f, z),
                                  new Color(1f, .68f, .35f), .44f, 4.5f);
            AddSpotLight(layer.transform, "Stage_Key_Left", new Vector3(-5.4f, 6.78f, 1.8f),
                new Vector3(-2.4f, 2.5f, 8.2f), new Color(1f, .72f, .43f), 1.45f, 14f, 65f);
            AddSpotLight(layer.transform, "Stage_Key_Right", new Vector3(5.4f, 6.78f, 1.8f),
                new Vector3(2.4f, 2.5f, 8.2f), new Color(1f, .69f, .37f), 1.35f, 14f, 65f);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, WholePrefab);
            if (saved == null) throw new Exception("无法保存完整 Prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("[空座 P0] 一楼精修已直接写入原有完整 Prefab。打开已有戏楼场景即可看到精修；不需要手动添加美术层。");
        }
        catch (Exception e)
        {
            Debug.LogError("[空座 P0] 自动精修整合未完成，原 Prefab 不应被替换：" + e.Message);
        }
        finally
        {
            if (prefabRoot != null) PrefabUtility.UnloadPrefabContents(prefabRoot);
            working = false;
        }
    }

    static void RetryWhenIdle()
    {
        if (working || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        waitingForReady = false;
        EditorApplication.update -= RetryWhenIdle;
        Queue();
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (!scene.IsValid() || EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject whole = AssetDatabase.LoadAssetAtPath<GameObject>(WholePrefab);
        if (whole == null || whole.transform.Find(ArtChild) == null) return;
        List<Transform> oldLayers = new List<Transform>();
        foreach (GameObject rootObj in scene.GetRootGameObjects())
            foreach (Transform node in rootObj.GetComponentsInChildren<Transform>(true))
                if (node.name == LegacyChild && node.parent != null &&
                    node.parent.Find(ArtChild) != null)
                    oldLayers.Add(node);
        foreach (Transform old in oldLayers)
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        if (oldLayers.Count > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[空座 P0] 已从本场景清理旧 V0.6 临时美术层。请按 Ctrl+S 保存场景。");
        }
    }

    static bool AllModelsAvailable()
    {
        foreach (string key in EnvironmentParts)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArtModelPath(key)) == null) return false;
        foreach (string key in ChairParts)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ChairModelPath(key)) == null) return false;
        foreach (string key in new string[] {
            "FLOOR_OAK", "OAK_DARK", "OAK_GOLD", "AGED_PLASTER",
            "CINNABAR_VELVET", "BRONZE", "LANTERN_PAPER", "SEAT_BURGUNDY"
        })
            foreach (string kind in new string[] { "Albedo", "Normal", "Mask" })
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(key, kind)) == null) return false;
        return true;
    }

    static string ArtModelPath(string key) { return Models + "09_Finish__" + key + ".obj"; }
    static string ChairModelPath(string key) { return Models + "09_FinishChair__" + key + ".obj"; }
    static string TexturePath(string key, string kind) { return Textures + "T_FINISH_" + key + "_" + kind + ".png"; }

    static Transform FindChildByPrefix(Transform root, string prefix)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith(prefix, StringComparison.Ordinal)) return t;
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Material GetOrMakeMat(string key)
    {
        Material cached;
        if (MatCache.TryGetValue(key, out cached)) return cached;
        EnsureFolder(Materials);
        string path = Materials + "/KZM_FINISH_" + key + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) { MatCache[key] = mat; return mat; }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) throw new Exception("未找到 URP Lit 或 Standard 着色器");
        string albedoPath = TexturePath(key, "Albedo");
        string normalPath = TexturePath(key, "Normal");
        string maskPath = TexturePath(key, "Mask");
        ConfigureTexture(albedoPath, false);
        ConfigureTexture(normalPath, true);
        ConfigureTexture(maskPath, false);
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
        if (albedo == null) throw new Exception("缺少贴图 " + albedoPath);

        mat = new Material(shader);
        mat.name = "KZM_FINISH_" + key;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (normal != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
            if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", .42f);
        }
        bool metal = key == "BRONZE";
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metal ? .85f : 0f);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", metal ? .52f : (key == "FLOOR_OAK" ? .35f : .23f));
        if (mask != null && mat.HasProperty("_MetallicGlossMap"))
        {
            mat.SetTexture("_MetallicGlossMap", mask);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        if (key == "LANTERN_PAPER")
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", new Color(.42f, .19f, .035f));
        }
        AssetDatabase.CreateAsset(mat, path);
        MatCache[key] = mat;
        return mat;
    }

    static void ConfigureTexture(string path, bool normal)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        bool changed = false;
        TextureImporterType desired = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (ti.textureType != desired) { ti.textureType = desired; changed = true; }
        if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; changed = true; }
        if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; changed = true; }
        if (changed) ti.SaveAndReimport();
    }

    static void AddPart(Transform parent, string path, Material material, string objectName)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new Exception("缺少模型 " + path);
        GameObject inst = PrefabUtility.InstantiatePrefab(asset, parent.gameObject.scene) as GameObject;
        if (inst == null) throw new Exception("无法实例化模型 " + path);
        inst.name = objectName;
        inst.transform.SetParent(parent, false);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
        foreach (Renderer renderer in inst.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = renderer.sharedMaterials;
            if (mats.Length == 0) renderer.sharedMaterial = material;
            else
            {
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                renderer.sharedMaterials = mats;
            }
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
        // Only the EXISTING V0.3 static colliders are authoritative for movement.
        foreach (Collider collider in inst.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);
    }

    static void AddPointLight(Transform parent, string name, Vector3 position,
                              Color color, float intensity, float range)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        Light lt = go.AddComponent<Light>();
        lt.type = LightType.Point; lt.color = color; lt.intensity = intensity;
        lt.range = range; lt.shadows = LightShadows.None;
    }

    static void AddSpotLight(Transform parent, string name, Vector3 position, Vector3 target,
                             Color color, float intensity, float range, float angle)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.LookRotation(target - position, Vector3.up);
        Light lt = go.AddComponent<Light>();
        lt.type = LightType.Spot; lt.color = color; lt.intensity = intensity;
        lt.range = range; lt.spotAngle = angle; lt.shadows = LightShadows.Soft;
    }
}

/// <summary>Also bake automatically if the original V0.3 rebuild tool regenerates the prefab.</summary>
public sealed class KZP0FineArtAssetWatcher : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                       string[] moved, string[] movedFrom)
    {
        const string prefab = "Assets/KongZuo/Art/KZ_OperaHouse_P0_V03/Generated/Prefabs/KZ_OperaHouse_P0_V03_FULL.prefab";
        foreach (string path in imported)
            if (path == prefab || path.Contains("/09_Finish") || path.Contains("/T_FINISH_"))
            { KZP0FineArtAutoBake.Queue(); return; }
    }
}
#endif
