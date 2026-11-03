# 《空座》Unity 第四周 P0 · 完整 C# 源代码（v0.3）

每个节标题即 Unity `Assets/KongZuo/` 内对应的相对路径。
请优先导入 ZIP 中的 `.cs` 文件，不建议把不同类直接粘成一个脚本。
Editor 脚本必须位于 `Editor/` 文件夹。
本地未安装 Unity 6.3，以下属于待在 Unity 中编译/Play 验证的源码。

## `Assets/KongZuo/Editor/KZMainProjectSetup.cs`

```csharp
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

        [MenuItem("空座/01 生成第四周P0灰盒场景")]
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
                "WASD移动，E调查，Tab笔记，数字1/3调整座次，Enter判定。", "开始测试");
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
                "票根上残留着局部座次与姓名。\n\n" +
                "示例线索（待主策划确认）：观众甲为一排六座，" +
                "观众乙为一排八座。\n七号位没有售票记录。",
                new Vector3(6.2f, 1.0f, -11.2f), new Vector3(.68f, .07f, .44f), redMat);
            Prop("一排座次示意图", KZActionKind.SeatPuzzle,
                "选择人物卡，恢复第一排部分座次。",
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
```

## `Assets/KongZuo/Scripts/Chapters/KZSeatPuzzle.cs`

```csharp
namespace KongZuo
{
    // 第四周可替换样例：一排六座=观众甲，七座=空，八座=观众乙。
    // 玩家只填写6、8；7号为留座规则，永远不能放人。示例姓名不属于正式剧本设定。
    public sealed class KZSeatPuzzle
    {
        public enum Result { MissingClues, Incomplete, Incorrect, Correct }

        // 0 = 未填；1 = 观众甲；2 = 观众乙。
        private int seat6;
        private int seat8;

        public string Seat6 { get { return Name(seat6); } }
        public string Seat7 { get { return "空（不可安排观众）"; } }
        public string Seat8 { get { return Name(seat8); } }

        private static string Name(int value)
        {
            switch (value)
            {
                case 1: return "观众甲";
                case 2: return "观众乙";
                default: return "未填";
            }
        }

        public bool Cycle(int index)
        {
            if (index != 0 && index != 2) return false; // 七号位置锁定
            int current = index == 0 ? seat6 : seat8;
            int other = index == 0 ? seat8 : seat6;
            for (int step = 1; step <= 3; ++step)
            {
                int next = (current + step) % 3;
                if (next != 0 && next == other) continue; // 不允许重复使用人物卡
                if (index == 0) seat6 = next;
                else seat8 = next;
                return true;
            }
            return false;
        }

        public Result Check(bool allCluesKnown)
        {
            if (!allCluesKnown) return Result.MissingClues;
            if (seat6 == 0 || seat8 == 0) return Result.Incomplete;
            return seat6 == 1 && seat8 == 2 ? Result.Correct : Result.Incorrect;
        }
    }
}
```

## `Assets/KongZuo/Scripts/Chapters/KZStoryDirector.cs`

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KongZuo
{
    // 序章与第一章的P0剧情控制器。之后可替换成 Timeline/正式任务系统。
    [DisallowMultipleComponent]
    public sealed class KZStoryDirector : MonoBehaviour
    {
        public static KZStoryDirector Current { get; private set; }
        public bool isPrologue = true;

        [Header("公共")]
        public KZFirstPersonPlayer player;
        public Light stageLamp;

        [Header("序章")]
        public GameObject frontDoor;
        public GameObject anomalyShadow;
        public GameObject programProp;

        [Header("第一章／1992残响")]
        public GameObject echo1992Group;
        public Transform echoActress;
        public Transform[] echoRoute;
        public Transform[] crowdHeads;
        public GameObject echoKey;
        public Transform echoPlayerView;

        private KZGameSession session;
        private bool visitedAudience;
        private bool leftAudienceAfterVisit;
        private bool busy;
        private float originalLampIntensity;
        private Color originalAmbient;

        public bool IsBusy { get { return busy; } }

        public string Objective
        {
            get
            {
                if (session == null) return "载入剧情中";
                if (isPrologue)
                {
                    if (!Has(KZFlags.Worksheet)) return "查看拆楼前资产清点表";
                    if (!Has(KZFlags.Door)) return "打开正门，进入长明戏楼";
                    if (!Has(KZFlags.Phone)) return "到办公室接听电话";
                    if (!Has(KZFlags.Seat)) return "调查观众厅的第一排七座";
                    if (!Has(KZFlags.Register)) return "前往售票口核对1992年的座位登记表";
                    if (!Has(KZFlags.AnomalyStarted)) return "离开观众厅后，从前厅重新进入";
                    if (!Has(KZFlags.AnomalyResolved)) return "靠近第一排，确认刚才的人影";
                    if (!Has(KZFlags.Program)) return "查看新出现的1992年节目单";
                    return "序章完成，准备进入第一章";
                }

                if (!Has(KZFlags.Ritual)) return "在前厅阅读留座戏宣传册";
                if (!Has(KZFlags.Photo)) return "查看旧合照及背面文字";
                if (!Has(KZFlags.Ticket)) return "前往售票口调查残缺票根";
                if (!Has(KZFlags.PuzzleSolved)) return "在售票口的座次图前复原部分座位";
                if (!Has(KZFlags.EchoFinished)) return "观察1992年的记忆残响";
                return "第一章灰盒完成：继续复核已获得的证据";
            }
        }

        private void Awake()
        {
            Current = this;
            session = KZGameSession.EnsureExists();
            originalLampIntensity = stageLamp == null ? 0f : stageLamp.intensity;
            originalAmbient = RenderSettings.ambientLight;
        }

        private void Start()
        {
            if (isPrologue)
            {
                if (frontDoor != null) frontDoor.SetActive(!Has(KZFlags.Door));
                if (anomalyShadow != null)
                    anomalyShadow.SetActive(Has(KZFlags.AnomalyStarted) &&
                                            !Has(KZFlags.AnomalyResolved));
                if (programProp != null) programProp.SetActive(Has(KZFlags.AnomalyResolved));

                if (!Has(KZFlags.Worksheet))
                    KZHud.ShowPage("临槐县文化馆 · 拆除前资产清点表",
                        "2004年10月21日 17:42\n\n长明戏楼拆除前资产清点表\n\n" +
                        "□ 戏服  □ 照片  □ 磁带\n□ 乐器  □ 演出资料  □ 历史档案\n\n" +
                        "手写备注：明早施工，今晚务必完成。\n\n" +
                        "你是县文化馆资料员林砚，负责最后的资料登记。",
                        () => Mark(KZFlags.Worksheet));
                else KZHud.ShowToast("继续进行拆楼前的清点工作。");
            }
            else
            {
                if (echo1992Group != null) echo1992Group.SetActive(false);
                if (Has(KZFlags.EchoStarted) && !Has(KZFlags.EchoFinished))
                    session.RemoveFlag(KZFlags.EchoStarted); // 中断恢复，不永久卡关
                KZHud.ShowPage("第一章 · 座",
                    "节目单指向1992年7月17日的留座戏。\n\n" +
                    "从前厅的民俗资料、旧合照与售票处的票根入手，" +
                    "再核对第一排中央那把椅子。\n\n" +
                    "这是玩法灰盒；观众姓名、座次规则均可由主策划修改。");
            }
        }

        private bool Has(string flag) { return session.HasFlag(flag); }
        private void Mark(string flag) { session.TryAddFlag(flag); }

        private bool ReadyForAnomaly()
        {
            return Has(KZFlags.Phone) && Has(KZFlags.Seat) && Has(KZFlags.Register);
        }

        private bool AllPuzzleClues()
        {
            return Has(KZFlags.Ritual) && Has(KZFlags.Photo) && Has(KZFlags.Ticket);
        }

        public void Handle(KZInteractable item)
        {
            if (item == null || busy) return;
            switch (item.action)
            {
                case KZActionKind.Worksheet:
                    KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.Worksheet));
                    break;
                case KZActionKind.FrontDoor:
                    if (!Has(KZFlags.Worksheet))
                        KZHud.ShowToast("先查看手中的清点表。");
                    else
                    {
                        Mark(KZFlags.Door);
                        item.gameObject.SetActive(false);
                        KZHud.ShowToast("门轴发出一声沉闷的摩擦声。你进入了戏楼。");
                    }
                    break;
                case KZActionKind.Phone:
                    KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.Phone));
                    break;
                case KZActionKind.Seat07:
                    if (Has(KZFlags.AnomalyResolved) && !Has(KZFlags.Program))
                    {
                        ShowFoundProgram();
                    }
                    else KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.Seat));
                    break;
                case KZActionKind.Register:
                    KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.Register));
                    break;
                case KZActionKind.Program:
                    if (!Has(KZFlags.AnomalyResolved))
                        KZHud.ShowToast("这里暂时还没有节目单。");
                    else ShowFoundProgram();
                    break;
                case KZActionKind.Ritual:
                    KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.Ritual));
                    break;
                case KZActionKind.OldPhoto:
                    KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.Photo));
                    break;
                case KZActionKind.Ticket:
                    KZHud.ShowPage(item.DisplayName, item.body, () => Mark(KZFlags.Ticket));
                    break;
                case KZActionKind.SeatPuzzle:
                    if (Has(KZFlags.PuzzleSolved))
                    {
                        KZHud.ShowToast("座次已恢复：一排七座留给亡者。");
                        return;
                    }
                    KZHud.ShowPuzzle(AllPuzzleClues, () =>
                    {
                        if (Has(KZFlags.PuzzleSolved)) return;
                        Mark(KZFlags.PuzzleSolved);
                        StartCoroutine(PlayEcho());
                    });
                    break;
                case KZActionKind.BackstageDoor:
                case KZActionKind.Poster:
                    KZHud.ShowPage(item.DisplayName, item.body);
                    break;
            }
        }

        private void ShowFoundProgram()
        {
            const string contents = "一九九二年七月十七日\n长明戏楼\n周明生留座戏\n\n" +
                "纸张像是刚刚被谁放在椅子上的。";
            if (Has(KZFlags.PrologueDone))
            {
                KZHud.ShowPage("新出现的旧节目单", contents);
                return;
            }
            KZHud.ShowPage("新出现的旧节目单", contents, () =>
            {
                Mark(KZFlags.Program);
                StartCoroutine(EnterFirstChapter());
            });
        }

        public void AudienceEntered()
        {
            if (!isPrologue || Has(KZFlags.AnomalyStarted)) return;
            if (visitedAudience && leftAudienceAfterVisit && ReadyForAnomaly())
            {
                if (session.TryAddFlag(KZFlags.AnomalyStarted))
                {
                    if (anomalyShadow != null) anomalyShadow.SetActive(true);
                    KZHud.ShowToast("观众厅暗了一瞬。第一排七座上，似乎坐着一个人……", 6f);
                }
            }
            visitedAudience = true;
        }

        public void AudienceExited(float playerZ)
        {
            if (!isPrologue) return;
            if (visitedAudience && playerZ < -7f) leftAudienceAfterVisit = true;
        }

        public void ApproachedSeat()
        {
            if (!isPrologue || busy || !Has(KZFlags.AnomalyStarted)
                || Has(KZFlags.AnomalyResolved)) return;
            StartCoroutine(ResolveAnomaly());
        }

        private IEnumerator ResolveAnomaly()
        {
            busy = true;
            KZHud.ShowToast("舞台上的灯突然闪亮，又迅速熄灭……", 2f);
            if (stageLamp != null) stageLamp.intensity = originalLampIntensity * 3f;
            yield return new WaitForSeconds(0.30f);
            if (stageLamp != null) stageLamp.intensity = 0f;
            yield return new WaitForSeconds(0.45f);
            if (anomalyShadow != null) anomalyShadow.SetActive(false);
            if (programProp != null) programProp.SetActive(true);
            if (stageLamp != null) stageLamp.intensity = originalLampIntensity;
            Mark(KZFlags.AnomalyResolved);
            busy = false;
            KZHud.ShowToast("人影消失了。座椅上多出一张节目单。", 6f);
        }

        private IEnumerator EnterFirstChapter()
        {
            busy = true;
            if (player != null) player.SetFreezeWalking(true);
            KZHud.ShowToast("《空座》\n第一章 · 座", 2.1f);
            yield return new WaitForSeconds(1.7f);
            Mark(KZFlags.PrologueDone);
            if (Application.CanStreamedLevelBeLoaded(KZSceneNames.ChapterOne))
                SceneManager.LoadScene(KZSceneNames.ChapterOne);
            else
            {
                busy = false;
                if (player != null) player.SetFreezeWalking(false);
                KZHud.ShowToast("第一章未加入 Build Profiles 的场景列表。");
            }
        }

        private IEnumerator PlayEcho()
        {
            if (!session.TryAddFlag(KZFlags.EchoStarted)) yield break;
            busy = true;
            if (player != null)
            {
                player.SetFreezeWalking(true);
                if (echoPlayerView != null)
                    player.Teleport(echoPlayerView.position, echoPlayerView.rotation);
            }
            KZHud.ShowToast("咚—— 一声锣响。\n戏楼的光线慢慢消失。", 3f);
            if (stageLamp != null) stageLamp.intensity = 0f;
            yield return new WaitForSeconds(1.1f);

            // 这里是双时代模型组开关，之后可接 Unity Timeline 与 URP Volume。
            if (echo1992Group != null) echo1992Group.SetActive(true);
            if (echoKey != null) echoKey.SetActive(true);
            RenderSettings.ambientLight = new Color(.60f, .40f, .26f);
            if (stageLamp != null) stageLamp.intensity = originalLampIntensity * 2f;
            KZHud.ShowToast("【1992 · 残响】观众坐满戏楼，台上又传来唱腔。", 5f);
            yield return new WaitForSeconds(1.3f);

            if (echoActress != null && echoRoute != null && echoRoute.Length >= 2)
            {
                echoActress.position = echoRoute[0].position;
                for (int i = 1; i < echoRoute.Length; ++i)
                {
                    Vector3 from = echoRoute[i - 1].position;
                    Vector3 to = echoRoute[i].position;
                    Vector3 direction = to - from;
                    if (direction.sqrMagnitude > 0.001f)
                        echoActress.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                    float duration = Mathf.Max(0.3f, direction.magnitude / 2.1f);
                    float elapsed = 0f;
                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        echoActress.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                        yield return null;
                    }
                    echoActress.position = to;
                }
            }

            KZHud.ShowToast("穿戏服的女子在一排七座前停下，缓缓坐了下去……", 5f);
            if (echoActress != null)
            {
                Vector3 start = echoActress.position;
                Vector3 sit = start + Vector3.down * .42f;
                float elapsed = 0f;
                while (elapsed < 1.2f)
                {
                    elapsed += Time.deltaTime;
                    echoActress.position = Vector3.Lerp(start, sit, Mathf.Clamp01(elapsed / 1.2f));
                    yield return null;
                }
            }

            if (crowdHeads != null && echoActress != null)
                foreach (Transform head in crowdHeads)
                    if (head != null)
                    {
                        Vector3 towards = echoActress.position + Vector3.up * 0.9f - head.position;
                        if (towards.sqrMagnitude > 0.001f)
                            head.rotation = Quaternion.LookRotation(towards, Vector3.up);
                    }

            KZHud.ShowToast("所有观众同时转头看向她。她伸手，取走了空座上的钥匙。", 4f);
            yield return new WaitForSeconds(1.8f);
            if (echoKey != null) echoKey.SetActive(false);
            yield return new WaitForSeconds(1.1f);

            // 无论正式 Timeline 后续怎样改，必须恢复原始时空与玩家输入。
            Restore2004();
            Mark(KZFlags.EchoFinished);
            Mark(KZFlags.ChapterOneDone);
            KZHud.ShowPage("第一章结束 · 座",
                "灯光重新亮起，戏楼又是2004年的破败模样。\n\n" +
                "你目睹一个穿戏服的女子坐上一排七座，从椅上取走钥匙。\n" +
                "这段记忆并不说明她为何这么做。\n\n" +
                "【第四周原型到此结束】第二章由后续团队任务接入。");
        }

        private void Restore2004()
        {
            if (echo1992Group != null) echo1992Group.SetActive(false);
            if (stageLamp != null) stageLamp.intensity = originalLampIntensity;
            RenderSettings.ambientLight = originalAmbient;
            if (player != null) player.SetFreezeWalking(false);
            busy = false;
        }

        private void OnDisable()
        {
            // 场景切换、禁用或意外中断时不要遗留锁定的玩家控制。
            Restore2004();
            if (Current == this) Current = null;
        }
    }
}
```

## `Assets/KongZuo/Scripts/Core/KZBootstrapEntry.cs`

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KongZuo
{
    public sealed class KZBootstrapEntry : MonoBehaviour
    {
        private void Start()
        {
            // 从总入口启动 = 新一局；直接 Play 单个关卡则不强制清空调试状态。
            KZGameSession.EnsureExists().ResetSession();
            if (!Application.CanStreamedLevelBeLoaded(KZSceneNames.Prologue))
            {
                Debug.LogError("[空座] 缺少序章场景；请先执行 空座/生成P0灰盒场景。");
                return;
            }
            SceneManager.LoadScene(KZSceneNames.Prologue);
        }
    }
}
```

## `Assets/KongZuo/Scripts/Core/KZFlags.cs`

```csharp
namespace KongZuo
{
    // 跨场景的剧情ID，必须统一命名；不要把剧情状态写在单个道具的私有变量里。
    public static class KZFlags
    {
        public const string Worksheet = "WORKSHEET_VIEWED";
        public const string Door = "FRONT_DOOR_OPENED";
        public const string Phone = "CALL_DONE";
        public const string Seat = "SEAT_INSPECTED";
        public const string Register = "REGISTER_READ";
        public const string AnomalyStarted = "EVT_ANOMALY_STARTED";
        public const string AnomalyResolved = "EVT_ANOMALY_RESOLVED";
        public const string Program = "PROGRAM_FOUND";
        public const string PrologueDone = "PROLOGUE_DONE";
        public const string Ritual = "RITUAL_READ";
        public const string Photo = "ZHOU_IDENTIFIED";
        public const string Ticket = "TICKETS_OBTAINED";
        public const string PuzzleSolved = "SEATING_SOLVED";
        public const string EchoStarted = "ECHO01_STARTED";
        public const string EchoFinished = "ECHO01_FINISHED";
        public const string ChapterOneDone = "CHAPTER1_DONE";
    }
}
```

## `Assets/KongZuo/Scripts/Core/KZGameSession.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KongZuo
{
    /// <summary>跨场景保留的最小剧情状态容器。正式存档系统由后续版本接入。</summary>
    [DisallowMultipleComponent]
    public sealed class KZGameSession : MonoBehaviour
    {
        public static KZGameSession Instance { get; private set; }

        private readonly HashSet<string> flags = new HashSet<string>(StringComparer.Ordinal);
        public event Action<string> FlagAdded;
        public string CurrentSceneName { get; private set; }

        public static KZGameSession EnsureExists()
        {
            if (Instance != null) return Instance;

            KZGameSession existing = FindFirstObjectByType<KZGameSession>();
            if (existing != null) return existing;

            return new GameObject("KZ_GameSession").AddComponent<KZGameSession>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            CurrentSceneName = SceneManager.GetActiveScene().name;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CurrentSceneName = scene.name;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        public bool HasFlag(string flag)
        {
            return !string.IsNullOrWhiteSpace(flag) && flags.Contains(flag);
        }

        public bool TryAddFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag)) return false;
            if (!flags.Add(flag)) return false;
            FlagAdded?.Invoke(flag);
            return true;
        }


        // 用于恢复中断的演出，不清除其他已经收集的线索。
        public bool RemoveFlag(string flag)
        {
            return !string.IsNullOrWhiteSpace(flag) && flags.Remove(flag);
        }

        public string[] GetFlagsSorted()
        {
            string[] result = new string[flags.Count];
            flags.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        public void ResetSession()
        {
            flags.Clear();
        }
    }
}
```

## `Assets/KongZuo/Scripts/Core/KZSceneNames.cs`

```csharp
namespace KongZuo
{
    /// <summary>场景名统一管理，禁止用散落的字符串拼写章节名称。</summary>
    public static class KZSceneNames
    {
        public const string Bootstrap = "00_Bootstrap";
        public const string Prologue = "01_Prologue";
        public const string ChapterOne = "02_Chapter01";

        public const string BootstrapPath = "Assets/KongZuo/Scenes/00_Bootstrap.unity";
        public const string ProloguePath = "Assets/KongZuo/Scenes/01_Prologue.unity";
        public const string ChapterOnePath = "Assets/KongZuo/Scenes/02_Chapter01.unity";
    }
}
```

## `Assets/KongZuo/Scripts/Gameplay/KZFirstPersonPlayer.cs`

```csharp
using UnityEngine;

namespace KongZuo
{
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class KZFirstPersonPlayer : MonoBehaviour
    {
        [SerializeField] private Transform eye;
        [SerializeField, Min(0f)] private float walkSpeed = 3.5f;
        [SerializeField, Min(0f)] private float sensitivity = 1f;
        [SerializeField] private float gravity = -19.6f;

        private CharacterController controller;
        private float pitch;
        private float verticalSpeed;
        private bool freezeWalking;

        public void SetCameraPivot(Transform pivot) { eye = pivot; }
        public void SetFreezeWalking(bool frozen) { freezeWalking = frozen; }

        public void Teleport(Vector3 position, Quaternion facing)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetPositionAndRotation(position, facing);
            controller.enabled = true;
            pitch = 0f;
            verticalSpeed = 0f;
            if (eye != null) eye.localRotation = Quaternion.identity;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (eye == null)
            {
                Camera cameraChild = GetComponentInChildren<Camera>();
                if (cameraChild != null) eye = cameraChild.transform;
            }
        }

        private void Start()
        {
            KZGameSession.EnsureExists();
            SetCursor(true);
        }

        private void OnDisable() { SetCursor(false); }

        private void Update()
        {
            if (KZHud.IsModal) return;

            if (KZInput.Escape()) SetCursor(Cursor.lockState != CursorLockMode.Locked);
            else if (Cursor.lockState != CursorLockMode.Locked && KZInput.Click()) SetCursor(true);

            if (Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 look = KZInput.Look() * sensitivity;
            transform.Rotate(Vector3.up, look.x, Space.Self);
            pitch = Mathf.Clamp(pitch - look.y, -82f, 82f);
            if (eye != null) eye.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // 残响时允许观察，但不允许移动；文档和谜题窗口期间完全暂停操作。
            if (freezeWalking) return;

            Vector2 axis = KZInput.Move();
            Vector3 direction = transform.right * axis.x + transform.forward * axis.y;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -1.5f;
            verticalSpeed += gravity * Time.deltaTime;
            controller.Move((direction * walkSpeed + Vector3.up * verticalSpeed) * Time.deltaTime);
        }

        private static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
```

## `Assets/KongZuo/Scripts/Gameplay/KZInput.cs`

```csharp
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KongZuo
{
    // 无需手工创建 InputAction：灰盒阶段直接监听键盘/鼠标。
    // 同时支持仅启用新输入系统、仅旧系统及 Both（新系统优先）。
    public static class KZInput
    {
        private static int lastInteractFrame = -1;

        public static Vector2 Move()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return Vector2.zero;
            int x = ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1 : 0)
                - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1 : 0);
            int y = ((k.wKey.isPressed || k.upArrowKey.isPressed) ? 1 : 0)
                - ((k.sKey.isPressed || k.downArrowKey.isPressed) ? 1 : 0);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Vector2.ClampMagnitude(
                new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#else
            return Vector2.zero;
#endif
        }

        public static Vector2 Look()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue() * 0.09f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 2f;
#else
            return Vector2.zero;
#endif
        }

        private static bool RawInteract()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }

        // 避免：同一帧 E 关闭文档后又重新触发射线交互。
        public static bool ConsumeInteract()
        {
            if (!RawInteract() || lastInteractFrame == Time.frameCount) return false;
            lastInteractFrame = Time.frameCount;
            return true;
        }

        public static bool Escape()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        public static bool Journal()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Tab);
#else
            return false;
#endif
        }

        public static bool Click()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        // index: 0=数字1, 1=数字2, 2=数字3
        public static bool Number(int index)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return false;
            switch (index)
            {
                case 0: return k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame;
                case 1: return k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame;
                case 2: return k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame;
                default: return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            switch (index)
            {
                case 0: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
                case 1: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
                case 2: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
                default: return false;
            }
#else
            return false;
#endif
        }

        public static bool Confirm()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.numpadEnterKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
            return false;
#endif
        }
    }
}
```

## `Assets/KongZuo/Scripts/Gameplay/KZInteractable.cs`

```csharp
using UnityEngine;

namespace KongZuo
{
    public enum KZActionKind
    {
        Worksheet, FrontDoor, Phone, Seat07, Register, Program,
        Ritual, OldPhoto, Ticket, SeatPuzzle, BackstageDoor, Poster
    }

    [DisallowMultipleComponent]
    public sealed class KZInteractable : MonoBehaviour
    {
        public KZActionKind action;
        public string displayName = "调查";
        [TextArea(2, 7)] public string body = "";
        public string DisplayName { get { return string.IsNullOrWhiteSpace(displayName) ? name : displayName; } }

        public void Interact()
        {
            if (KZStoryDirector.Current == null)
            {
                KZHud.ShowToast("本场景未安装 KZStoryDirector。");
                return;
            }
            KZStoryDirector.Current.Handle(this);
        }
    }
}
```

## `Assets/KongZuo/Scripts/Gameplay/KZInteractor.cs`

```csharp
using UnityEngine;

namespace KongZuo
{
    [DisallowMultipleComponent]
    public sealed class KZInteractor : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0.5f)] private float reach = 3.3f;
        public void SetCamera(Camera camera) { viewCamera = camera; }

        private void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            if (KZHud.IsModal || Cursor.lockState != CursorLockMode.Locked ||
                (KZStoryDirector.Current != null && KZStoryDirector.Current.IsBusy))
            {
                KZHud.SetHint("");
                return;
            }

            KZInteractable item = null;
            if (viewCamera != null &&
                Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward,
                    out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                item = hit.collider.GetComponentInParent<KZInteractable>();
            }

            KZHud.SetHint(item == null ? "" : "[E] " + item.DisplayName);
            if (item != null && KZInput.ConsumeInteract()) item.Interact();
        }
    }
}
```

## `Assets/KongZuo/Scripts/Gameplay/KZStoryTrigger.cs`

```csharp
using UnityEngine;

namespace KongZuo
{
    public enum KZZone { Audience, ApproachSeat }

    // 给 BoxCollider 勾选 Is Trigger。只响应玩家 CharacterController。
    [RequireComponent(typeof(BoxCollider))]
    public sealed class KZStoryTrigger : MonoBehaviour
    {
        public KZZone zone;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<KZFirstPersonPlayer>() == null) return;
            if (KZStoryDirector.Current == null) return;
            if (zone == KZZone.Audience) KZStoryDirector.Current.AudienceEntered();
            else KZStoryDirector.Current.ApproachedSeat();
        }

        private void OnTriggerExit(Collider other)
        {
            if (zone != KZZone.Audience ||
                other.GetComponentInParent<KZFirstPersonPlayer>() == null ||
                KZStoryDirector.Current == null) return;
            KZStoryDirector.Current.AudienceExited(other.transform.position.z);
        }
    }
}
```

## `Assets/KongZuo/Scripts/UI/KZHud.cs`

```csharp
using System;
using UnityEngine;

namespace KongZuo
{
    // 第四周测试UI。使用IMGUI，不额外依赖TMP和Canvas；美术入场后可整体替换。
    [DisallowMultipleComponent]
    public sealed class KZHud : MonoBehaviour
    {
        public string chapterName = "《空座》";
        private static KZHud current;
        private static string hint = "";

        private string toast = "";
        private string pageTitle;
        private string pageBody;
        private Action pageClosed;
        private bool journalOpen;
        private bool puzzleOpen;
        private KZSeatPuzzle puzzle = new KZSeatPuzzle();
        private Func<bool> cluesKnown;
        private Action puzzleSolved;
        private string puzzleFeedback = "";
        private Vector2 scroll;
        private float toastUntil;

        public static bool IsModal
        {
            get { return current != null &&
                (current.pageTitle != null || current.journalOpen || current.puzzleOpen); }
        }

        private void Awake() { current = this; }
        private void OnDestroy()
        {
            if (current == this) { current = null; hint = ""; }
        }

        public static void SetHint(string value) { hint = value ?? ""; }

        public static void ShowToast(string text, float seconds = 5f)
        {
            if (current == null) { Debug.Log("[空座] " + text); return; }
            current.toast = text ?? "";
            current.toastUntil = Time.unscaledTime + seconds;
        }

        public static void ShowPage(string title, string body, Action onClose = null)
        {
            if (current == null)
            {
                Debug.Log("[空座] " + title + ": " + body);
                if (onClose != null) onClose();
                return;
            }
            current.journalOpen = false;
            current.puzzleOpen = false;
            current.pageTitle = title ?? "资料检视";
            current.pageBody = body ?? "";
            current.scroll = Vector2.zero;
            current.pageClosed = onClose;
        }

        public static void ShowPuzzle(Func<bool> isReady, Action onSolved)
        {
            if (current == null) { Debug.LogWarning("缺少 KZHud。"); return; }
            current.journalOpen = false;
            current.pageTitle = null;
            current.pageBody = null;
            current.puzzleOpen = true;
            current.cluesKnown = isReady;
            current.puzzleSolved = onSolved;
            current.puzzleFeedback = "根据宣传册、合照与票根填写座次。";
        }

        private void Update()
        {
            if (pageTitle != null)
            {
                if (KZInput.ConsumeInteract())
                {
                    Action callback = pageClosed;
                    pageTitle = null;
                    pageBody = null;
                    pageClosed = null;
                    if (callback != null) callback();
                }
                return;
            }

            if (puzzleOpen)
            {
                if (KZInput.ConsumeInteract())
                {
                    puzzleOpen = false;
                    return;
                }
                if (KZInput.Number(1))
                    puzzleFeedback = "一排七座必须留空，不能安排观众。";
                if (KZInput.Number(0)) puzzle.Cycle(0);
                if (KZInput.Number(2)) puzzle.Cycle(2);
                if (KZInput.Confirm())
                {
                    KZSeatPuzzle.Result result = puzzle.Check(cluesKnown != null && cluesKnown());
                    switch (result)
                    {
                        case KZSeatPuzzle.Result.MissingClues:
                            puzzleFeedback = "还缺少关键证据：请查看宣传册、旧合照、票根。";
                            break;
                        case KZSeatPuzzle.Result.Incomplete:
                            puzzleFeedback = "还有位置未填写；按数字1和3切换人物。";
                            break;
                        case KZSeatPuzzle.Result.Incorrect:
                            puzzleFeedback = "座次与已有线索不符，可以继续修改。";
                            break;
                        case KZSeatPuzzle.Result.Correct:
                            puzzleOpen = false;
                            Action callback = puzzleSolved;
                            puzzleSolved = null;
                            if (callback != null) callback();
                            break;
                    }
                }
                return;
            }

            if (KZInput.Journal()) journalOpen = !journalOpen;
            if (journalOpen && (KZInput.ConsumeInteract() || KZInput.Escape()))
                journalOpen = false;
        }

        private void OnGUI()
        {
            GUIStyle label = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true, fontSize = 16, alignment = TextAnchor.UpperLeft
            };
            GUIStyle head = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true, fontSize = 21, fontStyle = FontStyle.Bold
            };

            GUI.Box(new Rect(14f, 14f, Mathf.Min(650f, Screen.width - 28f), 102f), "");
            GUI.Label(new Rect(28f, 22f, Screen.width - 60f, 30f), chapterName, head);
            string objective = KZStoryDirector.Current == null
                ? "等待关卡逻辑" : KZStoryDirector.Current.Objective;
            GUI.Label(new Rect(28f, 54f, Screen.width - 60f, 55f), "当前目标：" + objective, label);

            if (pageTitle != null)
            {
                float w = Mathf.Min(760f, Screen.width - 40f);
                float h = Mathf.Max(160f, Mathf.Min(460f, Screen.height - 140f));
                Rect area = new Rect((Screen.width - w) * .5f, (Screen.height - h) * .5f, w, h);
                GUI.Box(area, "");
                GUILayout.BeginArea(new Rect(area.x + 24f, area.y + 20f, area.width - 48f, area.height - 40f));
                GUILayout.Label(pageTitle, head);
                scroll = GUILayout.BeginScrollView(scroll);
                GUILayout.Label(pageBody, label);
                GUILayout.EndScrollView();
                GUILayout.FlexibleSpace();
                GUILayout.Label("按 E 收起文档／继续", label);
                GUILayout.EndArea();
            }
            else if (puzzleOpen)
            {
                float w = Mathf.Min(760f, Screen.width - 40f);
                float h = Mathf.Max(160f, Mathf.Min(440f, Screen.height - 140f));
                Rect area = new Rect((Screen.width - w) * .5f, (Screen.height - h) * .5f, w, h);
                GUI.Box(area, "");
                GUILayout.BeginArea(new Rect(area.x + 24f, area.y + 18f, area.width - 48f, area.height - 36f));
                GUILayout.Label("座次复原（可替换规则样例）", head);
                GUILayout.Space(12f);
                GUILayout.Label("一排六座 [按1切换]：" + puzzle.Seat6, label);
                GUILayout.Label("一排七座 [按2尝试]：" + puzzle.Seat7, label);
                GUILayout.Label("一排八座 [按3切换]：" + puzzle.Seat8, label);
                GUILayout.Space(12f);
                GUILayout.Label("请依据已读票根安排观众甲与观众乙。" +
                    "\n一排七座必须留空，且同一观众不能出现在两张椅子上。", label);
                GUILayout.Space(12f);
                GUILayout.Label(puzzleFeedback, label);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Enter 提交判定 · E 返回现场", label);
                GUILayout.EndArea();
            }
            else if (journalOpen)
            {
                float w = Mathf.Min(620f, Screen.width - 36f);
                float h = Mathf.Max(160f, Mathf.Min(440f, Screen.height - 140f));
                Rect area = new Rect(Screen.width - w - 16f, 132f, w, h);
                GUI.Box(area, "");
                GUILayout.BeginArea(new Rect(area.x + 20f, area.y + 18f, area.width - 40f, area.height - 36f));
                GUILayout.Label("调查笔记", head);
                GUILayout.Label(CollectedClues(), label);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Tab／E／Esc 收起", label);
                GUILayout.EndArea();
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(hint))
                {
                    float w = Mathf.Min(410f, Screen.width - 24f);
                    GUI.Box(new Rect((Screen.width - w) * .5f, Screen.height - 128f, w, 45f), hint);
                }
                GUI.Label(new Rect(Screen.width * .5f - 7f, Screen.height * .5f - 12f, 18f, 22f), "+");
            }

            if (Time.unscaledTime < toastUntil && !string.IsNullOrEmpty(toast))
            {
                float w = Mathf.Min(950f, Screen.width - 30f);
                GUI.Box(new Rect((Screen.width - w) * .5f, Screen.height - 74f, w, 57f), toast);
            }
            GUI.Label(new Rect(18f, Screen.height - 25f, Screen.width - 30f, 23f),
                "WASD 移动  ·  鼠标观察  ·  E 调查/关闭  ·  Tab 笔记  ·  Esc 鼠标释放/捕获");
        }

        private static string CollectedClues()
        {
            KZGameSession s = KZGameSession.EnsureExists();
            string result = "";
            if (s.HasFlag(KZFlags.Worksheet)) result += "• 资产清点表：明早施工，今晚务必完成。\n\n";
            if (s.HasFlag(KZFlags.Phone)) result += "• 电话：何文斌提醒后台危险，只登记数量。\n\n";
            if (s.HasFlag(KZFlags.Seat)) result += "• 第一排七座：椅面比其他位置干净。\n\n";
            if (s.HasFlag(KZFlags.Register)) result += "• 1992座位登记表：一排七座——空。\n\n";
            if (s.HasFlag(KZFlags.Program)) result += "• 节目单：1992年7月17日，周明生留座戏。\n\n";
            if (s.HasFlag(KZFlags.Ritual)) result += "• 留座戏：亡者留座，戏散前不得取遗物。\n\n";
            if (s.HasFlag(KZFlags.Photo)) result += "• 旧合照背面：周明生，1972—1992。\n\n";
            if (s.HasFlag(KZFlags.Ticket)) result += "• 票根：可与座次表核对。\n\n";
            if (s.HasFlag(KZFlags.PuzzleSolved)) result += "• 座次图：一排七座必须留空。\n\n";
            if (s.HasFlag(KZFlags.EchoFinished)) result += "• 残响：女子在空座坐下，拿走钥匙。\n\n";
            return result.Length == 0 ? "暂时没有记录。" : result;
        }
    }
}
```
