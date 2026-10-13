using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using UnityEngine.UI;
using TMPro;
using UnityEditor.Events;
public static class LobbyAtmosphereAuthoringTask
{
 const string E="C:/Users/wkeif/Documents/Codex/2026-10-09/task/LobbyAtmosphereEvidence";
 const string A="Assets/Prefabs/LobbyAtmosphere";
 
 static bool FinalizeSavedBindings(){if(!LobbyStormRepairTask.RequireSafeEditMode())return false;
 Scene scene=default;bool opened=false;
 try{
 scene=SceneManager.GetSceneByPath("Assets/Scenes/Lobby.unity");if(!scene.isLoaded){if(!LobbyStormRepairTask.RequireSafeEditMode())return false;scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity",OpenSceneMode.Additive);opened=true;}if(scene.isDirty)return false;
 var roots=scene.GetRootGameObjects();var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(File.ReadAllText(E+"/atmosphere-owner-profile.txt").Trim());if(!profile)throw new Exception("Validated profile missing");
 var global=roots.SelectMany(x=>x.GetComponentsInChildren<Volume>(true)).Single(x=>x.name=="Global Volume");
 var camera=roots.SelectMany(x=>x.GetComponentsInChildren<Camera>(true)).Single(x=>x.name=="Main Camera");
 var data=camera.GetComponent<UniversalAdditionalCameraData>();
 var world=roots.SelectMany(x=>x.GetComponentsInChildren<LobbyIntroductionWorldSequence>(true)).Single();
 var controller=roots.SelectMany(x=>x.GetComponentsInChildren<LobbyIntroAtmosphere>(true)).Single();
 var lightning=roots.SelectMany(x=>x.GetComponentsInChildren<LightningEffectManager>(true)).Single();
 if(!lightning.ValidateAuthoredBindings(out var error))throw new Exception("Storm repair regressed: "+error);
 if(!File.Exists(E+"/Lobby_before_global_grade_finalization.unity"))File.Copy("Assets/Scenes/Lobby.unity",E+"/Lobby_before_global_grade_finalization.unity",false);
 Undo.RecordObject(global,"Bind validated full Lobby profile");global.sharedProfile=profile;global.weight=1;EditorUtility.SetDirty(global);
 Undo.RecordObject(data,"Enable authored Lobby grade");data.renderPostProcessing=true;EditorUtility.SetDirty(data);
 Ref(world,"_Atmosphere",controller);Ref(controller,"_Camera",camera);PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
 var enclosure=roots.Single(x=>x.name=="Lobby stairwell enclosure");
 foreach(var t in enclosure.GetComponentsInChildren<Transform>()){if(t.name=="Rear stairwell plaster return"||t.name=="Outer stairwell plaster return"){Undo.RecordObject(t,"Close physical gap above stairs");var position=t.position;position.y=2.25f;t.position=position;var scale=t.localScale;scale.y=4.5f;t.localScale=scale;PrefabUtility.RecordPrefabInstancePropertyModifications(t);}}
  GameObject verificationCamera=null;
 try{
 verificationCamera=new GameObject("Temporary final Lobby verification camera");SceneManager.MoveGameObjectToScene(verificationCamera,scene);
 var view=verificationCamera.AddComponent<Camera>();view.CopyFrom(camera);view.enabled=false;
 var viewData=verificationCamera.AddComponent<UniversalAdditionalCameraData>();viewData.renderPostProcessing=data.renderPostProcessing;viewData.volumeLayerMask=data.volumeLayerMask;viewData.volumeTrigger=view.transform;
 var ws=new SerializedObject(world);var entry=(Transform)ws.FindProperty("_EntryCamera").objectReferenceValue;var reception=(Transform)ws.FindProperty("_ReceptionCamera").objectReferenceValue;
 foreach(var pair in new[]{("09-final-entry.png",0f),("10-final-mid.png",.5f),("11-final-reception.png",1f)}){view.transform.position=Vector3.Lerp(entry.position,reception.position,pair.Item2);view.transform.rotation=Quaternion.Slerp(entry.rotation,reception.rotation,pair.Item2);Capture(view,E+"/"+pair.Item1);}
 File.WriteAllLines(E+"/final-lighting-native.txt",roots.SelectMany(x=>x.GetComponentsInChildren<Light>(true)).Select(x=>x.name+" enabled="+x.enabled+" active="+x.gameObject.activeInHierarchy+" type="+x.type+" color="+x.color+" intensity="+x.intensity+" range="+x.range+" position="+x.transform.position));
 }finally{if(verificationCamera)UnityEngine.Object.DestroyImmediate(verificationCamera);}
 if(!LobbyStormRepairTask.RequireSafeEditMode(false))throw new InvalidOperationException("Editor state changed; scene save cancelled.");EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(scene))throw new Exception("Full Lobby grade binding save failed");
 File.WriteAllText(E+"/global-grade-saved.txt","PASS saved actual Lobby: existing Global Volume sharedProfile="+AssetDatabase.GetAssetPath(global.sharedProfile)+" weight="+global.weight+" Main Camera post-processing="+data.renderPostProcessing+"; existing intro controller and camera bound; rear/outer stair return height4.5; storm authored bindings still valid. No duplicate roots.");
 return true;
 }catch(Exception ex){File.WriteAllText(E+"/global-grade-error.txt",ex.ToString());return true;}finally{if(opened&&scene.isLoaded&&!scene.isDirty&&!EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)EditorSceneManager.CloseScene(scene,true);}
 }
 static void Ref(UnityEngine.Object target,string field,UnityEngine.Object value){var so=new SerializedObject(target);var p=so.FindProperty(field);if(p==null)throw new Exception("Missing field "+field);p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
 static void Box(Transform root,string name,Vector3 position,Vector3 scale,Material material){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root,false);g.transform.position=position;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;g.isStatic=true;}
 static void Capture(Camera camera,string path){var rt=new RenderTexture(1600,1000,24);rt.Create();var old=camera.targetTexture;var active=RenderTexture.active;try{camera.targetTexture=rt;var request=new RenderPipeline.StandardRequest{destination=rt};if(RenderPipeline.SupportsRenderRequest(camera,request))RenderPipeline.SubmitRenderRequest(camera,request);else camera.Render();RenderTexture.active=rt;var t=new Texture2D(1600,1000,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1600,1000),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);}finally{camera.targetTexture=old;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}}
 static void AuthorSkip(){
 const string path="Assets/Resources/LobbyIntroductionUI.prefab";
 if(!File.Exists(E+"/LobbyIntroductionUI_before_skip.prefab"))File.Copy(path,E+"/LobbyIntroductionUI_before_skip.prefab",false);
 var root=PrefabUtility.LoadPrefabContents(path);
 try{
 var ui=root.GetComponent<StoryUI>();var so=new SerializedObject(ui);var content=(GameObject)so.FindProperty("_Content").objectReferenceValue;
 var hint=content.transform.Cast<Transform>().Single(x=>x.name=="Skip hint").gameObject;var progress=content.GetComponentsInChildren<IntroductionSkipCircle>(true).Single();
 var label=hint.GetComponent<TMP_Text>();label.text="CLICK TO SKIP\nor hold SPACE";label.raycastTarget=true;label.color=new Color(.92549f,.99608f,.79216f,1);
 var button=hint.GetComponent<Button>()??hint.AddComponent<Button>();button.targetGraphic=label;button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.RemoveAllListeners();while(button.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(button.onClick,0);UnityEventTools.AddPersistentListener(button.onClick,ui.RequestSkip);
 if(!content.GetComponent<GraphicRaycaster>())content.AddComponent<GraphicRaycaster>();
 content.transform.localScale=Vector3.one;var canvas=content.GetComponent<Canvas>();canvas.sortingOrder=32761;
 var art=content.transform.Cast<Transform>().Where(t=>t.gameObject!=hint&&t!=progress.transform).Select(t=>t.gameObject).ToArray();var array=so.FindProperty("_WorldArtwork");array.arraySize=art.Length;for(int i=0;i<art.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=art[i];so.ApplyModifiedPropertiesWithoutUndo();
 PrefabUtility.SaveAsPrefabAsset(root,path);
 var test=UnityEngine.Object.Instantiate(root);try{var testUI=test.GetComponent<StoryUI>();testUI.Prepare();testUI.PrepareWorld();var ts=new SerializedObject(testUI);var circle=(IntroductionSkipCircle)ts.FindProperty("_SkipProgress").objectReferenceValue;if(!circle.gameObject.activeInHierarchy)throw new Exception("Skip hidden in world mode");if(test.GetComponentsInChildren<Button>().Length!=1)throw new Exception("Authored clickable Skip missing");int requests=0;testUI.SkipRequested+=()=>requests++;var testClick=test.GetComponentInChildren<Button>().onClick;testClick.SetPersistentListenerState(0,UnityEngine.Events.UnityEventCallState.EditorAndRuntime);testClick.Invoke();if(requests!=1)throw new Exception("Skip click did not route exactly once");testUI.Close();testUI.Prepare();testUI.PrepareWorld();if(!circle.gameObject.activeInHierarchy)throw new Exception("Skip replay hidden");File.WriteAllText(E+"/skip-prefab-validation.txt","PASS authored Skip label/button and progress remain active in world mode; prefab click raises one SkipRequested; close/reprepare/world replay visible. Canvas32761, GraphicRaycaster; existing EventSystem/InputSystem handles click. Live entry/walk/dolly/caption cancellation not tested.");}finally{UnityEngine.Object.DestroyImmediate(test);}
 }finally{PrefabUtility.UnloadPrefabContents(root);}
 }
 static void AuthorLightning(LightningEffectManager lightning,Scene scene){
 if(lightning.ValidateAuthoredBindings(out var existingError))return;
 var so=new SerializedObject(lightning);var windows=so.FindProperty("_Windows");var receivers=so.FindProperty("_ReceiverRoots");var materialPath="Assets/Prefabs/Storm/WindowLightningAuthored.mat";
 var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(!material){material=new Material(Shader.Find("HauntedFish/WindowLightning"));AssetDatabase.CreateAsset(material,materialPath);}
 var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Storm/StormLightningHallway.prefab");var group=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);Undo.RegisterCreatedObjectUndo(group,"Authored lobby window passes");group.name="Authored lobby lightning visuals";
 var template=group.GetComponent<LightningEffectManager>();var ts=new SerializedObject(template);var flash=(Canvas)ts.FindProperty("_FlashCanvas").objectReferenceValue;Undo.DestroyObjectImmediate(template);
 var sources=new System.Collections.Generic.HashSet<MeshRenderer>();for(int i=0;i<receivers.arraySize;i++){var root=(Transform)receivers.GetArrayElementAtIndex(i).objectReferenceValue;if(root)foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))if(renderer.GetComponent<MeshFilter>()&&renderer.GetComponent<MeshFilter>().sharedMesh)sources.Add(renderer);}
 var bindings=new System.Collections.Generic.List<LightningEffectManager.WindowPass>();
 for(int w=0;w<windows.arraySize;w++){var window=(SpriteRenderer)windows.GetArrayElementAtIndex(w).objectReferenceValue;if(!window||!window.sprite)continue;foreach(var source in sources){
 var passGO=new GameObject("Authored window pass - "+window.name+" - "+source.name);SceneManager.MoveGameObjectToScene(passGO,scene);passGO.transform.SetParent(group.transform,false);passGO.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);passGO.transform.localScale=source.transform.lossyScale;passGO.layer=source.gameObject.layer;var mesh=passGO.AddComponent<MeshFilter>();mesh.sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;var pass=passGO.AddComponent<MeshRenderer>();pass.sharedMaterials=Enumerable.Repeat(material,mesh.sharedMesh.subMeshCount).ToArray();pass.enabled=false;pass.shadowCastingMode=ShadowCastingMode.Off;pass.receiveShadows=false;pass.lightProbeUsage=LightProbeUsage.Off;pass.reflectionProbeUsage=ReflectionProbeUsage.Off;bindings.Add(new LightningEffectManager.WindowPass{Window=window,Pass=pass});}}
 if(bindings.Count==0)throw new Exception("No authored Lobby window pass bindings");
 group.SetActive(true);PrefabUtility.SaveAsPrefabAssetAndConnect(group,A+"/LobbyWindowStormVisuals.prefab",InteractionMode.AutomatedAction);
 so.Update();var array=so.FindProperty("_AuthoredWindowPasses");array.arraySize=bindings.Count;for(int i=0;i<bindings.Count;i++){var item=array.GetArrayElementAtIndex(i);item.FindPropertyRelative("Window").objectReferenceValue=bindings[i].Window;item.FindPropertyRelative("Pass").objectReferenceValue=bindings[i].Pass;}so.FindProperty("_FlashCanvas").objectReferenceValue=flash;so.ApplyModifiedPropertiesWithoutUndo();
 if(!lightning.ValidateAuthoredBindings(out var error))throw new Exception(error);
 File.WriteAllText(E+"/lightning-authored.txt","PASS authored "+bindings.Count+" window receiver passes, serialized white-frame Canvas, shared additive material; original window masks and strike owner retained. No runtime object construction.");
 }
 [MenuItem("Tools/Haunted Fish/Finalize Authored Lobby Atmosphere")]
 public static void Run(){if(!LobbyStormRepairTask.RequireSafeEditMode())return;if(File.Exists(E+"/saved.txt")){FinalizeSavedBindings();return;}Scene scene=default;bool opened=false;GameObject cameraGO=null;int undo=-1;try{
 Directory.CreateDirectory(E);
 scene=SceneManager.GetSceneByPath("Assets/Scenes/Lobby.unity");if(!scene.isLoaded){if(!LobbyStormRepairTask.RequireSafeEditMode())return;scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity",OpenSceneMode.Additive);opened=true;}
 if(scene.isDirty){File.WriteAllText(E+"/status.txt","Waiting: Lobby contains unsaved changes; no writes.");return;}
 var roots=scene.GetRootGameObjects();var world=roots.SelectMany(x=>x.GetComponentsInChildren<LobbyIntroductionWorldSequence>(true)).Single();if(new SerializedObject(world).FindProperty("_Atmosphere")==null)throw new Exception("Intro atmosphere hook not compiled yet");
 var shell=roots.Single(x=>x.name=="Haunted Fish Hotel Lobby").GetComponentsInChildren<Renderer>().First(x=>x.name.StartsWith("Wall shell"));
 var stairs=roots.SelectMany(x=>x.GetComponentsInChildren<MeshRenderer>()).Where(x=>x.name.Contains("tread")).ToArray();File.WriteAllLines(E+"/stair-bounds.txt",stairs.Select(x=>x.name+" "+x.bounds));
 var camera=roots.SelectMany(x=>x.GetComponentsInChildren<Camera>(true)).First(x=>x.name=="Main Camera");
 var w=new SerializedObject(world);var entry=(Transform)w.FindProperty("_EntryCamera").objectReferenceValue;var reception=(Transform)w.FindProperty("_ReceptionCamera").objectReferenceValue;
 cameraGO=new GameObject("Temporary dolly visual verification");SceneManager.MoveGameObjectToScene(cameraGO,scene);var close=cameraGO.AddComponent<Camera>();close.CopyFrom(camera);close.enabled=false;close.nearClipPlane=.05f;close.farClipPlane=100;
 var originalData=camera.GetComponent<UniversalAdditionalCameraData>();var cd=cameraGO.AddComponent<UniversalAdditionalCameraData>();cd.renderPostProcessing=true;cd.volumeLayerMask=originalData.volumeLayerMask;cd.volumeTrigger=close.transform;
 close.transform.SetPositionAndRotation(entry.position,entry.rotation);Capture(close,E+"/01-entry-before.png");
 close.transform.position=Vector3.Lerp(entry.position,reception.position,.5f);close.transform.rotation=Quaternion.Slerp(entry.rotation,reception.rotation,.5f);Capture(close,E+"/02-dolly-before.png");
 if(!File.Exists(E+"/Lobby_before_atmosphere.unity"))File.Copy(Application.dataPath+"/Scenes/Lobby.unity",E+"/Lobby_before_atmosphere.unity",false);
 AuthorSkip();
 if(!LobbyStormRepairTask.RequireSafeEditMode())return;Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Author lobby intro atmosphere, stair enclosure and indoor storm acoustic treatment");
 Directory.CreateDirectory(A);AssetDatabase.Refresh();
 var lightOwner=roots.SelectMany(x=>x.GetComponentsInChildren<LightningEffectManager>(true)).Single();AuthorLightning(lightOwner,scene);
 var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(File.ReadAllText(E+"/atmosphere-owner-profile.txt").Trim());if(!profile)throw new Exception("Post-processing owner profile is not a valid imported VolumeProfile");
 var global=roots.SelectMany(x=>x.GetComponentsInChildren<Volume>(true)).Single(x=>x.name=="Global Volume");Undo.RecordObject(global,"Bind validated Lobby mood profile");global.sharedProfile=profile;global.weight=1;EditorUtility.SetDirty(global);Undo.RecordObject(originalData,"Enable authored Lobby post-processing");originalData.renderPostProcessing=true;EditorUtility.SetDirty(originalData);
 var atmosphere=new GameObject("Lobby dolly atmosphere");SceneManager.MoveGameObjectToScene(atmosphere,scene);Undo.RegisterCreatedObjectUndo(atmosphere,"Authored intro Volume");
 var volume=atmosphere.AddComponent<Volume>();volume.isGlobal=true;volume.priority=50;volume.weight=0;volume.sharedProfile=profile;
 var controller=atmosphere.AddComponent<LobbyIntroAtmosphere>();Ref(controller,"_Volume",volume);
 PrefabUtility.SaveAsPrefabAssetAndConnect(atmosphere,A+"/LobbyDollyAtmosphere.prefab",InteractionMode.AutomatedAction);
 Ref(controller,"_Camera",camera);PrefabUtility.RecordPrefabInstancePropertyModifications(controller);Ref(world,"_Atmosphere",controller);
 var enclosure=new GameObject("Lobby stairwell enclosure");SceneManager.MoveGameObjectToScene(enclosure,scene);Undo.RegisterCreatedObjectUndo(enclosure,"Authored stairwell closure");
 // Panels stand beyond the existing treads and curved shell; no outward wall is removed.
 Box(enclosure.transform,"Rear stairwell plaster return",new Vector3(-6.85f,2.25f,13.2f),new Vector3(6.1f,4.5f,.24f),shell.sharedMaterial);
 Box(enclosure.transform,"Outer stairwell plaster return",new Vector3(-10.0f,2.25f,9.4f),new Vector3(.24f,4.5f,7.8f),shell.sharedMaterial);
 Box(enclosure.transform,"Stairwell lower floor closure",new Vector3(-6.9f,-.13f,11.1f),new Vector3(6.2f,.24f,4.2f),roots.Single(x=>x.name=="Haunted Fish Hotel Lobby").GetComponentsInChildren<Renderer>().First(x=>x.name.StartsWith("Floor |")).sharedMaterial);
 PrefabUtility.SaveAsPrefabAssetAndConnect(enclosure,A+"/LobbyStairwellEnclosure.prefab",InteractionMode.AutomatedAction);
 var storm=roots.SelectMany(x=>x.GetComponentsInChildren<LobbyStormAudio>(true)).Single();var so=new SerializedObject(storm);
 var rain=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Storm/IndoorTransmission/HFH_Rain_Indoor_Mix_Loop_60s_Muffled.wav");if(!rain)throw new Exception("Muffled rain not imported");so.FindProperty("_RainLoop").objectReferenceValue=rain;
 var names=new[]{"HFH_Thunder_Medium_Roll","HFH_Thunder_Soft_Crack","HFH_Thunder_Distant_Long","HFH_Thunder_Far_Rumble"};var clips=so.FindProperty("_ThunderClips");for(int i=0;i<names.Length;i++){var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Storm/IndoorTransmission/"+names[i]+"_Muffled.wav");if(!clip)throw new Exception("Muffled thunder not imported");clips.GetArrayElementAtIndex(i).objectReferenceValue=clip;}
 so.FindProperty("_RainGain").floatValue=.72f;so.FindProperty("_ThunderGain").floatValue=.85f;so.FindProperty("_RainDuringDialogue").floatValue=.45f;so.FindProperty("_ThunderDuringDialogue").floatValue=.6f;so.ApplyModifiedPropertiesWithoutUndo();
 var lightning=so.FindProperty("_Lightning").objectReferenceValue;PrefabUtility.SaveAsPrefabAssetAndConnect(storm.gameObject,A+"/LobbyIndoorStormAudio.prefab",InteractionMode.AutomatedAction);Ref(storm,"_Lightning",lightning);PrefabUtility.RecordPrefabInstancePropertyModifications(storm);
 // Direct lifecycle assertions use the authored controller, without launching gameplay.
 float oldWeight=volume.weight;bool oldPP=originalData.renderPostProcessing;controller.Begin();if(volume.weight!=1||!originalData.renderPostProcessing)throw new Exception("Begin atmosphere failed");controller.Begin();controller.End();controller.End();if(volume.weight!=oldWeight||originalData.renderPostProcessing!=oldPP)throw new Exception("Atmosphere restoration failed");
 controller.Begin();world.Close();if(volume.weight!=oldWeight||originalData.renderPostProcessing!=oldPP)throw new Exception("World Close atmosphere restoration failed");
 volume.weight=1;
 foreach(var pair in new[]{("03-entry-after.png",0f),("04-dolly-after.png",.5f),("05-reception-after.png",1f)}){close.transform.position=Vector3.Lerp(entry.position,reception.position,pair.Item2);close.transform.rotation=Quaternion.Slerp(entry.rotation,reception.rotation,pair.Item2);Capture(close,E+"/"+pair.Item1);}
 // Bright view proves geometry closes sightlines independently of cinematic darkening.
 volume.weight=0;close.transform.position=entry.position;close.transform.LookAt(new Vector3(-6.9f,2.2f,11.4f));close.fieldOfView=52;Capture(close,E+"/06-stair-enclosure-neutral.png");
 UnityEngine.Object.DestroyImmediate(cameraGO);cameraGO=null;volume.weight=oldWeight;EditorUtility.SetDirty(world);
 AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Lobby save failed");
 File.WriteAllText(E+"/saved.txt","PASS saved authored atmosphere Volume/profile and enclosure prefabs; storm prefab uses Web-compatible muffled/reflection-baked clips. Direct Begin/End idempotence and World.Close restoration passed. Original camera PP and Volume weight restored. Dolly entry/mid/exit captured; visual inspection required. Normal completion/skip/error route through existing Close. Runtime skip/playback and human audio audition NOT tested.");
 
 }catch(Exception ex){if(undo>=0&&LobbyStormRepairTask.RequireSafeEditMode(false))Undo.RevertAllDownToGroup(undo);File.WriteAllText(E+"/error.txt",ex.ToString());}finally{if(cameraGO)UnityEngine.Object.DestroyImmediate(cameraGO);if(opened&&scene.isLoaded&&!scene.isDirty&&!EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)EditorSceneManager.CloseScene(scene,true);}}
}

