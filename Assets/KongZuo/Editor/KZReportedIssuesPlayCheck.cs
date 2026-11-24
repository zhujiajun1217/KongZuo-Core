#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using KongZuo;

[InitializeOnLoad]
public static class KZReportedIssuesPlayCheck
{
 const string Pending="KZ_ScreenshotFix_PlayCheck";
 const string SaveKey="KongZuo.P0.Progress.v1";
 static double readyAt;
 static KZReportedIssuesPlayCheck()
 {
  EditorApplication.playModeStateChanged+=Changed;
  if(SessionState.GetBool(Pending,false))
   EditorApplication.delayCall+=Resume;
 }
 public static void Run()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play mode first.");
  Directory.CreateDirectory("Logs/ExternalProps");
  SessionState.SetBool("KZ_FixCheck_HadSave",PlayerPrefs.HasKey(SaveKey));
  SessionState.SetString("KZ_FixCheck_Save",PlayerPrefs.GetString(SaveKey,""));
  SessionState.SetInt("KZ_FixCheck_Stage",0);
  SessionState.EraseString("KZ_FixCheck_Error");
  SessionState.SetBool(Pending,true);
  BeginScene();
 }
 static void Resume()
 {
  if(!SessionState.GetBool(Pending,false))return;
  if(EditorApplication.isPlaying){readyAt=EditorApplication.timeSinceStartup+3;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
 }
 static void Changed(PlayModeStateChange state)
 {
  if(!SessionState.GetBool(Pending,false))return;
  if(state==PlayModeStateChange.EnteredPlayMode)Resume();
  if(state==PlayModeStateChange.EnteredEditMode)
   EditorApplication.delayCall+=()=>
   {
    if(SessionState.GetInt("KZ_FixCheck_Stage",0)<2)BeginScene();
    else Finish();
   };
 }
 static void BeginScene()
 {
  PlayerPrefs.DeleteKey(SaveKey);PlayerPrefs.Save();
  int stage=SessionState.GetInt("KZ_FixCheck_Stage",0);
  EditorSceneManager.OpenScene(stage==0?"Assets/KongZuo/Scenes/01_Prologue.unity":"Assets/KongZuo/Scenes/02_Chapter01.unity",OpenSceneMode.Single);
  EditorApplication.isPlaying=true;
 }
 static void ClosePage()
 {
  var hud=UnityEngine.Object.FindFirstObjectByType<KZHud>();
  typeof(KZHud).GetMethod("ClosePage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);
 }
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 static void Tick()
 {
  if(EditorApplication.timeSinceStartup<readyAt)return;
  EditorApplication.update-=Tick;
  int stage=SessionState.GetInt("KZ_FixCheck_Stage",0);
  try
  {
   var scene=SceneManager.GetActiveScene();
   var roots=scene.GetRootGameObjects();
   var items=roots.SelectMany(r=>r.GetComponentsInChildren<KZInteractable>(true)).ToArray();
   var log=new StringBuilder();
   var session=KZGameSession.EnsureExists();
   ClosePage();
   var generated=roots.First(r=>r.name=="KZ_ExternalProps_Mainline");
   Require(!generated.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("KZ_")&&t!=generated.transform&&t.name.Contains("替身")),"Runtime fallback visual appeared under external props.");
   foreach(var item in items.Where(i=>i.transform.Find("ExternalModel")!=null))
    Require(!item.GetComponentsInChildren<Transform>(true).Any(t=>t.name.Contains("替身")),"Runtime fallback visual added to "+item.name);
   int chairs=roots.Where(r=>r.name.StartsWith("chinese_yoke-back_armchair")).Count();
   var seating=roots.FirstOrDefault(r=>r.name=="KZ_ExternalSeating_9x13");
   if(seating!=null)chairs+=seating.transform.childCount;
   Require(chairs==117,"Expected 117 external chairs, found "+chairs);
   foreach(var root in roots.Where(r=>r.name=="KZ_OperaHouse_P0_V03_From11_Integrated"))
   foreach(var filter in root.GetComponentsInChildren<MeshFilter>(false))
   {
    string path=AssetDatabase.GetAssetPath(filter.sharedMesh);
    if(path.Contains("/08_Seat")||path.Contains("/09_FinishChair"))
    {
     var renderer=filter.GetComponent<Renderer>();Require(renderer==null||!renderer.enabled,"Legacy chair still visible: "+filter.name);
    }
   }
   log.AppendLine("117 external chairs loaded; old chair renderers hidden; no legacy runtime fallback visuals on external models.");
   Physics.SyncTransforms();
   var benchHits=Physics.OverlapCapsule(new Vector3(-2.7f,.58f,-13.3f),new Vector3(-2.7f,1.5f,-13.3f),.22f,~0,QueryTriggerInteraction.Ignore);
   Require(benchHits.Length==0,"Deleted foyer bench still blocks walking: "+string.Join(",",benchHits.Select(c=>c.name)));
   log.AppendLine("Deleted foyer bench area has no blocking collider.");
   if(stage==0)
   {
    var phone=items.First(i=>i.action==KZActionKind.Phone);
    var box=phone.GetComponent<BoxCollider>();
    var eye=new Vector3(box.bounds.center.x,1.62f,-14.9f);
    Require(Physics.Raycast(eye,(box.bounds.center-eye).normalized,out var hit,3.3f,~0,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<KZInteractable>()==phone,"Office phone cannot be reached from desk front.");
    phone.Interact();ClosePage();
    Require(session.HasFlag(KZFlags.Phone),"Reading new phone did not complete Phone objective.");
    log.AppendLine("New phone hit from desk front; Interact + close page completes Phone flag.");
   }
   else
   {
    session.TryAddFlag(KZFlags.Ritual);session.TryAddFlag(KZFlags.Photo);session.TryAddFlag(KZFlags.Ticket);session.TryAddFlag(KZFlags.PhotoAligned);
    var director=KZStoryDirector.Current;
    Require(director.Objective.Contains("场务")&&director.Objective.Contains("售票"),"WorkRecord objective lacks a location.");
    var record=items.First(i=>i.action==KZActionKind.WorkRecord);
    var box=record.GetComponent<BoxCollider>();
    var eye=new Vector3(box.bounds.center.x,1.62f,-14.75f);
    Require(Physics.Raycast(eye,(box.bounds.center-eye).normalized,out var hit,3.3f,~0,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<KZInteractable>()==record,"WorkRecord not reachable from ticket desk front.");
    record.Interact();ClosePage();
    Require(session.HasFlag(KZFlags.WorkRecord),"Reading WorkRecord failed to set the story flag.");
    Require(!director.Objective.Contains("阅读周明生"),"WorkRecord objective did not advance.");
    log.AppendLine("Photo-aligned objective points to ticket desk; record hit from front; reading advances story to relic identification.");
   }
   File.WriteAllText("Logs/ExternalProps/"+scene.name+"-playcheck.txt",log.ToString());
   Debug.Log("KZ_FIX_PLAYCHECK_OK "+scene.name);
   SessionState.SetInt("KZ_FixCheck_Stage",stage+1);
  }
  catch(Exception error)
  {
   SessionState.SetString("KZ_FixCheck_Error",error.ToString());
   SessionState.SetInt("KZ_FixCheck_Stage",2);Debug.LogException(error);
  }
  EditorApplication.isPlaying=false;
 }
 static void Finish()
 {
  if(SessionState.GetBool("KZ_FixCheck_HadSave",false))PlayerPrefs.SetString(SaveKey,SessionState.GetString("KZ_FixCheck_Save",""));
  else PlayerPrefs.DeleteKey(SaveKey);
  PlayerPrefs.Save();SessionState.SetBool(Pending,false);
  string error=SessionState.GetString("KZ_FixCheck_Error","");
  SessionState.EraseString("KZ_FixCheck_Error");
  Debug.Log(string.IsNullOrEmpty(error)?"KZ_ALL_FIX_PLAYCHECKS_OK":"KZ_FIX_PLAYCHECK_FAILED "+error);
  EditorApplication.Exit(string.IsNullOrEmpty(error)?0:1);
 }
}
#endif
