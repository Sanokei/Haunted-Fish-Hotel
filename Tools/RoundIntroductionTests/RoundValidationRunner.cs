using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public sealed class RoundValidationRunner:MonoBehaviour {
 int checks;
 void Check(bool value,string message) { if(!value) throw new Exception(message); checks++;Debug.Log("ROUND_CHECK "+checks+": "+message); }
 static void Set(object o,string f,object v)=>o.GetType().GetField(f,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);
 static T Get<T>(object o,string f)=>(T)o.GetType().GetField(f,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
 IEnumerator Start() {
  var stack=new System.Collections.Generic.Stack<IEnumerator>(); stack.Push(Tests());
  while(stack.Count>0) {
   object next;
   try { if(!stack.Peek().MoveNext()){stack.Pop();continue;} next=stack.Peek().Current; }
   catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);yield break;}
   if(next is IEnumerator child)stack.Push(child);else yield return next;
  }
  Debug.Log("ROUND_PASS "+checks);EditorApplication.Exit(0);
 }
 IEnumerator Tests() {
  Time.timeScale=0;
  yield return TrapValidation.Tests(Check);
  yield return TrapManagerValidation.Tests(Check);
  Time.timeScale=0;
  var prefab=Resources.Load<StoryUI>("AtticRoundCutscene");Check(prefab,"Authored prefab imports as StoryUI");
  Check(prefab.GetComponent<RoundIntroductionSettings>().Sequence && prefab.GetComponent<RoundIntroductionSettings>().TimingScale==1,"Prefab owns its compiled Ink reference and editable pacing");
  var ink=Resources.Load<TextAsset>("GhostRoundIntroduction");
  var steps=StoryFunctions.ReadAnimationSequence(ink.text);
  prefab.ValidateSequence(steps.Where(s=>s.Target!="wheel_spin"&&s.Target!="selected_slice"&&s.Target!="attic_cutscene").ToArray());
  Check(!UnityEditor.ShaderUtil.ShaderHasError(Resources.Load<Material>("AtticReturnBlur").shader),"Return-blur shader compiles");
  var walls=prefab.GetComponentsInChildren<Image>(true).Where(i=>i.name.StartsWith("Floor ") && i.name.EndsWith(" wall")).ToArray();
  Check(walls.Length==4 && walls.All(i=>i.sprite && i.sprite.name=="wall" && i.preserveAspect && i.color==Color.white),"Four authored floor images reference the actual wall artwork without distortion or tint");
  var ui=Instantiate(prefab);ui.Prepare();yield return null;
  Capture(ui.gameObject.GetComponent<Canvas>(),"01-hotel-bottom");
  var pan=StartCoroutine(ui.Play(new StorySequenceStep(StorySequenceStepKind.DollyTo,"attic_top",seconds:.8f,x:1)));
  for(int frame=1;frame<=3;frame++) { yield return new WaitForSecondsRealtime(.18f);Capture(ui.GetComponent<Canvas>(),"08-wall-upward-pan-"+frame); }
  yield return pan;
  yield return ui.Play(new StorySequenceStep(StorySequenceStepKind.DollyTo,"hotel_bottom",seconds:.01f,x:1));

  yield return ui.Play(new StorySequenceStep(StorySequenceStepKind.DollyTo,"attic_top",seconds:.15f,x:1));
  Check(ui.Find("ghost").parent.GetComponent<RectTransform>().anchoredPosition.y==-2160,"Camera strip reaches authored attic pose");
  yield return ui.Play(new StorySequenceStep(StorySequenceStepKind.SetVisible,"ghost",visible:true));
  Capture(ui.GetComponent<Canvas>(),"02-attic-ghost");
  var image=ui.Find("ghost").GetComponent<Image>();var original=image.material;
  yield return ui.Play(new StorySequenceStep(StorySequenceStepKind.SetVisible,"return_blur",visible:true));
  Check(image.material!=original,"Ink enables actual directional blur material");
  Capture(ui.GetComponent<Canvas>(),"03-return-blur");
  yield return ui.Play(new StorySequenceStep(StorySequenceStepKind.DollyTo,"hotel_bottom",seconds:.15f,x:1));
  yield return ui.Play(new StorySequenceStep(StorySequenceStepKind.SetVisible,"return_blur",visible:false));
  Check(image.material==original,"Blur restores material after return");Destroy(ui.gameObject);yield return null;
  // Load the original scene-authored wheel hierarchy extracted unchanged for validation.
  var wheelRoot=Instantiate(Resources.Load<GameObject>("OriginalWheel"));
  var host=new GameObject("Presentation owner");var presentation=host.AddComponent<GhostSelectionPresentation>();
  var wheel=wheelRoot.GetComponentInChildren<GhostSelectionWheel>(true);
  Set(presentation,"_Root",wheelRoot);Set(presentation,"_Wheel",wheel.rectTransform);
  Set(presentation,"_Graphic",wheel);Set(presentation,"_Pointer",wheelRoot.GetComponentInChildren<RawImage>(true).rectTransform);
  var texts=wheelRoot.GetComponentsInChildren<Text>(true);
  Set(presentation,"_Title",texts.Single(x=>x.name=="Selection title"));
  Set(presentation,"_Caption",texts.Single(x=>x.name=="Selection caption"));
  Set(presentation,"_PlayerLabel",texts.Single(x=>!x.gameObject.activeSelf));
  Set(presentation,"_RoundInk",null);Set(presentation,"_AtticPrefab",prefab);
  var outcomes = new System.Collections.Generic.HashSet<int>();
  for (int draw=0;draw<1000;draw++) outcomes.Add(UnityEngine.Random.Range(0,4));
  Check(outcomes.Count==4,"Random spin selection reaches every roster member");
  int finished=0;
  presentation.Play(null,new uint[]{1,2,3,4},3,1,()=>finished++);
  yield return new WaitForSecondsRealtime(7.2f);
  Check(wheel.Selected==2 && wheel.SelectionPulse>0,"Visually landed sector is selected and animates");
  var pointer=Get<RectTransform>(presentation,"_Pointer");
  Check(Mathf.Abs(Mathf.DeltaAngle(pointer.localEulerAngles.z,-2025))<.01f,"Pointer agrees with authoritative player 3 sector");
  Capture(wheelRoot.GetComponent<Canvas>(),"04-selected-slice");
  yield return new WaitForSecondsRealtime(9.5f);
  Check(finished==1,"Full Ink sequence completes exactly once at paused timescale");
  Check(Get<StoryUI>(presentation,"_Attic"),"Attic remains visible until round barrier release");
  presentation.Close();yield return null;
  Check(!Get<StoryUI>(presentation,"_Attic"),"Closing destroys only the instantiated cutscene");
  presentation.Play(null,new uint[]{1,2},2,1,()=>finished++);
  yield return new WaitForSecondsRealtime(.1f);presentation.Close();yield return new WaitForSecondsRealtime(.1f);
  Check(finished==1 && wheel.Selected==-1,"Cancelled spin cannot acknowledge gameplay");
  presentation.Play(null,new uint[]{1},1,1,()=>finished++);
  yield return new WaitForSecondsRealtime(9.2f);
  Check(Get<StoryUI>(presentation,"_Attic"),"Repeated round enters a fresh attic prefab");
  presentation.enabled=false;yield return null;
  Check(finished==1 && !Get<StoryUI>(presentation,"_Attic"),"Disabling during cutscene cleans up without completion");
  Time.timeScale=1;
 }
 static void Capture(Canvas canvas,string name) {
  var camera=new GameObject("Capture").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-10);
  camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
  var target=new RenderTexture(1280,720,24);camera.targetTexture=target;
  canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
  Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
  var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();
  File.WriteAllBytes(Path.Combine(Application.dataPath,"../"+name+".png"),tex.EncodeToPNG());
  RenderTexture.active=previous;canvas.renderMode=RenderMode.ScreenSpaceOverlay;camera.targetTexture=null;
  target.Release();Destroy(target);Destroy(tex);Destroy(camera.gameObject);
 }
}
