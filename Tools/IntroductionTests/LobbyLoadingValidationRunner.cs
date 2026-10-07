using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Runs only in the disposable validation project. Connection state is controlled here;
// production connection/retry code and the actual authored Lobby/Ink remain unchanged.
public static class ValidateLobbyLoading {
 public static void RunVisual() => RunScene(true);
 public static void Run() => RunScene(false);
 static void RunScene(bool visual) {
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");
  var session=UnityEngine.Object.FindFirstObjectByType<HauntedHotelMultiplayer>();
  var data=new SerializedObject(session);data.FindProperty("_AutoCreateOnStart").boolValue=false;data.ApplyModifiedPropertiesWithoutUndo();
  new GameObject("Lobby loading regression observer").AddComponent<LobbyLoadingValidationRunner>().VisualOnly=visual;
  EditorSceneManager.SaveScene(scene,"Assets/LoadingValidation.unity");EditorApplication.EnterPlaymode();
 }
}
public sealed class LobbyLoadingValidationRunner:MonoBehaviour {
 public bool VisualOnly;
 int checks;string folder;HauntedHotelMultiplayer session;LobbyManager manager;LobbyIntroductionPresentation intro;HotelConnectionScreen screen;CanvasGroup overlay;
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 static void Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
 void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("LOADING_CHECK "+checks+": "+message);}
 void State(HotelSessionState state,string status){Call(session,"SetState",state,status);}
 void Verify(bool pending,string status){Check(overlay.alpha==(pending?1:0)&&overlay.blocksRaycasts==pending&&overlay.interactable==pending,"Readiness independently owns overlay visibility and input: "+status);Check(Get<TMP_Text>(screen,"_Status").text==status,"Existing loading status is preserved: "+status);}
 void Replay(){Call(manager,"BeginIntroduction");Check(manager.IntroductionPlaying&&intro.UI,"Replay starts actual authored Ink presentation");}
 IEnumerator Start(){folder=System.IO.Path.Combine(Application.dataPath,"../LobbyLoadingEvidence");Directory.CreateDirectory(folder);var stack=new Stack<IEnumerator>();stack.Push(Tests());while(stack.Count>0){object next;try{if(!stack.Peek().MoveNext()){stack.Pop();continue;}next=stack.Peek().Current;}catch(Exception e){Debug.LogException(e);File.WriteAllText(System.IO.Path.Combine(folder,"result.txt"),"FAIL "+e);EditorApplication.Exit(1);yield break;}if(next is IEnumerator child)stack.Push(child);else yield return next;}File.WriteAllText(System.IO.Path.Combine(folder,"result.txt"),"PASS "+checks+" actual authored Lobby loading/Ink checks; connection states controlled in disposable Editor only.");Debug.Log("LOADING_PASS "+checks);EditorApplication.Exit(0);}
 IEnumerator Tests(){
  yield return null;session=FindFirstObjectByType<HauntedHotelMultiplayer>();manager=FindFirstObjectByType<LobbyManager>();intro=manager.GetComponent<LobbyIntroductionPresentation>();screen=FindFirstObjectByType<HotelConnectionScreen>();
  Check(screen&&screen.gameObject.activeInHierarchy&&screen.transform.lossyScale.x>0&&screen.transform.lossyScale.y>0,"Existing authored loading screen is active at visible scale");overlay=Get<CanvasGroup>(screen,"_Overlay");
  Check(Get<HauntedHotelMultiplayer>(screen,"_Lobby")==session,"Existing connection screen binds to current session");
  Check(manager.IntroductionPlaying&&intro.UI,"Ordinary Lobby entry begins existing intro");
  Check(intro.UI.GetComponentInChildren<Canvas>(true).sortingOrder>screen.GetComponent<Canvas>().sortingOrder&&screen.GetComponent<Canvas>().sortingOrder>10,"Intro sorts above existing connection UI above underlying Lobby/menu");
  State(HotelSessionState.Connecting,"Connecting...");Verify(true,"Connecting...");yield return Capture("01-intro-over-pending-connection");
  manager.SkipIntroduction();Verify(true,"Connecting...");Check(!manager.IntroductionPlaying&&!intro.UI&&!session.IntroductionPlaying&&!session.Session.Connected,"Early skip closes intro without completing connection");
  yield return Capture("02-early-skip-existing-loading");if(VisualOnly)yield break;yield return null;Verify(true,"Connecting...");
  State(HotelSessionState.AwaitingPlayer,"Waiting for player...");Verify(true,"Waiting for player...");Check(Mathf.Approximately(Get<RectTransform>(screen,"_ProgressFill").anchorMax.x,.9f),"Existing progress follows connection state after skip");
  State(HotelSessionState.Failed,"Connection failed. Retrying...");Verify(true,"Connection failed. Retrying...");yield return Capture("03-failure-existing-loading");
  State(HotelSessionState.PreparingTransport,"Preparing connection...");Verify(true,"Preparing connection...");
  State(HotelSessionState.Connected,"Connected");Verify(false,"Connected");Check(session.Session.Connected&&!session.Session.InputFocused,"Connection after skip releases existing blocker without orphaned intro focus");yield return Capture("04-connected-lobby");
  Replay();State(HotelSessionState.Connecting,"Connecting...");yield return new WaitForSecondsRealtime(10f);Check(manager.IntroductionPlaying,"Late skip occurs while actual reading sequence is running");yield return Capture("05-late-intro-over-loading");manager.SkipIntroduction();Verify(true,"Connecting...");yield return Capture("06-late-skip-existing-loading");
  Replay();State(HotelSessionState.Connected,"Connected");Verify(false,"Connected");Check(manager.IntroductionPlaying&&intro.UI&&session.IntroductionPlaying&&session.Session.InputFocused,"Connection before skip leaves intro and its input focus running");manager.SkipIntroduction();Check(!intro.UI&&!session.Session.InputFocused,"Skip after connection reveals ready Lobby and releases intro focus");Verify(false,"Connected");
  Replay();State(HotelSessionState.Connecting,"Connecting...");float deadline=Time.unscaledTime+40;
  while(manager.IntroductionPlaying&&Time.unscaledTime<deadline){VerifyPendingFrame();yield return null;}
  Check(!manager.IntroductionPlaying&&!intro.UI,"Actual Ink completes normally without skip");Verify(true,"Connecting...");Check(!session.Session.Connected,"Normal intro completion cannot manufacture connection readiness");yield return Capture("07-normal-completion-pending-loading");
  State(HotelSessionState.Connected,"Connected");Verify(false,"Connected");Check(!session.Session.InputFocused,"Normal completion then connection leaves no orphaned focus");
  Replay();State(HotelSessionState.Connecting,"Connecting...");manager.enabled=false;Verify(true,"Connecting...");Check(!intro.UI&&!session.IntroductionPlaying,"Interrupted intro leaves pending loading visible");State(HotelSessionState.Connected,"Connected");Verify(false,"Connected");manager.enabled=true;Check(!manager.IntroductionPlaying,"Re-enable after interruption does not restart completed local intro");
  screen.enabled=false;State(HotelSessionState.Connecting,"Reconnecting...");screen.enabled=true;Verify(true,"Reconnecting...");State(HotelSessionState.Connected,"Connected");Verify(false,"Connected");
 }
 void VerifyPendingFrame(){if(overlay.alpha!=1||!overlay.blocksRaycasts||session.ReadyToPlay)throw new Exception("Unready Lobby flash during normal intro");}
 IEnumerator Capture(string name){
  // The production Canvas remains ScreenSpaceOverlay. Camera.Render conversion is
  // unsuitable for this scene; native screenshots need a GUI Editor (not batch mode).
  Canvas.ForceUpdateCanvases();var rect=(RectTransform)screen.transform;var image=screen.GetComponent<Image>();
  Check(image&&image.color==Color.black&&image.raycastTarget&&RectTransformUtility.RectangleContainsScreenPoint(rect,new Vector2(Screen.width*.5f,Screen.height*.5f),null),"Existing opaque blackscreen covers screen center: "+name);
  var events=UnityEngine.EventSystems.EventSystem.current;var temporary=false;
  if(!events){events=new GameObject("Disposable raycast test provider").AddComponent<UnityEngine.EventSystems.EventSystem>();temporary=true;}
  var hits=new List<UnityEngine.EventSystems.RaycastResult>();events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events){position=new Vector2(Screen.width*.5f,Screen.height*.5f)},hits);
  bool loadingHit=hits.Exists(hit=>hit.gameObject==screen.gameObject||hit.gameObject.transform.IsChildOf(screen.transform));
  Check(loadingHit==(overlay.alpha>0),"Existing loading raycast blocker follows readiness: "+name);
  if(temporary)Destroy(events.gameObject);
  var path=System.IO.Path.Combine(folder,name+"-native.png");if(File.Exists(path))File.Delete(path);ScreenCapture.CaptureScreenshot(path);yield return new WaitForSecondsRealtime(.3f);
  if(!File.Exists(path))Debug.Log("LOADING_CAPTURE_UNAVAILABLE: Native overlay screenshot is unavailable in this execution mode: "+name);
 }
}
