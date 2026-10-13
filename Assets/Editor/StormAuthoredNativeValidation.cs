using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
public static class StormAuthoredNativeValidation
{
 const string E="C:/Users/wkeif/Documents/Codex/2026-10-09/task/LobbyAtmosphereEvidence";
 static int Checks;
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Checks++;}
 static object Get(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
 static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
 static object Call(object target,string name,params object[] values)=>target.GetType().GetMethod(name,Flags).Invoke(target,values);
 [MenuItem("Tools/Haunted Fish/Validate Authored Storm Prefabs")]
 public static void Run(){if(!LobbyStormRepairTask.RequireSafeEditMode())return;Checks=0;GameObject root=null;try{
 Directory.CreateDirectory(E);root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Storm/StormLightningAttic.prefab");root.SetActive(true);
 var controller=root.GetComponent<LightningEffectManager>();Check(controller.ValidateAuthoredBindings(out var error),"Attic serialized room/Canvas bindings: "+error);
 var canvas=(Canvas)Get(controller,"_FlashCanvas");var lights=(Light[])Get(controller,"_AuthoredRoomLights");Check(lights.Length==1,"One authored attic room light");Check(((LightningEffectManager.WindowPass[])Get(controller,"_AuthoredWindowPasses")).Length==0,"Attic requires no literal window mask");
 Set(controller,"_FlashCanvas",null);Check(!controller.ValidateAuthoredBindings(out error),"Missing authored white frame rejected");Set(controller,"_FlashCanvas",canvas);
 Set(controller,"_AuthoredRoomLights",new Light[]{lights[0],lights[0]});Check(!controller.ValidateAuthoredBindings(out error),"Duplicate authored light rejected");Set(controller,"_AuthoredRoomLights",Array.Empty<Light>());Check(!controller.ValidateAuthoredBindings(out error),"Missing all authored visuals rejected");Set(controller,"_AuthoredRoomLights",lights);
 int starts=0,stops=0,strikes=0;long last=0;controller.AmbientStateChanged+=active=>{if(active)starts++;else stops++;};controller.StrikeStarted+=id=>{Check(id>last,"Strictly increasing strike id");last=id;strikes++;};
 bool originalEnabled=lights[0].enabled;float originalIntensity=lights[0].intensity;Color originalColor=lights[0].color;int objects=Resources.FindObjectsOfTypeAll<GameObject>().Length;
 Check((bool)Call(controller,"Prepare"),"Prepare succeeds on actual authored prefab");Call(controller,"BeginAmbient");Call(controller,"BeginAmbient");Check(starts==1,"Ambient start event is idempotent");
 var enumerator=(IEnumerator)Call(controller,"Strike",true);for(int i=0;i<4;i++)Check(enumerator.MoveNext(),"Actual strike advances white-frame/pulse");Check(strikes==1,"Exactly one event for one actual strike");Check(lights[0].enabled&&lights[0].intensity>0,"Same strike drives authored room light");Check(Resources.FindObjectsOfTypeAll<GameObject>().Length==objects,"Prepare/strike constructs no GameObjects");
 controller.StopAmbient();controller.StopAmbient();Call(controller,"OnDisable");Call(controller,"OnDestroy");Check(stops==1&&!controller.AmbientActive&&!controller.Playing,"Stop/disable/destroy notify once and stop clock");Check(lights[0].enabled==originalEnabled&&lights[0].intensity==originalIntensity&&lights[0].color==originalColor,"Restores authored light state");
 Check((bool)Call(controller,"Prepare"),"Fresh activation prepares without leaked prior state");Call(controller,"BeginAmbient");var second=(IEnumerator)Call(controller,"Strike",false);Check(second.MoveNext()&&strikes==2,"Next lifetime emits one new monotonic strike");controller.StopAmbient();Check(starts==2&&stops==2,"Second lifetime owns one start and stop");
 PrefabUtility.UnloadPrefabContents(root);root=null;
 foreach(var name in new[]{"Indoor","Attic"}){root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Storm/StormAudio"+name+".prefab");var audio=root.GetComponent<HauntedFish.Multiplayer.LobbyStormAudio>();var so=new SerializedObject(audio);var rain=(AudioClip)so.FindProperty("_RainLoop").objectReferenceValue;Check(rain&&rain.channels==2&&((EditorUserBuildSettings.activeBuildTarget==BuildTarget.WebGL&&rain.frequency==44100)||rain.frequency==48000)&&Math.Abs(rain.length-60)<.002&&Math.Abs((double)rain.samples/rain.frequency-60)<.002,name+" native stereo rain retains source60s duration (WebGL AAC or preserved48k)");Check(AssetDatabase.GetAssetPath(rain).Contains("IndoorTransmission")== (name=="Indoor"),name+" correct acoustic variant");var sources=new[]{"_RainSource","_ThunderSource","_ThunderSourceB"}.Select(f=>(AudioSource)so.FindProperty(f).objectReferenceValue).ToArray();Check(sources.All(x=>x&&!x.playOnAwake&&x.spatialBlend==0)&&sources.Distinct().Count()==3,name+" three dedicated authored 2D sources");Check(so.FindProperty("_ThunderClips").arraySize==4,name+" four retained thunder cues");PrefabUtility.UnloadPrefabContents(root);root=null;}
 File.WriteAllText(E+"/storm-native-validation.txt","PASS "+Checks+" native checks: missing/duplicate references, actual strike enumerator emits once, one ambient event per lifetime, no runtime GameObject construction, state restoration/idempotent stop/disable/destroy, indoor/attic clips and source profiles. Audible/live gameplay and hallway projected pixel output not tested.");
 }catch(Exception e){File.WriteAllText(E+"/storm-native-error.txt",e.ToString());Debug.LogException(e);}finally{if(root)PrefabUtility.UnloadPrefabContents(root);}}
}
