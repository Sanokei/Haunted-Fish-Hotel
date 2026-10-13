#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using HauntedFish.BossFight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class BossArenaValidation {
    static void RequireIdleEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Requires idle Edit mode outside prefab editing.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save or discard scene changes before explicitly running this tool.");
    }

 static BossArenaManager arena;
 static BossCharacterPresentation presentation;
 static bool capturedReaction, checkedSpin;
 static int stage,checks,results,fishGoalsLaunched;
 static double entered;
 static string firstSnapshot;
 static uint checkedArtEpoch;static int checkedArtScore=-1;
 const string Evidence="Tools/BossFightTests/Evidence/2026-10-10-native-gameplay-rerun";
 static string Prefix=>System.IO.Path.Combine(System.IO.Path.GetTempPath(),"HauntedFishBossArenaPlayNative-20261010-rerun");
 [Serializable] sealed class SceneRecord {public string Path;public bool Loaded,Active;}
 [Serializable] sealed class SavedSetup {public SceneRecord[] Scenes;public string[] Selection;}
 static void OnState(PlayModeStateChange state)
 {
     if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool("BossArenaValidation.Restore",false))return;
     SessionState.SetBool("BossArenaValidation.Restore",false);
     Application.runInBackground=SessionState.GetBool("BossArenaValidation.Background",false);
     var saved=JsonUtility.FromJson<SavedSetup>(SessionState.GetString("BossArenaValidation.Setup",""));
     try
     {
         if(saved==null)throw new Exception("Missing original editor setup journal");
         if(saved.Scenes.Length==0||saved.Scenes.All(s=>string.IsNullOrEmpty(s.Path)))
             EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
         else EditorSceneManager.RestoreSceneManagerSetup(saved.Scenes.Select(s=>new SceneSetup{path=s.Path,isLoaded=s.Loaded,isActive=s.Active}).ToArray());
         Selection.objects=saved.Selection.Select(s=>GlobalObjectId.TryParse(s,out var id)?GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id):null).Where(o=>o).ToArray();
         if(saved.Scenes.Any(s=>!string.IsNullOrEmpty(s.Path)))
         {
             var now=EditorSceneManager.GetSceneManagerSetup();
             if(!saved.Scenes.Select(s=>s.Path+"|"+s.Loaded+"|"+s.Active).SequenceEqual(now.Select(s=>s.path+"|"+s.isLoaded+"|"+s.isActive)))throw new Exception("Original saved editor scene setup was not restored");
         }
         string outcome=File.Exists(Evidence+"/results.txt")?File.ReadAllText(Evidence+"/results.txt"):"FAIL no native result";
         outcome+="\nPASS original clean editor setup/selection restored after Play; background setting restored.";
         File.WriteAllText(Evidence+"/results.txt",outcome);File.WriteAllText(Prefix+".txt",outcome);
     }
     catch(Exception error){File.WriteAllText(Prefix+".txt","FAIL test-helper restoration: "+error);}
 }
 static void Finish(int code)
 {
     EditorApplication.update-=Tick;SessionState.SetBool("BossArenaValidation.Armed",false);
     if(SessionState.GetBool("BossArenaValidation.Restore",false))EditorApplication.ExitPlaymode();
     else {File.WriteAllText(Prefix+".txt","FAIL no connected-editor restoration state; refusing to close editor.");}
 }
 static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Debug.Log("BOSS CHECK "+name);}
 [InitializeOnLoadMethod]
    static void Resume()
    {
        EditorApplication.playModeStateChanged-=OnState;EditorApplication.playModeStateChanged+=OnState;
        if(!SessionState.GetBool("BossArenaValidation.Armed",false))
        {
            // Only resume an explicitly started run; request files never start Play.
            return;
        }
        Application.runInBackground=true; // Production bootstrap sets this; standalone test scene does not.
        entered = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Haunted Hotel/Validate boss physics in restored Play session")]
    public static void Run(){ RequireIdleEditMode();
        if(File.Exists(Prefix+".hold"))return; // Lobby integration holds all Play transitions.
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        Directory.CreateDirectory(Evidence);
        if(Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
        {File.WriteAllText(Prefix+".txt","BLOCKED: user has unsaved scene edits; preserving scenes and refusing Play transition.");return;}
        string presentation=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"HauntedFishBossPresentationOcclusionNative-20261010.txt");
        if(!File.Exists(presentation)||File.ReadAllText(presentation).Contains("FAIL "))return;
        var saved=new SavedSetup{Scenes=EditorSceneManager.GetSceneManagerSetup().Select(s=>new SceneRecord{Path=s.path,Loaded=s.isLoaded,Active=s.isActive}).ToArray(),Selection=Selection.objects.Select(o=>GlobalObjectId.GetGlobalObjectIdSlow(o).ToString()).ToArray()};
        SessionState.SetBool("BossArenaValidation.Background",Application.runInBackground);
        string journal=JsonUtility.ToJson(saved);var roundTrip=JsonUtility.FromJson<SavedSetup>(journal);
        if(!saved.Scenes.Select(s=>s.Path+"|"+s.Loaded+"|"+s.Active).SequenceEqual(roundTrip.Scenes.Select(s=>s.Path+"|"+s.Loaded+"|"+s.Active)))throw new Exception("Editor setup journal does not preserve scene records");
        SessionState.SetString("BossArenaValidation.Setup",journal);
        SessionState.SetBool("BossArenaValidation.Restore",true);SessionState.SetBool("BossArenaValidation.Armed",true);
        EditorApplication.playModeStateChanged-=OnState;EditorApplication.playModeStateChanged+=OnState;
        EditorSceneManager.OpenScene("Assets/Scenes/BossFight.unity");
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();entered=EditorApplication.timeSinceStartup;
    }
 static void Tick(){
 try {
 if(EditorApplication.timeSinceStartup-entered>45)throw new Exception("Boss test timeout stage "+stage+"; frame="+Time.frameCount+" unscaled="+Time.unscaledTime+" scale="+Time.timeScale+" paused="+EditorApplication.isPaused+" background="+Application.runInBackground+" active="+(arena&&arena.Active)+" ready="+(arena&&arena.ParticipantsReady)+" snapshot="+(arena?arena.Snapshot:"none"));
 if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
 if(!arena){
 arena=UnityEngine.Object.FindFirstObjectByType<BossArenaManager>();
 if(!arena)return;
 Check(arena.Puck&&arena.Camera,"authored references");
 presentation=UnityEngine.Object.FindFirstObjectByType<BossCharacterPresentation>();
 Check(presentation&&presentation.FishActor&&presentation.GhostActor,"authored matched character reactions");
 var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/BossFight/FeltHockeyTable.obj");
 Check(model&&model.GetComponentsInChildren<MeshFilter>(true).Length>0,"OBJ imported meshes");
 var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/BossFight/FeltHockeyTable.prefab");
 Check(prefab&&prefab.GetComponentsInChildren<BoxCollider>(true).Length>10,"table prefab colliders");
 Check(arena.Puck.GetComponent<SphereCollider>().sharedMaterial.bounciness>.9f,"bounce material imported");
 
 Check(!arena.Camera.gameObject.activeSelf,"participant camera starts inactive");
 Check(!arena.ApplyInput(1,Vector2.one)&&!arena.TryBegin(1,2),"observer cannot author");
 // Practice must never enter authoritative match state or award a result.
 arena.BeginEditorPractice();
 Check(arena.EditorPracticing&&!arena.Active&&arena.FishId==0&&arena.GhostId==0,"editor practice has no network contestant IDs");
 Check(!arena.TryBegin(1,2)&&!arena.RegisterGoal(true)&&results==0,"practice cannot award wins or overlap a real match");
 typeof(BossArenaManager).GetField("_NextServe",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(arena,0f);
 typeof(BossArenaManager).GetMethod("FixedUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(arena,null);
 Check(!arena.Puck.isKinematic&&arena.Puck.linearVelocity.sqrMagnitude>0,"practice serves physical cue ball");
 arena.SetEditorPracticeInput(Vector2.one,true);
 Check(arena.Puck.isKinematic,"settings pause freezes practice puck");
 arena.SetEditorPracticeInput(Vector2.zero,false);
 Check(!arena.Puck.isKinematic&&arena.Puck.linearVelocity.sqrMagnitude>0,"practice pause restores physical motion");
 arena.StopEditorPractice();
 Check(!arena.EditorPracticing&&!arena.Active&&arena.Puck.isKinematic,"practice exits without scored result");
 var rig=presentation.GhostActor.GetComponentInChildren<BossSegmentedRig>(true);
 Check(presentation.FishActor.GetComponentsInChildren<SpriteRenderer>(true).Length==1&&rig&&rig.ValidateAuthoredBindings(out string rigError)&&rig.ActiveArtwork.Count(r=>r.enabled&&r.gameObject.activeInHierarchy)==9,"one fish proxy and one authored nine-part ghost opponent");
 arena.SetAuthority(true);arena.Completed+=r=>results++;
 Check(arena.TryBegin(1,2),"authority begins");
 Check(!arena.ApplyInput(1,Vector2.one),"admission blocks input");
 arena.SetParticipantsReady(true);Check(!arena.TryBegin(3,4),"busy arena");
 var storm=arena.GetComponentInParent<Transform>().root.GetComponentInChildren<LightningEffectManager>(true);
 var audio=arena.GetComponentInParent<Transform>().root.GetComponentInChildren<HauntedFish.Multiplayer.LobbyStormAudio>(true);
 var fishArtwork=presentation.FishActor.GetComponentInChildren<SpriteRenderer>(true);var ghostArtwork=presentation.GhostActor.GetComponentInChildren<SpriteRenderer>(true);
 var fishPaddle=(Rigidbody)typeof(BossArenaManager).GetField("_FishPaddle",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(arena);
 var ghostPaddle=(Rigidbody)typeof(BossArenaManager).GetField("_GhostPaddle",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(arena);
 foreach(uint viewer in new uint[]{1,2})
 {
     arena.SetLocalViewer(viewer,true);
     Check(!arena.Camera.orthographic&&arena.Camera.transform.position.z*(viewer==1?-1:1)>0,"native Play camera at owned end "+viewer);
     Check(fishArtwork.enabled==(viewer!=1)&&rig.ActiveArtwork.All(r=>r.enabled==(viewer!=2)),"native Play every own segment hidden/opponent visible "+viewer);
     var paddle=viewer==1?fishPaddle:ghostPaddle;float dx=viewer==1?.8f:-.8f;
     var screen=arena.Camera.WorldToScreenPoint(paddle.position+new Vector3(dx,0,0));var puckBefore=arena.Puck.position;
     var axis=arena.PointerAxis(viewer,new Vector2(screen.x,screen.y),.05f);
     Check(axis.x*dx>0&&axis.magnitude<=1.001f&&arena.Puck.position==puckBefore,"native Play pointer from owned end without puck teleport "+viewer);
     Check(storm&&storm.AmbientActive&&audio&&audio.gameObject.activeInHierarchy,"native Play owns one active attic storm/audio "+viewer);
 }
 arena.SetLocalViewer(3,true);Check(arena.Camera.gameObject.activeSelf,"hallway spectator view");
 Check(!fishArtwork.enabled&&ghostArtwork.enabled,"spectator hides near decoration and retains far opponent without removing physics");
 Check(!arena.ApplyInput(3,Vector2.one),"spectator paddle input denied");
 arena.SetLocalViewer(3,false);Check(!arena.Camera.gameObject.activeSelf,"spectator view exit");
 Check(!storm.AmbientActive&&!storm.gameObject.activeSelf&&!audio.gameObject.activeSelf&&!audio.GetComponentsInChildren<AudioSource>(true).Any(s=>s.isPlaying),"native Play return stops attic clock and every rain/thunder source");
 arena.SetLocalParticipant(3);Check(!arena.Camera.gameObject.activeSelf,"participant API still excludes spectators");
 arena.SetLocalViewer(0,true);Check(!arena.Camera.gameObject.activeSelf,"zero identity cannot enable view");
 arena.SetLocalParticipant(1);Check(arena.Camera.gameObject.activeSelf,"participant view");
 stage=1;entered=EditorApplication.timeSinceStartup;return;
 }
 var artState=JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot);
 if(artState!=null&&(artState.Epoch!=checkedArtEpoch||artState.FishScore!=checkedArtScore))
 {
     var activeRig=presentation.GhostActor.GetComponentInChildren<BossSegmentedRig>(true);
     Check(activeRig.VariantIndex==BossGhostLifeStage.VariantIndex(artState.FishScore),"physical/replicated goals select correct ghost life variant");
     checkedArtEpoch=artState.Epoch;checkedArtScore=artState.FishScore;
 }
 double t=EditorApplication.timeSinceStartup-entered;
 if(stage==1){
 arena.ApplyInput(1,new Vector2(1,1));
 if(t<1.4||JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot).Serving)return;
 Check(arena.Puck.linearVelocity.sqrMagnitude>0,"native serve moves cue ball");
 firstSnapshot=arena.Snapshot;
 Render();
 arena.Puck.position=new Vector3(1000,1.8f,4.7f);arena.Puck.linearVelocity=new Vector3(0,0,15);
 fishGoalsLaunched=1;stage=2;entered=EditorApplication.timeSinceStartup;
 }else if(stage==2){
 var pending=JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot);
 var activeRig=presentation.GhostActor.GetComponentInChildren<BossSegmentedRig>(true);Check(activeRig.VariantIndex==BossGhostLifeStage.VariantIndex(pending.FishScore),"physical fish goals advance the correct ghost life art");
 if(!pending.ResultIssued&&pending.FishScore==fishGoalsLaunched&&!pending.Serving&&fishGoalsLaunched<3)
 {fishGoalsLaunched++;arena.Puck.position=new Vector3(1000,1.8f,4.7f);arena.Puck.linearVelocity=new Vector3(0,0,15);return;}
 if(pending.ResultIssued&&pending.ReactionRemaining>.2f&&!capturedReaction){
 Check(arena.Active&&results==0&&presentation.Playing,"terminal Ink reaction holds shared view before completion");
 Check(!arena.TryBegin(3,4)&&!arena.ApplyInput(1,Vector2.one),"terminal reaction cannot admit input or re-entry");
 Check(presentation.FishId==1&&presentation.GhostId==2,"visual identities match admitted players");
 Render("02-win-lose-reactions");capturedReaction=true;
 }
 if(arena.Active)return;
 Check(results==1,"native fish goal finishes once");
 Check(capturedReaction&&presentation.EventsPlayed==3&&!presentation.Playing&&Baseline(),"winning reaction once and baseline restored");
 var s=JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot);
 Check(s.WinnerFish&&s.FishScore==3,"fish threshold three");
 Check(!arena.RegisterGoal(true),"repeat collision rejected");
 Check(arena.TryBegin(1,2),"re-entry");arena.SetParticipantsReady(true);
 stage=3;entered=EditorApplication.timeSinceStartup;
 }else if(stage==3){
 if(t<1.3||JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot).Serving)return;
 arena.Puck.position=new Vector3(1000,1.8f,-4.7f);arena.Puck.linearVelocity=new Vector3(0,0,-15);
 stage=4;entered=EditorApplication.timeSinceStartup;
 }else if(stage==4){
 var s=JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot);
 if(s.GhostScore<1)return;
 if(arena.Active)return;
 stage=6;entered=EditorApplication.timeSinceStartup;
 }else if(stage==6){
 if(arena.Active)return;
 Check(results==2,"ghost one completes once");
 Check(presentation.EventsPlayed==4&&Baseline()&&!presentation.Playing,"single ghost goal final reaction restores baseline");
 var s=JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot);
 Check(!s.WinnerFish&&s.GhostScore==1,"ghost threshold one");
 Check(arena.TryBegin(5,6)&&arena.Cancel(6)&&results==3,"departure cancels");
 Check(!arena.Cancel(5)&&!arena.Active,"duplicate departure");
 string latest=arena.Snapshot;arena.SetAuthority(false);
 Check(arena.ApplySnapshot(latest),"observer reconstruction");
 Check(!arena.ApplySnapshot(firstSnapshot)&&!arena.ApplySnapshot(latest),"stale and duplicate snapshot");
 Check(!arena.ApplyInput(5,Vector2.one)&&!arena.RegisterGoal(true),"observer input and scoring reject");
 arena.SetLocalParticipant(0);Check(!arena.Camera.gameObject.activeSelf,"view returns");
 var replay=JsonUtility.FromJson<BossArenaSnapshot>(latest);replay.Epoch++;replay.Revision=1;replay.Cancelled=false;replay.Active=true;replay.ResultIssued=false;replay.ReactionEvent=1;replay.ReactionRemaining=.7f;
 Check(arena.ApplySnapshot(JsonUtility.ToJson(replay))&&presentation.Playing,"observer reconstructs authoritative in-progress reaction");
 int eventCount=presentation.EventsPlayed;replay.Revision++;
 Check(arena.ApplySnapshot(JsonUtility.ToJson(replay))&&presentation.EventsPlayed==eventCount,"snapshot revision cannot replay same reaction");
 arena.SetLocalViewer(5,false);Check(!presentation.Playing&&Baseline(),"viewer exit interrupts and restores Ink transforms");
 replay.Epoch++;replay.Revision=1;Check(arena.ApplySnapshot(JsonUtility.ToJson(replay))&&presentation.Playing,"new match epoch permits reaction again");
 presentation.enabled=false;Check(!presentation.Playing&&Baseline(),"component interruption restores pose");presentation.enabled=true;
 replay.Revision++;replay.Cancelled=true;replay.ReactionRemaining=0;Check(arena.ApplySnapshot(JsonUtility.ToJson(replay))&&!presentation.Playing&&Baseline(),"authoritative cancellation resets reaction");
 arena.SetAuthority(true);Check(arena.TryBegin(7,8),"fresh authority match after observer interruption");arena.SetParticipantsReady(true);
 fishGoalsLaunched=0;stage=7;entered=EditorApplication.timeSinceStartup;return;
 }else if(stage==7){
 var progress=JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot);
 if(t<1.3||progress.Serving||progress.FishScore!=fishGoalsLaunched)return;
 fishGoalsLaunched++;
 arena.Puck.position=new Vector3(1000,1.8f,4.7f);arena.Puck.linearVelocity=new Vector3(0,0,15);
 if(fishGoalsLaunched==3){stage=8;entered=EditorApplication.timeSinceStartup;}
 }else if(stage==8){
 var pending=JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot);if(!pending.ResultIssued)return;
 Check(arena.Active&&presentation.Playing&&results==3,"physical terminal goal holds completion for reaction");
 Check(arena.Cancel(8)&&!arena.Active&&results==4&&Baseline()&&!presentation.Playing,"participant departure cancels pending terminal reaction once");
 Check(JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot).Cancelled&&!arena.Cancel(7),"cancelled terminal snapshot and duplicate departure rejection");
 stage=9;entered=EditorApplication.timeSinceStartup;
 }else if(stage==9){
 if(t<1.2)return;Check(results==4&&Baseline(),"cancelled reaction cannot deliver a delayed second result");
 Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"/results.txt","PASS "+checks+" real arena checks; native trigger fish3 ghost1; "+results+" results.");
 Debug.Log("BOSS VALIDATION PASSED "+checks);
 Finish(0);
 }
 }catch(Exception e){Debug.LogException(e);Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"/results.txt","FAIL "+e);Finish(1);}
 }
 static bool Baseline()=>presentation.FishActor.anchoredPosition.sqrMagnitude<.00001f&&presentation.GhostActor.anchoredPosition.sqrMagnitude<.00001f&&Quaternion.Angle(presentation.FishActor.localRotation,Quaternion.identity)<.01f&&Quaternion.Angle(presentation.GhostActor.localRotation,Quaternion.identity)<.01f;
 static void Render(string name="arena"){
 var c=arena.Camera;var rt=new RenderTexture(1280,900,24);c.targetTexture=rt;c.Render();RenderTexture.active=rt;
 var image=new Texture2D(1280,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,900),0,0);image.Apply();
 Directory.CreateDirectory(Evidence);File.WriteAllBytes(Evidence+"/"+name+".png",image.EncodeToPNG());
 c.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
 }
}
#endif

