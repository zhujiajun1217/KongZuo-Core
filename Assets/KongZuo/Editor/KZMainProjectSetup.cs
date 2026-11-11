#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace KongZuo.EditorTools
{
    // 一次性生成测试场景。不会覆盖已有.unity场景，不会替换URP项目设置。
    public static class KZMainProjectSetup
    {
        private const string RequiredVersion = "6000.3.26f1";
        private const string SceneFolder = "Assets/KongZuo/Scenes";
        private const string MatFolder = "Assets/KongZuo/Art/Prototype";

        private static Material floorMat, wallMat, woodMat, redMat, paperMat, darkMat, goldMat;
        private static KZFirstPersonPlayer player;
        private static Light stageLamp;
        private static GameObject frontDoor;
        private static GameObject seat07;

        [MenuItem("空座/01 生成主线P0灰盒场景")]
        public static void CreateGreyboxScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(KZSceneNames.BootstrapPath) != null ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(KZSceneNames.ProloguePath) != null ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(KZSceneNames.ChapterOnePath) != null)
            {
                EditorUtility.DisplayDialog("已保护场景",
                    "场景文件已经存在，生成器拒绝覆盖。请继续使用原有场景，" +
                    "或备份后由主程序负责人手动移走旧场景。", "好的");
                return;
            }

            if (Application.unityVersion != RequiredVersion)
            {
                EditorUtility.DisplayDialog("Unity 版本不一致",
                    "团队严格要求 " + RequiredVersion + "，当前为 " +
                    Application.unityVersion + "。请先通过Unity Hub切换到正确版本。", "返回");
                return;
            }

            if (GraphicsSettings.currentRenderPipeline == null ||
                !GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("UniversalRenderPipelineAsset"))
            {
                EditorUtility.DisplayDialog("需要URP",
                    "当前没有启用Scriptable Render Pipeline。" +
                    "请用 Unity Hub 新建 Universal 3D 项目，勿使用Built-in 3D模板。", "好的");
                return;
            }

            EnsureFolder(SceneFolder);
            EnsureFolder(MatFolder);
            CreateMaterials();
            SaveScene(KZSceneNames.BootstrapPath, BuildBootstrap);
            SaveScene(KZSceneNames.ProloguePath, BuildPrologue);
            SaveScene(KZSceneNames.ChapterOnePath, BuildChapter01);

            // 新建主工程使用这三个场景作为唯一启动链。
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(KZSceneNames.BootstrapPath, true),
                new EditorBuildSettingsScene(KZSceneNames.ProloguePath, true),
                new EditorBuildSettingsScene(KZSceneNames.ChapterOnePath, true)
            };
            PlayerSettings.productName = "空座";
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(KZSceneNames.BootstrapPath);
            EditorUtility.DisplayDialog("空座：P0灰盒创建成功",
                "已生成00_Bootstrap、01_Prologue、02_Chapter01。\n" +
                "现在点击Play，即可从工作单开始测试序章与第一章。\n" +
                "WASD移动，E调查，F照片对位，Tab笔记。", "开始测试");
        }

        [MenuItem("空座/02 检查版本与场景")]
        public static void ValidateSetup()
        {
            bool version = Application.unityVersion == RequiredVersion;
            bool urp = GraphicsSettings.currentRenderPipeline != null &&
                GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("UniversalRenderPipelineAsset");
            bool scenes =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(KZSceneNames.BootstrapPath) != null &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(KZSceneNames.ProloguePath) != null &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(KZSceneNames.ChapterOnePath) != null;

            bool build = false;
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
                if (entry.path == KZSceneNames.BootstrapPath && entry.enabled) build = true;

            string report =
                (version ? "[OK]" : "[检查]") + " 编辑器: " + Application.unityVersion + "\n" +
                (urp ? "[OK]" : "[错误]") + " SRP/URP模板: " +
                    (GraphicsSettings.currentRenderPipeline == null ? "无" :
                        GraphicsSettings.currentRenderPipeline.GetType().Name) + "\n" +
                (scenes ? "[OK]" : "[错误]") + " 三个关卡文件存在\n" +
                (build ? "[OK]" : "[错误]") + " 启动场景已添加构建列表\n\n" +
                "请另行验证 Input Handling 与 Windows Build Support。";
            Debug.Log("[空座] 工程检查\n" + report);
            EditorUtility.DisplayDialog("工程检查", report, "关闭");
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static Material Material(string name, Color color)
        {
            string path = MatFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("找不到 URP/Lit Shader，请从Universal 3D创建项目。");
            Material result = new Material(shader);
            result.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(result, path);
            return result;
        }

        private static void CreateMaterials()
        {
            floorMat = Material("M_Floor", new Color(.40f, .36f, .32f));
            wallMat = Material("M_Wall", new Color(.67f, .60f, .51f));
            woodMat = Material("M_Wood", new Color(.35f, .23f, .17f));
            redMat = Material("M_Curtain", new Color(.54f, .13f, .17f));
            paperMat = Material("M_Paper", new Color(.88f, .76f, .52f));
            darkMat = Material("M_Shadow", new Color(.10f, .13f, .17f));
            goldMat = Material("M_Key", new Color(.83f, .60f, .20f));
        }

        private static void SaveScene(string path, Action builder)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            builder();
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new IOException("保存失败：" + path);
        }

        private static GameObject Block(string name, Vector3 position, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject ChildBlock(Transform parent, string name,
            Vector3 localPosition, Vector3 scale, Material mat)
        {
            GameObject go = Block(name, Vector3.zero, scale, mat);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        private static GameObject Prop(string name, KZActionKind action, string description,
            Vector3 position, Vector3 size, Material mat)
        {
            GameObject go = Block(name, position, size, mat);
            KZInteractable interact = go.AddComponent<KZInteractable>();
            interact.action = action;
            interact.displayName = name;
            interact.body = description;
            return go;
        }

        private static void Stand(Vector3 position)
        {
            Block("资料桌", position + Vector3.up * .46f, new Vector3(1.75f, .92f, 1.35f), woodMat);
        }

        private static void BuildBootstrap()
        {
            GameObject root = new GameObject("KZ_Bootstrap");
            root.AddComponent<KZGameSession>();
            root.AddComponent<KZBootstrapEntry>();
        }

        private static void BuildBase(bool prologue, string chapter)
        {
            player = null;
            frontDoor = null;
            seat07 = null;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.33f, .29f, .28f);
            Block("1F_地面", new Vector3(0f, -.15f, -.4f),
                new Vector3(20f, .3f, 40.2f), floorMat);

            // 面朝+Z的长轴：室外(-18) → 前厅(-14~-9) → 池座(-8~8) → 戏台(9~16)。
            Block("西立面", new Vector3(-9.8f, 2.6f, 2.2f),
                new Vector3(.40f, 5.2f, 35.4f), wallMat);
            Block("东立面", new Vector3(9.8f, 2.6f, 2.2f),
                new Vector3(.40f, 5.2f, 35.4f), wallMat);
            Block("北端背景", new Vector3(0f, 3f, 19.7f),
                new Vector3(20f, 6f, .4f), wallMat);
            Block("正门西侧墙", new Vector3(-5.65f, 2.5f, -15.1f),
                new Vector3(8.3f, 5f, .4f), wallMat);
            Block("正门东侧墙", new Vector3(5.65f, 2.5f, -15.1f),
                new Vector3(8.3f, 5f, .4f), wallMat);
            Block("门楣", new Vector3(0f, 3.57f, -15.1f),
                new Vector3(3f, 2.85f, .4f), darkMat);
            if (prologue)
                frontDoor = Prop("长明戏楼旧木门", KZActionKind.FrontDoor, "",
                    new Vector3(0f, 1.08f, -15.13f), new Vector3(2.65f, 2.16f, .22f), woodMat);
            Block("室外石阶限位", new Vector3(0f, .28f, -20.1f),
                new Vector3(20f, .56f, .25f), darkMat);

            // 西侧办公室和东侧售票口各有一个通向中央门厅的工作人员缺口。
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 3.2f;
                Block("侧间隔墙_南", new Vector3(x, 2.3f, -13.8f),
                    new Vector3(.20f, 4.6f, 2.5f), wallMat);
                Block("侧间隔墙_北", new Vector3(x, 2.3f, -9.68f),
                    new Vector3(.20f, 4.6f, 1.35f), wallMat);
                Block("侧房间后墙", new Vector3(side * 6.4f, 2.3f, -8.9f),
                    new Vector3(6.5f, 4.6f, .22f), wallMat);
            }

            // 灰盒戏台：台面、红幕、双柱、两侧可以走的低阶梯。
            Block("戏台台面", new Vector3(0f, .4f, 12f),
                new Vector3(15.6f, .8f, 7.1f), woodMat);
            Block("戏台门楣", new Vector3(0f, 4.2f, 15.8f),
                new Vector3(16f, .7f, .6f), redMat);
            Block("红幕", new Vector3(0f, 2.5f, 15.7f),
                new Vector3(13.8f, 3.5f, .22f), redMat);
            Block("舞台西柱", new Vector3(-7.5f, 2.5f, 14.0f),
                new Vector3(.5f, 4.3f, .5f), darkMat);
            Block("舞台东柱", new Vector3(7.5f, 2.5f, 14.0f),
                new Vector3(.5f, 4.3f, .5f), darkMat);
            KZInteractable backstage = Block("后台封闭门", new Vector3(-4.8f, 1.8f, 16.0f),
                new Vector3(2f, 2f, .3f), darkMat).AddComponent<KZInteractable>();
            backstage.action = KZActionKind.BackstageDoor;
            backstage.displayName = "封闭的后台门";
            backstage.body = "何文斌提醒过：后台火灾后木头已经朽坏，暂时不能进入。";
            for (int side = -1; side <= 1; side += 2)
            {
                Block("下台阶_高", new Vector3(side * 8f, .33f, 8.6f),
                    new Vector3(1.5f, .66f, .60f), woodMat);
                Block("下台阶_中", new Vector3(side * 8f, .23f, 8.0f),
                    new Vector3(1.5f, .46f, .60f), woodMat);
                Block("下台阶_低", new Vector3(side * 8f, .12f, 7.4f),
                    new Vector3(1.5f, .24f, .60f), woodMat);
            }

            // 座位模型暂为5排×13座；7号位永远位于第一排正中。
            for (int row = 0; row < 5; ++row)
            {
                for (int number = 1; number <= 13; ++number)
                {
                    float x = (number - 7) * 1.2f;
                    float z = 6.8f - row * 2.25f;
                    GameObject chair = new GameObject(
                        string.Format("第{0}排_{1:00}座", row + 1, number));
                    chair.transform.position = new Vector3(x, 0f, z);
                    Material chairMat = row == 0 && number == 7 ? paperMat : woodMat;
                    ChildBlock(chair.transform, "椅面", new Vector3(0f, .53f, 0f),
                        new Vector3(.94f, .14f, .86f), chairMat);
                    ChildBlock(chair.transform, "椅背", new Vector3(0f, 1.1f, -.36f),
                        new Vector3(.94f, 1.13f, .13f), chairMat);
                    ChildBlock(chair.transform, "左腿", new Vector3(-.35f, .25f, .26f),
                        new Vector3(.13f, .50f, .13f), darkMat);
                    ChildBlock(chair.transform, "右腿", new Vector3(.35f, .25f, .26f),
                        new Vector3(.13f, .50f, .13f), darkMat);
                    if (row == 0 && number == 7)
                    {
                        seat07 = chair;
                        KZInteractable action = chair.AddComponent<KZInteractable>();
                        action.action = KZActionKind.Seat07;
                        action.displayName = "第一排七座";
                        action.body =
                            "椅背刻着：一排七座。\n\n" +
                            "其他座面布满灰尘，唯独这里看上去干净一些。\n" +
                            "也许可以去售票处找旧记录核对。";
                    }
                }
            }

            GameObject sun = new GameObject("方向光_阴雨傍晚");
            Light directional = sun.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = .65f;
            sun.transform.rotation = Quaternion.Euler(47f, -35f, 0f);

            GameObject spotlight = new GameObject("戏台聚光灯");
            spotlight.transform.position = new Vector3(0f, 4.8f, 7.1f);
            spotlight.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
            stageLamp = spotlight.AddComponent<Light>();
            stageLamp.type = LightType.Spot;
            stageLamp.range = 28f;
            stageLamp.spotAngle = 96f;
            stageLamp.color = new Color(1f, .79f, .57f);
            stageLamp.intensity = 1.7f;

            BuildPlayer(prologue ? new Vector3(0f, 0f, -18.0f)
                : new Vector3(0f, 0f, -10.5f), chapter);
        }

        private static void BuildPlayer(Vector3 position, string chapter)
        {
            GameObject go = new GameObject("Player_LinYan");
            go.transform.position = position;
            CharacterController cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.center = new Vector3(0f, .9f, 0f);
            cc.radius = .31f;
            cc.stepOffset = .30f;

            GameObject eye = new GameObject("Eyes_Camera");
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            Camera view = eye.AddComponent<Camera>();
            view.fieldOfView = 72f;
            eye.AddComponent<AudioListener>();

            player = go.AddComponent<KZFirstPersonPlayer>();
            player.SetCameraPivot(eye.transform);
            go.AddComponent<KZInteractor>().SetCamera(view);
            new GameObject("KZ_HUD").AddComponent<KZHud>().chapterName = chapter;
        }

        private static KZStoryDirector Story(bool isPrologue)
        {
            KZStoryDirector director = new GameObject("StoryDirector_P0")
                .AddComponent<KZStoryDirector>();
            director.isPrologue = isPrologue;
            director.player = player;
            director.stageLamp = stageLamp;
            director.frontDoor = frontDoor;
            return director;
        }

        private static void Trigger(string name, KZZone zone, Vector3 center, Vector3 size)
        {
            GameObject obj = new GameObject(name);
            obj.transform.position = center;
            BoxCollider collider = obj.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = true;
            obj.AddComponent<KZStoryTrigger>().zone = zone;
        }

        private static GameObject ShadowFigure(string name, Transform parent, Vector3 position,
            Material costume)
        {
            GameObject figure = new GameObject(name);
            if (parent != null) figure.transform.SetParent(parent, false);
            figure.transform.position = position;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "身体_占位";
            body.transform.SetParent(figure.transform, false);
            body.transform.localPosition = new Vector3(0f, .92f, 0f);
            body.transform.localScale = new Vector3(.30f, .52f, .28f);
            body.GetComponent<Renderer>().sharedMaterial = costume;
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "头部_占位";
            head.transform.SetParent(figure.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            head.transform.localScale = new Vector3(.38f, .38f, .38f);
            head.GetComponent<Renderer>().sharedMaterial = darkMat;
            return figure;
        }

        private static void BuildPrologue()
        {
            BuildBase(true, "序章 · 拆楼前夜");
            KZStoryDirector director = Story(true);

            Stand(new Vector3(-4.0f, 0f, -17.0f));
            Prop("重看资产清点表", KZActionKind.Worksheet,
                "临槐县文化馆 · 长明戏楼拆除前资产清点表\n\n" +
                "戏服、照片、磁带、乐器、演出资料、历史档案。\n" +
                "明早施工，今晚务必完成。",
                new Vector3(-4.0f, 1.0f, -17.0f), new Vector3(.8f, .06f, .65f), paperMat);
            Stand(new Vector3(-6.3f, 0f, -11.2f));
            Prop("办公室电话", KZActionKind.Phone,
                "何文斌：“到了？”\n林砚：“刚到。”\n\n" +
                "何文斌：“值钱的早搬走了，剩下的照表登记。后台烧过，木头也朽了，别乱钻。”\n" +
                "林砚：“档案也登记？”\n何文斌：“登记数量就行。十二年前那些东西……没什么整理价值。”",
                new Vector3(-6.3f, 1.03f, -11.2f), new Vector3(.55f, .22f, .45f), darkMat);

            Stand(new Vector3(6.3f, 0f, -11.2f));
            Prop("1992年座位登记表", KZActionKind.Register,
                "长明戏楼 · 1992年座位登记表。\n\n" +
                "一排五座……（受潮模糊）\n一排六座……（受潮模糊）\n" +
                "一排七座——空\n一排八座……（受潮模糊）\n\n" +
                "这一行被人反复描过，唯独七号空着。",
                new Vector3(6.3f, 1.0f, -11.2f), new Vector3(.85f, .06f, .65f), paperMat);

            Prop("戏楼旧海报", KZActionKind.Poster,
                "褪色戏报还贴在前厅：长明戏楼，演出资料待整理。",
                new Vector3(2.0f, 1.6f, -14.8f), new Vector3(.8f, 1.2f, .09f), redMat);

            GameObject shadow = ShadowFigure("一排七座的人影_异常", null,
                new Vector3(0f, .02f, 6.8f), redMat);
            director.anomalyShadow = shadow;
            shadow.SetActive(false);

            GameObject program = Prop("新出现的旧节目单", KZActionKind.Program,
                "一九九二年七月十七日\n长明戏楼\n周明生留座戏\n\n" +
                "纸张像是刚刚被谁放在这张椅子上的。",
                new Vector3(.14f, .66f, 6.94f), new Vector3(.65f, .06f, .43f), paperMat);
            director.programProp = program;
            program.SetActive(false);

            Trigger("观众厅出入检测", KZZone.Audience,
                new Vector3(0f, 1.4f, 0f), new Vector3(17f, 3.4f, 16f));
            Trigger("靠近第一排的人影消失", KZZone.ApproachSeat,
                new Vector3(0f, 1.4f, 5.0f), new Vector3(17f, 3.4f, 5.7f));
        }

        private static void BuildChapter01()
        {
            BuildBase(false, "第一章 · 座");
            KZStoryDirector director = Story(false);

            Stand(new Vector3(-1.5f, 0f, -12f));
            Prop("留座戏旧宣传册", KZActionKind.Ritual,
                "若班中有人横死，头七前可以为亡者留座唱戏。\n" +
                "第一排正中不得坐人；戏散之前，不得取亡者遗物。\n\n" +
                "铅笔批注：活人占座，死人带走。",
                new Vector3(-1.5f, 1.0f, -12f), new Vector3(.78f, .06f, .65f), paperMat);

            Stand(new Vector3(1.7f, 0f, -12f));
            Prop("周明生旧合照", KZActionKind.OldPhoto,
                "一张1992年的旧合照，年轻场务被红笔圈了出来。\n\n" +
                "翻看背面：周明生，1972—1992。\n\n" +
                "灰盒阶段用文字代表照片正反面翻转；正式检视组件可由UI同学替换。",
                new Vector3(1.7f, 1.0f, -12f), new Vector3(.9f, .06f, .65f), paperMat);

            Stand(new Vector3(6.4f, 0f, -11.2f));
            Prop("残缺的旧票根", KZActionKind.Ticket,
                "票根受潮严重，只能辨认出1992年7月17日和“七号座未售”几个字。\n\n" +
                "其余姓名与座位信息已经模糊不清。",
                new Vector3(6.2f, 1.0f, -11.2f), new Vector3(.68f, .07f, .44f), redMat);
            Prop("一排座次示意图", KZActionKind.SeatRecord,
                "示意图上第一排中央的七号位置没有标记。纸边旧注：留座戏期间，中央座位不可坐人。",
                new Vector3(7.5f, 1.22f, -11.2f), new Vector3(.80f, .12f, .62f), paperMat);

            CreateEchoActors(director);
        }

        private static Transform Point(string name, Vector3 where)
        {
            GameObject obj = new GameObject(name);
            obj.transform.position = where;
            return obj.transform;
        }

        private static void CreateEchoActors(KZStoryDirector director)
        {
            GameObject group = new GameObject("Year1992_仅残响激活");
            List<Transform> heads = new List<Transform>();

            // 群演只使用少量几何体占位，不涉及身份真相或后续章节。
            for (int i = 0; i < 12; ++i)
            {
                int seatNumber = i < 6 ? i + 1 : i + 8;
                Vector3 where = new Vector3((seatNumber - 7) * 1.2f, 0f, 6.8f);
                GameObject extra = ShadowFigure("1992观众_" + i, group.transform, where, darkMat);
                Transform head = extra.transform.Find("头部_占位");
                if (head != null) heads.Add(head);
            }

            GameObject actress = ShadowFigure("沈芸_演出占位_可替换Timeline",
                group.transform, new Vector3(5.9f, .8f, 12f), redMat);
            director.echoActress = actress.transform;

            GameObject keys = Block("周明生的钥匙_残响", new Vector3(0f, .68f, 6.98f),
                new Vector3(.24f, .12f, .12f), goldMat);
            keys.transform.SetParent(group.transform, true);
            director.echoKey = keys;

            director.echoRoute = new[]
            {
                Point("ECHO00_舞台候场", new Vector3(5.9f,.8f,12f)),
                Point("ECHO01_东侧台口", new Vector3(7.3f,.8f,9.6f)),
                Point("ECHO02_东侧高阶", new Vector3(8f,.6f,8.7f)),
                Point("ECHO03_东侧低阶", new Vector3(8f,.2f,7.6f)),
                Point("ECHO04_台前走道", new Vector3(8f,0f,7.2f)),
                Point("ECHO05_空座侧面", new Vector3(.75f,0f,7.65f)),
                Point("ECHO06_一排七座", new Vector3(0f,0f,6.8f))
            };
            director.crowdHeads = heads.ToArray();
            director.echoPlayerView = Point("ECHO_玩家观察点", new Vector3(0f, 0f, -6.3f));
            director.echo1992Group = group;
            group.SetActive(false);
        }
    }
}
#endif
