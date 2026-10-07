using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
#endif
public static class LiveRoundValidation {
#if UNITY_EDITOR
 public static void Play() {
  PlayerSettings.companyName="CodexLocalValidation";PlayerSettings.productName="HotelLiveRoundValidation";
  UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Helper.unity");EditorApplication.EnterPlaymode();
 }
 public static void Build() {
  PlayerSettings.companyName="CodexLocalValidation";
  PlayerSettings.productName="HotelLiveRoundValidation";
  var path=System.IO.Path.GetFullPath("Build/HotelLiveRound.exe");Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
  var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
   scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
   locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
  });
  Debug.Log("LIVE_BUILD "+result.summary.result+" errors="+result.summary.totalErrors);
  EditorApplication.Exit(result.summary.result==BuildResult.Succeeded?0:1);
 }
#endif
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Start() {
  var args=Environment.GetCommandLineArgs();if(!args.Contains("--live-round-validation"))return;
  var root=new GameObject("Local live round validation monitor");UnityEngine.Object.DontDestroyOnLoad(root);root.AddComponent<LiveRoundMonitor>();
 }
}
public sealed class LiveRoundMonitor:MonoBehaviour {
 string role,path,scenario;bool interrupting;HauntedHotelMultiplayer session;GameSceneController scene;
 float begin,spinStarted;int lastVersion,releaseCount;bool lastReleased,loggedChosen,loggedLanded,loggedPulse,loggedAttic,loggedReveal,loggedReturn,loggedComplete;
 bool connecting,readyBound,finished,slowApplied;int acknowledgements;GhostSelectionPresentation presentation;int checks;
 static FieldInfo Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance);
 static T Get<T>(object o,string name)=>(T)Field(o,name).GetValue(o);
 static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
 static void Quit(int code){
#if UNITY_EDITOR
  if(Application.isEditor){EditorApplication.Exit(code);return;}
#endif
  Application.Quit(code);
 }
 string FilePath(string name)=>System.IO.Path.Combine(path,name);
 void Note(string message){Debug.Log("LIVE_"+role+" "+message);File.AppendAllText(FilePath(role+"-events.txt"),Time.realtimeSinceStartup.ToString("F3")+" "+message+"\n");}
 void Check(bool value,string message){if(!value){Fail(message);return;}checks++;Note("CHECK "+checks+" "+message);}
 void Fail(string message){if(finished)return;finished=true;Note("FAIL "+message);File.WriteAllText(FilePath(role+"-result.txt"),"FAIL "+message);Quit(1);}
 void Awake(){
  var args=Environment.GetCommandLineArgs();role=args[Array.IndexOf(args,"--role")+1];path=args[Array.IndexOf(args,"--evidence")+1];Directory.CreateDirectory(path);
  scenario=args.Contains("--scenario")?args[Array.IndexOf(args,"--scenario")+1]:"normal";
  begin=Time.realtimeSinceStartup;SceneManager.sceneLoaded+=Loaded;Application.runInBackground=true;
  // This test uses its own PlayerSettings product namespace; no user identity is touched.
  PlayerPrefs.SetString("HauntedHotel.PlayerId",Guid.NewGuid().ToString("N"));PlayerPrefs.Save();
  Application.logMessageReceived+=OnLog;
 }
 void OnLog(string message,string trace,LogType type){if(type==LogType.Exception)File.AppendAllText(FilePath(role+"-exceptions.txt"),message+"\n"+trace+"\n");}
 void Loaded(Scene loaded,LoadSceneMode mode){
  session=FindFirstObjectByType<HauntedHotelMultiplayer>();if(session&&!connecting)Set(session,"_AutoCreateOnStart",false);
  if(loaded.name=="Game"){
   scene=FindFirstObjectByType<GameSceneController>();presentation=FindFirstObjectByType<GhostSelectionPresentation>();
   if(role=="peer"&&!slowApplied){
    // A slower peer verifies the real server barrier waits for the entire presentation.
    var prefab=Get<StoryUI>(presentation,"_AtticPrefab");prefab.GetComponent<RoundIntroductionSettings>().TimingScale=1.25f;slowApplied=true;
   }
   Note("SCENE_GAME");
  }
 }
 void Update(){
  if(finished||interrupting)return;if(Time.realtimeSinceStartup-begin>240){Fail("Timed out: "+(session?session.Status:"No multiplayer bootstrap"));return;}
  if(!session)session=FindFirstObjectByType<HauntedHotelMultiplayer>();
  if(session&&!connecting){
   if(role=="host"){connecting=true;Note("CREATE_LOBBY");session.CreateLobby();}
   else if(File.Exists(FilePath("room-code.txt"))){connecting=true;Note("JOIN_LOBBY");session.JoinLobby(File.ReadAllText(FilePath("room-code.txt")).Trim());}
  }
  if(session&&connecting&&session.State==HotelSessionState.Failed){Fail("Networking blocked: "+session.Status);return;}
  if(session&&session.ReadyToPlay){
   if(role=="host"&&!File.Exists(FilePath("room-code.txt"))){File.WriteAllText(FilePath("room-code.txt"),session.Code);Note("HOST_CONNECTED");}
   if(role=="peer"&&!File.Exists(FilePath("peer-connected.txt"))){File.WriteAllText(FilePath("peer-connected.txt"),"connected");Note("PEER_CONNECTED");}
   if(!readyBound&&File.Exists(FilePath("peer-connected.txt"))&&session.PlayerCount>= (role=="host"?2:0)){
    // Use the production public ready-zone callback; movement is outside this test.
    session.BindReadyZone(p=>true);readyBound=true;Note("READY_ZONE_BOUND");
   }
  }
  var local=HotelPlayer.LocalPlayer;if(!local||string.IsNullOrEmpty(local.RoundRoster))return;
  if(!scene)scene=FindFirstObjectByType<GameSceneController>();if(!presentation)presentation=FindFirstObjectByType<GhostSelectionPresentation>();
  if(local.RoundVersion!=lastVersion){
   lastVersion=local.RoundVersion;spinStarted=Time.realtimeSinceStartup;loggedChosen=loggedLanded=loggedPulse=loggedAttic=loggedReveal=loggedReturn=loggedComplete=false;lastReleased=false;releaseCount=0;
   Note("ROUND "+lastVersion+" GHOST "+local.GhostId+" ROSTER "+local.RoundRoster);
   File.WriteAllText(FilePath(role+"-round.txt"),lastVersion+"|"+local.GhostId+"|"+local.RoundRoster);
  }
  var watched=Get<bool>(scene,"_Watched");
  var graphic=Get<GhostSelectionWheel>(presentation,"_Graphic");
  if(graphic.Selected>=0&&!loggedLanded){loggedLanded=true;
   float spinSeconds=Time.realtimeSinceStartup-spinStarted;
   Check(spinSeconds>=6.5f&&spinSeconds<=7.5f,"ORIGINAL_SEVEN_SECOND_SPIN seconds="+spinSeconds.ToString("F3"));
   var roster=local.RoundRoster.Split(',').Select(uint.Parse).ToArray();int index=Array.IndexOf(roster,local.GhostId);
   var pointer=Get<RectTransform>(presentation,"_Pointer");float expected=-(1800+(index+.5f)*360/roster.Length);
   Check(graphic.Selected==index&&Mathf.Abs(Mathf.DeltaAngle(pointer.localEulerAngles.z,expected))<.02f,"LANDED_MATCHES_SHARED_GHOST");
  }
  if(graphic.SelectionPulse>.05f&&!loggedPulse){loggedPulse=true;Check(loggedLanded,"SELECTED_SLICE_ANIMATES_AFTER_LANDING");}
  var attic=Get<StoryUI>(presentation,"_Attic");
  if(attic&&!loggedAttic){loggedAttic=true;Check(loggedLanded,"ATTIC_AFTER_LANDING");Note("ATTIC_ENTER");if(role=="peer"&&scenario=="interrupted"&&!interrupting){interrupting=true;StartCoroutine(InterruptPeer());}}
  if(attic&&attic.Find("ghost").gameObject.activeSelf&&!loggedReveal){loggedReveal=true;Check(loggedAttic&&Mathf.Abs(((RectTransform)attic.Find("ghost").parent).anchoredPosition.y+2160)<.1f,"GHOST_REVEAL_AFTER_ASCENT");Note("GHOST_REVEAL");}
  if(attic&&attic.Find("return_blur").gameObject.activeSelf&&!loggedReturn){loggedReturn=true;Check(loggedReveal&&attic.Find("ghost").GetComponent<UnityEngine.UI.Image>().material.shader.name=="HauntedFish/AtticReturnBlur","BLURRED_RETURN_AFTER_REVEAL");Note("BLURRED_RETURN");}
  if(watched&&!loggedComplete){loggedComplete=true;acknowledgements++;Check(loggedReturn&&loggedPulse,"COMPLETION_AFTER_BLURRED_RETURN");Note("PRESENTATION_COMPLETE");File.WriteAllText(FilePath(role+"-completed.txt"),Time.realtimeSinceStartup.ToString("F3"));}
  if(local.RoundReleased&&!lastReleased){releaseCount++;lastReleased=true;Check(loggedComplete,"RELEASE_AFTER_LOCAL_COMPLETION");Note("ROUND_RELEASED");StartCoroutine(VerifyReleased(local));}
 }
 IEnumerator InterruptPeer(){
  yield return new WaitForSecondsRealtime(.25f);
  Check(!loggedComplete&&!HotelPlayer.LocalPlayer.RoundReleased,"INTERRUPT_DURING_CUTSCENE_BEFORE_ACKNOWLEDGEMENT");
  Note("DISCONNECT_DURING_ATTIC");File.WriteAllText(FilePath("peer-interrupted.txt"),lastVersion.ToString());
  session.LeaveLobby();yield return new WaitForSecondsRealtime(2);
  Check(!File.Exists(FilePath("peer-completed.txt")),"INTERRUPTED_PEER_NEVER_ACKNOWLEDGED");
  if(!finished){finished=true;File.WriteAllText(FilePath(role+"-result.txt"),"PASS "+checks);Quit(0);}
 }
 IEnumerator VerifyReleased(HotelPlayer local){
  yield return new WaitForSecondsRealtime(2);
  local.FinishGhostSelection(lastVersion);local.FinishGhostSelection(lastVersion);
  yield return new WaitForSecondsRealtime(.5f);
  if(scenario=="interrupted"){
   Check(role=="host"&&session.PlayerCount==1&&File.Exists(FilePath("peer-interrupted.txt")),"SUPPORTED_DISCONNECT_REMOVED_INTERRUPTED_PEER");
   Check(loggedComplete&&releaseCount==1&&acknowledgements==1,"SURVIVING_HOST_COMPLETES_AND_RELEASES_ONCE_AFTER_INTERRUPTION");
   if(!finished){finished=true;File.WriteAllText(FilePath(role+"-result.txt"),"PASS "+checks);Quit(0);}yield break;
  }
  Check(File.Exists(FilePath("host-completed.txt"))&&File.Exists(FilePath("peer-completed.txt")),"RELEASE_WAITED_FOR_BOTH_PEERS");
  Check(File.ReadAllText(FilePath("host-round.txt"))==File.ReadAllText(FilePath("peer-round.txt")),"PEERS_SHARE_VERSION_GHOST_ROSTER");
  Check(releaseCount==1&&acknowledgements==1&&local.RoundReleased,"EXACTLY_ONE_COMPLETION_AND_RELEASE");
  if(finished)yield break;
  File.WriteAllText(FilePath(role+"-normal-pass.txt"),"PASS "+checks);
  // The host waits for the peer to disconnect; this exercises the production disconnect path.
  if(role=="peer"){
   Note("DISCONNECT_AFTER_ROUND");session.LeaveLobby();
   yield return new WaitForSecondsRealtime(2);
   finished=true;File.WriteAllText(FilePath(role+"-result.txt"),"PASS "+checks);Quit(0);
  }else{
   float deadline=Time.realtimeSinceStartup+20;
   while(session.PlayerCount!=1&&Time.realtimeSinceStartup<deadline)yield return null;
   Check(session.PlayerCount==1&&releaseCount==1,"PEER_DISCONNECT_DOES_NOT_RELEASE_ROUND_TWICE");
   if(!finished){finished=true;File.WriteAllText(FilePath(role+"-result.txt"),"PASS "+checks);Quit(0);}
  }
 }
}
