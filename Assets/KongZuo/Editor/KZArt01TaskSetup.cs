#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using KongZuo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Art-only additions: no story state, player movement or building collision changes.
public static class KZArt01TaskSetup
{
    private const string Root = "Assets/KongZuo/Art/Art01";
    private const string Materials = "Assets/KongZuo/Art/KZ_OperaHouse_P0_V03/Generated/Materials/";
    private const string ExistingSeat = "Assets/KongZuo/Art/KZ_OperaHouse_P0_V03/Generated/Prefabs/KZ_P0_R01_S07.prefab";
    private const string LayerName = "KZ_Art01_TaskPass";
    private const string ArtName = "KZ_OperaHouse_P0_V03_From11_Integrated";
    private static readonly string[] Scenes = {
        "Assets/KongZuo/Scenes/01_Prologue.unity",
        "Assets/KongZuo/Scenes/02_Chapter01.unity"
    };

    [MenuItem("Tools/KZ/Art-01/Apply Task Pass")]
    public static void Apply()
    {
        Guard();
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            GenerateAssets();
            foreach (string path in Scenes) ApplyScene(path);
            BuildReviewScene();
            Validate();
            AssetDatabase.SaveAssets();
            Debug.Log("KZ_ART01_OK: generated props, review scene and both playable scene passes validated.");
        }
        finally
        {
            if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }

    private static void Guard()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before applying Art-01.");
        if (Application.isBatchMode)
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Refusing to discard an unsaved scene.");
    }

    public static void GenerateAssets()
    {
        Directory.CreateDirectory(Root + "/Prefabs");
        Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Meshes");
        Directory.CreateDirectory(Root + "/Scenes");
        AssetDatabase.Refresh();
        // Finish materials are shared with the building, never cloned per renderer.
        Material bronze = Mat("KZM_FINISH_BRONZE");
        Material wood = Mat("KZM_FINISH_OAK_DARK");
        Material red = Mat("KZM_FINISH_CINNABAR_VELVET");
        Material ivory = Mat("KZM_CLOTH_CREAM");
        Material dark = Mat("KZM_SOOT");
        Material flame = OwnMaterial("Flame", new Color(1f, .58f, .14f), 0f, .1f, false);
        flame.EnableKeyword("_EMISSION");
        flame.SetColor("_EmissionColor", new Color(3.2f, 1.05f, .12f));
        EditorUtility.SetDirty(flame);
        OwnMaterial("Glass", new Color(.78f, .84f, .80f, .14f), 0f, .82f, true);
        OwnMaterial("MirrorSilver", new Color(.47f, .52f, .50f), .88f, .77f, false);
        Material dust = OwnMaterial("Dust", new Color(.54f, .51f, .44f, .22f), 0f, 0f, true);
        MakeDustTexture(dust);

        GameObject lamp = New("Everlight");
        try
        {
            Lathe(lamp.transform, "TurnedFoot", new[] {
                new Vector2(0f,0f), new Vector2(.16f,0f), new Vector2(.17f,.025f),
                new Vector2(.145f,.045f), new Vector2(.11f,.06f), new Vector2(.055f,.10f),
                new Vector2(.035f,.20f), new Vector2(.075f,.23f), new Vector2(.13f,.245f),
                new Vector2(.125f,.29f), new Vector2(.07f,.32f), new Vector2(0f,.32f)
            }, bronze);
            Lathe(lamp.transform, "GlassChimney", new[] {
                new Vector2(.075f,.31f), new Vector2(.09f,.35f), new Vector2(.088f,.42f),
                new Vector2(.052f,.49f), new Vector2(.045f,.60f), new Vector2(.05f,.63f)
            }, LoadOwn("Glass"));
            Part(lamp.transform,"Wick",PrimitiveType.Cylinder,new Vector3(0,.335f,0),new Vector3(.025f,.025f,.025f),dark);
            Lathe(lamp.transform,"Flame",new[] {
                new Vector2(0f,.35f),new Vector2(.023f,.37f),new Vector2(.022f,.405f),
                new Vector2(.009f,.44f),new Vector2(0f,.47f)
            },flame);
            Light light = New("WarmPool",lamp.transform).AddComponent<Light>();
            light.transform.localPosition = new Vector3(0,.41f,0);
            light.type = LightType.Point;
            light.color = new Color(1f,.61f,.28f);
            light.intensity = 1.3f;
            light.range = 4f;
            light.shadows = LightShadows.None;
            Save(lamp);
        }
        finally { Object.DestroyImmediate(lamp); }

        GameObject keys = New("Keyring");
        try
        {
            Ring(keys.transform,"SplitRing",Vector3.zero,.035f,.0035f,bronze);
            for (int i=0;i<3;i++)
            {
                Transform key=New("Key_"+i,keys.transform).transform;
                key.localPosition=new Vector3((i-1)*.018f,.005f,.025f);
                key.localRotation=Quaternion.Euler(0,(i-1)*24,0);
                Ring(key,"Bow",Vector3.zero,.014f,.003f,bronze);
                Box(key,"Shaft",new Vector3(0,0,.046f),new Vector3(.007f,.005f,.069f),bronze);
                for(int tooth=0;tooth<3;tooth++)
                    Box(key,"Tooth_"+tooth,new Vector3(.007f,0,.06f+tooth*.008f),new Vector3(.018f-tooth*.002f,.006f,.004f),bronze);
            }
            Save(keys);
        }
        finally { Object.DestroyImmediate(keys); }

        GameObject shoes=New("OperaShoes");
        try
        {
            for(int i=0;i<2;i++)
            {
                Transform shoe=New(i==0?"LeftShoe":"RightShoe",shoes.transform).transform;
                shoe.localPosition=new Vector3((i==0?-1:1)*.075f,0,0);
                shoe.localRotation=Quaternion.Euler(0,i==0?-8:8,0);
                ShoeMesh(shoe,"LayeredSole",.0f,.045f,ivory);
                ShoeMesh(shoe,"ClothUpper",.045f,.12f,red);
                Part(shoe,"DarkOpening",PrimitiveType.Sphere,new Vector3(0,.109f,-.065f),new Vector3(.065f,.012f,.09f),dark);
                Box(shoe,"HeelBand",new Vector3(0,.085f,-.133f),new Vector3(.068f,.015f,.012f),ivory);
            }
            Save(shoes);
        }
        finally { Object.DestroyImmediate(shoes); }

        GameObject makeup=New("MakeupBox");
        try
        {
            Box(makeup.transform,"Base",new Vector3(0,.018f,0),new Vector3(.32f,.036f,.23f),wood);
            Box(makeup.transform,"Front",new Vector3(0,.073f,.108f),new Vector3(.32f,.075f,.014f),wood);
            Box(makeup.transform,"Back",new Vector3(0,.073f,-.108f),new Vector3(.32f,.075f,.014f),wood);
            foreach(float x in new[]{-.153f,.153f})
                Box(makeup.transform,"Side",new Vector3(x,.073f,0),new Vector3(.014f,.075f,.23f),wood);
            Box(makeup.transform,"VelvetTray",new Vector3(0,.04f,0),new Vector3(.285f,.008f,.196f),red);
            Transform lid=New("OpenLid",makeup.transform).transform;
            lid.localPosition=new Vector3(0,.11f,.108f);
            lid.localRotation=Quaternion.Euler(-75,0,0);
            Box(lid,"LidPanel",new Vector3(0,0,.11f),new Vector3(.32f,.016f,.23f),wood);
            Box(lid,"InsetMirror",new Vector3(0,.009f,.11f),new Vector3(.257f,.003f,.167f),LoadOwn("MirrorSilver"));
            for(int i=0;i<3;i++)
                Part(makeup.transform,"PigmentPot_"+i,PrimitiveType.Cylinder,new Vector3(-.09f+i*.085f,.065f,.035f),new Vector3(.055f,.021f,.055f),i==1?red:ivory);
            Box(makeup.transform,"Brush",new Vector3(.03f,.07f,-.05f),new Vector3(.19f,.009f,.009f),bronze);
            Box(makeup.transform,"Latch",new Vector3(0,.09f,.119f),new Vector3(.026f,.035f,.007f),bronze);
            Save(makeup);
        }
        finally { Object.DestroyImmediate(makeup); }

        GameObject mirror=New("Mirror");
        try
        {
            Box(mirror.transform,"SilverPlaceholder_NotRealtimeReflection",new Vector3(0,.48f,0),new Vector3(.52f,.74f,.018f),LoadOwn("MirrorSilver"));
            foreach(float x in new[]{-.29f,.29f})
                Box(mirror.transform,"CarvedSide",new Vector3(x,.48f,-.01f),new Vector3(.065f,.85f,.055f),wood);
            foreach(float y in new[]{.06f,.90f})
                Box(mirror.transform,"FrameRail",new Vector3(0,y,-.01f),new Vector3(.645f,.07f,.055f),wood);
            Box(mirror.transform,"Foot",new Vector3(0,.025f,0),new Vector3(.69f,.05f,.22f),wood);
            Save(mirror);
        }
        finally { Object.DestroyImmediate(mirror); }

        GameObject door=New("DoorTrim");
        try
        {
            // Trim only. Do not block the existing walking/interaction opening.
            foreach(float x in new[]{-.66f,.66f})
                Box(door.transform,"Jamb",new Vector3(x,1.1f,0),new Vector3(.09f,2.2f,.12f),wood);
            Box(door.transform,"Lintel",new Vector3(0,2.23f,0),new Vector3(1.41f,.13f,.15f),wood);
            Save(door);
        }
        finally { Object.DestroyImmediate(door); }

        GameObject ticket=New("TicketSide");
        try
        {
            Box(ticket.transform,"CounterSide",new Vector3(0,.44f,0),new Vector3(.55f,.88f,.44f),wood);
            Box(ticket.transform,"CounterTop",new Vector3(0,.91f,0),new Vector3(.62f,.06f,.51f),wood);
            Box(ticket.transform,"CashDrawerPlaceholder",new Vector3(0,1.03f,0),new Vector3(.38f,.18f,.32f),bronze);
            Box(ticket.transform,"DrawerGrip",new Vector3(0,1.04f,-.17f),new Vector3(.12f,.018f,.025f),dark);
            Save(ticket);
        }
        finally { Object.DestroyImmediate(ticket); }
        AssetDatabase.SaveAssets();
    }

    private static void ApplyScene(string path)
    {
        Scene scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
        ApplyToScene(scene);
        if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save "+path);
    }

    // Called by the main theatre integration after its building and interactions are ready.
    public static void ApplyToScene(Scene scene)
    {
        string path=scene.path;
        GameObject art=FindRoot(scene,ArtName);
        foreach(GameObject candidate in scene.GetRootGameObjects())
        {
            if(art==null && Find(candidate.transform,"D_Row01Seat07")!=null) art=candidate;
        }
        if(art==null) throw new InvalidOperationException(path+": integrated building missing; run the existing integration menu first.");
        art.name=ArtName;
        PrefabUtility.RecordPrefabInstancePropertyModifications(art);
        // Keep the three still-valid beam overrides authored in the original scene.
        // Two historical override IDs no longer exist in the current building prefab.
        long[] beamIds={1655034640484464130L,8103686996711970863L,8791354829395931536L};
        foreach(Transform part in art.GetComponentsInChildren<Transform>(true))
        {
            Object source=PrefabUtility.GetCorrespondingObjectFromSource(part);
            if(source==null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long id)) continue;
            if(guid!=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(art)))) continue;
            if(Array.IndexOf(beamIds,id)<0) continue;
            Vector3 position=part.localPosition;
            position.y=10f;
            part.localPosition=position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(part);
        }
        Transform seat=Find(art.transform,"R01_S07__P0_KEY_Seat");
        Transform stage=Find(art.transform,"P0_StageCentre");
        Transform makeup=Find(art.transform,"P0_MakeupRoom");
        Transform register=Find(art.transform,"C_TicketRegister");
        Transform office=Find(art.transform,"B_OfficeTelephone");
        if(seat==null||stage==null||makeup==null||register==null||office==null)
            throw new InvalidOperationException(path+": required art anchors missing.");
        // Build first; replace only this tool's prior output after all required anchors exist.
        GameObject old=FindRoot(scene,LayerName);
        Transform layer=New(LayerName).transform;
        try
        {
            Transform symbols=New("CoreSymbols",layer).transform;
            Transform marker=New("EmptySeat_UsesExistingAuthoredModel",symbols).transform;
            marker.SetPositionAndRotation(seat.position,seat.rotation);
            Vector3 lampPosition=stage.position+new Vector3(-5.8f,0,-2f);
            Box(symbols,"EverlightPedestal",lampPosition+Vector3.up*.35f,new Vector3(.48f,.70f,.48f),Mat("KZM_FINISH_OAK_DARK"));
            Place("Everlight",symbols,lampPosition+Vector3.up*.70f);

            // Resolve the actual room sign after model import, including OBJ axis conversion.
            Renderer makeupSign=null;
            foreach(MeshFilter filter in art.GetComponentsInChildren<MeshFilter>(true))
                if(filter.sharedMesh!=null && AssetDatabase.GetAssetPath(filter.sharedMesh).Contains("SIGN_MAKEUP"))
                    makeupSign=filter.GetComponent<Renderer>();
            if(makeupSign==null) throw new InvalidOperationException("Authored makeup-room sign missing.");
            makeup.position=new Vector3(makeupSign.bounds.center.x,0f,16f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(makeup);
            float roomSide=Mathf.Sign(makeup.position.x);
            Transform dressing=New("MakeupRoom_Integrated",layer).transform;
            dressing.position=makeup.position;
            // Use the existing two tables, whose top surface is y=.865; do not add
            // another console through the authored furniture or its wall mirrors.
            Vector3 table=makeup.position+new Vector3(-1.1f*roomSide,.87f,1.2f);
            Place("Mirror",dressing,table+new Vector3(-.36f*roomSide,0,.14f));
            Place("MakeupBox",dressing,table+new Vector3(.27f*roomSide,0,-.07f));
            Place("OperaShoes",dressing,table+new Vector3(1.85f*roomSide,0,-.04f));
            Place("Keyring",dressing,table+new Vector3(2.18f*roomSide,.005f,-.08f)).name="DressingKeyring";
            Place("Everlight",dressing,table+new Vector3(2.55f*roomSide,0,.03f)).name="MakeupEverlight";
            GameObject chairAsset=AssetDatabase.LoadAssetAtPath<GameObject>(ExistingSeat);
            if(chairAsset==null) throw new FileNotFoundException(ExistingSeat);
            GameObject chair=(GameObject)PrefabUtility.InstantiatePrefab(chairAsset,dressing);
            chair.name="DressingChair";
            chair.transform.SetPositionAndRotation(makeup.position+new Vector3(-1.1f*roomSide,0,.1f),Quaternion.identity);
            // This pass adds visuals only; use the room's existing physical layout.
            foreach(Collider collider in chair.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            Place("Keyring",layer,office.position+new Vector3(.25f,.02f,.12f));
            Place("TicketSide",layer,new Vector3(register.position.x+1.15f,0,register.position.z+.35f));

            Transform entrances=New("EntranceDetails",layer).transform;
            Transform side=Find(art.transform,"K_EastLockedWorkDoor");
            if(side!=null)
            {
                Transform trim=Place("DoorTrim",entrances,new Vector3(side.position.x,0,side.position.z));
                trim.rotation=side.rotation;
            }
            // Existing backstage interaction establishes the exact door pose.
            foreach(GameObject root in scene.GetRootGameObjects())
                foreach(KZInteractable item in root.GetComponentsInChildren<KZInteractable>(true))
                    if(item.action==KZActionKind.BackstageDoor && item.GetComponent<Renderer>()!=null)
                    {
                        Bounds bounds=item.GetComponent<Renderer>().bounds;
                        Transform trim=Place("DoorTrim",entrances,new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));
                        trim.name="BackstageDoorTrim";
                        trim.rotation=item.transform.rotation;
                    }

            Transform dust=New("DustOverlays_NoColliders",layer).transform;
            Dust(dust,"PedestalDust",lampPosition+new Vector3(0,.705f,0),new Vector2(.42f,.42f));
            Dust(dust,"MakeupDust",table+Vector3.up*.002f,new Vector2(1.48f,.53f));
            Dust(dust,"MakeupDustEast",table+new Vector3(2.1f*roomSide,.002f,0),new Vector2(1.48f,.53f));
            Dust(dust,"TicketDust",new Vector3(register.position.x+1.15f,.944f,register.position.z+.35f),new Vector2(.60f,.49f));
            KZMakeupRoomDoorSetup.Apply(art,layer,makeup.position);
            // Leave Seat07 clean: the story explicitly contrasts it with surrounding seats.
            ValidateLayer(layer.gameObject);
            if(old!=null) Object.DestroyImmediate(old);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("KZ_ART01_SCENE: "+path+"; stage="+stage.position+"; makeup="+makeup.position+"; office="+office.position);
        }
        catch
        {
            Object.DestroyImmediate(layer.gameObject);
            throw;
        }
    }

    [MenuItem("Tools/KZ/Art-01/Validate Task Pass")]
    public static void Validate()
    {
        Guard();
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] previous=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach(string path in Scenes)
            {
                Scene scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                ValidateScene(scene);
                int players=0,directors=0,seatActions=0;
                foreach(GameObject root in scene.GetRootGameObjects())
                {
                    players+=root.GetComponentsInChildren<KZFirstPersonPlayer>(true).Length;
                    directors+=root.GetComponentsInChildren<KZStoryDirector>(true).Length;
                    foreach(KZInteractable item in root.GetComponentsInChildren<KZInteractable>(true))
                        if(item.action==KZActionKind.Seat07 && item.gameObject.activeInHierarchy && item.GetComponentInChildren<Collider>()!=null)
                            seatActions++;
                }
                if(players!=1 || directors!=1 || seatActions<1)
                    throw new InvalidOperationException(path+": missing/duplicate player, director or missing seat interaction.");
                Debug.Log("KZ_ART01_VALIDATE_OK: "+path);
            }
        }
        finally { if(previous.Length>0) EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }

    public static void ValidateScene(Scene scene)
    {
        GameObject layer=FindRoot(scene,LayerName);
        if(layer==null) throw new InvalidOperationException(scene.path+": Art-01 layer missing.");
        int count=0;
        foreach(GameObject root in scene.GetRootGameObjects()) if(root.name==LayerName) count++;
        if(count!=1) throw new InvalidOperationException(scene.path+": duplicate Art-01 layers.");
        ValidateLayer(layer);
        Transform room=Find(layer.transform,"MakeupRoom_Integrated");
        if(room==null) throw new InvalidOperationException(scene.path+": integrated dressing room missing.");
        foreach(string name in new[]{"Mirror","MakeupBox","OperaShoes","MakeupEverlight","DressingKeyring"})
        {
            Transform prop=Find(room,name);
            if(prop==null || !prop.gameObject.activeInHierarchy)
                throw new InvalidOperationException(scene.path+": dressing-room prop missing/disabled: "+name);
            Vector3 p=prop.position;
            if(Mathf.Abs(p.x)<5.64f || Mathf.Abs(p.x)>9.26f || p.z<16.92f || p.z>17.60f || Mathf.Abs(p.y-.87f)>.02f)
                throw new InvalidOperationException(scene.path+": prop is outside the authored makeup tables: "+name);
        }
    }

    private static void ValidateLayer(GameObject layer)
    {
        foreach(string required in new[]{"Everlight","Keyring","OperaShoes","MakeupBox","Mirror","TicketSide","DustOverlays_NoColliders"})
            if(Find(layer.transform,required)==null)
                throw new InvalidOperationException("Art-01 object missing: "+required);
        if(layer.GetComponentsInChildren<Collider>(true).Length!=0 || layer.GetComponentsInChildren<KZInteractable>(true).Length!=0)
            throw new InvalidOperationException("Art-only layer must not introduce collisions or story interactions.");
        foreach(Transform part in layer.GetComponentsInChildren<Transform>(true))
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(part.gameObject)>0)
                throw new InvalidOperationException("Missing script: "+part.name);
        foreach(MeshRenderer renderer in layer.GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter filter=renderer.GetComponent<MeshFilter>();
            if(filter==null || filter.sharedMesh==null) throw new InvalidOperationException("Missing mesh: "+renderer.name);
            foreach(Material material in renderer.sharedMaterials)
                if(material==null || material.shader==null || !material.shader.isSupported)
                    throw new InvalidOperationException("Missing/unsupported material: "+renderer.name);
        }
    }

    private static void BuildReviewScene()
    {
        Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.35f,.35f,.35f);
        Transform root=New("Art01_Review_NotGameplay").transform;
        Box(root,"Ground",new Vector3(0,-.06f,0),new Vector3(8,.1f,5),Mat("KZM_FINISH_FLOOR_OAK"));
        GameObject seatAsset=AssetDatabase.LoadAssetAtPath<GameObject>(ExistingSeat);
        if(seatAsset==null) throw new FileNotFoundException(ExistingSeat);
        GameObject seat=(GameObject)PrefabUtility.InstantiatePrefab(seatAsset);
        seat.transform.SetParent(root,false);
        seat.transform.localPosition=new Vector3(-1.8f,0,0);
        Place("Everlight",root,new Vector3(-.85f,0,0));
        Box(root,"PropTable",new Vector3(.8f,.38f,0),new Vector3(2.3f,.76f,.85f),Mat("KZM_FINISH_OAK_DARK"));
        Place("Mirror",root,new Vector3(1.35f,.76f,.13f));
        Place("MakeupBox",root,new Vector3(.6f,.76f,-.1f));
        Place("OperaShoes",root,new Vector3(.0f,.76f,-.1f));
        Place("Keyring",root,new Vector3(1.2f,.76f,-.3f));
        Place("TicketSide",root,new Vector3(2.6f,0,.2f));
        Light light=New("ReviewKey").AddComponent<Light>();
        light.type=LightType.Directional;
        light.intensity=2f;
        light.transform.rotation=Quaternion.Euler(40,-30,0);
        Camera camera=New("ReviewCamera").AddComponent<Camera>();
        camera.transform.position=new Vector3(-3.5f,2.7f,-5.5f);
        camera.transform.LookAt(new Vector3(0,.6f,0));
        camera.fieldOfView=44;
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.15f,.17f,.16f);
        if(!EditorSceneManager.SaveScene(scene,Root+"/Scenes/Art01_Review.unity")) throw new IOException("Could not save review scene.");
    }

    [MenuItem("Tools/KZ/Art-01/Capture Review")]
    public static void CaptureReview()
    {
        Guard();
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] previous=EditorSceneManager.GetSceneManagerSetup();
        RenderTexture target=null;
        Texture2D pixels=null;
        RenderTexture active=RenderTexture.active;
        try
        {
            Scene scene=EditorSceneManager.OpenScene(Root+"/Scenes/Art01_Review.unity",OpenSceneMode.Single);
            Camera camera=null;
            foreach(GameObject go in scene.GetRootGameObjects())
                if(go.GetComponent<Camera>()!=null) camera=go.GetComponent<Camera>();
            if(camera==null) throw new InvalidOperationException("Review camera missing.");
            Directory.CreateDirectory("Logs/Art01");
            target=new RenderTexture(1600,1000,24);
            pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            camera.targetTexture=target;
            Vector3[] positions={camera.transform.position,new Vector3(-1.55f,.8f,-1.2f),new Vector3(.5f,1.65f,-1.05f)};
            Vector3[] lookAt={new Vector3(0,.6f,0),new Vector3(-.85f,.32f,0),new Vector3(.65f,.84f,0)};
            string[] names={"overview","everlight","whitebox-props"};
            for(int i=0;i<names.Length;i++)
            {
                camera.transform.position=positions[i];
                camera.transform.LookAt(lookAt[i]);
                camera.Render();
                RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);
                pixels.Apply();
                File.WriteAllBytes("Logs/Art01/"+names[i]+".png",pixels.EncodeToPNG());
            }
            camera.targetTexture=null;
            Debug.Log("KZ_ART01_CAPTURE_OK: Logs/Art01/*.png");
        }
        finally
        {
            RenderTexture.active=active;
            if(target!=null) { target.Release(); Object.DestroyImmediate(target); }
            if(pixels!=null) Object.DestroyImmediate(pixels);
            if(previous.Length>0) EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }

    private static Material Mat(string name)
    {
        Material result=AssetDatabase.LoadAssetAtPath<Material>(Materials+name+".mat");
        if(result==null) throw new FileNotFoundException(Materials+name+".mat");
        return result;
    }

    private static Material LoadOwn(string name) { return AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+name+".mat"); }

    private static Material OwnMaterial(string name,Color color,float metal,float smooth,bool transparent)
    {
        Material material=LoadOwn(name);
        if(material==null)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            material=new Material(shader);
            AssetDatabase.CreateAsset(material,Root+"/Materials/"+name+".mat");
        }
        material.SetColor("_BaseColor",color);
        material.SetFloat("_Metallic",metal);
        material.SetFloat("_Smoothness",smooth);
        if(transparent)
        {
            material.SetFloat("_Surface",1);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0);
            material.SetFloat("_Cull",(float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType","Transparent");
            material.renderQueue=(int)RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void MakeDustTexture(Material material)
    {
        const int size=64;
        string path=Root+"/Materials/DustMask.asset";
        Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(texture==null)
        {
            texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            AssetDatabase.CreateAsset(texture,path);
        }
        Color[] pixels=new Color[size*size];
        for(int y=0;y<size;y++) for(int x=0;x<size;x++)
        {
            float edge=Mathf.Min(Mathf.Min(x,y),Mathf.Min(size-1-x,size-1-y))/10f;
            float noise=Mathf.PerlinNoise(x*.19f+7f,y*.19f+11f);
            pixels[y*size+x]=new Color(1,1,1,Mathf.SmoothStep(0,1,Mathf.Clamp01(edge))*noise);
        }
        texture.SetPixels(pixels);
        texture.wrapMode=TextureWrapMode.Clamp;
        texture.Apply();
        material.SetTexture("_BaseMap",texture);
        EditorUtility.SetDirty(texture);
        EditorUtility.SetDirty(material);
    }

    private static GameObject New(string name,Transform parent=null)
    {
        GameObject go=new GameObject(name);
        if(parent!=null) go.transform.SetParent(parent,false);
        return go;
    }

    private static Transform Place(string name,Transform parent,Vector3 world)
    {
        GameObject asset=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+name+".prefab");
        if(asset==null) throw new FileNotFoundException(name);
        GameObject instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
        instance.transform.position=world;
        return instance.transform;
    }

    private static void Save(GameObject go)
    {
        if(PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+go.name+".prefab")==null)
            throw new IOException("Could not save "+go.name);
    }

    private static GameObject Part(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {
        GameObject go=GameObject.CreatePrimitive(type);
        go.name=name;
        go.transform.SetParent(parent,false);
        go.transform.localPosition=position;
        go.transform.localScale=scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial=material;
        return go;
    }

    private static void Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
    { Part(parent,name,PrimitiveType.Cube,position,scale,material); }

    private static void Dust(Transform parent,string name,Vector3 position,Vector2 size)
    {
        GameObject go=Part(parent,name,PrimitiveType.Quad,position,new Vector3(size.x,size.y,1),LoadOwn("Dust"));
        go.transform.localRotation=Quaternion.Euler(90,0,0);
        go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        go.GetComponent<Renderer>().receiveShadows=false;
    }

    private static void MeshPart(Transform parent,string name,Vector3[] vertices,int[] triangles,Vector2[] uv,Material material)
    {
        string path=Root+"/Meshes/"+name+".asset";
        Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null) { mesh=new Mesh(); AssetDatabase.CreateAsset(mesh,path); }
        mesh.Clear();
        mesh.name=name;
        mesh.vertices=vertices;
        mesh.triangles=triangles;
        mesh.uv=uv;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        GameObject go=New(name,parent);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial=material;
    }

    private static void Lathe(Transform parent,string name,Vector2[] profile,Material material)
    {
        const int segments=64;
        Vector3[] v=new Vector3[profile.Length*(segments+1)];
        Vector2[] uv=new Vector2[v.Length];
        List<int> triangles=new List<int>();
        for(int row=0;row<profile.Length;row++) for(int i=0;i<=segments;i++)
        {
            int n=row*(segments+1)+i;
            float a=i*2*Mathf.PI/segments;
            v[n]=new Vector3(profile[row].x*Mathf.Cos(a),profile[row].y,profile[row].x*Mathf.Sin(a));
            uv[n]=new Vector2((float)i/segments,(float)row/(profile.Length-1));
            if(row<profile.Length-1 && i<segments)
            {
                int next=n+segments+1;
                triangles.AddRange(new[]{n,next,n+1,n+1,next,next+1});
            }
        }
        MeshPart(parent,name,v,triangles.ToArray(),uv,material);
    }

    private static void Ring(Transform parent,string name,Vector3 position,float radius,float tube,Material material)
    {
        const int segments=40,sides=8;
        Vector3[] v=new Vector3[(segments+1)*(sides+1)];
        Vector2[] uv=new Vector2[v.Length];
        List<int> triangles=new List<int>();
        for(int i=0;i<=segments;i++) for(int j=0;j<=sides;j++)
        {
            float a=2*Mathf.PI*i/segments,b=2*Mathf.PI*j/sides;
            int n=i*(sides+1)+j;
            v[n]=new Vector3((radius+tube*Mathf.Cos(b))*Mathf.Cos(a),tube*Mathf.Sin(b),(radius+tube*Mathf.Cos(b))*Mathf.Sin(a));
            uv[n]=new Vector2((float)i/segments,(float)j/sides);
            if(i<segments && j<sides) triangles.AddRange(new[]{n,n+1,n+sides+1,n+1,n+sides+2,n+sides+1});
        }
        string meshName=name=="SplitRing"?"SplitRingMesh":"KeyBowMesh";
        MeshPart(parent,meshName,v,triangles.ToArray(),uv,material);
        parent.GetChild(parent.childCount-1).localPosition=position;
    }

    private static void ShoeMesh(Transform parent,string name,float bottom,float top,Material material)
    {
        // Elliptical sections give the cloth shoe a raised toe and narrower heel.
        const int sides=16;
        float[] zs={-.14f,-.11f,-.03f,.06f,.13f,.17f};
        float[] widths={.015f,.038f,.052f,.055f,.04f,.004f};
        int capStart=zs.Length*(sides+1);
        Vector3[] v=new Vector3[capStart+2];
        Vector2[] uv=new Vector2[v.Length];
        List<int> triangles=new List<int>();
        for(int row=0;row<zs.Length;row++) for(int i=0;i<=sides;i++)
        {
            float a=2*Mathf.PI*i/sides;
            int n=row*(sides+1)+i;
            float lift=row>=4?(row-3)*.012f:0;
            v[n]=new Vector3(widths[row]*Mathf.Cos(a),(bottom+top)*.5f+Mathf.Sin(a)*(top-bottom)*.5f+lift,zs[row]);
            uv[n]=new Vector2((float)i/sides,(float)row/(zs.Length-1));
            if(row<zs.Length-1 && i<sides)
                triangles.AddRange(new[]{n,n+1,n+sides+1,n+1,n+sides+2,n+sides+1});
        }
        v[capStart]=new Vector3(0,(bottom+top)*.5f,zs[0]);
        v[capStart+1]=new Vector3(0,(bottom+top)*.5f+.024f,zs[zs.Length-1]);
        for(int i=0;i<sides;i++)
        {
            int end=(zs.Length-1)*(sides+1)+i;
            triangles.AddRange(new[]{capStart,i+1,i,capStart+1,end,end+1});
        }
        MeshPart(parent,name,v,triangles.ToArray(),uv,material);
    }

    private static Transform Find(Transform root,string name)
    {
        foreach(Transform child in root.GetComponentsInChildren<Transform>(true))
            if(child.name==name) return child;
        return null;
    }

    private static GameObject FindRoot(Scene scene,string name)
    {
        foreach(GameObject go in scene.GetRootGameObjects()) if(go.name==name) return go;
        return null;
    }
}
#endif
