#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HauntedFish.BossFight;
// Explicitly run in the granted editor, with the authored BossFight scene already in Play Mode.
// This never starts another editor, opens another project, fabricates contestants or changes persistent art.
public sealed class BossDefenseNativeRegression : MonoBehaviour
{
 static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(o);
 BossArenaManager arena;int checks;const string Folder="Tools/BossFightTests/Evidence/expanded-native";
 public static void RunInCurrentPlayScene()
 {
  if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode in the granted authored BossFight scene first.");
  var arena=FindFirstObjectByType<BossArenaManager>();if(!arena||arena.Active)throw new Exception("Idle authored standalone arena required.");
  var observer=new GameObject("Editor-only boss regression observer").AddComponent<BossDefenseNativeRegression>();observer.arena=arena;observer.StartCoroutine(observer.Execute());
 }
 void Check(bool condition,string label){if(!condition)throw new Exception(label);checks++;Debug.Log("BOSS_DEFENSE_NATIVE_CHECK: "+label);}
 IEnumerator Execute()
 {
  var work=Tests();while(true){object next;try{if(!work.MoveNext())break;next=work.Current;}catch(Exception e){Debug.LogException(e);arena.StopEditorPractice();Finish(false,e.ToString());yield break;}yield return next;}
  arena.StopEditorPractice();Finish(true,"authored geometry, physical two-axis dragging and real cue-ball contacts");
 }
 void Finish(bool pass,string result)
 {
  Directory.CreateDirectory(Folder);string message=(pass?"PASS":"FAIL")+" native checks="+checks+" "+result;File.WriteAllText(Folder+"/results.txt",message);Debug.Log("BOSS_DEFENSE_NATIVE_"+message);
  Destroy(gameObject);if(Environment.GetCommandLineArgs().Contains("--boss-defense-validation"))EditorApplication.Exit(pass?0:1);
 }
 void Capture(string name,Camera camera)
 {
  Directory.CreateDirectory(Folder);var rt=new RenderTexture(1280,720,24);rt.Create();var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);var active=RenderTexture.active;var oldTarget=camera.targetTexture;
  try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Folder+"/"+name,texture.EncodeToPNG());}
  finally{camera.targetTexture=oldTarget;RenderTexture.active=active;Destroy(texture);Destroy(rt);}
 }
 IEnumerator Tests()
 {
  var board=arena.Defenses;var drag=arena.DragInput;Check(FindObjectsByType<BossDefensePiece>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(p=>p.gameObject.scene==arena.gameObject.scene)==6,"exactly six defense components, no prefab reconnection duplicates");
  Check(board&&board.ValidateAuthoredBindings(out _)&&drag&&drag.ValidateAuthoredBindings(out _),"six real authored art slots and input bindings");
  var bodies=FindObjectsByType<Rigidbody>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(b=>b.gameObject.scene==arena.gameObject.scene).ToArray();var strikers=Array.FindAll(bodies,b=>b!=arena.Puck&&b.GetComponent<BossDefensePiece>()==null);
  Check(strikers.Length==2,"exactly two physical strikers; spectators add no striker: "+string.Join(",",strikers.Select(b=>b.name)));
  Check(strikers[0].constraints==(RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation)&&strikers[1].constraints==strikers[0].constraints,"both strikers permit X and Z; freeze vertical and rotation");
  Check(arena.Puck.constraints==(RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ),"cue-ball permits lateral velocity and horizontal bounce");
  arena.BeginEditorPractice();arena.SetLocalViewer(1,true);yield return new WaitForSeconds(1.2f);var fish=strikers[0].position.z<strikers[1].position.z?strikers[0]:strikers[1];var start=fish.position;var camera=arena.Camera;Capture("fish-before.png",camera);
  drag.Tick(1,board.Epoch,true,true,camera.WorldToScreenPoint(start),false,false,false,.05f);Check(!drag.StrikerCaptured,"hover alone cannot move striker");
  drag.Tick(1,board.Epoch,true,true,camera.WorldToScreenPoint(start),true,true,false,.05f);Check(drag.StrikerCaptured,"actual camera ray captures owned physical striker");
  var target=start+new Vector3(.9f,0,.8f);for(int i=0;i<8;i++){var axis=drag.Tick(1,board.Epoch,true,true,camera.WorldToScreenPoint(target),false,true,false,.05f);arena.SetEditorPracticeInput(axis,false);yield return new WaitForFixedUpdate();}
  Check(fish.position.x>start.x+.1f&&fish.position.z>start.z+.1f,"real Rigidbody moved laterally AND forward under drag");Capture("fish-after-drag.png",camera);Check(fish.position.z<=-.5f&&Mathf.Abs(fish.position.x-1000)<3,"drag stays in fish half");
  drag.Tick(1,board.Epoch,true,true,camera.WorldToScreenPoint(target),false,false,true,.05f);Check(!drag.StrikerCaptured,"release ends capture");arena.SetEditorPracticeInput(Vector2.zero,false);
  var puckStart=arena.Puck.position;drag.Tick(1,board.Epoch,true,true,camera.WorldToScreenPoint(puckStart),true,true,false,.05f);Check(!drag.StrikerCaptured,"cue-ball cannot be captured as striker");drag.Cancel();
  // Test a real ghost-end projection against the same horizontal two-axis physical policy.
  var ghost=fish==strikers[0]?strikers[1]:strikers[0];arena.SetEditorPracticeViewer(BossViewerSide.Ghost);var ghostStart=ghost.position;var ghostCamera=arena.Camera;drag.Tick(2,board.Epoch,true,true,ghostCamera.WorldToScreenPoint(ghostStart),true,true,false,.05f);Check(drag.StrikerCaptured,"ghost-end actual ray captures only ghost striker");
  for(int i=0;i<8;i++){var axis=drag.Tick(2,board.Epoch,true,true,ghostCamera.WorldToScreenPoint(ghostStart+new Vector3(.8f,0,-.8f)),false,true,false,.05f);arena.SetEditorPracticeGhostInput(axis);yield return new WaitForFixedUpdate();}
  Check(ghost.position.x>ghostStart.x+.1f&&ghost.position.z<ghostStart.z-.1f,"ghost physical Rigidbody moves on both mirrored axes");Capture("ghost-after-drag.png",ghostCamera);drag.Cancel();arena.SetEditorPracticeGhostInput(Vector2.zero);arena.SetEditorPracticeViewer(BossViewerSide.Fish);
  Check(arena.AxisToTarget(ghost.position,ghost.position+new Vector3(.5f,0,-.5f),.05f,true).x>0,"mirrored ghost world X target keeps lateral direction");
  // Native click/drag of an actual rack model, using the same production capture/validation event.
  arena.Puck.isKinematic=true;arena.Puck.position=new Vector3(1000,1.7f,1.2f);fish.position=new Vector3(1001.5f,1.7f,-3.6f);board.Begin(board.Epoch+1,1,2,true);board.SetPlayable(true,true);Physics.SyncTransforms();
  Action<int,uint,Vector2> place=(slot,seq,targetPosition)=>{bool accepted=board.Place(1,board.Epoch,slot,seq,targetPosition);drag.Reply(slot,seq,board.Epoch,accepted);};drag.DefenseMoveRequested+=place;
  drag.Tick(1,board.Epoch,true,true,camera.WorldToScreenPoint(board.Piece(0).RackPosition+Vector3.up*.3f),true,true,false,.05f);Check(drag.DefenseCaptured&&!drag.StrikerCaptured,"actual bottle rack ray captures only owned defense");
  drag.Tick(1,board.Epoch,true,true,camera.WorldToScreenPoint(board.TablePosition(new Vector2(-1.5f,-2.5f))+Vector3.up*.3f),false,true,false,.05f);Check(board.Piece(0).State.Placed,"native drag event validates and places actual 3D bottle");Capture("defense-drag-placed.png",camera);drag.DefenseMoveRequested-=place;drag.Cancel();
  Check(!board.Place(99,board.Epoch,1,1,new Vector2(1,-2)),"spectator placement rejected by native authority");Check(!board.Place(2,board.Epoch,1,1,new Vector2(1,-2)),"foreign contestant cannot place fish slot");Check(!board.Place(1,board.Epoch+1,1,1,new Vector2(1,-2)),"stale epoch rejected by native authority");Check(!board.Place(1,board.Epoch,1,1,new Vector2(1,2)),"opponent-half placement rejected");Check(!board.Place(1,board.Epoch,1,1,new Vector2(-1.5f,-2.5f)),"stacking rejected");
  for(int slot=0;slot<3;slot++)
  {
   // Begin resets health/racks, not a new network match or user reward.
   board.Begin(board.Epoch+1,1,2,true);board.SetPlayable(true,true);var spot=new Vector2(-1.5f,-2);
   arena.Puck.isKinematic=true;arena.Puck.position=new Vector3(1000,1.7f,1.2f);fish.position=new Vector3(1001.5f,1.7f,-3.6f);
   Check(board.Place(1,board.Epoch,slot,1,spot),"native placement accepted for own slot "+slot);var piece=board.Piece(slot);Check(piece.GetComponentsInChildren<MeshRenderer>(true).Length>0,"defense uses actual 3D source mesh "+slot);
   int health=slot+1;
   for(int hit=0;hit<health;hit++)
   {
    board.ContactEnded(slot,arena.Puck.GetInstanceID());piece.ResetContacts();arena.Puck.position=board.TablePosition(spot)+new Vector3(0,.08f,-1.1f);arena.Puck.isKinematic=false;arena.Puck.linearVelocity=new Vector3(0,0,6);
    float deadline=Time.unscaledTime+1.5f;int before=piece.State.Health;while(piece.State.Health==before&&Time.unscaledTime<deadline)yield return new WaitForFixedUpdate();
    Check(piece.State.Health==before-1,"real cue-ball collision removes exactly one health "+slot+"/"+hit);Check(piece.State.Broken||arena.Puck.linearVelocity.z<0,"surviving physical defense bounces cue-ball");
    arena.Puck.isKinematic=true;arena.Puck.position=new Vector3(1000,1.7f,1.2f);yield return new WaitForFixedUpdate();
   }
   Check(piece.State.Broken,"expected 1/2/3 real contacts break source model "+slot);yield return new WaitForSeconds(.22f);Capture("defense-"+slot+"-broken.png",camera);
  }
  var view=Get<BossArenaView>(arena,"_Views");view.Select(BossViewerSide.Ghost,true);Capture("ghost-end.png",view.CurrentCamera);view.Select(BossViewerSide.Spectator,true);Capture("spectator.png",view.CurrentCamera);view.Select(BossViewerSide.Fish,true);
  board.Begin(board.Epoch+1,1,2,true);Check(board.Piece(0).State.Health==1&&board.Piece(1).State.Health==2&&board.Piece(2).State.Health==3&&!board.Piece(0).State.Placed,"new match restores rack health");
  var rig=Get<BossSegmentedRig>(view,"_GhostFraming");var boundsLog=new System.Collections.Generic.List<string>();
  for(int variant=0;variant<3;variant++){
   arena.GetType().GetField("_PracticeFishScore",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(arena,variant);rig.SetLifeStage(variant);
   for(int reaction=0;reaction<2;reaction++){rig.PlayReaction(reaction==0,variant==2,.9f,.9f);yield return new WaitForSeconds(.35f);Check(rig.VariantIndex==variant,"native life-stage art selected "+variant);Check(rig.HasAnimatedPose,"native boss reaction animates authored segments "+variant+"/"+reaction);Capture("variant-"+variant+"-reaction-"+reaction+".png",camera);
    foreach(var artwork in rig.ActiveArtwork.OfType<SpriteRenderer>()){var rect=OpaqueViewport(artwork,camera);boundsLog.Add(variant+"/"+reaction+" "+artwork.name+" viewport="+rect);Check(rect.yMax<=.985f&&rect.xMin>=-.03f&&rect.xMax<=1.03f,"opaque reaction art within frame "+variant+"/"+reaction+"/"+artwork.name);}
   }
  }File.WriteAllLines(Folder+"/reaction-framing.txt",boundsLog);

 }
 static Rect OpaqueViewport(SpriteRenderer renderer,Camera camera){var sprite=renderer.sprite;var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);try{string path=AssetDatabase.GetAssetPath(sprite.texture);if(!texture.LoadImage(File.ReadAllBytes(path)))throw new Exception("Sprite source decode failed");var pixels=texture.GetPixels32();var rect=sprite.rect;float minX=float.PositiveInfinity,minY=minX,maxX=float.NegativeInfinity,maxY=maxX;for(int y=(int)rect.y;y<rect.yMax;y+=6)for(int x=(int)rect.x;x<rect.xMax;x+=6){if(pixels[y*texture.width+x].a<64)continue;var local=new Vector3((x-rect.x-sprite.pivot.x)/sprite.pixelsPerUnit,(y-rect.y-sprite.pivot.y)/sprite.pixelsPerUnit,0);if(renderer.flipX)local.x=-local.x;if(renderer.flipY)local.y=-local.y;var point=camera.WorldToViewportPoint(renderer.transform.TransformPoint(local));minX=Mathf.Min(minX,point.x);maxX=Mathf.Max(maxX,point.x);minY=Mathf.Min(minY,point.y);maxY=Mathf.Max(maxY,point.y);}return Rect.MinMaxRect(minX,minY,maxX,maxY);}finally{Destroy(texture);}}

}
#endif
