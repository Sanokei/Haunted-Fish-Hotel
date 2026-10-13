#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class TheatreScreenIntegration
{
 public static void Run()
 {
  if(!Application.isBatchMode||!Environment.GetCommandLineArgs().Contains("--theatre-native-validation"))throw new Exception("Explicit owned batch only");
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/theatre.unity",OpenSceneMode.Single);var root=scene.GetRootGameObjects().Single();
  var frame=root.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Both-role tutorial screen");
  var old=frame.GetComponent<RawImage>();if(old){var texture=old.texture;UnityEngine.Object.DestroyImmediate(frame.GetComponent<AspectRatioFitter>());UnityEngine.Object.DestroyImmediate(old);var go=new GameObject("Aspect fitted video inside authored screen",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));var rect=go.GetComponent<RectTransform>();rect.SetParent(frame,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;var raw=go.GetComponent<RawImage>();raw.texture=texture;raw.raycastTarget=false;var fit=go.GetComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=16f/9;}
  frame.anchorMin=new Vector2(.06f,.05f);frame.anchorMax=new Vector2(.94f,.89f);frame.offsetMin=frame.offsetMax=Vector2.zero;
  var surround=root.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Screen surround");surround.anchorMin=new Vector2(.045f,.035f);surround.GetComponent<Image>().color=new Color(.18f,.17f,.19f);
  root.GetComponentInChildren<Camera>().backgroundColor=new Color(.09f,.08f,.10f);
  PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Theatre/TheatrePresentation.prefab");EditorSceneManager.SaveScene(scene);TheatreNativeValidation.Run();
 }
}
#endif