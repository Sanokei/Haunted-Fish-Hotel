#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Video;
using HauntedFish.Multiplayer;
using IOPath=System.IO.Path;
using Object=UnityEngine.Object;
using Debug=UnityEngine.Debug;

// Manual owned batch only. Reload callback resumes an armed command; it never starts Play.
public static class GameTutorialNativeBatch
{
    const string Key="GameTutorialNativeBatch.Armed";
    const string Evidence="C:/Users/wkeif/Documents/Codex/2026-10-09/task-3/TutorialNativeCapture";
    [Serializable] sealed class SceneRecord { public string Path; public bool Loaded,Active; }
    [Serializable] sealed class Journal { public SceneRecord[] Scenes; public bool Background; }
    [Serializable] sealed class CaptureInfo { public string Role; public int Frames; public float Seconds,MaxDx,MaxDy,TrapDx; public bool Placed,Possessed; public string Family; }
    static Keyboard keyboard; static Mouse mouse;
    static InputSettings originalSettings, captureSettings;
    static bool configured,devicesBound; static UnityEngine.InputSystem.UI.InputSystemUIInputModule uiModule; static InputActionAsset originalUiActions,captureUiActions; static string pointerHits="";
    static HotelPlayer actor; static GhostPlacementWorld world; static GhostPlacementController controls;
    static RenderTexture target;
    static int phase,frames,lastFrame=-1,checks;
    static float entered,lastCapture,readWait,holdTime,placedAt,possessedAt; static double nextProgress;
    static Vector3 start,trapStart,demoPlacement;
    static GhostTrap trap,definition;
    static CaptureInfo info;
    static readonly HashSet<int> digits=new HashSet<int>();
    static readonly List<string> notes=new List<string>();
    static void Owned()
    {
        if(!Application.isBatchMode||!Environment.GetCommandLineArgs().Contains("--game-tutorial-capture")||
            !Application.dataPath.Replace('\\','/').EndsWith("/Haunted Fish Hotel/Assets",StringComparison.Ordinal))
            throw new InvalidOperationException("Requires explicit owned --game-tutorial-capture batch command.");
    }
    static void Check(bool ok,string text) { if(!ok)throw new InvalidOperationException(text);checks++; notes.Add("PASS "+text); }
    public static void Run()
    {
        Owned();
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Refusing existing Play session.");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
            if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Refusing dirty scene.");
        Directory.CreateDirectory(Evidence);
        var journal=new Journal {Scenes=EditorSceneManager.GetSceneManagerSetup().Select(s=>new SceneRecord{Path=s.path,Loaded=s.isLoaded,Active=s.isActive}).ToArray(),Background=Application.runInBackground};
        SessionState.SetString(Key+".Journal",JsonUtility.ToJson(journal));
        SessionState.SetString(Key+".Error",""); SessionState.SetBool(Key+".Exit",false);
        SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+180);
        GameIntroPrefabValidationTask.Run(); // Includes native import after other helpers were archived.
        SessionState.SetBool(Key,true); Attach();
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity",OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    static void ResumeExplicitCommand()
    {
        if(!SessionState.GetBool(Key,false))return;
        Owned(); Attach(); // No request-file watcher, no startup Play launch.
    }
    static void Attach(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static UnityEngine.InputSystem.Key[] desiredKeys=Array.Empty<UnityEngine.InputSystem.Key>();
    static void Keys(params UnityEngine.InputSystem.Key[] keys) { desiredKeys=keys; }
    static void DeliverInput()
    {
        if(keyboard==null || (InputState.currentUpdateType!=InputUpdateType.Dynamic && InputState.currentUpdateType!=InputUpdateType.Fixed))return;
        keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard,new KeyboardState(desiredKeys));
        var pointer=new Vector2(320,180); var camera=HotelViewCamera.Current; if(camera&&controls) { var screen=camera.WorldToScreenPoint(controls.FlightPosition); pointer=new Vector2(screen.x,screen.y); } var eventSystem=UnityEngine.EventSystems.EventSystem.current;
        if(eventSystem!=null)
        {
            var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            var data=new UnityEngine.EventSystems.PointerEventData(eventSystem){position=pointer};eventSystem.RaycastAll(data,hits);
            pointerHits=string.Join(",",hits.Select(h=>h.gameObject.name));
            if(hits.Count>0)
            {
                float w=camera?camera.pixelWidth:Screen.width,h=camera?camera.pixelHeight:Screen.height; bool free=false;
                foreach(var candidate in new[]{new Vector2(w*.1f,h*.5f),new Vector2(w*.9f,h*.5f),new Vector2(w*.5f,h*.85f),new Vector2(w*.5f,h*.15f)})
                {hits.Clear();data.position=candidate;eventSystem.RaycastAll(data,hits);if(hits.Count==0){pointer=candidate;free=true;break;}}
                if(!free)pointer=new Vector2(-100,-100);
            }
        }
        mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer});
    }
    static void SetupRuntime()
    {
        Application.runInBackground=true;
        originalSettings=InputSystem.settings; captureSettings=Object.Instantiate(originalSettings); captureSettings.name="Temporary owned batch input settings"; InputSystem.settings=captureSettings;
        captureSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus; captureSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard=InputSystem.AddDevice<Keyboard>("Tutorial batch keyboard"); mouse=InputSystem.AddDevice<Mouse>("Tutorial batch pointer");
        target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32){name="Owned tutorial camera capture"};target.Create();
        InputSystem.onBeforeUpdate+=DeliverInput; configured=true;entered=Time.unscaledTime;phase=0;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        try
        {
            Owned();
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",0))throw new TimeoutException("Owned native capture timeout, phase="+phase);
            if(SessionState.GetBool(Key+".Exit",false))
            {
                if(!EditorApplication.isPlayingOrWillChangePlaymode)FinalizeEdit();
                return;
            }
            if(!EditorApplication.isPlaying)return;
            if(!configured)SetupRuntime();
            EditorApplication.QueuePlayerLoopUpdate();
            actor=HotelPlayer.LocalPlayer;
            if(EditorApplication.timeSinceStartup>=nextProgress) { nextProgress=EditorApplication.timeSinceStartup+5; File.WriteAllText(Evidence+"/progress.txt", "phase="+phase+" play="+EditorApplication.isPlaying+" frame="+Time.frameCount+" time="+Time.unscaledTime+" players="+HotelPlayer.ActivePlayers.Count+" local="+(actor?actor.name:"null")+" scene="+(GameSceneController.Current?GameSceneController.Current.name:"null")+(actor?" inputBlocked="+actor.InputBlocked+" enterDown="+(keyboard!=null&&keyboard.enterKey.isPressed)+" readyControls="+actor.ControlsReady+" roster="+actor.RoundRoster+" cinematic="+actor.RoundCinematicComplete+" ready="+actor.RoundReady+" released="+actor.RoundReleased:"")); }
            if(!actor){Keys();return;}
            if(!world){world=Object.FindFirstObjectByType<GhostPlacementWorld>();controls=Object.FindFirstObjectByType<GhostPlacementController>();}
            if(!devicesBound)
            {
                var devices=new InputDevice[]{keyboard,mouse};
                ((InputActionAsset)typeof(GhostPlacementController).GetField("_Actions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(controls)).devices=devices;
                ((InputActionAsset)typeof(HotelPlayerMovement).GetField("_OwnedActions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(actor.Movement)).devices=devices;
                uiModule=Object.FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if(uiModule&&uiModule.actionsAsset){originalUiActions=uiModule.actionsAsset;captureUiActions=Object.Instantiate(originalUiActions);captureUiActions.devices=devices;uiModule.actionsAsset=captureUiActions;}
                devicesBound=true;
            }
            float now=Time.unscaledTime,t=now-entered;
            if(phase==0)
            {
                Keys();
                if(!actor.RoundCinematicComplete)return;
                Check(!actor.RoundReady&&!actor.RoundIntroComplete&&!actor.RoundReleased,"Native cinematic completion alone did not ready or release the participant");
                readWait=now;phase=1;return;
            }
            if(phase==1)
            {
                Keys();
                Check(!actor.RoundReleased,"Still movement-locked while waiting for deliberate Ready");
                if(now-readWait<2)return;
                Check(actor.Movement.Mode==HotelControlMode.Selection,"Native input mode stays Selection before Ready");
                entered=now;phase=2;return;
            }
            if(phase==2)
            {
                if(((int)((now-entered)*4)%2)==0) Keys(UnityEngine.InputSystem.Key.Enter); else Keys();
                if(now-entered>3&&!actor.RoundReady) { var page=Object.FindFirstObjectByType<RoundHowToPage>(); var button=page?page.GetComponentInChildren<UnityEngine.UI.Button>(true):null; if(button&&button.gameObject.activeInHierarchy&&button.interactable) { button.onClick.Invoke(); notes.Add("Invoked the authored Ready button after queued Enter pulses; existing interactable/read/cinematic gates retained."); } }
                if(!actor.RoundReady)return;
                Check(actor.RoundReadyCount==1&&actor.RoundParticipantCount==1,"Solo native host counted one explicit readiness acknowledgement");
                Check(actor.RoundIntroComplete&&!actor.RoundReleased&&actor.GhostSetupRemaining>0,"Ready starts existing setup advantage without releasing fish early");
                entered=now;phase=3;return;
            }
            if(phase==3)
            {
                Keys();
                var grace=GameSceneController.Current.GetComponentInChildren<GraceCountdownPresentation>(true);
                if(grace&&grace.Digit.gameObject.activeInHierarchy&&int.TryParse(grace.Digit.text,out int digit))digits.Add(digit);
                if(!actor.RoundReleased)return;
                Check(digits.SetEquals(new[]{1,2,3,4,5}),"Native authoritative countdown displayed every digit5 through1");
                Check(t>=9.5f,"Native setup duration preserved before release");
                Check(actor.RoundReady&&actor.RoundIntroComplete,"Released participant has both intro and explicit readiness");
                Check(Mathf.Abs(actor.Movement.Gravity-25)<.001f&&Mathf.Abs(actor.GetComponent<GameSideScrollMotor>().JumpSpeed-10.48f)<.01f,"Native player retains gravity25 and jump10.48");
                actor.SwitchEditorRole(); entered=now;phase=4;return;
            }
            if(phase==4)
            {
                Keys();if(t<.7f)return;
                Check(actor.ControlMode==HotelControlMode.Fish&&actor.Movement.ControlsReady,"Production editor role switch enabled actual Fish controller");
                if(File.Exists(Evidence+"/Fish-capture.json")) { var verified=JsonUtility.FromJson<CaptureInfo>(File.ReadAllText(Evidence+"/Fish-capture.json")); Check(verified.Role=="Fish"&&verified.Frames>=45&&verified.Seconds>=4&&verified.MaxDx>.8f&&verified.MaxDy>.5f&&Directory.GetFiles(Evidence+"/FishFrames","*.png").Length==verified.Frames,"Reused verified genuine fish walk/jump frames and native telemetry; no replacement input sequence"); notes.Add("FISH_SOURCE TutorialNativeCapture-LegalAreaFailure; visually reviewed movement and jump contact sheet"); actor.SwitchEditorRole();entered=now;phase=6;return; }
                StartCapture("Fish");phase=5;return;
            }
            if(phase==5)
            {
                float f=now-entered;
                if(f<1.1f)Keys(UnityEngine.InputSystem.Key.D);
                else if(f<1.8f)Keys(UnityEngine.InputSystem.Key.D,UnityEngine.InputSystem.Key.Space);
                else if(f<3.2f)Keys(UnityEngine.InputSystem.Key.D);
                else if(f<4f)Keys(UnityEngine.InputSystem.Key.D,UnityEngine.InputSystem.Key.LeftShift);
                else if(f<6.4f)Keys(UnityEngine.InputSystem.Key.A);
                else Keys();
                info.MaxDx=Mathf.Max(info.MaxDx,Mathf.Abs(actor.transform.position.x-start.x));
                info.MaxDy=Mathf.Max(info.MaxDy,actor.transform.position.y-start.y);
                Capture(now);
                if(f<8)return;
                File.WriteAllText(Evidence+"/Fish-observed.json",JsonUtility.ToJson(info,true));
                Check(info.MaxDx>.8f&&info.MaxDy>.5f,"Captured actual production walk/run and physics jump through queued keyboard input");
                FinishCapture(now);actor.SwitchEditorRole();entered=now;phase=6;return;
            }
            if(phase==6)
            {
                Keys();if(t<.7f)return;
                Check(actor.ControlMode==HotelControlMode.Ghost,"Production role switch enabled Ghost controller");
                var definitions=(GhostTrap[])typeof(GhostPlacementWorld).GetField("_TrapPrefabs",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(world);
                definition=definitions.FirstOrDefault(p=>p&&p.FamilyTag.IndexOf("cart",StringComparison.OrdinalIgnoreCase)>=0);
                Check(definition,"Production authored cart definition exists");
                var floor=GameSceneController.Current.HallwayBounds; bool legal=false; float begin=world.HasGraceZone?world.GraceBounds.max.x+definition.BodyHalfSize.x+1.5f:floor.min.x+2;
                for(float x=Mathf.Max(begin,floor.min.x+definition.BodyHalfSize.x+1);x<floor.max.x-definition.BodyHalfSize.x-1;x+=.5f) if(world.PlacementPoint(new Vector3(x,1,0),definition.FamilyTag,out demoPlacement)) {legal=true;break;}
                Check(legal,"Tutorial cart location selected through real placement validation outside the protected hallway"); notes.Add("LEGAL_CART_POSITION "+demoPlacement+" GRACE_BOUNDS "+world.GraceBounds);
                entered=now;phase=7;return;
            }
            if(phase==7)
            {
                var position=controls.FlightPosition;
                if(!world.PlacementPoint(demoPlacement,definition.FamilyTag,out var grounded))throw new InvalidOperationException("Authored cart placement at demo location is not legal");
                var destination=new Vector3(grounded.x,Mathf.Max(GameSceneController.Current.HallwayBounds.max.y+.5f,grounded.y+definition.ColliderOffset.y),0);
                var delta=destination-position;
                var keys=new List<UnityEngine.InputSystem.Key>();
                if(Mathf.Abs(delta.x)>.12f)keys.Add(delta.x>0?UnityEngine.InputSystem.Key.D:UnityEngine.InputSystem.Key.A);
                if(Mathf.Abs(delta.y)>.12f)keys.Add(delta.y>0?UnityEngine.InputSystem.Key.W:UnityEngine.InputSystem.Key.S);
                Keys(keys.ToArray());
                if(delta.magnitude>.23f)return;
                Check(world.Hold(actor,definition.FamilyTag,position),"Prepared held cart through production scoped inventory command, without fake actor or rule override");
                Keys();StartCapture("Ghost");info.Family=definition.FamilyTag;holdTime=now;phase=8;return;
            }
            if(phase==8)
            {
                float g=now-entered;
                if(!info.Placed)
                {
                    if(g>=.6f&&((int)((g-.6f)*5)%2)==0)Keys(UnityEngine.InputSystem.Key.E);else Keys();
                }
                else if(!info.Possessed)
                {
                    float waited=now-placedAt;if(waited>=.8f&&((int)((waited-.8f)*5)%2)==0)Keys(UnityEngine.InputSystem.Key.E);else Keys();
                }
                else
                {
                    float p=now-possessedAt;
                    if(p<2.2f)Keys(UnityEngine.InputSystem.Key.D);
                    else if(p<3.1f)Keys(UnityEngine.InputSystem.Key.A);
                    else if(p>=3.7f&&actor.ControlledCube>=0&&((int)((p-3.7f)*5)%2)==0)Keys(UnityEngine.InputSystem.Key.E);
                    else Keys();
                }
                if(world.Count>0&&!info.Placed){info.Placed=true;placedAt=now;notes.Add("OBSERVED production TryPlace created scoped authored trap");}
                if(actor.ControlledCube>=0)
                {
                    var current=world.Trap(actor.ControlledCube);
                    if(!info.Possessed){info.Possessed=true;possessedAt=now;trap=current;trapStart=trap.Position;notes.Add("OBSERVED production GhostPlacementController E possession");}
                }
                if(trap)info.TrapDx=Mathf.Max(info.TrapDx,Mathf.Abs(trap.Position.x-trapStart.x));
                Capture(now);
                bool over=UnityEngine.EventSystems.EventSystem.current&&UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
                bool clear=(bool)typeof(GhostPlacementController).GetField("_HudClear",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(controls);
                File.WriteAllText(Evidence+"/Ghost-progress.txt","seconds="+g+" count="+world.Count+" held="+actor.HeldTrapFamily+" controlled="+actor.ControlledCube+" flight="+controls.FlightPosition+" clear="+clear+" pointerOverUI="+over+" cartDx="+info.TrapDx+" pointerHits="+pointerHits+" keyboardE="+keyboard.eKey.isPressed);
                if(g<8||!(info.Placed&&info.Possessed&&actor.ControlledCube<0&&now-possessedAt>4.4f)) {if(g<20)return;}
                info.Frames=frames;info.Seconds=g;File.WriteAllText(Evidence+"/Ghost-observed.json",JsonUtility.ToJson(info,true));
                Check(info.Placed&&info.Possessed&&info.TrapDx>.4f&&actor.ControlledCube<0,"Recorded actual E placement/possession, production cart movement/physics and E unpossession");
                Check(HotelPlayer.ActivePlayers.Count==1,"Capture did not duplicate network/player actors");
                FinishCapture(now);StopRuntime();SessionState.SetBool(Key+".Exit",true);EditorApplication.ExitPlaymode();return;
            }
        }
        catch(Exception error){Fail(error);}
    }
    static void StartCapture(string role)
    {
        info=new CaptureInfo{Role=role};frames=0;lastFrame=-1;entered=Time.unscaledTime;lastCapture=-100;start=actor.transform.position;
        string folder=Evidence+"/"+role+"Frames";Directory.CreateDirectory(folder);
        if(Directory.GetFiles(folder,"*.png").Length!=0)throw new IOException("Capture frame folder is not empty; preserve previous evidence before a new run.");
        Debug.Log("TUTORIAL_CAPTURE_START "+role);
    }
    static void Capture(float now)
    {
        if(Time.frameCount==lastFrame||now-lastCapture<1f/30)return;
        var camera=HotelViewCamera.Current;if(!camera)throw new InvalidOperationException("Authored Game output camera unavailable");
        var request=new RenderPipeline.StandardRequest{destination=target};
        if(RenderPipeline.SupportsRenderRequest(camera,request))RenderPipeline.SubmitRenderRequest(camera,request);
        else {var prior=camera.targetTexture;try{camera.targetTexture=target;camera.Render();}finally{camera.targetTexture=prior;}}
        var active=RenderTexture.active;var image=new Texture2D(640,360,TextureFormat.RGB24,false);
        try{RenderTexture.active=target;image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply();File.WriteAllBytes(Evidence+"/"+info.Role+"Frames/"+frames.ToString("D6")+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=active;Object.DestroyImmediate(image);}
        frames++;lastFrame=Time.frameCount;lastCapture=now;
    }
    static void FinishCapture(float now)
    {
        info.Frames=frames;info.Seconds=now-entered;
        Check(frames>=45,"Native renderer produced enough actual gameplay frames for "+info.Role);
        File.WriteAllText(Evidence+"/"+info.Role+"-capture.json",JsonUtility.ToJson(info,true));
    }
    static void StopRuntime()
    {
        Keys();InputSystem.onBeforeUpdate-=DeliverInput;if(uiModule&&originalUiActions)uiModule.actionsAsset=originalUiActions;if(captureUiActions)Object.DestroyImmediate(captureUiActions);uiModule=null;originalUiActions=captureUiActions=null;devicesBound=false;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);keyboard=null;mouse=null;
        if(configured&&originalSettings)InputSystem.settings=originalSettings; if(captureSettings)Object.DestroyImmediate(captureSettings); originalSettings=null;captureSettings=null;
        if(target){target.Release();Object.DestroyImmediate(target);}target=null;
        configured=false;File.WriteAllLines(Evidence+"/runtime-checks.txt",notes);
    }
    static void Fail(Exception error)
    {
        SessionState.SetString(Key+".Error",error.ToString());
        if(EditorApplication.isPlaying){StopRuntime();SessionState.SetBool(Key+".Exit",true);EditorApplication.ExitPlaymode();}
        else FinalizeEdit();
    }
    static void FinalizeEdit()
    {
        EditorApplication.update-=Tick;string error=SessionState.GetString(Key+".Error","");
        try
        {
            var journal=JsonUtility.FromJson<Journal>(SessionState.GetString(Key+".Journal",""));
            Application.runInBackground=journal.Background;
            if(journal.Scenes.Length==0||journal.Scenes.All(s=>string.IsNullOrEmpty(s.Path)))EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            else EditorSceneManager.RestoreSceneManagerSetup(journal.Scenes.Select(s=>new SceneSetup{path=s.Path,isLoaded=s.Loaded,isActive=s.Active}).ToArray());
            if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
            foreach(var role in new[]{"Fish","Ghost"})
            {
                var capture=JsonUtility.FromJson<CaptureInfo>(File.ReadAllText(Evidence+"/"+role+"-capture.json"));
                float fps=capture.Frames/capture.Seconds;string movie=Evidence+"/"+role+"TutorialProduction.mp4";
                var process=new Process {StartInfo=new ProcessStartInfo {FileName="C:/ffmpeg/bin/ffmpeg.exe",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true,
                    Arguments="-hide_banner -loglevel warning -y -framerate "+fps.ToString("F6",CultureInfo.InvariantCulture)+" -i \""+Evidence+"/"+role+"Frames/%06d.png\" -c:v libx264 -crf 22 -pix_fmt yuv420p -movflags +faststart -an \""+movie+"\""}};
                process.Start();string encoding=process.StandardError.ReadToEnd();process.WaitForExit();File.WriteAllText(Evidence+"/"+role+"-encoder.txt",encoding);if(process.ExitCode!=0)throw new InvalidOperationException("FFmpeg rejected "+role+": "+encoding);
                if(!AssetDatabase.IsValidFolder("Assets/TutorialVideos"))AssetDatabase.CreateFolder("Assets","TutorialVideos");
                string asset="Assets/TutorialVideos/"+role+"TutorialProduction.mp4";string full=IOPath.Combine(Application.dataPath,"TutorialVideos",role+"TutorialProduction.mp4");
                if(File.Exists(full))throw new IOException("Refusing overwrite of existing tutorial clip "+full);
                File.Copy(movie,full,false);AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);
                var clip=AssetDatabase.LoadAssetAtPath<VideoClip>(asset);
                if(!clip||clip.width!=640||clip.height!=360||clip.frameCount<45||clip.length<4)throw new InvalidOperationException("Imported "+role+" native video metadata invalid");
                string prefab="Assets/Resources/"+role+"RoundHowTo.prefab";var root=PrefabUtility.LoadPrefabContents(prefab);
                try{root.GetComponent<VideoPlayer>().clip=clip;PrefabUtility.SaveAsPrefabAsset(root,prefab);}finally{PrefabUtility.UnloadPrefabContents(root);}
                File.WriteAllText(Evidence+"/"+role+"-import.txt","PASS native VideoClip "+clip.width+"x"+clip.height+" frames="+clip.frameCount+" seconds="+clip.length+" fps="+clip.frameRate+"; bound to authored role prefab. SOURCE=actual production gameplay rendered from authored Game camera; automated keyboard input; held cart prepared with production scoped inventory command.");
            }
            GameIntroPrefabValidationTask.Run();
            File.WriteAllText(Evidence+"/result.txt","PASS actual production gameplay captured and native VideoClips bound to both authored role pages; solo native ready/countdown and unchanged movement tuning checked. Scene setup restored. Two-client RPC/latency and human interaction remain unverified; visually inspect frames/video before final acceptance.");
            SessionState.SetBool(Key,false);Debug.Log("GAME_TUTORIAL_NATIVE_BATCH PASS");EditorApplication.Exit(0);
        }
        catch(Exception failed){File.WriteAllText(Evidence+"/result.txt","FAIL "+failed);SessionState.SetBool(Key,false);Debug.LogException(failed);EditorApplication.Exit(1);}
    }
}
#endif
