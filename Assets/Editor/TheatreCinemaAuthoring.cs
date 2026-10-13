#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HauntedFish.Theatre;
// STAGED ONLY. Explicit clean owned editor invocation, supplied imported kit asset; no guessed paths.
public static class TheatreCinemaAuthoring
{
 public static void Apply(TheatrePresentation theatre,GameObject splitThreeRow,Material filmMaterial,GameObject optionalFiveRow=null)
 {
  if(!theatre||!splitThreeRow||!filmMaterial)throw new Exception("Supply actual imported chair row and authored CinemaFilm material.");
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new Exception("Confirmed exclusive idle editor required.");
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve user dirty scenes before authoring.");
  var scene=theatre.gameObject.scene;if(scene.path!="Assets/Scenes/theatre.unity")throw new Exception("Author only the owned theatre scene.");
  var root=theatre.transform;var serialized=new SerializedObject(theatre);var camera=(Camera)serialized.FindProperty("_Camera").objectReferenceValue;var video=(VideoPlayer)serialized.FindProperty("_Video").objectReferenceValue;var walker=(Transform)serialized.FindProperty("_Walker").objectReferenceValue;var before=walker.localPosition;
  if(!camera||!camera.orthographic||!video||!video.targetTexture)throw new Exception("Existing authored camera/video/RenderTexture missing.");
  foreach(var canvas in root.GetComponentsInChildren<Canvas>(true))canvas.enabled=false;
  foreach(var raycaster in root.GetComponentsInChildren<GraphicRaycaster>(true))raycaster.enabled=false;
  // Film is a world mesh, not a UI canvas. Keep the original authored video target identity.
  if(root.Find("Cinema film - world projection"))throw new Exception("Cinema geometry already authored; inspect rather than duplicating.");
  var film=GameObject.CreatePrimitive(PrimitiveType.Quad);film.name="Cinema film - world projection";film.transform.SetParent(root,false);film.transform.localPosition=new Vector3(0,10,-10);film.transform.localScale=new Vector3(35.55556f,20,1);UnityEngine.Object.DestroyImmediate(film.GetComponent<Collider>());film.GetComponent<Renderer>().sharedMaterial=filmMaterial;filmMaterial.SetTexture("_MainTex",video.targetTexture);
  var seats=new GameObject("SEATING - authored cloud chairs, no collision");seats.transform.SetParent(root,false);PlaceRow(splitThreeRow,seats.transform,new Vector3(0,.70f,-26),2.5f,5.670f);
  if(optionalFiveRow)PlaceRow(optionalFiveRow,seats.transform,new Vector3(0,.35f,-23.6f),2,3.570f);
  var own=(SpriteRenderer)serialized.FindProperty("_OwnSilhouette").objectReferenceValue;if(own){var p=own.transform.localPosition;p.y=-.3f;own.transform.localPosition=p;own.color=Color.black;}
  // Modest physical spill illuminates actual chair materials without changing the pure black silhouettes.
  var spill=new GameObject("SCREEN LIGHT - quiet film spill");spill.transform.SetParent(root,false);spill.transform.localPosition=new Vector3(0,9,-10.2f);spill.transform.localRotation=Quaternion.LookRotation(Vector3.back);var light=spill.AddComponent<Light>();light.type=LightType.Spot;light.spotAngle=115;light.range=18;light.intensity=.15f;light.color=new Color(.55f,.7f,.6f);light.shadows=LightShadows.None;
  if(walker.localPosition!=before)throw new Exception("Owned A/D lane changed");
  EditorSceneManager.MarkSceneDirty(scene); // Save/verification remains explicit; no scene or prefab auto-save here.
 }
 static void PlaceRow(GameObject source,Transform parent,Vector3 position,float scale,float expectedWidth)
 {
  var go=(GameObject)PrefabUtility.InstantiatePrefab(source);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.identity;
  if(go.GetComponentsInChildren<Collider>(true).Length>0||go.GetComponentsInChildren<Rigidbody>(true).Length>0)throw new Exception("Cloud chair kit must be decorative, collider-free.");
  var renderers=go.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)throw new Exception("Imported kit has no chair meshes");var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);if(Mathf.Abs(bounds.size.x-expectedWidth)>.04f)throw new Exception("Imported row units/pivot need verification: width="+bounds.size.x+" expected="+expectedWidth);
  go.transform.localScale=Vector3.one*scale;go.name=source.name+" - authored scale "+scale;
 }
}
#endif
