#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class KZExternalPropSetup
{
 const string Source="Assets/KongZuo/Art/Art01/Materials";
 const string LayerName="KZ_ExternalProps_Mainline";
 static readonly string[] Scenes={"Assets/KongZuo/Scenes/01_Prologue.unity","Assets/KongZuo/Scenes/02_Chapter01.unity"};
 static UnityEngine.SceneManagement.Scene current;
 static Transform layer;
 static readonly System.Collections.Generic.Dictionary<string,Material[]> Overrides=new System.Collections.Generic.Dictionary<string,Material[]>();
 static string PathFor(string key)
 {
  string p=AssetDatabase.GetAllAssetPaths().FirstOrDefault(x=>x.StartsWith(Source+"/")&&x.Contains(key)&&(x.EndsWith(".fbx")||x.EndsWith(".glb")||x.EndsWith(".blend")));
  if(p==null)throw new Exception("Missing source: "+key);return p;
 }
 static Bounds BoundsOf(GameObject go)
 {
  var rs=go.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)throw new Exception("No renderer "+go.name);
  var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;
 }
 static Transform Find(string name)
 {
  foreach(var root in current.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;
  return null;
 }
 // Dimensions use rendered world bounds, preserving each importer's axis conversion.
 static GameObject Place(string key,string name,Vector3 bottom,float width,float yaw=0,Vector3? euler=null)
 {
  string path=PathFor(key);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
  if(asset==null)throw new Exception("Cannot load "+path);
  var go=new GameObject(name);go.transform.SetParent(layer,false);
  PrefabUtility.InstantiatePrefab(asset,go.transform);
  go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.Euler(euler??new Vector3(0,yaw,0));
  var b=BoundsOf(go);float span=Mathf.Max(b.size.x,b.size.z);
  go.transform.localScale*=width/span;
  b=BoundsOf(go);go.transform.position+=bottom-new Vector3(b.center.x,b.min.y,b.center.z);
  foreach(var c in go.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
  foreach(var data in go.GetComponentsInChildren<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(true))UnityEngine.Object.DestroyImmediate(data);
  foreach(var data in go.GetComponentsInChildren<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>(true))UnityEngine.Object.DestroyImmediate(data);
  foreach(var listener in go.GetComponentsInChildren<AudioListener>(true))UnityEngine.Object.DestroyImmediate(listener);
  foreach(var camera in go.GetComponentsInChildren<Camera>(true))UnityEngine.Object.DestroyImmediate(camera);
  foreach(var l in go.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(l);
  if(Overrides.TryGetValue(path,out var mats))
  {
   var rs=go.GetComponentsInChildren<Renderer>(true);
   if(rs.Length==1)rs[0].sharedMaterials=mats;
  }
  // Local material assets supplied alongside the imported mesh.
  string folder=Path.GetDirectoryName(path).Replace('\\','/');
  string parent=folder.EndsWith("/source")?folder.Substring(0,folder.Length-7):folder;
  var custom=AssetDatabase.LoadAssetAtPath<Material>(parent+"/New Material.mat")??AssetDatabase.LoadAssetAtPath<Material>(parent+"/textures/New Material.mat");
  if(custom!=null)foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.sharedMaterial=custom;
  if(path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)) ApplyTextures(go,parent);
  return go;
 }
 static void ApplyTextures(GameObject go,string folder)
 {
  var textures=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith(folder+"/")&&(p.EndsWith(".png")||p.EndsWith(".jpg"))&&(p.Contains("BaseColor")||p.Contains("_Base.")||p.Contains("albedo"))).ToArray();
  if(textures.Length==0)return;
  string output="Assets/KongZuo/Art/Art01/ExternalMaterials";
  if(!AssetDatabase.IsValidFolder(output))AssetDatabase.CreateFolder("Assets/KongZuo/Art/Art01","ExternalMaterials");
  foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))
  {
   var mats=renderer.sharedMaterials;
   for(int i=0;i<mats.Length;i++)
   {

    string hint=mats[i]==null?"":mats[i].name;
    string tex=textures.FirstOrDefault(p=>hint.Contains("1002")&&p.Contains("1002")||hint.Contains("1003")&&p.Contains("1003"))??textures.FirstOrDefault(p=>p.Contains("1001"))??textures[0];
    string matPath=output+"/"+Path.GetFileNameWithoutExtension(tex)+".mat";
    var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
    if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,matPath);}
    material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(tex));material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.22f);EditorUtility.SetDirty(material);mats[i]=material;
   }
   renderer.sharedMaterials=mats;
  }
 }
 static float Surface(GameObject furniture,float x,float z)
 {
  float y=float.MinValue;
  foreach(var filter in furniture.GetComponentsInChildren<MeshFilter>(true))
  {
   if(filter.sharedMesh==null)continue;
   var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
   if(collider.Raycast(new Ray(new Vector3(x,8,z),Vector3.down),out var hit,12))y=Mathf.Max(y,hit.point.y);
   UnityEngine.Object.DestroyImmediate(collider);
  }
  return y==float.MinValue?BoundsOf(furniture).max.y:y;
 }
 static Vector3 On(GameObject furniture,float x,float z){return new Vector3(x,Surface(furniture,x,z)+.006f,z);}
 static void Hide(string name)
 {
  var t=Find(name);if(t==null)return;t.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(t);
 }
 static void Furniture(GameObject go,bool precise=false)
 {
  if(precise)
  {
   foreach(var filter in go.GetComponentsInChildren<MeshFilter>(true))
    if(filter.sharedMesh!=null){var mesh=filter.gameObject.AddComponent<MeshCollider>();mesh.sharedMesh=filter.sharedMesh;}
   return;
  }
  var b=BoundsOf(go);var box=go.AddComponent<BoxCollider>();box.center=go.transform.InverseTransformPoint(b.center);
  box.size=new Vector3(b.size.x/Mathf.Abs(go.transform.lossyScale.x),b.size.y/Mathf.Abs(go.transform.lossyScale.y),b.size.z/Mathf.Abs(go.transform.lossyScale.z));
 }
 static void Bind(string targetName,string key,Vector3 bottom,float width,Vector3? rotation=null)
 {
  var target=Find(targetName);if(target==null)return;
  // Retain the original story component and all director references, including inactive program props.
  foreach(var r in target.GetComponentsInChildren<Renderer>(true))r.enabled=false;
  foreach(var c in target.GetComponentsInChildren<Collider>(true))c.enabled=false;
  var old=target.Find("ExternalModel");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
  var go=Place(key,"ExternalModel",bottom,width,0,rotation);var b=BoundsOf(go);
  target.position=b.center;target.rotation=Quaternion.identity;target.localScale=Vector3.one;
  go.transform.SetParent(target,true);
  var box=target.GetComponent<BoxCollider>()??target.gameObject.AddComponent<BoxCollider>();
  box.enabled=true;box.isTrigger=false;box.center=Vector3.zero;box.size=new Vector3(Mathf.Max(.14f,b.size.x),Mathf.Max(.035f,b.size.y),Mathf.Max(.12f,b.size.z));
  PrefabUtility.RecordPrefabInstancePropertyModifications(target);
 }
 [MenuItem("Tools/空座/外部模型/布置序章与第一章")]
 public static void Apply()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit play mode first.");
  if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
  Directory.CreateDirectory("Logs/ExternalProps");
  foreach(string path in Scenes)
  {
   current=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
   Overrides.Clear();
   foreach(var root in current.GetRootGameObjects())
   {
    var source=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
    if(!source.StartsWith(Source+"/")||source.Contains("armchair"))continue;
    var rs=root.GetComponentsInChildren<Renderer>(true);if(rs.Length==1)Overrides[source]=rs[0].sharedMaterials;
    // Preserve hand-authored samples for reference without leaving duplicates in the playable space.
    root.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(root);
   }
   var old=Find(LayerName);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
   layer=new GameObject(LayerName).transform;
   Hide("30_Furniture_EastDesk");Hide("30_Furniture_WestDesk");
   foreach(string name in new[]{"02_FrontRooms__TIMBER__Furniture_EastDesk","02_FrontRooms__TIMBER_DARK__Furniture_EastDesk","02_FrontRooms__TIMBER_LIGHT__Furniture_EastDesk",
    "02_FrontRooms__TIMBER__Furniture_WestDesk","02_FrontRooms__TIMBER_DARK__Furniture_WestDesk"})Hide(name);
   var officeDesk=Place("Mesa_Salon","办公室木桌（装饰）",new Vector3(-7.5f,0,-16.15f),2.8f,90);
   var db=BoundsOf(officeDesk);var ds=officeDesk.transform.localScale;ds.y*=.94f/db.size.y;officeDesk.transform.localScale=ds;
   Furniture(officeDesk);
   // Office: the phone remains at its story anchor, with a small working desk light.
   Bind("办公室电话","Phone_001.fbx",On(officeDesk,-8.10f,-16.05f),.52f);
   if(Find("办公室电话")==null)Place("Phone_001.fbx","电话（装饰）",On(officeDesk,-8.10f,-16.05f),.52f);
   Place("台灯.glb","台灯（装饰）",On(officeDesk,-7.45f,-15.85f),.50f,145);
   Place("Holder_Leather","信堆（装饰）",new Vector3(-6.55f,.94f,-16.1f),.28f);
   Place("Book_low","旧书_办公室（装饰）",new Vector3(-7.0f,.94f,-16.2f),.27f);
   var shelf=Place("shelf.fbx","办公室壁架（装饰）",new Vector3(-9.12f,1.25f,-16.3f),.45f,90);
   var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/KongZuo/Art/KZ_OperaHouse_P0_V03/Generated/Materials/KZM_FINISH_OAK_DARK.mat");
   foreach(var r in shelf.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=Enumerable.Repeat(wood,r.sharedMaterials.Length).ToArray();
   // Ticket counter: spread the evidence along the front edge so individual E targets stay distinct.
   var counter=Place("traditional_chinese_altar_table","售票资料台（装饰）",new Vector3(7.3f,0,-16.3f),2.8f);
   var cb=BoundsOf(counter);
   // Keep a readable one-metre counter height while retaining source proportions on X/Z.
   var scale=counter.transform.localScale;scale.y*=1.0f/cb.size.y;counter.transform.localScale=scale;
   cb=BoundsOf(counter);counter.transform.position+=Vector3.up*(-cb.min.y);Furniture(counter,true);
   Place("收银台（售票口）/","收银机（装饰）",On(counter,8.35f,-16.36f),.48f,0);
   Bind("1992年座位登记表","Notebook_Leather",On(counter,7.25f,-15.98f),.36f);
   Bind("残缺的旧票根","票根2/Untitled",On(counter,7.05f,-15.98f),.29f);
   Bind("一排座次示意图","Envelope_Paper_Stacked",On(counter,7.95f,-15.98f),.35f);
   Bind("周明生场务物品记录","Notebook_Leather",On(counter,6.25f,-16.20f),.43f);
   Bind("七号座遗物推理记录","Envelope.fbx",On(counter,7.15f,-16.58f),.32f,new Vector3(0,0,90));
   Place("Bundled_Paper","纸堆（装饰）",new Vector3(5.62f,0,-16.55f),.38f);
   // Foyer information table connects the programme and old photograph to the auditorium route.
   var table=Place("Mesa_Salon","前厅长桌（装饰）",new Vector3(-4.42f,0,-16.25f),2.8f,0);Furniture(table);
   float top=BoundsOf(table).max.y+.01f;
   Bind("留座戏旧宣传册","Book_low",new Vector3(-4.42f,top,-16.98f),.36f);
   Bind("周明生旧合照","Decorative_Picture_Frame",new Vector3(-4.42f,top,-15.58f),.43f,new Vector3(90,0,0));
   Bind("新出现的旧节目单","Envelope_Paper_Stacked",new Vector3(.14f,.69f,2.63f),.32f);
   // Backstage side rooms: keep the central door approach and the echo route clear.
   var rail=Place("Modular_Wooden_Rail","服装间矮屏风（装饰）",new Vector3(-3.85f,0,17.05f),1.15f);Furniture(rail);
   Place("Book_low","后台旧书（装饰）",new Vector3(-2.8f,0,17.25f),.28f,15);
   Place("Paper_Rolled","档案室门边旧档（装饰）",new Vector3(3.75f,0,13.1f),.42f);
   // Existing art-only placeholders remain as empty anchors for the original integration validator.
   var placeholder=Find("TicketSide");if(placeholder!=null)foreach(var r in placeholder.GetComponentsInChildren<Renderer>(true))r.enabled=false;
   var oldMirror=Find("Mirror");if(oldMirror!=null)foreach(var r in oldMirror.GetComponentsInChildren<Renderer>(true))r.enabled=false;
   FixFrontFurniture();
   CleanSharedPlaceholders();
   AlignDirectoryAndCabinet();
   HideLegacySeating();
   RemoveBenchCollision();
   ValidateCurrent();
   EditorSceneManager.MarkSceneDirty(current);if(!EditorSceneManager.SaveScene(current))throw new Exception("Cannot save "+path);
   Debug.Log("KZ_EXTERNAL_APPLIED "+path);
  }
  AssetDatabase.SaveAssets();Debug.Log("KZ_EXTERNAL_APPLY_OK");
 }
 public static void AuditBench()
 {
  current=EditorSceneManager.OpenScene(Scenes[0],OpenSceneMode.Single);Physics.SyncTransforms();
  foreach(var c in Physics.OverlapCapsule(new Vector3(-2.7f,.58f,-13.3f),new Vector3(-2.7f,1.5f,-13.3f),.22f,~0,QueryTriggerInteraction.Ignore))
  {
   var m=c as MeshCollider;
   if(m!=null)
   {
    var vertices=m.sharedMesh.vertices;var triangles=m.sharedMesh.triangles;
    var inside=new System.Collections.Generic.HashSet<int>();
    for(int i=0;i<vertices.Length;i++)
    {
     var world=m.transform.TransformPoint(vertices[i]);
     if(world.x>=-3.80f&&world.x<=-1.60f&&world.y>=.40f&&world.y<=.58f&&world.z>=-13.55f&&world.z<=-13.05f)inside.Add(i);
    }
    int full=0,crossing=0;var bounds=new Bounds();bool first=true;
    for(int i=0;i<triangles.Length;i+=3)
    {
     int contained=(inside.Contains(triangles[i])?1:0)+(inside.Contains(triangles[i+1])?1:0)+(inside.Contains(triangles[i+2])?1:0);
     if(contained==3){full++;for(int j=0;j<3;j++){var point=m.transform.TransformPoint(vertices[triangles[i+j]]);if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}}
     else if(contained!=0)crossing++;
    }
    Debug.Log("KZ_BENCH_GEOMETRY_PROOF exactTriangles="+full+" crossingTriangles="+crossing+" retainedTriangles="+(triangles.Length/3-full)+" vertices="+inside.Count+" independentWorldBounds="+bounds.ToString("F5"));
   }
   Debug.Log("KZ_BENCH_COLLIDER "+c.name+" type="+c.GetType().Name+" mesh="+(m==null?"NONE":AssetDatabase.GetAssetPath(m.sharedMesh))+" bounds="+c.bounds.ToString("F4"));
  }
 }
 public static void AuditFixes()
 {
  Directory.CreateDirectory("Logs/ExternalProps");
  foreach(string path in Scenes)
  {
   current=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
   var log=new StringBuilder();
   foreach(var root in current.GetRootGameObjects())
   {
    string source=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
    if(source.Contains("armchair"))log.AppendLine("CHAIR "+root.name+" p="+root.transform.position.ToString("F3")+" r="+root.transform.eulerAngles.ToString("F2")+" s="+root.transform.localScale.ToString("F3"));
    foreach(var t in root.GetComponentsInChildren<Transform>(true))
    {
     var item=t.GetComponent<KongZuo.KZInteractable>();
     if(item!=null)log.AppendLine("ITEM "+item.name+" action="+item.action+" active="+item.gameObject.activeInHierarchy+" p="+t.position.ToString("F3"));
     var r=t.GetComponent<Renderer>();if(r==null)continue;
     var b=r.bounds;
     if((b.center.z< -12 && b.center.z> -21 && b.size.x<8)||(t.name.Contains("Seat")&&t.name.Contains("P0")))
      log.AppendLine("RENDER "+t.name+" on="+r.enabled+" active="+t.gameObject.activeInHierarchy+" bounds="+b.ToString("F3")+" path="+AssetDatabase.GetAssetPath(t.GetComponent<MeshFilter>()==null?null:t.GetComponent<MeshFilter>().sharedMesh));
    }
   }
   File.WriteAllText("Logs/ExternalProps/"+Path.GetFileNameWithoutExtension(path)+"-fix-audit.txt",log.ToString());
  }
  Debug.Log("KZ_FIX_AUDIT_OK");
 }
 static void MoveToWall(Transform item,float plane,bool xAxis,bool positiveSide)
 {
  if(item==null)return;
  var b=BoundsOf(item.gameObject);
  float edge=xAxis?(positiveSide?b.max.x:b.min.x):(positiveSide?b.max.z:b.min.z);
  var move=xAxis?new Vector3(plane-edge,0,0):new Vector3(0,0,plane-edge);
  item.position+=move;PrefabUtility.RecordPrefabInstancePropertyModifications(item);
 }
 static void FixFrontFurniture()
 {
  foreach(string name in new[]{"02_FrontRooms__TIMBER__Furniture_CenterRight","02_FrontRooms__TIMBER_DARK__Furniture_CenterRight","02_FrontRooms__TIMBER_LIGHT__Furniture_CenterRight"})
   Hide(name);
  MoveToWall(Find("办公室壁架（装饰）"),-9.83f,true,false);

  foreach(var root in current.GetRootGameObjects())
   foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
    if(filter.sharedMesh!=null&&AssetDatabase.GetAssetPath(filter.sharedMesh).EndsWith("/02_FrontRooms__SIGN_LEDGER.obj"))
     MoveToWall(filter.transform,-12.565f,false,true);
  var dust=Find("TicketDust");if(dust!=null)dust.gameObject.SetActive(false);
  var work=Find("周明生场务物品记录");
  if(work!=null)
  {
   var item=work.GetComponent<KongZuo.KZInteractable>();
   if(item!=null)item.displayName="周明生的场务物品记录";

  }
 }
 static void AlignDirectoryAndCabinet()
 {
  var body=Find("02_FrontRooms__TIMBER_DARK__Furniture_WestCabinet");
  var front=Find("02_FrontRooms__TIMBER_LIGHT__Furniture_WestCabinet");
  foreach(var part in new[]{body,front})
  {
   if(part==null)throw new Exception("Cabinet part missing.");
   var original=PrefabUtility.GetCorrespondingObjectFromSource(part);
   if(original==null)throw new Exception("Cabinet source missing.");
   part.localPosition=original.localPosition;
  }
  float delta=-12.535f-BoundsOf(body.gameObject).max.z;
  foreach(var part in new[]{body,front}){part.position+=Vector3.forward*delta;PrefabUtility.RecordPrefabInstancePropertyModifications(part);}
  MoveToWall(Find("02_FrontRooms__TIMBER_DARK__Frame_Window_E"),-12.535f,false,true);
  // Move only the cabinet's three book spines in the already-cleaned shared paper mesh.
  foreach(var root in current.GetRootGameObjects())
  foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
  {
   if(filter.sharedMesh==null||AssetDatabase.GetAssetPath(filter.sharedMesh)!=CleanMeshes+"/02_FrontRooms__PAPER_WithoutOldProps.asset")continue;
   var mesh=filter.sharedMesh;var vertices=mesh.vertices;
   for(int i=0;i<vertices.Length;i++)
   {
    var world=filter.transform.TransformPoint(vertices[i]);
    if(Mathf.Abs(world.x)>8.7f&&Mathf.Abs(world.x)<9.8f&&world.y>1.45f&&world.y<1.79f&&world.z> -13.65f&&world.z< -13.57f)
     vertices[i]+=filter.transform.InverseTransformVector(Vector3.forward*delta);
   }
   mesh.vertices=vertices;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
  }
 }
 static void RemoveBenchCollision()
 {
  const string sourcePath="Assets/KongZuo/Art/Art01/Meshes/MakeupDoor_4b02dbdec12156445884ab72d2889ab6_-2432090755550338912_Doorway.asset";
  string output=CleanMeshes+"/99_Collision_WithoutFoyerBench.asset";
  var original=AssetDatabase.LoadAssetAtPath<Mesh>(sourcePath);
  if(original==null)throw new Exception("Original building collision mesh missing.");
  int applied=0;
  foreach(var root in current.GetRootGameObjects())
  foreach(var collider in root.GetComponentsInChildren<MeshCollider>(true))
  {
   string path=AssetDatabase.GetAssetPath(collider.sharedMesh);
   if(path!=sourcePath&&path!=output)continue;
   var copy=UnityEngine.Object.Instantiate(original);var vertices=original.vertices;int removed=0;
   Func<int,bool> inside=index=>
   {
    var p=collider.transform.TransformPoint(vertices[index]);
    return p.x>=-3.80f&&p.x<=-1.60f&&p.y>=.40f&&p.y<=.58f&&p.z>=-13.55f&&p.z<=-13.05f;
   };
   for(int sub=0;sub<original.subMeshCount;sub++)
   {
    var faces=original.GetTriangles(sub);var kept=new System.Collections.Generic.List<int>();
    for(int i=0;i<faces.Length;i+=3)
    {
     bool a=inside(faces[i]),b=inside(faces[i+1]),c=inside(faces[i+2]);
     if(a&&b&&c){removed++;continue;}
     if(a||b||c)throw new Exception("Bench is connected to unrelated collision geometry; refusing to alter it.");
     kept.Add(faces[i]);kept.Add(faces[i+1]);kept.Add(faces[i+2]);
    }
    copy.SetTriangles(kept,sub);
   }
   if(removed!=12||copy.triangles.Length/3!=1236)throw new Exception("Collision audit changed; refusing to alter it.");
   if(!copy.vertices.SequenceEqual(original.vertices))throw new Exception("Collision vertex data unexpectedly changed.");
   var saved=AssetDatabase.LoadAssetAtPath<Mesh>(output);
   if(saved==null){AssetDatabase.CreateAsset(copy,output);saved=copy;}
   else{EditorUtility.CopySerialized(copy,saved);UnityEngine.Object.DestroyImmediate(copy);EditorUtility.SetDirty(saved);}
   collider.sharedMesh=saved;PrefabUtility.RecordPrefabInstancePropertyModifications(collider);applied++;
  }
  if(applied==0)throw new Exception("Building collision instance missing.");
  Debug.Log("KZ_FOYER_BENCH_COLLISION_REMOVED "+current.name+" instances="+applied);
 }
 static void HideLegacySeating()
 {
  int count=0;
  var art=Find("KZ_OperaHouse_P0_V03_From11_Integrated");if(art==null)throw new Exception("Building missing.");
  foreach(var filter in art.GetComponentsInChildren<MeshFilter>(true))
  {
   string path=AssetDatabase.GetAssetPath(filter.sharedMesh);
   if(!path.Contains("/08_Seat")&&!path.Contains("/09_FinishChair"))continue;
   var renderer=filter.GetComponent<Renderer>();if(renderer==null)continue;
   renderer.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);count++;
  }
  if(count==0)throw new Exception("No legacy chair meshes found.");
  Debug.Log("KZ_LEGACY_CHAIR_RENDERERS_HIDDEN "+current.name+" count="+count);
 }
 const string SeatingPath="Assets/KongZuo/Art/Art01/Prefabs/MainlineExternalSeating.prefab";
 static void BuildSeatingTemplate()
 {
  var scene=EditorSceneManager.OpenScene(Scenes[1],OpenSceneMode.Single);
  var chairs=scene.GetRootGameObjects().Where(r=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r).Contains("chinese_yoke-back_armchair")).ToArray();
  if(chairs.Length!=117)throw new Exception("Expected the 117 authored chapter-one chairs; found "+chairs.Length);
  var parent=new GameObject("KZ_ExternalSeating_9x13");
  try
  {
   foreach(var chair in chairs)
   {
    var asset=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(chair));
    var copy=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent.transform);
    copy.name=chair.name;copy.transform.SetPositionAndRotation(chair.transform.position,chair.transform.rotation);copy.transform.localScale=chair.transform.localScale;
   }
   PrefabUtility.SaveAsPrefabAsset(parent,SeatingPath);
  }
  finally{UnityEngine.Object.DestroyImmediate(parent);}
 }
 static void SyncPrologueSeating()
 {
  current=EditorSceneManager.OpenScene(Scenes[0],OpenSceneMode.Single);
  var old=Find("KZ_ExternalSeating_9x13")??Find("MainlineExternalSeating");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
  var seating=AssetDatabase.LoadAssetAtPath<GameObject>(SeatingPath);
  var instance=(GameObject)PrefabUtility.InstantiatePrefab(seating,current);instance.name="KZ_ExternalSeating_9x13";PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
  // Copy the existing chapter's visibility for the authored building seats; keep their story colliders.
  var sourceScene=EditorSceneManager.OpenScene(Scenes[1],OpenSceneMode.Additive);
  var sourceArt=sourceScene.GetRootGameObjects().First(r=>r.name=="KZ_OperaHouse_P0_V03_From11_Integrated");
  var targetArt=current.GetRootGameObjects().First(r=>r.name==sourceArt.name);
  var states=new System.Collections.Generic.Dictionary<long,bool>();
  foreach(var renderer in sourceArt.GetComponentsInChildren<Renderer>(true))
  {
   var original=PrefabUtility.GetCorrespondingObjectFromSource(renderer);
   if(original!=null&&AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original,out string guid,out long id))states[id]=renderer.enabled&&renderer.gameObject.activeInHierarchy;
  }
  foreach(var renderer in targetArt.GetComponentsInChildren<Renderer>(true))
  {
   var filter=renderer.GetComponent<MeshFilter>();
   string meshPath=filter==null?"":AssetDatabase.GetAssetPath(filter.sharedMesh);
   if(!meshPath.Contains("/08_Seat")&&!meshPath.Contains("/09_FinishChair"))continue;
   var original=PrefabUtility.GetCorrespondingObjectFromSource(renderer);
   if(original!=null&&AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original,out string guid,out long id)&&states.TryGetValue(id,out bool enabled))
   {renderer.enabled=enabled;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
  }
  EditorSceneManager.CloseScene(sourceScene,true);
  EditorSceneManager.MarkSceneDirty(current);EditorSceneManager.SaveScene(current);
 }
 [MenuItem("Tools/空座/外部模型/修复截图反馈八项")]
 public static void FixReportedIssues()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play mode first.");
  if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
  BuildSeatingTemplate();Apply();SyncPrologueSeating();AssetDatabase.SaveAssets();
  Debug.Log("KZ_SCREENSHOT_FIXES_OK");
 }
 [MenuItem("Tools/空座/外部模型/删除悬空装饰笔记本")]
 public static void RemoveFloatingNotebook()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play mode first.");
  if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
  Directory.CreateDirectory("Logs/ExternalProps");
  foreach(string path in Scenes)
  {
   current=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
   layer=Find(LayerName);if(layer==null)throw new Exception("External placement missing.");
   var notebook=layer.Find("账单外壳（装饰）");
   if(notebook!=null)
   {
    if(notebook.GetComponentsInChildren<KongZuo.KZInteractable>(true).Length!=0)
     throw new Exception("Refusing to remove an interactable as decoration.");
    UnityEngine.Object.DestroyImmediate(notebook.gameObject);
   }
   if(layer.Find("账单外壳（装饰）")!=null)throw new Exception("Floating notebook remains.");
   ValidateCurrent();KZArt01TaskSetup.ValidateScene(current);
   EditorSceneManager.MarkSceneDirty(current);
   if(!EditorSceneManager.SaveScene(current))throw new Exception("Cannot save "+path);
  }
  Debug.Log("KZ_FLOATING_NOTEBOOK_REMOVED_OK");
 }
 [MenuItem("Tools/空座/外部模型/调整电话台灯和记录")]
 public static void AdjustDeskProps()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play mode first.");
  if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
  foreach(string path in Scenes)
  {
   current=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
   layer=Find(LayerName);if(layer==null)throw new Exception("Placement layer missing.");
   var office=Find("办公室木桌（装饰）");var counter=Find("售票资料台（装饰）");
   if(office==null||counter==null)throw new Exception("Desk missing.");
   foreach(string name in new[]{"台灯（装饰）","电话（装饰）","化妆间边桌（装饰）","化妆间旧镜（装饰）","后台木凳（装饰）","场务记录桌签","场务记录桌签底板"})
   {
    var item=Find(name);if(item!=null)UnityEngine.Object.DestroyImmediate(item.gameObject);
   }
   Bind("办公室电话","Phone_001.fbx",On(office.gameObject,-8.10f,-16.05f),.52f);
   if(Find("办公室电话")==null)Place("Phone_001.fbx","电话（装饰）",On(office.gameObject,-8.10f,-16.05f),.52f);
   Place("台灯.glb","台灯（装饰）",On(office.gameObject,-7.45f,-15.85f),.50f,145);
   Bind("周明生场务物品记录","Notebook_Leather",On(counter.gameObject,6.25f,-16.20f),.43f);
   ValidateCurrent();KZArt01TaskSetup.ValidateScene(current);
   EditorSceneManager.MarkSceneDirty(current);
   if(!EditorSceneManager.SaveScene(current))throw new Exception("Could not save "+path);
  }
  AssetDatabase.SaveAssets();Debug.Log("KZ_DESK_ADJUSTMENTS_OK");
 }
 const string CleanMeshes="Assets/KongZuo/Art/Art01/ExternalMeshes";
 static void CleanSharedPlaceholders()
 {
  if(!AssetDatabase.IsValidFolder(CleanMeshes))AssetDatabase.CreateFolder("Assets/KongZuo/Art/Art01","ExternalMeshes");
  var report=new StringBuilder();
  int changed=0;
  foreach(var root in current.GetRootGameObjects())
  foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
  {
   var originalFilter=PrefabUtility.GetCorrespondingObjectFromSource(filter);
   if(originalFilter==null||originalFilter.sharedMesh==null)continue;
   var original=originalFilter.sharedMesh;
   string path=AssetDatabase.GetAssetPath(original);
   string name=Path.GetFileNameWithoutExtension(path);
   bool whole=name=="02_FrontRooms__BLACK"||name=="02_FrontRooms__METAL"||name=="02_FrontRooms__WARM_LIGHT";
   bool lamp=name=="02_FrontRooms__BRASS";
   bool paper=name=="02_FrontRooms__PAPER";
   if(!whole&&!lamp&&!paper)continue;
   var renderer=filter.GetComponent<Renderer>();
   if(renderer==null)throw new Exception("Missing renderer for "+name);
   var vertices=original.vertices;
   Func<int,bool> selected=index=>
   {
    Vector3 p=filter.transform.TransformPoint(vertices[index]);
    float x=Mathf.Abs(p.x);
    if(whole)return x>5.3f&&x<9.1f&&p.y>=.79f&&p.y<=2.5f&&p.z> -17.5f&&p.z< -15.1f;
    if(lamp)return x>=6.469f&&x<=6.868f&&p.y>=.849f&&p.y<=1.776f&&p.z>=-16.007f&&p.z<=-15.599f;
    return x>=6.799f&&x<=7.641f&&p.y>=1.006f&&p.y<=1.030f&&p.z>=-15.686f&&p.z<=-15.114f;
   };
   if(whole)
   {
    // Whole-object hiding is permitted only after every vertex is confirmed inside the prop envelope.
    if(!Enumerable.Range(0,vertices.Length).All(selected))throw new Exception("Unexpected geometry outside the confirmed prop envelope: "+name);
    renderer.enabled=false;
    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
    report.AppendLine(name+": hidden confirmed placeholder renderer; source triangles="+original.triangles.Length/3);
    changed++;continue;
   }
   var copy=UnityEngine.Object.Instantiate(original);
   int removed=0,retained=0;
   for(int sub=0;sub<original.subMeshCount;sub++)
   {
    var faces=original.GetTriangles(sub);
    var kept=new System.Collections.Generic.List<int>();
    for(int i=0;i<faces.Length;i+=3)
    {
     // Require all three vertices inside the identified prop, never a broad triangle-centre cut.
     if(selected(faces[i])&&selected(faces[i+1])&&selected(faces[i+2])){removed++;continue;}
     kept.Add(faces[i]);kept.Add(faces[i+1]);kept.Add(faces[i+2]);retained++;
    }
    copy.SetTriangles(kept,sub);
   }
   if(removed==0||retained==0)throw new Exception("Unexpected shared prop geometry in "+name+"; removed="+removed+" retained="+retained);
   string output=CleanMeshes+"/"+name+"_WithoutOldProps.asset";
   var saved=AssetDatabase.LoadAssetAtPath<Mesh>(output);
   if(saved==null){AssetDatabase.CreateAsset(copy,output);saved=copy;}
   else{EditorUtility.CopySerialized(copy,saved);UnityEngine.Object.DestroyImmediate(copy);EditorUtility.SetDirty(saved);}
   filter.sharedMesh=saved;
   PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
   // Update an existing collider only when it used this exact original visual mesh.
   foreach(var collider in filter.GetComponents<MeshCollider>())
    if(collider.sharedMesh==original){collider.sharedMesh=saved;PrefabUtility.RecordPrefabInstancePropertyModifications(collider);}
   renderer.enabled=true;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
   report.AppendLine(name+": removed="+removed+" triangles; retained="+retained+"; source unchanged; replacement="+output);
   changed++;
  }
  if(changed!=5)throw new Exception("Expected five confirmed placeholder mesh groups, found "+changed);
  File.WriteAllText("Logs/ExternalProps/"+Path.GetFileNameWithoutExtension(current.path)+"-cleanup.txt",report.ToString());
 }
 [MenuItem("Tools/空座/外部模型/清理共享网格旧占位")]
 public static void Cleanup()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play mode first.");
  if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
  Directory.CreateDirectory("Logs/ExternalProps");
  foreach(string path in Scenes)
  {
   current=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
   layer=Find(LayerName);if(layer==null)throw new Exception("External placement layer missing.");
   CleanSharedPlaceholders();
   ValidateCurrent();KZArt01TaskSetup.ValidateScene(current);
   EditorSceneManager.MarkSceneDirty(current);
   if(!EditorSceneManager.SaveScene(current))throw new Exception("Cannot save "+path);
  }
  AssetDatabase.SaveAssets();Debug.Log("KZ_EXTERNAL_CLEANUP_OK");
 }

 static void ValidateCurrent()
 {
  Physics.SyncTransforms();
  var roots=current.GetRootGameObjects();
  if(roots.Count(r=>r.name==LayerName)!=1)throw new Exception("Duplicate external prop layers.");
  if(roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(false)).Count(c=>c.enabled)!=1)throw new Exception("Expected one active gameplay camera.");
  if(roots.SelectMany(r=>r.GetComponentsInChildren<KongZuo.KZFirstPersonPlayer>(true)).Count()!=1)throw new Exception("Expected one player.");
  if(roots.SelectMany(r=>r.GetComponentsInChildren<KongZuo.KZStoryDirector>(true)).Count()!=1)throw new Exception("Expected one story director.");
  foreach(var root in current.GetRootGameObjects())
   foreach(var t in root.GetComponentsInChildren<Transform>(true))
    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script "+t.name);
  foreach(var r in layer.GetComponentsInChildren<Renderer>(true))
   foreach(var m in r.sharedMaterials)if(m==null||m.shader==null||!m.shader.isSupported)throw new Exception("Invalid material "+r.name);
  var items=current.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<KongZuo.KZInteractable>(true)).Where(i=>i.GetComponentInChildren<Renderer>(true)!=null && i.transform.Find("ExternalModel")!=null).ToArray();
  var report=new StringBuilder();
  foreach(var item in items)
  {
   var box=item.GetComponent<BoxCollider>();if(box==null||!box.enabled)throw new Exception("No interaction collider "+item.name);
   // Sample the actual first-person ray from four possible approaches, including counter front and sides.
   if(!item.gameObject.activeInHierarchy){report.AppendLine(item.name+" inactive story prop, reference retained");continue;}
   bool reachable=false;
   foreach(var offset in new[]{new Vector3(0,0,1.1f),new Vector3(0,0,-1.1f),new Vector3(-1.1f,0,0),new Vector3(1.1f,0,0)})
   {
    var eye=new Vector3(box.bounds.center.x,1.62f,box.bounds.center.z)+offset;
    var delta=box.bounds.center-eye;
    if(Physics.Raycast(eye,delta.normalized,out var hit,3.3f,~0,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<KongZuo.KZInteractable>()==item){reachable=true;break;}
   }
   report.AppendLine(item.name+" reachable="+reachable+" bounds="+box.bounds);
   if(!reachable)throw new Exception("Interaction obstructed: "+item.name);
  }
  File.WriteAllText("Logs/ExternalProps/"+Path.GetFileNameWithoutExtension(current.path)+"-validation.txt",report.ToString());
 }
 [MenuItem("Tools/空座/外部模型/验证与截图")]
 public static void Review()
 {
  Directory.CreateDirectory("Logs/ExternalProps");
  foreach(string path in Scenes)
  {
   current=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);layer=Find(LayerName);if(layer==null)throw new Exception("Missing layer");
   ValidateCurrent();KZArt01TaskSetup.ValidateScene(current);
   var summary=new StringBuilder();
   foreach(var root in current.GetRootGameObjects())
    foreach(var r in root.GetComponentsInChildren<Renderer>(false))
     if(r.transform.IsChildOf(layer)||r.transform.parent!=null&&r.transform.parent.name=="ExternalModel")
      summary.AppendLine(r.name+" "+r.bounds.ToString("F3"));
   File.WriteAllText("Logs/ExternalProps/"+Path.GetFileNameWithoutExtension(path)+"-bounds.txt",summary.ToString());
   var cam=new GameObject("TemporaryReviewCamera").AddComponent<Camera>();cam.fieldOfView=65;
   Vector3[] eye={new Vector3(-6.9f,1.65f,-14.4f),new Vector3(7.2f,1.7f,-14.55f),new Vector3(-2.55f,1.7f,-15.9f),new Vector3(-7.5f,1.7f,14.5f),new Vector3(7.8f,1.7f,-15.0f),new Vector3(0,2f,-9.7f),new Vector3(-6.6f,1.8f,-15.1f)};
   Vector3[] focus={new Vector3(-7.7f,1,-16),new Vector3(7.3f,1,-16.3f),new Vector3(-4.42f,.8f,-16.25f),new Vector3(-7.5f,1,17),new Vector3(8f,1.4f,-12.56f),new Vector3(0,1f,2.5f),new Vector3(-9.5f,1.65f,-16.3f)};
   string[] names={"office","ticket","foyer","makeup","directory","seating","wall-shelf"};
   for(int i=0;i<eye.Length;i++){cam.transform.position=eye[i];cam.transform.LookAt(focus[i]);Capture(cam,Path.GetFileNameWithoutExtension(path)+"-"+names[i]);}
   UnityEngine.Object.DestroyImmediate(cam.gameObject);
  }
  Debug.Log("KZ_EXTERNAL_REVIEW_OK");
 }

 static void Capture(Camera camera,string name)
 {
 var rt=new RenderTexture(1600,1200,24);var px=new Texture2D(1600,1200,TextureFormat.RGB24,false);var active=RenderTexture.active;
 camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;px.ReadPixels(new Rect(0,0,1600,1200),0,0);px.Apply();File.WriteAllBytes("Logs/ExternalProps/"+name+".png",px.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(px);
 }
}
#endif
