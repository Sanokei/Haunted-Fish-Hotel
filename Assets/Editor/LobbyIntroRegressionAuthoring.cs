// STAGED ONLY. Execute only after source baseline guards, exclusive idle-editor grant, and native compilation.
using System;
using System.IO;
using System.Linq;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
public static class LobbyIntroRegressionAuthoring {
 const string E=@"C:\Users\wkeif\Documents\Codex\2026-10-09\task\LobbyMoodPreparation\IntroRegressionStage";
 const string UIPath="Assets/Resources/LobbyIntroductionUI.prefab",DialogPath="Assets/Resources/MultiplayerDialoguePanel.prefab";
 public static void Apply(){
 if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Idle Edit Mode required");
 for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save unrelated user changes first");
 Directory.CreateDirectory(E);File.Copy(UIPath,System.IO.Path.Combine(E,"UI-before-regressions.prefab"),true);File.Copy(DialogPath,System.IO.Path.Combine(E,"Dialogue-before-regressions.prefab"),true);File.Copy("Assets/Scenes/Lobby.unity",System.IO.Path.Combine(E,"Lobby-before-regressions.unity"),true);
 var voice=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/BubbleSpeech.wav");if(!voice)throw new Exception("Existing BubbleSpeech asset missing");
 var root=PrefabUtility.LoadPrefabContents(UIPath);
 try{
 var ui=root.GetComponentInChildren<StoryUI>(true);var so=new SerializedObject(ui);var white=(GameObject)so.FindProperty("_WhiteFrame").objectReferenceValue;var content=(GameObject)so.FindProperty("_Content").objectReferenceValue;var introCanvas=content.GetComponent<Canvas>();if(!introCanvas)throw new Exception("Authored intro Canvas missing");introCanvas.renderMode=RenderMode.ScreenSpaceOverlay;introCanvas.sortingOrder=32765;
 var found=root.transform.Find("Full viewport flash");var flash=found?found.gameObject:new GameObject("Full viewport flash",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));if(!found)flash.transform.SetParent(root.transform,false);flash.layer=5;flash.SetActive(true);var canvas=flash.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32767;var scaler=flash.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=1;
 var rect=(RectTransform)white.transform;rect.SetParent(flash.transform,false);Stretch(rect);var image=white.GetComponent<Image>();image.color=Color.white;image.sprite=null;image.maskable=false;image.raycastTarget=false;white.SetActive(false);
 var bgFound=content.transform.Find("Intro opaque backplate");var bg=bgFound?bgFound.gameObject:new GameObject("Intro opaque backplate",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));if(!bgFound)bg.transform.SetParent(content.transform,false);bg.layer=5;Stretch((RectTransform)bg.transform);bg.transform.SetAsFirstSibling();var plate=bg.GetComponent<Image>();plate.color=Color.black;plate.maskable=false;plate.raycastTarget=false;bg.SetActive(true);
 var artwork=so.FindProperty("_WorldArtwork");bool contains=false;for(int i=0;i<artwork.arraySize;i++)if(artwork.GetArrayElementAtIndex(i).objectReferenceValue==bg)contains=true;if(!contains){int i=artwork.arraySize;artwork.InsertArrayElementAtIndex(i);artwork.GetArrayElementAtIndex(i).objectReferenceValue=bg;so.ApplyModifiedProperties();}
 var audio=(AudioSource)so.FindProperty("_Audio").objectReferenceValue;LobbyFireplaceSfxBinding.Bind(audio);PrefabUtility.SaveAsPrefabAsset(root,UIPath);
 }finally{PrefabUtility.UnloadPrefabContents(root);}
 root=PrefabUtility.LoadPrefabContents(DialogPath);
 try{var dialog=root.GetComponentInChildren<WorldDialogueCanvas>(true);if(!dialog)throw new Exception("Authored dialogue Canvas missing");var source=Source(dialog.transform,"Authored speaker bubbles",voice,true);var so=new SerializedObject(dialog);so.FindProperty("_Voice").objectReferenceValue=source;so.ApplyModifiedProperties();PrefabUtility.SaveAsPrefabAsset(root,DialogPath);}finally{PrefabUtility.UnloadPrefabContents(root);}
 var prior=SceneManager.GetActiveScene().path;try{
 var scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();var manager=all.Select(x=>x.GetComponent<LobbyManager>()).Single(x=>x);var presentation=all.Select(x=>x.GetComponent<LobbyIntroductionPresentation>()).Single(x=>x);var world=all.Select(x=>x.GetComponent<LobbyIntroductionWorldSequence>()).Single(x=>x);var storm=all.Select(x=>x.GetComponent<LightningEffectManager>()).Single(x=>x);var stormAudio=all.Select(x=>x.GetComponent<LobbyStormAudio>()).Single(x=>x);
 var existing=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="Authored Lobby Introduction UI");if(!existing){existing=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UIPath),scene);existing.name="Authored Lobby Introduction UI";}var authoredUI=existing.GetComponentInChildren<StoryUI>(true);var ps=new SerializedObject(presentation);ps.FindProperty("_AuthoredUI").objectReferenceValue=authoredUI;ps.FindProperty("_Lightning").objectReferenceValue=storm;ps.ApplyModifiedProperties();
 foreach(var screen in all.Select(x=>x.GetComponent<HotelConnectionScreen>()).Where(x=>x)){var cs=new SerializedObject(screen);cs.FindProperty("_Introduction").objectReferenceValue=manager;var overlay=(CanvasGroup)cs.FindProperty("_Overlay").objectReferenceValue;cs.ApplyModifiedProperties();overlay.alpha=1;overlay.blocksRaycasts=true;overlay.interactable=true;var c=overlay.GetComponentInParent<Canvas>(true);c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=32766;var background=overlay.GetComponent<Image>();if(background){var color=background.color;color.a=1;background.color=color;}overlay.gameObject.SetActive(true);}
 var ws=new SerializedObject(world);var actor=(HotelFishSprite)ws.FindProperty("_AuthoredFish").objectReferenceValue;
 if(!actor){var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HotelNetworkPlayer.prefab");var template=player.GetComponentInChildren<HotelFishSprite>(true);if(!template||template.GetComponentsInChildren<HotelPlayer>(true).Length!=0)throw new Exception("Cinematic fish must be visual-only, never duplicate network player");var clone=UnityEngine.Object.Instantiate(template.gameObject);SceneManager.MoveGameObjectToScene(clone,scene);clone.name="Authored cinematic fish";clone.transform.SetParent(world.transform,false);actor=clone.GetComponent<HotelFishSprite>();clone.SetActive(false);}
 var caption=Source(actor.transform,"Authored intro talking",voice,false);ws.FindProperty("_AuthoredFish").objectReferenceValue=actor;ws.FindProperty("_CaptionVoice").objectReferenceValue=caption;ws.FindProperty("_StormAudio").objectReferenceValue=stormAudio;ws.ApplyModifiedProperties();
 var ss=new SerializedObject(stormAudio);foreach(var field in new[]{"_RainSource","_ThunderSource","_ThunderSourceB"}){var source=(AudioSource)ss.FindProperty(field).objectReferenceValue;if(source)LobbyFireplaceSfxBinding.Bind(source);}
  var radio=all.Select(x=>x.GetComponent<LobbyRadioPlayback>()).Single(x=>x);var radioSo=new SerializedObject(radio);var radioSource=(AudioSource)radioSo.FindProperty("_Source").objectReferenceValue;var music=AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/HotelAudio.mixer").OfType<AudioMixerGroup>().Single(x=>AssetDatabase.TryGetGUIDAndLocalFileIdentifier(x,out string guid,out long id)&&guid=="6cc882a789454a5aabbc3a05e5d3ec03"&&id==24300001);radioSource.outputAudioMixerGroup=music;PrefabUtility.RecordPrefabInstancePropertyModifications(radioSource);EditorUtility.SetDirty(radioSource);
 EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Lobby save failed");AssetDatabase.SaveAssets();File.WriteAllText(System.IO.Path.Combine(E,"authored-regression-status.txt"),"Authored UI/fish/voice and independent pixel-scale flash Canvas saved; existing cover/SFX refs reused; actual four-aspect rendered coverage/audio/cold-start/skip/replay/connect acceptance PENDING.");
 }finally{if(!string.IsNullOrEmpty(prior)&&prior!="Assets/Scenes/Lobby.unity")EditorSceneManager.OpenScene(prior);}
 }
 static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;}
 static AudioSource Source(Transform parent,string name,AudioClip clip,bool spatial){var found=parent.Find(name);var go=found?found.gameObject:new GameObject(name);if(!found)go.transform.SetParent(parent,false);var source=go.GetComponent<AudioSource>();if(!source)source=go.AddComponent<AudioSource>();source.clip=clip;source.playOnAwake=false;source.loop=true;source.volume=.3f;source.pitch=1;source.dopplerLevel=0;source.spatialBlend=spatial?1:0;source.minDistance=1.5f;source.maxDistance=18;source.rolloffMode=AudioRolloffMode.Linear;LobbyFireplaceSfxBinding.Bind(source);return source;}
}

