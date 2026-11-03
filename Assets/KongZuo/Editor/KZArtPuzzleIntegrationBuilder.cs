#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using KongZuo.LMLWeek4;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KongZuo.EditorTools
{
    /// <summary>Copies the playable week-four puzzle, then fits it to the art team's P2 opera house.</summary>
    public static class KZArtPuzzleIntegrationBuilder
    {
        private const string SourceScene = "Assets/KongZuo/LMLWeek4/Scenes/LML_Week4_Demo.unity";
        private const string TargetScene = "Assets/KongZuo/Scenes/KZ_ArtPuzzle_Integration.unity";
        private const string ArtPrefab = "Assets/KongZuo/ZhaiModel/KongZuo_OperaHouse_P2/Generated/PF_KZ_P2_OperaHouse_All.prefab";
        private const string PuzzleAsset = "Assets/KongZuo/LMLWeek4/KZW4_RelicPuzzle.asset";
        private const string AudioAsset = "Assets/KongZuo/LMLWeek4/KZW4_AudioCatalog.asset";
        private const string ArtName = "KZ_ArtPuzzle_OperaHouse_P2";
        private const string CollisionName = "KZ_ArtPuzzle_Collision";
        private static readonly Regex SeatPattern = new Regex(@"^R\d{2}_Seat\d{2}(?:_KEY_RESERVED)?$", RegexOptions.Compiled);

        [MenuItem("空座/联调/生成美术解谜整合场景")]
        public static void GenerateFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("请先退出 Play", "退出 Play 模式后再生成联调场景。", "知道了");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try { Generate(); }
            catch (Exception error) { Debug.LogError("[空座联调] 生成失败：" + error); }
        }

        // Unity -executeMethod entry for validation in a separate project copy.
        public static void GenerateBatch() { Generate(); }

        [MenuItem("空座/联调/打开美术解谜测试场景")]
        public static void OpenFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("请先退出 Play", "退出 Play 模式后再打开联调场景。", "知道了");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) == null)
            {
                GenerateFromMenu();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) == null) return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
        }

        [InitializeOnLoad]
        private static class FirstImport
        {
            static FirstImport() { EditorApplication.delayCall += TryGenerate; }

            private static void TryGenerate()
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) != null) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    EditorApplication.delayCall += TryGenerate;
                    return;
                }
                try { Generate(); }
                catch (Exception error) { Debug.LogError("[空座联调] 自动生成未完成。可从 空座 → 联调 菜单重试。\n" + error); }
            }
        }

        private static void Generate()
        {
            if (Application.unityVersion != "6000.3.26f1")
                Debug.LogWarning("[空座联调] 当前 Unity " + Application.unityVersion + "；原工程版本为 6000.3.26f1。");

            Require<SceneAsset>(SourceScene, "第四周可玩谜题场景");
            GameObject artPrefab = Require<GameObject>(ArtPrefab, "长明戏楼完整美术 Prefab");
            KZW4PuzzleDefinition puzzle = Require<KZW4PuzzleDefinition>(PuzzleAsset, "遗物谜题配置");
            KZW4AudioCatalog audio = Require<KZW4AudioCatalog>(AudioAsset, "第四周音频目录");
            if (!puzzle.IsValid()) throw new InvalidOperationException("遗物谜题配置不完整：" + PuzzleAsset);

            bool created = AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) == null;
            if (created && !AssetDatabase.CopyAsset(SourceScene, TargetScene))
                throw new IOException("Unity 无法复制解谜场景到 " + TargetScene);

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(TargetScene);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Additive);
            bool saved = false;
            try
            {
                SceneManager.SetActiveScene(scene);
                Integrate(scene, artPrefab, puzzle, audio);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("无法保存 " + TargetScene);
                saved = true;
                Debug.Log("[空座联调] KZ_ART_PUZZLE_READY: " + TargetScene);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded && previous != scene)
                    SceneManager.SetActiveScene(previous);
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                if (created && !saved) AssetDatabase.DeleteAsset(TargetScene);
            }
        }

        private static T Require<T>(string path, string label) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new FileNotFoundException(label + "缺失：" + path);
            return asset;
        }

        private static void Integrate(Scene scene, GameObject prefab, KZW4PuzzleDefinition puzzle, KZW4AudioCatalog audio)
        {
            GameObject playerObject = RequireRoot(scene, "LML_W4_玩家_林砚");
            KZW4DemoPlayer player = RequireComponent<KZW4DemoPlayer>(playerObject);
            KZW4DemoDirector director = RequireComponent<KZW4DemoDirector>(RequireRoot(scene, "LML_W4_DemoDirector"));
            KZW4AudioManager audioManager = RequireComponent<KZW4AudioManager>(RequireRoot(scene, "LML_W4_AudioManager"));
            KZW4DemoInteractable oldSeat = RequireComponent<KZW4DemoInteractable>(RequireRoot(scene, "第1排_7座"));
            if (director.photoVantage == null || director.photoLandmarks == null || director.photoLandmarks.Length != 3)
                throw new InvalidOperationException("第四周场景的照片机位或三个地标引用缺失。");
            if (director.actressRoute == null || director.actressRoute.Length < 3 || director.keyProp == null)
                throw new InvalidOperationException("第四周残响路径或钥匙引用缺失。");
            if (director.echo1992Group == null || director.actress == null || director.stageLight == null ||
                director.crowdHeads == null || director.crowdHeads.Length == 0)
                throw new InvalidOperationException("第四周残响角色、灯光或年代状态引用缺失。");
            foreach (Transform waypoint in director.actressRoute)
                if (waypoint == null) throw new InvalidOperationException("第四周残响路线包含空锚点。");
            foreach (Transform head in director.crowdHeads)
                if (head == null) throw new InvalidOperationException("第四周观众剪影包含空引用。");

            director.definition = puzzle;
            director.player = player;
            director.viewCamera = player.eyeCamera;
            director.audioManager = audioManager;
            audioManager.catalog = audio;
            player.director = director;
            player.transform.position = new Vector3(0f, 0f, -11.8f);
            player.transform.rotation = Quaternion.identity;
            if (player.eyeCamera != null) player.eyeCamera.transform.localRotation = Quaternion.identity;

            GameObject art = FindRoot(scene, ArtName);
            if (art == null)
            {
                art = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (art == null) throw new InvalidOperationException("戏楼 Prefab 无法实例化。");
                art.name = ArtName;
            }
            art.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            art.transform.localScale = Vector3.one;

            // Keep the tested puzzle objects and their references; only retire the old visible building and five chairs.
            HideGreybox(scene, "地面_2004", false);
            HideGreybox(scene, "左侧旧墙", false);
            HideGreybox(scene, "右侧旧墙", false);
            HideGreybox(scene, "舞台台面", false);
            HideGreybox(scene, "戏台绛红幕布", false);
            HideGreybox(scene, "戏台左柱", true);
            HideGreybox(scene, "戏台右柱", true);
            HideGreybox(scene, "右侧梯_高", true);
            HideGreybox(scene, "右侧梯_低", true);
            for (int number = 5; number <= 9; number++)
                HideGreybox(scene, "第1排_" + number + "座", true);
            GameObject stage = RequireRoot(scene, "舞台台面");
            stage.transform.position = new Vector3(0f, .4f, 12f);
            stage.transform.localScale = new Vector3(15.6f, .8f, 7.1f);

            int seatCount = 0;
            Transform artSeatSeven = null;
            foreach (Transform item in art.GetComponentsInChildren<Transform>(true))
            {
                if (!SeatPattern.IsMatch(item.name)) continue;
                seatCount++;
                BoxCollider collider = item.GetComponent<BoxCollider>();
                if (collider == null) collider = item.gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = false;
                collider.center = new Vector3(0f, .75f, 0f);
                collider.size = new Vector3(.95f, 1.5f, .82f);
                if (item.name == "R01_Seat07_KEY_RESERVED") artSeatSeven = item;
            }
            if (seatCount != 65 || artSeatSeven == null)
                throw new InvalidOperationException("美术座位结构不完整：找到 " + seatCount + " 席；缺少第一排七座。");
            KZW4DemoInteractable seatAction = artSeatSeven.GetComponent<KZW4DemoInteractable>();
            if (seatAction == null) seatAction = artSeatSeven.gameObject.AddComponent<KZW4DemoInteractable>();
            seatAction.kind = KZW4DemoInteraction.SeatSeven;
            seatAction.displayName = oldSeat.displayName;
            seatAction.contents = oldSeat.contents;

            director.photoVantage.position = new Vector3(0f, 1.62f, -4.8f);
            director.photoVantage.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            director.photoLandmarks[0].position = new Vector3(-7.5f, 2.9f, 14f);
            director.photoLandmarks[1].position = new Vector3(7.5f, 2.9f, 14f);
            director.photoLandmarks[2].position = new Vector3(8f, .52f, 7.65f);
            director.actressRoute[director.actressRoute.Length - 1].position = new Vector3(0f, 0f, 6.8f);
            director.keyProp.transform.position = new Vector3(0f, .72f, 6.8f);

            GameObject collision = FindRoot(scene, CollisionName);
            if (collision == null) collision = NewRoot(scene, CollisionName);
            EnsureBox(collision.transform, "入口西墙", new Vector3(-5.75f, 2.5f, -15.1f), new Vector3(8.5f, 5f, .4f));
            EnsureBox(collision.transform, "入口东墙", new Vector3(5.75f, 2.5f, -15.1f), new Vector3(8.5f, 5f, .4f));
            EnsureBox(collision.transform, "后墙", new Vector3(0f, 2.5f, 18.6f), new Vector3(20f, 5f, .4f));
            EnsureBox(collision.transform, "外院地面", new Vector3(0f, -.25f, -22.5f), new Vector3(31f, .5f, 15f));
            EnsureBox(collision.transform, "外院西边界", new Vector3(-15.4f, 1.5f, -22.5f), new Vector3(.4f, 3f, 15f));
            EnsureBox(collision.transform, "外院东边界", new Vector3(15.4f, 1.5f, -22.5f), new Vector3(.4f, 3f, 15f));
            EnsureBox(collision.transform, "外院南边界", new Vector3(0f, 1.5f, -29.5f), new Vector3(31f, 3f, .4f));
            EnsureBox(collision.transform, "舞台西柱", new Vector3(-7.5f, 2.2f, 14f), new Vector3(.6f, 4.4f, .6f));
            EnsureBox(collision.transform, "舞台东柱", new Vector3(7.5f, 2.2f, 14f), new Vector3(.6f, 4.4f, .6f));
            foreach (float side in new[] { -1f, 1f })
            {
                string prefix = side < 0f ? "西侧" : "东侧";
                EnsureBox(collision.transform, prefix + "下台阶", new Vector3(side * 8f, .12f, 7.4f), new Vector3(1.5f, .24f, .6f));
                EnsureBox(collision.transform, prefix + "中台阶", new Vector3(side * 8f, .23f, 8.0f), new Vector3(1.5f, .46f, .6f));
                EnsureBox(collision.transform, prefix + "上台阶", new Vector3(side * 8f, .33f, 8.6f), new Vector3(1.5f, .66f, .6f));
            }

            ValidateScene(scene, director, player, audioManager, art, prefab);
            Debug.Log("[空座联调] 已连接 65 席碰撞和七号座交互、照片三地标、三份证据、遗物判断、残响与音频。");
        }

        private static void ValidateScene(Scene scene, KZW4DemoDirector director, KZW4DemoPlayer player,
            KZW4AudioManager audio, GameObject art, GameObject sourcePrefab)
        {
            if (Count<KZW4DemoPlayer>(scene) != 1 || Count<CharacterController>(scene) != 1 ||
                Count<Camera>(scene) != 1 || Count<AudioListener>(scene) != 1 ||
                Count<KZW4DemoDirector>(scene) != 1 || Count<KZW4AudioManager>(scene) != 1)
                throw new InvalidOperationException("玩家、摄像机、AudioListener、谜题 Director 或音频管理器数量不为 1。");
            if (player.eyeCamera == null || director.viewCamera != player.eyeCamera || director.audioManager != audio)
                throw new InvalidOperationException("玩家视角、谜题或音频引用未正确连接。");
            if (PrefabUtility.GetCorrespondingObjectFromSource(art) != sourcePrefab)
                Debug.LogWarning("[空座联调] 戏楼实例与原 Prefab 的来源关联需要在 Inspector 中复查。");
            int clues = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (KZW4DemoInteractable item in root.GetComponentsInChildren<KZW4DemoInteractable>(true))
                    if (item.kind == KZW4DemoInteraction.Ritual || item.kind == KZW4DemoInteraction.Photo ||
                        item.kind == KZW4DemoInteraction.WorkRecord) clues++;
            if (clues != 3) throw new InvalidOperationException("应有三份可调查证据，实际 " + clues + " 份。");
            int missingScripts = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject);
            if (missingScripts > 0) Debug.LogWarning("[空座联调] 新场景仍有 " + missingScripts + " 个 Missing Script，请在 Inspector 检查。");
            if (director.definition == null || audio.catalog == null)
                throw new InvalidOperationException("遗物配置或音频目录引用丢失。");
            foreach (string cue in new[] { "ambience.2004", "ambience.1992", "paper", "phone", "gong", "puzzle.ok", "puzzle.fail", "key" })
            {
                KZW4AudioCatalog.Entry entry = audio.catalog.Find(cue);
                if (entry == null || entry.clip == null)
                    Debug.LogWarning("[空座联调] 音频事件缺少实际音频文件：" + cue);
            }
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component != null && (component.GetType().Name == "KZV03Director" ||
                        component.GetType().Name == "KZStoryDirector" || component.GetType().Name == "KZHud"))
                        throw new InvalidOperationException("联调场景包含冲突的旧 Director/UI：" + component.GetType().Name);
        }

        private static void HideGreybox(Scene scene, string name, bool disableCollision)
        {
            GameObject root = RequireRoot(scene, name);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            if (disableCollision)
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }

        private static GameObject RequireRoot(Scene scene, string name)
        {
            GameObject root = FindRoot(scene, name);
            if (root == null) throw new InvalidOperationException("源解谜场景缺少对象：" + name);
            return root;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        private static T RequireComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null) throw new InvalidOperationException(go.name + " 缺少 " + typeof(T).Name);
            return component;
        }

        private static GameObject NewRoot(Scene scene, string name)
        {
            GameObject go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        private static void EnsureBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(parent, false);
                child = go.transform;
            }
            child.position = center;
            child.rotation = Quaternion.identity;
            child.localScale = Vector3.one;
            BoxCollider box = child.GetComponent<BoxCollider>();
            if (box == null) box = child.gameObject.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = size;
            box.isTrigger = false;
        }

        private static int Count<T>(Scene scene) where T : Component
        {
            int result = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                result += root.GetComponentsInChildren<T>(true).Length;
            return result;
        }
    }
}
#endif
