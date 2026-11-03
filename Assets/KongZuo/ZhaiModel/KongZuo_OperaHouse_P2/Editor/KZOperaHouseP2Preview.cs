#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Changming Opera House art-only builder.
// All generated files stay under this isolated folder. No gameplay code is touched.
public static class KZOperaHouseP2Preview
{
    private const string ROOT = "Assets/KongZuo/ZhaiModel/KongZuo_OperaHouse_P2";
    private const string GEN = ROOT + "/Generated";
    private const string MATS = GEN + "/Materials";
    private const string MODEL = ROOT + "/Models";

    private struct ColorSpec
    {
        public float R, G, B;
        public ColorSpec(float r,float g,float b) { R=r;G=g;B=b; }
    }

    private static readonly Dictionary<string, ColorSpec> Colors = new Dictionary<string,ColorSpec>
    {
        {"STONE",new ColorSpec(0.4300f,0.4100f,0.3500f)},
        {"STONE_LITE",new ColorSpec(0.6000f,0.5700f,0.4900f)},
        {"PLASTER",new ColorSpec(0.6500f,0.5700f,0.4500f)},
        {"PLASTER_DARK",new ColorSpec(0.4200f,0.3800f,0.3200f)},
        {"TIMBER",new ColorSpec(0.3000f,0.1700f,0.1200f)},
        {"TIMBER_DARK",new ColorSpec(0.1800f,0.1050f,0.0850f)},
        {"TIMBER_LITE",new ColorSpec(0.4400f,0.2900f,0.2000f)},
        {"ROOF_TILE",new ColorSpec(0.2500f,0.3800f,0.3600f)},
        {"ROOF_SHADOW",new ColorSpec(0.1500f,0.2700f,0.2700f)},
        {"ROOF_GLAZE",new ColorSpec(0.3500f,0.4900f,0.4300f)},
        {"CINNABAR",new ColorSpec(0.5300f,0.1700f,0.1400f)},
        {"CURTAIN",new ColorSpec(0.4800f,0.1400f,0.1300f)},
        {"CURTAIN_SHADE",new ColorSpec(0.3300f,0.0950f,0.0850f)},
        {"GOLD",new ColorSpec(0.6900f,0.4800f,0.2200f)},
        {"BRASS",new ColorSpec(0.5100f,0.3700f,0.2100f)},
        {"DARK_PANEL",new ColorSpec(0.1000f,0.1000f,0.1000f)},
        {"SEAT_CUSHION",new ColorSpec(0.3700f,0.1900f,0.1500f)},
        {"TREE_WOOD",new ColorSpec(0.2400f,0.2000f,0.1500f)},
        {"TREE_FOLIAGE",new ColorSpec(0.2400f,0.3200f,0.2500f)},
        {"TREE_FOLIAGE2",new ColorSpec(0.3300f,0.3800f,0.2900f)},
        {"SIGN_TEXT",new ColorSpec(1.0000f,1.0000f,1.0000f)},
        {"LANTERN",new ColorSpec(0.7100f,0.4100f,0.2200f)},
    };

    [MenuItem("Tools/空座/长明戏楼美术/① 生成独立外景预览")]
    public static void GenerateExterior() { CreatePreview(false); }

    [MenuItem("Tools/空座/长明戏楼美术/② 生成独立室内预览")]
    public static void GenerateInterior() { CreatePreview(true); }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\','/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }

    private static Material MakeMaterial(string key)
    {
        EnsureFolder(GEN); EnsureFolder(MATS);
        string path = MATS + "/M_" + key + ".mat";
        Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader=Shader.Find("Universal Render Pipeline/Lit");
        if (shader==null) shader=Shader.Find("Standard");
        if (shader==null) throw new InvalidOperationException("Missing URP/Standard Shader");
        material = new Material(shader);
        var spec = Colors[key];
        var c = new Color(spec.R,spec.G,spec.B,1f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",c);
        if (material.HasProperty("_Color")) material.SetColor("_Color",c);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", key=="ROOF_TILE" ? .27f : .18f);
        if (key == "SIGN_TEXT")
        {
            string texpath = ROOT + "/Textures/T_ChangMing_Sign.png";
            Texture2D tex=AssetDatabase.LoadAssetAtPath<Texture2D>(texpath);
            if (tex!=null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap",tex);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex",tex);
            }
        }
        AssetDatabase.CreateAsset(material,path);
        return material;
    }

    private static GameObject AddMeshGroup(string group, Transform parent)
    {
        var go = new GameObject(group);
        go.transform.SetParent(parent,false);
        string projectRoot=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        string directory=Path.Combine(projectRoot,MODEL,group);
        if (!Directory.Exists(directory)) { Debug.LogError("Missing model folder: "+directory); return go; }
        var paths=Directory.GetFiles(directory,"*.obj",SearchOption.TopDirectoryOnly);
        Array.Sort(paths,StringComparer.Ordinal);
        int imported=0;
        foreach(var assetFile in paths)
        {
            string path=assetFile.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Replace((char)92,'/');
            if (!path.EndsWith(".obj",StringComparison.OrdinalIgnoreCase)) continue;
            string name=Path.GetFileNameWithoutExtension(path);
            var divider=name.LastIndexOf("__",StringComparison.Ordinal);
            if (divider<0) continue;
            string materialKey=name.Substring(divider+2);
            if (!Colors.ContainsKey(materialKey)) continue;
            GameObject source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source==null) continue;
            GameObject instance=(GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name=name;
            instance.transform.SetParent(go.transform,false);
            instance.transform.localPosition=Vector3.zero;
            instance.transform.localEulerAngles=Vector3.zero;
            instance.transform.localScale=Vector3.one;
            var m=MakeMaterial(materialKey);
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++) materials[i]=m;
                renderer.sharedMaterials=materials;
            }
            imported++;
        }
        if(imported==0) Debug.LogWarning("No OBJ imported for group: "+group);
        return go;
    }

    private static void AddSeats(Transform parent)
    {
        GameObject all=new GameObject("04_Seats__65_seats__R01_S07_key");
        all.transform.SetParent(parent,false);
        GameObject prefabBase=AddMeshGroup("Chair_Wood_A",all.transform);
        GameObject specialBase=AddMeshGroup("Chair_Key07",all.transform);
        // Template instances inside generated preview are disabled; real seats are separately selectable.
        prefabBase.SetActive(false);
        specialBase.SetActive(false);
        float[] rowZ={6.8f,4.55f,2.30f,0.05f,-2.20f};
        for(int r=0;r<5;r++)
        {
            GameObject row=new GameObject("Row_"+(r+1).ToString("00"));
            row.transform.SetParent(all.transform,false);
            for(int seat=1;seat<=13;seat++)
            {
                bool key=(r==0 && seat==7);
                GameObject refobj= key ? specialBase : prefabBase;
                GameObject item=UnityEngine.Object.Instantiate(refobj,row.transform);
                item.name="R"+(r+1).ToString("00")+"_Seat"+seat.ToString("00")+(key ? "_KEY_RESERVED" : "");
                item.SetActive(true);
                item.transform.localPosition=new Vector3((seat-7)*1.2f,0,rowZ[r]);
                item.transform.localRotation=Quaternion.identity;
                item.transform.localScale=Vector3.one;
            }
        }
        // Do not keep duplicate imported chair templates inside final scene.
        UnityEngine.Object.DestroyImmediate(prefabBase);
        UnityEngine.Object.DestroyImmediate(specialBase);
    }

    // Three independent art prefabs for the programmer to review individually.
    // We only SAVE prefabs into this package's Generated folder; never alter gameplay scenes.
    private static void SaveSectionPrefab(string assetPath, params GameObject[] sections)
    {
        GameObject staging = new GameObject(Path.GetFileNameWithoutExtension(assetPath));
        try
        {
            foreach (GameObject section in sections)
            {
                if (section == null) continue;
                GameObject part = UnityEngine.Object.Instantiate(section, staging.transform);
                part.name = section.name;
                part.transform.localPosition = section.transform.localPosition;
                part.transform.localRotation = section.transform.localRotation;
                part.transform.localScale = section.transform.localScale;
                // In the interior preview the exterior roof is hidden only for viewing.
                // The prefab of the complete exterior must ALWAYS preserve its roof.
                part.SetActive(true);
            }
            PrefabUtility.SaveAsPrefabAsset(staging, assetPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(staging);
        }
    }

    private static void AddPreviewLighting(bool interior)
    {
        var lightObj=new GameObject("Preview_Directional_Light_Not_Gameplay");
        var light=lightObj.AddComponent<Light>();
        light.type=LightType.Directional;
        light.intensity=interior ? .8f : 1.25f;
        light.color=new Color(1f,.91f,.80f);
        lightObj.transform.rotation=Quaternion.Euler(35,-24,0);
        if(interior)
        {
            for(int i=0;i<3;i++)
            {
                GameObject g=new GameObject("Preview_Hall_PointLight_"+i);
                Light l=g.AddComponent<Light>(); l.type=LightType.Point;
                l.range=16f; l.intensity=7f;
                l.color=new Color(1f,.69f,.45f);
                g.transform.position=new Vector3(0,4,-6+i*7);
            }
        }
    }

    private static void AddCamera(bool interior)
    {
        GameObject obj=new GameObject(interior ? "Preview_Camera_Interior" : "Preview_Camera_Exterior");
        Camera cam=obj.AddComponent<Camera>();
        cam.nearClipPlane=.08f; cam.farClipPlane=160f;
        obj.tag="MainCamera";
        if(interior) obj.transform.position=new Vector3(0,2.12f,-8.1f);
        else obj.transform.position=new Vector3(22,13,-36);
        Vector3 target=interior ? new Vector3(0,2.8f,11.2f) : new Vector3(0,4.3f,-3.5f);
        obj.transform.LookAt(target);
    }

    private static void CreatePreview(bool interior)
    {
        // Safety: no automatic scene or art generation inside a GitHub team working copy.
        string workspace=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
        if (!workspace.Contains("/UnitySandbox/") && !workspace.EndsWith("/KongZuo_ZhaiEchoSandbox",StringComparison.OrdinalIgnoreCase) && !File.Exists(Path.Combine(workspace,"Assets","KongZuo","Editor","KZMergedArtSetup.cs")))
        {
            EditorUtility.DisplayDialog("请先打开沙盒工程", "当前 Unity 工程路径不在 UnitySandbox 下。为了保护《空座》团队主工程，本工具不会运行。请在 Unity Hub 打开 KongZuo_ZhaiEchoSandbox，再试一次。", "我知道了");
            return;
        }
        string projectRoot=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        if(!Directory.Exists(Path.Combine(projectRoot,MODEL,"Exterior_Roof")) || !Directory.Exists(Path.Combine(projectRoot,MODEL,"Opera_Stage")))
        {
            EditorUtility.DisplayDialog("模型未安装完整", "找不到 Models/Exterior_Roof 或 Models/Opera_Stage。请检查解压目录层级。", "我知道了");
            return;
        }
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder(GEN);
        Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        GameObject root=new GameObject("KZ_OperaHouse_P2__ART_ONLY__SAFE_TO_DELETE");
        var shell=AddMeshGroup("Exterior_Structure",root.transform);
        shell.name="01_Exterior_Facade_Entrance";
        var roof=AddMeshGroup("Exterior_Roof",root.transform);
        roof.name="02_Exterior_Roof__Hide_for_Interior";
        var sign=AddMeshGroup("Exterior_Signboard",root.transform);
        sign.name="03_Exterior_Signboard";
        var hall=AddMeshGroup("Audience_Hall",root.transform);
        hall.name="04_Interior_Architecture";
        var stage=AddMeshGroup("Opera_Stage",root.transform);
        stage.name="05_Opera_Stage";
        AddSeats(root.transform);
        var yard=AddMeshGroup("Exterior_EntranceScenery",root.transform);
        yard.name="06_Entrance_Scenery";
        // Safe modular deliverables: exterior, hall with seats, and stage can be moved separately.
        var seats=root.transform.Find("04_Seats__65_seats__R01_S07_key");
        SaveSectionPrefab(GEN + "/PF_KZ_P2_01_Exterior.prefab",shell,roof,sign,yard);
        SaveSectionPrefab(GEN + "/PF_KZ_P2_02_AudienceHall.prefab",hall,seats==null ? null : seats.gameObject);
        SaveSectionPrefab(GEN + "/PF_KZ_P2_03_OperaStage.prefab",stage);
        roof.SetActive(!interior);
        // Exterior panorama includes existing interior assets but roof visually encloses them.
        AddPreviewLighting(interior);
        AddCamera(interior);
        string path=GEN + (interior ? "/KZ_P2_Interior_Preview.unity" : "/KZ_P2_Exterior_Preview.unity");
        if(!EditorSceneManager.SaveScene(scene,path)) throw new Exception("Scene save failed: "+path);
        string prefab=GEN+"/PF_KZ_P2_OperaHouse_All.prefab";
        if (interior) PrefabUtility.SaveAsPrefabAsset(root,prefab);
        Selection.activeGameObject=root;
        EditorGUIUtility.PingObject(root);
        SceneView sceneView=SceneView.lastActiveSceneView;
        if(sceneView!=null)
        {
            sceneView.pivot=interior ? new Vector3(0,2,8) : new Vector3(0,3,-6);
            sceneView.rotation=Quaternion.Euler(interior ? 16:24,interior ? 0:325,0);
            sceneView.size=interior ? 18 : 41;
            sceneView.Repaint();
        }
        Debug.Log("KZ OperaHouse P2 preview generated: " + path + "; team original scenes remain unchanged.");
    }
}
#endif
