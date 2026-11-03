#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace KongZuo.V03.EditorTools
{
    /// <summary>
    /// Creates separate, non-overwriting playable greyboxes for the V0.3 story.
    /// Does NOT overwrite the team's existing 00/01/02 scenes, KZStoryDirector, or LMLWeek4.
    /// </summary>
    public static class KZV03SceneBuilder
    {
        private const string Version = "6000.3.26f1";
        private const string MaterialsFolder = "Assets/KongZuo/V03/Materials";
        private static Material wood, floor, wall, stage, paper, red, brass, violet, dark, marker;
        private static KZV03Director director;
        private static GameObject specialSeat;

        [MenuItem("空座/V0.3/01 生成独立测试场景（保留原场景）")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (Application.unityVersion != Version)
            {
                if (!EditorUtility.DisplayDialog("Unity版本提醒", "团队版本为 " + Version +
                    "，当前版本 " + Application.unityVersion + "。\n推荐切回团队版本，以免资产序列化格式变化。", "仍在备份工程中继续", "取消")) return;
            }

            string[] paths = { P(KZV03Scenes.Prologue), P(KZV03Scenes.Seat), P(KZV03Scenes.Person) };
            foreach (string path in paths)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null || File.Exists(path))
                {
                    EditorUtility.DisplayDialog("保护已有场景", "检测到已有V0.3场景：\n" + path +
                        "\n\n为了防止覆盖，生成器本次不执行。可直接打开它。需要重建时，请先在项目外备份并手动移走三个V0.3场景。", "明白");
                    return;
                }

            EnsureFolder(KZV03Scenes.Folder);
            EnsureFolder(MaterialsFolder);
            CreateMaterials();
            BuildAndSave(KZV03Scenes.Prologue, KZV03Director.Part.Prologue);
            BuildAndSave(KZV03Scenes.Seat, KZV03Director.Part.Seat);
            BuildAndSave(KZV03Scenes.Person, KZV03Director.Part.Person);

            // Only append. Do not replace the team's existing build-scene ordering.
            List<EditorBuildSettingsScene> build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (string path in paths)
            {
                bool found = false;
                foreach (EditorBuildSettingsScene entry in build) if (entry.path == path) { found=true; break; }
                if (!found) build.Add(new EditorBuildSettingsScene(path,true));
            }
            EditorBuildSettings.scenes = build.ToArray();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(paths[0]);
            EditorUtility.DisplayDialog("《空座》V0.3场景已生成",
                "新场景均位于 Assets/KongZuo/V03/Scenes，旧场景没有被覆盖。\n\n" +
                "当前打开序章。点击 Play，按照 HUD 的目标调查。\n" +
                "WASD / 鼠标 / E / Tab；按 Esc 释放或锁定鼠标。\n\n" +
                "重要：这是剧情流程灰盒，尚不等于通过美术、配音和36分30秒时长验收。", "开始检查");
        }

        [MenuItem("空座/V0.3/02 检查场景与兼容性")]
        public static void Validate()
        {
            string text = "编辑器版本：" + Application.unityVersion + (Application.unityVersion==Version?" [匹配]":" [建议一致]") + "\n";
            foreach (string n in new[] { KZV03Scenes.Prologue, KZV03Scenes.Seat, KZV03Scenes.Person })
            {
                string path = P(n);
                bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
                bool inBuild = false;
                foreach (EditorBuildSettingsScene e in EditorBuildSettings.scenes) if (e.path == path && e.enabled) inBuild = true;
                text += (exists?"[OK]":"[缺失]") + " " + n + "；构建列表=" + (inBuild?"已启用":"未启用") + "\n";
            }
            string lml = "Assets/KongZuo/LMLWeek4/Runtime/KZW4AudioManager.cs";
            text += "\n第四周源文件：" + (AssetDatabase.LoadAssetAtPath<MonoScript>(lml)!=null?"已发现（未修改）":"未发现；会使用静音灰盒占位") + "\n";
            text += "V0.3版只在新目录生成文件；不会自动修改原有第一章和第四周脚本。";
            Debug.Log("[空座V0.3] " + text);
            EditorUtility.DisplayDialog("V0.3项目检查", text, "关闭");
        }

        private static string P(string scene) { return KZV03Scenes.Folder + "/" + scene + ".unity"; }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\','/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Material Mat(string name, Color tint)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("缺少URP/Lit或Standard材质Shader");
            m = new Material(shader) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor",tint);
            else m.color = tint;
            AssetDatabase.CreateAsset(m,path);
            return m;
        }
        private static void CreateMaterials()
        {
            floor = Mat("V03_Stone", new Color(.43f,.42f,.40f));
            wall = Mat("V03_Walls", new Color(.65f,.56f,.45f));
            wood = Mat("V03_Wood", new Color(.31f,.23f,.19f));
            stage = Mat("V03_Stage", new Color(.27f,.18f,.17f));
            paper = Mat("V03_Parchment", new Color(.91f,.77f,.49f));
            red = Mat("V03_RedCurtain", new Color(.55f,.16f,.22f));
            brass = Mat("V03_Copper",new Color(.84f,.62f,.31f));
            violet = Mat("V03_Indigo",new Color(.29f,.32f,.56f));
            dark = Mat("V03_Dark",new Color(.12f,.16f,.20f));
            marker = Mat("V03_Interactable",new Color(.44f,.72f,.69f));
        }

        private static GameObject Cube(string name, Vector3 pos, Vector3 scale, Material m, Transform parent=null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && m != null) r.sharedMaterial = m;
            return go;
        }

        private static GameObject Prop(string name, string id, Vector3 pos, Vector3 size, Material material)
        {
            GameObject o = Cube(name,pos,size,material);
            KZV03Item item = o.AddComponent<KZV03Item>();
            item.id = id;
            item.displayName = name;
            return o;
        }

        private static void BuildAndSave(string name, KZV03Director.Part part)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            BuildStudio(part);
            BuildSystems(part);
            if (part == KZV03Director.Part.Prologue) BuildPrologueItems();
            else if (part == KZV03Director.Part.Seat) BuildSeatItems();
            else BuildPersonItems();
            if (!EditorSceneManager.SaveScene(scene,P(name))) throw new IOException("保存失败："+name);
        }

        private static void BuildStudio(KZV03Director.Part part)
        {
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.39f,.41f,.44f);
            RenderSettings.fog=true;
            RenderSettings.fogColor=new Color(.46f,.47f,.46f);
            RenderSettings.fogDensity=.015f;

            Cube("1F地面 · 南正门 / 前厅 / 观众厅 / 戏台 / 后台",new Vector3(0f,-.24f,0f),new Vector3(27f,.48f,60f),floor);
            Cube("戏楼西墙",new Vector3(-12.5f,1.55f,2f),new Vector3(.35f,3.1f,53f),wall);
            Cube("戏楼东墙",new Vector3(12.5f,1.55f,2f),new Vector3(.35f,3.1f,53f),wall);
            Cube("正门左墙",new Vector3(-6.8f,1.55f,-22.2f),new Vector3(11.4f,3.1f,.4f),wall);
            Cube("正门右墙",new Vector3(6.8f,1.55f,-22.2f),new Vector3(11.4f,3.1f,.4f),wall);
            Cube("长明戏楼匾额（灰盒）",new Vector3(0,3f,-22.25f),new Vector3(3f,.6f,.3f),wood);
            Cube("前厅-西办公室展示台",new Vector3(-8f,.55f,-16f),new Vector3(3f,1.1f,2.2f),wood);
            Cube("前厅-东售票窗口台",new Vector3(8f,.55f,-16f),new Vector3(3f,1.1f,2.2f),wood);
            Cube("戏台主台",new Vector3(0f,.16f,10f),new Vector3(18f,.32f,6f),stage);
            Cube("戏台侧幕·左",new Vector3(-7.9f,1.9f,10.9f),new Vector3(1.3f,3.8f,.5f),red);
            Cube("戏台侧幕·右",new Vector3(7.9f,1.9f,10.9f),new Vector3(1.3f,3.8f,.5f),red);
            Cube("戏台对景立柱·左",new Vector3(-6f,2f,7.1f),new Vector3(.55f,4f,.55f),wood);
            Cube("戏台对景立柱·右",new Vector3(6f,2f,7.1f),new Vector3(.55f,4f,.55f),wood);
            Cube("右侧小梯（对景标记）",new Vector3(8.8f,.35f,5.9f),new Vector3(2f,.7f,1.5f),wood);

            specialSeat=null;
            for (int row=0; row<3; row++)
                for (int chair=1; chair<=13; chair++)
                {
                    float x=(chair-7)*.97f;
                    float z=3.45f-row*2.45f;
                    bool seven=row==0 && chair==7;
                    GameObject seatRoot=new GameObject("观众厅_第"+(row+1)+"排_"+chair+"座"+(seven?"_留座":""));
                    Cube("座面",new Vector3(x,.58f,z),new Vector3(.82f,.15f,.70f),seven?paper:wood,seatRoot.transform);
                    Cube("椅背",new Vector3(x,1.02f,z+.32f),new Vector3(.86f,.83f,.12f),wood,seatRoot.transform);
                    Cube("椅脚左",new Vector3(x-.26f,.29f,z),new Vector3(.12f,.6f,.12f),dark,seatRoot.transform);
                    Cube("椅脚右",new Vector3(x+.26f,.29f,z),new Vector3(.12f,.6f,.12f),dark,seatRoot.transform);
                    if (seven) specialSeat=seatRoot;
                }
            // Side aisles at x ≈ ±9, no central aisle: as prescribed by V0.3.
            Cube("侧道边缘标记·左",new Vector3(-8.2f,.025f,-1f),new Vector3(.14f,.05f,10f),dark);
            Cube("侧道边缘标记·右",new Vector3(8.2f,.025f,-1f),new Vector3(.14f,.05f,10f),dark);

            if (part == KZV03Director.Part.Person) BuildBackstageRooms();
            else Cube("前两幕：后台尚未开放的阻隔（仅供测试）",new Vector3(0f,1.55f,14.6f),new Vector3(25f,3.1f,.45f),wall);
        }

        private static void BuildBackstageRooms()
        {
            Cube("后台北墙",new Vector3(0,1.7f,28f),new Vector3(25f,3.4f,.35f),wall);
            for(int i=0;i<=4;i++)
            {
                float x=-10f+i*5f;
                Cube("房间隔墙"+i,new Vector3(x,1.55f,24f),new Vector3(.28f,3.1f,8f),wall);
            }
            // Makeup room, costume room: wide gaps fitted with workroom doors.
            foreach(float minX in new [] { -10f, -5f })
            {
                Cube("敞开的房门左墙",new Vector3(minX+.65f,1.5f,20f),new Vector3(1.3f,3f,.25f),wall);
                Cube("敞开的房门右墙",new Vector3(minX+4.35f,1.5f,20f),new Vector3(1.3f,3f,.25f),wall);
            }
            Cube("档案室封门",new Vector3(2.5f,1.5f,20f),new Vector3(4.7f,3f,.25f),wall);
            Cube("配电室封门",new Vector3(7.5f,1.5f,20f),new Vector3(4.7f,3f,.25f),wall);
            Cube("戏班化妆台",new Vector3(-7.5f,.55f,25f),new Vector3(3.7f,1.1f,1.5f),wood);
            Cube("服装间戏箱",new Vector3(-2.7f,.5f,24.7f),new Vector3(2.5f,1f,1.6f),wood);
            Cube("后台西侧 B1 封闭楼梯",new Vector3(-11.0f,1.35f,17.5f),new Vector3(2.3f,2.7f,1.4f),dark);
        }

        private static void BuildSystems(KZV03Director.Part part)
        {
            GameObject root=new GameObject("KZ_V03_Director");
            director=root.AddComponent<KZV03Director>();
            director.part=part;
            root.AddComponent<KZV03Hud>();
            AudioSource a=root.AddComponent<AudioSource>();
            a.playOnAwake=false;a.spatialBlend=0f;a.volume=.30f;
            AudioSource fx=root.AddComponent<AudioSource>();
            fx.playOnAwake=false;fx.spatialBlend=0f;fx.volume=.65f;
            director.ambienceSource=a;
            director.effectsSource=fx;
            string baseAudio="Assets/KongZuo/LMLWeek4/Audio/Placeholder/";
            director.ambience2004=AssetDatabase.LoadAssetAtPath<AudioClip>(baseAudio+"ambience_2004.wav");
            director.ambience1992=AssetDatabase.LoadAssetAtPath<AudioClip>(baseAudio+"ambience_1992.wav");
            director.gong=AssetDatabase.LoadAssetAtPath<AudioClip>(baseAudio+"gong.wav");
            director.paper=AssetDatabase.LoadAssetAtPath<AudioClip>(baseAudio+"paper.wav");
            director.telephone=AssetDatabase.LoadAssetAtPath<AudioClip>(baseAudio+"phone.wav");
            director.keySfx=AssetDatabase.LoadAssetAtPath<AudioClip>(baseAudio+"key.wav");
            // Optional audio lookup only; never edits the LMLWeek4 files or its audio manager.

            GameObject player=new GameObject("林砚_第一人称角色");
            CharacterController cc=player.AddComponent<CharacterController>();
            cc.height=1.9f;cc.center=new Vector3(0f,.95f,0f);cc.radius=.32f;cc.stepOffset=.36f;
            KZV03Player pc=player.AddComponent<KZV03Player>();
            Camera camera=new GameObject("Eyes_Camera").AddComponent<Camera>();
            camera.transform.SetParent(player.transform);
            camera.transform.localPosition=new Vector3(0f,1.63f,0f);
            camera.fieldOfView=72f;
            camera.gameObject.AddComponent<AudioListener>();
            pc.viewCamera=camera;
            director.player=pc;
            player.transform.position=part==KZV03Director.Part.Prologue?new Vector3(0f,.1f,-25.2f):
                part==KZV03Director.Part.Seat?new Vector3(-8.3f,.1f,3.2f):new Vector3(-8f,.42f,11.5f);
            if (part==KZV03Director.Part.Person) player.transform.rotation=Quaternion.Euler(0f,0f,0f);
            else if (part==KZV03Director.Part.Seat) player.transform.rotation=Quaternion.Euler(0f,70f,0f);

            GameObject sun=new GameObject("长明戏楼 · 低光环境光");
            Light l=sun.AddComponent<Light>(); l.type=LightType.Directional;l.intensity=.35f;
            sun.transform.rotation=Quaternion.Euler(45f,-32f,0f);
            GameObject lamp=new GameObject("台口残灯");
            Light spot=lamp.AddComponent<Light>(); spot.type=LightType.Point;spot.range=18f;spot.intensity=7.5f;
            lamp.transform.position=new Vector3(0f,4.5f,8.8f);
            director.stageLamp=spot;

            GameObject view=new GameObject("残响观看点");
            view.transform.position=new Vector3(-8.3f,.1f,3.4f);
            view.transform.rotation=Quaternion.Euler(0f,90f,0f);
            director.seatView=view.transform;
        }

        private static void BuildPrologueItems()
        {
            GameObject door=Prop("戏楼正门（需要推开）","p_door",new Vector3(0f,1.45f,-22.2f),new Vector3(2.4f,2.9f,.30f),wood);
            director.frontDoor=door;
            Prop("办公室座机 · 何文斌电话","p_phone",new Vector3(-8f,1.28f,-16.3f),new Vector3(.78f,.5f,.6f),dark);
            Prop("1992年登记表 · 售票口","p_register",new Vector3(8f,1.21f,-15.9f),new Vector3(1.3f,.16f,.76f),paper);
            Prop("旧火灾剪报（可选）","p_poster",new Vector3(-5f,1.3f,-19.5f),new Vector3(1.2f,1.15f,.18f),paper);
            Prop("火灾档案移交目录（可选）","p_archive",new Vector3(-9.4f,1.15f,-16.5f),new Vector3(.60f,.09f,.55f),paper);
            Prop("留座戏民俗宣传册（可选）","p_folklore",new Vector3(1.9f,1.05f,-17.8f),new Vector3(.80f,.13f,.6f),paper);
            if(specialSeat!=null)
            {
                KZV03Item seat=specialSeat.AddComponent<KZV03Item>();seat.id="p_seat";seat.displayName="第一排七座 · 异常干净的座位";
            }
            director.anomalyFigure=CreateSilhouette("七号座上的静止人影",new Vector3(0f,1.1f,3.25f),violet);
            director.anomalyFigure.SetActive(false);
            director.programProp=Prop("突然出现的节目单","p_program",new Vector3(0f,1.2f,2.89f),new Vector3(.58f,.08f,.52f),paper);
            director.programProp.SetActive(false);
        }

        private static void BuildSeatItems()
        {
            if(specialSeat!=null)
            {
                KZV03Item seat=specialSeat.AddComponent<KZV03Item>();seat.id="c1_seat";seat.displayName="重新调查一排七座 · 残响";
            }
            Prop("节目单背面数字17","c1_program",new Vector3(.9f,1.05f,3.0f),new Vector3(.55f,.09f,.48f),paper);
            Prop("售票口旧照片","c1_photo",new Vector3(8f,1.19f,-15.9f),new Vector3(.95f,.11f,.65f),paper);
            Prop("旧照片对景观察点 · 多次按E","c1_align",new Vector3(-2.6f,1f,-9.4f),new Vector3(.90f,1.9f,.68f),marker);
            Prop("遗物陈设点交单","c1_handover",new Vector3(-3.1f,1.12f,-17.5f),new Vector3(.80f,.12f,.6f),paper);
            Prop("周明生与铜钥匙的旧合照（可选）","c1_zhou_photo",new Vector3(1.9f,1.30f,-17.5f),new Vector3(1.1f,1.0f,.22f),paper);
            director.echoFigure=CreateSilhouette("1992年蓝边戏衣女人 · 残响演员",new Vector3(-7f,1.25f,7.2f),violet);
            director.echoFigure.SetActive(false);
            director.echoKey=Cube("1992年七号座铜钥匙",new Vector3(0f,.78f,3.45f),new Vector3(.32f,.06f,.2f),brass);
            director.echoKey.SetActive(false);
        }

        private static void BuildPersonItems()
        {
            Prop("侧台2004年维修钥匙","c2_maintenance",new Vector3(-8.4f,1.03f,14.6f),new Vector3(.50f,.13f,.30f),brass);
            GameObject makeupDoor=Prop("化妆间工作门（需要维修钥匙）","c2_workroom_door",new Vector3(-7.5f,1.15f,20f),new Vector3(2.1f,2.3f,.28f),wood);
            GameObject costumeDoor=Prop("服装间工作门（需要维修钥匙）","c2_workroom_door",new Vector3(-2.5f,1.15f,20f),new Vector3(2.1f,2.3f,.28f),wood);
            director.workroomDoors=new [] { makeupDoor, costumeDoor };
            Prop("化妆间门边的1992年排班表（可选）","c2_roster",new Vector3(-8.95f,1.40f,21.5f),new Vector3(.85f,.90f,.22f),paper);
            Prop("沈芸旧剧照 · 靛蓝滚边","c2_portrait",new Vector3(-8.7f,1.24f,24.6f),new Vector3(.76f,1.05f,.20f),paper);
            Prop("沈芸与顾兰的小桥生活照","c2_life_photo",new Vector3(-6.5f,1.27f,24.6f),new Vector3(.70f,.90f,.20f),paper);
            Prop("眉笔盒与生活杂物（可选）","c2_everyday",new Vector3(-9.1f,1.18f,26.5f),new Vector3(.50f,.16f,.50f),marker);
            Prop("抽屉：旧节目册别扔","c2_booklet_hint",new Vector3(-7.7f,1.25f,25.0f),new Vector3(.72f,.14f,.54f),paper);
            Prop("布袋里的节目册 · 购票凭证","c2_travel",new Vector3(-6.1f,1.28f,25.0f),new Vector3(.62f,.18f,.43f),brass);
            Prop("沈芸未寄出的信","c2_letter",new Vector3(-8.5f,1.26f,26f),new Vector3(.68f,.11f,.44f),paper);
            Prop("桌角旧红线 · 争执残响","c2_red_thread",new Vector3(-6.3f,1.28f,26.0f),new Vector3(.20f,.12f,.62f),red);
            Prop("许承山工作服内袋纸条","c2_xu_note",new Vector3(-4.1f,1.48f,24.4f),new Vector3(.70f,.70f,.20f),paper);
            Prop("旧戏箱：周明生钥匙登记","c2_chest_label",new Vector3(-2.7f,1.13f,25.35f),new Vector3(.9f,.32f,.20f),brass);
            Prop("顾兰的戏衣与空纸套（可选）","c2_gu_sleeve",new Vector3(-1.2f,1.6f,23.1f),new Vector3(.66f,1.20f,.25f),violet);
            Prop("车票怎么不见了？（可选）","c2_missing_ticket",new Vector3(-6.7f,1.35f,26.5f),new Vector3(.48f,.08f,.45f),paper);
            Prop("后台通廊：旧工作路线示意","c2_east_route",new Vector3(6.9f,1.4f,17.5f),new Vector3(1.15f,1.0f,.25f),paper);
            Prop("西侧通B1楼梯封挡","c2_locked_room",new Vector3(-10.9f,1.4f,16.8f),new Vector3(1.75f,.80f,.22f),dark);
            Prop("东侧工作门 · 内侧锁住","c2_east_door",new Vector3(11.4f,1.4f,16f),new Vector3(.38f,2.8f,1.75f),wood);
            GameObject record=Prop("清点相机：拍东门锁座与划痕","c2_record",new Vector3(9.3f,1.1f,17.6f),new Vector3(.6f,.35f,.28f),marker);
            KZV03Item item=record.GetComponent<KZV03Item>();item.showAfterEastEcho=true;
            director.recordPoint=record;
            record.SetActive(false);
            director.argumentFigures=new GameObject("争执残响演员组");
            CreateSilhouette("沈芸 · 蓝边戏衣",new Vector3(-7f,1.3f,23.6f),violet).transform.SetParent(director.argumentFigures.transform);
            CreateSilhouette("许承山 · 半句争执",new Vector3(-6.2f,1.3f,21.1f),dark).transform.SetParent(director.argumentFigures.transform);
            director.argumentFigures.SetActive(false);
            director.doorFigure=CreateSilhouette("东侧门短残响 · 女子",new Vector3(10.2f,1.3f,16f),violet);
            director.doorFigure.SetActive(false);
        }

        private static GameObject CreateSilhouette(string name,Vector3 at, Material costume)
        {
            GameObject root=new GameObject(name);
            root.transform.position=at;
            Cube("旧戏衣轮廓",at+new Vector3(0,.20f,0),new Vector3(.58f,1.28f,.35f),costume,root.transform);
            Cube("人影头部",at+new Vector3(0,1.04f,0),new Vector3(.32f,.39f,.32f),dark,root.transform);
            Cube("靛蓝滚边左袖",at+new Vector3(-.38f,.55f,0),new Vector3(.17f,.74f,.24f),costume,root.transform);
            Cube("靛蓝滚边右袖",at+new Vector3(.38f,.55f,0),new Vector3(.17f,.74f,.24f),costume,root.transform);
            foreach(Collider c in root.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            return root;
        }
    }
}
#endif
