#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using HauntedFish.BossFight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class BossArenaValidation {
 static BossArenaManager arena;
 static BossCharacterPresentation presentation;
 static bool capturedReaction, checkedSpin;
 static int stage,checks,results;
 static double entered;
 static string firstSnapshot;
 static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Debug.Log("BOSS CHECK "+name);}
 [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool("BossArenaValidation.Armed", false)) return;
        entered = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    public static void Run(){
 SessionState.SetBool("BossArenaValidation.Armed", true);
        EditorSceneManager.OpenScene("Assets/Scenes/BossFight.unity");
 EditorApplication.update+=Tick;EditorApplication.isPlaying=true;entered=EditorApplication.timeSinceStartup;
 }
 static void Tick(){
 try {
 if(EditorApplication.timeSinceStartup-entered>45)throw new Exception("Boss test timeout stage "+stage);
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
 arena.SetAuthority(true);arena.Completed+=r=>results++;
 Check(arena.TryBegin(1,2),"authority begins");
 Check(!arena.ApplyInput(1,Vector2.one),"admission blocks input");
 arena.SetParticipantsReady(true);Check(!arena.TryBegin(3,4),"busy arena");
 arena.SetLocalViewer(3,true);Check(arena.Camera.gameObject.activeSelf,"hallway spectator view");
 Check(!arena.ApplyInput(3,Vector2.one),"spectator paddle input denied");
 arena.SetLocalViewer(3,false);Check(!arena.Camera.gameObject.activeSelf,"spectator view exit");
 arena.SetLocalParticipant(3);Check(!arena.Camera.gameObject.activeSelf,"participant API still excludes spectators");
 arena.SetLocalViewer(0,true);Check(!arena.Camera.gameObject.activeSelf,"zero identity cannot enable view");
 arena.SetLocalParticipant(1);Check(arena.Camera.gameObject.activeSelf,"participant view");
 stage=1;entered=EditorApplication.timeSinceStartup;return;
 }
 double t=EditorApplication.timeSinceStartup-entered;
 if(stage==1){
 arena.ApplyInput(1,new Vector2(1,1));
 if(t<1.4||JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot).Serving)return;
 Check(arena.Puck.linearVelocity.sqrMagnitude>0,"native serve moves cue ball");
 firstSnapshot=arena.Snapshot;
 Render();
 arena.Puck.position=new Vector3(1000,1.8f,4.7f);arena.Puck.linearVelocity=new Vector3(0,0,15);
 stage=2;entered=EditorApplication.timeSinceStartup;
 }else if(stage==2){
 var pending=JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot);
 if(pending.ResultIssued&&pending.ReactionRemaining>.2f&&!capturedReaction){
 Check(arena.Active&&results==0&&presentation.Playing,"terminal Ink reaction holds shared view before completion");
 Check(!arena.TryBegin(3,4)&&!arena.ApplyInput(1,Vector2.one),"terminal reaction cannot admit input or re-entry");
 Check(presentation.FishId==1&&presentation.GhostId==2,"visual identities match admitted players");
 Render("02-win-lose-reactions");capturedReaction=true;
 }
 if(arena.Active)return;
 Check(results==1,"native fish goal finishes once");
 Check(capturedReaction&&presentation.EventsPlayed==1&&!presentation.Playing&&Baseline(),"winning reaction once and baseline restored");
 var s=JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot);
 Check(s.WinnerFish&&s.FishScore==1,"fish threshold one");
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
 Check(arena.Active&&results==1,"ghost one continues");
 stage=5;entered=EditorApplication.timeSinceStartup;
 }else if(stage==5){
 var during=JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot);
 if(during.ReactionRemaining>0&&during.ReactionRemaining<.3f&&!checkedSpin){
 Check(presentation.Playing&&Quaternion.Angle(presentation.FishActor.localRotation,Quaternion.identity)>5,"conceding fish Ink frustration spins");
 Check(presentation.EventsPlayed==2,"first ghost score emits exactly one new event");Render("03-score-frustration-spin");checkedSpin=true;
 }
 if(t<1.3||during.Serving)return;
 Check(checkedSpin&&Baseline()&&!presentation.Playing,"new serve restores both character baselines");
 arena.Puck.position=new Vector3(1000,1.8f,-4.7f);arena.Puck.linearVelocity=new Vector3(0,0,-15);
 stage=6;entered=EditorApplication.timeSinceStartup;
 }else if(stage==6){
 if(arena.Active)return;
 Check(results==2,"ghost two completes once");
 Check(presentation.EventsPlayed==3&&Baseline()&&!presentation.Playing,"second ghost score final reaction repeats cleanly");
 var s=JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot);
 Check(!s.WinnerFish&&s.GhostScore==2,"ghost threshold two");
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
 stage=7;entered=EditorApplication.timeSinceStartup;return;
 }else if(stage==7){
 if(t<1.3||JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot).Serving)return;
 arena.Puck.position=new Vector3(1000,1.8f,4.7f);arena.Puck.linearVelocity=new Vector3(0,0,15);
 stage=8;entered=EditorApplication.timeSinceStartup;
 }else if(stage==8){
 var pending=JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot);if(!pending.ResultIssued)return;
 Check(arena.Active&&presentation.Playing&&results==3,"physical terminal goal holds completion for reaction");
 Check(arena.Cancel(8)&&!arena.Active&&results==4&&Baseline()&&!presentation.Playing,"participant departure cancels pending terminal reaction once");
 Check(JsonUtility.FromJson<BossArenaSnapshot>(arena.Snapshot).Cancelled&&!arena.Cancel(7),"cancelled terminal snapshot and duplicate departure rejection");
 stage=9;entered=EditorApplication.timeSinceStartup;
 }else if(stage==9){
 if(t<1.2)return;Check(results==4&&Baseline(),"cancelled reaction cannot deliver a delayed second result");
 Directory.CreateDirectory("BossFightEvidence");File.WriteAllText("BossFightEvidence/results.txt",checks+" real arena checks passed; native trigger fish1 ghost2; "+results+" results.");
 Debug.Log("BOSS VALIDATION PASSED "+checks);
 EditorApplication.update-=Tick;SessionState.SetBool("BossArenaValidation.Armed",false);EditorApplication.Exit(0);
 }
 }catch(Exception e){Debug.LogException(e);EditorApplication.update-=Tick;SessionState.SetBool("BossArenaValidation.Armed",false);EditorApplication.Exit(1);}
 }
 static bool Baseline()=>presentation.FishActor.anchoredPosition.sqrMagnitude<.00001f&&presentation.GhostActor.anchoredPosition.sqrMagnitude<.00001f&&Quaternion.Angle(presentation.FishActor.localRotation,Quaternion.identity)<.01f&&Quaternion.Angle(presentation.GhostActor.localRotation,Quaternion.identity)<.01f;
 static void Render(string name="arena"){
 var c=arena.Camera;var rt=new RenderTexture(1280,900,24);c.targetTexture=rt;c.Render();RenderTexture.active=rt;
 var image=new Texture2D(1280,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,900),0,0);image.Apply();
 Directory.CreateDirectory("BossFightEvidence");File.WriteAllBytes("BossFightEvidence/"+name+".png",image.EncodeToPNG());
 c.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
 }
}
#endif

