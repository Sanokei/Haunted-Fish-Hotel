// STAGED ONLY. Publish runtime flicker + this authoring helper after exclusive Lobby grant and settings mixer publication.
using System;
using System.IO;
using System.Linq;
using HauntedFish.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class LobbyFireplaceAuthoring {
 const string E=@"C:\Users\wkeif\Documents\Codex\2026-10-09\task\LobbyMoodPreparation";
 const string ClipPath="Assets/Art/FireplaceAudio/HFH_Lobby_Fireplace_GentleLoop_60s_Mono48k.wav";
 public static void Apply(){
 if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Idle Edit Mode required");
 for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve unrelated unsaved changes first");
 var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);if(!clip||clip.channels!=1||Math.Abs(clip.length-60)>.01)throw new Exception("Verified 60s mono fireplace clip must import first");
 const string path="Assets/Scenes/Lobby.unity";File.Copy(path,System.IO.Path.Combine(E,"Lobby-before-fireplace.unity"),true);var prior=SceneManager.GetActiveScene().path;
 try{
 var scene=EditorSceneManager.OpenScene(path);var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
 var fire=all.Select(x=>x.GetComponent<MeshFilter>()).Single(x=>x&&x.name=="Fireplace | dark firebox back");var center=fire.transform.TransformPoint(fire.sharedMesh.bounds.center);var inward=new Vector3(-center.x,0,0).normalized;if(inward.sqrMagnitude<.9)throw new Exception("Inspect native firebox opening orientation");
 var root=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="Lobby fireplace ambience");if(!root){root=new GameObject("Lobby fireplace ambience");SceneManager.MoveGameObjectToScene(root,scene);}root.transform.position=center;root.transform.rotation=Quaternion.LookRotation(inward,Vector3.up);
 var hearth=Point(root,"Hearth warm flicker",center+inward*.35f+Vector3.up*.2f,new Color(1,.52f,.22f),14,11);
 var bounce=Point(root,"Fireplace room bounce",center+inward*2+Vector3.up*1.5f,new Color(1,.72f,.4f),7,18);
 var audioTransform=root.transform.Find("Fireplace crackle");var audioGo=audioTransform?audioTransform.gameObject:new GameObject("Fireplace crackle");if(!audioTransform)audioGo.transform.SetParent(root.transform);audioGo.transform.position=center+inward*.15f+Vector3.up*.14f;
 var source=audioGo.GetComponent<AudioSource>();if(!source)source=audioGo.AddComponent<AudioSource>();source.clip=clip;source.playOnAwake=false;source.loop=true;source.spatialBlend=1;source.dopplerLevel=0;source.pitch=1;source.volume=.8f;source.minDistance=6;source.maxDistance=28;source.rolloffMode=AudioRolloffMode.Logarithmic;source.priority=160;source.reverbZoneMix=0;LobbyFireplaceSfxBinding.Bind(source);
 var reverb=audioGo.GetComponent<AudioReverbFilter>();if(!reverb)reverb=audioGo.AddComponent<AudioReverbFilter>();reverb.reverbPreset=AudioReverbPreset.User;reverb.room=-1500;reverb.roomHF=-900;reverb.decayTime=.65f;reverb.decayHFRatio=.55f;reverb.reflectionsLevel=-2400;reverb.reflectionsDelay=.015f;reverb.reverbLevel=-2000;reverb.reverbDelay=.03f;reverb.diffusion=70;reverb.density=60;
 var flicker=root.GetComponent<LobbyFireplaceFlicker>();if(!flicker)flicker=root.AddComponent<LobbyFireplaceFlicker>();var so=new SerializedObject(flicker);so.FindProperty("_Hearth").objectReferenceValue=hearth;so.FindProperty("_Bounce").objectReferenceValue=bounce;so.FindProperty("_Crackle").objectReferenceValue=source;so.ApplyModifiedProperties();
 var fill=scene.GetRootGameObjects().Single(x=>x.name=="Lobby indoor practical fill");foreach(var light in fill.GetComponentsInChildren<Light>(true)){if(light.name=="Central muted pool")light.intensity=2.4f;if(light.name=="General diffuse fill")light.intensity=1.5f;EditorUtility.SetDirty(light);}
 EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Lobby save failed");
 File.WriteAllText(System.IO.Path.Combine(E,"fireplace-authored.txt"),"Native firebox center="+center+" inward="+inward+" hearth="+hearth.transform.position+" bounce="+bounce.transform.position+" source="+source.transform.position+" clip="+AssetDatabase.GetAssetPath(clip)+" SFX group="+AssetDatabase.GetAssetPath(source.outputAudioMixerGroup)+"; actual visual/flicker/fullmix/slider/loop acceptance pending.");
 }finally{if(!string.IsNullOrEmpty(prior)&&prior!=path)EditorSceneManager.OpenScene(prior);}
 }
 static Light Point(GameObject root,string name,Vector3 position,Color color,float intensity,float range){var found=root.transform.Find(name);var go=found?found.gameObject:new GameObject(name);if(!found)go.transform.SetParent(root.transform);go.transform.position=position;var light=go.GetComponent<Light>();if(!light)light=go.AddComponent<Light>();light.type=LightType.Point;light.lightmapBakeType=LightmapBakeType.Realtime;light.color=color;light.intensity=intensity;light.range=range;light.shadows=LightShadows.None;return light;}
}
