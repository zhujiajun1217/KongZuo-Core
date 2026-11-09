#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using KongZuo;

// Repeatable adaptation of the playable scenes to the authored P0 V0.3 building.
public static class KZMergedArtSetup
{
    private const string ArtRoot = "Assets/KongZuo/Art/KZ_OperaHouse_P0_V03";
    private const string PrefabPath = ArtRoot + "/Generated/Prefabs/KZ_OperaHouse_P0_V03_FULL.prefab";
    private const string ArtInstanceName = "KZ_OperaHouse_P0_V03_From11_Integrated";
    private const string FirstFloorCollisionName = "99_PhysicalColliders_Floor01__do_not_disable";
    private const string AdaptedMarker = "KZ_Scene_Adapted_Art02_20261106_FullPrefab";
    private const string PreviousAdaptedMarker = "KZ_Scene_Adapted_From11_20261102";
    private const string EarlierAdaptedMarker = "KZ_Scene_Adapted_Art02_20261106";
    private const string PreviousBeamMarker = "KZ_Scene_Adapted_Art02_20261106_NoFirstFloorBeams";
    private const string PreviousBeamMarkerV2 = "KZ_Scene_Adapted_Art02_20261106_NoFirstFloorBeamsV2";
    private static readonly long[] InteriorAdjustedTransformIds =
    {
        1655034640484464130L,
        2617335912776364710L,
        4088696844770859463L,
        8103686996711970863L,
        8791354829395931536L
    };
    private static readonly float[] InteriorAdjustedLocalY = { 10f, 9f, 9f, 10f, 10f };
    private static readonly string[] LegacyArtInstanceNames =
    {
        "KZ_OperaHouse_P2_Integrated_Visuals",
        "KZ_OperaHouse_P2__ART_ONLY__SAFE_TO_DELETE",
        "PF_KZ_P2_OperaHouse_All",
        "KZ_OperaHouse_P0_V03_Integrated_Visuals"
    };

    [MenuItem("Tools/空座/整合/将戏楼美术接入序章和第一章")]
    public static void Integrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before integrating the updated Art02 FULL prefab.");
        if (!Application.isBatchMode && HasUnsavedLoadedScenes() &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.Refresh();
        GameObject prefab = BuildLatestPrefab();
        if (prefab == null)
            throw new InvalidOperationException("Latest art prefab was not generated: " + PrefabPath);

        IntegrateScene("Assets/KongZuo/Scenes/01_Prologue.unity", prefab);
        IntegrateScene("Assets/KongZuo/Scenes/02_Chapter01.unity", prefab);
        AssetDatabase.SaveAssets();
        Debug.Log("KZ_ART_INTEGRATION_OK: playable prologue and chapter 1 use the latest Art02 FULL prefab.");
    }

    private static GameObject BuildLatestPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            throw new FileNotFoundException("Latest art prefab is missing: " + PrefabPath);
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    private static bool HasUnsavedLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.IsValid() && scene.isLoaded && scene.isDirty) return true;
        }
        return false;
    }

    [MenuItem("Tools/空座/整合/检查序章与第一章")]
    public static void ValidatePlayableScenes()
    {
        string[] paths = {
            "Assets/KongZuo/Scenes/01_Prologue.unity",
            "Assets/KongZuo/Scenes/02_Chapter01.unity"
        };
        foreach (string path in paths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int players = 0, directors = 0, huds = 0, cameras = 0, listeners = 0;
            int missingScripts = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                players += root.GetComponentsInChildren<KZFirstPersonPlayer>(true).Length;
                directors += root.GetComponentsInChildren<KZStoryDirector>(true).Length;
                huds += root.GetComponentsInChildren<KZHud>(true).Length;
                cameras += root.GetComponentsInChildren<Camera>(true).Length;
                listeners += root.GetComponentsInChildren<AudioListener>(true).Length;
                foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(part.gameObject);
            }
            GameObject art = FindRoot(scene, ArtInstanceName);
            if (art == null) throw new InvalidOperationException(path + ": latest art is missing.");
            KZStoryDirector director = FindDirector(scene);
            if (director == null) throw new InvalidOperationException(path + ": story director is missing.");
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(art) != PrefabPath)
                throw new InvalidOperationException(path + ": final From11 art prefab is not in use.");
            if (FindChildRecursive(art.transform, "08_AudienceSeats_9x13__no_centre_aisle") == null)
                throw new InvalidOperationException(path + ": final audience seating layout is missing.");
            ValidateInteriorModelLayout(art);
            Transform firstFloor = FindChildRecursive(art.transform, FirstFloorCollisionName);
            Transform stage = FindChildRecursive(art.transform, "04_Stage");
            Transform stair = FindChildRecursive(art.transform, "H_StageStair_Right");
            if (firstFloor == null || !firstFloor.gameObject.activeInHierarchy ||
                stage == null || !stage.gameObject.activeInHierarchy ||
                stair == null || !stair.gameObject.activeInHierarchy)
                throw new InvalidOperationException(path + ": the stage or latest stair model is missing/disabled.");
            if (players != 1 || directors != 1 || huds != 1 || cameras != 1 || listeners != 1)
                throw new InvalidOperationException(path + ": duplicate/missing player, director, HUD or camera.");
            if (missingScripts != 0)
                throw new InvalidOperationException(path + ": Missing Script count=" + missingScripts);
            if (FindRoot(scene, "KZ_OperaHouse_P2_Integrated_Visuals") != null)
                throw new InvalidOperationException(path + ": old P2 building remains active.");
            GameObject audience = FindRoot(scene, "观众厅出入检测");
            if (director != null && director.isPrologue &&
                (audience == null || audience.GetComponent<BoxCollider>() == null ||
                 !audience.GetComponent<BoxCollider>().isTrigger))
                throw new InvalidOperationException(path + ": auditorium zone is missing.");
            if (!director.isPrologue &&
                (director.photoAlignment == null || director.photoAlignment.referenceView == null ||
                 director.photoAlignment.landmarks == null ||
                 director.photoAlignment.landmarks.Length < 3 ||
                 FindRoot(scene, "周明生场务物品记录") == null ||
                 FindRoot(scene, "七号座遗物推理记录") == null))
                throw new InvalidOperationException(path + ": chapter-one clue/photo calibration is incomplete.");
            Debug.Log("[空座检查] " + path + ": latest art prefab, stage, stair and first-floor collision present; no Missing Scripts.");
        }
    }

    // Complete the import once, after Unity has finished bringing in the OBJ assets.
    [InitializeOnLoad]
    private static class FirstOpenIntegration
    {
        private const string SessionKey = "KZ_Art_Integration_FullPrefab_Attempted_This_Session";

        static FirstOpenIntegration()
        {
            EditorApplication.delayCall += TryIntegrate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += TryIntegrate;
        }

        private static void TryIntegrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[空座] 当前正在播放，退出 Play 模式后运行 Tools/空座/整合 菜单以应用一楼横梁调整。");
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryIntegrate;
                return;
            }
            if (SessionState.GetBool(SessionKey, false)) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (loaded.IsValid() && loaded.isDirty)
                {
                    Debug.LogWarning("[空座] 当前场景有未保存编辑，跳过自动适配。请保存后运行 Tools/空座/整合 菜单。");
                    return;
                }
            }
            SessionState.SetBool(SessionKey, true);

            string scenePath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Assets/KongZuo/Scenes/01_Prologue.unity");
            if (!File.Exists(scenePath)) return;
            string sceneText = File.ReadAllText(scenePath);
            if (sceneText.Contains(AdaptedMarker) && !ContainsLegacyArtName(sceneText)) return;
            try
            {
                Integrate();
            }
            catch (Exception error)
            {
                Debug.LogError("空座美术自动整合未完成。可用 Tools/空座/整合 菜单重试。\n" + error);
            }
        }
    }

    private static void IntegrateScene(string path, GameObject prefab)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        KZStoryDirector director = FindDirector(scene);
        if (director == null)
            throw new InvalidOperationException(path + ": playable scene is missing its story director.");
        GameObject art = null;
        int hiddenObjects = 0;
        int removedLegacyArt = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == ArtInstanceName)
            {
                string currentPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                if (currentPrefabPath == PrefabPath)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    removedLegacyArt++;
                }
                continue;
            }
            if (IsLegacyArtInstance(root.name))
            {
                UnityEngine.Object.DestroyImmediate(root);
                removedLegacyArt++;
                continue;
            }
        }

        art = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        if (art == null) throw new InvalidOperationException("Could not instantiate art prefab in " + path);
        art.name = ArtInstanceName;
        art.transform.position = Vector3.zero;
        art.transform.rotation = Quaternion.identity;
        art.transform.localScale = Vector3.one;
        ApplyInteriorModelLayout(art);
        if (art == null) throw new InvalidOperationException("Integrated art instance was not found in " + path);
        Transform firstFloorCollision = FindChildRecursive(art.transform, FirstFloorCollisionName);
        if (firstFloorCollision == null)
            throw new InvalidOperationException("The authored first-floor collision mesh is missing.");
        firstFloorCollision.gameObject.SetActive(true);

        Transform entrance = RequireAnchor(art, "A_MainEntrance");
        Transform phone = RequireAnchor(art, "B_OfficeTelephone");
        Transform register = RequireAnchor(art, "C_TicketRegister");
        Transform seatAnchor = RequireAnchor(art, "D_Row01Seat07");
        Transform poster = RequireAnchor(art, "E_FoyerPoster");
        Transform stage = RequireAnchor(art, "P0_StageCentre");
        Transform stairLeft = RequireAnchor(art, "H_StageStair_Left");
        Transform stairRight = RequireAnchor(art, "H_StageStair_Right");

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!IsGreyboxVisual(root.name)) continue;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.enabled) { renderer.enabled = false; hiddenObjects++; }
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }
        MoveRoot(scene, "长明戏楼旧木门", entrance.position + Vector3.up * 1.08f);
        MoveRoot(scene, "办公室电话", phone.position);
        MoveRoot(scene, "1992年座位登记表", register.position);
        MoveRoot(scene, "戏楼旧海报", poster.position);
        MoveRoot(scene, "重看资产清点表", entrance.position + new Vector3(-2.0f, 1f, -2.2f));
        MoveRoot(scene, "留座戏旧宣传册", entrance.position + new Vector3(-1.8f, 1.0f, 2.7f));
        MoveRoot(scene, "周明生旧合照", entrance.position + new Vector3(1.8f, 1.0f, 2.7f));
        MoveRoot(scene, "残缺的旧票根", register.position + new Vector3(-.35f, 0f, .15f));
        MoveRoot(scene, "一排座次示意图", register.position + new Vector3(.45f, .22f, .15f));
        MoveRoot(scene, "新出现的旧节目单", seatAnchor.position + new Vector3(.14f, .10f, .23f));
        MoveRoot(scene, "一排七座的人影_异常",
            new Vector3(seatAnchor.position.x, 0f, seatAnchor.position.z));
        MoveRoot(scene, "后台封闭门", stage.position + new Vector3(-4.8f, .9f, 4.4f));

        if (director.player != null && director.isPrologue && !Application.isPlaying)
            director.player.transform.position = entrance.position + new Vector3(0f, 0f, -2.3f);
        if (director.stageLamp != null)
            director.stageLamp.transform.position = stage.position + new Vector3(0f, 4.0f, -4.0f);
        TransferSeatInteraction(scene, art, seatAnchor);
        RepositionEcho(director, seatAnchor, stage, stairRight);
        RepositionTriggers(scene, seatAnchor);
        if (!director.isPrologue)
        {
            ConfigurePhotoPuzzle(scene, director, stage, stairRight);
            EnsureEvidence(scene, "周明生场务物品记录", KZActionKind.WorkRecord,
                "场务物品核对：钥匙串由周明生保管。旧工牌归办公室，铜哨归巡场人员。" +
                "\n\n那天留座戏结束后，钥匙没有按惯例交回。",
                register.position + new Vector3(-.55f, .08f, .5f));
            EnsureEvidence(scene, "七号座遗物推理记录", KZActionKind.RelicDecision,
                "把旧照片和场务物品记录放在一起核对。",
                register.position + new Vector3(.55f, .08f, .5f));
        }
        GameObject previousMarker = FindRoot(scene, PreviousAdaptedMarker);
        if (previousMarker != null) UnityEngine.Object.DestroyImmediate(previousMarker);
        GameObject earlierMarker = FindRoot(scene, EarlierAdaptedMarker);
        if (earlierMarker != null) UnityEngine.Object.DestroyImmediate(earlierMarker);
        GameObject previousBeamMarker = FindRoot(scene, PreviousBeamMarker);
        if (previousBeamMarker != null) UnityEngine.Object.DestroyImmediate(previousBeamMarker);
        GameObject previousBeamMarkerV2 = FindRoot(scene, PreviousBeamMarkerV2);
        if (previousBeamMarkerV2 != null) UnityEngine.Object.DestroyImmediate(previousBeamMarkerV2);
        if (FindRoot(scene, AdaptedMarker) == null)
            new GameObject(AdaptedMarker);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + path);
        Debug.Log("KZ_ART_SCENE_OK: " + path + "; hidden greybox renderers=" + hiddenObjects +
            "; removed legacy art=" + removedLegacyArt);
    }

    private static void ApplyInteriorModelLayout(GameObject art)
    {
        string prefabGuid = AssetDatabase.AssetPathToGUID(PrefabPath);
        bool[] applied = new bool[InteriorAdjustedTransformIds.Length];
        foreach (Transform instanceTransform in art.GetComponentsInChildren<Transform>(true))
        {
            UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(instanceTransform);
            if (source == null ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string sourceGuid, out long sourceId) ||
                sourceGuid != prefabGuid)
                continue;

            for (int i = 0; i < InteriorAdjustedTransformIds.Length; i++)
            {
                if (applied[i] || sourceId != InteriorAdjustedTransformIds[i]) continue;
                Vector3 localPosition = instanceTransform.localPosition;
                localPosition.y = InteriorAdjustedLocalY[i];
                instanceTransform.localPosition = localPosition;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instanceTransform);
                applied[i] = true;
            }
        }

        for (int i = 0; i < applied.Length; i++)
            if (!applied[i])
                throw new InvalidOperationException("Could not apply Interior layout to art transform " +
                    InteriorAdjustedTransformIds[i] + ".");
    }

    private static void ValidateInteriorModelLayout(GameObject art)
    {
        string prefabGuid = AssetDatabase.AssetPathToGUID(PrefabPath);
        bool[] validated = new bool[InteriorAdjustedTransformIds.Length];
        foreach (Transform instanceTransform in art.GetComponentsInChildren<Transform>(true))
        {
            UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(instanceTransform);
            if (source == null ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string sourceGuid, out long sourceId) ||
                sourceGuid != prefabGuid)
                continue;

            for (int i = 0; i < InteriorAdjustedTransformIds.Length; i++)
            {
                if (validated[i] || sourceId != InteriorAdjustedTransformIds[i]) continue;
                if (Mathf.Abs(instanceTransform.localPosition.y - InteriorAdjustedLocalY[i]) > 0.001f)
                    throw new InvalidOperationException("Prologue art does not match the Interior layout at transform " +
                        InteriorAdjustedTransformIds[i] + ".");
                validated[i] = true;
            }
        }

        for (int i = 0; i < validated.Length; i++)
            if (!validated[i])
                throw new InvalidOperationException("Could not validate Interior layout at art transform " +
                    InteriorAdjustedTransformIds[i] + ".");
    }

    private static bool ContainsLegacyArtName(string text)
    {
        foreach (string name in LegacyArtInstanceNames)
            if (text.Contains(name)) return true;
        return false;
    }

    private static bool IsLegacyArtInstance(string name)
    {
        foreach (string legacyName in LegacyArtInstanceNames)
            if (name == legacyName || name.StartsWith(legacyName, StringComparison.Ordinal)) return true;
        return false;
    }

    private static Transform RequireAnchor(GameObject art, string name)
    {
        Transform found = FindChildRecursive(art.transform, name);
        if (found == null) throw new InvalidOperationException("Art anchor missing: " + name);
        return found;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    private static void MoveRoot(Scene scene, string name, Vector3 position)
    {
        GameObject root = FindRoot(scene, name);
        if (root != null) root.transform.position = position;
    }

    private static KZStoryDirector FindDirector(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            KZStoryDirector director = root.GetComponent<KZStoryDirector>();
            if (director != null) return director;
        }
        return null;
    }

    private static void TransferSeatInteraction(Scene scene, GameObject art, Transform seatAnchor)
    {
        GameObject oldSeat = FindRoot(scene, "第1排_07座");
        Transform newSeat = FindChildRecursive(art.transform, "R01_S07__P0_KEY_Seat");
        if (oldSeat == null || newSeat == null)
            throw new InvalidOperationException("Old or authored first-row seat 07 is missing.");
        KZInteractable source = oldSeat.GetComponent<KZInteractable>();
        if (source == null) throw new InvalidOperationException("Seat 07 interaction is missing.");
        KZInteractable target = newSeat.GetComponent<KZInteractable>();
        if (target == null) target = newSeat.gameObject.AddComponent<KZInteractable>();
        target.action = source.action;
        target.displayName = source.displayName;
        target.body = source.body;
        foreach (Collider collider in oldSeat.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        oldSeat.transform.position = new Vector3(seatAnchor.position.x, 0f, seatAnchor.position.z);
    }

    private static void RepositionEcho(KZStoryDirector director, Transform seat,
        Transform stage, Transform rightStair)
    {
        Vector3 seatPos = new Vector3(seat.position.x, 0f, seat.position.z);
        if (director.anomalyShadow != null)
        {
            director.anomalyShadow.transform.position = seatPos;
            foreach (Collider collider in director.anomalyShadow.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }
        if (director.programProp != null)
            director.programProp.transform.position = seatPos + new Vector3(.14f, .69f, .23f);
        if (director.echoKey != null)
            director.echoKey.transform.position = seatPos + new Vector3(0f, .72f, .14f);
        if (director.echoRoute != null && director.echoRoute.Length >= 7)
        {
            Vector3 p = stage.position;
            Vector3 stair = rightStair.position;
            Vector3[] route = {
                p + new Vector3(5.9f, 0f, 0f),
                p + new Vector3(7.3f, 0f, -1.7f),
                stair + new Vector3(0f, .6f, .5f),
                stair + new Vector3(0f, .2f, -.5f),
                stair + new Vector3(0f, 0f, -1.1f),
                seatPos + new Vector3(.8f, 0f, .75f),
                seatPos
            };
            for (int i = 0; i < route.Length; i++)
                if (director.echoRoute[i] != null) director.echoRoute[i].position = route[i];
        }
        if (director.echoPlayerView != null)
            director.echoPlayerView.position = seatPos + new Vector3(0f, 0f, -13f);
        if (director.crowdHeads != null)
            for (int i = 0; i < director.crowdHeads.Length; i++)
                if (director.crowdHeads[i] != null)
                    director.crowdHeads[i].parent.position = seatPos +
                        new Vector3((i < 6 ? i - 6 : i - 5) * 1.06f, 0f, 0f);
    }

    private static void RepositionTriggers(Scene scene, Transform seat)
    {
        GameObject audience = FindRoot(scene, "观众厅出入检测");
        if (audience != null)
        {
            audience.transform.position = new Vector3(0f, 1.4f, -2.75f);
            BoxCollider box = audience.GetComponent<BoxCollider>();
            if (box != null) { box.size = new Vector3(18f, 3.4f, 15f); box.isTrigger = true; }
        }
        GameObject approach = FindRoot(scene, "靠近第一排的人影消失");
        if (approach != null)
        {
            approach.transform.position = new Vector3(
                seat.position.x, 1.4f, seat.position.z + .95f);
            BoxCollider box = approach.GetComponent<BoxCollider>();
            if (box != null) { box.size = new Vector3(15f, 3.4f, 1.9f); box.isTrigger = true; }
        }
    }

    private static void ConfigurePhotoPuzzle(Scene scene, KZStoryDirector director,
        Transform stage, Transform stairRight)
    {
        KZPhotoAlignmentPuzzle photo = director.photoAlignment;
        if (photo == null) photo = director.GetComponent<KZPhotoAlignmentPuzzle>();
        if (photo == null) photo = director.gameObject.AddComponent<KZPhotoAlignmentPuzzle>();
        Vector3 vantage = new Vector3(0f, 1.62f, -10.7f);
        Transform reference = EnsurePoint(scene, "KZ_Photo_ReferenceView", vantage);
        reference.rotation = Quaternion.LookRotation(
            stage.position + Vector3.up * 2f - vantage, Vector3.up);
        Transform left = EnsurePoint(scene, "KZ_Photo_LeftColumn",
            stage.position + new Vector3(-6.1f, 2.25f, -2.8f));
        Transform right = EnsurePoint(scene, "KZ_Photo_RightColumn",
            stage.position + new Vector3(6.1f, 2.25f, -2.8f));
        Transform stair = EnsurePoint(scene, "KZ_Photo_RightStageStair",
            stairRight.position + Vector3.up * .8f);
        photo.referenceView = reference;
        photo.landmarks = new[] { left, right, stair };
        photo.viewCamera = director.player == null ? null :
            director.player.GetComponentInChildren<Camera>(true);
        director.photoAlignment = photo;
        if (photo.viewCamera == null)
            throw new InvalidOperationException("Photo calibration camera is missing.");
        foreach (Transform marker in photo.landmarks)
        {
            Vector3 local = Quaternion.Inverse(reference.rotation) *
                (marker.position - reference.position);
            if (local.z <= 0f)
                throw new InvalidOperationException("Photo landmark is behind the reference camera: " + marker.name);
        }
    }

    private static Transform EnsurePoint(Scene scene, string name, Vector3 position)
    {
        GameObject point = FindRoot(scene, name);
        if (point == null) point = new GameObject(name);
        point.transform.position = position;
        return point.transform;
    }

    private static void EnsureEvidence(Scene scene, string name, KZActionKind action,
        string body, Vector3 position)
    {
        GameObject prop = FindRoot(scene, name);
        if (prop == null)
        {
            prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = name;
        }
        prop.transform.position = position;
        prop.transform.localScale = new Vector3(.5f, .04f, .36f);
        Renderer renderer = prop.GetComponent<Renderer>();
        Material paper = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/KongZuo/Art/Prototype/M_Paper.mat");
        if (renderer != null && paper != null) renderer.sharedMaterial = paper;
        KZInteractable item = prop.GetComponent<KZInteractable>();
        if (item == null) item = prop.AddComponent<KZInteractable>();
        item.action = action;
        item.displayName = name;
        item.body = body;
    }

    private static Transform FindChildRecursive(Transform parent, string exactName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == exactName) return child;
            Transform found = FindChildRecursive(child, exactName);
            if (found != null) return found;
        }
        return null;
    }

    private static bool IsGreyboxVisual(string name)
    {
        if (name.StartsWith("第", StringComparison.Ordinal) && name.Contains("排_") && name.EndsWith("座", StringComparison.Ordinal)) return true;
        if (name == "资料桌") return true;
        if (name.StartsWith("侧间隔墙_", StringComparison.Ordinal)) return true;
        if (name.StartsWith("侧房间后墙", StringComparison.Ordinal)) return true;
        switch (name)
        {
            case "1F_地面":
            case "西立面":
            case "东立面":
            case "北端背景":
            case "正门西侧墙":
            case "正门东侧墙":
            case "门楣":
            case "室外石阶限位":
            case "戏台台面":
            case "戏台门楣":
            case "红幕":
            case "舞台西柱":
            case "舞台东柱":
            case "下台阶_高":
            case "下台阶_中":
            case "下台阶_低":
                return true;
            default:
                return false;
        }
    }
}
#endif
