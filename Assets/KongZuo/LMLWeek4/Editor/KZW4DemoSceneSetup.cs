#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace KongZuo.LMLWeek4.EditorTools
{
    /// <summary>
    /// Creates assets ONLY under Assets/KongZuo/LMLWeek4.
    /// Never edits the shared Bootstrap / Prologue / Chapter01 scenes or Build Settings.
    /// </summary>
    public static class KZW4DemoSceneSetup
    {
        private const string Root = "Assets/KongZuo/LMLWeek4";
        private const string Materials = Root + "/Materials";
        private const string DemoScene = Root + "/Scenes/LML_Week4_Demo.unity";
        private const string PuzzlePath = Root + "/KZW4_RelicPuzzle.asset";
        private const string AudioPath = Root + "/KZW4_AudioCatalog.asset";

        private static Material floor, wall, wood, paper, stage, shadow, gold;

        [MenuItem("空座/LML 第四周/01 生成独立演示场景")]
        public static void CreateDemo()
        {
            if (Application.unityVersion != "6000.3.26f1")
            {
                EditorUtility.DisplayDialog("版本核对", "当前使用 " + Application.unityVersion +
                    "；团队规定 6000.3.26f1。请先确认版本，不自动转换项目。", "关闭");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(DemoScene) || AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScene) != null)
            {
                EditorUtility.DisplayDialog("保护已有作品", "独立测试场景已经存在，拒绝覆盖。\n" +
                    DemoScene + "\n如果想重新生成，请先通过Git备份并明确删除旧演示场景。", "关闭");
                return;
            }
            EnsureFolder(Root);
            EnsureFolder(Materials);
            EnsureFolder(Root + "/Scenes");
            AssetDatabase.Refresh();
            KZW4PuzzleDefinition puzzle = MakePuzzleDefinition();
            KZW4AudioCatalog catalog = MakeCatalog();
            MakeMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.34f, .31f, .29f);
            Block("地面_2004", new Vector3(0f, -.15f, 0f), new Vector3(20f, .3f, 40f), floor);
            Block("左侧旧墙", new Vector3(-9.8f, 2.4f, 0f), new Vector3(.4f, 4.8f, 37f), wall);
            Block("右侧旧墙", new Vector3(9.8f, 2.4f, 0f), new Vector3(.4f, 4.8f, 37f), wall);
            Block("舞台台面", new Vector3(0f, .5f, 11.5f), new Vector3(14.4f, 1f, 8f), wood);
            Block("戏台绛红幕布", new Vector3(0f, 2.6f, 15f), new Vector3(13f, 3.5f, .23f), stage);
            Block("戏台左柱", new Vector3(-5.2f, 2.1f, 10.4f), new Vector3(.40f, 4.2f, .45f), shadow);
            Block("戏台右柱", new Vector3(5.2f, 2.1f, 10.4f), new Vector3(.40f, 4.2f, .45f), shadow);
            Block("右侧梯_高", new Vector3(7.4f, .36f, 8.35f), new Vector3(1.2f, .72f, .8f), wood);
            Block("右侧梯_低", new Vector3(7.4f, .18f, 7.65f), new Vector3(1.2f, .36f, .7f), wood);

            Transform[] marks = new Transform[3];
            marks[0] = new GameObject("照片地标_舞台左柱").transform;
            marks[0].position = new Vector3(-5.2f, 2.9f, 10.4f);
            marks[1] = new GameObject("照片地标_舞台右柱").transform;
            marks[1].position = new Vector3(5.2f, 2.9f, 10.4f);
            marks[2] = new GameObject("照片地标_右侧台阶").transform;
            marks[2].position = new Vector3(7.4f, .52f, 7.65f);
            Transform photoVantage = new GameObject("1992布置照_参考机位_请勿移动").transform;
            photoVantage.position = new Vector3(0f, 1.62f, -4.8f);
            photoVantage.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            for (int n = 5; n <= 9; n++)
            {
                float x = (n - 7) * 1.24f;
                GameObject chair = new GameObject("第1排_" + n + "座");
                chair.transform.position = new Vector3(x, 0f, 5.8f);
                Material seatMat = n == 7 ? paper : wood;
                ChildBlock(chair.transform, "椅面", new Vector3(0f, .5f, 0f), new Vector3(.88f, .15f, .78f), seatMat);
                ChildBlock(chair.transform, "椅背", new Vector3(0f, 1.05f, -.3f), new Vector3(.88f, 1.02f, .13f), seatMat);
                if (n == 7)
                {
                    KZW4DemoInteractable seatItem = chair.AddComponent<KZW4DemoInteractable>();
                    seatItem.kind = KZW4DemoInteraction.SeatSeven;
                    seatItem.displayName = "一排七座 · 异常干净的空椅";
                }
            }

            AddDocument("留座戏宣传册", KZW4DemoInteraction.Ritual, -4.4f,
                "留座戏的规定：\n\n班中若有人横死，头七之前可为亡者留座唱戏。" +
                "第一排正中不得坐人，需摆放亡者生前的一件遗物。" +
                "最后三声锣响之前，不能擅自拿走。\n\n批注：活人占座，死人带走。");
            AddDocument("1992开戏前布置照片", KZW4DemoInteraction.Photo, -.1f,
                "照片摄于1992年开戏前，隐约可见舞台、第一排座椅与一件金属物。\n\n" +
                "由于透视和模糊，你还不能确定物件属于哪张椅子。\n\n" +
                "去观众厅按 F 举起照片，对齐舞台两根立柱和右侧台阶。");
            AddDocument("周明生场务物品记录", KZW4DemoInteraction.WorkRecord, 4.2f,
                "场务：周明生\n1992年7月遗物登记\n\n" +
                "钥匙串：用于戏台侧门与旧道具库；钥匙环边缘有一枚金属牌。\n" +
                "旧工牌：皮绳挂证件。\n铜哨：独立一件。\n\n" +
                "结合旧照片的轮廓判断留座戏摆放了什么。");
            AddPhone();

            GameObject sun = new GameObject("2004散射天光");
            Light sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(.85f, .84f, .78f);
            sunLight.intensity = .85f;
            sun.transform.rotation = Quaternion.Euler(45f, 25f, 0f);
            GameObject lamp = new GameObject("戏台聚光灯");
            lamp.transform.position = new Vector3(0f, 4.5f, 5f);
            lamp.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
            Light stageLamp = lamp.AddComponent<Light>();
            stageLamp.type = LightType.Spot;
            stageLamp.spotAngle = 95f;
            stageLamp.range = 24f;
            stageLamp.color = new Color(1f, .74f, .49f);
            stageLamp.intensity = 1.25f;

            GameObject group1992 = new GameObject("ECHO_1992_GROUP");
            Transform actress = MakeActor("沈芸_演出占位", new Vector3(5.8f, 1f, 11.0f), group1992.transform);
            Transform[] route = new Transform[3];
            Vector3[] waypoints = { new Vector3(6f, 0f, 8.0f), new Vector3(3.0f, 0f, 6.9f), new Vector3(0f, 0f, 6.7f) };
            for (int i = 0; i < route.Length; i++)
            {
                route[i] = new GameObject("沈芸路线_" + (i + 1)).transform;
                route[i].position = waypoints[i];
            }
            Transform[] heads = new Transform[4];
            for (int i = 0; i < heads.Length; i++)
            {
                float x = (i < 2 ? -1 : 1) * (1.65f + (i % 2) * 1.65f);
                Transform crowd = MakeActor("观众剪影_" + (i + 1), new Vector3(x, 0f, 3.5f), group1992.transform);
                heads[i] = crowd.Find("头部");
            }
            GameObject echoKey = Block("周明生钥匙_1992", new Vector3(0f, .72f, 5.8f),
                new Vector3(.2f, .11f, .27f), gold);
            echoKey.transform.SetParent(group1992.transform, true);
            group1992.SetActive(false);

            GameObject player = new GameObject("LML_W4_玩家_林砚");
            player.transform.position = new Vector3(0f, 0f, -12.7f);
            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = .3f;
            cc.center = new Vector3(0f, .9f, 0f);
            cc.stepOffset = .27f;
            GameObject eye = new GameObject("EyeCamera");
            eye.transform.SetParent(player.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            Camera camera = eye.AddComponent<Camera>();
            camera.fieldOfView = 72f;
            eye.AddComponent<AudioListener>();
            KZW4DemoPlayer controller = player.AddComponent<KZW4DemoPlayer>();
            controller.eyeCamera = camera;

            GameObject audio = new GameObject("LML_W4_AudioManager");
            KZW4AudioManager audioController = audio.AddComponent<KZW4AudioManager>();
            audioController.catalog = catalog;
            GameObject directorObject = new GameObject("LML_W4_DemoDirector");
            KZW4DemoDirector director = directorObject.AddComponent<KZW4DemoDirector>();
            director.definition = puzzle;
            director.player = controller;
            director.viewCamera = camera;
            director.photoVantage = photoVantage;
            director.photoLandmarks = marks;
            director.audioManager = audioController;
            director.echo1992Group = group1992;
            director.actress = actress;
            director.actressRoute = route;
            director.crowdHeads = heads;
            director.keyProp = echoKey;
            director.stageLight = stageLamp;
            controller.director = director;

            if (!EditorSceneManager.SaveScene(scene, DemoScene))
                throw new IOException("独立演示场景写入失败：" + DemoScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(DemoScene);
            EditorUtility.DisplayDialog("创建成功 · 不改共享场景",
                "已生成：" + DemoScene + "\n\n没有修改正式序章、第一章或Build Settings。" +
                "\nPlay 后：E阅读三份资料 → F照片对位 → G选钥匙 → 返回七号座E → 观看残响。" +
                "\nF10 调音量，M 试听年代音，R重置。", "开始测试");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Material MakeMaterial(string label, Color color)
        {
            string path = Materials + "/" + label + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Universal 3D (URP) shader missing");
            mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void MakeMaterials()
        {
            floor = MakeMaterial("LML_Floor", new Color(.47f, .39f, .30f));
            wall = MakeMaterial("LML_Wall", new Color(.64f, .57f, .43f));
            wood = MakeMaterial("LML_Wood", new Color(.35f, .23f, .16f));
            paper = MakeMaterial("LML_Paper", new Color(.86f, .77f, .57f));
            stage = MakeMaterial("LML_Stage", new Color(.49f, .17f, .19f));
            shadow = MakeMaterial("LML_Dark", new Color(.13f, .20f, .22f));
            gold = MakeMaterial("LML_Key", new Color(.90f, .64f, .32f));
        }

        private static GameObject Block(string name, Vector3 center, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = center;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject ChildBlock(Transform parent, string name,
            Vector3 localPosition, Vector3 size, Material mat)
        {
            GameObject go = Block(name, Vector3.zero, size, mat);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        private static void AddDocument(string title, KZW4DemoInteraction kind, float x, string body)
        {
            Block("资料桌_" + title, new Vector3(x, .44f, -9.3f), new Vector3(2.1f, .84f, 1.35f), wood);
            GameObject doc = Block(title, new Vector3(x, .94f, -9.3f),
                new Vector3(1.2f, .065f, .74f), paper);
            KZW4DemoInteractable item = doc.AddComponent<KZW4DemoInteractable>();
            item.kind = kind;
            item.displayName = title;
            item.contents = body;
        }

        private static void AddPhone()
        {
            Block("电话桌", new Vector3(-7.2f, .44f, -11.6f), new Vector3(1.8f, .84f, 1.4f), wood);
            GameObject phone = Block("办公室电话_音频试听", new Vector3(-7.2f, .99f, -11.6f),
                new Vector3(.55f, .15f, .28f), shadow);
            KZW4DemoInteractable item = phone.AddComponent<KZW4DemoInteractable>();
            item.kind = KZW4DemoInteraction.Phone;
            item.displayName = "电话 / 语音与字幕测试";
            item.contents = "电话响起。何文斌提醒：今晚只需登记数量，后台不要乱进。\n\n" +
                "这段对白即使没有配音文件，也会输出文字字幕。";
        }

        private static Transform MakeActor(string name, Vector3 at, Transform parent)
        {
            GameObject actor = new GameObject(name);
            actor.transform.SetParent(parent, true);
            actor.transform.position = at;
            ChildBlock(actor.transform, "身体", new Vector3(0f, .76f, 0f),
                new Vector3(.36f, 1.17f, .29f), stage);
            return MakeHead(actor.transform);
        }

        private static Transform MakeHead(Transform root)
        {
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "头部";
            head.transform.SetParent(root, false);
            head.transform.localPosition = new Vector3(0f, 1.46f, 0f);
            head.transform.localScale = new Vector3(.38f, .38f, .38f);
            head.GetComponent<Renderer>().sharedMaterial = shadow;
            return root;
        }

        private static KZW4PuzzleDefinition MakePuzzleDefinition()
        {
            KZW4PuzzleDefinition item = AssetDatabase.LoadAssetAtPath<KZW4PuzzleDefinition>(PuzzlePath);
            if (item != null) return item;
            item = ScriptableObject.CreateInstance<KZW4PuzzleDefinition>();
            AssetDatabase.CreateAsset(item, PuzzlePath);
            return item;
        }

        private static KZW4AudioCatalog MakeCatalog()
        {
            KZW4AudioCatalog item = AssetDatabase.LoadAssetAtPath<KZW4AudioCatalog>(AudioPath);
            if (item != null) return item;
            item = ScriptableObject.CreateInstance<KZW4AudioCatalog>();
            AddCue(item, "ambience.2004", KZW4AudioCategory.Ambience, "ambience_2004.wav", .65f, 0f);
            AddCue(item, "ambience.1992", KZW4AudioCategory.Ambience, "ambience_1992.wav", .75f, 0f);
            AddCue(item, "paper", KZW4AudioCategory.Sfx, "paper.wav", .75f, .1f);
            AddCue(item, "phone", KZW4AudioCategory.Sfx, "phone.wav", .8f, .2f);
            AddCue(item, "gong", KZW4AudioCategory.Sfx, "gong.wav", .9f, 2f);
            AddCue(item, "puzzle.ok", KZW4AudioCategory.Sfx, "puzzle_ok.wav", .8f, .1f);
            AddCue(item, "puzzle.fail", KZW4AudioCategory.Sfx, "puzzle_fail.wav", .7f, .1f);
            AddCue(item, "key", KZW4AudioCategory.Sfx, "key.wav", .8f, .2f);
            AddCue(item, "voice.phone", KZW4AudioCategory.Voice, null, 1f, 0f);
            AssetDatabase.CreateAsset(item, AudioPath);
            return item;
        }

        private static void AddCue(KZW4AudioCatalog library, string id, KZW4AudioCategory category,
            string clipName, float volume, float cooldown)
        {
            KZW4AudioCatalog.Entry e = new KZW4AudioCatalog.Entry();
            e.id = id;
            e.category = category;
            e.volume = volume;
            e.cooldownSeconds = cooldown;
            if (!string.IsNullOrEmpty(clipName))
                e.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Placeholder/" + clipName);
            library.entries.Add(e);
        }
    }
}
#endif
