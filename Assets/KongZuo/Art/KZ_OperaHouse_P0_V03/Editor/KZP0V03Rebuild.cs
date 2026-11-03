#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Changming Opera House P0 V0.3 team scene assembler.
/// Uses the local P0 OBJ assets to generate one prefab and two preview scenes.
/// Does not modify existing gameplay scenes.
/// </summary>
public static class KZP0V03Rebuild
{
    const string Root = "Assets/KongZuo/Art/KZ_OperaHouse_P0_V03";
    const string ModelDir = Root + "/Models";
    const string TextureDir = Root + "/Textures";
    const string GenDir = Root + "/Generated";
    const string MatDir = GenDir + "/Materials";
    const string PrefabDir = GenDir + "/Prefabs";
    const string SceneDir = GenDir + "/Scenes";
    const string SceneExterior = SceneDir + "/KZ_P0_V03_Exterior.unity";
    const string SceneInterior = SceneDir + "/KZ_P0_V03_Interior.unity";
    const string PrefabWhole = PrefabDir + "/KZ_OperaHouse_P0_V03_FULL.prefab";
    // The first-floor auditorium/stage timber reaches roughly Y=7.6.  Place
    // floor two at Y=8.0 to prevent the slab from cutting through that frame.
    const float SecondFloorBaseHeight = 8.0f;
    // Keep floor two as tall as the enlarged first-floor space.
    const float SecondFloorHeight = 8.0f;
    // The authored perimeter has uneven wall tops (roughly Y=4.2..5.2).
    // Join at Y=4.0 with a small overlap so all four sides remain sealed.
    const float FirstFloorWallJoinHeight = 4.0f;
    // The source roof's lowest eaves were authored at approximately Y=4.6.
    const float AuthoredRoofBaseHeight = 4.6f;

    static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>(StringComparer.Ordinal)
    {
        { "STONE", new Color(0.4300f,0.4000f,0.3500f) },
        { "STONE_LIGHT", new Color(0.6000f,0.5700f,0.5100f) },
        { "STONE_DARK", new Color(0.2900f,0.3000f,0.3000f) },
        { "PLASTER", new Color(0.6400f,0.5700f,0.4700f) },
        { "PLASTER_OLD", new Color(0.5200f,0.4800f,0.4000f) },
        { "TIMBER", new Color(0.3100f,0.1900f,0.1500f) },
        { "TIMBER_DARK", new Color(0.2000f,0.1200f,0.1050f) },
        { "TIMBER_LIGHT", new Color(0.4500f,0.3100f,0.2300f) },
        { "ROOF_TILE", new Color(0.2600f,0.4000f,0.3800f) },
        { "ROOF_SHADOW", new Color(0.1500f,0.2500f,0.2500f) },
        { "ROOF_GLAZE", new Color(0.3700f,0.5200f,0.4600f) },
        { "CINNABAR", new Color(0.5300f,0.1900f,0.1600f) },
        { "CURTAIN", new Color(0.4800f,0.1300f,0.1300f) },
        { "CURTAIN_DARK", new Color(0.3000f,0.0900f,0.1000f) },
        { "GOLD", new Color(0.7200f,0.5100f,0.2200f) },
        { "BRASS", new Color(0.5500f,0.4000f,0.2400f) },
        { "BLACK", new Color(0.1200f,0.1300f,0.1400f) },
        { "SEAT", new Color(0.3500f,0.2100f,0.1800f) },
        { "SEAT_CUSHION", new Color(0.3100f,0.1500f,0.1400f) },
        { "INK", new Color(0.1000f,0.2000f,0.2000f) },
        { "CLOTH_BLUE", new Color(0.2400f,0.3500f,0.4000f) },
        { "CLOTH_CREAM", new Color(0.6800f,0.5900f,0.4500f) },
        { "CLOTH_ROSE", new Color(0.4700f,0.2500f,0.2200f) },
        { "PAPER", new Color(0.7800f,0.7000f,0.5500f) },
        { "GLASS", new Color(0.3900f,0.4900f,0.4900f) },
        { "SMOKE", new Color(0.2100f,0.1800f,0.1600f) },
        { "DUST", new Color(0.4300f,0.4300f,0.3800f) },
        { "METAL", new Color(0.3600f,0.3600f,0.3400f) },
        { "SOOT", new Color(0.1500f,0.1400f,0.1300f) },
        { "WARM_LIGHT", new Color(0.9500f,0.6600f,0.3500f) },
        { "FOLIAGE", new Color(0.2800f,0.3800f,0.3100f) },
        { "GRAYBOX", new Color(0.5200f,0.5400f,0.5500f) },
    };
    static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();
    static readonly string[] RepeatableStoreyZones = new string[] {
        "01_Exterior", "02_FrontRooms", "03_Auditorium", "04_Stage",
        "05_Backstage", "06_RearRooms" };
    static bool generating;

    [MenuItem("Tools/空座/楼层/① 生成完整替换包场景", false, 110)]
    public static void BuildAll()
    {
        if (generating || !CanSwitchScenes()) return;
        generating = true;
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.IsValidFolder(ModelDir))
                throw new InvalidOperationException("P0 Models folder missing. Copy the whole ZIP Assets folder, not only this script.");
            EnsureFolder(MatDir); EnsureFolder(PrefabDir); EnsureFolder(SceneDir);
            MatCache.Clear();
            var regular = MakeSeatPrefab(false);
            var special = MakeSeatPrefab(true);
            if (regular == null || special == null)
                throw new InvalidOperationException("Chair source OBJs failed to import.");
            // Only uses source OBJs contained in this package.
            GameObject assembled = BuildRoot(regular, special);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(assembled, PrefabWhole);
            if (saved == null) throw new InvalidOperationException("Could not save new complete prefab: " + PrefabWhole);
            UnityEngine.Object.DestroyImmediate(assembled);

            // Rebuilding the complete prefab replaces its previous contents.
            // Reapply the V06 finished art before any scene instantiates the prefab.
            if (!KZP0FineArtAutoBake.ApplyAfterRebuild())
                throw new InvalidOperationException("精修美术层未能恢复；请检查 09_Finish 模型和贴图资源是否已导入。未生成新预览场景。");

            CreateDemoScene(PrefabWhole, SceneExterior, false);
            CreateDemoScene(PrefabWhole, SceneInterior, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(SceneInterior, OpenSceneMode.Single);
            FocusInterior();
        }
        catch (Exception ex)
        {
            Debug.LogError("[KZ P0 V0.3] Generation stopped: " + ex);
        }
        finally { generating = false; }
    }

    // No modal prompts. Never discard changes in another open team scene.
    static bool CanSwitchScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.isDirty)
            {
                Debug.LogWarning("[KZ P0 V0.3] Save the currently modified Unity scene before generating or switching previews.");
                return false;
            }
        }
        return true;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\','/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Material GetMat(string name)
    {
        if (MatCache.ContainsKey(name)) return MatCache[name];
        EnsureFolder(MatDir);
        string path = MatDir + "/KZM_" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("HDRP/Lit");
            if (shader == null) throw new InvalidOperationException("No usable Lit shader found. Please check Unity's render pipeline.");
            mat = new Material(shader) { name = "KZM_" + name };
            AssetDatabase.CreateAsset(mat, path);
        }
        Color color = Color.white;
        if (Palette.ContainsKey(name)) color = Palette[name];
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", name == "BRASS" || name == "GOLD" || name == "GLASS" ? 0.48f : 0.15f);
        if (name.StartsWith("SIGN_", StringComparison.Ordinal))
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureDir + "/T_" + name + ".png");
            if (tex == null) Debug.LogWarning("P0 sign texture missing for " + name);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        }
        EditorUtility.SetDirty(mat);
        MatCache[name] = mat;
        return mat;
    }

    static string[] ObjPaths(string prefix)
    {
        if (!Directory.Exists(ModelDir)) return new string[0];
        return Directory.GetFiles(ModelDir, prefix + "__*.obj", SearchOption.TopDirectoryOnly)
            .Select(s => s.Replace('\\','/'))
            .OrderBy(s => s, StringComparer.Ordinal).ToArray();
    }

    static GameObject AddModelParts(string prefix, Transform parent, bool addMeshCollider = false)
    {
        GameObject zone = new GameObject(prefix);
        zone.transform.SetParent(parent, false);
        string[] paths = ObjPaths(prefix);
        if (paths.Length == 0) Debug.LogWarning("P0: no model files for " + prefix);
        foreach (string path in paths)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) { Debug.LogError("OBJ not imported: " + path); continue; }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (instance == null) { Debug.LogError("Cannot instantiate OBJ: " + path); continue; }
            instance.name = Path.GetFileNameWithoutExtension(path);
            instance.transform.SetParent(zone.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            string filename = Path.GetFileNameWithoutExtension(path);
            int divider = filename.LastIndexOf("__", StringComparison.Ordinal);
            string materialName = divider >= 0 ? filename.Substring(divider+2) : "STONE";
            Material mat = GetMat(materialName);
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = r.sharedMaterials;
                for (int i=0; i<shared.Length; i++) shared[i] = mat;
                r.sharedMaterials = shared;
                if (addMeshCollider) r.enabled = false;
            }
            if (addMeshCollider)
            {
                foreach (MeshFilter mf in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    MeshCollider mc = mf.GetComponent<MeshCollider>();
                    if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = false; // static scene collision with openings preserved
                }
            }
        }
        return zone;
    }

    static GameObject MakeSeatPrefab(bool special)
    {
        string type = special ? "08_Seat07" : "08_SeatRegular";
        string assetPath = PrefabDir + "/KZ_P0_" + (special ? "R01_S07" : "WoodenSeat") + ".prefab";
        GameObject temp = new GameObject(special ? "P0_R01_S07_EmptySeat" : "P0_WoodenSeat");
        AddModelParts(type, temp.transform);
        // Actual single seat is local; a row is built from independent prefab instances.
        var bc = temp.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, .68f, -.12f);
        bc.size = new Vector3(.81f, 1.36f, .78f);
        var p = PrefabUtility.SaveAsPrefabAsset(temp, assetPath);
        UnityEngine.Object.DestroyImmediate(temp);
        return p;
    }

    static GameObject BuildRoot(GameObject normalSeat, GameObject specialSeat)
    {
        GameObject root = new GameObject("KZ_P0_V03_CHANGMING_OPERA_HOUSE");
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        GameObject firstFloor = new GameObject("第一层");
        firstFloor.transform.SetParent(root.transform, false);
        foreach (string s in RepeatableStoreyZones) AddModelParts(s, firstFloor.transform);

        // Floor two is intentionally plain greybox geometry.  Do not clone
        // the detailed ground floor: that would also repeat the entrance,
        // doors, trees, furniture and front paving on the upper level.
        GameObject secondFloor = new GameObject("第二层");
        secondFloor.transform.SetParent(root.transform, false);
        secondFloor.transform.localPosition = Vector3.up * SecondFloorBaseHeight;
        CreateGrayboxStorey(secondFloor.transform);

        // The roof used to sit immediately above floor one.  It now sits above
        // floor two while preserving all of its original proportions.
        GameObject roof = AddModelParts("07_Roofs", root.transform);
        roof.name = "房顶";
        float roofOffset = SecondFloorBaseHeight + SecondFloorHeight - AuthoredRoofBaseHeight;
        roof.transform.localPosition = Vector3.up * roofOffset;
        roof.SetActive(true);
        GameObject collision = AddModelParts("99_Collision", firstFloor.transform, true);
        collision.name = "99_PhysicalColliders_Floor01__do_not_disable";
        GameObject seats = new GameObject("08_AudienceSeats_9x13__no_centre_aisle");
        seats.transform.SetParent(firstFloor.transform, false);
        for (int row=0; row<9; row++)
        {
            GameObject r = new GameObject("Row_"+(row+1).ToString("00"));
            r.transform.SetParent(seats.transform, false);
            for (int seat=1; seat<=13; seat++)
            {
                bool key=(row==0 && seat==7);
                GameObject prefab = key ? specialSeat : normalSeat;
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = key ? "R01_S07__P0_KEY_Seat" :
                    "R"+(row+1).ToString("00")+"_S"+seat.ToString("00");
                instance.transform.SetParent(r.transform,false);
                instance.transform.localPosition=new Vector3((seat-7)*1.06f,0f,2.40f-row*1.39f);
                instance.transform.localRotation=Quaternion.identity;
                instance.transform.localScale=Vector3.one;
            }
        }
        CreateAnchors(firstFloor.transform);
        return root;
    }

    static void CreateGrayboxStorey(Transform parent)
    {
        // Main plaster shell bounds in the source model are approximately
        // X -10.18..10.18 and Z -18.175..18.175.
        const float width = 20.36f;
        const float depth = 36.35f;
        const float wall = 0.24f;
        const float slab = 0.20f;
        Material material = GetMat("GRAYBOX");

        // Keep the second-floor top at local Y=8, but extend its perimeter
        // downward from world Y=8 to Y=4, slightly overlapping every side of
        // the uneven first-floor perimeter so no daylight gap remains.
        float wallBottom = FirstFloorWallJoinHeight - SecondFloorBaseHeight;
        float wallTop = SecondFloorHeight;
        float joinedWallHeight = wallTop - wallBottom;
        float joinedWallCenter = (wallTop + wallBottom) * .5f;

        // Four connected slab bands form a true mezzanine ring.  The opening
        // X -7..7, Z -10..13 stays completely open above auditorium and stage.
        CreateGrayboxCube(parent, "南侧放映室与检修回廊楼板【颜色标注：旧木棕】",
            new Vector3(0f, slab*.5f, -14.0875f), new Vector3(width, slab, 8.175f), material);
        CreateGrayboxCube(parent, "北侧观景回廊楼板【颜色标注：旧木棕】",
            new Vector3(0f, slab*.5f, 15.5875f), new Vector3(width, slab, 5.175f), material);
        CreateGrayboxCube(parent, "西侧观景回廊楼板【颜色标注：旧木棕】",
            new Vector3(-8.59f, slab*.5f, 1.5f), new Vector3(3.18f, slab, 23f), material);
        // The east side is split to leave a generous opening for the U stair.
        CreateGrayboxCube(parent, "东侧观景回廊_外沿楼板【颜色标注：旧木棕】",
            new Vector3(9.59f, slab*.5f, 1.5f), new Vector3(1.18f, slab, 23f), material);
        CreateGrayboxCube(parent, "东侧观景回廊_主楼板【颜色标注：旧木棕】",
            new Vector3(8f, slab*.5f, 3.75f), new Vector3(2f, slab, 18.5f), material);
        CreateGrayboxCube(parent, "Wall_Front__no_door", new Vector3(0f, joinedWallCenter, -depth * .5f),
            new Vector3(width, joinedWallHeight, wall), material);
        CreateGrayboxCube(parent, "Wall_Back", new Vector3(0f, joinedWallCenter, depth * .5f),
            new Vector3(width, joinedWallHeight, wall), material);
        CreateGrayboxCube(parent, "Wall_Left", new Vector3(-width * .5f, joinedWallCenter, 0f),
            new Vector3(wall, joinedWallHeight, depth), material);
        CreateGrayboxCube(parent, "Wall_Right", new Vector3(width * .5f, joinedWallCenter, 0f),
            new Vector3(wall, joinedWallHeight, depth), material);

        CreateSecondFloorRoomsAndProps(parent, material);
    }

    static void CreateSecondFloorRoomsAndProps(Transform parent, Material material)
    {
        Transform room = new GameObject("01_放映室_房间分区").transform;
        room.SetParent(parent, false);
        const float roomWidth = 8.5f;
        const float roomDepth = 4.6f;
        const float roomHeight = 3.2f;
        const float partition = .16f;
        const float roomZ = -15.25f;

        CreateGrayboxCube(room, "放映室_西墙【颜色标注：灰白旧墙】",
            new Vector3(-roomWidth*.5f, roomHeight*.5f, roomZ),
            new Vector3(partition, roomHeight, roomDepth), material);
        // East wall contains the room door opening.
        CreateGrayboxCube(room, "放映室_东墙南段【颜色标注：灰白旧墙】",
            new Vector3(roomWidth*.5f, roomHeight*.5f, -15.925f),
            new Vector3(partition, roomHeight, 3.25f), material);
        CreateGrayboxCube(room, "放映室_东墙北段【颜色标注：灰白旧墙】",
            new Vector3(roomWidth*.5f, roomHeight*.5f, -13.025f),
            new Vector3(partition, roomHeight, .15f), material);
        CreateGrayboxCube(room, "放映室_门楣【颜色标注：深褐木色】",
            new Vector3(roomWidth*.5f, 2.65f, -13.7f),
            new Vector3(.24f, .25f, 1.2f), material);
        CreateGrayboxCube(room, "放映室_南墙【颜色标注：灰白旧墙】",
            new Vector3(0f, roomHeight*.5f, roomZ-roomDepth*.5f),
            new Vector3(roomWidth, roomHeight, partition), material);
        // Projection opening faces +Z, directly toward the stage.
        CreateGrayboxCube(room, "放映室_北墙左段【颜色标注：灰白旧墙】",
            new Vector3(-2.775f, roomHeight*.5f, roomZ+roomDepth*.5f),
            new Vector3(2.95f, roomHeight, partition), material);
        CreateGrayboxCube(room, "放映室_北墙右段【颜色标注：灰白旧墙】",
            new Vector3(2.775f, roomHeight*.5f, roomZ+roomDepth*.5f),
            new Vector3(2.95f, roomHeight, partition), material);
        CreateGrayboxCube(room, "放映窗口下墙【颜色标注：灰白旧墙】",
            new Vector3(0f,.45f,roomZ+roomDepth*.5f), new Vector3(2.6f,.9f,partition), material);
        CreateGrayboxCube(room, "放映窗口上墙【颜色标注：灰白旧墙】",
            new Vector3(0f,2.65f,roomZ+roomDepth*.5f), new Vector3(2.6f,1.1f,partition), material);
        CreateGrayboxCube(room, "放映窗口窗台【颜色标注：深褐木色】",
            new Vector3(0f,.93f,roomZ+roomDepth*.5f), new Vector3(2.8f,.12f,.28f), material);
        CreateGrayboxCube(room, "放映窗口顶框【颜色标注：深褐木色】",
            new Vector3(0f,2.07f,roomZ+roomDepth*.5f), new Vector3(2.8f,.12f,.28f), material);

        Transform props = new GameObject("02_放映室_物品灰盒").transform;
        props.SetParent(parent, false);
        CreateGrayboxCube(props, "放映桌【颜色标注：旧木棕】",
            new Vector3(-1.8f, .65f, -15.5f), new Vector3(3.8f, 1.3f, 1.25f), material);
        CreateProjector(props, "左放映机", new Vector3(-2.5f, 1.55f, -15.25f), material);
        CreateProjector(props, "右放映机", new Vector3(-.8f, 1.55f, -15.25f), material);
        CreateGrayboxCube(props, "胶片器材柜【颜色标注：暗绿色金属】",
            new Vector3(2.75f, 1.15f, -16.25f), new Vector3(1.8f, 2.3f, .75f), material);
        for (int shelf=0; shelf<3; shelf++)
            CreateGrayboxCube(props, "器材柜层板"+(shelf+1)+"【颜色标注：暗绿色金属】",
                new Vector3(2.75f, .45f+shelf*.65f, -15.84f), new Vector3(1.65f, .08f, .12f), material);
        CreateGrayboxCube(props, "旧海报【颜色标注：褪色红黄纸张】",
            new Vector3(-2.1f, 1.75f, -17.64f), new Vector3(1.1f, 1.55f, .04f), material);

        Transform balcony = new GameObject("03_检修观演回廊_仅栏杆_无观众席").transform;
        balcony.SetParent(parent, false);
        CreateInnerRailing(balcony, "南侧", new Vector3(0f,0f,-10f), 14f, true, material);
        CreateInnerRailing(balcony, "北侧", new Vector3(0f,0f,13f), 14f, true, material);
        CreateInnerRailing(balcony, "西侧", new Vector3(-7f,0f,1.5f), 23f, false, material);
        // Leave the south part of the east inner edge open for the stair exit.
        CreateInnerRailing(balcony, "东侧", new Vector3(7f,0f,3.85f), 18.3f, false, material);

        Transform stairs = new GameObject("04_一楼右通道_中式回折楼梯").transform;
        stairs.SetParent(parent, false);
        const int stepsPerFlight = 16;
        const float stepRise = .25f;
        const float stepRun = .35f;
        CreateGrayboxCube(stairs, "一楼楼梯下平台【颜色标注：朱红木色】",
            new Vector3(8f, -SecondFloorBaseHeight+.1f, -5.7f),
            new Vector3(2.1f, .2f, 1.6f), material);
        CreateStairFlight(stairs, "第一跑", 8f, -6.5f, -1f, 0f,
            stepsPerFlight, stepRise, stepRun, material);
        CreateGrayboxCube(stairs, "半层回转平台【颜色标注：朱红木色】",
            new Vector3(7.2f, -3.9f, -12.35f), new Vector3(3.4f, .2f, 1.6f), material);
        CreateStairFlight(stairs, "第二跑", 6.4f, -11.75f, 1f, 4f,
            stepsPerFlight, stepRise, stepRun, material);
        CreateGrayboxCube(stairs, "二楼楼梯上平台【颜色标注：朱红木色】",
            new Vector3(7.2f, .1f, -5.8f), new Vector3(3.4f, .2f, 1.6f), material);

        CreateHangingLantern(parent, "回廊灯笼_左", new Vector3(-6.5f, 2.5f, -9f), material);
        CreateHangingLantern(parent, "回廊灯笼_右", new Vector3(6.5f, 2.5f, -9f), material);
    }

    static void CreateInnerRailing(Transform parent, string sideName, Vector3 center,
        float length, bool alongX, Material material)
    {
        Vector3 railScale = alongX ? new Vector3(length,.12f,.12f) : new Vector3(.12f,.12f,length);
        CreateGrayboxCube(parent, sideName+"中式栏杆_上横梁【颜色标注：朱红木色】",
            center+Vector3.up*1.08f, railScale, material);
        CreateGrayboxCube(parent, sideName+"中式栏杆_中横梁【颜色标注：朱红木色】",
            center+Vector3.up*.55f, railScale, material);
        int postCount = Mathf.CeilToInt(length/1.5f);
        for (int post=0; post<=postCount; post++)
        {
            float offset = -length*.5f + length*post/postCount;
            Vector3 p = center + (alongX ? Vector3.right*offset : Vector3.forward*offset);
            CreateGrayboxCube(parent, sideName+"雕花栏杆柱"+(post+1)+"【颜色标注：朱红木色】",
                p+Vector3.up*.6f, new Vector3(.12f,1.2f,.12f), material);
        }
    }

    static void CreateStairFlight(Transform parent, string prefix, float x, float startZ,
        float zDirection, float startWorldHeight, int stepCount, float stepRise,
        float stepRun, Material material)
    {
        for (int step=0; step<stepCount; step++)
        {
            GameObject tread = CreateGrayboxCube(parent,
                prefix+"_踏步"+(step+1)+"【颜色标注：朱红木色】",
                new Vector3(x, startWorldHeight-SecondFloorBaseHeight+stepRise*(step+.5f),
                    startZ+zDirection*step*stepRun),
                new Vector3(1.45f,stepRise,.42f), material);
            Collider collider = tread.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }

        float horizontalRun = (stepCount-1)*stepRun;
        float verticalRise = stepCount*stepRise;
        float rampLength = Mathf.Sqrt(horizontalRun*horizontalRun + verticalRise*verticalRise);
        float angle = Mathf.Atan2(verticalRise,horizontalRun)*Mathf.Rad2Deg;
        GameObject ramp = CreateGrayboxCube(parent, prefix+"_连续通行碰撞斜坡_不可见",
            new Vector3(x, startWorldHeight+verticalRise*.5f-SecondFloorBaseHeight,
                startZ+zDirection*horizontalRun*.5f),
            new Vector3(1.32f,.14f,rampLength), material);
        ramp.transform.localRotation = Quaternion.Euler(-zDirection*angle,0f,0f);
        ramp.GetComponent<Renderer>().enabled = false;

        for (int step=0; step<stepCount; step+=4)
        {
            float y = startWorldHeight-SecondFloorBaseHeight+stepRise*(step+1f)+.55f;
            float z = startZ+zDirection*step*stepRun;
            CreateGrayboxCube(parent, prefix+"_护栏柱【颜色标注：朱红木色】",
                new Vector3(x-.83f,y,z), new Vector3(.1f,1.1f,.1f), material);
            CreateGrayboxCube(parent, prefix+"_护栏柱【颜色标注：朱红木色】",
                new Vector3(x+.83f,y,z), new Vector3(.1f,1.1f,.1f), material);
        }
    }

    static void CreateProjector(Transform parent, string prefix, Vector3 position, Material material)
    {
        CreateGrayboxCube(parent, prefix+"机身【颜色标注：深灰金属】",
            position, new Vector3(1.15f, .7f, .8f), material);
        GameObject lens = CreateGrayboxCylinder(parent, prefix+"镜头【颜色标注：黑色玻璃】",
            position+new Vector3(0f,0f,.52f), new Vector3(.22f,.28f,.22f), material);
        lens.transform.localRotation = Quaternion.Euler(90f,0f,0f);
        CreateGrayboxCylinder(parent, prefix+"胶片盘【颜色标注：银灰金属】",
            position+new Vector3(0f,.62f,0f), new Vector3(.48f,.07f,.48f), material);
    }

    static void CreateHangingLantern(Transform parent, string prefix, Vector3 position, Material material)
    {
        CreateGrayboxCylinder(parent, prefix+"灯体【颜色标注：旧红纸面暖黄光】",
            position, new Vector3(.38f,.7f,.38f), material);
        CreateGrayboxCube(parent, prefix+"吊杆【颜色标注：深褐木色】",
            position+new Vector3(0f,.75f,0f), new Vector3(.08f,.8f,.08f), material);
    }

    static GameObject CreateGrayboxCube(Transform parent, string name, Vector3 position,
        Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        // CreatePrimitive supplies a BoxCollider, so upper floors and walls are
        // immediately usable for blockout navigation without copied detail collision.
        return cube;
    }

    static GameObject CreateGrayboxCylinder(Transform parent, string name, Vector3 position,
        Vector3 scale, Material material)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = position;
        cylinder.transform.localScale = scale;
        cylinder.GetComponent<Renderer>().sharedMaterial = material;
        return cylinder;
    }

    static void Anchor(Transform parent, string name, float x, float y, float z)
    {
        GameObject go=new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition=new Vector3(x,y,z);
    }

    static void CreateAnchors(Transform parent)
    {
        Transform anchors = new GameObject("90_P0_AuthoringAnchors__no_gameplay_scripts").transform;
        anchors.SetParent(parent,false);
        Anchor(anchors,"A_MainEntrance",0,0,-18);
        Anchor(anchors,"B_OfficeTelephone",-7.75f,.95f,-15.85f);
        Anchor(anchors,"C_TicketRegister",7.22f,1.03f,-15.4f);
        Anchor(anchors,"D_Row01Seat07",0,.59f,2.40f);
        Anchor(anchors,"E_FoyerPoster",-2.65f,1.95f,-17.78f);
        Anchor(anchors,"F_PhotoViewpoint_BackWest",-3,1.6f,-10.55f);
        Anchor(anchors,"G_AudienceReturnPoint",0,0,-10.7f);
        Anchor(anchors,"H_StageStair_Left",-8.65f,.4f,4.2f);
        Anchor(anchors,"H_StageStair_Right",8.65f,.4f,4.2f);
        Anchor(anchors,"I_WingGate",8.7f,1.1f,11f);
        Anchor(anchors,"J_B1BlockedWest",-10,0,12.6f);
        Anchor(anchors,"K_EastLockedWorkDoor",10,1.1f,12.55f);
        Anchor(anchors,"P0_MakeupRoom",-7.5f,0,16);
        Anchor(anchors,"P0_CostumeRoom",-2.5f,0,16);
        Anchor(anchors,"P0_ArchiveLocked",2.5f,0,14);
        Anchor(anchors,"P0_ElectricalLocked",7.5f,0,14);
        Anchor(anchors,"P0_Office",-7.5f,0,-16);
        Anchor(anchors,"P0_TicketBooth",7.5f,0,-16);
        Anchor(anchors,"P0_StageCentre",0,.9f,7.5f);
    }

    static GameObject FindChildRecursive(Transform root, string startsWith)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
            if (t.name.StartsWith(startsWith,StringComparison.Ordinal)) return t.gameObject;
        return null;
    }

    static void CreateDemoScene(string prefabPath,string scenePath,bool interior)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) throw new InvalidOperationException("The saved P0 Prefab cannot be loaded.");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = prefab.name;
        instance.transform.position = Vector3.zero;
        GameObject roof = FindChildRecursive(instance.transform,"房顶");
        if (roof == null) roof = FindChildRecursive(instance.transform,"07_Roofs__toggle");
        if (roof == null) throw new InvalidOperationException("The detachable roof group is missing.");
        // Keep the relocated roof visible in both preview scenes.  The old
        // interior-preview behavior hid it even though it now belongs above
        // the second floor.
        roof.SetActive(true);
        if (interior) CreatePlayablePlayer();
        else
        {
            // The exterior scene remains a presentation preview.
            GameObject camera = new GameObject("_PREVIEW_CAMERA_NOT_FOR_GAMEPLAY");
            var ca = camera.AddComponent<Camera>();
            ca.clearFlags = CameraClearFlags.Skybox;
            ca.fieldOfView = 55;
            camera.transform.position = new Vector3(38,29,-47);
            camera.transform.LookAt(new Vector3(0,8,-1));
        }
        GameObject sun=new GameObject("_PREVIEW_SUN");
        var dl=sun.AddComponent<Light>();
        dl.type=LightType.Directional;dl.intensity=interior?1.35f:1.55f;
        dl.color=new Color(1.0f,.86f,.73f);
        sun.transform.rotation=Quaternion.Euler(43,-33,0);
        // Multiple modest interior lights; avoid external URP postprocessing dependencies.
        if (interior)
        {
            AddLamp("Foyer",0,3.45f,-14.55f,10f,1.5f);
            AddLamp("Office",-7.5f,2.8f,-15f,6f,1.3f);
            AddLamp("Ticket",7.5f,2.8f,-15f,6f,1.3f);
            AddLamp("AudienceBack",0,5.1f,-7f,12f,1.3f);
            AddLamp("AudienceFront",0,5.4f,1.2f,12f,1.2f);
            AddLamp("Stage",0,6.2f,7.1f,13f,1.5f);
            AddLamp("Backstage",0,3.3f,12.6f,12f,1.1f);
            AddLamp("Makeup",-7.5f,3.0f,16f,7f,1.2f);
            AddLamp("Costume",-2.5f,3.0f,16f,7f,1.0f);
        }
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.38f,.39f,.39f);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),scenePath))
            throw new InvalidOperationException("Could not save scene: " + scenePath);
    }

    [MenuItem("Tools/空座/P0 V0.3/④ 当前场景补建 Player（不重建模型）", false, 113)]
    static void AddPlayerToCurrentScene()
    {
        FPSController existing = UnityEngine.Object.FindFirstObjectByType<FPSController>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            Debug.Log("[KZ P0 V0.3] Player already exists at scene root: " + existing.name);
            return;
        }

        GameObject player = CreatePlayablePlayer();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = player;
        EditorGUIUtility.PingObject(player);
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("[KZ P0 V0.3] Added Player and Main Camera without rebuilding the model.");
    }

    static GameObject CreatePlayablePlayer()
    {
        GameObject player = new GameObject("Player");
        // No parent: Player is a scene-root sibling of the generated building.
        player.transform.SetParent(null);
        player.transform.position = new Vector3(0f, .1f, -22f);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.center = new Vector3(0f, .9f, 0f);
        controller.radius = .31f;
        controller.stepOffset = .30f;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 72f;
        cameraObject.AddComponent<AudioListener>();

        FPSController fps = player.AddComponent<FPSController>();
        fps.playerCamera = cameraObject.transform;
        return player;
    }

    static void AddLamp(string name,float x,float y,float z,float range,float intensity)
    {
        GameObject obj = new GameObject("_PREVIEW_POINTLIGHT_"+name);
        obj.transform.position=new Vector3(x,y,z);
        var lt=obj.AddComponent<Light>();
        lt.type=LightType.Point;lt.range=range;lt.intensity=intensity;
        lt.color=new Color(1f,.79f,.56f);
        lt.shadows=LightShadows.None;
    }

    static void FocusInterior()
    {
        SceneView view=SceneView.lastActiveSceneView;
        if (view != null)
        {
            view.orthographic = false;
            view.LookAt(new Vector3(0,1.5f,0), Quaternion.Euler(45,25,0), 30f, true);
            view.Repaint();
        }
    }

    enum FloorView { First, Second, Roof }

    [MenuItem("Tools/空座/楼层/第一层（屏蔽其他层）", false, 120)]
    static void ShowFirstFloorOnly() { ShowFloorOnly(FloorView.First); }

    [MenuItem("Tools/空座/楼层/第二层（屏蔽其他层）", false, 121)]
    static void ShowSecondFloorOnly() { ShowFloorOnly(FloorView.Second); }

    [MenuItem("Tools/空座/楼层/房顶（屏蔽其他层）", false, 122)]
    static void ShowRoofOnly() { ShowFloorOnly(FloorView.Roof); }

    static void ShowFloorOnly(FloorView requested)
    {
        GameObject building = FindBuildingInActiveScene();
        if (building == null)
        {
            Debug.LogError("[KZ P0 V0.3] 当前场景中没有找到 KZ_OperaHouse_P0_V03_FULL。");
            EditorUtility.DisplayDialog("楼层显示", "当前场景中没有找到戏楼模型。", "确定");
            return;
        }

        Transform[] children = Enumerable.Range(0, building.transform.childCount)
            .Select(i => building.transform.GetChild(i)).ToArray();
        Undo.RecordObjects(children.Select(t => (UnityEngine.Object)t.gameObject).ToArray(), "切换戏楼显示楼层");

        GameObject selectedGroup = null;
        foreach (Transform child in children)
        {
            FloorView group = ClassifyFloor(child.name);
            bool visible = group == requested;
            child.gameObject.SetActive(visible);
            if (visible && selectedGroup == null) selectedGroup = child.gameObject;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = selectedGroup != null ? selectedGroup : building;
        SceneView.RepaintAll();
    }

    static FloorView ClassifyFloor(string objectName)
    {
        if (objectName == "第二层" || objectName.StartsWith("10_SecondFloor", StringComparison.Ordinal))
            return FloorView.Second;
        if (objectName == "房顶" || objectName.StartsWith("07_Roofs", StringComparison.Ordinal))
            return FloorView.Roof;
        // 新层级中的“第一层”，以及旧 Prefab 中其余所有一楼对象。
        return FloorView.First;
    }

    static GameObject FindBuildingInActiveScene()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name.StartsWith("KZ_OperaHouse_P0_V03_FULL", StringComparison.Ordinal) ||
                root.name.StartsWith("KZ_P0_V03_CHANGMING_OPERA_HOUSE", StringComparison.Ordinal))
                return root;
        }
        return null;
    }

}
#endif
